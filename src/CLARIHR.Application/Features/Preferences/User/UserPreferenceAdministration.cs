using System.Text.RegularExpressions;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Preferences.Common;
using CLARIHR.Domain.Preferences;
using FluentValidation;

namespace CLARIHR.Application.Features.Preferences.User;

public sealed record UserPreferenceResponse(
    Guid Id,
    string Language,
    IReadOnlyCollection<UserSocialLinkResponse> SocialLinks,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record UserSocialLinkResponse(string ProviderCode, string Url);

public sealed record GetCurrentUserPreferencesQuery : IQuery<UserPreferenceResponse>;

public sealed record UpdateCurrentUserPreferencesCommand(string Language) : ICommand<UserPreferenceResponse>;

public sealed record ReplaceCurrentUserSocialLinksCommand(
    IReadOnlyCollection<UpdateCurrentUserSocialLinkItem> Items) : ICommand<UserPreferenceResponse>;

public sealed record UpdateCurrentUserSocialLinkItem(string ProviderCode, string Url);

internal sealed class UpdateCurrentUserPreferencesCommandValidator : AbstractValidator<UpdateCurrentUserPreferencesCommand>
{
    public UpdateCurrentUserPreferencesCommandValidator()
    {
        RuleFor(command => command.Language)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .WithMessage("Language format is invalid.");
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
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
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
            preference = UserPreference.Create(currentUserResult.Value.Id);
            userPreferenceRepository.Add(preference);
        }

        preference.ReplaceSocialLinks(command.Items.Select(static item => new UserSocialLinkInput(item.ProviderCode, item.Url)));

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

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
            preference = UserPreference.Create(currentUserResult.Value.Id, command.Language);
            userPreferenceRepository.Add(preference);
        }
        else
        {
            preference.UpdateLanguage(command.Language);
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserPreferenceResponse>.Success(UserPreferenceAdministrationHelpers.Map(preference));
    }
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
            preference.CreatedUtc,
            preference.ModifiedUtc);
}
