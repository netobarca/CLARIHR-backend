using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Application.Features.CompetencyFramework.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// R1 (re-audit 2026-06-06): the CompetencyFramework mutations bound their collection size at the
/// validator boundary (mirroring the <c>ReplaceCurrentUserSocialLinks</c> <c>.Must(items.Count &lt;= N)</c>
/// convention) so one privileged request cannot submit an unbounded conduct / behavior set and drive a
/// huge in-memory build + bulk insert. Drift-proof: each boundary is built from the
/// <see cref="CompetencyFrameworkValidationRules"/> constant (Max+1 rejected, Max accepted), so removing a
/// cap rule or moving a bound off the constant turns one of these red.
///
/// Note (2026-06-18): the competency matrix moved from a single bulk replace to per-item CRUD, so the
/// per-profile <see cref="CompetencyFrameworkValidationRules.MaxMatrixItems"/> cap is now enforced in the
/// Add handler (a pre-insert count check returning <c>JOB_PROFILE_COMPETENCY_MATRIX_ITEM_LIMIT_REACHED</c>),
/// not at a validator boundary; only the per-item <see cref="CompetencyFrameworkValidationRules.MaxConductsPerMatrixItem"/>
/// cap remains a validator rule and is guarded here.
/// </summary>
public sealed class CompetencyFrameworkCollectionCapGuardrailTests
{
    [Fact]
    public void MatrixItemValidator_WhenConductsExceedMax_ShouldReportConductIdsCountViolation()
    {
        var validator = new AddJobProfileCompetencyMatrixItemCommandValidator();

        var result = validator.Validate(BuildAddCommand(
            conductCount: CompetencyFrameworkValidationRules.MaxConductsPerMatrixItem + 1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "ConductIds");
    }

    [Fact]
    public void MatrixItemValidator_WhenConductsAtMax_ShouldBeValid()
    {
        var validator = new AddJobProfileCompetencyMatrixItemCommandValidator();

        var result = validator.Validate(BuildAddCommand(
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

    private static AddJobProfileCompetencyMatrixItemCommand BuildAddCommand(int conductCount) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Enumerable.Range(0, conductCount).Select(_ => Guid.NewGuid()).ToArray(),
            ExpectedEvidence: null,
            SortOrder: 0);

    private static IReadOnlyCollection<CompetencyConductBehaviorInput> BuildBehaviors(int count) =>
        Enumerable.Range(0, count)
            .Select(_ => new CompetencyConductBehaviorInput(Guid.NewGuid(), Notes: null, SortOrder: 0))
            .ToArray();
}
