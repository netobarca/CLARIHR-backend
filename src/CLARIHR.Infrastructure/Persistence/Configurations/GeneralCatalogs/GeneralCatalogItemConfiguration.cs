using CLARIHR.Domain.GeneralCatalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.GeneralCatalogs;

internal abstract class GeneralCatalogItemConfigurationBase<TCatalogItem>(
    string tableName,
    string primaryKeyName,
    string publicIdIndexName,
    string countryCodeIndexName,
    string countryActiveSortIndexName,
    IEnumerable<object>? seedData = null)
    : IEntityTypeConfiguration<TCatalogItem>
    where TCatalogItem : GeneralCatalogItem
{
    public virtual void Configure(EntityTypeBuilder<TCatalogItem> builder)
    {
        builder.ToTable(tableName);

        builder.HasKey(item => item.Id)
            .HasName(primaryKeyName);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.CountryCatalogItemId).HasColumnName("country_catalog_item_id");
        builder.Property(item => item.CountryCode).HasColumnName("country_code").HasMaxLength(2);
        builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(item => item.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(80);
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.CountryCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.CountryCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName(publicIdIndexName);

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName(countryCodeIndexName);

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.IsActive, item.SortOrder })
            .HasDatabaseName(countryActiveSortIndexName);

        // Most general catalogs are seeded per-country at runtime (DevSeedService); the ones that must
        // exist in every environment opt in by passing static HasData here (see EmploymentStatusCatalogItem).
        if (seedData is not null)
        {
            builder.HasData(seedData);
        }
    }
}

