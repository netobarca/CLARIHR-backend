using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.CompetencyFramework;

public sealed class JobProfileCompetencyExpectationConduct : TenantEntity
{
    private JobProfileCompetencyExpectationConduct()
    {
    }

    private JobProfileCompetencyExpectationConduct(long competencyConductId, int sortOrder)
    {
        if (competencyConductId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(competencyConductId), "Competency conduct id must be greater than zero.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        CompetencyConductId = competencyConductId;
        SortOrder = sortOrder;
    }

    public long JobProfileCompetencyExpectationId { get; private set; }

    public JobProfileCompetencyExpectation JobProfileCompetencyExpectation { get; private set; } = null!;

    public long CompetencyConductId { get; private set; }

    public int SortOrder { get; private set; }

    public static JobProfileCompetencyExpectationConduct Create(long competencyConductId, int sortOrder) =>
        new(competencyConductId, sortOrder);
}
