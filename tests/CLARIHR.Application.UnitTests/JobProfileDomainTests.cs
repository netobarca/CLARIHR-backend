using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.UnitTests;

public sealed class JobProfileDomainTests
{
    [Fact]
    public void JobProfile_Create_ShouldNormalizeCodeAndTitle()
    {
        var profile = JobProfile.Create("  jp-001 ", "  Analista de Nomina  ");

        Assert.Equal("JP-001", profile.Code);
        Assert.Equal("JP-001", profile.NormalizedCode);
        Assert.Equal("Analista de Nomina", profile.Title);
        Assert.Equal("ANALISTA DE NOMINA", profile.NormalizedTitle);
    }

    /// <summary>
    /// H-09 — was <c>ShouldKeepVersionWhenBumpDisabled</c>, asserting that the (now removed)
    /// <c>bumpVersion: false</c> flag left version AND token untouched. The flag existed only so creation
    /// would not land on version 2. Now the creation path leaves the profile at version <b>0</b> — no
    /// approved revision yet — while the token rotates like on any other write.
    /// </summary>
    [Fact]
    public void JobProfile_UpdateCore_OnFreshProfile_ShouldStayAtVersionZero()
    {
        var profile = JobProfile.Create("JP-001", "Analista");

        Assert.Equal(0, profile.Version);
        var initialToken = profile.ConcurrencyToken;

        profile.UpdateCore(
            "JP-001",
            "Analista",
            objective: "Objetivo",
            orgUnitId: 1,
            reportsToJobProfileId: null,
            positionCategoryId: null,
            strategicObjectiveCatalogItemId: null,
            assignedWorkEquipmentCatalogItemId: null,
            responsibilityCatalogItemId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: "Responsabilidades",
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null);

        Assert.Equal(0, profile.Version);
        Assert.NotEqual(initialToken, profile.ConcurrencyToken);
    }

    [Fact]
    public void JobProfile_UpdateCore_WithoutOrgUnit_ShouldThrow()
    {
        var profile = JobProfile.Create("JP-001", "Analista");

        Assert.Throws<ArgumentOutOfRangeException>(() => profile.UpdateCore(
            "JP-001",
            "Analista",
            objective: "Objetivo",
            orgUnitId: 0,
            reportsToJobProfileId: null,
            positionCategoryId: null,
            strategicObjectiveCatalogItemId: null,
            assignedWorkEquipmentCatalogItemId: null,
            responsibilityCatalogItemId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: "Responsabilidades",
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null));
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
            orgUnitId: 1,
            reportsToJobProfileId: null,
            positionCategoryId: null,
            strategicObjectiveCatalogItemId: null,
            assignedWorkEquipmentCatalogItemId: null,
            responsibilityCatalogItemId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: "Responsabilidades",
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null);

        // H-09 — asserted `Version == 2` before. Editing the descriptor is not a revision of it, so the
        // number must not move; the token rotation is the half that must survive, because both used to live
        // inside the same `if (bumpVersion)` block and dropping them together would have killed optimistic
        // concurrency on the profile core without a single test noticing.
        Assert.Equal(0, profile.Version);
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
            orgUnitId: 1,
            reportsToJobProfileId: null,
            positionCategoryId: null,
            strategicObjectiveCatalogItemId: null,
            assignedWorkEquipmentCatalogItemId: null,
            responsibilityCatalogItemId: null,
            decisionScope: null,
            assignedResources: null,
            responsibilities: "Responsabilidades generales",
            marketSalaryReference: null,
            valuationNotes: null,
            effectiveFromUtc: null,
            effectiveToUtc: null);

        profile.ReplaceRequirements([
            JobProfileRequirement.Create(
                JobRequirementType.Experience,
                requirementTypeCatalogItemId: null,
                catalogItemId: null,
                catalogItem: null,
                description: "3 anios",
                sortOrder: 1)
        ]);

        profile.ReplaceFunctions([
            JobProfileFunction.Create(
                JobFunctionType.General,
                frequencyCatalogItemId: null,
                "Ejecutar procesos de nomina",
                sortOrder: 1)
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
