using CLARIHR.Application.Abstractions.Companies;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Records the invitation-mail lifecycle separately so a test can assert the ordering that matters:
/// <see cref="Enqueued"/> happens inside the transaction, <see cref="Delivered"/> only after the
/// handler commits. A handler that enqueues and never flushes shows up as delivered-but-empty.
/// </summary>
internal sealed class TestPendingEmailDispatcher : IPendingEmailDispatcher
{
    private readonly List<CompanyUserInvitationEmailMessage> _pending = [];

    public List<CompanyUserInvitationEmailMessage> Enqueued { get; } = [];

    public List<CompanyUserInvitationEmailMessage> Delivered { get; } = [];

    public int DiscardCount { get; private set; }

    public bool HasPending => _pending.Count > 0;

    public void Enqueue(CompanyUserInvitationEmailMessage message)
    {
        _pending.Add(message);
        Enqueued.Add(message);
    }

    public Task FlushAsync(CancellationToken cancellationToken)
    {
        Delivered.AddRange(_pending);
        _pending.Clear();
        return Task.CompletedTask;
    }

    public void Discard()
    {
        DiscardCount++;
        _pending.Clear();
    }
}
