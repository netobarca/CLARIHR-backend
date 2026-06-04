using System.Text.Json;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.Preferences.Common;
using CLARIHR.Domain.Preferences;
using FluentValidation;

namespace CLARIHR.Application.Features.Preferences.Company;

public sealed record CompanyPreferenceResponse(
    Guid Id,
    string CurrencyCode,
    string TimeZone,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record GetCompanyPreferencesQuery(Guid CompanyId) : IQuery<CompanyPreferenceResponse>;

public sealed record UpdateCompanyPreferencesCommand(
    Guid CompanyId,
    string CurrencyCode,
    string TimeZone,
    Guid ConcurrencyToken) : ICommand<CompanyPreferenceResponse>;

public sealed record CompanyPreferencePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchCompanyPreferencesCommand(
    Guid CompanyId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<CompanyPreferencePatchOperation> Operations) : ICommand<CompanyPreferenceResponse>;

internal sealed class GetCompanyPreferencesQueryValidator : AbstractValidator<GetCompanyPreferencesQuery>
{
    public GetCompanyPreferencesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class UpdateCompanyPreferencesCommandValidator : AbstractValidator<UpdateCompanyPreferencesCommand>
{
    public UpdateCompanyPreferencesCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        // Trim before the exact-length check to match the domain normalizer (which trims first); a raw
        // 3-char value like " US" trims to length 2 and would otherwise throw a 500 in Update.
        RuleFor(command => command.CurrencyCode)
            .NotEmpty()
            .Must(static currencyCode => currencyCode is not null && currencyCode.Trim().Length == 3)
            .WithMessage("Currency code must be exactly 3 characters.");
        RuleFor(command => command.TimeZone)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchCompanyPreferencesCommandValidator : AbstractValidator<PatchCompanyPreferencesCommand>
{
    public PatchCompanyPreferencesCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
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

internal sealed class GetCompanyPreferencesQueryHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyPreferenceRepository companyPreferenceRepository)
    : IQueryHandler<GetCompanyPreferencesQuery, CompanyPreferenceResponse>
{
    public async Task<Result<CompanyPreferenceResponse>> Handle(
        GetCompanyPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(authorizationResult.Error);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(query.CompanyId, cancellationToken);
        return preference is null
            ? Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.CompanyPreferenceNotFound)
            : Result<CompanyPreferenceResponse>.Success(CompanyPreferenceAdministrationHelpers.Map(preference));
    }
}

internal sealed class UpdateCompanyPreferencesCommandHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompanyPreferencesCommand, CompanyPreferenceResponse>
{
    public async Task<Result<CompanyPreferenceResponse>> Handle(
        UpdateCompanyPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(authorizationResult.Error);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
        if (preference is null)
        {
            return Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.CompanyPreferenceNotFound);
        }

        if (preference.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.ConcurrencyConflict);
        }

        preference.Update(command.CurrencyCode, command.TimeZone);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CompanyPreferenceResponse>.Success(CompanyPreferenceAdministrationHelpers.Map(preference));
    }
}

internal sealed class PatchCompanyPreferencesCommandHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchCompanyPreferencesCommand, CompanyPreferenceResponse>
{
    public async Task<Result<CompanyPreferenceResponse>> Handle(
        PatchCompanyPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(authorizationResult.Error);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
        if (preference is null)
        {
            return Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.CompanyPreferenceNotFound);
        }

        if (preference.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompanyPreferenceResponse>.Failure(PreferenceErrors.ConcurrencyConflict);
        }

        var state = CompanyPreferencePatchState.From(preference);

        var applied = CompanyPreferencePatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(applied.Error);
        }

        var validation = CompanyPreferencePatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<CompanyPreferenceResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<CompanyPreferenceResponse>.Success(CompanyPreferenceAdministrationHelpers.Map(preference));
        }

        // Scalar-only patch: currencyCode/timeZone are the entity's only mutable fields and are
        // re-normalized + re-validated by Update exactly as the PUT path does. The applier already
        // enforced the same length rules (currencyCode == 3, timeZone <= 100) before we get here.
        preference.Update(state.CurrencyCode, state.TimeZone);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CompanyPreferenceResponse>.Success(CompanyPreferenceAdministrationHelpers.Map(preference));
    }
}

internal sealed class CompanyPreferencePatchState
{
    public string CurrencyCode { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public bool HasMutation { get; set; }

    public static CompanyPreferencePatchState From(CompanyPreference preference) =>
        new()
        {
            CurrencyCode = preference.CurrencyCode,
            TimeZone = preference.TimeZone
        };
}

internal sealed class CompanyPreferencePatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class CompanyPreferencePatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<CompanyPreferencePatchOperation> operations, CompanyPreferencePatchState state)
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
                return ValidationFailure(operation.Path, "Only root company preference properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (CompanyPreferencePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(CompanyPreferencePatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Validate the TRIMMED currency code: the domain normalizer (CompanyNormalization) trims before
        // its exact-length check, so a raw 3-char value that trims shorter (e.g. " US") would otherwise
        // pass here and then throw an unmapped ArgumentException in Update → HTTP 500 instead of a 400.
        if (string.IsNullOrWhiteSpace(state.CurrencyCode))
        {
            errors["currencyCode"] = ["Currency code is required."];
        }
        else if (state.CurrencyCode.Trim().Length != 3)
        {
            errors["currencyCode"] = ["Currency code must be exactly 3 characters."];
        }

        if (string.IsNullOrWhiteSpace(state.TimeZone))
        {
            errors["timeZone"] = ["Time zone is required."];
        }
        else if (state.TimeZone.Length > 100)
        {
            errors["timeZone"] = ["Time zone must be 100 characters or fewer."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        CompanyPreferencePatchState state,
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

        if (IsSegment(property, "currencyCode"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Currency code cannot be removed.");
            }

            state.CurrencyCode = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "timeZone"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Time zone cannot be removed.");
            }

            state.TimeZone = ReadRequiredString(value, path);
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
            throw new CompanyPreferencePatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new CompanyPreferencePatchValueException(path, "Value must be a string.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

internal static class CompanyPreferenceAdministrationHelpers
{
    public static CompanyPreferenceResponse Map(CompanyPreference preference) =>
        new(
            preference.PublicId,
            preference.CurrencyCode,
            preference.TimeZone,
            preference.ConcurrencyToken,
            preference.CreatedUtc,
            preference.ModifiedUtc);
}
