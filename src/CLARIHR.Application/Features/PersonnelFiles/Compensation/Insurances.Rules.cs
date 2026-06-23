using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Coded errors for the employee insurance hardening (Fase 1): anti-duplicate policy per employee and
/// per-insurance beneficiary (D-13), plus the primary-beneficiary allocation cap (D-09). Catalog and
/// field validations (insurance/range/currency/document type out of catalog, negative amounts, date
/// order) surface as <c>common.validation</c> (400), like kinship today, so they need no entry here.
/// Every code below must have a matching entry in BackendMessages.resx and BackendMessages.es.resx
/// (parity is enforced by <c>BackendMessageLocalizationTests</c>).
/// </summary>
internal static class InsuranceErrors
{
    public static readonly Error PolicyDuplicate = new(
        "INSURANCE_POLICY_DUPLICATE",
        "The employee already has an insurance with the same policy number.",
        ErrorType.Conflict);

    public static readonly Error BeneficiaryDuplicate = new(
        "INSURANCE_BENEFICIARY_DUPLICATE",
        "This insurance already has a beneficiary with the same document.",
        ErrorType.Conflict);

    public static readonly Error AllocationExceeded = new(
        "INSURANCE_BENEFICIARY_ALLOCATION_INVALID",
        "Active primary beneficiaries cannot exceed 100% allocation for this insurance.",
        ErrorType.UnprocessableEntity);
}

/// <summary>
/// Pure cross-row rules for employee insurances and their beneficiaries. Insurance is an informative
/// benefit (D-01): multiple insurances coexist (D-05), so there is no single-active invariant; the only
/// cross-row invariants are anti-duplicate (D-13) and the primary-allocation cap (D-09). Keeping it pure
/// (operating on already-loaded sibling collections) makes every check unit-testable without a database.
/// </summary>
internal static class InsuranceRules
{
    internal sealed record ExistingInsurance(Guid PublicId, string? NormalizedPolicyNumber);

    internal sealed record ExistingBeneficiary(
        Guid PublicId,
        string? NormalizedDocumentKey,
        bool IsActive,
        bool IsPrimary,
        decimal? AllocationPercentage);

    public static string? NormalizePolicy(string? policyNumber) =>
        string.IsNullOrWhiteSpace(policyNumber) ? null : policyNumber.Trim().ToUpperInvariant();

    /// <summary>Document identity key (type + number) used for per-insurance beneficiary de-duplication.</summary>
    public static string? NormalizeDocumentKey(string? documentTypeCode, string? documentNumber)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            return null;
        }

        var type = string.IsNullOrWhiteSpace(documentTypeCode) ? string.Empty : documentTypeCode.Trim().ToUpperInvariant();
        return $"{type}|{documentNumber.Trim().ToUpperInvariant()}";
    }

    /// <summary>A beneficiary defaults to PRINCIPAL when no type is given (CONTINGENTE must be explicit).</summary>
    public static bool IsPrimary(string? beneficiaryType) =>
        string.IsNullOrWhiteSpace(beneficiaryType)
        || string.Equals(beneficiaryType.Trim(), PersonnelFileInsuranceBeneficiary.TypePrimary, StringComparison.OrdinalIgnoreCase);

    /// <summary>(D-13) The same policy number cannot repeat for one employee (the candidate excludes itself on update).</summary>
    public static Result CheckPolicyUnique(
        Guid? candidatePublicId,
        string? candidatePolicyNumber,
        IReadOnlyCollection<ExistingInsurance> siblings)
    {
        var normalized = NormalizePolicy(candidatePolicyNumber);
        if (normalized is null)
        {
            return Result.Success();
        }

        var duplicate = siblings.Any(sibling =>
            sibling.PublicId != candidatePublicId
            && sibling.NormalizedPolicyNumber == normalized);
        return duplicate ? Result.Failure(InsuranceErrors.PolicyDuplicate) : Result.Success();
    }

    /// <summary>(D-13) The same active beneficiary (document type + number) cannot repeat within one insurance.</summary>
    public static Result CheckBeneficiaryUnique(
        Guid? candidatePublicId,
        string? candidateDocumentTypeCode,
        string? candidateDocumentNumber,
        IReadOnlyCollection<ExistingBeneficiary> siblings)
    {
        var key = NormalizeDocumentKey(candidateDocumentTypeCode, candidateDocumentNumber);
        if (key is null)
        {
            return Result.Success();
        }

        var duplicate = siblings.Any(sibling =>
            sibling.PublicId != candidatePublicId
            && sibling.IsActive
            && sibling.NormalizedDocumentKey == key);
        return duplicate ? Result.Failure(InsuranceErrors.BeneficiaryDuplicate) : Result.Success();
    }

    /// <summary>
    /// (D-09) The sum of allocation across ACTIVE PRIMARY beneficiaries of one insurance cannot exceed 100%.
    /// Enforced as a cap (≤ 100), not exact equality, so beneficiaries can be entered incrementally; the
    /// candidate excludes itself on update. Contingent / inactive beneficiaries do not count toward the cap.
    /// </summary>
    public static Result CheckPrimaryAllocation(
        Guid? candidatePublicId,
        bool candidateIsActive,
        string? candidateBeneficiaryType,
        decimal? candidateAllocation,
        IReadOnlyCollection<ExistingBeneficiary> siblings)
    {
        var othersTotal = siblings
            .Where(sibling => sibling.PublicId != candidatePublicId && sibling.IsActive && sibling.IsPrimary)
            .Sum(sibling => sibling.AllocationPercentage ?? 0m);

        var candidateContribution = candidateIsActive && IsPrimary(candidateBeneficiaryType)
            ? candidateAllocation ?? 0m
            : 0m;

        return othersTotal + candidateContribution > 100m
            ? Result.Failure(InsuranceErrors.AllocationExceeded)
            : Result.Success();
    }
}
