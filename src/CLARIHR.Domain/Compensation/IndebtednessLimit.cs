using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Compensation;

/// <summary>
/// The maximum indebtedness percentage the company tolerates for ONE type of recurring deduction (REQ-010, D-16):
/// e.g. "a bank loan may not push the employee past 25% of their income". It is the per-type refinement of the
/// company-wide <c>CompanyPreference.MaxIndebtednessPercent</c>, and it <b>prevails</b> over it for deductions of
/// its type — even when it is MORE permissive (that is the point: a type can be granted its own ceiling).
///
/// A tenant with no global preference and no row here has <b>no indebtedness control at all</b>, and deductions are
/// registered without any warning. That is deliberate: the whole feature is opt-in by configuration.
/// </summary>
public sealed class IndebtednessLimit : TenantEntity
{
    public const int MaxTypeCodeLength = 60;

    private IndebtednessLimit()
    {
    }

    private IndebtednessLimit(string recurringDeductionTypeCode, decimal maxPercent, bool isActive)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        RecurringDeductionTypeCode = NormalizeCode(recurringDeductionTypeCode);
        MaxPercent = ValidatePercent(maxPercent);
        IsActive = isActive;
    }

    /// <summary>The recurring-deduction type this ceiling applies to (the REQ-008 catalog code).</summary>
    public string RecurringDeductionTypeCode { get; private set; } = string.Empty;

    /// <summary>The ceiling, in (0, 100].</summary>
    public decimal MaxPercent { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static IndebtednessLimit Create(string recurringDeductionTypeCode, decimal maxPercent, bool isActive) =>
        new(recurringDeductionTypeCode, maxPercent, isActive);

    private static decimal ValidatePercent(decimal maxPercent) =>
        maxPercent is <= 0m or > 100m
            ? throw new ArgumentOutOfRangeException(
                nameof(maxPercent),
                "The indebtedness limit must be greater than 0 and at most 100.")
            : maxPercent;

    private static string NormalizeCode(string code) =>
        string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("RecurringDeductionTypeCode cannot be empty.", nameof(code))
            : code.Trim().ToUpperInvariant();
}
