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
        long commercialPlanVersionId,
        string planCode,
        string planName,
        int planVersionNumber,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        CompanySubscriptionPeriodicity periodicity,
        string currencyCode,
        SubscriptionStatus status,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        Guid activatedByUserPublicId,
        DateTime activatedAtUtc)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "Company id must be greater than zero.");
        }

        if (commercialPlanId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commercialPlanId), "Commercial plan id must be a persisted non-zero identifier.");
        }

        if (commercialPlanVersionId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commercialPlanVersionId), "Commercial plan version id must be a persisted non-zero identifier.");
        }

        if (planVersionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(planVersionNumber), "Plan version number must be greater than zero.");
        }

        if (!Enum.IsDefined(periodicity))
        {
            throw new ArgumentOutOfRangeException(nameof(periodicity), "Subscription periodicity is invalid.");
        }

        CompanyId = companyId;
        CommercialPlanId = commercialPlanId;
        CommercialPlanVersionId = commercialPlanVersionId;
        PlanCode = CompanyNormalization.NormalizePlanCode(planCode);
        PlanName = CompanyNormalization.Clean(planName, nameof(planName));
        PlanVersionNumber = planVersionNumber;
        BaseMonthlyFee = NormalizeAmount(baseMonthlyFee, nameof(baseMonthlyFee));
        PricePerActiveEmployee = NormalizeAmount(pricePerActiveEmployee, nameof(pricePerActiveEmployee));
        Periodicity = periodicity;
        CurrencyCode = CompanyNormalization.NormalizeCurrencyCode(currencyCode);
        Status = status;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        ActivatedByUserPublicId = activatedByUserPublicId;
        ActivatedAtUtc = activatedAtUtc;
    }

    public long CompanyId { get; private set; }

    public long CommercialPlanId { get; private set; }

    public long CommercialPlanVersionId { get; private set; }

    public string PlanCode { get; private set; } = string.Empty;

    public string PlanName { get; private set; } = string.Empty;

    public int PlanVersionNumber { get; private set; }

    public decimal BaseMonthlyFee { get; private set; }

    public decimal PricePerActiveEmployee { get; private set; }

    public CompanySubscriptionPeriodicity Periodicity { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public SubscriptionStatus Status { get; private set; }

    public DateTime StartDateUtc { get; private set; }

    public DateTime? EndDateUtc { get; private set; }

    public Guid ActivatedByUserPublicId { get; private set; }

    public DateTime ActivatedAtUtc { get; private set; }

    public static CompanySubscription Activate(long companyId, CommercialPlan commercialPlan, DateTime startDateUtc) =>
        Activate(
            companyId,
            commercialPlan,
            CompanySubscriptionPeriodicity.Monthly,
            startDateUtc,
            Guid.Empty,
            startDateUtc);

    public static CompanySubscription Activate(
        long companyId,
        CommercialPlan commercialPlan,
        CompanySubscriptionPeriodicity periodicity,
        DateTime startDateUtc,
        Guid activatedByUserPublicId,
        DateTime activatedAtUtc)
    {
        var planVersion = commercialPlan.GetVersionEffectiveOn(startDateUtc);

        return new CompanySubscription(
            companyId,
            commercialPlan.Id,
            planVersion.Id,
            commercialPlan.Code,
            commercialPlan.Name,
            planVersion.VersionNumber,
            planVersion.BaseMonthlyFee,
            planVersion.PricePerActiveEmployee,
            periodicity,
            planVersion.CurrencyCode,
            SubscriptionStatus.Active,
            startDateUtc,
            endDateUtc: null,
            activatedByUserPublicId,
            activatedAtUtc);
    }

    public static CompanySubscription Schedule(
        long companyId,
        CommercialPlan commercialPlan,
        CompanySubscriptionPeriodicity periodicity,
        DateTime startDateUtc,
        Guid activatedByUserPublicId,
        DateTime activatedAtUtc)
    {
        var planVersion = commercialPlan.GetVersionEffectiveOn(startDateUtc);

        return new CompanySubscription(
            companyId,
            commercialPlan.Id,
            planVersion.Id,
            commercialPlan.Code,
            commercialPlan.Name,
            planVersion.VersionNumber,
            planVersion.BaseMonthlyFee,
            planVersion.PricePerActiveEmployee,
            periodicity,
            planVersion.CurrencyCode,
            SubscriptionStatus.Scheduled,
            startDateUtc,
            endDateUtc: null,
            activatedByUserPublicId,
            activatedAtUtc);
    }

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

    public void PromoteScheduled(DateTime promotedAtUtc)
    {
        if (Status != SubscriptionStatus.Scheduled)
        {
            throw new InvalidOperationException("Only scheduled subscriptions can be promoted.");
        }

        if (promotedAtUtc < StartDateUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(promotedAtUtc), "Promotion cannot happen before the scheduled start date.");
        }

        Status = SubscriptionStatus.Active;
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
