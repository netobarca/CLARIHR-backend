using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CommercialAddonEntitlement : AuditableEntity
{
    private CommercialAddonEntitlement()
    {
    }

    private CommercialAddonEntitlement(string addonCode, string capabilityCode, bool isEnabled)
    {
        AddonCode = CompanyNormalization.NormalizePlanCode(addonCode);
        CapabilityCode = CommercialCapabilityCatalog.NormalizeKnownCode(capabilityCode);
        ModuleKey = CommercialCapabilityCatalog.Get(CapabilityCode).ModuleKey;
        IsEnabled = isEnabled;
    }

    public long CommercialAddonId { get; private set; }

    public string AddonCode { get; private set; } = string.Empty;

    public string CapabilityCode { get; private set; } = string.Empty;

    public string ModuleKey { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public static CommercialAddonEntitlement Create(string addonCode, string moduleKey, bool isEnabled = true) =>
        new(addonCode, CommercialCapabilityCatalog.GetByModuleKey(moduleKey).Code, isEnabled);

    public static CommercialAddonEntitlement CreateForCapability(string addonCode, string capabilityCode, bool isEnabled = true) =>
        new(addonCode, capabilityCode, isEnabled);
}
