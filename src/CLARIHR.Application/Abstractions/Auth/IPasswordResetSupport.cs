namespace CLARIHR.Application.Abstractions.Auth;

public interface IPasswordResetLinkBuilder
{
    string Build(string token);
}

public interface IPasswordResetPolicyProvider
{
    int GetTokenLifetimeMinutes();

    int GetCooldownMinutes();
}
