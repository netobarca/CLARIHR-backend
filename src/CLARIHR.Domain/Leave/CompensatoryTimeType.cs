using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Leave;

/// <summary>
/// Canonical operation codes for a compensatory-time type: whether the type credits hours into the
/// fund (<see cref="Credits"/>), debits hours from it (<see cref="Debits"/>), or can be used for
/// both (<see cref="Both"/>). Hybrid model (REQ-002 D-15): these constants are the source of truth;
/// the country-scoped <c>compensatory-time-operations</c> general catalog only backs i18n/UI.
/// </summary>
public static class CompensatoryTimeOperations
{
    public const string Credits = "ACREDITA";
    public const string Debits = "DEBITA";
    public const string Both = "AMBAS";

    public static readonly IReadOnlyCollection<string> All = [Credits, Debits, Both];

    public static bool IsValid(string? operationCode) =>
        operationCode is not null && All.Contains(operationCode.Trim().ToUpperInvariant());
}

/// <summary>
/// Company-managed master of compensatory-time types ("tipos de tiempo compensatorio", REQ-002
/// D-05). Each type declares an <see cref="OperationCode"/> (credits / debits / both) and a
/// <see cref="CreditFactor"/> (worked hours × factor = credited hours, snapshotted on each credit —
/// RN-02, so editing the factor never recomputes historical credits). The master starts EMPTY: there
/// is no seeder / template / load-template (D-05); the administrator creates the types. Mirrors the
/// governed leave masters (<see cref="MedicalClinic"/> / <see cref="IncapacityType"/>).
/// </summary>
public sealed class CompensatoryTimeType : TenantEntity
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 200;
    public const int MaxOperationCodeLength = 20;

    private CompensatoryTimeType()
    {
    }

    private CompensatoryTimeType(
        Guid publicId,
        string code,
        string name,
        string operationCode,
        decimal creditFactor,
        int sortOrder)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        SetOperationCode(operationCode);
        SetCreditFactor(creditFactor);
        SortOrder = sortOrder;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Operation code — one of <see cref="CompensatoryTimeOperations"/> (normalized upper).</summary>
    public string OperationCode { get; private set; } = string.Empty;

    /// <summary>Hours-worked multiplier (&gt; 0, default 1.00). Snapshotted on each credit (RN-02).</summary>
    public decimal CreditFactor { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static CompensatoryTimeType Create(
        string code,
        string name,
        string operationCode,
        decimal creditFactor,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            code,
            name,
            operationCode,
            creditFactor,
            sortOrder);

    public void Update(
        string code,
        string name,
        string operationCode,
        decimal creditFactor,
        int sortOrder)
    {
        SetCode(code);
        SetName(name);
        SetOperationCode(operationCode);
        SetCreditFactor(creditFactor);
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

    private void SetOperationCode(string operationCode)
    {
        var normalized = LeaveNormalization.NormalizeCode(operationCode);
        if (!CompensatoryTimeOperations.IsValid(normalized))
        {
            throw new ArgumentException("Operation code must be ACREDITA, DEBITA or AMBAS.", nameof(operationCode));
        }

        OperationCode = normalized;
    }

    private void SetCreditFactor(decimal creditFactor)
    {
        if (creditFactor <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(creditFactor), "Credit factor must be greater than zero.");
        }

        CreditFactor = creditFactor;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
