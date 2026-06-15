using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Worker.LoadTests;

/// <summary>
/// Representative Job Profile payloads for the §8.2 worker render load test:
/// an empty profile (minimal sections) and a rich one (all sub-collections +
/// compensation), so throughput/latency can be compared across profile sizes.
/// Mirrors the builders in JobProfilePdfRendererTests.
/// </summary>
internal static class Payloads
{
    private static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime GeneratedAt = new(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc);

    public static JobProfilePrintResponse Empty() => new(EmptyProfile(), GeneratedAt);

    public static JobProfilePrintResponse Rich() => new(RichProfile(), GeneratedAt);

    private static JobProfileResponse EmptyProfile() =>
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
            CreatedAtUtc: SeedDate,
            ModifiedAtUtc: null);

    private static JobProfileResponse RichProfile() =>
        EmptyProfile() with
        {
            Objective = "Liderar la estrategia de desarrollo de producto.",
            Responsibilities = "Definir el roadmap, gestionar el equipo y reportar al CTO.",
            AssignedResources = "Equipo de 12 personas, presupuesto anual de $1.2M.",
            DecisionScope = "Decisiones de arquitectura y staffing del área.",
            MarketSalaryReference = "Top 25% del mercado regional.",
            EffectiveFromUtc = SeedDate,
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
                ResolvedEffectiveFromUtc: SeedDate,
                ResolvedEffectiveToUtc: null)
        };
}
