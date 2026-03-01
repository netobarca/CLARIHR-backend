namespace CLARIHR.Application.Abstractions.Auth;

public interface IRefreshTokenHasher
{
    string Hash(string refreshToken);
}
