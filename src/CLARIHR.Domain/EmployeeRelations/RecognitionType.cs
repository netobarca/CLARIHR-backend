using CLARIHR.Domain.Common;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Domain.EmployeeRelations;

/// <summary>
/// Company-managed master of recognition types ("tipos de reconocimiento", REQ-003 D-06/RF-001):
/// felicitación escrita, desempeño sobresaliente, productividad, antigüedad, otro. The master ships
/// with a seeded template (REQ-003 aclaración №8, Anexo A.2) via <c>EmployeeRelationsTemplateSeeder</c>,
/// idempotent by normalized code (tenant edits are never overwritten). Mirrors the governed leave
/// masters (<see cref="MedicalClinic"/> / <see cref="CompensatoryTimeType"/>): filtered unique
/// <c>(tenant, normalized_code) WHERE is_active</c>, logical activate/inactivate with a usage guard.
/// </summary>
public sealed class RecognitionType : TenantEntity
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 200;

    private RecognitionType()
    {
    }

    private RecognitionType(Guid publicId, string code, string name, int sortOrder)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        SortOrder = sortOrder;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static RecognitionType Create(string code, string name, int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);

    public void Update(string code, string name, int sortOrder)
    {
        SetCode(code);
        SetName(name);
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
