using System.Text.Json;
using System.Text.RegularExpressions;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.Preferences.Common;
using CLARIHR.Domain.Preferences;
using FluentValidation;

namespace CLARIHR.Application.Features.Preferences.User;

public sealed record UserPreferenceResponse(
    Guid Id,
    string Language,
    IReadOnlyCollection<UserSocialLinkResponse> SocialLinks,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record UserSocialLinkResponse(string ProviderCode, string Url);

public sealed record GetCurrentUserPreferencesQuery : IQuery<UserPreferenceResponse>;

public sealed record UpdateCurrentUserPreferencesCommand(
    string Language,
    Guid ConcurrencyToken) : ICommand<UserPreferenceResponse>;

public sealed record ReplaceCurrentUserSocialLinksCommand(
    IReadOnlyCollection<UpdateCurrentUserSocialLinkItem> Items,
    Guid ConcurrencyToken) : ICommand<UserPreferenceResponse>;

public sealed record UpdateCurrentUserSocialLinkItem(string ProviderCode, string Url);

public sealed record UserPreferencePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchCurrentUserPreferencesCommand(
    Guid ConcurrencyToken,
    IReadOnlyCollection<UserPreferencePatchOperation> Operations) : ICommand<UserPreferenceResponse>;

internal sealed class UpdateCurrentUserPreferencesCommandValidator : AbstractValidator<UpdateCurrentUserPreferencesCommand>
{
    public UpdateCurrentUserPreferencesCommandValidator()
    {
        RuleFor(command => command.Language)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .WithMessage("Language format is invalid.");
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ReplaceCurrentUserSocialLinksCommandValidator : AbstractValidator<ReplaceCurrentUserSocialLinksCommand>
{
    public ReplaceCurrentUserSocialLinksCommandValidator()
    {
        RuleFor(command => command.Items)
            .NotNull()
            .Must(static items => items.Count <= 10)
            .WithMessage("A maximum of 10 social links is allowed.");

        RuleForEach(command => command.Items)
            .ChildRules(item =>
            {
                item.RuleFor(link => link.ProviderCode)
                    .NotEmpty()
                    .MaximumLength(50)
                    .Matches("^[A-Za-z0-9_.-]+$")
                    .WithMessage("Provider code format is invalid.");

                item.RuleFor(link => link.Url)
                    .NotEmpty()
                    .MaximumLength(500)
                    .Must(BeAbsoluteHttpsUrl)
                    .WithMessage("Url must be an absolute https URL.");
            });

        RuleFor(command => command.Items)
            .Must(HaveUniqueProviderCodes)
            .WithMessage("Provider codes must be unique.");

        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }

    private static bool HaveUniqueProviderCodes(IReadOnlyCollection<UpdateCurrentUserSocialLinkItem> items) =>
        items
            .Select(static item => item.ProviderCode.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .Count() == items.Count;

    private static bool BeAbsoluteHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsedUri) &&
        string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}

internal sealed class PatchCurrentUserPreferencesCommandValidator : AbstractValidator<PatchCurrentUserPreferencesCommand>
{
    public PatchCurrentUserPreferencesCommandValidator()
    {
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class GetCurrentUserPreferencesQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IUserPreferenceRepository userPreferenceRepository,
    IUnitOfWork unitOfWork)
    : IQueryHandler<GetCurrentUserPreferencesQuery, UserPreferenceResponse>
{
    public async Task<Result<UserPreferenceResponse>> Handle(
        GetCurrentUserPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await UserPreferenceAdministrationHelpers.ResolveCurrentUserAsync(currentUserService, userRepository, cancellationToken);
        if (currentUserResult.IsFailure)
        {
            return Result<UserPreferenceResponse>.Failure(currentUserResult.Error);
        }

        var preference = await userPreferenceRepository.GetByUserIdAsync(currentUserResult.Value.Id, cancellationToken);
        if (preference is null)
        {
            preference = UserPreference.Create(currentUserResult.Value.Id);
            userPreferenceRepository.Add(preference);
            try
            {
                _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueConstraintViolationException exception)
                when (UserPreferenceConstraintViolations.IsUserConflict(exception.ConstraintName))
            {
                // UP-A: a concurrent first access provisioned the singleton first; the unique (user_id)
                // index rejected this insert. Re-read the row the winning request committed and return it
                // — a GET must not surface the first-access race as a 500.
                preference = await userPreferenceRepository.GetByUserIdAsync(currentUserResult.Value.Id, cancellationToken);
                if (preference is null)
                {
                    throw;
                }
            }
        }

        return Result<UserPreferenceResponse>.Success(UserPreferenceAdministrationHelpers.Map(preference));
    }
}

internal sealed class ReplaceCurrentUserSocialLinksCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IUserPreferenceRepository userPreferenceRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReplaceCurrentUserSocialLinksCommand, UserPreferenceResponse>
{
    public async Task<Result<UserPreferenceResponse>> Handle(
        ReplaceCurrentUserSocialLinksCommand command,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await UserPreferenceAdministrationHelpers.ResolveCurrentUserAsync(currentUserService, userRepository, cancellationToken);
        if (currentUserResult.IsFailure)
        {
            return Result<UserPreferenceResponse>.Failure(currentUserResult.Error);
        }

        var preference = await userPreferenceRepository.GetByUserIdAsync(currentUserResult.Value.Id, cancellationToken);
        if (preference is null)
        {
            // First write auto-provisions the singleton; there is no prior token to conflict with,
            // so the supplied If-Match is intentionally ignored on this branch.
            preference = UserPreference.Create(currentUserResult.Value.Id);
            userPreferenceRepository.Add(preference);
        }
        else if (preference.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<UserPreferenceResponse>.Failure(PreferenceErrors.ConcurrencyConflict);
        }

        preference.ReplaceSocialLinks(command.Items.Select(static item => new UserSocialLinkInput(item.ProviderCode, item.Url)));

        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception)
            when (UserPreferenceConstraintViolations.IsUserConflict(exception.ConstraintName))
        {
            // UP-A: a concurrent first write provisioned the singleton first (only the auto-provision
            // INSERT can trip the unique (user_id) index — the existing-preference path is an UPDATE).
            // Surface a retryable conflict; re-applying here is unsafe (the failed insert stays Added).
            return Result<UserPreferenceResponse>.Failure(PreferenceErrors.ConcurrencyConflict);
        }

        return Result<UserPreferenceResponse>.Success(UserPreferenceAdministrationHelpers.Map(preference));
    }
}

internal sealed class UpdateCurrentUserPreferencesCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IUserPreferenceRepository userPreferenceRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCurrentUserPreferencesCommand, UserPreferenceResponse>
{
    public async Task<Result<UserPreferenceResponse>> Handle(
        UpdateCurrentUserPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await UserPreferenceAdministrationHelpers.ResolveCurrentUserAsync(currentUserService, userRepository, cancellationToken);
        if (currentUserResult.IsFailure)
        {
            return Result<UserPreferenceResponse>.Failure(currentUserResult.Error);
        }

        var preference = await userPreferenceRepository.GetByUserIdAsync(currentUserResult.Value.Id, cancellationToken);
        if (preference is null)
        {
            // First write auto-provisions the singleton; there is no prior token to conflict with,
            // so the supplied If-Match is intentionally ignored on this branch.
            preference = UserPreference.Create(currentUserResult.Value.Id, command.Language);
            userPreferenceRepository.Add(preference);
        }
        else if (preference.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<UserPreferenceResponse>.Failure(PreferenceErrors.ConcurrencyConflict);
        }
        else
        {
            preference.UpdateLanguage(command.Language);
        }

        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception)
            when (UserPreferenceConstraintViolations.IsUserConflict(exception.ConstraintName))
        {
            // UP-A: a concurrent first write provisioned the singleton first (only the auto-provision
            // INSERT can trip the unique (user_id) index — the existing-preference path is an UPDATE).
            // Surface a retryable conflict; re-applying here is unsafe (the failed insert stays Added).
            return Result<UserPreferenceResponse>.Failure(PreferenceErrors.ConcurrencyConflict);
        }

        return Result<UserPreferenceResponse>.Success(UserPreferenceAdministrationHelpers.Map(preference));
    }
}

internal sealed class PatchCurrentUserPreferencesCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IUserPreferenceRepository userPreferenceRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchCurrentUserPreferencesCommand, UserPreferenceResponse>
{
    public async Task<Result<UserPreferenceResponse>> Handle(
        PatchCurrentUserPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await UserPreferenceAdministrationHelpers.ResolveCurrentUserAsync(currentUserService, userRepository, cancellationToken);
        if (currentUserResult.IsFailure)
        {
            return Result<UserPreferenceResponse>.Failure(currentUserResult.Error);
        }

        var preference = await userPreferenceRepository.GetByUserIdAsync(currentUserResult.Value.Id, cancellationToken);
        var isNewlyProvisioned = preference is null;
        if (preference is null)
        {
            // First write auto-provisions the singleton; there is no prior token to conflict with,
            // so the supplied If-Match is intentionally ignored on this branch.
            preference = UserPreference.Create(currentUserResult.Value.Id);
            userPreferenceRepository.Add(preference);
        }
        else if (preference.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<UserPreferenceResponse>.Failure(PreferenceErrors.ConcurrencyConflict);
        }

        var state = UserPreferencePatchState.From(preference);

        var applied = UserPreferencePatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<UserPreferenceResponse>.Failure(applied.Error);
        }

        var validation = UserPreferencePatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<UserPreferenceResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation && !isNewlyProvisioned)
        {
            return Result<UserPreferenceResponse>.Success(UserPreferenceAdministrationHelpers.Map(preference));
        }

        // Scalar-only patch: language is the entity's only patchable field and is re-normalized +
        // re-validated by UpdateLanguage exactly as the PUT path does. The applier already enforced
        // the same format rule (^[A-Za-z]{2,3}$) before we get here.
        if (state.HasMutation)
        {
            preference.UpdateLanguage(state.Language);
        }

        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception)
            when (UserPreferenceConstraintViolations.IsUserConflict(exception.ConstraintName))
        {
            // UP-A: a concurrent first write provisioned the singleton first (only the auto-provision
            // INSERT can trip the unique (user_id) index — the existing-preference path is an UPDATE).
            // Surface a retryable conflict; re-applying here is unsafe (the failed insert stays Added).
            return Result<UserPreferenceResponse>.Failure(PreferenceErrors.ConcurrencyConflict);
        }

        return Result<UserPreferenceResponse>.Success(UserPreferenceAdministrationHelpers.Map(preference));
    }
}

