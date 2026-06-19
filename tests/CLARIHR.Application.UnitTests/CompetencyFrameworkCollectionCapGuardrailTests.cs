using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Application.Features.CompetencyFramework.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// R1 (re-audit 2026-06-06): the CompetencyFramework collection-replace mutations bound their collection
/// size at the validator boundary (mirroring the <c>ReplaceCurrentUserSocialLinks</c>
/// <c>.Must(items.Count &lt;= N)</c> convention), so one privileged request cannot submit an unbounded
/// matrix / behavior set and drive a huge in-memory build + bulk insert. The per-item N+1 itself was
/// already removed in F1/F4 — this guards the *count*. Drift-proof: each boundary is built from the
/// <see cref="CompetencyFrameworkValidationRules"/> constant (Max+1 rejected, Max accepted), so removing
/// a cap rule or moving a bound off the constant turns one of these red.
/// </summary>
public sealed class CompetencyFrameworkCollectionCapGuardrailTests
{
    [Fact]
    public void MatrixValidator_WhenItemsExceedMax_ShouldReportItemsCountViolation()
    {
        var validator = new UpdateJobProfileCompetencyMatrixCommandValidator();

        var result = validator.Validate(new UpdateJobProfileCompetencyMatrixCommand(
            Guid.NewGuid(),
            BuildMatrixItems(CompetencyFrameworkValidationRules.MaxMatrixItems + 1),
            Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Items");
    }

    [Fact]
    public void MatrixValidator_WhenItemsAtMax_ShouldBeValid()
    {
        var validator = new UpdateJobProfileCompetencyMatrixCommandValidator();

        var result = validator.Validate(new UpdateJobProfileCompetencyMatrixCommand(
            Guid.NewGuid(),
            BuildMatrixItems(CompetencyFrameworkValidationRules.MaxMatrixItems),
            Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void MatrixItemValidator_WhenConductsExceedMax_ShouldReportConductIdsCountViolation()
    {
        var validator = new JobProfileCompetencyMatrixItemInputValidator();

        var result = validator.Validate(BuildItem(
            conductCount: CompetencyFrameworkValidationRules.MaxConductsPerMatrixItem + 1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "ConductIds");
    }

    [Fact]
    public void MatrixItemValidator_WhenConductsAtMax_ShouldBeValid()
    {
        var validator = new JobProfileCompetencyMatrixItemInputValidator();

        var result = validator.Validate(BuildItem(
            conductCount: CompetencyFrameworkValidationRules.MaxConductsPerMatrixItem));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void BehaviorsValidator_WhenBehaviorsExceedMax_ShouldReportBehaviorsCountViolation()
    {
        var validator = new UpdateCompetencyConductBehaviorsCommandValidator();

        var result = validator.Validate(new UpdateCompetencyConductBehaviorsCommand(
            Guid.NewGuid(),
            BuildBehaviors(CompetencyFrameworkValidationRules.MaxBehaviorsPerConduct + 1),
            Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Behaviors");
    }

    [Fact]
    public void BehaviorsValidator_WhenBehaviorsAtMax_ShouldBeValid()
    {
        var validator = new UpdateCompetencyConductBehaviorsCommandValidator();

        var result = validator.Validate(new UpdateCompetencyConductBehaviorsCommand(
            Guid.NewGuid(),
            BuildBehaviors(CompetencyFrameworkValidationRules.MaxBehaviorsPerConduct),
            Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    private static IReadOnlyCollection<JobProfileCompetencyMatrixItemInput> BuildMatrixItems(int count) =>
        Enumerable.Range(0, count)
            // Each item now requires at least one conduct, so the items-cap cases stay otherwise-valid.
            .Select(_ => BuildItem(conductCount: 1))
            .ToArray();

    private static JobProfileCompetencyMatrixItemInput BuildItem(int conductCount) =>
        new(
            Guid.NewGuid(),
            Enumerable.Range(0, conductCount).Select(_ => Guid.NewGuid()).ToArray(),
            ExpectedEvidence: null,
            SortOrder: 0);

    private static IReadOnlyCollection<CompetencyConductBehaviorInput> BuildBehaviors(int count) =>
        Enumerable.Range(0, count)
            .Select(_ => new CompetencyConductBehaviorInput(Guid.NewGuid(), Notes: null, SortOrder: 0))
            .ToArray();
}
