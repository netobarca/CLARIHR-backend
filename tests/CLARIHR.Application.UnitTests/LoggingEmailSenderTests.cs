using CLARIHR.Application.Abstractions.Email;
using CLARIHR.Infrastructure.Email.Providers;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The development transport must never put a usable action link in the logs. The link is a
/// single-use credential and the e-mail is its only channel — a log sink is not a delivery mechanism
/// and not a vault.
/// </summary>
public sealed class LoggingEmailSenderTests
{
    private const string Link = "https://app.test/accept-invitation?token=abcdefghijklmnwxyz";

    [Fact]
    public async Task SendAsync_ShouldNeverLogARedeemableLink()
    {
        var logger = new CapturingLogger();

        await new LoggingEmailSender(logger).SendAsync(CreateMessage(), CancellationToken.None);

        Assert.DoesNotContain("abcdefghijklmnwxyz", logger.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Link, logger.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_ShouldStillShowTheDestinationSoAMisconfiguredUrlIsDiagnosable()
    {
        // A link left pointing at localhost in a deployed environment is a silent failure: the mail
        // arrives and the button goes nowhere. The masked destination is what makes it visible.
        var logger = new CapturingLogger();

        await new LoggingEmailSender(logger).SendAsync(CreateMessage(), CancellationToken.None);

        Assert.Contains("https://app.test/accept-invitation", logger.Text, StringComparison.Ordinal);
        Assert.Contains("abcd...wxyz", logger.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_ShouldLogTheSubjectAndRecipient()
    {
        var logger = new CapturingLogger();

        await new LoggingEmailSender(logger).SendAsync(CreateMessage(), CancellationToken.None);

        Assert.Contains("invitee@acme.test", logger.Text, StringComparison.Ordinal);
        Assert.Contains("Activa tu cuenta", logger.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_ShouldReportItselfAsTheLoggingProviderAndIssueNoMessageId()
    {
        var receipt = await new LoggingEmailSender(new CapturingLogger())
            .SendAsync(CreateMessage(), CancellationToken.None);

        Assert.Equal("Logging", receipt.Provider);
        Assert.Null(receipt.ProviderMessageId);
    }

    private static EmailMessage CreateMessage() =>
        new(
            new EmailAddress("invitee@acme.test", "Ana Lopez"),
            "Activa tu cuenta",
            $"<p>Hola</p><a href=\"{Link}\">Activar</a>",
            $"Hola\n\n{Link}\n");

    private sealed class CapturingLogger : ILogger<LoggingEmailSender>
    {
        private readonly List<string> _messages = [];

        public string Text => string.Join("\n", _messages);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _messages.Add(formatter(state, exception));
    }
}
