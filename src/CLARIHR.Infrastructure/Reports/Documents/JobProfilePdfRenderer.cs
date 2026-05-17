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

    public Task RenderAsync(JobProfilePrintResponse payload, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(destination);

        var document = _mapper.Map(payload);
        _renderer.Render(document, destination);
        return Task.CompletedTask;
    }
}
