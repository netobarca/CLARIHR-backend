namespace CLARIHR.Infrastructure.Configuration;

/// <summary>
/// Front-end landing page for a company-user invitation. The other two token flows already had one
/// (<see cref="PasswordResetOptions.FrontendResetUrl"/>, <c>EmailVerificationOptions.FrontendVerifyUrl</c>);
/// invitations did not, because the message only ever carried the raw token and the "delivery" was a
/// log line. A real provider needs a clickable link, so the URL becomes configuration like the others.
/// </summary>
public sealed class InvitationOptions
{
    public const string SectionName = "Authentication:Invitation";

    public string FrontendAcceptUrl { get; init; } = "http://localhost:3000/accept-invitation";
}
