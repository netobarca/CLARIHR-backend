using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;

/// <summary>How a status transition is attempted: through the one decision or through an annulment/revocation.</summary>
public enum PersonnelTransactionTransitionVia
{
    Decision,
    Annulment,
}

/// <summary>Outcome of validating the suspension block against the type flag and the date range (RN-05).</summary>
public enum SuspensionBlockValidation
{
    Valid,

    /// <summary>Suspension dates were supplied on a type that does not apply suspension (SUSPENSION_NOT_ALLOWED_FOR_TYPE).</summary>
    NotAllowedForType,

    /// <summary>The type applies suspension but a date is missing (SUSPENSION_DATES_REQUIRED).</summary>
    DatesRequired,

    /// <summary>Start date is after the end date (SUSPENSION_RANGE_INVALID).</summary>
    RangeInvalid,
}

/// <summary>Outcome of validating the deduction block (RN-06).</summary>
public enum DeductionValidation
{
    Valid,

    /// <summary>The deduction flag is set but the amount is missing or not positive (DEDUCTION_AMOUNT_REQUIRED).</summary>
    AmountRequired,
}

/// <summary>A normalized, validated availability-query window (RF-013).</summary>
public sealed record AvailabilityWindow(DateOnly Start, DateOnly End);

/// <summary>Result of normalizing the availability query range: invalid when start &gt; end (TIME_AVAILABILITY_RANGE_INVALID).</summary>
public sealed record AvailabilityWindowResult(bool IsValid, AvailabilityWindow? Window);

/// <summary>
/// The "otras transacciones de personal" flow/validation arithmetic (REQ-003 §3.5, golden suite A.4) — 100%
/// pure and deterministic: no clock, no database, no side-effects. It is the single source of truth for the
/// one-decision state machine, the double anti-self-approval check, the suspension/deduction block validation,
/// the calendar-inclusive suspension days, and the range-intersection primitive shared by the suspension
/// overlap (RN-18) and the time-availability query (RN-15). Recognitions and disciplinary actions consume the
/// same primitives; the family-specific validators live in each handler.
/// </summary>
public static class PersonnelTransactionRules
{
    /// <summary>
    /// The one-decision state machine (RN-01): a <see cref="PersonnelTransactionTransitionVia.Decision"/> moves
    /// EN_REVISION → APLICADA/RECHAZADA; an <see cref="PersonnelTransactionTransitionVia.Annulment"/> moves to
    /// ANULADA from EN_REVISION (trámite withdrawal) or APLICADA (revocation). Every other transition is invalid.
    /// </summary>
    public static bool CanTransition(string from, string to, PersonnelTransactionTransitionVia via)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return false;
        }

        return via switch
        {
            PersonnelTransactionTransitionVia.Decision =>
                from == PersonnelTransactionStatuses.EnRevision
                && to is PersonnelTransactionStatuses.Aplicada or PersonnelTransactionStatuses.Rechazada,
            PersonnelTransactionTransitionVia.Annulment =>
                to == PersonnelTransactionStatuses.Anulada
                && from is PersonnelTransactionStatuses.EnRevision or PersonnelTransactionStatuses.Aplicada,
            _ => false,
        };
    }

    /// <summary>
    /// Double anti-self-approval (RN-02): the decision/revocation is a self-decision when the current user is
    /// the subject employee (<paramref name="linkedUserId"/>) OR the user who registered the transaction
    /// (<paramref name="registeredByUserId"/>). A third party returns false. Comparison is case-insensitive and
    /// ignores empty references.
    /// </summary>
    public static bool IsSelfDecision(string? linkedUserId, string? registeredByUserId, string currentUserId) =>
        Matches(linkedUserId, currentUserId) || Matches(registeredByUserId, currentUserId);

    /// <summary>
    /// Validates the suspension block (RN-05): with the type flag set the range is required and coherent
    /// (start ≤ end); without the flag no suspension dates may travel. Future ranges are allowed.
    /// </summary>
    public static SuspensionBlockValidation ValidateSuspensionBlock(
        bool typeAppliesSuspension,
        DateOnly? start,
        DateOnly? end)
    {
        var hasStart = start.HasValue;
        var hasEnd = end.HasValue;

        if (!typeAppliesSuspension)
        {
            return hasStart || hasEnd ? SuspensionBlockValidation.NotAllowedForType : SuspensionBlockValidation.Valid;
        }

        if (!hasStart || !hasEnd)
        {
            return SuspensionBlockValidation.DatesRequired;
        }

        return end!.Value < start!.Value ? SuspensionBlockValidation.RangeInvalid : SuspensionBlockValidation.Valid;
    }

    /// <summary>Calendar-inclusive suspension days (P-04): <c>end − start + 1</c>. Throws when end precedes start.</summary>
    public static int SuspensionDays(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new ArgumentException("The suspension end date cannot precede the start date.", nameof(end));
        }

        return end.DayNumber - start.DayNumber + 1;
    }

    /// <summary>Validates the deduction block (RN-06): with the flag set the amount must be present and positive.</summary>
    public static DeductionValidation ValidateDeduction(bool hasDeduction, decimal? amount)
    {
        if (!hasDeduction)
        {
            return DeductionValidation.Valid;
        }

        return amount is > 0m ? DeductionValidation.Valid : DeductionValidation.AmountRequired;
    }

    /// <summary>
    /// Inclusive range intersection primitive (RN-18 suspension overlap / RN-15 availability intersection):
    /// two ranges overlap when <c>aStart ≤ bEnd &amp;&amp; aEnd ≥ bStart</c>. A single-day range (start == end)
    /// participates like any other.
    /// </summary>
    public static bool RangesOverlap(DateOnly aStart, DateOnly aEnd, DateOnly bStart, DateOnly bEnd) =>
        aStart <= bEnd && aEnd >= bStart;

    /// <summary>
    /// Normalizes/validates the availability query range (RF-013): the range is mandatory (a null range is a
    /// handler-level 400 TIME_AVAILABILITY_RANGE_REQUIRED) and coherent — <c>start ≤ end</c>; otherwise the
    /// result is invalid (422 TIME_AVAILABILITY_RANGE_INVALID).
    /// </summary>
    public static AvailabilityWindowResult BuildAvailabilityWindow(DateOnly rangeStart, DateOnly rangeEnd) =>
        rangeEnd < rangeStart
            ? new AvailabilityWindowResult(false, null)
            : new AvailabilityWindowResult(true, new AvailabilityWindow(rangeStart, rangeEnd));

    private static bool Matches(string? candidate, string? currentUserId)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(currentUserId))
        {
            return false;
        }

        return string.Equals(candidate.Trim(), currentUserId.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
