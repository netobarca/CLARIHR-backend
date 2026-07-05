using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Dedicated errors for definitive-retirement requests ("retiro definitivo"). Each code requires an EN + ES
/// resource entry (parity: <c>BackendMessageLocalizationTests</c>). Field-level validation (required codes,
/// note lengths, required dates) is handled by the validators (400) and is NOT here.
/// </summary>
internal static class RetirementErrors
{
    public static readonly Error RequestNotFound = new(
        "RETIREMENT_REQUEST_NOT_FOUND",
        "The retirement request was not found.", ErrorType.NotFound);

    public static readonly Error EmployeeNotEligible = new(
        "RETIREMENT_REQUEST_EMPLOYEE_NOT_ELIGIBLE",
        "The employee is not eligible for a retirement request (it must be an active, completed employee file without a retirement in force).", ErrorType.UnprocessableEntity);

    public static readonly Error RequestAlreadyOpen = new(
        "RETIREMENT_REQUEST_ALREADY_OPEN",
        "The employee already has an open retirement request (SOLICITADA or AUTORIZADA).", ErrorType.UnprocessableEntity);

    public static readonly Error RequesterInvalid = new(
        "RETIREMENT_REQUEST_REQUESTER_INVALID",
        "The requester must reference a valid, active personnel file of the company.", ErrorType.UnprocessableEntity);

    public static readonly Error DateIncoherent = new(
        "RETIREMENT_REQUEST_DATE_INCOHERENT",
        "The request date cannot be in the future, the retirement date cannot precede the hire date, and no active assignment or contract may start after the retirement date.", ErrorType.UnprocessableEntity);

    public static readonly Error StateRuleViolation = new(
        "RETIREMENT_REQUEST_STATE_RULE_VIOLATION",
        "The retirement request is not in a state that allows this operation.", ErrorType.UnprocessableEntity);

    public static readonly Error ResolutionTargetInvalid = new(
        "RETIREMENT_RESOLUTION_TARGET_INVALID",
        "The resolution target must be AUTORIZADA or RECHAZADA.", ErrorType.UnprocessableEntity);

    public static readonly Error ResolutionNotesRequired = new(
        "RETIREMENT_RESOLUTION_NOTES_REQUIRED",
        "Rejecting a retirement request requires a note.", ErrorType.UnprocessableEntity);

    public static readonly Error SelfActionForbidden = new(
        "RETIREMENT_SELF_ACTION_FORBIDDEN",
        "The subject employee cannot authorize, execute or revert their own retirement.", ErrorType.Forbidden);

    public static readonly Error RequesterCannotAuthorize = new(
        "RETIREMENT_REQUESTER_CANNOT_AUTHORIZE",
        "The requester of a retirement cannot authorize it (separation of duties).", ErrorType.Forbidden);

    public static readonly Error ExecutionDateNotReached = new(
        "RETIREMENT_EXECUTION_DATE_NOT_REACHED",
        "The retirement cannot be executed before its retirement date.", ErrorType.UnprocessableEntity);

    public static readonly Error ExecutionStateConflict = new(
        "RETIREMENT_EXECUTION_STATE_CONFLICT",
        "The employee's profile no longer matches an executable state (it may have been retired or modified by another operation).", ErrorType.UnprocessableEntity);

    public static readonly Error LastAdminConflict = new(
        "RETIREMENT_LAST_ADMIN_CONFLICT",
        "The employee is the company's last active administrator; transfer the administration before executing the retirement.", ErrorType.UnprocessableEntity);

    public static readonly Error ReversalReasonRequired = new(
        "RETIREMENT_REVERSAL_REASON_REQUIRED",
        "Reverting a retirement requires a reason.", ErrorType.UnprocessableEntity);

    public static readonly Error ReversalWindowExpired = new(
        "RETIREMENT_REVERSAL_WINDOW_EXPIRED",
        "The 30-day reversal window since the execution has expired; use a rehire to reincorporate the employee.", ErrorType.UnprocessableEntity);

