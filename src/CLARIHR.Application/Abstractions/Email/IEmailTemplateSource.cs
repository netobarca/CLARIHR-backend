namespace CLARIHR.Application.Abstractions.Email;

/// <summary>
/// The e-mails the system sends. One key per message, independent of who delivers it.
/// </summary>
public enum EmailTemplateKey
{
    /// <summary>First invitation to a company user.</summary>
    Invitation = 1,

    /// <summary>Re-sent invitation (same link, different copy: "here it is again").</summary>
    ResetInvitation = 2,

    PasswordReset = 3,

    EmailVerification = 4,
}

/// <summary>
/// A raw template, before substitution. The subject travels inside the HTML (its <c>&lt;title&gt;</c>)
/// so a single file owns the whole message and translators/designers never have to edit two places.
/// </summary>
public sealed record EmailTemplateContent(string Html, string Text);

/// <summary>
/// Where templates come from. Split from rendering on purpose: moving the templates to the database
/// (editable through our own API, per tenant) later means writing one more implementation of this
/// interface — the renderer, the services and the transports stay untouched.
/// </summary>
public interface IEmailTemplateSource
{
    Task<EmailTemplateContent> GetAsync(EmailTemplateKey key, CancellationToken cancellationToken);
}

/// <summary>
/// Turns a template plus its placeholder values into a ready-to-send <see cref="EmailMessage"/>.
/// Placeholders are written <c>{{NAME}}</c> in both the HTML and the text body.
/// </summary>
public interface IEmailTemplateRenderer
{
    Task<EmailMessage> RenderAsync(
        EmailTemplateKey key,
        EmailAddress to,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken);
}
