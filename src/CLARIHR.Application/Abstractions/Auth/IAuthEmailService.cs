namespace CLARIHR.Application.Abstractions.Auth;

public sealed record PasswordResetEmailMessage(
    string ToEmail,
    string FirstName,
    string LastName,
    string ResetLink,
    DateTime ExpiresAtUtc);

public sealed record EmailVerificationEmailMessage(
    string ToEmail,
    string FirstName,
    string LastName,
    string VerificationLink,
    DateTime ExpiresAtUtc);

public interface IAuthEmailService
{
    Task SendPasswordResetAsync(PasswordResetEmailMessage message, CancellationToken cancellationToken);

    Task SendEmailVerificationAsync(EmailVerificationEmailMessage message, CancellationToken cancellationToken);
}
