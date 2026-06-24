using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.CompetencyFramework;

public sealed class JobProfileCompetencyExpectation : TenantEntity
{
    private readonly List<JobProfileCompetencyExpectationConduct> _conducts = [];

    private JobProfileCompetencyExpectation()
    {
    }

    private JobProfileCompetencyExpectation(
        Guid publicId,
        long jobProfileId,
        long occupationalPyramidLevelId,
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long behaviorLevelCatalogItemId,
        string? expectedEvidence,
        decimal? expectedValue,
        int sortOrder)
    {
        if (jobProfileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(jobProfileId), "Job profile id must be greater than zero.");
        }

        if (occupationalPyramidLevelId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(occupationalPyramidLevelId), "Occupational pyramid level id must be greater than zero.");
        }

        if (competencyCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(competencyCatalogItemId), "Competency catalog item id must be greater than zero.");
        }

        if (competencyTypeCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(competencyTypeCatalogItemId), "Competency type catalog item id must be greater than zero.");
        }

        if (behaviorLevelCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(behaviorLevelCatalogItemId), "Behavior level catalog item id must be greater than zero.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        PublicId = publicId;
        JobProfileId = jobProfileId;
        OccupationalPyramidLevelId = occupationalPyramidLevelId;
        CompetencyCatalogItemId = competencyCatalogItemId;
        CompetencyTypeCatalogItemId = competencyTypeCatalogItemId;
        BehaviorLevelCatalogItemId = behaviorLevelCatalogItemId;
        ExpectedEvidence = CompetencyFrameworkNormalization.CleanOptional(expectedEvidence);
        ExpectedValue = expectedValue;
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long JobProfileId { get; private set; }

    public long OccupationalPyramidLevelId { get; private set; }

    public long CompetencyCatalogItemId { get; private set; }

    public long CompetencyTypeCatalogItemId { get; private set; }

    public long BehaviorLevelCatalogItemId { get; private set; }

    public string? ExpectedEvidence { get; private set; }

    /// <summary>
    /// The expected competency score for this matrix cell, expressed in the company's active
    /// <see cref="CompetencyRatingScale"/> (decision D-02/D-04). Optional; when present, the per-employee gap
    /// is computed as ExpectedValue − achieved score (decision D-05).
    /// </summary>
    public decimal? ExpectedValue { get; private set; }

    public int SortOrder { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<JobProfileCompetencyExpectationConduct> Conducts => _conducts;

    public static JobProfileCompetencyExpectation Create(
        long jobProfileId,
        long occupationalPyramidLevelId,
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long behaviorLevelCatalogItemId,
        string? expectedEvidence,
        decimal? expectedValue,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            jobProfileId,
            occupationalPyramidLevelId,
            competencyCatalogItemId,
            competencyTypeCatalogItemId,
            behaviorLevelCatalogItemId,
            expectedEvidence,
            expectedValue,
            sortOrder);

    public void ReplaceConducts(IEnumerable<JobProfileCompetencyExpectationConduct> items)
    {
        _conducts.Clear();
        _conducts.AddRange(items);
        RefreshConcurrencyToken();
    }

    public void Update(
        long occupationalPyramidLevelId,
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long behaviorLevelCatalogItemId,
        string? expectedEvidence,
        decimal? expectedValue,
        int sortOrder)
    {
        if (occupationalPyramidLevelId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(occupationalPyramidLevelId), "Occupational pyramid level id must be greater than zero.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        OccupationalPyramidLevelId = occupationalPyramidLevelId;
        CompetencyCatalogItemId = competencyCatalogItemId;
        CompetencyTypeCatalogItemId = competencyTypeCatalogItemId;
        BehaviorLevelCatalogItemId = behaviorLevelCatalogItemId;
        ExpectedEvidence = CompetencyFrameworkNormalization.CleanOptional(expectedEvidence);
        ExpectedValue = expectedValue;
        SortOrder = sortOrder;
        RefreshConcurrencyToken();
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
