using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CommercialAddonEntitlement : AuditableEntity
{
    private CommercialAddonEntitlement()
    {
    }

    private CommercialAddonEntitlement(string addonCode, string moduleKey, bool isEnabled)
    {
        AddonCode = CompanyNormalization.NormalizePlanCode(addonCode);
        ModuleKey = CommercialModuleCatalog.NormalizeKnownKey(moduleKey);
        IsEnabled = isEnabled;
    }

    public long CommercialAddonId { get; private set; }

    public string AddonCode { get; private set; } = string.Empty;

    public string ModuleKey { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public static CommercialAddonEntitlement Create(string addonCode, string moduleKey, bool isEnabled = true) =>
        new(addonCode, moduleKey, isEnabled);
}
