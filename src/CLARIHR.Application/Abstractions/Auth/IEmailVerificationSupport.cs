namespace CLARIHR.Application.Abstractions.Auth;

public interface IEmailVerificationLinkBuilder
{
    string Build(string token);
}

public interface IEmailVerificationPolicyProvider
{
    int GetTokenLifetimeMinutes();

    int GetCooldownMinutes();
}
