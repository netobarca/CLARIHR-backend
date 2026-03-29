using CLARIHR.Domain.Locations;

namespace CLARIHR.Domain.OrgStructureCatalogs;

public static class CompanyTypeCatalog
{
    public static IReadOnlyList<CompanyTypeCatalogDefinition> Items { get; } =
    [
        Create(-8100, "SV", "SA_DE_CV", "Sociedad Anonima de Capital Variable", "Sociedad mercantil con capital representado en acciones y posibilidad de variacion de capital.", 10),
        Create(-8101, "SV", "S_DE_RL", "Sociedad de Responsabilidad Limitada", "Sociedad mercantil de cuotas con responsabilidad limitada al aporte de los socios.", 20),
        Create(-8102, "SV", "INDIVIDUAL_ENTERPRISE", "Empresa Individual", "Operacion empresarial inscrita a nombre de una sola persona.", 30),
        Create(-8103, "SV", "COOPERATIVE", "Cooperativa", "Entidad asociativa organizada bajo el regimen cooperativo.", 40),
        Create(-8104, "SV", "ASSOCIATION", "Asociacion", "Entidad asociativa sin fines de lucro reconocida legalmente.", 50),

        Create(-8200, "MX", "SA_DE_CV", "Sociedad Anonima de Capital Variable", "Sociedad mercantil mexicana con capital representado en acciones.", 10),
        Create(-8201, "MX", "S_DE_RL_DE_CV", "Sociedad de Responsabilidad Limitada de Capital Variable", "Sociedad mercantil mexicana de partes sociales con responsabilidad limitada.", 20),
        Create(-8202, "MX", "SAS", "Sociedad por Acciones Simplificada", "Sociedad mercantil simplificada constituida por accionistas.", 30),
        Create(-8203, "MX", "BRANCH_OFFICE", "Sucursal", "Establecimiento mexicano dependiente de una sociedad matriz.", 40),
        Create(-8204, "MX", "AC", "Asociacion Civil", "Persona moral de naturaleza civil sin fines preponderantemente mercantiles.", 50),

        Create(-8300, "US", "LLC", "Limited Liability Company", "Business entity with limited liability and flexible tax treatment.", 10),
        Create(-8301, "US", "C_CORP", "C Corporation", "Corporation taxed separately from its owners.", 20),
        Create(-8302, "US", "S_CORP", "S Corporation", "Corporation with pass-through taxation under eligible IRS election.", 30),
        Create(-8303, "US", "LLP", "Limited Liability Partnership", "Partnership structure with liability protections for partners.", 40),
        Create(-8304, "US", "NONPROFIT", "Nonprofit Corporation", "Corporation organized for charitable, educational or public benefit purposes.", 50)
    ];

    private static CompanyTypeCatalogDefinition Create(
        long id,
        string countryCode,
        string code,
        string name,
        string? description,
        int sortOrder)
    {
        var countryCatalogItemId = CountryCatalog.Items
            .Single(item => item.Code == countryCode)
            .Id;

        return new CompanyTypeCatalogDefinition(id, countryCatalogItemId, countryCode, code, name, description, sortOrder);
    }
}

public sealed record CompanyTypeCatalogDefinition(
    long Id,
    long CountryCatalogItemId,
    string CountryCode,
    string Code,
    string Name,
    string? Description,
    int SortOrder);
