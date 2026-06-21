using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the pure employee-rehire eligibility engine <see cref="RehireEligibilityRules"/>:
/// employee-only (RN-02), retired-only (RN-02), prior-period closure (RN-14/D-13), and the
/// "not rehireable" authorized override (RN-06/D-04). Mirrors <see cref="EmploymentAssignmentRulesTests"/>.
/// </summary>
public sealed class RehireEligibilityRulesTests
{
    private static RehireEligibilityRules.Input Input(
        PersonnelFileRecordType recordType = PersonnelFileRecordType.Employee,
        PersonnelFileLifecycleStatus lifecycleStatus = PersonnelFileLifecycleStatus.Completed,
        bool isRetired = true,
        bool isRehireBlocked = false,
        bool callerHasAuthorizeRehirePermission = false,
        bool authorizationReasonProvided = false,
        bool priorPeriodClosureConfirmed = true) =>
        new(
            recordType,
            lifecycleStatus,
            isRetired,
            isRehireBlocked,
            callerHasAuthorizeRehirePermission,
            authorizationReasonProvided,
            priorPeriodClosureConfirmed);

    [Fact]
    public void Evaluate_RetiredEmployee_NotBlocked_Succeeds()
    {
        var result = RehireEligibilityRules.Evaluate(Input());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_NonEmployee_FailsNotAnEmployee()
    {
        var result = RehireEligibilityRules.Evaluate(Input(recordType: PersonnelFileRecordType.Candidate));

        Assert.True(result.IsFailure);
        Assert.Equal(RehireErrors.NotAnEmployee, result.Error);
    }

    [Theory]
    [InlineData(PersonnelFileLifecycleStatus.Draft, true)]      // Draft file (retired flag irrelevant) → not retired by lifecycle
    [InlineData(PersonnelFileLifecycleStatus.Completed, false)] // Completed but still active (no retirement date) → not retired
    public void Evaluate_DraftOrActive_FailsNotRetired(PersonnelFileLifecycleStatus status, bool isRetired)
    {
        var result = RehireEligibilityRules.Evaluate(Input(lifecycleStatus: status, isRetired: isRetired));

        Assert.True(result.IsFailure);
        Assert.Equal(RehireErrors.NotRetired, result.Error);
    }

    [Fact]
    public void Evaluate_PriorPeriodNotConfirmed_FailsPriorPeriodOpen()
    {
        var result = RehireEligibilityRules.Evaluate(Input(priorPeriodClosureConfirmed: false));

        Assert.True(result.IsFailure);
        Assert.Equal(RehireErrors.PriorPeriodOpen, result.Error);
    }

    [Fact]
    public void Evaluate_Blocked_WithoutPermission_FailsRequiresAuthorization()
    {
        // Reason provided but no permission — still blocked.
        var result = RehireEligibilityRules.Evaluate(Input(isRehireBlocked: true, authorizationReasonProvided: true));

        Assert.True(result.IsFailure);
        Assert.Equal(RehireErrors.RequiresAuthorization, result.Error);
    }

    [Fact]
    public void Evaluate_Blocked_WithPermission_WithoutReason_FailsRequiresAuthorization()
    {
        var result = RehireEligibilityRules.Evaluate(Input(isRehireBlocked: true, callerHasAuthorizeRehirePermission: true));

        Assert.True(result.IsFailure);
        Assert.Equal(RehireErrors.RequiresAuthorization, result.Error);
    }

    [Fact]
    public void Evaluate_Blocked_WithPermissionAndReason_Succeeds()
    {
        var result = RehireEligibilityRules.Evaluate(
            Input(isRehireBlocked: true, callerHasAuthorizeRehirePermission: true, authorizationReasonProvided: true));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_Blocked_ButPriorPeriodOpen_PrioritizesPriorPeriodOpen()
    {
        // Closure is validated before the rehire-block override.
        var result = RehireEligibilityRules.Evaluate(Input(isRehireBlocked: true, priorPeriodClosureConfirmed: false));

        Assert.True(result.IsFailure);
        Assert.Equal(RehireErrors.PriorPeriodOpen, result.Error);
    }
}
