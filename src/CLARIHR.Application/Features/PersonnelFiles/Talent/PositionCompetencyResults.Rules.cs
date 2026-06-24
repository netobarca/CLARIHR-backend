using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Dedicated errors for the position-competency results feature (the "Competencias del puesto" screen).
/// Field-level checks (achieved score required, evaluation date required/not-future) live in the FluentValidation
/// validator (400); these reference/catalog/scale errors are returned by the handler after consulting the
/// database (422). Each code has a matching bilingual resource entry (localization-parity test).
/// </summary>
internal static class PositionCompetencyResultErrors
{
    public static readonly Error ExpectationInvalid = new(
        "POSITION_COMPETENCY_EXPECTATION_INVALID",
        "The selected competency expectation does not exist for this company.",
        ErrorType.UnprocessableEntity);

    public static readonly Error NotInProfile = new(
        "POSITION_COMPETENCY_NOT_IN_PROFILE",
        "The competency is not part of the competency matrix of the employee's assigned position.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ScaleNotConfigured = new(
        "POSITION_COMPETENCY_SCALE_NOT_CONFIGURED",
        "No active competency rating scale is configured for the company.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ScoreOutOfRange = new(
        "POSITION_COMPETENCY_SCORE_OUT_OF_RANGE",
        "The achieved score is outside the company's active competency rating scale.",
        ErrorType.UnprocessableEntity);
}

internal static class PositionCompetencyResultRules
{
    /// <summary>Decision D-05: the gap is computed as expected − achieved; null when no expected score exists.</summary>
    public static decimal? DeriveGap(decimal? expectedScore, decimal achievedScore) =>
        expectedScore.HasValue ? expectedScore.Value - achievedScore : null;
}