internal sealed class UserPreferencePatchState
{
    public string Language { get; set; } = string.Empty;
    public bool HasMutation { get; set; }

    public static UserPreferencePatchState From(UserPreference preference) =>
        new()
        {
            Language = preference.Language
        };
}

internal sealed class UserPreferencePatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class UserPreferencePatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    private static readonly Regex LanguageRegex = new(
        "^[A-Za-z]{2,3}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static Result Apply(IReadOnlyCollection<UserPreferencePatchOperation> operations, UserPreferencePatchState state)
    {
        foreach (var operation in operations)
        {
            var op = operation.Op.Trim();
            if (!SupportedOperations.Contains(op))
            {
                return ValidationFailure(operation.Path, $"Unsupported JSON Patch operation '{operation.Op}'.");
            }

            var segments = ParsePath(operation.Path);
            if (segments.Length != 1)
            {
                return ValidationFailure(operation.Path, "Only root user preference properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (UserPreferencePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(UserPreferencePatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Validate the language format here (mirrors UpdateCurrentUserPreferencesCommandValidator and the
        // domain normalizer). The anchored regex also rejects whitespace-padded values, so a raw value that
        // the domain would trim+reject (e.g. " en", "e1") fails as a 400 instead of throwing in UpdateLanguage.
        if (string.IsNullOrWhiteSpace(state.Language))
        {
            errors["language"] = ["Language is required."];
        }
        else if (!LanguageRegex.IsMatch(state.Language))
        {
            errors["language"] = ["Language format is invalid."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        UserPreferencePatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "concurrencyToken"))
        {
            return ValidationFailure(path, "The concurrency token cannot be patched; send the current token in the If-Match header.");
        }

        if (IsSegment(property, "id") || IsSegment(property, "publicId") ||
            IsSegment(property, "createdAtUtc") || IsSegment(property, "modifiedAtUtc"))
        {
            return ValidationFailure(path, "This property is read-only and cannot be patched.");
        }

        if (IsSegment(property, "socialLinks"))
        {
            return ValidationFailure(path, "Social links cannot be patched; replace them via PUT /social-links.");
        }

        if (IsSegment(property, "language"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Language cannot be removed.");
            }

            state.Language = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static string[] ParsePath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(UnescapeJsonPointerSegment)
            .ToArray();

    private static string UnescapeJsonPointerSegment(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

    private static bool IsNull(JsonElement? value) =>
        !value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    private static bool IsSegment(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static string ReadRequiredString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new UserPreferencePatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new UserPreferencePatchValueException(path, "Value must be a string.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

internal static class UserPreferenceAdministrationHelpers
{
    public static async Task<Result<CLARIHR.Domain.Auth.User>> ResolveCurrentUserAsync(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUserService.UserId, out var userPublicId))
        {
            return Result<CLARIHR.Domain.Auth.User>.Failure(PreferenceErrors.InvalidCurrentUser);
        }

        var user = await userRepository.GetByPublicIdAsync(userPublicId, cancellationToken);
        return user is null
            ? Result<CLARIHR.Domain.Auth.User>.Failure(PreferenceErrors.InvalidCurrentUser)
            : Result<CLARIHR.Domain.Auth.User>.Success(user);
    }

    public static UserPreferenceResponse Map(UserPreference preference) =>
        new(
            preference.PublicId,
            preference.Language,
            preference.SocialLinks
                .OrderBy(static socialLink => socialLink.SortOrder)
                .Select(static socialLink => new UserSocialLinkResponse(
                    socialLink.ProviderCode,
                    socialLink.Url))
                .ToArray(),
            preference.ConcurrencyToken,
            preference.CreatedUtc,
            preference.ModifiedUtc);
}
