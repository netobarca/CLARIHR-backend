using System.Text;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Infrastructure.Reports.Documents;

namespace CLARIHR.Application.UnitTests;

public sealed class JobProfilePdfRendererTests
{
    static JobProfilePdfRendererTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public async Task RenderAsync_WhenPayloadIsMinimal_ShouldProduceValidPdf()
    {
        var renderer = new JobProfilePdfRenderer();
        var payload = BuildPayload(profile: BuildEmptyProfile());

        await using var stream = new MemoryStream();

        await renderer.RenderAsync(payload, stream, CancellationToken.None);

        AssertValidPdf(stream);
    }

    [Fact]
    public async Task RenderAsync_WhenTokenIsCancelledDuringRender_ShouldThrowOperationCanceled()
    {
        // §5.2: QuestPDF's GeneratePdf doesn't honor cancellation mid-render.
        // The renderer must re-check the token after the inner Render returns
        // so a mid-render cancellation still surfaces as OCE and the worker
        // marks the job cancelled (instead of uploading the wasted bytes).
        using var cts = new CancellationTokenSource();
        var cancellingRenderer = new CancellingDocumentModelRenderer(cts);
        var renderer = new JobProfilePdfRenderer(new JobProfileDocumentMapper(), cancellingRenderer);
        var payload = BuildPayload(BuildEmptyProfile());

        await using var stream = new MemoryStream();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => renderer.RenderAsync(payload, stream, cts.Token));

