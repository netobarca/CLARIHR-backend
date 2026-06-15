using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Structural fidelity coverage for the format-agnostic Document AST refactor
/// (technical-debt doc 01 §2.2): asserts the mapper reproduces the exact
/// sections, ordering, formatting, placeholders and empty-state rules the
/// QuestPDF renderer previously hardcoded.
/// </summary>
public sealed class JobProfileDocumentMapperTests
{
    private static readonly JobProfileDocumentMapper Mapper = new();

    [Fact]
    public void Map_EmptyProfile_ProducesHeaderSectionsAndEmptyStates()
    {
        var document = Mapper.Map(BuildPayload(BuildEmptyProfile()));

        Assert.Equal("Gerente de Desarrollo", document.Title);
        Assert.Equal(
            new[] { ("Código", "MGR-001"), ("Versión", "1"), ("Estado", "Published") },
            document.HeaderFields.Select(f => (f.Label, f.Value)).ToArray());
        Assert.Equal("2026-05-09 12:00:00Z", document.GeneratedText);

        // Optional paragraph sections are omitted when their source value is blank.
        Assert.Equal(
            new[]
            {
                "Información general",
                "Funciones",
                "Requisitos",
                "Competencias",
                "Entrenamientos sugeridos",
                "Beneficios",
                "Condiciones laborales",
                "Relaciones",
                "Posiciones dependientes",
            },
            document.Sections.Select(s => s.Title).ToArray());

        var general = Assert.IsType<KeyValueBlock>(Single(document, "Información general"));
        Assert.Equal(
            new[]
            {
                ("Unidad organizativa", "—"),
                ("Reporta a", "—"),
                ("Vigencia desde", "—"),
                ("Vigencia hasta", "—"),
                ("Activo", "Sí"),
            },
            general.Items.Select(i => (i.Label, i.Value)).ToArray());

        var functions = Assert.IsType<MutedTextBlock>(Single(document, "Funciones"));
        Assert.Equal("Sin información registrada.", functions.Text);
        Assert.IsType<MutedTextBlock>(Single(document, "Posiciones dependientes"));
    }

    [Fact]
    public void Map_RichProfile_FormatsOrdersAndPlacesEverySection()
    {
        var document = Mapper.Map(BuildPayload(BuildRichProfile()));

        Assert.Equal(
            ["Objetivo", "Responsabilidades", "Recursos asignados", "Alcance de decisión"],
            document.Sections.Select(s => s.Title)
                .Where(t => t is "Objetivo" or "Responsabilidades" or "Recursos asignados" or "Alcance de decisión"));

        var objective = Assert.IsType<ParagraphBlock>(Single(document, "Objetivo"));
        Assert.Equal("Liderar la estrategia de desarrollo de producto.", objective.Text);

        // Functions: type enum mapped to Spanish and rows ordered by SortOrder.
        var functions = Assert.IsType<TableBlock>(Single(document, "Funciones"));
        Assert.Equal(new[] { "General", "Gestión del roadmap del área" }, functions.Rows[0].ToArray());
        Assert.Equal(new[] { "Específica", "Mentoría de tech leads" }, functions.Rows[1].ToArray());

        var competencies = Assert.IsType<TableBlock>(Single(document, "Competencias"));
        Assert.Equal("Pensamiento estratégico (Cognitiva)", competencies.Rows[0][1]);
        Assert.Contains("• Define visión a 3 años", competencies.Rows[0][3]);

        var trainings = Assert.IsType<BulletListBlock>(Single(document, "Entrenamientos sugeridos"));
        Assert.Equal("Programa de Liderazgo Ejecutivo", trainings.Items[0].Text);
        Assert.Equal("Recomendado", trainings.Items[0].Notes);

        var benefitsBlocks = SectionBlocks(document, "Beneficios");
        Assert.IsType<BulletListBlock>(Assert.Single(benefitsBlocks));

        var compensationBlocks = SectionBlocks(document, "Compensación");
        var comp = Assert.IsType<KeyValueBlock>(compensationBlocks[0]);
        Assert.Equal("120,000.00", comp.Items.Single(i => i.Label == "Salario base").Value);
        var market = Assert.IsType<LabeledParagraphBlock>(compensationBlocks[1]);
        Assert.Equal("Referencia de mercado: ", market.Label);
        Assert.Equal("Top 25% del mercado regional.", market.Text);

        var relations = Assert.IsType<TableBlock>(Single(document, "Relaciones"));
        Assert.Equal("Interna", relations.Rows[0][0]);
        Assert.Equal("Externa", relations.Rows[1][0]);
    }

