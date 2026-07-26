namespace CLARIHR.Application.Abstractions.Email;

/// <summary>
/// **The only provider seam.** Adding SendGrid, Mailgun, SES or an SMTP relay means writing one class
/// that implements this interface and registering it — nothing else in the system changes.
///
/// <para>Everything above this line is provider-agnostic on purpose: the templates, the subject, the
/// placeholder substitution and the decision of what each e-mail says all live in our code, so
/// switching providers never means re-creating content in someone else's console.</para>
///
/// <para>Implementations must be stateless and safe to resolve per scope. They may throw
/// <see cref="EmailDeliveryException"/> on failure; callers decide whether that is fatal (for us it
/// never is — see <c>IPendingEmailDispatcher</c>).</para>
/// </summary>
public interface IEmailSender
{
    /// <summary>Name of the transport, for logs and diagnostics (e.g. <c>Brevo</c>, <c>Logging</c>).</summary>
    string Provider { get; }

    Task<EmailDeliveryReceipt> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Raised when a transport could not deliver. Provider-neutral so callers never catch a
/// provider-specific type.
/// </summary>
public sealed class EmailDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
