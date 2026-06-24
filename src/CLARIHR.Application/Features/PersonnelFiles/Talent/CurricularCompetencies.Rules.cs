using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Coded errors for the curricular-competencies hardening (Fase 1): catalog-backed requirement type (D-01/D-02),
/// competency domain (D-03) and metric (D-04), the experience/metric coherence (D-06), and the anti-duplicate
/// invariant (D-05). Every code below must have a matching entry in BackendMessages.resx and
/// BackendMessages.es.resx (parity is enforced by <c>BackendMessageLocalizationTests</c>).
/// </summary>
internal static class CurricularCompetencyErrors
{
    public static readonly Error RequirementTypeInvalid = new(
        "CURRICULAR_COMPETENCY_REQUIREMENT_TYPE_INVALID",
        "The requirement type code is not valid for the active Organizational-Structure catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error DomainInvalid = new(
        "CURRICULAR_COMPETENCY_DOMAIN_INVALID",
        "The competency domain code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error MetricInvalid = new(
        "CURRICULAR_COMPETENCY_METRIC_INVALID",
        "The metric code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error MetricRequired = new(
        "CURRICULAR_COMPETENCY_METRIC_REQUIRED",
        "A metric is required when an experience time value is provided.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ExperienceNegative = new(
        "CURRICULAR_COMPETENCY_EXPERIENCE_NEGATIVE",
        "The experience time value cannot be negative.",
        ErrorType.UnprocessableEntity);

    public static readonly Error Duplicate = new(
        "CURRICULAR_COMPETENCY_DUPLICATE",
        "The employee already has a curricular competency with the same requirement type and name.",
        ErrorType.Conflict);
}

/// <summary>
/// The catalog codes resolved to their canonical form during validation. The handlers persist these (instead of
/// the raw input) so the requirement-type code and competency-domain code are stored canonically, which keeps the
/// anti-duplicate unique index stable regardless of the casing/whitespace the caller submitted (plan §R-T2).
/// </summary>
internal sealed record CurricularCompetencyResolved(string RequirementTypeCode, string CompetencyDomain, string? MetricCode);

/// <summary>
/// Pure rules for employee curricular competencies. Unlike employment assignments or authorization substitutions,
/// curricular competencies legitimately coexist (an employee accumulates many requirements/competencies), so the
/// only cross-row invariant is anti-duplicate (D-05); the intra-record invariant is experience-value coherence
/// (D-06). Keeping it pure (operating on already-loaded sibling keys) makes every check unit-testable without a
/// database.
/// </summary>
internal static class CurricularCompetencyRules
{
    internal sealed record Existing(Guid PublicId, string Key);

    /// <summary>De-duplication key: requirement-type code + requirement name, both trimmed and upper-cased.</summary>
    public static string Key(string requirementTypeCode, string requirementName) =>
        $"{requirementTypeCode.Trim().ToUpperInvariant()}|{requirementName.Trim().ToUpperInvariant()}";

    /// <summary>(D-06) Experience must be ≥ 0 (0 allowed); a metric is required once an experience value is given.</summary>
    public static Result ValidateExperience(decimal? value, string? metricCode)
    {
        if (value is < 0m)
        {
            return Result.Failure(CurricularCompetencyErrors.ExperienceNegative);
        }

        if (value is not null && string.IsNullOrWhiteSpace(metricCode))
        {
            return Result.Failure(CurricularCompetencyErrors.MetricRequired);
        }

        return Result.Success();
    }

    /// <summary>(D-05) The same requirement type + name cannot repeat for one employee (the candidate excludes itself on update).</summary>
    public static Result CheckDuplicate(
        Guid? candidatePublicId,
        string requirementTypeCode,
        string requirementName,
        IReadOnlyCollection<Existing> siblings)
    {
        var key = Key(requirementTypeCode, requirementName);
        var duplicate = siblings.Any(sibling => sibling.PublicId != candidatePublicId && sibling.Key == key);
        return duplicate ? Result.Failure(CurricularCompetencyErrors.Duplicate) : Result.Success();
    }
}
