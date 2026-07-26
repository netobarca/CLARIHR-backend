namespace CLARIHR.Application.Abstractions.Companies;

/// <summary>
/// Turns a raw invitation token into the front-end URL the invited user clicks. Mirrors
/// <c>IPasswordResetLinkBuilder</c>; kept out of the message record so handlers keep passing the raw
/// token and only the delivery layer knows about URLs.
/// </summary>
public interface IInvitationLinkBuilder
{
    string Build(string token);
}
