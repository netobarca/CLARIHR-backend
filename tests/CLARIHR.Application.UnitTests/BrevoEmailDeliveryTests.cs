using System.Net;
using System.Text;
using System.Text.Json;
using CLARIHR.Application.Abstractions.Email;
using CLARIHR.Infrastructure.Email.Providers.Brevo;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Exercises the Brevo transport against a stubbed <see cref="HttpMessageHandler"/> — the provider is
/// never contacted. It sends rendered content (subject + html + text), never a provider template id:
/// the templates are ours, so switching provider does not mean re-creating them elsewhere.
/// </summary>
public sealed class BrevoEmailDeliveryTests
{
    [Fact]
    public async Task SendAsync_WhenProviderAccepts_ShouldPostRenderedContentAndReturnTheReceipt()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.Created, """{"messageId":"<abc@brevo>"}"""));
        var sender = CreateSender(handler);

        var receipt = await sender.SendAsync(CreateMessage(), CancellationToken.None);

        Assert.Equal("Brevo", receipt.Provider);
        Assert.Equal("<abc@brevo>", receipt.ProviderMessageId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.brevo.test/v3/smtp/email", request.Uri);
        Assert.Equal("secret-key", request.ApiKey);

        using var payload = JsonDocument.Parse(request.Body);
        var root = payload.RootElement;
        Assert.Equal("Activa tu cuenta", root.GetProperty("subject").GetString());
        Assert.Equal("<p>Hola Ana</p>", root.GetProperty("htmlContent").GetString());
        Assert.Equal("Hola Ana", root.GetProperty("textContent").GetString());
        Assert.Equal("no-reply@clarihr.test", root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("invitee@acme.test", root.GetProperty("to")[0].GetProperty("email").GetString());

        // The payload must not carry a provider template id — that is the coupling this design removes.
        Assert.False(root.TryGetProperty("templateId", out _));
    }

    [Fact]
    public async Task SendAsync_WhenProviderRejectsWithClientError_ShouldFailWithoutRetrying()
    {
        // An unverified sender or a malformed address is a 400: retrying reproduces it exactly.
        var handler = new StubHttpMessageHandler(_ =>
            Json(HttpStatusCode.BadRequest, """{"code":"invalid_parameter","message":"Invalid sender"}"""));
        var sender = CreateSender(handler);

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync(CreateMessage(), CancellationToken.None));

        Assert.Single(handler.Requests);
        Assert.Contains("Invalid sender", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WhenProviderIsUnavailable_ShouldRetryUpToTheConfiguredLimit()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var sender = CreateSender(handler, maxRetries: 2);

        await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync(CreateMessage(), CancellationToken.None));

        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task SendAsync_WhenRateLimited_ShouldRetryAndSucceed()
    {
        // 429 is the one 4xx worth retrying: the payload is fine, the account is over quota this second.
        var attempt = 0;
        var handler = new StubHttpMessageHandler(_ => ++attempt == 1
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            : Json(HttpStatusCode.Created, """{"messageId":"<retried@brevo>"}"""));
        var sender = CreateSender(handler);

        var receipt = await sender.SendAsync(CreateMessage(), CancellationToken.None);

        Assert.Equal("<retried@brevo>", receipt.ProviderMessageId);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SendAsync_ShouldRaiseTheProviderNeutralException()
    {
        // Callers must never have to catch a Brevo-specific type; swapping provider must not change
        // any catch clause in the system.
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("dns failure"));
        var sender = CreateSender(handler, maxRetries: 0);

        var exception = await Record.ExceptionAsync(() => sender.SendAsync(CreateMessage(), CancellationToken.None));

        Assert.IsType<EmailDeliveryException>(exception);
    }

    private static EmailMessage CreateMessage() =>
        new(new EmailAddress("invitee@acme.test", "Ana Lopez"), "Activa tu cuenta", "<p>Hola Ana</p>", "Hola Ana");

    private static BrevoEmailSender CreateSender(StubHttpMessageHandler handler, int maxRetries = 2)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.test/") };
        httpClient.DefaultRequestHeaders.Add("api-key", "secret-key");

        return new BrevoEmailSender(
            httpClient,
            Options.Create(new BrevoOptions
            {
                ApiKey = "secret-key",
                BaseUrl = "https://api.brevo.test/",
                SenderName = "CLARIHR",
                SenderEmail = "no-reply@clarihr.test",
                TimeoutSeconds = 5,
                MaxRetries = maxRetries,
            }),
            NullLogger<BrevoEmailSender>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record CapturedRequest(HttpMethod Method, string Uri, string Body, string? ApiKey);

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                body,
                request.Headers.TryGetValues("api-key", out var values) ? values.FirstOrDefault() : null));

            return responder(request);
        }
    }
}
