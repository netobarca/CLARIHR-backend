using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class PlanEntitlement : AuditableEntity
{
    private PlanEntitlement()
    {
    }

    private PlanEntitlement(string planCode, string moduleKey, bool isEnabled)
    {
        PlanCode = CompanyNormalization.NormalizePlanCode(planCode);
        ModuleKey = CommercialModuleCatalog.NormalizeKnownKey(moduleKey);
        IsEnabled = isEnabled;
    }

    public long CommercialPlanId { get; private set; }

    public string PlanCode { get; private set; } = string.Empty;

    public string ModuleKey { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public static PlanEntitlement Create(string planCode, string moduleKey, bool isEnabled = true) =>
        new(planCode, moduleKey, isEnabled);
}
