using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Coded errors for the authorization-substitution rules (D-01…D-12). Every code must have a matching
/// entry in BackendMessages.resx and BackendMessages.es.resx (parity is enforced by
/// <c>BackendMessageLocalizationTests</c>).
/// </summary>
internal static class AuthorizationSubstitutionErrors
{
    public static readonly Error SubstituteNotFound = new(
        "AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_NOT_FOUND",
        "The selected substitute could not be found.",
        ErrorType.NotFound);

    public static readonly Error SubstituteNotEligible = new(
        "AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_NOT_ELIGIBLE",
        "The selected substitute must be an active, completed employee in the same company.",
        ErrorType.UnprocessableEntity);

    public static readonly Error TypeCodeInvalid = new(
        "AUTHORIZATION_SUBSTITUTION_TYPE_CODE_INVALID",
        "The substitution type code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error PositionNotOwned = new(
        "AUTHORIZATION_SUBSTITUTION_POSITION_NOT_OWNED",
        "The selected position must be an active assignment of the substitute.",
        ErrorType.UnprocessableEntity);

    public static readonly Error PeriodOverlap = new(
        "AUTHORIZATION_SUBSTITUTION_PERIOD_OVERLAP",
        "The employee already has an active substitution with an overlapping effective period.",
        ErrorType.Conflict);

    public static readonly Error SubstituteUnavailable = new(
        "AUTHORIZATION_SUBSTITUTION_SUBSTITUTE_UNAVAILABLE",
        "The substitute is unavailable: they are being substituted (absent) during an overlapping period.",
        ErrorType.UnprocessableEntity);
}

/// <summary>
/// Pure business rules for authorization substitutions (scope = whole employee, D-04). The handlers load the
/// titular's other substitutions and the substitute's substitutions (cross-feature), then call
/// <see cref="Evaluate"/>; keeping the rules in a pure module mirrors <see cref="EmploymentAssignmentRules"/>
/// and makes every invariant unit-testable without a database.
///
/// <para>Design notes. The mandatory <c>EndDate</c> (D-03) is enforced in the validator (400
/// <c>common.validation</c>), not here. "Two active at once" (RF-005 <c>SUBSTITUTION_ALREADY_ACTIVE</c>) is
/// subsumed by <see cref="AuthorizationSubstitutionErrors.PeriodOverlap"/>: with a required end date, two
/// active substitutions coinciding in time is exactly two active periods that overlap. Non-overlapping future
/// substitutions are allowed (scheduling), consistent with the effective-state model (RF-006). Blocking, not
/// supersession (D-07).</para>
/// </summary>
internal static class AuthorizationSubstitutionRules
{
    /// <summary>One of the titular's (or the substitute's) existing substitutions; the candidate row is excluded by PublicId.</summary>
    internal sealed record ExistingSubstitution(Guid PublicId, DateTime StartDate, DateTime EndDate, bool IsActive);

    /// <summary>The proposed substitution after applying the command (PublicId is null on Add).</summary>
    internal sealed record Candidate(Guid? PublicId, DateTime StartDate, DateTime EndDate, bool IsActive);

    /// <summary>Outcome of a successful evaluation (no side effects; blocking, not supersession — D-07).</summary>
    internal sealed record Evaluation;

    public static Result<Evaluation> Evaluate(
        Candidate candidate,
        IReadOnlyCollection<ExistingSubstitution> titularSubstitutions,
        IReadOnlyCollection<ExistingSubstitution> substituteAsTitularSubstitutions)
    {
        // Overlap/availability rules only apply to an ACTIVE candidate. An inactive (parked) designation can
        // always be edited without colliding, mirroring the inactive-assignment carve-out in the multi-position rules.
        if (!candidate.IsActive)
        {
            return Result<Evaluation>.Success(new Evaluation());
        }

        // (D-04/D-07) Single effective substitution per titular: no other ACTIVE substitution of the titular
        // may overlap the candidate's effective window.
        var overlapsTitular = titularSubstitutions.Any(other =>
            other.IsActive
            && other.PublicId != candidate.PublicId
            && EmploymentAssignmentRules.RangesOverlap(candidate.StartDate, candidate.EndDate, other.StartDate, other.EndDate));
        if (overlapsTitular)
        {
            return Result<Evaluation>.Failure(AuthorizationSubstitutionErrors.PeriodOverlap);
        }

        // (D-06) Substitute unavailable: the chosen substitute cannot be the titular of another ACTIVE
        // substitution whose window overlaps (they are themselves being substituted / absent). The candidate's
        // own row is excluded by PublicId so re-saving an existing designation does not collide with itself.
        var substituteBusy = substituteAsTitularSubstitutions.Any(other =>
            other.IsActive
            && other.PublicId != candidate.PublicId
            && EmploymentAssignmentRules.RangesOverlap(candidate.StartDate, candidate.EndDate, other.StartDate, other.EndDate));
        if (substituteBusy)
        {
            return Result<Evaluation>.Failure(AuthorizationSubstitutionErrors.SubstituteUnavailable);
        }

        return Result<Evaluation>.Success(new Evaluation());
    }
}
