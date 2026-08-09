using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.JobProfiles;

public sealed class JobProfile : TenantEntity
{
    private readonly List<JobProfileRequirement> _requirements = [];
    private readonly List<JobProfileFunction> _functions = [];
    private readonly List<JobProfileRelation> _relations = [];
    private readonly List<JobProfileCompetency> _competencies = [];
    private readonly List<JobProfileTraining> _trainings = [];
    private readonly List<JobProfileBenefit> _benefits = [];
    private readonly List<JobProfileWorkingCondition> _workingConditions = [];
    private readonly List<JobProfileDependentPosition> _dependentPositions = [];

    private JobProfile()
    {
    }

    private JobProfile(Guid publicId, string code, string title)
    {
        PublicId = publicId;
        SetCode(code);
        SetTitle(title);
        Status = JobProfileStatus.Draft;
        IsActive = true;
        Version = 1;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string NormalizedTitle { get; private set; } = string.Empty;

    public string? Objective { get; private set; }

    public long OrgUnitId { get; private set; }

    public long? ReportsToJobProfileId { get; private set; }

    public long? PositionCategoryId { get; private set; }

    public long? StrategicObjectiveCatalogItemId { get; private set; }

    public long? AssignedWorkEquipmentCatalogItemId { get; private set; }

    public long? ResponsibilityCatalogItemId { get; private set; }

    public string? DecisionScope { get; private set; }

    public string? AssignedResources { get; private set; }

    public string? Responsibilities { get; private set; }

    public string? MarketSalaryReference { get; private set; }

    public string? ValuationNotes { get; private set; }

    public JobProfileStatus Status { get; private set; }

    public int Version { get; private set; }

    public DateTime? EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<JobProfileRequirement> Requirements => _requirements;

    public IReadOnlyCollection<JobProfileFunction> Functions => _functions;

    public IReadOnlyCollection<JobProfileRelation> Relations => _relations;

    public IReadOnlyCollection<JobProfileCompetency> Competencies => _competencies;

    public IReadOnlyCollection<JobProfileTraining> Trainings => _trainings;

    public IReadOnlyCollection<JobProfileBenefit> Benefits => _benefits;

    public IReadOnlyCollection<JobProfileWorkingCondition> WorkingConditions => _workingConditions;

    public IReadOnlyCollection<JobProfileDependentPosition> DependentPositions => _dependentPositions;

    public static JobProfile Create(string code, string title) => new(Guid.NewGuid(), code, title);

    public void UpdateCore(
        string code,
        string title,
        string? objective,
        long orgUnitId,
        long? reportsToJobProfileId,
        long? positionCategoryId,
        long? strategicObjectiveCatalogItemId,
        long? assignedWorkEquipmentCatalogItemId,
        long? responsibilityCatalogItemId,
        string? decisionScope,
        string? assignedResources,
        string? responsibilities,
        string? marketSalaryReference,
        string? valuationNotes,
        DateTime? effectiveFromUtc,
        DateTime? effectiveToUtc,
        bool bumpVersion = true)
    {
        EnsureEditable();
        EnsurePositiveId(orgUnitId, nameof(orgUnitId));

        var normalizedEffectiveFromUtc = NormalizeOptionalUtc(effectiveFromUtc);
        var normalizedEffectiveToUtc = NormalizeOptionalUtc(effectiveToUtc);

        if (normalizedEffectiveFromUtc.HasValue &&
            normalizedEffectiveToUtc.HasValue &&
            normalizedEffectiveFromUtc.Value > normalizedEffectiveToUtc.Value)
        {
            throw new InvalidOperationException("Effective start date cannot be greater than effective end date.");
        }

        SetCode(code);
        SetTitle(title);
        Objective = JobProfileNormalization.CleanOptional(objective);
        OrgUnitId = orgUnitId;
        ReportsToJobProfileId = reportsToJobProfileId;
        PositionCategoryId = positionCategoryId;
        StrategicObjectiveCatalogItemId = strategicObjectiveCatalogItemId;
        AssignedWorkEquipmentCatalogItemId = assignedWorkEquipmentCatalogItemId;
        ResponsibilityCatalogItemId = responsibilityCatalogItemId;
        DecisionScope = JobProfileNormalization.CleanOptional(decisionScope);
        AssignedResources = JobProfileNormalization.CleanOptional(assignedResources);
        Responsibilities = JobProfileNormalization.CleanOptional(responsibilities);
        MarketSalaryReference = JobProfileNormalization.CleanOptional(marketSalaryReference);
        ValuationNotes = JobProfileNormalization.CleanOptional(valuationNotes);
        EffectiveFromUtc = normalizedEffectiveFromUtc;
        EffectiveToUtc = normalizedEffectiveToUtc;

        if (bumpVersion)
        {
            Version++;
            RefreshConcurrencyToken();
        }
    }

    public void ReplaceRequirements(IEnumerable<JobProfileRequirement> items)
    {
        EnsureEditable();
        _requirements.Clear();
        _requirements.AddRange(items);
    }

    public void ReplaceFunctions(IEnumerable<JobProfileFunction> items)
    {
        EnsureEditable();
        _functions.Clear();
        _functions.AddRange(items);
    }

    public void ReplaceRelations(IEnumerable<JobProfileRelation> items)
    {
        EnsureEditable();
        _relations.Clear();
        _relations.AddRange(items);
    }

    public void ReplaceCompetencies(IEnumerable<JobProfileCompetency> items)
    {
        EnsureEditable();
        _competencies.Clear();
        _competencies.AddRange(items);
    }

    public void ReplaceTrainings(IEnumerable<JobProfileTraining> items)
    {
        EnsureEditable();
        _trainings.Clear();
        _trainings.AddRange(items);
    }

    public void ReplaceBenefits(IEnumerable<JobProfileBenefit> items)
    {
        EnsureEditable();
        _benefits.Clear();
        _benefits.AddRange(items);
    }

    public void ReplaceWorkingConditions(IEnumerable<JobProfileWorkingCondition> items)
    {
        EnsureEditable();
        _workingConditions.Clear();
        _workingConditions.AddRange(items);
    }

    public void ReplaceDependentPositions(IEnumerable<JobProfileDependentPosition> items)
    {
        EnsureEditable();
        _dependentPositions.Clear();
        _dependentPositions.AddRange(items);
    }

    public void AddRequirement(JobProfileRequirement item, bool bumpVersion = true)
    {
        EnsureEditable();
        _requirements.Add(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public JobProfileRequirement GetRequirement(Guid publicId) =>
        _requirements.FirstOrDefault(x => x.PublicId == publicId)
        ?? throw new InvalidOperationException($"Requirement with PublicId {publicId} was not found.");

    public void RemoveRequirement(JobProfileRequirement item, bool bumpVersion = true)
    {
        EnsureEditable();
        _requirements.Remove(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public void AddFunction(JobProfileFunction item, bool bumpVersion = true)
    {
        EnsureEditable();
        _functions.Add(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public JobProfileFunction GetFunction(Guid publicId) =>
        _functions.FirstOrDefault(x => x.PublicId == publicId)
        ?? throw new InvalidOperationException($"Function with PublicId {publicId} was not found.");

    public void RemoveFunction(JobProfileFunction item, bool bumpVersion = true)
    {
        EnsureEditable();
        _functions.Remove(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public void AddRelation(JobProfileRelation item, bool bumpVersion = true)
    {
        EnsureEditable();
        _relations.Add(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public JobProfileRelation GetRelation(Guid publicId) =>
        _relations.FirstOrDefault(x => x.PublicId == publicId)
        ?? throw new InvalidOperationException($"Relation with PublicId {publicId} was not found.");

    public void RemoveRelation(JobProfileRelation item, bool bumpVersion = true)
    {
        EnsureEditable();
        _relations.Remove(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public void AddCompetency(JobProfileCompetency item, bool bumpVersion = true)
    {
        EnsureEditable();
        _competencies.Add(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public JobProfileCompetency GetCompetency(Guid publicId) =>
        _competencies.FirstOrDefault(x => x.PublicId == publicId)
        ?? throw new InvalidOperationException($"Competency with PublicId {publicId} was not found.");

    public void RemoveCompetency(JobProfileCompetency item, bool bumpVersion = true)
    {
        EnsureEditable();
        _competencies.Remove(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public void AddTraining(JobProfileTraining item, bool bumpVersion = true)
    {
        EnsureEditable();
        _trainings.Add(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public JobProfileTraining GetTraining(Guid publicId) =>
        _trainings.FirstOrDefault(x => x.PublicId == publicId)
        ?? throw new InvalidOperationException($"Training with PublicId {publicId} was not found.");

    public void RemoveTraining(JobProfileTraining item, bool bumpVersion = true)
    {
        EnsureEditable();
        _trainings.Remove(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public void AddBenefit(JobProfileBenefit item, bool bumpVersion = true)
    {
        EnsureEditable();
        _benefits.Add(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public JobProfileBenefit GetBenefit(Guid publicId) =>
        _benefits.FirstOrDefault(x => x.PublicId == publicId)
        ?? throw new InvalidOperationException($"Benefit with PublicId {publicId} was not found.");

    public void RemoveBenefit(JobProfileBenefit item, bool bumpVersion = true)
    {
        EnsureEditable();
        _benefits.Remove(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public void AddWorkingCondition(JobProfileWorkingCondition item, bool bumpVersion = true)
    {
        EnsureEditable();
        _workingConditions.Add(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public JobProfileWorkingCondition GetWorkingCondition(Guid publicId) =>
        _workingConditions.FirstOrDefault(x => x.PublicId == publicId)
        ?? throw new InvalidOperationException($"Working condition with PublicId {publicId} was not found.");

    public void RemoveWorkingCondition(JobProfileWorkingCondition item, bool bumpVersion = true)
    {
        EnsureEditable();
        _workingConditions.Remove(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public void AddDependentPosition(JobProfileDependentPosition item, bool bumpVersion = true)
    {
        EnsureEditable();
        _dependentPositions.Add(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    public JobProfileDependentPosition GetDependentPosition(Guid publicId) =>
        _dependentPositions.FirstOrDefault(x => x.PublicId == publicId)
        ?? throw new InvalidOperationException($"Dependent position with PublicId {publicId} was not found.");

    public void RemoveDependentPosition(JobProfileDependentPosition item, bool bumpVersion = true)
    {
        EnsureEditable();
        _dependentPositions.Remove(item);
        if (bumpVersion) BumpDescriptorVersion();
    }

    /// <summary>
    /// Version bump that is allowed on a PUBLISHED profile. Only the competency matrix uses it: the
    /// matrix is an operational overlay on an approved descriptor, not part of the descriptor, so it
    /// keeps working after publication (H-01 scope decision). Descriptor writes must use
    /// <see cref="BumpDescriptorVersion"/> instead.
    /// </summary>
    public void BumpVersion()
    {
        EnsureNotArchived();
        Version++;
    }

    /// <summary>
    /// H-01 — version bump for writes to the DESCRIPTOR (the profile core and its 9 collections).
    /// Refuses on a published profile: the approved descriptor is frozen until <see cref="Reopen"/>.
    /// </summary>
    public void BumpDescriptorVersion()
    {
        EnsureEditable();
        Version++;
    }

    public void Publish()
    {
        // H-01: an explicit state guard. Without it, publishing an ARCHIVED profile fell through to
        // EnsureEditable() and surfaced as PUBLISH_REQUIREMENTS_MISSING — a misleading error for what
        // is really a state conflict.
        if (Status != JobProfileStatus.Draft)
        {
            throw new JobProfileStateException("Only a draft job profile can be published.");
        }

        if (string.IsNullOrWhiteSpace(Objective))
        {
            throw new InvalidOperationException("Objective is required to publish the job profile.");
        }

        if (_requirements.Count == 0)
        {
            throw new InvalidOperationException("At least one requirement is required to publish the job profile.");
        }

        if (_functions.Count == 0)
        {
            throw new InvalidOperationException("At least one function is required to publish the job profile.");
        }

        if (string.IsNullOrWhiteSpace(Responsibilities))
        {
            throw new InvalidOperationException("Responsibilities are required to publish the job profile.");
        }

        Status = JobProfileStatus.Published;
        IsActive = true;
        Version++;
        RefreshConcurrencyToken();
    }

    /// <summary>
    /// H-01 — <c>Published</c> → <c>Draft</c>, the controlled way to correct an approved descriptor.
    /// The reason lives in the audit trail, not on the aggregate. Existing position slots and their
    /// occupants are deliberately untouched: the state governs new downstream writes only.
    /// </summary>
    public void Reopen()
    {
        if (Status != JobProfileStatus.Published)
        {
            throw new JobProfileStateException("Only a published job profile can be reopened.");
        }

        Status = JobProfileStatus.Draft;
        Version++;
        RefreshConcurrencyToken();
    }

    public void Archive()
    {
        if (Status == JobProfileStatus.Archived)
        {
            return;
        }

        Status = JobProfileStatus.Archived;
        IsActive = false;
        Version++;
        RefreshConcurrencyToken();
    }

    /// <summary>
    /// H-01 — the DESCRIPTOR invariant, and the one every mutator of this aggregate routes through: an
    /// approved (published) job descriptor is a document with employment and audit value, so it is
    /// immutable until explicitly reopened, and an archived one is frozen for good.
    /// <para>
    /// Deliberately NOT the same check as <see cref="EnsureNotArchived"/>. The weaker one exists because
    /// <see cref="BumpVersion"/> routes through it, and the competency matrix — an operational overlay
    /// that stays writable on a published profile — is its only caller.
    /// </para>
    /// </summary>
    private void EnsureEditable()
    {
        EnsureNotArchived();

        if (Status == JobProfileStatus.Published)
        {
            throw new JobProfileStateException(
                "Published job profiles cannot be modified. Reopen the profile first.");
        }
    }

    /// <summary>
    /// The weaker invariant: only an ARCHIVED profile is frozen. Reached exclusively through
    /// <see cref="BumpVersion"/> — see the note there before adding callers.
    /// </summary>
    private void EnsureNotArchived()
    {
        if (Status == JobProfileStatus.Archived)
        {
            throw new JobProfileStateException("Archived job profiles cannot be modified.");
        }
    }

    private void SetCode(string code)
    {
        Code = JobProfileNormalization.NormalizeCode(code);
        NormalizedCode = Code;
    }

    private void SetTitle(string title)
    {
        Title = JobProfileNormalization.Clean(title, nameof(title));
        NormalizedTitle = JobProfileNormalization.NormalizeName(title);
    }

    private static void EnsurePositiveId(long id, string parameterName)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Identifier must be greater than zero.");
        }
    }

    private static DateTime? NormalizeOptionalUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
