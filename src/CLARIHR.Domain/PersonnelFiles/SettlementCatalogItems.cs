using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Section of the settlement ("liquidación") calculation a concept belongs to (D-07/D-13 of the
/// settlement module). Persisted as a string on <see cref="SettlementConceptCatalogItem"/> and on
/// each settlement line. The reserve/provision and the summary are computed blocks, not concept
/// classes.
/// </summary>
public enum SettlementConceptClass
{
    Ingreso = 0,
    Descuento = 1,
    PagoPatronal = 2,
}

/// <summary>
/// Income-tax exemption rule the engine applies per concept (RN-10, ratified: the system controls
/// the taxable excess — it is never left to a manual override). Persisted as a string.
/// </summary>
public enum SettlementExemptionRule
{
    /// <summary>Fully taxable (no exemption): the whole amount joins the Renta base.</summary>
    Ninguna = 0,

    /// <summary>
    /// Exempt up to <c>ExemptionMultiplier × minimum monthly wage</c> (SV: aguinaldo, 2× — P-02);
    /// only the excess joins the Renta base.
    /// </summary>
    HastaLimitePorMinimo = 1,

    /// <summary>
    /// Exempt up to the legally computed amount (SV: indemnización / renuncia voluntaria — Art. 4
    /// LISR); any excess (typically an upward override) joins the Renta base.
    /// </summary>
    HastaMontoLegal = 2,
}

/// <summary>
/// Country-scoped enriched catalog of settlement ("liquidación") concepts (D-07, seed SV — 17
/// codes). Beyond code/name it carries the attributes the calculation engine consumes: the section
/// (<see cref="SettlementConceptClass"/>), the ISSS/AFP/Renta affectation matrix (income concepts),
/// the income-tax <see cref="SettlementExemptionRule"/> with its optional multiplier, whether the
/// engine computes the line (vs manual entry), and the employer rate for the pagos-patronales
/// section (ISSS_PATRONAL 7.50 / AFP_PATRONAL 8.75 / INCAF 1.00). Mirrors
/// <see cref="CLARIHR.Domain.Compensation.CompensationConceptTypeCatalogItem"/> (derives from
/// <see cref="CountryScopedCatalogItem"/> and adds business columns; seeded via HasData).
/// </summary>
public sealed class SettlementConceptCatalogItem : CountryScopedCatalogItem
{
    private SettlementConceptCatalogItem()
    {
    }

    private SettlementConceptCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        SettlementConceptClass conceptClass,
        bool affectsIsss,
        bool affectsAfp,
        bool affectsRenta,
        SettlementExemptionRule exemptionRule,
        decimal? exemptionMultiplier,
        bool isSystemCalculated,
        decimal? defaultRatePercent,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        ConceptClass = conceptClass;
        AffectsIsss = affectsIsss;
        AffectsAfp = affectsAfp;
        AffectsRenta = affectsRenta;
        ExemptionRule = exemptionRule;
        ExemptionMultiplier = exemptionMultiplier;
        IsSystemCalculated = isSystemCalculated;
        DefaultRatePercent = defaultRatePercent;
    }

    public SettlementConceptClass ConceptClass { get; private set; }

    /// <summary>Whether the (income) concept joins the ISSS contribution base.</summary>
    public bool AffectsIsss { get; private set; }

    /// <summary>Whether the (income) concept joins the AFP contribution base.</summary>
    public bool AffectsAfp { get; private set; }

    /// <summary>Whether the (income) concept joins the Renta (income-tax) base, subject to <see cref="ExemptionRule"/>.</summary>
    public bool AffectsRenta { get; private set; }

    public SettlementExemptionRule ExemptionRule { get; private set; }

    /// <summary>Multiplier over the minimum monthly wage for <see cref="SettlementExemptionRule.HastaLimitePorMinimo"/> (SV aguinaldo: 2.00).</summary>
    public decimal? ExemptionMultiplier { get; private set; }

    /// <summary>True when the engine computes the line (formulas/suggestions); false for manual-amount concepts.</summary>
    public bool IsSystemCalculated { get; private set; }

    /// <summary>Employer rate (%) for pagos-patronales concepts (ISSS_PATRONAL 7.50 / AFP_PATRONAL 8.75 / INCAF 1.00 — P-02).</summary>
    public decimal? DefaultRatePercent { get; private set; }

    public static SettlementConceptCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        SettlementConceptClass conceptClass,
        bool affectsIsss,
        bool affectsAfp,
        bool affectsRenta,
        SettlementExemptionRule exemptionRule,
        decimal? exemptionMultiplier,
        bool isSystemCalculated,
        decimal? defaultRatePercent,
        bool isActive,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            countryCatalogItemId,
            countryCode,
            code,
            name,
            conceptClass,
            affectsIsss,
            affectsAfp,
            affectsRenta,
            exemptionRule,
            exemptionMultiplier,
            isSystemCalculated,
            defaultRatePercent,
            isActive,
            sortOrder);
}
