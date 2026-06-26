using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>Lifecycle state of an exit-interview form definition (RF-008).</summary>
public enum ExitInterviewFormStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}

/// <summary>
/// An exit-interview form definition (tenant-scoped, D-01 exclusive module). Built in Draft, published
/// (locked), associated to a single retirement reason (D-03), and optionally anonymous at the whole-form
/// level (D-06). Groups, fields and options hang off it via FK (managed per-entity, mirroring the
/// medical-claim → document pattern).
/// </summary>
public sealed class ExitInterviewForm : TenantEntity
{
    private ExitInterviewForm()
    {
    }

    private ExitInterviewForm(string name, string? description, bool isAnonymous)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        Status = ExitInterviewFormStatus.Draft;
        Version = 1;
        IsActive = true;
        Apply(name, description, isAnonymous);
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsAnonymous { get; private set; }

    public ExitInterviewFormStatus Status { get; private set; }

    public int Version { get; private set; }

    // The single retirement reason this published form serves (D-03); null until associated.
    public string? RetirementReasonCode { get; private set; }

    // True when this is the active form for its reason (single-active per reason, D-03).
    public bool IsActiveForReason { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public static ExitInterviewForm Create(string name, string? description, bool isAnonymous) =>
        new(name, description, isAnonymous);

    /// <summary>Edits header fields. Only meaningful in Draft (the handler enforces the state rule).</summary>
    public void UpdateDefinition(string name, string? description, bool isAnonymous)
    {
        Apply(name, description, isAnonymous);
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Publish()
    {
        Status = ExitInterviewFormStatus.Published;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Reopens a published form for editing as a new version (RF-008): back to Draft, version bumped, and
    /// no longer the active form for its reason. Historical submissions keep their own version snapshot.
    /// </summary>
    public void ReopenForEditing()
    {
        Status = ExitInterviewFormStatus.Draft;
        Version += 1;
        IsActiveForReason = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Archive()
    {
        Status = ExitInterviewFormStatus.Archived;
        IsActiveForReason = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Associates this (published) form to a single retirement reason and marks it active for it.</summary>
    public void AssignReason(string reasonCode)
    {
        RetirementReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode.Trim().ToUpperInvariant();
        IsActiveForReason = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Deactivates this form for its reason (used when another form takes over — single-active).</summary>
    public void DeactivateForReason()
    {
        IsActiveForReason = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void Apply(string name, string? description, bool isAnonymous)
    {
        Name = (name ?? throw new ArgumentNullException(nameof(name))).Trim();
        NormalizedName = Name.ToUpperInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsAnonymous = isAnonymous;
    }
}

/// <summary>A section (group) of an exit-interview form that organizes its fields (RF-004).</summary>
public sealed class ExitInterviewFormGroup : TenantEntity
{
    private ExitInterviewFormGroup()
    {
    }

    private ExitInterviewFormGroup(string title, string? description, int displayOrder)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        Apply(title, description, displayOrder);
    }

    public long ExitInterviewFormId { get; private set; }

    public ExitInterviewForm ExitInterviewForm { get; private set; } = null!;

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public void BindToForm(long exitInterviewFormId) => ExitInterviewFormId = exitInterviewFormId;

    public static ExitInterviewFormGroup Create(string title, string? description, int displayOrder) =>
        new(title, description, displayOrder);

    public void Update(string title, string? description, int displayOrder)
    {
        Apply(title, description, displayOrder);
        ConcurrencyToken = Guid.NewGuid();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void Apply(string title, string? description, int displayOrder)
    {
        Title = (title ?? throw new ArgumentNullException(nameof(title))).Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DisplayOrder = displayOrder;
    }
}

/// <summary>
/// A field of an exit-interview form (RF-005). Captures the business attributes: control type, key,
/// title, description, weight, required, order, optional group, and per-type config (range / length /
/// scale). Anonymity is NOT a field attribute (D-06 — it is set on the form).
/// </summary>
public sealed class ExitInterviewFormField : TenantEntity
{
    private ExitInterviewFormField()
    {
    }

    private ExitInterviewFormField(
        long? exitInterviewFormGroupId,
        string controlTypeCode,
        string fieldKey,
        string title,
        string? description,
        decimal weight,
        bool isRequired,
        int displayOrder,
        decimal? minValue,
        decimal? maxValue,
        int? maxLength,
        int? scaleMax)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        ExitInterviewFormGroupId = exitInterviewFormGroupId;
        Apply(controlTypeCode, fieldKey, title, description, weight, isRequired, displayOrder, minValue, maxValue, maxLength, scaleMax);
    }

    public long ExitInterviewFormId { get; private set; }

    public ExitInterviewForm ExitInterviewForm { get; private set; } = null!;

    public long? ExitInterviewFormGroupId { get; private set; }

    public ExitInterviewFormGroup? ExitInterviewFormGroup { get; private set; }

    public string ControlTypeCode { get; private set; } = string.Empty;

    public string FieldKey { get; private set; } = string.Empty;

    public string NormalizedFieldKey { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal Weight { get; private set; }

    public bool IsRequired { get; private set; }

    public int DisplayOrder { get; private set; }

    public decimal? MinValue { get; private set; }

    public decimal? MaxValue { get; private set; }

    public int? MaxLength { get; private set; }

    public int? ScaleMax { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public void BindToForm(long exitInterviewFormId) => ExitInterviewFormId = exitInterviewFormId;

    public void SetGroup(long? exitInterviewFormGroupId) => ExitInterviewFormGroupId = exitInterviewFormGroupId;

    public static ExitInterviewFormField Create(
        long? exitInterviewFormGroupId,
        string controlTypeCode,
        string fieldKey,
        string title,
        string? description,
        decimal weight,
        bool isRequired,
        int displayOrder,
        decimal? minValue,
        decimal? maxValue,
        int? maxLength,
        int? scaleMax) =>
        new(exitInterviewFormGroupId, controlTypeCode, fieldKey, title, description, weight, isRequired, displayOrder, minValue, maxValue, maxLength, scaleMax);

    public void Update(
        long? exitInterviewFormGroupId,
        string controlTypeCode,
        string fieldKey,
        string title,
        string? description,
        decimal weight,
        bool isRequired,
        int displayOrder,
        decimal? minValue,
        decimal? maxValue,
        int? maxLength,
        int? scaleMax)
    {
        ExitInterviewFormGroupId = exitInterviewFormGroupId;
        Apply(controlTypeCode, fieldKey, title, description, weight, isRequired, displayOrder, minValue, maxValue, maxLength, scaleMax);
        ConcurrencyToken = Guid.NewGuid();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void Apply(
        string controlTypeCode,
        string fieldKey,
        string title,
        string? description,
        decimal weight,
        bool isRequired,
        int displayOrder,
        decimal? minValue,
        decimal? maxValue,
        int? maxLength,
        int? scaleMax)
    {
        ControlTypeCode = (controlTypeCode ?? throw new ArgumentNullException(nameof(controlTypeCode))).Trim().ToUpperInvariant();
        FieldKey = (fieldKey ?? throw new ArgumentNullException(nameof(fieldKey))).Trim();
        NormalizedFieldKey = FieldKey.ToUpperInvariant();
        Title = (title ?? throw new ArgumentNullException(nameof(title))).Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Weight = weight;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
        MinValue = minValue;
        MaxValue = maxValue;
        MaxLength = maxLength;
        ScaleMax = scaleMax;
    }
}

/// <summary>An option of a selection-type exit-interview field, with an optional score (RF-006, D-07).</summary>
public sealed class ExitInterviewFormFieldOption : TenantEntity
{
    private ExitInterviewFormFieldOption()
    {
    }

    private ExitInterviewFormFieldOption(string optionCode, string label, decimal? score, int displayOrder)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        Apply(optionCode, label, score, displayOrder);
    }

    public long ExitInterviewFormFieldId { get; private set; }

    public ExitInterviewFormField ExitInterviewFormField { get; private set; } = null!;

    public string OptionCode { get; private set; } = string.Empty;

    public string NormalizedOptionCode { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public decimal? Score { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public void BindToField(long exitInterviewFormFieldId) => ExitInterviewFormFieldId = exitInterviewFormFieldId;

    public static ExitInterviewFormFieldOption Create(string optionCode, string label, decimal? score, int displayOrder) =>
        new(optionCode, label, score, displayOrder);

    public void Update(string optionCode, string label, decimal? score, int displayOrder)
    {
        Apply(optionCode, label, score, displayOrder);
        ConcurrencyToken = Guid.NewGuid();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void Apply(string optionCode, string label, decimal? score, int displayOrder)
    {
        OptionCode = (optionCode ?? throw new ArgumentNullException(nameof(optionCode))).Trim().ToUpperInvariant();
        NormalizedOptionCode = OptionCode;
        Label = (label ?? throw new ArgumentNullException(nameof(label))).Trim();
        Score = score;
        DisplayOrder = displayOrder;
    }
}
