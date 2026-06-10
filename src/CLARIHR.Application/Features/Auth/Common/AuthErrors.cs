using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.Auth.Common;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = new(
        "auth.login.invalid_credentials",
        "The provided credentials are invalid.",
        ErrorType.Unauthorized);

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

    public static readonly Error InvalidCurrentUser = new(
        "auth.logout.invalid_current_user",
        "The authenticated user context is invalid.",
        ErrorType.Unauthorized);

    public static readonly Error InvitationTokenInvalid = new(
        "auth.invitation.invalid_token",
        "The invitation token is invalid or expired.",
        ErrorType.Unauthorized);

    // AC-1: the invitation is valid but its company has been archived — do not mint a session against an
    // archived company.
    public static readonly Error InvitationCompanyUnavailable = new(
        "auth.invitation.company_unavailable",
        "The company for this invitation is no longer available.",
        ErrorType.Conflict);

    public static readonly Error PasswordResetTokenInvalid = new(
        "auth.password_reset.invalid_token",
        "The password reset token is invalid or expired.",
        ErrorType.Unauthorized);

    public static readonly Error EmailVerificationTokenInvalid = new(
        "auth.email_verification.invalid_token",
        "The email verification token is invalid or expired.",
        ErrorType.Unauthorized);

    public static readonly Error TokenConfigurationInvalid = new(
        "auth.token_configuration_invalid",
        "JWT token generation is not configured correctly.",
        ErrorType.Unexpected);
}
