using CLARIHR.Domain.Common;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Domain.EmployeeRelations;

/// <summary>
/// Company-managed master of disciplinary-action causes ("causas de amonestación", REQ-003 D-06/RF-003):
/// inasistencia injustificada, llegadas tardías, incumplimiento de funciones, conducta indebida, daño a
/// bienes, otro. The optional <see cref="DeductionConceptTypeCode"/> is the default egreso concept a
/// disciplinary action of this cause proposes for a payroll deduction; it is validated in the handler
/// against the tenant's country <c>compensation-concept-types</c> catalog (active, <c>Nature = Egreso</c>)
/// with error <c>DEDUCTION_CONCEPT_INVALID</c>. The seeded template ships every cause WITHOUT a concept
/// (REQ-003 P-14 "no hay multas"). Seeded template + filtered unique
/// <c>(tenant, normalized_code) WHERE is_active</c>, mirroring <see cref="RecognitionType"/>.
/// </summary>
public sealed class DisciplinaryActionCause : TenantEntity
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 200;
    public const int MaxDeductionConceptTypeCodeLength = 80;

    private DisciplinaryActionCause()
    {
    }

    private DisciplinaryActionCause(
        Guid publicId,
        string code,
        string name,
        string? deductionConceptTypeCode,
        int sortOrder)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        SetDeductionConceptTypeCode(deductionConceptTypeCode);
        SortOrder = sortOrder;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Optional default egreso concept code (validated in the handler; null = no deduction default).</summary>
    public string? DeductionConceptTypeCode { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static DisciplinaryActionCause Create(
        string code,
        string name,
        string? deductionConceptTypeCode,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, deductionConceptTypeCode, sortOrder);

    public void Update(string code, string name, string? deductionConceptTypeCode, int sortOrder)
    {
        SetCode(code);
        SetName(name);
        SetDeductionConceptTypeCode(deductionConceptTypeCode);
        SortOrder = sortOrder;
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        IsActive = false;
        RefreshConcurrencyToken();
    }

    private void SetCode(string code)
    {
        Code = LeaveNormalization.NormalizeCode(code);
        if (Code.Length > MaxCodeLength)
        {
            throw new ArgumentException($"Code must be {MaxCodeLength} characters or fewer.", nameof(code));
        }

        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = LeaveNormalization.Clean(name, nameof(name));
        if (Name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));
        }

        NormalizedName = LeaveNormalization.NormalizeName(Name);
    }

    private void SetDeductionConceptTypeCode(string? deductionConceptTypeCode)
    {
        var normalized = LeaveNormalization.CleanOptional(deductionConceptTypeCode)?.ToUpperInvariant();
        if (normalized is not null && normalized.Length > MaxDeductionConceptTypeCodeLength)
        {
            throw new ArgumentException(
                $"Deduction concept code must be {MaxDeductionConceptTypeCodeLength} characters or fewer.",
                nameof(deductionConceptTypeCode));
        }

        DeductionConceptTypeCode = normalized;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
