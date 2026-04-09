using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class PlanEntitlement : AuditableEntity
{
    private PlanEntitlement()
    {
    }

    private PlanEntitlement(string planCode, string capabilityCode, bool isEnabled)
    {
        PlanCode = CompanyNormalization.NormalizePlanCode(planCode);
        CapabilityCode = CommercialCapabilityCatalog.NormalizeKnownCode(capabilityCode);
        ModuleKey = CommercialCapabilityCatalog.Get(CapabilityCode).ModuleKey;
        IsEnabled = isEnabled;
    }

    public long CommercialPlanId { get; private set; }

    public string PlanCode { get; private set; } = string.Empty;

    public string CapabilityCode { get; private set; } = string.Empty;

    public string ModuleKey { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public static PlanEntitlement Create(string planCode, string moduleKey, bool isEnabled = true) =>
        new(planCode, CommercialCapabilityCatalog.GetByModuleKey(moduleKey).Code, isEnabled);

    public static PlanEntitlement CreateForCapability(string planCode, string capabilityCode, bool isEnabled = true) =>
        new(planCode, capabilityCode, isEnabled);
}
