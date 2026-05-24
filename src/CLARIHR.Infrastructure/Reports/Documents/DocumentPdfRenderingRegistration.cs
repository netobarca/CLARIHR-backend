using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Reports.Documents;

/// <summary>
/// Wires document PDF rendering. The engine behind the
/// <see cref="IDocumentModelRenderer"/> seam is chosen at startup from
/// configuration (<c>Reporting:Pdf:Engine</c>) so QuestPDF can be swapped for
/// another generator without touching the document/mapping logic
/// (technical-debt doc 01 §4.2).
///
/// <para>To add an engine (e.g. Gotenberg, iText):</para>
/// <list type="number">
///   <item>Implement <see cref="IDocumentModelRenderer"/> in a new adapter (AST → output).</item>
///   <item>Add its key to <see cref="PdfEngines"/>.</item>
///   <item>Add a branch in <c>RegisterEngine</c> below (register the adapter plus any
///   engine-specific setup — an <c>HttpClient</c> for Gotenberg, a license for iText).</item>
/// </list>
/// QuestPDF's Community license is set here, <b>only</b> when QuestPDF is the active
/// engine — it no longer leaks into general infrastructure registration.
/// </summary>
internal static class DocumentPdfRenderingRegistration
{
    public static IServiceCollection AddDocumentPdfRendering(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PdfRenderingOptions>(configuration.GetSection(PdfRenderingOptions.SectionName));

        var engine = configuration[$"{PdfRenderingOptions.SectionName}:Engine"];
        engine = string.IsNullOrWhiteSpace(engine) ? PdfEngines.QuestPdf : engine.Trim();

        // Engine-agnostic: the payload mapper and the thin payload→PDF seam are
        // the same regardless of which engine renders the AST.
        services.AddScoped<IJobProfileDocumentMapper, JobProfileDocumentMapper>();
        services.AddScoped<IDocumentPdfRenderer<JobProfilePrintResponse>, JobProfilePdfRenderer>();

        RegisterEngine(services, configuration, engine);
        return services;
    }

    private static void RegisterEngine(IServiceCollection services, IConfiguration configuration, string engine)
    {
        if (string.Equals(engine, PdfEngines.QuestPdf, StringComparison.OrdinalIgnoreCase))
        {
            // QuestPDF requires its license set once before any render. Kept on the
            // QuestPDF branch so swapping engines drops the dependency cleanly.
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            services.AddScoped<IDocumentModelRenderer, QuestPdfDocumentRenderer>();
            return;
        }

        if (string.Equals(engine, PdfEngines.Gotenberg, StringComparison.OrdinalIgnoreCase))
        {
            // Gotenberg renders out-of-process over HTTP (no PDF-library license).
            // Bind its options and a typed HttpClient pointed at the service.
            services.Configure<GotenbergOptions>(configuration.GetSection(GotenbergOptions.SectionName));
            services.AddHttpClient<IDocumentModelRenderer, GotenbergDocumentRenderer>(static (provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<GotenbergOptions>>().Value;
                client.BaseAddress = new Uri(EnsureTrailingSlash(options.NormalizedBaseUrl));
                client.Timeout = options.NormalizedTimeout;
            });
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported PDF engine '{engine}' (config '{PdfRenderingOptions.SectionName}:Engine'). " +
            $"Supported engines: {PdfEngines.QuestPdf}, {PdfEngines.Gotenberg}. To add another (e.g. iText), implement " +
            $"{nameof(IDocumentModelRenderer)} and register it in {nameof(DocumentPdfRenderingRegistration)} " +
            "(see technical-debt doc 01 §4.2).");
    }

    private static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";
}
