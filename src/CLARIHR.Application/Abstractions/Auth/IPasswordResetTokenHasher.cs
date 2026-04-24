namespace CLARIHR.Application.Abstractions.Auth;

public interface IPasswordResetTokenHasher
{
    string Hash(string token);
}