    [Fact]
    public void Map_FunctionsOutOfOrder_AreSortedBySortOrder()
    {
        var profile = BuildEmptyProfile() with
        {
            Functions =
            [
                new JobProfileFunctionResponse(Guid.NewGuid(), null, JobFunctionType.Specific, "Segunda", 2, Guid.NewGuid()),
                new JobProfileFunctionResponse(Guid.NewGuid(), null, JobFunctionType.General, "Primera", 1, Guid.NewGuid()),
            ],
        };

        var table = Assert.IsType<TableBlock>(Single(Mapper.Map(BuildPayload(profile)), "Funciones"));

        Assert.Equal("Primera", table.Rows[0][1]);
        Assert.Equal("Segunda", table.Rows[1][1]);
    }

    private static DocumentBlock Single(DocumentModel document, string sectionTitle) =>
        Assert.Single(SectionBlocks(document, sectionTitle));

    private static IReadOnlyList<DocumentBlock> SectionBlocks(DocumentModel document, string sectionTitle) =>
        document.Sections.Single(s => s.Title == sectionTitle).Blocks;

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

    private static JobProfileResponse BuildRichProfile() =>
        BuildEmptyProfile() with
        {
            Objective = "Liderar la estrategia de desarrollo de producto.",
            Responsibilities = "Definir el roadmap, gestionar el equipo y reportar al CTO.",
            AssignedResources = "Equipo de 12 personas, presupuesto anual de $1.2M.",
            DecisionScope = "Decisiones de arquitectura y staffing del área.",
            MarketSalaryReference = "Top 25% del mercado regional.",
            EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            OrgUnitName = "Tecnología",
            ReportsToJobProfileCode = "CTO-001",
            ReportsToJobProfileTitle = "Chief Technology Officer",
            Requirements =
            [
                new JobProfileRequirementResponse(Guid.NewGuid(), null, null, JobRequirementType.Education, "Ingeniería en Sistemas", 1, Guid.NewGuid()),
                new JobProfileRequirementResponse(Guid.NewGuid(), null, null, JobRequirementType.Experience, "8+ años", 2, Guid.NewGuid()),
            ],
            Functions =
            [
                new JobProfileFunctionResponse(Guid.NewGuid(), null, JobFunctionType.General, "Gestión del roadmap del área", 1, Guid.NewGuid()),
                new JobProfileFunctionResponse(Guid.NewGuid(), null, JobFunctionType.Specific, "Mentoría de tech leads", 2, Guid.NewGuid()),
            ],
            Relations =
            [
                new JobProfileRelationResponse(Guid.NewGuid(), null, JobRelationType.Internal, "Gerente de Producto", "Coordinación", 1, Guid.NewGuid()),
                new JobProfileRelationResponse(Guid.NewGuid(), null, JobRelationType.External, "Proveedores cloud", "Negociación", 2, Guid.NewGuid()),
            ],
            Competencies =
            [
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
                    Conducts: [new JobProfileCompetencyConductResponse(Guid.NewGuid(), "Define visión a 3 años", 1)]),
            ],
            Trainings =
            [
                new JobProfileTrainingResponse(Guid.NewGuid(), null, "Programa de Liderazgo Ejecutivo", "Recomendado", 1, Guid.NewGuid()),
            ],
            Benefits =
            [
                new JobProfileBenefitResponse(Guid.NewGuid(), null, "Seguro médico premium", "Cobertura familiar", 1, Guid.NewGuid()),
            ],
            WorkingConditions =
            [
                new JobProfileWorkingConditionResponse(Guid.NewGuid(), null, null, "Trabajo híbrido", "2 días remoto", 1, Guid.NewGuid()),
            ],
            DependentPositions =
            [
                new JobProfileDependentPositionResponse(Guid.NewGuid(), Guid.NewGuid(), "DEV-LEAD", "Tech Lead", 3, "Reportes directos", Guid.NewGuid()),
            ],
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
                ResolvedEffectiveToUtc: null),
        };
}
