namespace CLARIHR.Application.Abstractions.Auth;

public interface IEmailVerificationTokenGenerator
{
    string Generate();
}
