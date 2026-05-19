using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Domain.PositionSlots;

namespace CLARIHR.Application.UnitTests;

// §PS5 guardrail: Application maps domain validation by stable error code, not
// by exception Message text. Two-layer guard:
//   1. Behavioral: every PositionSlotDomainErrorCode dispatches to a known
//      Application Error (and the mapping is exhaustive — no enum value reaches
//      the `_ => throw` arm).
//   2. Structural: PositionSlotAdministration.cs contains zero
//      `exception.Message.Contains` calls (the pre-§PS5 fragile pattern).
public sealed class PositionSlotDomainErrorMappingGuardrailsTests
{
    public static TheoryData<PositionSlotDomainErrorCode, string> CodeToErrorCases() => new()
    {
        { PositionSlotDomainErrorCode.EffectiveFromRequired, PositionSlotErrors.EffectiveDatesInvalid.Code },
        { PositionSlotDomainErrorCode.EffectiveDateRangeInvalid, PositionSlotErrors.EffectiveDatesInvalid.Code },
        { PositionSlotDomainErrorCode.MaxEmployeesInvalid, PositionSlotErrors.CapacityRuleViolation.Code },
        { PositionSlotDomainErrorCode.OccupiedEmployeesNegative, PositionSlotErrors.CapacityRuleViolation.Code },
        { PositionSlotDomainErrorCode.OccupiedExceedsCapacity, PositionSlotErrors.CapacityRuleViolation.Code },
        { PositionSlotDomainErrorCode.SuspendedOccupancyConflict, PositionSlotErrors.SuspendedOccupancyConflict.Code },
        { PositionSlotDomainErrorCode.DirectDependencySelfReference, PositionSlotErrors.DependencySelfReference.Code },
        { PositionSlotDomainErrorCode.FunctionalDependencySelfReference, PositionSlotErrors.DependencySelfReference.Code },
    };

    [Theory]
    [MemberData(nameof(CodeToErrorCases))]
    public void MapDomainValidation_ShouldMapEveryCodeToExpectedError(PositionSlotDomainErrorCode code, string expectedErrorCode)
    {
        var exception = new PositionSlotDomainException(code, "irrelevant — mapping is by code, not message");

        var mapped = PositionSlotCommandSupport.MapDomainValidation(exception);

        Assert.Equal(expectedErrorCode, mapped.Code);
    }

    [Fact]
    public void MapDomainValidation_ShouldCoverEveryEnumValue_NoneReachesDefaultArm()
    {
        // Belt-and-suspenders: if a new code is added to the enum, this test
        // fails (the `_ => throw` arm in MapByCode triggers ArgumentOutOfRangeException),
        // forcing the contributor to add the mapping.
        foreach (var code in Enum.GetValues<PositionSlotDomainErrorCode>())
        {
            var exception = new PositionSlotDomainException(code, "synthetic");

            var mapped = PositionSlotCommandSupport.MapDomainValidation(exception);

            Assert.NotNull(mapped);
            Assert.False(string.IsNullOrWhiteSpace(mapped.Code), $"Code {code} mapped to an empty error code.");
        }
    }

    [Fact]
    public void MapDomainValidation_WhenExceptionIsUntyped_ShouldFallBackToCapacityRuleViolation()
    {
        // Defensive: a non-typed InvalidOperationException (a domain throw that
        // forgot to use PositionSlotDomainException, or a leak from a 3rd-party)
        // still produces a validation error rather than a 500.
        var mapped = PositionSlotCommandSupport.MapDomainValidation(new InvalidOperationException("not a domain code"));

        Assert.Equal(PositionSlotErrors.CapacityRuleViolation.Code, mapped.Code);
    }

    [Fact]
    public void PositionSlotAdministration_ShouldNotMatchDomainExceptionsByMessageText()
    {
        // Drift-proof: a future "quick fix" that resurrects Message.Contains
        // (the §PS5 anti-pattern) fails here. Scoped to the file, not whole repo —
        // other features may legitimately inspect text in non-domain contexts.
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CLARIHR.Application",
            "Features",
            "PositionSlots",
            "PositionSlotAdministration.cs");

        var content = File.ReadAllText(path);

        Assert.DoesNotContain("exception.Message.Contains", content, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "CLARIHR.Application")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test output path.");
    }
}
