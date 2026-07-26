namespace CLARIHR.Application.Abstractions.Companies;

/// <summary>
/// Buffers invitation e-mails raised while a database transaction is open and delivers them only
/// after that transaction commits.
///
/// <para>Sending straight from the handler was harmless while <c>IEmailService</c> only wrote to the
/// log, but with a real provider it means an outbound HTTP call holding the connection and its locks
/// for the whole round trip — and, worse, two ways to be wrong: a slow provider rolls back a
/// perfectly good user creation, and a send that succeeds before a failed commit invites a user that
/// does not exist.</para>
///
/// <para>Contract: <see cref="Enqueue"/> inside the transaction, <see cref="FlushAsync"/> after
/// <c>CommitAsync</c>, <see cref="Discard"/> on the rollback path. Anything still pending when the
/// scope ends is dropped and logged as an error — losing an invitation is recoverable through
/// <c>reset-invitation</c>, inviting a phantom user is not.</para>
/// </summary>
public interface IPendingEmailDispatcher
{
    /// <summary>Gets a value indicating whether at least one message is awaiting delivery.</summary>
    bool HasPending { get; }

    /// <summary>Buffers a message. Never performs I/O.</summary>
    void Enqueue(CompanyUserInvitationEmailMessage message);

    /// <summary>
    /// Delivers and clears everything buffered. Delivery failures are logged, never rethrown: the
    /// business operation already committed and must not be reported as failed because the mail
    /// provider is down.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken);

    /// <summary>Drops everything buffered. Call this whenever the transaction is rolled back.</summary>
    void Discard();
}
