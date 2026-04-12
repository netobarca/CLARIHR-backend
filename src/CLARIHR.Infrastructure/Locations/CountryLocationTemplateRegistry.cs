using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Domain.Locations;

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
                ElSalvadorTerritorialCatalog.Departments
                    .Select(static department => new CountryLocationNode(
                        department.Code,
                        department.Name,
                        "Departamento",
                        ElSalvadorTerritorialCatalog.Municipalities
                            .Where(municipality => municipality.DepartmentCode == department.Code)
                            .Select(static municipality => new CountryLocationNode(
                                municipality.Code,
                                municipality.Name,
                                "Municipio",
                                []))
                            .ToArray()))
                    .ToArray()));
}

internal sealed record CountryLocationTemplate(
    string CountryCode,
    CountryLocationNode Root);

internal sealed record CountryLocationNode(
    string Code,
    string Name,
    string? Description,
    IReadOnlyCollection<CountryLocationNode> Children);
