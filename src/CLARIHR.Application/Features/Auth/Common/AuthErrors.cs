using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.Auth.Common;

public static class AuthErrors
{
    public static readonly Error UserAlreadyExists = new(
        "auth.user_already_exists",
        "A user with the same email already exists.",
        ErrorType.Conflict);

    public static readonly Error ExternalTokenInvalid = new(
        "auth.external.invalid_token",
        "The external identity token is invalid.",
        ErrorType.Unauthorized);

    public static readonly Error ExternalEmailMissing = new(
        "auth.external.email_missing",
        "The external provider did not return an email address.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ExternalProviderNotSupported = new(
        "auth.external.provider_not_supported",
        "The requested external provider is not supported.",
        ErrorType.Validation);

    public static readonly Error ExternalProviderConfigurationInvalid = new(
        "auth.external.provider_configuration_invalid",
        "The external provider is not configured correctly.",
        ErrorType.Unexpected);

    public static readonly Error ExternalProviderLinkConflict = new(
        "auth.external.provider_link_conflict",
        "The user is already linked to a different external provider.",
        ErrorType.Conflict);

    public static readonly Error ExternalEmailLinkNotAllowed = new(
        "auth.external.email_link_not_allowed",
        "The external account cannot be linked automatically to the existing user for this email.",
        ErrorType.Conflict);

    public static readonly Error UserNotActive = new(
        "auth.user_not_active",
        "The user account is not active.",
        ErrorType.Unauthorized);

    public static readonly Error RefreshTokenInvalid = new(
        "auth.refresh.invalid_token",
        "The refresh token is invalid or expired.",
        ErrorType.Unauthorized);

    public static readonly Error TokenConfigurationInvalid = new(
        "auth.token_configuration_invalid",
        "JWT token generation is not configured correctly.",
        ErrorType.Unexpected);
}
