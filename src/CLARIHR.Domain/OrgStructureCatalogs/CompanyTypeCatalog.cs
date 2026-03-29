namespace CLARIHR.Domain.OrgStructureCatalogs;

public static class CompanyTypeCatalog
{
    public static IReadOnlyList<CompanyTypeCatalogDefinition> Items { get; } =
    [
        new("SA_DE_CV", "Sociedad anonima de capital variable", "Forma societaria mercantil con accionistas y capital variable.", 10),
        new("LIMITED_LIABILITY", "Sociedad de responsabilidad limitada", "Entidad mercantil con socios y responsabilidad limitada al aporte.", 20),
        new("INDIVIDUAL_ENTERPRISE", "Empresa individual", "Operacion empresarial registrada a nombre de una sola persona.", 30),
        new("BRANCH_OFFICE", "Sucursal", "Operacion local dependiente de una sociedad matriz.", 40),
        new("COOPERATIVE", "Cooperativa", "Organizacion asociativa orientada a beneficio comun de sus asociados.", 50),
        new("ASSOCIATION", "Asociacion sin fines de lucro", "Entidad privada sin fines de lucro con fines asociativos o gremiales.", 60),
        new("FOUNDATION", "Fundacion", "Entidad sin fines de lucro orientada a fines sociales, educativos o beneficos.", 70),
        new("PUBLIC_INSTITUTION", "Institucion publica", "Entidad publica o autonoma con estructura formal de personal.", 80)
    ];
}

public sealed record CompanyTypeCatalogDefinition(
    string Code,
    string Name,
    string? Description,
    int SortOrder);
