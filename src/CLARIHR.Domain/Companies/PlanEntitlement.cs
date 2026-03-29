using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class PlanEntitlement : AuditableEntity
{
    private PlanEntitlement()
    {
    }

    private PlanEntitlement(long commercialPlanId, string planCode, string moduleKey, bool isEnabled)
    {
        if (commercialPlanId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commercialPlanId), "Commercial plan id must be a persisted non-zero identifier.");
        }

        CommercialPlanId = commercialPlanId;
        PlanCode = CompanyNormalization.NormalizePlanCode(planCode);
        ModuleKey = CompanyNormalization.NormalizeModuleKey(moduleKey);
        IsEnabled = isEnabled;
    }

    public long CommercialPlanId { get; private set; }

    public string PlanCode { get; private set; } = string.Empty;

    public string ModuleKey { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public static PlanEntitlement Create(long commercialPlanId, string planCode, string moduleKey, bool isEnabled) =>
        new(commercialPlanId, planCode, moduleKey, isEnabled);
}
