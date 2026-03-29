using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CompanySubscription : AuditableEntity
{
    private CompanySubscription()
    {
    }

    private CompanySubscription(
        long companyId,
        long commercialPlanId,
        string planCode,
        string planName,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        SubscriptionStatus status,
        DateTime startDateUtc,
        DateTime? endDateUtc)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "Company id must be greater than zero.");
        }

        if (commercialPlanId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commercialPlanId), "Commercial plan id must be a persisted non-zero identifier.");
        }

        CompanyId = companyId;
        CommercialPlanId = commercialPlanId;
        PlanCode = CompanyNormalization.NormalizePlanCode(planCode);
        PlanName = CompanyNormalization.Clean(planName, nameof(planName));
        BaseMonthlyFee = NormalizeAmount(baseMonthlyFee, nameof(baseMonthlyFee));
        PricePerActiveEmployee = NormalizeAmount(pricePerActiveEmployee, nameof(pricePerActiveEmployee));
        Status = status;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
    }

    public long CompanyId { get; private set; }

    public long CommercialPlanId { get; private set; }

    public string PlanCode { get; private set; } = string.Empty;

    public string PlanName { get; private set; } = string.Empty;

    public decimal BaseMonthlyFee { get; private set; }

    public decimal PricePerActiveEmployee { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public DateTime StartDateUtc { get; private set; }

    public DateTime? EndDateUtc { get; private set; }

    public static CompanySubscription Activate(
        long companyId,
        long commercialPlanId,
        string planCode,
        string planName,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        DateTime startDateUtc) =>
        new(
            companyId,
            commercialPlanId,
            planCode,
            planName,
            baseMonthlyFee,
            pricePerActiveEmployee,
            SubscriptionStatus.Active,
            startDateUtc,
            endDateUtc: null);

    public static CompanySubscription Activate(long companyId, CommercialPlan commercialPlan, DateTime startDateUtc) =>
        Activate(
            companyId,
            commercialPlan.Id,
            commercialPlan.Code,
            commercialPlan.Name,
            commercialPlan.BaseMonthlyFee,
            commercialPlan.PricePerActiveEmployee,
            startDateUtc);

    public void Cancel(DateTime endDateUtc)
    {
        if (Status != SubscriptionStatus.Active)
        {
            throw new InvalidOperationException("Only active subscriptions can be cancelled.");
        }

        if (endDateUtc < StartDateUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(endDateUtc), "End date cannot be earlier than start date.");
        }

        Status = SubscriptionStatus.Cancelled;
        EndDateUtc = endDateUtc;
    }

    private static decimal NormalizeAmount(decimal value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Amount must be greater than or equal to zero.");
        }

        return value;
    }
}
