namespace CLARIHR.Application.Abstractions.Companies;

public interface IInvitationTokenHasher
{
    string Hash(string token);
}