    public static readonly Error ReversalBlockedByRehire = new(
        "RETIREMENT_REVERSAL_BLOCKED_BY_REHIRE",
        "The retirement cannot be reverted because the employee was rehired after the execution.", ErrorType.UnprocessableEntity);

    public static readonly Error ReversalStateDiverged = new(
        "RETIREMENT_REVERSAL_STATE_DIVERGED",
        "The employee's current state no longer matches what the execution left; the reversal was blocked to avoid restoring over modified data.", ErrorType.UnprocessableEntity);

    public static readonly Error ReversalNotMostRecent = new(
        "RETIREMENT_REVERSAL_NOT_MOST_RECENT",
        "Only the employee's most recent executed retirement can be reverted.", ErrorType.UnprocessableEntity);

    // Settlement module D-17 (closes this module's D-14 integration point): draft settlements are annulled
    // automatically by the reversal; an ISSUED one blocks it until manually annulled.
    public static readonly Error ReversalBlockedBySettlement = new(
        "RETIREMENT_REVERSAL_BLOCKED_BY_SETTLEMENT",
        "The retirement has an ISSUED settlement; annul the settlement first, then revert.", ErrorType.UnprocessableEntity);
}

/// <summary>
/// Pure, unit-testable retirement rules (no database access). Cross-aggregate checks (catalog codes, requester
/// lookup, open-request uniqueness, rehire signals) are database-backed and live in the handlers, not here.
/// Domain transition guards live on <see cref="PersonnelFileRetirementRequest"/>. The clock ALWAYS enters via
/// the <c>asOfUtc</c> parameter (<c>IDateTimeProvider.UtcNow</c> in handlers — never <c>DateTime.UtcNow</c>).
/// </summary>
internal static class RetirementRequestRules
{
    /// <summary>Maximum calendar days after the execution during which a reversal is allowed (RN-012.4, ratified).</summary>
    public const int ReversalWindowDays = 30;

    /// <summary>(RN-001.1) Employee + Completed, active file, and no retirement in force on the profile.</summary>
    public static bool IsEligibleForRequest(bool isCompletedEmployee, bool fileIsActive, DateTime? profileRetirementDate) =>
        isCompletedEmployee && fileIsActive && profileRetirementDate is null;

    /// <summary>
    /// (RN-001.4 / RF-016) The request date is not in the future and the retirement date does not precede
    /// the hire date. UTC-date semantics (pre-development clarification #2).
    /// </summary>
    public static bool AreDatesCoherent(DateTime requestDate, DateTime retirementDate, DateTime hireDate, DateTime asOfUtc) =>
        requestDate.Date <= asOfUtc.Date && retirementDate.Date >= hireDate.Date;

    /// <summary>(D-05) Manual execution is only allowed once the retirement date arrives (UTC date).</summary>
    public static bool IsExecutableOn(DateTime retirementDate, DateTime asOfUtc) =>
        retirementDate.Date <= asOfUtc.Date;

    /// <summary>
    /// (RN-012.4, ratified) Exact-timestamp window: the reversal must happen no later than 30 calendar days
    /// after the execution timestamp.
    /// </summary>
    public static bool IsWithinReversalWindow(DateTime executionDateUtc, DateTime asOfUtc) =>
        asOfUtc <= executionDateUtc.AddDays(ReversalWindowDays);

    /// <summary>
    /// (R-T5) Closing rows at the retirement date would violate the end-after-start check constraints when an
    /// active assignment/contract STARTS after that date (e.g. a plaza granted after a retroactive baja).
    /// </summary>
    public static bool HasClosingBlockers(IEnumerable<DateTime> activeRowStartDates, DateTime retirementDate) =>
        activeRowStartDates.Any(startDate => startDate.Date > retirementDate.Date);
}
