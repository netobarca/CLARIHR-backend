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

        // The engine adapter owns how it renders: QuestPDF offloads its
        // synchronous GeneratePdf to the thread pool, an HTTP engine would do
        // real async I/O (technical-debt doc 01 §4.2/§5.1).
        await _renderer.RenderAsync(document, destination, cancellationToken);

        // Engine-agnostic safety net: some engines (QuestPDF) don't honor
        // cancellation mid-render. Re-check after the render so a token signal
        // that arrived mid-render still surfaces as OperationCanceledException
        // and the worker marks the job cancelled instead of writing the wasted
        // bytes downstream (doc 01 §5.2).
        cancellationToken.ThrowIfCancellationRequested();
    }
}
