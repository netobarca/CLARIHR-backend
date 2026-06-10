namespace CLARIHR.Application.Abstractions.Auth;

public interface IEmailVerificationTokenHasher
{
    string Hash(string token);
}
