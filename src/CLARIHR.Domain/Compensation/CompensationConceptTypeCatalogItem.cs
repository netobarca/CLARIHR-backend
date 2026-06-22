using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Compensation;

/// <summary>
/// Catálogo enriquecido (country-scoped) de tipos de concepto de compensación (ingreso/egreso).
/// A diferencia de los catálogos generales simples, transporta atributos por defecto que el
/// frontend usa para precargar y que la futura nómina consume: naturaleza, si es de ley, clase de
/// descuento por defecto, modo y base de cálculo por defecto, y tasas/tope (ISSS/AFP). Sigue el
/// patrón de <see cref="CLARIHR.Domain.Banks.BankCatalogItem"/> (deriva de
/// <see cref="CountryScopedCatalogItem"/> y agrega columnas extra).
/// </summary>
public sealed class CompensationConceptTypeCatalogItem : CountryScopedCatalogItem
{
    private CompensationConceptTypeCatalogItem()
    {
    }

    private CompensationConceptTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        CompensationNature nature,
        bool isStatutory,
        DeductionClass? defaultDeductionClass,
        CompensationCalculationType defaultCalculationType,
        string? defaultCalculationBaseCode,
        decimal? defaultEmployeeRate,
        decimal? defaultEmployerRate,
        decimal? contributionCap,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        Nature = nature;
        IsStatutory = isStatutory;
        DefaultDeductionClass = defaultDeductionClass;
        DefaultCalculationType = defaultCalculationType;
        DefaultCalculationBaseCode = NormalizeOptionalCode(defaultCalculationBaseCode);
        DefaultEmployeeRate = defaultEmployeeRate;
        DefaultEmployerRate = defaultEmployerRate;
        ContributionCap = contributionCap;
    }

    public CompensationNature Nature { get; private set; }

    public bool IsStatutory { get; private set; }

    public DeductionClass? DefaultDeductionClass { get; private set; }

    public CompensationCalculationType DefaultCalculationType { get; private set; }

    public string? DefaultCalculationBaseCode { get; private set; }

    public decimal? DefaultEmployeeRate { get; private set; }

    public decimal? DefaultEmployerRate { get; private set; }

    public decimal? ContributionCap { get; private set; }

    public static CompensationConceptTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        CompensationNature nature,
        bool isStatutory,
        DeductionClass? defaultDeductionClass,
        CompensationCalculationType defaultCalculationType,
        string? defaultCalculationBaseCode,
        decimal? defaultEmployeeRate,
        decimal? defaultEmployerRate,
        decimal? contributionCap,
        bool isActive,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            countryCatalogItemId,
            countryCode,
            code,
            name,
            nature,
            isStatutory,
            defaultDeductionClass,
            defaultCalculationType,
            defaultCalculationBaseCode,
            defaultEmployeeRate,
            defaultEmployerRate,
            contributionCap,
            isActive,
            sortOrder);

    public void UpdateDetails(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        CompensationNature nature,
        bool isStatutory,
        DeductionClass? defaultDeductionClass,
        CompensationCalculationType defaultCalculationType,
        string? defaultCalculationBaseCode,
        decimal? defaultEmployeeRate,
        decimal? defaultEmployerRate,
        decimal? contributionCap,
        int sortOrder)
    {
        base.Update(countryCatalogItemId, countryCode, code, name, sortOrder);
        Nature = nature;
        IsStatutory = isStatutory;
        DefaultDeductionClass = defaultDeductionClass;
        DefaultCalculationType = defaultCalculationType;
        DefaultCalculationBaseCode = NormalizeOptionalCode(defaultCalculationBaseCode);
        DefaultEmployeeRate = defaultEmployeeRate;
        DefaultEmployerRate = defaultEmployerRate;
        ContributionCap = contributionCap;
    }

    private static string? NormalizeOptionalCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}
