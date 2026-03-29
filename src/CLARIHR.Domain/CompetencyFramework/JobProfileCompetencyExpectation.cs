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
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long JobProfileId { get; private set; }

    public long OccupationalPyramidLevelId { get; private set; }

    public long CompetencyCatalogItemId { get; private set; }

    public long CompetencyTypeCatalogItemId { get; private set; }

    public long BehaviorLevelCatalogItemId { get; private set; }

    public string? ExpectedEvidence { get; private set; }

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
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            jobProfileId,
            occupationalPyramidLevelId,
            competencyCatalogItemId,
            competencyTypeCatalogItemId,
            behaviorLevelCatalogItemId,
            expectedEvidence,
            sortOrder);

    public void ReplaceConducts(IEnumerable<JobProfileCompetencyExpectationConduct> items)
    {
        _conducts.Clear();
        _conducts.AddRange(items);
        RefreshConcurrencyToken();
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
