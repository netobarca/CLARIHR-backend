namespace CLARIHR.Application.Abstractions.Companies;

public interface IEmailService
{
    Task SendCompanyUserInvitationAsync(CompanyUserInvitationEmailMessage message, CancellationToken cancellationToken);
}

public sealed record CompanyUserInvitationEmailMessage(
    string Email,
    string FirstName,
    string LastName,
    string CompanyName,
    string Token,
    DateTime ExpiresUtc,
    CompanyUserInvitationEmailKind Kind);

public enum CompanyUserInvitationEmailKind
{
    Invitation = 1,
    ResetInvitation = 2
}
