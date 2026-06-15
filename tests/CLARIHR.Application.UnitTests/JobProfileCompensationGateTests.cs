using System.Text.Json;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Infrastructure.Reports.Handlers;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Confidentiality coverage for the job-profile PDF salary gate
/// (technical-debt doc 01 §N2 / §N3). Proves the gate is fail-closed and that
/// excluded salary data is physically removed from the payload — so it never
/// reaches the renderer / PDF bytes — rather than masked. §N3: also proves the
/// gate resists JSON key-casing variants (exact, case-sensitive read).
/// </summary>
public sealed class JobProfileCompensationGateTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"includeCompensation\":false}")]
    [InlineData("{\"includeCompensation\":null}")]
    [InlineData("{\"includeCompensation\":\"nonsense\"}")]
    [InlineData("{\"other\":1}")]
    // §N3: a client-supplied key with a different casing must NOT satisfy the
    // gate (exact, case-sensitive read). Includes the legacy/attack shape where
    // the request-side stamp left both keys: the canonical false must win.
    [InlineData("{\"IncludeCompensation\":true,\"includeCompensation\":false}")]
    [InlineData("{\"IncludeCompensation\":true}")]
    [InlineData("{\"INCLUDECOMPENSATION\":true}")]
    [InlineData("{\"includecompensation\":true}")]
    public void Apply_WhenFlagNotExplicitlyTrue_StripsSalaryData(string parametersJson)
    {
        var payload = BuildPayloadWithCompensation();

        var result = JobProfileCompensationGate.Apply(payload, Parse(parametersJson));

        Assert.Null(result.Profile.Compensation);
        Assert.Null(result.Profile.MarketSalaryReference);
        // Non-salary content is untouched.
        Assert.Equal("Gerente de Desarrollo", result.Profile.Title);
        Assert.Equal(payload.GeneratedAtUtc, result.GeneratedAtUtc);
    }

    [Fact]
    public void Apply_WhenFlagExplicitlyTrue_PreservesSalaryData()
    {
        var payload = BuildPayloadWithCompensation();

        var result = JobProfileCompensationGate.Apply(
            payload, Parse("{\"includeCompensation\":true}"));

        Assert.NotNull(result.Profile.Compensation);
        Assert.Equal(120_000m, result.Profile.Compensation!.BaseAmount);
        Assert.Equal("Top 25% del mercado regional.", result.Profile.MarketSalaryReference);
    }

    [Fact]
    public void Apply_StrippedPayload_ProducesNoCompensationSectionInDocument()
    {
        var stripped = JobProfileCompensationGate.Apply(
            BuildPayloadWithCompensation(), Parse("{}"));

        var document = new JobProfileDocumentMapper().Map(stripped);

        Assert.DoesNotContain(document.Sections, section => section.Title == "Compensación");
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static JobProfilePrintResponse BuildPayloadWithCompensation()
    {
        var profile = new JobProfileResponse(
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
            MarketSalaryReference: "Top 25% del mercado regional.",
            ValuationNotes: null,
            EffectiveFromUtc: null,
            EffectiveToUtc: null,
            IsActive: true,
            Requirements: Array.Empty<JobProfileRequirementResponse>(),
            Functions: Array.Empty<JobProfileFunctionResponse>(),
            Relations: Array.Empty<JobProfileRelationResponse>(),
            Competencies: Array.Empty<JobProfileCompetencyResponse>(),
            Trainings: Array.Empty<JobProfileTrainingResponse>(),
            Compensation: new JobProfileCompensationResponse(
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
            Benefits: Array.Empty<JobProfileBenefitResponse>(),
            WorkingConditions: Array.Empty<JobProfileWorkingConditionResponse>(),
            DependentPositions: Array.Empty<JobProfileDependentPositionResponse>(),
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedAtUtc: null);

        return new JobProfilePrintResponse(profile, new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc));
    }
}
