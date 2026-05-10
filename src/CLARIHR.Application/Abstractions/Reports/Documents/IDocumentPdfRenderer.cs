namespace CLARIHR.Application.Abstractions.Reports.Documents;

public interface IDocumentPdfRenderer<in TPayload>
{
    Task RenderAsync(TPayload payload, Stream destination, CancellationToken cancellationToken);
}
