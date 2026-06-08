using CLARIHR.Application.Features.Locations.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// WCT-A (Locations family alignment): pins each dup-key constraint helper to its own single-sourced
/// index name and asserts it rejects the others'. The Create/Update/Patch handlers across the family
/// (LocationLevels order, WorkCenterTypes / LocationGroups / WorkCenters code) map a concurrent dup-key
/// write to a clean 409 via <c>catch (UniqueConstraintViolationException) when (Is*Conflict(name))</c>;
/// because the EF configs single-source the very same constants in <c>HasDatabaseName</c>, this test is
/// what guarantees the <c>when</c> filter actually matches the real index name. A copy-paste mis-wiring
/// (e.g. a helper comparing against the wrong constant) would silently let the 23505 escape as a 500
/// again — exactly the regression WCT-A closed.
/// </summary>
public sealed class LocationConstraintViolationsTests
{
    [Fact]
    public void EachHelper_AcceptsItsOwnIndexNameAndRejectsTheOthers()
    {
        // Accepts its own single-sourced index name.
        Assert.True(LocationConstraintViolations.IsLevelOrderConflict(LocationValidationRules.LevelOrderUniqueConstraintName));
        Assert.True(LocationConstraintViolations.IsWorkCenterTypeCodeConflict(LocationValidationRules.WorkCenterTypeCodeUniqueConstraintName));
        Assert.True(LocationConstraintViolations.IsGroupCodeConflict(LocationValidationRules.GroupCodeUniqueConstraintName));
        Assert.True(LocationConstraintViolations.IsWorkCenterCodeConflict(LocationValidationRules.WorkCenterCodeUniqueConstraintName));

        // Rejects every sibling's index name (guards against a copy-paste mis-wiring between the four).
        Assert.False(LocationConstraintViolations.IsGroupCodeConflict(LocationValidationRules.WorkCenterCodeUniqueConstraintName));
        Assert.False(LocationConstraintViolations.IsGroupCodeConflict(LocationValidationRules.WorkCenterTypeCodeUniqueConstraintName));
        Assert.False(LocationConstraintViolations.IsWorkCenterCodeConflict(LocationValidationRules.GroupCodeUniqueConstraintName));
        Assert.False(LocationConstraintViolations.IsWorkCenterCodeConflict(LocationValidationRules.WorkCenterTypeCodeUniqueConstraintName));
        Assert.False(LocationConstraintViolations.IsWorkCenterTypeCodeConflict(LocationValidationRules.GroupCodeUniqueConstraintName));
        Assert.False(LocationConstraintViolations.IsLevelOrderConflict(LocationValidationRules.GroupCodeUniqueConstraintName));
    }

    [Fact]
    public void Helpers_RejectNullAndUnknownConstraintNames()
    {
        Assert.False(LocationConstraintViolations.IsGroupCodeConflict(null));
        Assert.False(LocationConstraintViolations.IsWorkCenterCodeConflict(null));
        Assert.False(LocationConstraintViolations.IsWorkCenterTypeCodeConflict("uq_something_unrelated"));
        Assert.False(LocationConstraintViolations.IsLevelOrderConflict("pk_location_levels"));
    }

    [Fact]
    public void DupKeyIndexNameConstants_AreAllDistinct()
    {
        var names = new[]
        {
            LocationValidationRules.LevelOrderUniqueConstraintName,
            LocationValidationRules.WorkCenterTypeCodeUniqueConstraintName,
            LocationValidationRules.GroupCodeUniqueConstraintName,
            LocationValidationRules.WorkCenterCodeUniqueConstraintName,
        };

        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }
}
