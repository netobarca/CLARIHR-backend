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
