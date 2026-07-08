using CLARIHR.Domain.Common;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Domain.EmployeeRelations;

/// <summary>
/// Company-managed master of disciplinary-action types ("tipos de amonestación", REQ-003 D-06/RF-002):
/// amonestación verbal/escrita, suspensión sin goce, otra. The <see cref="AppliesSuspension"/> flag
/// declares whether a disciplinary action of this type carries a suspension block (only
/// SUSPENSION_SIN_GOCE ships as <c>true</c>, Anexo A.2). Changing the flag never rewrites existing
/// records (the record snapshots the flag at creation — RN, PR-2). Seeded template + filtered unique
/// <c>(tenant, normalized_code) WHERE is_active</c>, mirroring <see cref="RecognitionType"/>.
/// </summary>
public sealed class DisciplinaryActionType : TenantEntity
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 200;

    private DisciplinaryActionType()
    {
    }

    private DisciplinaryActionType(Guid publicId, string code, string name, bool appliesSuspension, int sortOrder)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        AppliesSuspension = appliesSuspension;
        SortOrder = sortOrder;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Whether a disciplinary action of this type carries a suspension block (default false).</summary>
    public bool AppliesSuspension { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static DisciplinaryActionType Create(string code, string name, bool appliesSuspension, int sortOrder) =>
        new(Guid.NewGuid(), code, name, appliesSuspension, sortOrder);

    public void Update(string code, string name, bool appliesSuspension, int sortOrder)
    {
        SetCode(code);
        SetName(name);
        AppliesSuspension = appliesSuspension;
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

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
