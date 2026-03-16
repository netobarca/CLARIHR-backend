using CLARIHR.Application.Features.Locations.Common;

namespace CLARIHR.Infrastructure.Locations;

internal static class CountryLocationTemplateRegistry
{
    public static bool TryGet(string countryCode, out CountryLocationTemplate template)
    {
        switch (LocationValidationRules.NormalizeCountryCode(countryCode))
        {
            case LocationValidationRules.ElSalvadorCountryCode:
                template = CreateElSalvador();
                return true;
            default:
                template = default!;
                return false;
        }
    }

    private static CountryLocationTemplate CreateElSalvador() =>
        new(
            LocationValidationRules.ElSalvadorCountryCode,
            new CountryLocationNode(
                LocationValidationRules.ElSalvadorCountryCode,
                LocationValidationRules.ElSalvadorCountryName,
                "Pais",
                [
                    new CountryLocationNode(
                        "SS",
                        "San Salvador",
                        "Departamento",
                        [
                            new CountryLocationNode("APOPA", "Apopa", "Municipio", []),
                            new CountryLocationNode("MEJICANOS", "Mejicanos", "Municipio", [])
                        ])
                ]));
}

internal sealed record CountryLocationTemplate(
    string CountryCode,
    CountryLocationNode Root);

internal sealed record CountryLocationNode(
    string Code,
    string Name,
    string? Description,
    IReadOnlyCollection<CountryLocationNode> Children);
