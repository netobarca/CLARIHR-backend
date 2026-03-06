using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.UnitTests;

public sealed class JobProfileDomainTests
{
    [Fact]
    public void JobProfile_Create_ShouldNormalizeCodeAndTitle()
    {
        var profile = JobProfile.Create("  jp-001 ", "  Analista de Nomina  ");

        Assert.Equal("jp-001", profile.Code);
        Assert.Equal("JP-001", profile.NormalizedCode);
        Assert.Equal("Analista de Nomina", profile.Title);
        Assert.Equal("ANALISTA DE NOMINA", profile.NormalizedTitle);
    }

    [Fact]
    public void JobProfile_UpdateCore_WithInitialSetup_ShouldKeepVersionWhenBumpDisabled()
    {
        var profile = JobProfile.Create("JP-001", "Analista");

        var initialVersion = profile.Version;
        var initialToken = profile.ConcurrencyToken;

        profile.UpdateCore(
            "JP-001",
            "Analista",
            objective: "Objetivo",
            orgUnitId: null,
            reportsToJobProfileId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: "Responsabilidades",
            benefitsSummary: null,
            workingConditionSummary: null,
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null,
            bumpVersion: false);

        Assert.Equal(initialVersion, profile.Version);
        Assert.Equal(initialToken, profile.ConcurrencyToken);
    }

    [Fact]
    public void JobProfile_UpdateCore_ShouldRefreshConcurrencyToken()
    {
        var profile = JobProfile.Create("JP-001", "Analista");
        var beforeToken = profile.ConcurrencyToken;

        profile.UpdateCore(
            "JP-001",
            "Analista Senior",
            objective: "Objetivo",
            orgUnitId: null,
            reportsToJobProfileId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: "Responsabilidades",
            benefitsSummary: null,
            workingConditionSummary: null,
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null);

        Assert.Equal(2, profile.Version);
        Assert.NotEqual(beforeToken, profile.ConcurrencyToken);
    }

    [Fact]
    public void JobProfile_Publish_WhenMissingMinimumData_ShouldThrow()
    {
        var profile = JobProfile.Create("JP-001", "Analista");

        Assert.Throws<InvalidOperationException>(() => profile.Publish());
    }

    [Fact]
    public void JobProfile_Publish_WithMinimumData_ShouldTransitionToPublished()
    {
        var profile = JobProfile.Create("JP-001", "Analista");

        profile.UpdateCore(
            "JP-001",
            "Analista",
            objective: "Objetivo del puesto",
            orgUnitId: null,
            reportsToJobProfileId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: "Responsabilidades generales",
            benefitsSummary: null,
            workingConditionSummary: null,
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null,
            bumpVersion: false);

        profile.ReplaceRequirements([
            JobProfileRequirement.Create(
                JobRequirementType.Experience,
                catalogItemId: null,
                catalogItem: null,
                description: "3 anios",
                sortOrder: 1)
        ]);

        profile.ReplaceFunctions([
            JobProfileFunction.Create(JobFunctionType.General, "Ejecutar procesos de nomina", sortOrder: 1)
        ]);

        var beforeToken = profile.ConcurrencyToken;

        profile.Publish();

        Assert.Equal(JobProfileStatus.Published, profile.Status);
        Assert.True(profile.IsActive);
        Assert.NotEqual(beforeToken, profile.ConcurrencyToken);
    }

    [Fact]
    public void JobProfileDependencyAnalyzer_WouldCreateReportsToCycle_ShouldReturnTrue()
    {
        var root = new JobProfileDependencyNodeData(1, Guid.NewGuid(), null, []);
        var child = new JobProfileDependencyNodeData(2, Guid.NewGuid(), 1, []);
        var leaf = new JobProfileDependencyNodeData(3, Guid.NewGuid(), 2, []);

        var graph = new[] { root, child, leaf }.ToDictionary(static node => node.InternalId);

        var createsCycle = JobProfileDependencyAnalyzer.WouldCreateReportsToCycle(
            sourceInternalId: 1,
            candidateReportsToInternalId: 3,
            graph);

        Assert.True(createsCycle);
    }

    [Fact]
    public void JobProfileDependencyAnalyzer_WouldCreateDependentCycle_ShouldReturnTrue()
    {
        var a = new JobProfileDependencyNodeData(1, Guid.NewGuid(), null, [2]);
        var b = new JobProfileDependencyNodeData(2, Guid.NewGuid(), null, [3]);
        var c = new JobProfileDependencyNodeData(3, Guid.NewGuid(), null, []);

        var createsCycle = JobProfileDependencyAnalyzer.WouldCreateDependentCycle(
            sourceInternalId: 3,
            candidateDependentInternalIds: [1],
            graph: [a, b, c]);

        Assert.True(createsCycle);
    }
}
