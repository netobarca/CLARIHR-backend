namespace CLARIHR.Application.Abstractions.Auth;

public sealed record PasswordResetEmailMessage(
    string ToEmail,
    string FirstName,
    string LastName,
    string ResetLink,
    DateTime ExpiresAtUtc);

public interface IAuthEmailService
{
    Task SendPasswordResetAsync(PasswordResetEmailMessage message, CancellationToken cancellationToken);
}
