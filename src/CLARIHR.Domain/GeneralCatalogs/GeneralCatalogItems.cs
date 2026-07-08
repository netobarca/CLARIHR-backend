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
