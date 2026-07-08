using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Leave;

/// <summary>
/// Canonical payer codes for an incapacity subsidy tranche (<see cref="IncapacityRiskParameter.PayerCode"/>).
/// </summary>
public static class IncapacityPayerCodes
{
    public const string Isss = "ISSS";
    public const string Empresa = "EMPRESA";
    public const string SinPago = "SIN_PAGO";

    public static readonly IReadOnlyCollection<string> All = [Isss, Empresa, SinPago];
}

/// <summary>
/// Input for one subsidy tranche when replacing an incapacity risk's parameter set via
/// <see cref="IncapacityRisk.ReplaceParameters"/>. A <c>null</c> <paramref name="DayTo"/> marks the
/// open-ended (last) tranche.
/// </summary>
public readonly record struct IncapacityRiskParameterInput(
    int DayFrom,
    int? DayTo,
    decimal SubsidyPercent,
    string PayerCode);

/// <summary>
/// Company-managed master of incapacity risks ("riesgos de incapacidad": enfermedad común, accidente de
/// trabajo, maternidad…). The boolean flags drive the day-counting rules of the incapacity engine, and —
/// when <see cref="HasSubsidy"/> — the child <see cref="Parameters"/> define the contiguous subsidy
/// tranches (day ranges → percent + payer).
/// </summary>
public sealed class IncapacityRisk : TenantEntity
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 150;

    private readonly List<IncapacityRiskParameter> _parameters = [];

    private IncapacityRisk()
    {
    }

    private IncapacityRisk(
        Guid publicId,
        string code,
        string name,
        bool countsSeventhDay,
        bool countsSaturday,
        bool countsHoliday,
        bool usesWorkSchedule,
        bool allowsIndefinite,
        bool allowsExtension,
        bool usesFund,
        bool hasSubsidy)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        ApplyFlags(
            countsSeventhDay,
            countsSaturday,
            countsHoliday,
            usesWorkSchedule,
            allowsIndefinite,
            allowsExtension,
            usesFund,
            hasSubsidy);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public bool CountsSeventhDay { get; private set; }

    public bool CountsSaturday { get; private set; }

    public bool CountsHoliday { get; private set; }

    public bool UsesWorkSchedule { get; private set; }

    public bool AllowsIndefinite { get; private set; }

    public bool AllowsExtension { get; private set; }

    public bool UsesFund { get; private set; }

    public bool HasSubsidy { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<IncapacityRiskParameter> Parameters => _parameters.AsReadOnly();

    public static IncapacityRisk Create(
        string code,
        string name,
        bool countsSeventhDay,
        bool countsSaturday,
        bool countsHoliday,
        bool usesWorkSchedule,
        bool allowsIndefinite,
        bool allowsExtension,
        bool usesFund,
        bool hasSubsidy) =>
        new(
            Guid.NewGuid(),
            code,
            name,
            countsSeventhDay,
            countsSaturday,
            countsHoliday,
            usesWorkSchedule,
            allowsIndefinite,
            allowsExtension,
            usesFund,
            hasSubsidy);

    public void Update(
        string code,
        string name,
        bool countsSeventhDay,
        bool countsSaturday,
        bool countsHoliday,
        bool usesWorkSchedule,
        bool allowsIndefinite,
        bool allowsExtension,
        bool usesFund,
        bool hasSubsidy)
    {
        if (!hasSubsidy && _parameters.Count > 0)
        {
            throw new ArgumentException(
                "Cannot turn off the subsidy while subsidy parameters exist. Replace the parameters with an empty set first.",
                nameof(hasSubsidy));
        }

        SetCode(code);
        SetName(name);
        ApplyFlags(
            countsSeventhDay,
            countsSaturday,
            countsHoliday,
            usesWorkSchedule,
            allowsIndefinite,
            allowsExtension,
            usesFund,
            hasSubsidy);
        RefreshConcurrencyToken();
    }

    /// <summary>
    /// Replaces the full subsidy tranche set. Tranches must start at day 1, be contiguous, and only the
    /// last one may be open-ended (<c>DayTo == null</c>); without subsidy the set must be empty.
    /// </summary>
    public void ReplaceParameters(IReadOnlyCollection<IncapacityRiskParameterInput> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (!HasSubsidy)
        {
            if (parameters.Count > 0)
            {
                throw new ArgumentException(
                    "A risk without subsidy cannot define subsidy parameters.",
                    nameof(parameters));
            }

            _parameters.Clear();
            RefreshConcurrencyToken();
            return;
        }

        if (parameters.Count == 0)
        {
            throw new ArgumentException(
                "A risk with subsidy requires at least one subsidy parameter.",
                nameof(parameters));
        }

        var inputs = parameters.ToList();
        var normalizedPayerCodes = new List<string>(inputs.Count);

        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];

            if (index == 0 && input.DayFrom != 1)
            {
                throw new ArgumentException("The first subsidy tranche must start at day 1.", nameof(parameters));
            }

            if (index > 0)
            {
                var previous = inputs[index - 1];
                if (previous.DayTo is null)
                {
                    throw new ArgumentException(
                        "Only the last subsidy tranche can be open-ended (DayTo == null).",
                        nameof(parameters));
                }

                if (input.DayFrom != previous.DayTo.Value + 1)
                {
                    throw new ArgumentException(
                        "Subsidy tranches must be contiguous: each tranche must start the day after the previous one ends.",
                        nameof(parameters));
                }
            }

            if (input.DayTo is { } dayTo && dayTo < input.DayFrom)
            {
                throw new ArgumentException(
                    "A subsidy tranche end day must be greater than or equal to its start day.",
                    nameof(parameters));
            }

            if (input.SubsidyPercent is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parameters),
                    "Subsidy percent must be between 0 and 100.");
            }

            var payerCode = LeaveNormalization.NormalizeCode(
                LeaveNormalization.Clean(input.PayerCode, nameof(parameters)));
            if (!IncapacityPayerCodes.All.Contains(payerCode))
            {
                throw new ArgumentException(
                    $"Payer code '{input.PayerCode}' is not supported. Allowed codes: {string.Join(", ", IncapacityPayerCodes.All)}.",
                    nameof(parameters));
            }

            normalizedPayerCodes.Add(payerCode);
        }

        _parameters.Clear();
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            _parameters.Add(IncapacityRiskParameter.Create(
                input.DayFrom,
                input.DayTo,
                input.SubsidyPercent,
                normalizedPayerCodes[index],
                sortOrder: index + 1));
        }

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

    private void ApplyFlags(
        bool countsSeventhDay,
        bool countsSaturday,
        bool countsHoliday,
        bool usesWorkSchedule,
        bool allowsIndefinite,
        bool allowsExtension,
        bool usesFund,
        bool hasSubsidy)
    {
        CountsSeventhDay = countsSeventhDay;
        CountsSaturday = countsSaturday;
        CountsHoliday = countsHoliday;
        UsesWorkSchedule = usesWorkSchedule;
        AllowsIndefinite = allowsIndefinite;
        AllowsExtension = allowsExtension;
        UsesFund = usesFund;
        HasSubsidy = hasSubsidy;
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
