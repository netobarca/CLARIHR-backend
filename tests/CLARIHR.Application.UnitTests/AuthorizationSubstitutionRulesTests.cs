using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the pure authorization-substitution rules (D-04/D-06/D-07): single effective
/// substitution per titular (overlap blocks), substitute availability (the substitute may not be the titular
/// of an overlapping active substitution), and the inactive/self carve-outs. Mirrors
/// <c>EmploymentAssignmentRulesTests</c> — no database, the handler supplies the loaded collections.
/// </summary>
public sealed class AuthorizationSubstitutionRulesTests
{
    private static readonly DateTime Jan1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Jan31 = new(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Feb1 = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Feb28 = new(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc);

    private static AuthorizationSubstitutionRules.Candidate ActiveCandidate(DateTime start, DateTime end, Guid? publicId = null) =>
        new(publicId, start, end, IsActive: true);

    private static AuthorizationSubstitutionRules.ExistingSubstitution Existing(DateTime start, DateTime end, bool isActive, Guid? publicId = null) =>
        new(publicId ?? Guid.NewGuid(), start, end, isActive);

    [Fact]
    public void Evaluate_InactiveCandidate_Succeeds_WithoutApplyingOverlapRules()
    {
        var candidate = new AuthorizationSubstitutionRules.Candidate(null, Jan1, Jan31, IsActive: false);
        var overlappingActive = new[] { Existing(Jan1, Jan31, isActive: true) };

        var result = AuthorizationSubstitutionRules.Evaluate(candidate, overlappingActive, []);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_NoOtherSubstitutions_Succeeds()
    {
        var result = AuthorizationSubstitutionRules.Evaluate(ActiveCandidate(Jan1, Jan31), [], []);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_OverlapsAnotherActiveTitularSubstitution_FailsPeriodOverlap()
    {
        var titular = new[] { Existing(Jan1, Jan31, isActive: true) };

        var result = AuthorizationSubstitutionRules.Evaluate(ActiveCandidate(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), Feb28), titular, []);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthorizationSubstitutionErrors.PeriodOverlap, result.Error);
    }

    [Fact]
    public void Evaluate_TouchingBoundaryWithActiveTitularSubstitution_FailsPeriodOverlap()
    {
        // Overlap is inclusive: a candidate starting on the day the prior one ends still collides.
        var titular = new[] { Existing(Jan1, Jan31, isActive: true) };

        var result = AuthorizationSubstitutionRules.Evaluate(ActiveCandidate(Jan31, Feb28), titular, []);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthorizationSubstitutionErrors.PeriodOverlap, result.Error);
    }

    [Fact]
    public void Evaluate_NonOverlappingFutureTitularSubstitution_Succeeds()
    {
        // Scheduling: a future, non-overlapping substitution is allowed (RF-006 effective-state model).
        var titular = new[] { Existing(Jan1, Jan31, isActive: true) };

        var result = AuthorizationSubstitutionRules.Evaluate(ActiveCandidate(Feb1, Feb28), titular, []);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_OverlapsOnlyInactiveTitularSubstitution_Succeeds()
    {
        var titular = new[] { Existing(Jan1, Jan31, isActive: false) };

        var result = AuthorizationSubstitutionRules.Evaluate(ActiveCandidate(Jan1, Jan31), titular, []);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_ExcludesItselfByPublicId_WhenEditingExisting()
    {
        var publicId = Guid.NewGuid();
        var titular = new[] { Existing(Jan1, Jan31, isActive: true, publicId: publicId) };

        var result = AuthorizationSubstitutionRules.Evaluate(ActiveCandidate(Jan1, Jan31, publicId), titular, []);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_SubstituteIsTitularOfOverlappingActiveSubstitution_FailsUnavailable()
    {
        var substituteAsTitular = new[] { Existing(Jan1, Jan31, isActive: true) };

        var result = AuthorizationSubstitutionRules.Evaluate(ActiveCandidate(new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), Feb28), [], substituteAsTitular);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthorizationSubstitutionErrors.SubstituteUnavailable, result.Error);
    }

    [Fact]
    public void Evaluate_SubstituteIsTitularOfNonOverlappingSubstitution_Succeeds()
    {
        var substituteAsTitular = new[] { Existing(Jan1, Jan31, isActive: true) };

        var result = AuthorizationSubstitutionRules.Evaluate(ActiveCandidate(Feb1, Feb28), [], substituteAsTitular);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_SubstituteIsTitularOfOverlappingButInactiveSubstitution_Succeeds()
    {
        var substituteAsTitular = new[] { Existing(Jan1, Jan31, isActive: false) };

        var result = AuthorizationSubstitutionRules.Evaluate(ActiveCandidate(Jan1, Jan31), [], substituteAsTitular);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_TitularOverlapTakesPrecedenceOverSubstituteUnavailable()
    {
        var titular = new[] { Existing(Jan1, Feb28, isActive: true) };
        var substituteAsTitular = new[] { Existing(Jan1, Feb28, isActive: true) };

        var result = AuthorizationSubstitutionRules.Evaluate(ActiveCandidate(Jan1, Jan31), titular, substituteAsTitular);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthorizationSubstitutionErrors.PeriodOverlap, result.Error);
    }

    [Theory]
    [InlineData("2026-01-01", "2026-01-31", "2026-02-01", "2026-02-28", false)] // disjoint, candidate after
    [InlineData("2026-02-01", "2026-02-28", "2026-01-01", "2026-01-31", false)] // disjoint, candidate before
    [InlineData("2026-01-01", "2026-01-31", "2026-01-31", "2026-02-28", true)]  // touch at boundary (inclusive)
    [InlineData("2026-01-01", "2026-01-31", "2026-01-10", "2026-01-20", true)]  // candidate contains other
    [InlineData("2026-01-10", "2026-01-20", "2026-01-01", "2026-01-31", true)]  // other contains candidate
    public void Evaluate_OverlapBoundaries_MatchExpectation(string candStart, string candEnd, string otherStart, string otherEnd, bool shouldBlock)
    {
        var candidate = ActiveCandidate(DateTime.Parse(candStart).ToUniversalTime(), DateTime.Parse(candEnd).ToUniversalTime());
        var titular = new[] { Existing(DateTime.Parse(otherStart).ToUniversalTime(), DateTime.Parse(otherEnd).ToUniversalTime(), isActive: true) };

        var result = AuthorizationSubstitutionRules.Evaluate(candidate, titular, []);

        Assert.Equal(!shouldBlock, result.IsSuccess);
    }
}
