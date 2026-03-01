namespace CLARIHR.Application.Abstractions.Authentication;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
}
