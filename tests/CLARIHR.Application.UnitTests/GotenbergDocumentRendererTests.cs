using System.Net;
using System.Text;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Infrastructure.Reports.Documents;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §4.2 (Gotenberg): the renderer must POST the serialized HTML to Gotenberg's
/// Chromium route, stream the returned PDF to the destination, and surface a
/// failing Gotenberg response as a typed error. HTTP is faked (no live service).
/// </summary>
public sealed class GotenbergDocumentRendererTests
{
    [Fact]
    public async Task RenderAsync_PostsHtmlToGotenberg_AndWritesReturnedPdf()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, "%PDF-1.7 fake %%EOF"u8.ToArray());
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://gotenberg.test/") };
        var renderer = new GotenbergDocumentRenderer(http);
        await using var output = new MemoryStream();

        await renderer.RenderAsync(SampleDocument(), output, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("http://gotenberg.test/forms/chromium/convert/html", handler.Request.RequestUri!.ToString());
        Assert.Contains("multipart/form-data", handler.ContentType, StringComparison.Ordinal);
        Assert.Contains("index.html", handler.RequestBody, StringComparison.Ordinal);  // HTML part present
        Assert.Contains("doc-title", handler.RequestBody, StringComparison.Ordinal);   // serialized AST rode along
        Assert.Equal("%PDF-1.7 fake %%EOF", Encoding.ASCII.GetString(output.ToArray()));
    }

    [Fact]
    public async Task RenderAsync_WhenGotenbergFails_ThrowsWithStatusAndBody()
    {
        var handler = new CapturingHandler(HttpStatusCode.InternalServerError, "boom"u8.ToArray());
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://gotenberg.test/") };
        var renderer = new GotenbergDocumentRenderer(http);
        await using var output = new MemoryStream();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => renderer.RenderAsync(SampleDocument(), output, CancellationToken.None));

        Assert.Contains("500", ex.Message, StringComparison.Ordinal);
        Assert.Contains("boom", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_AgainstLiveGotenberg_ProducesValidPdf()
    {
        // e2e: runs only when a live Gotenberg URL is provided (CLARIHR_GOTENBERG_E2E_URL).
        // No-op otherwise so CI without the service stays green.
        var baseUrl = Environment.GetEnvironmentVariable("CLARIHR_GOTENBERG_E2E_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        var renderer = new GotenbergDocumentRenderer(http);
        await using var output = new MemoryStream();

        await renderer.RenderAsync(SampleDocument(), output, CancellationToken.None);

        var bytes = output.ToArray();
        Assert.True(bytes.Length > 0, "Gotenberg returned an empty PDF.");
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length)), StringComparison.Ordinal);
        var tail = Encoding.ASCII.GetString(bytes, Math.Max(0, bytes.Length - 32), Math.Min(32, bytes.Length));
        Assert.Contains("%%EOF", tail, StringComparison.Ordinal);
    }

    private static DocumentModel SampleDocument() =>
        new("Perfil", Array.Empty<DocumentField>(), "hoy",
            new[] { new DocumentSection("S", new DocumentBlock[] { new ParagraphBlock("texto") }) });

    private sealed class CapturingHandler(HttpStatusCode status, byte[] responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;
        public string ContentType { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            ContentType = request.Content?.Headers.ContentType?.ToString() ?? string.Empty;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status) { Content = new ByteArrayContent(responseBody) };
        }
    }
}
