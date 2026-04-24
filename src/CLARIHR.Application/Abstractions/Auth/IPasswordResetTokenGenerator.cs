namespace CLARIHR.Application.Abstractions.Auth;

public interface IPasswordResetTokenGenerator
{
    string Generate();
}
