using CLARIHR.Application.Abstractions.Email;
using CLARIHR.Infrastructure.Email;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The templates are ours, not the provider's — so they are code, and they get tested like code.
/// </summary>
public sealed class EmailTemplateTests
{
    private static readonly EmailAddress Recipient = new("ana@acme.test", "Ana Lopez");

    [Theory]
    [InlineData(EmailTemplateKey.Invitation)]
    [InlineData(EmailTemplateKey.ResetInvitation)]
    [InlineData(EmailTemplateKey.PasswordReset)]
    [InlineData(EmailTemplateKey.EmailVerification)]
    public async Task EveryTemplateKey_ShouldHaveBothBodiesEmbedded(EmailTemplateKey key)
    {
        // Adding a key without adding its two files would only fail at send time, in production.
        var content = await new EmbeddedEmailTemplateSource().GetAsync(key, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(content.Html));
        Assert.False(string.IsNullOrWhiteSpace(content.Text));
    }

    [Theory]
    [InlineData(EmailTemplateKey.Invitation)]
    [InlineData(EmailTemplateKey.ResetInvitation)]
    [InlineData(EmailTemplateKey.PasswordReset)]
    [InlineData(EmailTemplateKey.EmailVerification)]
    public async Task EveryTemplate_ShouldRenderASubjectAndCarryTheActionLinkInBothBodies(EmailTemplateKey key)
    {
        var rendered = await CreateRenderer().RenderAsync(key, Recipient, CreateValues(), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(rendered.Subject));
        Assert.DoesNotContain("{{", rendered.Subject, StringComparison.Ordinal);

        // A mail whose only purpose is to carry a link must carry it in both bodies — plain-text
        // clients are exactly the ones that cannot fall back to the HTML part.
        Assert.Contains("https://app.test/go?token=raw", rendered.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("https://app.test/go?token=raw", rendered.TextBody, StringComparison.Ordinal);

        // The subject lives in <title>; it must not also be rendered inside the body.
        Assert.DoesNotContain("<title>", rendered.HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(EmailTemplateKey.Invitation)]
    [InlineData(EmailTemplateKey.ResetInvitation)]
    [InlineData(EmailTemplateKey.PasswordReset)]
    [InlineData(EmailTemplateKey.EmailVerification)]
    public async Task EveryTemplate_ShouldSubstituteEveryPlaceholderItDeclares(EmailTemplateKey key)
    {
        var rendered = await CreateRenderer().RenderAsync(key, Recipient, CreateValues(), CancellationToken.None);

        // An unsubstituted {{FOO}} reaching a real inbox is the visible symptom of a typo between the
        // template and the service that feeds it.
        Assert.DoesNotContain("{{", rendered.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", rendered.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_ShouldHtmlEncodeValuesInTheHtmlBodyButNotInThePlainTextBody()
    {
        // A surname is user-supplied. Without encoding, `<script>` would be injected into a mail we
        // send on the company's behalf.
        var values = CreateValues();
        var hostile = new Dictionary<string, string>(values, StringComparer.Ordinal)
        {
            ["FIRSTNAME"] = "<script>alert(1)</script>",
        };

        var rendered = await CreateRenderer().RenderAsync(
            EmailTemplateKey.Invitation,
            Recipient,
            hostile,
            CancellationToken.None);

        Assert.DoesNotContain("<script>", rendered.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", rendered.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("<script>alert(1)</script>", rendered.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_ShouldLeaveAnUnknownPlaceholderVisibleRatherThanBlank()
    {
        var source = new InlineTemplateSource(
            "<title>Asunto</title><p>Hola {{FIRSTNAME}}, falta {{UNKNOWN}}</p>",
            "Hola {{FIRSTNAME}}, falta {{UNKNOWN}}");

        var rendered = await new EmailTemplateRenderer(source).RenderAsync(
            EmailTemplateKey.Invitation,
            Recipient,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["FIRSTNAME"] = "Ana" },
            CancellationToken.None);

        // Visible beats silent: a mail that says {{UNKNOWN}} gets reported; one missing a link does not.
        Assert.Contains("{{UNKNOWN}}", rendered.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Hola Ana", rendered.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_WhenTemplateHasNoTitle_ShouldFailExplaining()
    {
        var source = new InlineTemplateSource("<p>sin asunto</p>", "sin asunto");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new EmailTemplateRenderer(source).RenderAsync(
                EmailTemplateKey.Invitation,
                Recipient,
                CreateValues(),
                CancellationToken.None));

        Assert.Contains("<title>", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_ShouldSubstitutePlaceholdersInsideTheSubject()
    {
        var source = new InlineTemplateSource("<title>{{COMPANYNAME}} te invitó</title><p>x</p>", "x");

        var rendered = await new EmailTemplateRenderer(source).RenderAsync(
            EmailTemplateKey.Invitation,
            Recipient,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["COMPANYNAME"] = "Acme HR" },
            CancellationToken.None);

        Assert.Equal("Acme HR te invitó", rendered.Subject);
    }

    private static EmailTemplateRenderer CreateRenderer() => new(new EmbeddedEmailTemplateSource());

    private static Dictionary<string, string> CreateValues() => new(StringComparer.Ordinal)
    {
        ["FIRSTNAME"] = "Ana",
        ["LASTNAME"] = "Lopez",
        ["COMPANYNAME"] = "Acme HR",
        ["ACTIONURL"] = "https://app.test/go?token=raw",
        ["EXPIRESUTC"] = "2026-08-01 12:00:00Z",
    };

    private sealed class InlineTemplateSource(string html, string text) : IEmailTemplateSource
    {
        public Task<EmailTemplateContent> GetAsync(EmailTemplateKey key, CancellationToken cancellationToken) =>
            Task.FromResult(new EmailTemplateContent(html, text));
    }
}
