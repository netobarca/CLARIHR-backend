using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.GeneralCatalogs;

public abstract class GeneralCatalogItem : CountryScopedCatalogItem
{
    protected GeneralCatalogItem()
    {
    }

    protected GeneralCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }
}

public sealed class LanguageCatalogItem : GeneralCatalogItem
{
    private LanguageCatalogItem()
    {
    }

    private LanguageCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static LanguageCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class LanguageLevelCatalogItem : GeneralCatalogItem
{
    private LanguageLevelCatalogItem()
    {
    }

    private LanguageLevelCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static LanguageLevelCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class TrainingTypeCatalogItem : GeneralCatalogItem
{
    private TrainingTypeCatalogItem()
    {
    }

    private TrainingTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static TrainingTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class AssignmentTypeCatalogItem : GeneralCatalogItem
{
    private AssignmentTypeCatalogItem()
    {
    }

    private AssignmentTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static AssignmentTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class SubstitutionTypeCatalogItem : GeneralCatalogItem
{
    private SubstitutionTypeCatalogItem()
    {
    }

    private SubstitutionTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static SubstitutionTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class MedicalClaimTypeCatalogItem : GeneralCatalogItem
{
    private MedicalClaimTypeCatalogItem()
    {
    }

    private MedicalClaimTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static MedicalClaimTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class MedicalClaimStatusCatalogItem : GeneralCatalogItem
{
    private MedicalClaimStatusCatalogItem()
    {
    }

    private MedicalClaimStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static MedicalClaimStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class AssetAccessTypeCatalogItem : GeneralCatalogItem
{
    private AssetAccessTypeCatalogItem()
    {
    }

    private AssetAccessTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static AssetAccessTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of bank-account types ("tipos de cuenta bancaria": ahorro, corriente, planilla…),
/// backing the MANDATORY <c>accountTypeCode</c> of personnel-file bank accounts (general-catalogs key
/// <c>account-types</c>). Previously the field was free text with no catalog to populate a combobox.
/// </summary>
public sealed class BankAccountTypeCatalogItem : GeneralCatalogItem
{
    private BankAccountTypeCatalogItem()
    {
    }

    private BankAccountTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static BankAccountTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Catalog of off-payroll transaction types ("tipos de transacción fuera de nómina": herramientas, EPP,
/// uniformes, promocionales, reconocimientos, regalos…). Country-scoped and user-managed: the <c>Code</c> is
/// entered by the administrator (D-03) and <c>Name</c> is the business "Descripción". Distinct from
/// <see cref="AssetAccessTypeCatalogItem"/> (custody assets) by decision D-02 — they are NOT shared.
/// </summary>
public sealed class OffPayrollTransactionTypeCatalogItem : GeneralCatalogItem
{
    private OffPayrollTransactionTypeCatalogItem()
    {
    }

    private OffPayrollTransactionTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static OffPayrollTransactionTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Catalog of employee economic-aid request types ("tipos de ayuda económica": emergencia médica,
/// gastos fúnebres, desastre natural, incendio, calamidad doméstica, accidente, otra). Country-scoped
/// and user-managed: the <c>Code</c> is entered by the administrator and <c>Name</c> is the business
/// "Descripción".
/// </summary>
public sealed class EconomicAidTypeCatalogItem : GeneralCatalogItem
{
    private EconomicAidTypeCatalogItem()
    {
    }

    private EconomicAidTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static EconomicAidTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Catalog of economic-aid request statuses ("estados de la solicitud": SOLICITADA, EN_REVISION,
/// PENDIENTE_DOCUMENTACION, APROBADA, RECHAZADA, DESEMBOLSADA, ANULADA). Country-scoped; the codes are
/// structural (seeded) — the dashboard/forward-compatible flow may add intermediate states.
/// </summary>
public sealed class EconomicAidStatusCatalogItem : GeneralCatalogItem
{
    private EconomicAidStatusCatalogItem()
    {
    }

    private EconomicAidStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static EconomicAidStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Catalog of employee certificate ("constancia") types: salario, laboral, embajada, tiempo laborado,
/// no descuento, carta de recomendación. Country-scoped and user-managed (the <c>Code</c> is entered by
/// the administrator). The canonical codes (CONSTANCIA_SALARIO, CONSTANCIA_EMBAJADA) drive whether the
/// generated PDF prints salary (D-15/D-20).
/// </summary>
public sealed class CertificateTypeCatalogItem : GeneralCatalogItem
{
    private CertificateTypeCatalogItem()
    {
    }

    private CertificateTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static CertificateTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Catalog of certificate-request statuses ("estados de la solicitud": SOLICITADA, EN_PROCESO, EMITIDA,
/// ENTREGADA, RECHAZADA, ANULADA). Country-scoped; codes are structural (seeded). The linear lifecycle
/// (D-04) references the canonical codes in <c>CertificateRequestStatuses</c>.
/// </summary>
public sealed class CertificateRequestStatusCatalogItem : GeneralCatalogItem
{
    private CertificateRequestStatusCatalogItem()
    {
    }

    private CertificateRequestStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static CertificateRequestStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Catalog of retirement-request statuses ("estados de la solicitud de retiro": SOLICITADA, AUTORIZADA,
/// RECHAZADA, ANULADA, EJECUTADA, REVERTIDA). Country-scoped; codes are structural (seeded) — the state
/// machine (D-04) references the canonical codes in <c>RetirementRequestStatuses</c>.
/// </summary>
public sealed class RetirementRequestStatusCatalogItem : GeneralCatalogItem
{
    private RetirementRequestStatusCatalogItem()
    {
    }

    private RetirementRequestStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static RetirementRequestStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Catalog of settlement ("liquidación") statuses: BORRADOR, EMITIDA, ANULADA. Country-scoped; codes
/// are structural (seeded) — the lifecycle (settlement module D-15) references the canonical codes in
/// <c>SettlementStatuses</c>. Scenarios (<c>Kind = ESCENARIO</c>) have no lifecycle and never carry a
/// status.
/// </summary>
public sealed class SettlementStatusCatalogItem : GeneralCatalogItem
{
    private SettlementStatusCatalogItem()
    {
    }

    private SettlementStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static SettlementStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>Catalog of certificate delivery methods ("medio de entrega": presencial, correo, portal). Country-scoped (D-18).</summary>
public sealed class CertificateDeliveryMethodCatalogItem : GeneralCatalogItem
{
    private CertificateDeliveryMethodCatalogItem()
    {
    }

    private CertificateDeliveryMethodCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static CertificateDeliveryMethodCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>Catalog of certificate purposes ("propósito": trámite bancario, crédito, visa/embajada, etc.). Country-scoped (D-18).</summary>
public sealed class CertificatePurposeCatalogItem : GeneralCatalogItem
{
    private CertificatePurposeCatalogItem()
    {
    }

    private CertificatePurposeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static CertificatePurposeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class DeliveryStatusCatalogItem : GeneralCatalogItem
{
    private DeliveryStatusCatalogItem()
    {
    }

    private DeliveryStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static DeliveryStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class PaymentMethodCatalogItem : GeneralCatalogItem
{
    private PaymentMethodCatalogItem()
    {
    }

    private PaymentMethodCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static PaymentMethodCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class EmploymentStatusCatalogItem : GeneralCatalogItem
{
    private EmploymentStatusCatalogItem()
    {
    }

    private EmploymentStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static EmploymentStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class DurationUnitCatalogItem : GeneralCatalogItem
{
    private DurationUnitCatalogItem()
    {
    }

    private DurationUnitCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static DurationUnitCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class ReferenceTypeCatalogItem : GeneralCatalogItem
{
    private ReferenceTypeCatalogItem()
    {
    }

    private ReferenceTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static ReferenceTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class CurrencyCatalogItem : GeneralCatalogItem
{
    private CurrencyCatalogItem()
    {
    }

    private CurrencyCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static CurrencyCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class PayPeriodCatalogItem : GeneralCatalogItem
{
    private PayPeriodCatalogItem()
    {
    }

    private PayPeriodCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static PayPeriodCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class CalculationBaseCatalogItem : GeneralCatalogItem
{
    private CalculationBaseCatalogItem()
    {
    }

    private CalculationBaseCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static CalculationBaseCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class ExperienceMetricCatalogItem : GeneralCatalogItem
{
    private ExperienceMetricCatalogItem()
    {
    }

    private ExperienceMetricCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static ExperienceMetricCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of employment contract types (general-catalogs key <c>contract-types</c>) backing
/// the <c>contractTypeCode</c> of the manual contract-history endpoint. Enriched (RF-011, DP-03): carries
/// an optional short <see cref="Abbreviation"/> and the <see cref="IsTemporary"/> flag that marks
/// fixed-term/temporary modalities; both are delivered by seed and surfaced via the dedicated
/// <c>GET /api/v1/contract-types</c> read (the generic catalog DTO stays thin).
/// </summary>
public sealed class ContractTypeCatalogItem : GeneralCatalogItem
{
    private ContractTypeCatalogItem()
    {
    }

    private ContractTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        string? abbreviation,
        bool isTemporary)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        Abbreviation = NormalizeOptionalAbbreviation(abbreviation);
        IsTemporary = isTemporary;
    }

    /// <summary>Optional short label (e.g. INDEF, PF) for compact grids/prints.</summary>
    public string? Abbreviation { get; private set; }

    /// <summary>True for fixed-term/temporary contract modalities (plazo fijo, obra, eventual…).</summary>
    public bool IsTemporary { get; private set; }

    public static ContractTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        string? abbreviation = null,
        bool isTemporary = false) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder, abbreviation, isTemporary);

    private static string? NormalizeOptionalAbbreviation(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}

/// <summary>
/// Country-scoped catalog of personnel-action types (general-catalogs key <c>action-types</c>) backing the
/// MANDATORY <c>actionTypeCode</c> of the append-only personnel-actions journal. Mirrors
/// <see cref="AssignmentTypeCatalogItem"/>; seeded for SV (includes the RECONTRATACION code the rehire flow emits).
/// </summary>
public sealed class ActionTypeCatalogItem : GeneralCatalogItem
{
    private ActionTypeCatalogItem()
    {
    }

    private ActionTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static ActionTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of personnel-action statuses (general-catalogs key <c>action-statuses</c>) backing
/// the MANDATORY <c>actionStatusCode</c> of the personnel-actions journal. Mirrors
/// <see cref="AssignmentTypeCatalogItem"/>; seeded for SV.
/// </summary>
public sealed class ActionStatusCatalogItem : GeneralCatalogItem
{
    private ActionStatusCatalogItem()
    {
    }

    private ActionStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static ActionStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of payroll (planilla) types (general-catalogs key <c>payroll-types</c>) backing the
/// optional <c>payrollTypeCode</c> of an employment assignment — the contractual pay modality
/// (MENSUAL/QUINCENAL/SEMANAL/…), distinct from <see cref="ContractTypeCatalogItem"/> (the contract nature).
/// Mirrors <see cref="ActionTypeCatalogItem"/>; seeded for SV (REQ-004 · tablero de acciones de personal).
/// </summary>
public sealed class PayrollTypeCatalogItem : GeneralCatalogItem
{
    private PayrollTypeCatalogItem()
    {
    }

    private PayrollTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static PayrollTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of recurring-income lifecycle STATUSES (general-catalogs key
/// <c>recurring-income-statuses</c>) backing the <c>statusCode</c> of a personnel-file recurring income
/// (REQ-005 · planilla ingresos cíclicos — the one-decision lifecycle EN_REVISION/VIGENTE/RECHAZADO/
/// SUSPENDIDO/FINALIZADO/ANULADO, D-14/P-03). Mirrors <see cref="PayrollTypeCatalogItem"/>; seeded for SV.
/// </summary>
public sealed class RecurringIncomeStatusCatalogItem : GeneralCatalogItem
{
    private RecurringIncomeStatusCatalogItem()
    {
    }

    private RecurringIncomeStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static RecurringIncomeStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of recurring-income SETTLEMENT ACTIONS (general-catalogs key
/// <c>recurring-income-settlement-actions</c>) backing the <c>settlementActionCode</c> of a personnel-file
/// recurring income (REQ-005 · planilla ingresos cíclicos — what happens to the outstanding plan when the
/// employee is settled: PAGAR_SALDO / CANCELAR, P-06). Mirrors <see cref="PayrollTypeCatalogItem"/>;
/// seeded for SV.
/// </summary>
public sealed class RecurringIncomeSettlementActionCatalogItem : GeneralCatalogItem
{
    private RecurringIncomeSettlementActionCatalogItem()
    {
    }

    private RecurringIncomeSettlementActionCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static RecurringIncomeSettlementActionCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of recurring-income TYPES (general-catalogs key <c>recurring-income-types</c>)
/// backing the <c>recurringIncomeTypeCode</c> of a personnel-file recurring income (REQ-005 · planilla
/// ingresos cíclicos — permanent salary-independent perks: ayuda para alimentación, gastos de
/// representación, combustible…, P-02, an editable template). Mirrors <see cref="PayrollTypeCatalogItem"/>;
/// seeded for SV.
/// </summary>
public sealed class RecurringIncomeTypeCatalogItem : GeneralCatalogItem
{
    private RecurringIncomeTypeCatalogItem()
    {
    }

    private RecurringIncomeTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static RecurringIncomeTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of one-time-income lifecycle STATUSES (general-catalogs key
/// <c>one-time-income-statuses</c>) backing the <c>statusCode</c> of a personnel-file one-time income
/// (REQ-006 · planilla ingresos eventuales — the authorization lifecycle EN_REVISION/AUTORIZADO/RECHAZADO/
/// APLICADO/ANULADO, where APLICADO is reversible; P-01). Mirrors <see cref="RecurringIncomeStatusCatalogItem"/>;
/// seeded for SV.
/// </summary>
public sealed class OneTimeIncomeStatusCatalogItem : GeneralCatalogItem
{
    private OneTimeIncomeStatusCatalogItem()
    {
    }

    private OneTimeIncomeStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static OneTimeIncomeStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of overtime-record lifecycle STATUSES (general-catalogs key
/// <c>overtime-record-statuses</c>) backing the <c>statusCode</c> of a personnel-file overtime record
/// (REQ-007 · horas extras del empleado — the authorization lifecycle EN_REVISION/AUTORIZADA/RECHAZADA/
/// APLICADA/ANULADA, where APLICADA is reversible; P-01/P-07). Feminine ("solicitud"). Mirrors
/// <see cref="OneTimeIncomeStatusCatalogItem"/>; seeded for SV.
/// </summary>
public sealed class OvertimeRecordStatusCatalogItem : GeneralCatalogItem
{
    private OvertimeRecordStatusCatalogItem()
    {
    }

    private OvertimeRecordStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static OvertimeRecordStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of hobbies (general-catalogs key <c>hobbies</c>) backing the required
/// <c>hobbyCode</c> of a personnel-file hobby (RF-005, DP-07). Seeded for SV.
/// </summary>
public sealed class HobbyCatalogItem : GeneralCatalogItem
{
    private HobbyCatalogItem()
    {
    }

    private HobbyCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static HobbyCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of association TYPES (general-catalogs key <c>associations</c>) backing the
/// required <c>associationCode</c> of a personnel-file association (RF-006, DP-07). Seeded for SV.
/// </summary>
public sealed class AssociationCatalogItem : GeneralCatalogItem
{
    private AssociationCatalogItem()
    {
    }

    private AssociationCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static AssociationCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of additional-benefit types (general-catalogs key
/// <c>additional-benefit-types</c>) backing the existing <c>benefitTypeCode</c> of a personnel-file
/// additional benefit (RF-010). Seeded for SV.
/// </summary>
public sealed class AdditionalBenefitTypeCatalogItem : GeneralCatalogItem
{
    private AdditionalBenefitTypeCatalogItem()
    {
    }

    private AdditionalBenefitTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static AdditionalBenefitTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of medical-clinic sectors (general-catalogs key <c>clinic-sectors</c>:
/// ISSS, pública, privada) backing the optional <c>sectorCode</c> of a company medical clinic
/// (vacaciones e incapacidades module). Seeded for SV.
/// </summary>
public sealed class ClinicSectorCatalogItem : GeneralCatalogItem
{
    private ClinicSectorCatalogItem()
    {
    }

    private ClinicSectorCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static ClinicSectorCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of incapacity registration statuses (general-catalogs key
/// <c>incapacity-statuses</c>: en revisión, registrada, anulada) backing the lifecycle of an
/// employee incapacity record (vacaciones e incapacidades module). Seeded for SV.
/// </summary>
public sealed class IncapacityStatusCatalogItem : GeneralCatalogItem
{
    private IncapacityStatusCatalogItem()
    {
    }

    private IncapacityStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static IncapacityStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of vacation request statuses (general-catalogs key
/// <c>vacation-request-statuses</c>: solicitada, aprobada, rechazada, anulada, devuelta parcial,
/// devuelta) backing the lifecycle of an employee vacation request (vacaciones e incapacidades
/// module). Seeded for SV.
/// </summary>
public sealed class VacationRequestStatusCatalogItem : GeneralCatalogItem
{
    private VacationRequestStatusCatalogItem()
    {
    }

    private VacationRequestStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static VacationRequestStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of compensatory-time record statuses (general-catalogs key
/// <c>compensatory-time-statuses</c>: registrada, anulada) backing the lifecycle of a compensatory-time
/// credit / absence (REQ-002). Hybrid model (D-15): the domain constants are canonical; this catalog
/// backs i18n/UI. Seeded for SV.
/// </summary>
public sealed class CompensatoryTimeStatusCatalogItem : GeneralCatalogItem
{
    private CompensatoryTimeStatusCatalogItem()
    {
    }

    private CompensatoryTimeStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static CompensatoryTimeStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of compensatory-time operations (general-catalogs key
/// <c>compensatory-time-operations</c>: acredita, debita, ambas) describing whether a compensatory-time
/// type credits, debits, or can do both to the fund (REQ-002). Hybrid model (D-15): the domain
/// constants (<c>CompensatoryTimeOperations</c>) are canonical; this catalog backs i18n/UI. Seeded for SV.
/// </summary>
public sealed class CompensatoryTimeOperationCatalogItem : GeneralCatalogItem
{
    private CompensatoryTimeOperationCatalogItem()
    {
    }

    private CompensatoryTimeOperationCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static CompensatoryTimeOperationCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of personnel-transaction statuses (general-catalogs key
/// <c>personnel-transaction-statuses</c>: en revisión, aplicada, rechazada, anulada) backing the
/// one-decision lifecycle shared by recognitions and disciplinary actions ("otras transacciones de
/// personal", REQ-003 D-15). Hybrid model: the domain constants (<c>PersonnelTransactionStatuses</c>)
/// are canonical; this catalog backs i18n/UI. Seeded for SV.
/// </summary>
public sealed class PersonnelTransactionStatusCatalogItem : GeneralCatalogItem
{
    private PersonnelTransactionStatusCatalogItem()
    {
    }

    private PersonnelTransactionStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static PersonnelTransactionStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of recurring-deduction lifecycle STATUSES (general-catalogs key
/// <c>recurring-deduction-statuses</c>) backing the <c>statusCode</c> of a personnel-file recurring
/// deduction (REQ-008 · planilla descuentos cíclicos — the one-decision lifecycle EN_REVISION/VIGENTE/
/// RECHAZADO/SUSPENDIDO/FINALIZADO/ANULADO, D-14). Mirrors
/// <see cref="RecurringIncomeStatusCatalogItem"/>; seeded for SV.
/// </summary>
public sealed class RecurringDeductionStatusCatalogItem : GeneralCatalogItem
{
    private RecurringDeductionStatusCatalogItem()
    {
    }

    private RecurringDeductionStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static RecurringDeductionStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of recurring-deduction SETTLEMENT ACTIONS (general-catalogs key
/// <c>recurring-deduction-settlement-actions</c>) backing the <c>settlementActionCode</c> of a
/// personnel-file recurring deduction (REQ-008 — what happens to the outstanding credit when the employee
/// is settled: DESCONTAR_SALDO / CANCELAR (condonación), D-12). Mirrors
/// <see cref="RecurringIncomeSettlementActionCatalogItem"/>; seeded for SV.
/// </summary>
public sealed class RecurringDeductionSettlementActionCatalogItem : GeneralCatalogItem
{
    private RecurringDeductionSettlementActionCatalogItem()
    {
    }

    private RecurringDeductionSettlementActionCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static RecurringDeductionSettlementActionCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of recurring-deduction TYPES (general-catalogs key
/// <c>recurring-deduction-types</c>) backing the <c>recurringDeductionTypeCode</c> of a personnel-file
/// recurring deduction (REQ-008 — préstamo bancario, procuraduría, cooperativa, asociación…, P-10, an
/// editable template). Mirrors <see cref="RecurringIncomeTypeCatalogItem"/>; seeded for SV.
/// </summary>
public sealed class RecurringDeductionTypeCatalogItem : GeneralCatalogItem
{
    private RecurringDeductionTypeCatalogItem()
    {
    }

    private RecurringDeductionTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static RecurringDeductionTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}
