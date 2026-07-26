using CLARIHR.Application.Abstractions.Companies;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Infrastructure.Companies;

/// <summary>
/// Scoped implementation of <see cref="IPendingEmailDispatcher"/>. One buffer per request (or per
/// background-job scope), drained explicitly by the handler once its transaction has committed.
/// </summary>
internal sealed class PendingEmailDispatcher(
    IEmailService emailService,
    ILogger<PendingEmailDispatcher> logger) : IPendingEmailDispatcher, IDisposable
{
    private readonly List<CompanyUserInvitationEmailMessage> _pending = [];

    public bool HasPending => _pending.Count > 0;

    public void Enqueue(CompanyUserInvitationEmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _pending.Add(message);
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        // Copy and clear first: a partial failure must not leave messages queued for a second
        // delivery attempt by the disposal path (that would duplicate invitations).
        var messages = _pending.ToArray();
        _pending.Clear();

        foreach (var message in messages)
        {
            try
            {
                await emailService.SendCompanyUserInvitationAsync(message, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Fail-open by design: the user already exists and the invitation token is already
                // persisted, so the operator can re-send with reset-invitation. Failing the request
                // here would roll nothing back — the transaction is committed — and would only turn a
                // provider outage into a user-creation outage.
                logger.LogError(
                    exception,
                    "CompanyUserInvitationDeliveryFailed email {Email} kind {Kind} expiresUtc {ExpiresUtc}. " +
                    "The account and its invitation token are committed; re-send with reset-invitation.",
                    message.Email,
                    message.Kind,
                    message.ExpiresUtc);
            }
        }
    }

    public void Discard() => _pending.Clear();

    public void Dispose()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        // Neither FlushAsync nor Discard ran. Dropping is the safe direction (a missing invitation is
        // recoverable, a phantom one is not), but it is always a bug in the calling handler.
        logger.LogError(
            "PendingInvitationEmailsDropped count {Count}. The scope ended with messages buffered: the " +
            "handler committed or rolled back without calling FlushAsync/Discard.",
            _pending.Count);
        _pending.Clear();
    }
}
