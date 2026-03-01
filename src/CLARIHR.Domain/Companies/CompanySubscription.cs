using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CompanySubscription : AuditableEntity
{
    private CompanySubscription()
    {
    }

    private CompanySubscription(
        long companyId,
        string planCode,
        SubscriptionStatus status,
        DateTime startDateUtc,
        DateTime? endDateUtc)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "Company id must be greater than zero.");
        }

        CompanyId = companyId;
        PlanCode = CompanyNormalization.NormalizePlanCode(planCode);
        Status = status;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
    }

    public long CompanyId { get; private set; }

    public string PlanCode { get; private set; } = string.Empty;

    public SubscriptionStatus Status { get; private set; }

    public DateTime StartDateUtc { get; private set; }

    public DateTime? EndDateUtc { get; private set; }

    public static CompanySubscription Activate(long companyId, string planCode, DateTime startDateUtc) =>
        new(companyId, planCode, SubscriptionStatus.Active, startDateUtc, endDateUtc: null);
}
