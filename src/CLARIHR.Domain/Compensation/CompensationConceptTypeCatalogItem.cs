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
        bool isBaseSalary,
        decimal? defaultPensionedEmployerRate,
        decimal? minContributionBase,
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
        IsBaseSalary = isBaseSalary;
        DefaultPensionedEmployerRate = defaultPensionedEmployerRate;
        MinContributionBase = minContributionBase;
    }

    public CompensationNature Nature { get; private set; }

    public bool IsStatutory { get; private set; }

    public DeductionClass? DefaultDeductionClass { get; private set; }

    public CompensationCalculationType DefaultCalculationType { get; private set; }

    public string? DefaultCalculationBaseCode { get; private set; }

    public decimal? DefaultEmployeeRate { get; private set; }

    public decimal? DefaultEmployerRate { get; private set; }

    public decimal? ContributionCap { get; private set; }

    /// <summary>
    /// Marks the concept type that represents the employee's base salary (D-12/DP-08). The
    /// single-active-base-salary rule keys off this flag instead of the magic
    /// <c>SALARIO_BASE</c> code (kept only as fallback for unflagged catalogs).
    /// </summary>
    public bool IsBaseSalary { get; private set; }

    /// <summary>
    /// Employer rate applied when the employee is a working pensioner (RF-007/DP-05; SV LISP 2022:
    /// same 8.75% as the regular employer rate, with annual CIAP refund). Editable legal default.
    /// </summary>
    public decimal? DefaultPensionedEmployerRate { get; private set; }

    /// <summary>
    /// Minimum contribution base — IBC mínimo (RF-007/DP-05; SV: the current minimum wage).
    /// Editable legal default; <see cref="ContributionCap"/> carries the IBC máximo.
    /// </summary>
    public decimal? MinContributionBase { get; private set; }

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
        bool isBaseSalary,
        bool isActive,
        int sortOrder,
        decimal? defaultPensionedEmployerRate = null,
        decimal? minContributionBase = null) =>
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
            isBaseSalary,
            defaultPensionedEmployerRate,
            minContributionBase,
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
        bool isBaseSalary,
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
        IsBaseSalary = isBaseSalary;
    }

    private static string? NormalizeOptionalCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}
