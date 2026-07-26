using System.Reflection;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Infrastructure.Companies;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The dispatcher exists so an invitation e-mail is never sent from inside an open transaction: the
/// provider call would hold the connection, a slow provider would roll back a good user creation, and
/// a send that beat a failed commit would invite a user that does not exist.
/// </summary>
public sealed class PendingEmailDispatcherTests
{
    [Fact]
    public async Task Enqueue_ShouldNotSendUntilFlush()
    {
        var transport = new RecordingEmailService();
        var dispatcher = CreateDispatcher(transport);

        dispatcher.Enqueue(CreateMessage("first@acme.test"));

        Assert.True(dispatcher.HasPending);
        Assert.Empty(transport.Sent);

        await dispatcher.FlushAsync(CancellationToken.None);

        Assert.False(dispatcher.HasPending);
        Assert.Equal(["first@acme.test"], transport.Sent.Select(static message => message.Email));
    }

    [Fact]
    public async Task Discard_ShouldDropEverythingBuffered()
    {
        // The rollback path: the account was never created, so its invitation must never go out.
        var transport = new RecordingEmailService();
        var dispatcher = CreateDispatcher(transport);

        dispatcher.Enqueue(CreateMessage("rolled-back@acme.test"));
        dispatcher.Discard();
        await dispatcher.FlushAsync(CancellationToken.None);

        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task FlushAsync_WhenTransportFails_ShouldNotThrowAndShouldNotRequeue()
    {
        // Fail-open: the user and the invitation token are already committed. Failing here would turn a
        // provider outage into a user-creation outage, and re-queueing would duplicate the invitation.
        var transport = new RecordingEmailService { ThrowOnSend = true };
        var dispatcher = CreateDispatcher(transport);

        dispatcher.Enqueue(CreateMessage("unreachable@acme.test"));

        var exception = await Record.ExceptionAsync(() => dispatcher.FlushAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.False(dispatcher.HasPending);
    }

    [Fact]
    public async Task FlushAsync_ShouldDeliverEveryBufferedMessageInOrder()
    {
        var transport = new RecordingEmailService();
        var dispatcher = CreateDispatcher(transport);

        dispatcher.Enqueue(CreateMessage("first@acme.test"));
        dispatcher.Enqueue(CreateMessage("second@acme.test"));

        await dispatcher.FlushAsync(CancellationToken.None);

        Assert.Equal(
            ["first@acme.test", "second@acme.test"],
            transport.Sent.Select(static message => message.Email));
    }

    [Fact]
    public async Task FlushAsync_TwiceShouldNotDuplicateDelivery()
    {
        var transport = new RecordingEmailService();
        var dispatcher = CreateDispatcher(transport);

        dispatcher.Enqueue(CreateMessage("once@acme.test"));
        await dispatcher.FlushAsync(CancellationToken.None);
        await dispatcher.FlushAsync(CancellationToken.None);

        Assert.Single(transport.Sent);
    }

    /// <summary>
    /// The whole point of the dispatcher is that no application code sends directly. If a handler
    /// injects <see cref="IEmailService"/> again it can (and eventually will) send from inside an open
    /// transaction, which is the defect this type was introduced to remove.
    /// </summary>
    [Fact]
    public void NoApplicationType_ShouldDependOnTheRawEmailTransport()
    {
        var offenders = typeof(IEmailService).Assembly
            .GetTypes()
            .Where(static type => type is { IsClass: true, IsAbstract: false })
            .Where(static type => type
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(static constructor => constructor.GetParameters())
                .Any(static parameter => parameter.ParameterType == typeof(IEmailService)))
            .Select(static type => type.FullName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"These application types inject {nameof(IEmailService)} directly: {string.Join(", ", offenders)}. " +
            $"Use {nameof(IPendingEmailDispatcher)} so the message is buffered inside the transaction and " +
            $"delivered after the commit; only the infrastructure dispatcher may hold the transport.");
    }

    private static PendingEmailDispatcher CreateDispatcher(IEmailService transport) =>
        new(transport, NullLogger<PendingEmailDispatcher>.Instance);

    private static CompanyUserInvitationEmailMessage CreateMessage(string email) =>
        new(
            email,
            "New",
            "Comer",
            "Acme HR",
            "raw-token",
            new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
            CompanyUserInvitationEmailKind.Invitation);

    private sealed class RecordingEmailService : IEmailService
    {
        public List<CompanyUserInvitationEmailMessage> Sent { get; } = [];

        public bool ThrowOnSend { get; init; }

        public Task SendCompanyUserInvitationAsync(
            CompanyUserInvitationEmailMessage message,
            CancellationToken cancellationToken)
        {
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("transport down");
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
