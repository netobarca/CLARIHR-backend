using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Reports.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §4.2: the PDF engine behind <see cref="IDocumentModelRenderer"/> is selected
/// from configuration and swappable. These tests pin the default (QuestPDF), the
/// case-insensitive selection, the engine-agnostic seam wiring, and the
/// fail-fast on an unknown/not-yet-implemented engine.
/// </summary>
public sealed class DocumentPdfRenderingRegistrationTests
{
    [Fact]
    public void AddDocumentPdfRendering_WithDefaultConfig_ResolvesQuestPdfEngine()
    {
        using var provider = BuildProvider(engine: null);

        Assert.IsType<QuestPdfDocumentRenderer>(provider.GetRequiredService<IDocumentModelRenderer>());
    }

    [Theory]
    [InlineData("QuestPdf")]
    [InlineData("questpdf")]      // case-insensitive
    [InlineData("  QuestPdf  ")]  // trimmed
    public void AddDocumentPdfRendering_WithQuestPdfEngine_ResolvesQuestPdf(string engine)
    {
        using var provider = BuildProvider(engine);

        Assert.IsType<QuestPdfDocumentRenderer>(provider.GetRequiredService<IDocumentModelRenderer>());
    }

    [Fact]
    public void AddDocumentPdfRendering_WiresEngineAgnosticSeam()
    {
        using var provider = BuildProvider(engine: null);

        // The payload→PDF seam resolves through the selected engine.
        Assert.IsType<JobProfilePdfRenderer>(
            provider.GetRequiredService<IDocumentPdfRenderer<JobProfilePrintResponse>>());
    }

    [Fact]
    public void AddDocumentPdfRendering_WithUnknownEngine_FailsFast()
    {
        // A not-yet-implemented engine must fail at startup, not silently fall back.
        var ex = Assert.Throws<InvalidOperationException>(() => BuildProvider("Gotenberg"));

        Assert.Contains("Gotenberg", ex.Message, StringComparison.Ordinal);
        Assert.Contains(PdfEngines.QuestPdf, ex.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(string? engine)
    {
        var settings = new Dictionary<string, string?>();
        if (engine is not null)
        {
            settings[$"{PdfRenderingOptions.SectionName}:Engine"] = engine;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new ServiceCollection()
            .AddDocumentPdfRendering(configuration)
            .BuildServiceProvider();
    }
}
