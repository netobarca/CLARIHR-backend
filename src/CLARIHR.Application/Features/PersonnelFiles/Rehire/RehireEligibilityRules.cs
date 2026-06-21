using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Pure, table-free eligibility rules for the employee-rehire flow (RN-02, RN-06, RN-14,
/// D-04/D-12/D-13). Mirrors <see cref="EmploymentAssignmentRules"/>: the handler gathers the
/// file/profile facts + caller permission + manual prior-period-closure confirmation, then calls
/// <see cref="Evaluate"/>, so every branch is unit-testable without a database. There is no
/// minimum-wait validation between retirement and rehire (RN-15 / D-12).
/// </summary>
internal static class RehireEligibilityRules
{
    internal sealed record Input(
        PersonnelFileRecordType RecordType,
        PersonnelFileLifecycleStatus LifecycleStatus,
        bool IsRetired,
        bool IsRehireBlocked,
        bool CallerHasAuthorizeRehirePermission,
        bool AuthorizationReasonProvided,
        bool PriorPeriodClosureConfirmed);

    internal static Result Evaluate(Input input)
    {
        // RN-02 — rehire only applies to employee files.
        if (input.RecordType != PersonnelFileRecordType.Employee)
        {
            return Result.Failure(RehireErrors.NotAnEmployee);
        }

        // RN-02 — the file must be a retired (completed + has a retirement date) employee. The retirement
        // date (kept on the profile) is the canonical "retired" signal now that IsEmploymentActive is gone.
        if (input.LifecycleStatus != PersonnelFileLifecycleStatus.Completed || !input.IsRetired)
        {
            return Result.Failure(RehireErrors.NotRetired);
        }

        // RN-14 / D-13 / D-17 — the prior period must be confirmed closed/settled (manual until payroll exists).
        if (!input.PriorPeriodClosureConfirmed)
        {
            return Result.Failure(RehireErrors.PriorPeriodOpen);
        }

        // RN-06 / D-04 — a "not rehireable" file needs an authorized override carrying a justification.
        if (input.IsRehireBlocked &&
            !(input.CallerHasAuthorizeRehirePermission && input.AuthorizationReasonProvided))
        {
            return Result.Failure(RehireErrors.RequiresAuthorization);
        }

        return Result.Success();
    }
}