        Assert.True(cancellingRenderer.RenderInvoked, "Inner renderer should have run to completion before the post-render cancellation check.");
    }

    [Fact]
    public async Task RenderAsync_WhenPayloadIsRich_ShouldProduceValidPdf()
    {
        var renderer = new JobProfilePdfRenderer();
        var payload = BuildPayload(profile: BuildRichProfile());

        await using var stream = new MemoryStream();

        await renderer.RenderAsync(payload, stream, CancellationToken.None);

        AssertValidPdf(stream);
    }

    [Fact]
    public async Task RenderAsync_WhenCompensationIsNull_ShouldNotThrow()
    {
        var renderer = new JobProfilePdfRenderer();
        var profile = BuildEmptyProfile() with { Compensation = null };
        var payload = BuildPayload(profile);

        await using var stream = new MemoryStream();

        await renderer.RenderAsync(payload, stream, CancellationToken.None);

        AssertValidPdf(stream);
    }

    private static void AssertValidPdf(MemoryStream stream)
    {
        var bytes = stream.ToArray();

        Assert.True(bytes.Length > 0, "PDF stream should not be empty.");

        var headerLength = Math.Min(bytes.Length, 5);
        var header = Encoding.ASCII.GetString(bytes, 0, headerLength);
        Assert.StartsWith("%PDF-", header, StringComparison.Ordinal);

        var trailerStart = Math.Max(0, bytes.Length - 16);
        var trailer = Encoding.ASCII.GetString(bytes, trailerStart, bytes.Length - trailerStart);
        Assert.Contains("%%EOF", trailer, StringComparison.Ordinal);
    }

    private static JobProfilePrintResponse BuildPayload(JobProfileResponse profile) =>
        new(profile, new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc));

    private static JobProfileResponse BuildEmptyProfile() =>
        new(
            Id: Guid.NewGuid(),
            CompanyId: Guid.NewGuid(),
            Code: "MGR-001",
            Title: "Gerente de Desarrollo",
            Status: JobProfileStatus.Published,
            Version: 1,
            Objective: null,
            OrgUnitId: null,
            OrgUnitName: null,
            ReportsToJobProfileId: null,
            ReportsToJobProfileCode: null,
            ReportsToJobProfileTitle: null,
            PositionCategoryId: null,
            StrategicObjectiveCatalogItemId: null,
            AssignedWorkEquipmentCatalogItemId: null,
            ResponsibilityCatalogItemId: null,
            DecisionScope: null,
            AssignedResources: null,
            Responsibilities: null,
            BenefitsSummary: null,
            WorkingConditionSummary: null,
            MarketSalaryReference: null,
            ValuationNotes: null,
            EffectiveFromUtc: null,
            EffectiveToUtc: null,
            IsActive: true,
            Requirements: Array.Empty<JobProfileRequirementResponse>(),
            Functions: Array.Empty<JobProfileFunctionResponse>(),
            Relations: Array.Empty<JobProfileRelationResponse>(),
            Competencies: Array.Empty<JobProfileCompetencyResponse>(),
            Trainings: Array.Empty<JobProfileTrainingResponse>(),
            Compensation: null,
            Benefits: Array.Empty<JobProfileBenefitResponse>(),
            WorkingConditions: Array.Empty<JobProfileWorkingConditionResponse>(),
            DependentPositions: Array.Empty<JobProfileDependentPositionResponse>(),
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedAtUtc: null);

    private static JobProfileResponse BuildRichProfile()
    {
        var minimal = BuildEmptyProfile();

        return minimal with
        {
            Objective = "Liderar la estrategia de desarrollo de producto.",
            Responsibilities = "Definir el roadmap, gestionar el equipo y reportar al CTO.",
            AssignedResources = "Equipo de 12 personas, presupuesto anual de $1.2M.",
            DecisionScope = "Decisiones de arquitectura y staffing del área.",
            BenefitsSummary = "Paquete competitivo con beneficios adicionales por desempeño.",
            WorkingConditionSummary = "Trabajo híbrido, 40 horas semanales.",
            MarketSalaryReference = "Top 25% del mercado regional.",
            EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            OrgUnitName = "Tecnología",
            ReportsToJobProfileCode = "CTO-001",
            ReportsToJobProfileTitle = "Chief Technology Officer",
            Requirements = new[]
            {
                new JobProfileRequirementResponse(Guid.NewGuid(), null, null, JobRequirementType.Education, "Ingeniería en Sistemas", 1, Guid.NewGuid()),
                new JobProfileRequirementResponse(Guid.NewGuid(), null, null, JobRequirementType.Experience, "8+ años en roles de gerencia técnica", 2, Guid.NewGuid())
            },
            Functions = new[]
            {
                new JobProfileFunctionResponse(Guid.NewGuid(), null, JobFunctionType.General, "Gestión del roadmap del área", 1, Guid.NewGuid()),
                new JobProfileFunctionResponse(Guid.NewGuid(), null, JobFunctionType.Specific, "Mentoría de tech leads", 2, Guid.NewGuid())
            },
            Relations = new[]
            {
                new JobProfileRelationResponse(Guid.NewGuid(), null, JobRelationType.Internal, "Gerente de Producto", "Coordinación de prioridades", 1, Guid.NewGuid()),
                new JobProfileRelationResponse(Guid.NewGuid(), null, JobRelationType.External, "Proveedores cloud", "Negociación de contratos", 2, Guid.NewGuid())
            },
            Competencies = new[]
            {
                new JobProfileCompetencyResponse(
                    Id: Guid.NewGuid(),
                    OccupationalPyramidLevelId: Guid.NewGuid(),
                    OccupationalPyramidLevelCode: "LEV3",
                    OccupationalPyramidLevelName: "Liderazgo",
                    OccupationalPyramidLevelOrder: 3,
                    CompetencyId: Guid.NewGuid(),
                    CompetencyCode: "STRAT",
                    CompetencyName: "Pensamiento estratégico",
                    CompetencyTypeId: Guid.NewGuid(),
                    CompetencyTypeCode: "COG",
                    CompetencyTypeName: "Cognitiva",
                    BehaviorLevelId: Guid.NewGuid(),
                    BehaviorLevelCode: "ADV",
                    BehaviorLevelName: "Avanzado",
                    ExpectedEvidence: "Anticipa tendencias del mercado",
                    SortOrder: 1,
                    Conducts: new[]
                    {
                        new JobProfileCompetencyConductResponse(Guid.NewGuid(), "Define visión a 3 años", 1)
                    })
            },
            Trainings = new[]
            {
                new JobProfileTrainingResponse(Guid.NewGuid(), null, "Programa de Liderazgo Ejecutivo", "Recomendado", 1, Guid.NewGuid())
            },
            Benefits = new[]
            {
                new JobProfileBenefitResponse(Guid.NewGuid(), null, "Seguro médico premium", "Cobertura familiar", 1, Guid.NewGuid())
            },
            WorkingConditions = new[]
            {
                new JobProfileWorkingConditionResponse(Guid.NewGuid(), null, null, "Trabajo híbrido", "2 días remoto", 1, Guid.NewGuid())
            },
            DependentPositions = new[]
            {
                new JobProfileDependentPositionResponse(Guid.NewGuid(), Guid.NewGuid(), "DEV-LEAD", "Tech Lead", 3, "Reportes directos", Guid.NewGuid())
            },
            Compensation = new JobProfileCompensationResponse(
                SalaryClassId: Guid.NewGuid(),
                SalaryClassName: "Ejecutivo Grado 2",
                SalaryScaleCode: "EXE2",
                SalaryTabulatorLineId: Guid.NewGuid(),
                CurrencyCode: "USD",
                BaseAmount: 120_000m,
                MinAmount: 110_000m,
                MaxAmount: 150_000m,
                ResolvedEffectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ResolvedEffectiveToUtc: null)
        };
    }

    private sealed class CancellingDocumentModelRenderer(CancellationTokenSource cts) : IDocumentModelRenderer
    {
        public bool RenderInvoked { get; private set; }

        public void Render(DocumentModel document, Stream destination)
        {
            // Simulate QuestPDF: ignore cancellation, run to completion, but the
            // token gets signaled mid-render. The renderer-under-test must
            // re-check the token after we return.
            cts.Cancel();
            RenderInvoked = true;
        }
    }
}
