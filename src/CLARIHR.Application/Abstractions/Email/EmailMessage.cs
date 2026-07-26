namespace CLARIHR.Application.Abstractions.Email;

/// <summary>A recipient. <paramref name="Name"/> is optional; providers fall back to the address.</summary>
public sealed record EmailAddress(string Email, string? Name = null);

/// <summary>
/// A fully rendered e-mail, ready to hand to any transport. Deliberately free of provider concepts:
/// no template id, no provider parameter bag. Everything a provider needs is here, and everything
/// here means the same thing to every provider.
/// </summary>
public sealed record EmailMessage(
    EmailAddress To,
    string Subject,
    string HtmlBody,
    string TextBody);

/// <summary>
/// What a transport reports back. <paramref name="ProviderMessageId"/> is whatever id the provider
/// assigns (Brevo's <c>messageId</c>, SES's <c>MessageId</c>, …) and is the handle used to find the
/// send in the provider's own logs; it is null when the transport does not issue one.
/// </summary>
public sealed record EmailDeliveryReceipt(string Provider, string? ProviderMessageId);
