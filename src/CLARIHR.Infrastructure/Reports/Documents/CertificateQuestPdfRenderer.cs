using System.Globalization;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CLARIHR.Infrastructure.Reports.Documents;

/// <summary>Renders a certificate PDF as a formal letter (D-15/D-17). Dedicated, QuestPDF-direct.</summary>
internal interface ICertificateDocumentRenderer
{
    Task RenderAsync(CertificatePrintPayload payload, byte[]? logoBytes, Stream destination, CancellationToken cancellationToken);
}

/// <summary>
/// QuestPDF-direct certificate renderer (D-15): a letter layout with the configurable company letterhead (logo,
/// issuing city + date), a centered title, the addressee, the structural body (from
/// <see cref="CertificateBodyComposer"/>), a signatory block and the footer/legal text. Intentionally separate
/// from the generic report <c>QuestPdfDocumentRenderer</c> (which is report-shaped, no logo). The QuestPDF
/// Community license is set in DI (the default report engine may be Gotenberg).
/// </summary>
internal sealed class CertificateQuestPdfRenderer : ICertificateDocumentRenderer
{
    private const string AccentColorHex = "#1F3A8A";
    private const string MutedColorHex = "#6B7280";
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    public async Task RenderAsync(CertificatePrintPayload payload, byte[]? logoBytes, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(destination);

        var body = CertificateBodyComposer.Compose(payload);
        var en = string.Equals(payload.LanguageCode, "en", StringComparison.OrdinalIgnoreCase);
        var cityDate = FormatCityDate(payload, en);
        var salutation = !string.IsNullOrWhiteSpace(payload.AddressedTo)
            ? payload.AddressedTo!
            : (en ? "To whom it may concern" : "A quien interese");

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(50);
                page.Size(PageSizes.Letter);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(text => text.FontSize(11).FontFamily(Fonts.Lato));

                page.Header().Element(header => ComposeHeader(header, payload, logoBytes, cityDate));
                page.Content().Element(content => ComposeContent(content, body, salutation));
                page.Footer().Element(footer => ComposeFooter(footer, payload));
            });
        });

        // QuestPDF exposes only a synchronous GeneratePdf; offload the CPU-bound render off the request thread.
        await Task.Run(() => pdf.GeneratePdf(destination), cancellationToken);
    }

    private static void ComposeHeader(IContainer container, CertificatePrintPayload payload, byte[]? logoBytes, string cityDate)
    {
        container.Column(column =>
        {
            if (logoBytes is { Length: > 0 })
            {
                column.Item().Width(150).Image(logoBytes);
            }

            column.Item().PaddingTop(8).AlignRight().Text(cityDate).FontColor(MutedColorHex);
            column.Item().PaddingTop(8).LineHorizontal(0.6f).LineColor(AccentColorHex);
        });
    }

    private static void ComposeContent(IContainer container, CertificateBody body, string salutation)
    {
        container.PaddingVertical(18).Column(column =>
        {
            column.Spacing(14);
            column.Item().AlignCenter().Text(body.Title).FontSize(16).Bold().FontColor(AccentColorHex);
            column.Item().PaddingTop(6).Text(salutation).SemiBold();

            foreach (var paragraph in body.Paragraphs)
            {
                column.Item().Text(paragraph);
            }
        });
    }

    private static void ComposeFooter(IContainer container, CertificatePrintPayload payload)
    {
        container.Column(column =>
        {
            column.Item().PaddingTop(36).AlignCenter().Column(signature =>
            {
                signature.Item().Width(240).LineHorizontal(0.8f).LineColor(MutedColorHex);

                if (!string.IsNullOrWhiteSpace(payload.SignatoryName))
                {
                    signature.Item().AlignCenter().Text(payload.SignatoryName).Bold();
                }

                if (!string.IsNullOrWhiteSpace(payload.SignatoryTitle))
                {
                    signature.Item().AlignCenter().Text(payload.SignatoryTitle).FontColor(MutedColorHex);
                }
            });

            if (!string.IsNullOrWhiteSpace(payload.FooterText))
            {
                column.Item().PaddingTop(12).AlignCenter().Text(payload.FooterText).FontSize(8).FontColor(MutedColorHex);
            }
        });
    }

    private static string FormatCityDate(CertificatePrintPayload payload, bool en)
    {
        var date = en
            ? payload.GeneratedAtUtc.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
            : payload.GeneratedAtUtc.ToString("d 'de' MMMM 'de' yyyy", Spanish);
        return string.IsNullOrWhiteSpace(payload.IssuingCity) ? date : $"{payload.IssuingCity}, {date}";
    }
}