internal sealed class LanguageCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<LanguageCatalogItem>
{
    public LanguageCatalogItemConfiguration()
        : base(
            "language_catalog_items",
            "pk_language_catalog_items",
            "uq_language_catalog_items__public_id",
            "uq_language_catalog_items__country_code",
            "ix_language_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetLanguageCatalogItems())
    {
    }
}

internal sealed class LanguageLevelCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<LanguageLevelCatalogItem>
{
    public LanguageLevelCatalogItemConfiguration()
        : base(
            "language_level_catalog_items",
            "pk_language_level_catalog_items",
            "uq_language_level_catalog_items__public_id",
            "uq_language_level_catalog_items__country_code",
            "ix_language_level_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetLanguageLevelCatalogItems())
    {
    }
}

internal sealed class TrainingTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<TrainingTypeCatalogItem>
{
    public TrainingTypeCatalogItemConfiguration()
        : base(
            "training_type_catalog_items",
            "pk_training_type_catalog_items",
            "uq_training_type_catalog_items__public_id",
            "uq_training_type_catalog_items__country_code",
            "ix_training_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetTrainingTypeCatalogItems())
    {
    }
}

internal sealed class AssignmentTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<AssignmentTypeCatalogItem>
{
    public AssignmentTypeCatalogItemConfiguration()
        : base(
            "assignment_type_catalog_items",
            "pk_assignment_type_catalog_items",
            "uq_assignment_type_catalog_items__public_id",
            "uq_assignment_type_catalog_items__country_code",
            "ix_assignment_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetAssignmentTypeCatalogItems())
    {
    }
}

internal sealed class PaymentMethodCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<PaymentMethodCatalogItem>
{
    public PaymentMethodCatalogItemConfiguration()
        : base(
            "payment_method_catalog_items",
            "pk_payment_method_catalog_items",
            "uq_payment_method_catalog_items__public_id",
            "uq_payment_method_catalog_items__country_code",
            "ix_payment_method_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetPaymentMethodCatalogItems())
    {
    }
}

internal sealed class SubstitutionTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<SubstitutionTypeCatalogItem>
{
    public SubstitutionTypeCatalogItemConfiguration()
        : base(
            "substitution_type_catalog_items",
            "pk_substitution_type_catalog_items",
            "uq_substitution_type_catalog_items__public_id",
            "uq_substitution_type_catalog_items__country_code",
            "ix_substitution_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetSubstitutionTypeCatalogItems())
    {
    }
}

internal sealed class MedicalClaimTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<MedicalClaimTypeCatalogItem>
{
    public MedicalClaimTypeCatalogItemConfiguration()
        : base(
            "medical_claim_type_catalog_items",
            "pk_medical_claim_type_catalog_items",
            "uq_medical_claim_type_catalog_items__public_id",
            "uq_medical_claim_type_catalog_items__country_code",
            "ix_medical_claim_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetMedicalClaimTypeCatalogItems())
    {
    }
}

internal sealed class MedicalClaimStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<MedicalClaimStatusCatalogItem>
{
    public MedicalClaimStatusCatalogItemConfiguration()
        : base(
            "medical_claim_status_catalog_items",
            "pk_medical_claim_status_catalog_items",
            "uq_medical_claim_status_catalog_items__public_id",
            "uq_medical_claim_status_catalog_items__country_code",
            "ix_medical_claim_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetMedicalClaimStatusCatalogItems())
    {
    }
}

internal sealed class AssetAccessTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<AssetAccessTypeCatalogItem>
{
    public AssetAccessTypeCatalogItemConfiguration()
        : base(
            "asset_access_type_catalog_items",
            "pk_asset_access_type_catalog_items",
            "uq_asset_access_type_catalog_items__public_id",
            "uq_asset_access_type_catalog_items__country_code",
            "ix_asset_access_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetAssetAccessTypeCatalogItems())
    {
    }
}

internal sealed class BankAccountTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<BankAccountTypeCatalogItem>
{
    public BankAccountTypeCatalogItemConfiguration()
        : base(
            "bank_account_type_catalog_items",
            "pk_bank_account_type_catalog_items",
            "uq_bank_account_type_catalog_items__public_id",
            "uq_bank_account_type_catalog_items__country_code",
            "ix_bank_account_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetBankAccountTypeCatalogItems())
    {
    }
}

internal sealed class OffPayrollTransactionTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<OffPayrollTransactionTypeCatalogItem>
{
    public OffPayrollTransactionTypeCatalogItemConfiguration()
        : base(
            "off_payroll_transaction_type_catalog_items",
            "pk_off_payroll_transaction_type_catalog_items",
            "uq_off_payroll_transaction_type_catalog_items__public_id",
            "uq_off_payroll_transaction_type_catalog_items__country_code",
            // Shortened (drops "country_") to stay within PostgreSQL's 63-char identifier limit.
            "ix_off_payroll_transaction_type_catalog_items__active_sort",
            GlobalCatalogSeedData.GetOffPayrollTransactionTypeCatalogItems())
    {
    }
}

internal sealed class DeliveryStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<DeliveryStatusCatalogItem>
{
    public DeliveryStatusCatalogItemConfiguration()
        : base(
            "delivery_status_catalog_items",
            "pk_delivery_status_catalog_items",
            "uq_delivery_status_catalog_items__public_id",
            "uq_delivery_status_catalog_items__country_code",
            "ix_delivery_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetDeliveryStatusCatalogItems())
    {
    }
}

internal sealed class EmploymentStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<EmploymentStatusCatalogItem>
{
    public EmploymentStatusCatalogItemConfiguration()
        : base(
            "employment_status_catalog_items",
            "pk_employment_status_catalog_items",
            "uq_employment_status_catalog_items__public_id",
            "uq_employment_status_catalog_items__country_code",
            "ix_employment_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetEmploymentStatusCatalogItems())
    {
    }
}

internal sealed class DurationUnitCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<DurationUnitCatalogItem>
{
    public DurationUnitCatalogItemConfiguration()
        : base(
            "duration_unit_catalog_items",
            "pk_duration_unit_catalog_items",
            "uq_duration_unit_catalog_items__public_id",
            "uq_duration_unit_catalog_items__country_code",
            "ix_duration_unit_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetDurationUnitCatalogItems())
    {
    }
}

internal sealed class ReferenceTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<ReferenceTypeCatalogItem>
{
    public ReferenceTypeCatalogItemConfiguration()
        : base(
            "reference_type_catalog_items",
            "pk_reference_type_catalog_items",
            "uq_reference_type_catalog_items__public_id",
            "uq_reference_type_catalog_items__country_code",
            "ix_reference_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetReferenceTypeCatalogItems())
    {
    }
}

internal sealed class CurrencyCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<CurrencyCatalogItem>
{
    public CurrencyCatalogItemConfiguration()
        : base(
            "currency_catalog_items",
            "pk_currency_catalog_items",
            "uq_currency_catalog_items__public_id",
            "uq_currency_catalog_items__country_code",
            "ix_currency_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetCurrencyCatalogItems())
    {
    }
}

internal sealed class PayPeriodCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<PayPeriodCatalogItem>
{
    public PayPeriodCatalogItemConfiguration()
        : base(
            "pay_period_catalog_items",
            "pk_pay_period_catalog_items",
            "uq_pay_period_catalog_items__public_id",
            "uq_pay_period_catalog_items__country_code",
            "ix_pay_period_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetPayPeriodCatalogItems())
    {
    }
}

internal sealed class CalculationBaseCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<CalculationBaseCatalogItem>
{
    public CalculationBaseCatalogItemConfiguration()
        : base(
            "calculation_base_catalog_items",
            "pk_calculation_base_catalog_items",
            "uq_calculation_base_catalog_items__public_id",
            "uq_calculation_base_catalog_items__country_code",
            "ix_calculation_base_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetCalculationBaseCatalogItems())
    {
    }
}

internal sealed class ExperienceMetricCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<ExperienceMetricCatalogItem>
{
    public ExperienceMetricCatalogItemConfiguration()
        : base(
            "experience_metric_catalog_items",
            "pk_experience_metric_catalog_items",
            "uq_experience_metric_catalog_items__public_id",
            "uq_experience_metric_catalog_items__country_code",
            "ix_experience_metric_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetExperienceMetricCatalogItems())
    {
    }
}

internal sealed class ContractTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<ContractTypeCatalogItem>
{
    public ContractTypeCatalogItemConfiguration()
        : base(
            "contract_type_catalog_items",
            "pk_contract_type_catalog_items",
            "uq_contract_type_catalog_items__public_id",
            "uq_contract_type_catalog_items__country_code",
            "ix_contract_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetContractTypeCatalogItems())
    {
    }

    // Enriched columns (RF-011): abbreviation + temporary flag, delivered by seed (DP-03).
    public override void Configure(EntityTypeBuilder<ContractTypeCatalogItem> builder)
    {
        base.Configure(builder);

        builder.Property(item => item.Abbreviation).HasColumnName("abbreviation").HasMaxLength(20);
        builder.Property(item => item.IsTemporary).HasColumnName("is_temporary");
    }
}

internal sealed class HobbyCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<HobbyCatalogItem>
{
    public HobbyCatalogItemConfiguration()
        : base(
            "hobby_catalog_items",
            "pk_hobby_catalog_items",
            "uq_hobby_catalog_items__public_id",
            "uq_hobby_catalog_items__country_code",
            "ix_hobby_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetHobbyCatalogItems())
    {
    }
}

internal sealed class AssociationCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<AssociationCatalogItem>
{
    public AssociationCatalogItemConfiguration()
        : base(
            "association_catalog_items",
            "pk_association_catalog_items",
            "uq_association_catalog_items__public_id",
            "uq_association_catalog_items__country_code",
            "ix_association_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetAssociationCatalogItems())
    {
    }
}

internal sealed class AdditionalBenefitTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<AdditionalBenefitTypeCatalogItem>
{
    public AdditionalBenefitTypeCatalogItemConfiguration()
        : base(
            "additional_benefit_type_catalog_items",
            "pk_additional_benefit_type_catalog_items",
            "uq_additional_benefit_type_catalog_items__public_id",
            "uq_additional_benefit_type_catalog_items__country_code",
            "ix_additional_benefit_type_catalog_items__active_sort",
            GlobalCatalogSeedData.GetAdditionalBenefitTypeCatalogItems())
    {
    }
}

internal sealed class ActionTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<ActionTypeCatalogItem>
{
    public ActionTypeCatalogItemConfiguration()
        : base(
            "action_type_catalog_items",
            "pk_action_type_catalog_items",
            "uq_action_type_catalog_items__public_id",
            "uq_action_type_catalog_items__country_code",
            "ix_action_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetActionTypeCatalogItems())
    {
    }
}

internal sealed class ActionStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<ActionStatusCatalogItem>
{
    public ActionStatusCatalogItemConfiguration()
        : base(
            "action_status_catalog_items",
            "pk_action_status_catalog_items",
            "uq_action_status_catalog_items__public_id",
            "uq_action_status_catalog_items__country_code",
            "ix_action_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetActionStatusCatalogItems())
    {
    }
}

internal sealed class EconomicAidTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<EconomicAidTypeCatalogItem>
{
    public EconomicAidTypeCatalogItemConfiguration()
        : base(
            "economic_aid_type_catalog_items",
            "pk_economic_aid_type_catalog_items",
            "uq_economic_aid_type_catalog_items__public_id",
            "uq_economic_aid_type_catalog_items__country_code",
            "ix_economic_aid_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetEconomicAidTypeCatalogItems())
    {
    }
}

internal sealed class EconomicAidStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<EconomicAidStatusCatalogItem>
{
    public EconomicAidStatusCatalogItemConfiguration()
        : base(
            "economic_aid_status_catalog_items",
            "pk_economic_aid_status_catalog_items",
            "uq_economic_aid_status_catalog_items__public_id",
            "uq_economic_aid_status_catalog_items__country_code",
            "ix_economic_aid_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetEconomicAidStatusCatalogItems())
    {
    }
}

internal sealed class CertificateTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<CertificateTypeCatalogItem>
{
    public CertificateTypeCatalogItemConfiguration()
        : base(
            "certificate_type_catalog_items",
            "pk_certificate_type_catalog_items",
            "uq_certificate_type_catalog_items__public_id",
            "uq_certificate_type_catalog_items__country_code",
            "ix_certificate_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetCertificateTypeCatalogItems())
    {
    }
}

internal sealed class CertificateRequestStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<CertificateRequestStatusCatalogItem>
{
    public CertificateRequestStatusCatalogItemConfiguration()
        : base(
            "certificate_request_status_catalog_items",
            "pk_certificate_request_status_catalog_items",
            "uq_certificate_request_status_catalog_items__public_id",
            "uq_certificate_request_status_catalog_items__country_code",
            // Shortened (drops "country_") to stay within PostgreSQL's 63-char identifier limit.
            "ix_certificate_request_status_catalog_items__active_sort",
            GlobalCatalogSeedData.GetCertificateRequestStatusCatalogItems())
    {
    }
}

internal sealed class RetirementRequestStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<RetirementRequestStatusCatalogItem>
{
    public RetirementRequestStatusCatalogItemConfiguration()
        : base(
            "retirement_request_status_catalog_items",
            "pk_retirement_request_status_catalog_items",
            "uq_retirement_request_status_catalog_items__public_id",
            "uq_retirement_request_status_catalog_items__country_code",
            // Shortened (drops "country_") to stay within PostgreSQL's 63-char identifier limit.
            "ix_retirement_request_status_catalog_items__active_sort",
            GlobalCatalogSeedData.GetRetirementRequestStatusCatalogItems())
    {
    }
}

internal sealed class SettlementStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<SettlementStatusCatalogItem>
{
    public SettlementStatusCatalogItemConfiguration()
        : base(
            "settlement_status_catalog_items",
            "pk_settlement_status_catalog_items",
            "uq_settlement_status_catalog_items__public_id",
            "uq_settlement_status_catalog_items__country_code",
            "ix_settlement_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetSettlementStatusCatalogItems())
    {
    }
}

internal sealed class CertificateDeliveryMethodCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<CertificateDeliveryMethodCatalogItem>
{
    public CertificateDeliveryMethodCatalogItemConfiguration()
        : base(
            "certificate_delivery_method_catalog_items",
            "pk_certificate_delivery_method_catalog_items",
            "uq_certificate_delivery_method_catalog_items__public_id",
            "uq_certificate_delivery_method_catalog_items__country_code",
            // Shortened (drops "country_") to stay within PostgreSQL's 63-char identifier limit.
            "ix_certificate_delivery_method_catalog_items__active_sort",
            GlobalCatalogSeedData.GetCertificateDeliveryMethodCatalogItems())
    {
    }
}

internal sealed class CertificatePurposeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<CertificatePurposeCatalogItem>
{
    public CertificatePurposeCatalogItemConfiguration()
        : base(
            "certificate_purpose_catalog_items",
            "pk_certificate_purpose_catalog_items",
            "uq_certificate_purpose_catalog_items__public_id",
            "uq_certificate_purpose_catalog_items__country_code",
            "ix_certificate_purpose_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetCertificatePurposeCatalogItems())
    {
    }
}

internal sealed class ClinicSectorCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<ClinicSectorCatalogItem>
{
    public ClinicSectorCatalogItemConfiguration()
        : base(
            "clinic_sector_catalog_items",
            "pk_clinic_sector_catalog_items",
            "uq_clinic_sector_catalog_items__public_id",
            "uq_clinic_sector_catalog_items__country_code",
            "ix_clinic_sector_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetClinicSectorCatalogItems())
    {
    }
}

internal sealed class IncapacityStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<IncapacityStatusCatalogItem>
{
    public IncapacityStatusCatalogItemConfiguration()
        : base(
            "incapacity_status_catalog_items",
            "pk_incapacity_status_catalog_items",
            "uq_incapacity_status_catalog_items__public_id",
            "uq_incapacity_status_catalog_items__country_code",
            "ix_incapacity_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetIncapacityStatusCatalogItems())
    {
    }
}

internal sealed class VacationRequestStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<VacationRequestStatusCatalogItem>
{
    public VacationRequestStatusCatalogItemConfiguration()
        : base(
            "vacation_request_status_catalog_items",
            "pk_vacation_request_status_catalog_items",
            "uq_vacation_request_status_catalog_items__public_id",
            "uq_vacation_request_status_catalog_items__country_code",
            "ix_vacation_request_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetVacationRequestStatusCatalogItems())
    {
    }
}

internal sealed class CompensatoryTimeStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<CompensatoryTimeStatusCatalogItem>
{
    public CompensatoryTimeStatusCatalogItemConfiguration()
        : base(
            "compensatory_time_status_catalog_items",
            "pk_compensatory_time_status_catalog_items",
            "uq_compensatory_time_status_catalog_items__public_id",
            "uq_compensatory_time_status_catalog_items__country_code",
            "ix_compensatory_time_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetCompensatoryTimeStatusCatalogItems())
    {
    }
}

internal sealed class CompensatoryTimeOperationCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<CompensatoryTimeOperationCatalogItem>
{
    public CompensatoryTimeOperationCatalogItemConfiguration()
        : base(
            "compensatory_time_operation_catalog_items",
            "pk_compensatory_time_operation_catalog_items",
            "uq_compensatory_time_operation_catalog_items__public_id",
            "uq_compensatory_time_operation_catalog_items__country_code",
            // Shortened to __active_sort: the full __country_active_sort would exceed the 63-char
            // PostgreSQL identifier limit (precedent: off_payroll_transaction_type_catalog_items).
            "ix_compensatory_time_operation_catalog_items__active_sort",
            GlobalCatalogSeedData.GetCompensatoryTimeOperationCatalogItems())
    {
    }
}

internal sealed class PersonnelTransactionStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<PersonnelTransactionStatusCatalogItem>
{
    public PersonnelTransactionStatusCatalogItemConfiguration()
        : base(
            "personnel_transaction_status_catalog_items",
            "pk_personnel_transaction_status_catalog_items",
            "uq_personnel_transaction_status_catalog_items__public_id",
            "uq_personnel_transaction_status_catalog_items__country_code",
            // Shortened to __active_sort: the full __country_active_sort would exceed the 63-char
            // PostgreSQL identifier limit (precedent: compensatory_time_operation_catalog_items).
            "ix_personnel_transaction_status_catalog_items__active_sort",
            GlobalCatalogSeedData.GetPersonnelTransactionStatusCatalogItems())
    {
    }
}

internal sealed class PayrollTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<PayrollTypeCatalogItem>
{
    public PayrollTypeCatalogItemConfiguration()
        : base(
            "payroll_type_catalog_items",
            "pk_payroll_type_catalog_items",
            "uq_payroll_type_catalog_items__public_id",
            "uq_payroll_type_catalog_items__country_code",
            "ix_payroll_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetPayrollTypeCatalogItems())
    {
    }
}

internal sealed class RecurringIncomeStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<RecurringIncomeStatusCatalogItem>
{
    public RecurringIncomeStatusCatalogItemConfiguration()
        : base(
            "recurring_income_status_catalog_items",
            "pk_recurring_income_status_catalog_items",
            "uq_recurring_income_status_catalog_items__public_id",
            "uq_recurring_income_status_catalog_items__country_code",
            "ix_recurring_income_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetRecurringIncomeStatusCatalogItems())
    {
    }
}

internal sealed class RecurringIncomeSettlementActionCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<RecurringIncomeSettlementActionCatalogItem>
{
    public RecurringIncomeSettlementActionCatalogItemConfiguration()
        // Table shortened to "settle_action": the full "recurring_income_settlement_action_catalog_items"
        // (48 chars) blows the 63-char PostgreSQL identifier limit on BOTH the __country_code (65) and the
        // __country_active_sort (72) index names. With the shortened table name the country_code index fits
        // (61) and the sort index still needs the __active_sort shortcut (precedent:
        // personnel_transaction_status_catalog_items / compensatory_time_operation_catalog_items).
        : base(
            "recurring_income_settle_action_catalog_items",
            "pk_recurring_income_settle_action_catalog_items",
            "uq_recurring_income_settle_action_catalog_items__public_id",
            "uq_recurring_income_settle_action_catalog_items__country_code",
            "ix_recurring_income_settle_action_catalog_items__active_sort",
            GlobalCatalogSeedData.GetRecurringIncomeSettlementActionCatalogItems())
    {
    }
}

internal sealed class RecurringIncomeTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<RecurringIncomeTypeCatalogItem>
{
    public RecurringIncomeTypeCatalogItemConfiguration()
        : base(
            "recurring_income_type_catalog_items",
            "pk_recurring_income_type_catalog_items",
            "uq_recurring_income_type_catalog_items__public_id",
            "uq_recurring_income_type_catalog_items__country_code",
            "ix_recurring_income_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetRecurringIncomeTypeCatalogItems())
    {
    }
}

internal sealed class OneTimeIncomeStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<OneTimeIncomeStatusCatalogItem>
{
    public OneTimeIncomeStatusCatalogItemConfiguration()
        : base(
            "one_time_income_status_catalog_items",
            "pk_one_time_income_status_catalog_items",
            "uq_one_time_income_status_catalog_items__public_id",
            "uq_one_time_income_status_catalog_items__country_code",
            "ix_one_time_income_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetOneTimeIncomeStatusCatalogItems())
    {
    }
}

internal sealed class OvertimeRecordStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<OvertimeRecordStatusCatalogItem>
{
    public OvertimeRecordStatusCatalogItemConfiguration()
        : base(
            "overtime_record_status_catalog_items",
            "pk_overtime_record_status_catalog_items",
            "uq_overtime_record_status_catalog_items__public_id",
            "uq_overtime_record_status_catalog_items__country_code",
            "ix_overtime_record_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetOvertimeRecordStatusCatalogItems())
    {
    }
}

internal sealed class OneTimeDeductionStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<OneTimeDeductionStatusCatalogItem>
{
    public OneTimeDeductionStatusCatalogItemConfiguration()
        : base(
            "one_time_deduction_status_catalog_items",
            "pk_one_time_deduction_status_catalog_items",
            "uq_one_time_deduction_status_catalog_items__public_id",
            "uq_one_time_deduction_status_catalog_items__country_code",
            // 63 chars exactly — the PostgreSQL identifier limit; one more character and it would be truncated.
            "ix_one_time_deduction_status_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetOneTimeDeductionStatusCatalogItems())
    {
    }
}

internal sealed class RecurringDeductionStatusCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<RecurringDeductionStatusCatalogItem>
{
    public RecurringDeductionStatusCatalogItemConfiguration()
        // "deduction" is 3 chars longer than "income": the __country_active_sort index name reaches 64 and
        // blows the 63-char PostgreSQL identifier limit, so it takes the __active_sort shortcut (precedent:
        // recurring_income_settle_action_catalog_items).
        : base(
            "recurring_deduction_status_catalog_items",
            "pk_recurring_deduction_status_catalog_items",
            "uq_recurring_deduction_status_catalog_items__public_id",
            "uq_recurring_deduction_status_catalog_items__country_code",
            "ix_recurring_deduction_status_catalog_items__active_sort",
            GlobalCatalogSeedData.GetRecurringDeductionStatusCatalogItems())
    {
    }
}

internal sealed class RecurringDeductionSettlementActionCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<RecurringDeductionSettlementActionCatalogItem>
{
    public RecurringDeductionSettlementActionCatalogItemConfiguration()
        // Doubly shortened ("deduction" -> "deduct" AND "settlement_action" -> "settle_action"): the mold's
        // single shortening still leaves uq__country_code at 64, one over the 63-char PostgreSQL limit.
        // With "deduct" the table is 44 chars and every explicit identifier fits (max 61).
        : base(
            "recurring_deduct_settle_action_catalog_items",
            "pk_recurring_deduct_settle_action_catalog_items",
            "uq_recurring_deduct_settle_action_catalog_items__public_id",
            "uq_recurring_deduct_settle_action_catalog_items__country_code",
            "ix_recurring_deduct_settle_action_catalog_items__active_sort",
            GlobalCatalogSeedData.GetRecurringDeductionSettlementActionCatalogItems())
    {
    }
}

internal sealed class RecurringDeductionTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<RecurringDeductionTypeCatalogItem>
{
    public RecurringDeductionTypeCatalogItemConfiguration()
        : base(
            "recurring_deduction_type_catalog_items",
            "pk_recurring_deduction_type_catalog_items",
            "uq_recurring_deduction_type_catalog_items__public_id",
            "uq_recurring_deduction_type_catalog_items__country_code",
            "ix_recurring_deduction_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetRecurringDeductionTypeCatalogItems())
    {
    }
}
