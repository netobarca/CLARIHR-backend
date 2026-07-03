using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.Common;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Pure, unit-testable business rules for a compensation concept (intrinsic invariants). Catalog-code
/// validity is checked separately in the handler (async, repository-backed); the stateful salary rules
/// (single active base salary per plaza, negotiated salary within the plaza range) are layered in via
/// <see cref="EvaluateBaseSalary"/>. Keeping the rules pure mirrors <c>EmploymentAssignmentRules</c>.
/// </summary>
internal static class CompensationConceptRules
{
    /// <summary>
    /// Legacy concept type code representing the employee's base salary (one active per plaza). Kept as
    /// fallback: the authoritative signal is the catalog's <c>IsBaseSalary</c> flag (D-12/DP-08).
    /// </summary>
    public const string BaseSalaryConceptTypeCode = "SALARIO_BASE";

    /// <summary>
    /// True when the concept type is a base salary: either flagged in the catalog (IsBaseSalary,
    /// passed in as normalized codes) or matching the legacy <see cref="BaseSalaryConceptTypeCode"/>.
    /// </summary>
    public static bool IsBaseSalaryConcept(string? conceptTypeCode, IReadOnlyCollection<string> baseSalaryConceptTypeCodes)
    {
        if (string.IsNullOrWhiteSpace(conceptTypeCode))
        {
            return false;
        }

        var normalized = conceptTypeCode.Trim().ToUpperInvariant();
        return baseSalaryConceptTypeCodes.Contains(normalized)
            || string.Equals(normalized, BaseSalaryConceptTypeCode, StringComparison.Ordinal);
    }

    internal sealed record Candidate(
        CompensationNature Nature,
        DeductionClass? DeductionClass,
        CompensationCalculationType CalculationType,
        decimal Value,
        string? CalculationBaseCode);

    /// <summary>Salary range of the plaza's job profile (from the tabulator); null = no band configured.</summary>
    internal sealed record SalaryRange(decimal? MinAmount, decimal? MaxAmount);

    public static Result Evaluate(Candidate candidate)
    {
        if (candidate.CalculationType == CompensationCalculationType.Percentage)
        {
            if (string.IsNullOrWhiteSpace(candidate.CalculationBaseCode))
            {
                return Result.Failure(CompensationErrors.CalculationBaseRequired);
            }

            if (candidate.Value < 0m || candidate.Value > 100m)
            {
                return Result.Failure(CompensationErrors.PercentageOutOfRange);
            }
        }

        if (candidate.Nature == CompensationNature.Egreso && candidate.DeductionClass is null)
        {
            return Result.Failure(CompensationErrors.DeductionClassRequired);
        }

        return Result.Success();
    }

    /// <summary>
    /// Base-salary specific rules (RF-002, R-3): exactly one active base salary per plaza, and the
    /// negotiated amount must fall within the plaza's salary range when one is configured (hard block).
    /// </summary>
    public static Result EvaluateBaseSalary(
        decimal negotiatedAmount,
        bool anotherActiveBaseSalaryExists,
        SalaryRange? range)
    {
        if (anotherActiveBaseSalaryExists)
        {
            return Result.Failure(CompensationErrors.BaseSalaryAlreadyActive);
        }

        if (range is not null)
        {
            if ((range.MinAmount is { } min && negotiatedAmount < min) ||
                (range.MaxAmount is { } max && negotiatedAmount > max))
            {
                return Result.Failure(CompensationErrors.SalaryOutOfProfileRange);
            }
        }

        return Result.Success();
    }
}
