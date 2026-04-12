namespace CLARIHR.Domain.Locations;

public static class ElSalvadorTerritorialCatalog
{
    public const string CountryCode = "SV";
    public const string CountryName = "El Salvador";

    public static IReadOnlyList<ElSalvadorDepartmentDefinition> Departments { get; } =
    [
        new("AHUACHAPAN", "Ahuachapan", ["AHUACHAPAN_CENTRO", "AHUACHAPAN_NORTE", "AHUACHAPAN_SUR"]),
        new("SANTA_ANA", "Santa Ana", ["SANTA_ANA_CENTRO", "SANTA_ANA_ESTE", "SANTA_ANA_NORTE", "SANTA_ANA_OESTE"]),
        new("SONSONATE", "Sonsonate", ["SONSONATE_CENTRO", "SONSONATE_ESTE", "SONSONATE_NORTE", "SONSONATE_OESTE"]),
        new("CHALATENANGO", "Chalatenango", ["CHALATENANGO_CENTRO", "CHALATENANGO_NORTE", "CHALATENANGO_SUR"]),
        new("LA_LIBERTAD", "La Libertad", ["LA_LIBERTAD_CENTRO", "LA_LIBERTAD_COSTA", "LA_LIBERTAD_ESTE", "LA_LIBERTAD_NORTE", "LA_LIBERTAD_SUR", "LA_LIBERTAD_OESTE"]),
        new("SAN_SALVADOR", "San Salvador", ["SAN_SALVADOR_CENTRO", "SAN_SALVADOR_ESTE", "SAN_SALVADOR_NORTE", "SAN_SALVADOR_OESTE", "SAN_SALVADOR_SUR"]),
        new("CUSCATLAN", "Cuscatlan", ["CUSCATLAN_NORTE", "CUSCATLAN_SUR"]),
        new("LA_PAZ", "La Paz", ["LA_PAZ_CENTRO", "LA_PAZ_ESTE", "LA_PAZ_OESTE"]),
        new("CABANAS", "Cabanas", ["CABANAS_ESTE", "CABANAS_OESTE"]),
        new("SAN_VICENTE", "San Vicente", ["SAN_VICENTE_NORTE", "SAN_VICENTE_SUR"]),
        new("USULUTAN", "Usulutan", ["USULUTAN_ESTE", "USULUTAN_NORTE", "USULUTAN_OESTE"]),
        new("SAN_MIGUEL", "San Miguel", ["SAN_MIGUEL_CENTRO", "SAN_MIGUEL_NORTE", "SAN_MIGUEL_OESTE"]),
        new("MORAZAN", "Morazan", ["MORAZAN_NORTE", "MORAZAN_SUR"]),
        new("LA_UNION", "La Union", ["LA_UNION_NORTE", "LA_UNION_SUR"])
    ];

    public static IReadOnlyList<ElSalvadorMunicipalityDefinition> Municipalities { get; } =
        Departments
            .SelectMany(
                static department => department.MunicipalityCodes.Select(
                    municipalityCode => new ElSalvadorMunicipalityDefinition(
                        municipalityCode,
                        HumanizeCode(municipalityCode),
                        department.Code)))
            .ToArray();

    private static string HumanizeCode(string code)
    {
        var words = code
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(static word => word[..1] + word[1..].ToLowerInvariant());

        return string.Join(" ", words);
    }
}

public sealed record ElSalvadorDepartmentDefinition(
    string Code,
    string Name,
    IReadOnlyCollection<string> MunicipalityCodes);

public sealed record ElSalvadorMunicipalityDefinition(
    string Code,
    string Name,
    string DepartmentCode);
