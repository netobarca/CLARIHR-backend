using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;

namespace CLARIHR.Infrastructure.Reports.Documents;

/// <summary>
/// Thin seam between the report-export pipeline and document generation:
/// maps the job-profile payload to the format-agnostic <see cref="DocumentModel"/>
/// and renders it to PDF. The mapping and the PDF layout are now separate,
/// single-sourced concerns (technical-debt doc 01 §2.2) — adding DOCX/HTML means
/// adding an <see cref="IDocumentModelRenderer"/>, not rewriting this class.
/// </summary>
internal sealed class JobProfilePdfRenderer : IDocumentPdfRenderer<JobProfilePrintResponse>
{
    private readonly IJobProfileDocumentMapper _mapper;
    private readonly IDocumentModelRenderer _renderer;

    public JobProfilePdfRenderer()
        : this(new JobProfileDocumentMapper(), new QuestPdfDocumentRenderer())
    {
    }

    public JobProfilePdfRenderer(IJobProfileDocumentMapper mapper, IDocumentModelRenderer renderer)
    {
        _mapper = mapper;
        _renderer = renderer;
    }

    public async Task RenderAsync(JobProfilePrintResponse payload, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(destination);

        var document = _mapper.Map(payload);

        // QuestPDF 2024.12.3 only exposes a synchronous GeneratePdf; offload the
        // CPU-bound render to the thread pool so the worker's pipeline thread
        // isn't blocked (technical-debt doc 01 §5.1). Replace with a native
        // GeneratePdfAsync when QuestPDF (or a successor library) ships one.
        await Task.Run(() => _renderer.Render(document, destination), cancellationToken);

        // Task.Run only honors the token before the action starts; QuestPDF does
        // not honor cancellation mid-render. Re-check after the render so a
        // token signal that arrived mid-render still surfaces as
        // OperationCanceledException and the worker marks the job cancelled
        // instead of writing the wasted bytes downstream (doc 01 §5.2).
        cancellationToken.ThrowIfCancellationRequested();
    }
}
