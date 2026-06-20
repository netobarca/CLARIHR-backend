using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileEmployeeProfileConfiguration : IEntityTypeConfiguration<PersonnelFileEmployeeProfile>
{
    public void Configure(EntityTypeBuilder<PersonnelFileEmployeeProfile> builder)
    {
        builder.ToTable("personnel_file_employee_profiles", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_employee_profiles__contract_dates",
                "contract_end_date is null or contract_start_date is null or contract_end_date >= contract_start_date"));

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_employee_profiles");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.EmployeeCode).HasColumnName("employee_code").HasMaxLength(80);
        builder.Property(item => item.NormalizedEmployeeCode).HasColumnName("normalized_employee_code").HasMaxLength(80);
        builder.Property(item => item.EmploymentStatusCode).HasColumnName("employment_status_code").HasMaxLength(80);
        builder.Property(item => item.IsEmploymentActive).HasColumnName("is_employment_active");
        builder.Property(item => item.ContractTypeCode).HasColumnName("contract_type_code").HasMaxLength(80);
        builder.Property(item => item.HireDate).HasColumnName("hire_date");
        builder.Property(item => item.RetirementCategoryCode).HasColumnName("retirement_category_code").HasMaxLength(80);
        builder.Property(item => item.RetirementReasonCode).HasColumnName("retirement_reason_code").HasMaxLength(80);
        builder.Property(item => item.RetirementNotes).HasColumnName("retirement_notes").HasMaxLength(2000);
        builder.Property(item => item.RetirementDate).HasColumnName("retirement_date");
        builder.Property(item => item.WorkdayCode).HasColumnName("workday_code").HasMaxLength(80);
        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code").HasMaxLength(80);
        builder.Property(item => item.OrgUnitPublicId).HasColumnName("org_unit_public_id");
        builder.Property(item => item.WorkCenterPublicId).HasColumnName("work_center_public_id");
        builder.Property(item => item.CostCenterPublicId).HasColumnName("cost_center_public_id");
        builder.Property(item => item.ContractStartDate).HasColumnName("contract_start_date");
        builder.Property(item => item.ContractEndDate).HasColumnName("contract_end_date");
        builder.Property(item => item.VacationConfigurationJson).HasColumnName("vacation_configuration_json").HasColumnType("jsonb");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_employee_profiles__personnel_file");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_employee_profiles__public_id");

        builder.HasIndex(item => new { item.TenantId, item.NormalizedEmployeeCode })
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_employee_profiles__tenant_employee_code");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_employee_profiles__tenant_file");
    }
}

internal sealed class PersonnelFileEmploymentAssignmentConfiguration : IEntityTypeConfiguration<PersonnelFileEmploymentAssignment>
{
    public void Configure(EntityTypeBuilder<PersonnelFileEmploymentAssignment> builder)
    {
        builder.ToTable("personnel_file_employment_assignments", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_employment_assignments__dates",
                "end_date is null or end_date >= start_date"));

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_employment_assignments");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.AssignmentTypeCode).HasColumnName("assignment_type_code").HasMaxLength(80);
        builder.Property(item => item.PositionSlotPublicId).HasColumnName("position_slot_public_id");
        builder.Property(item => item.OrgUnitPublicId).HasColumnName("org_unit_public_id");
        builder.Property(item => item.WorkCenterPublicId).HasColumnName("work_center_public_id");
        builder.Property(item => item.CostCenterPublicId).HasColumnName("cost_center_public_id");
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.IsPrimary).HasColumnName("is_primary");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_employment_assignments__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_employment_assignments__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StartDate }).HasDatabaseName("ix_personnel_file_employment_assignments__tenant_file_start");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.IsActive, item.IsPrimary }).HasDatabaseName("ix_personnel_file_employment_assignments__tenant_file_active_primary");
    }
}

internal sealed class PersonnelFileContractHistoryConfiguration : IEntityTypeConfiguration<PersonnelFileContractHistory>
{
    public void Configure(EntityTypeBuilder<PersonnelFileContractHistory> builder)
    {
        builder.ToTable("personnel_file_contract_histories", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_contract_histories__dates",
                "contract_end_date is null or contract_end_date >= contract_date"));

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_contract_histories");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.ContractTypeCode).HasColumnName("contract_type_code").HasMaxLength(80);
        builder.Property(item => item.ContractDate).HasColumnName("contract_date");
        builder.Property(item => item.ContractEndDate).HasColumnName("contract_end_date");
        builder.Property(item => item.PositionSlotPublicId).HasColumnName("position_slot_public_id");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_contract_histories__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_contract_histories__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.ContractDate }).HasDatabaseName("ix_personnel_file_contract_histories__tenant_file_contract_date");
    }
}

internal sealed class PersonnelFileSalaryItemConfiguration : IEntityTypeConfiguration<PersonnelFileSalaryItem>
{
    public void Configure(EntityTypeBuilder<PersonnelFileSalaryItem> builder)
    {
        builder.ToTable("personnel_file_salary_items", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_salary_items__amount_non_negative",
                "amount >= 0"));

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_salary_items");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.IncomeTypeCode).HasColumnName("income_type_code").HasMaxLength(80);
        builder.Property(item => item.SalaryRubricCode).HasColumnName("salary_rubric_code").HasMaxLength(80);
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code").HasMaxLength(40);
        builder.Property(item => item.PayPeriodCode).HasColumnName("pay_period_code").HasMaxLength(40);
        builder.Property(item => item.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_salary_items__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_salary_items__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StartDate, item.IsActive }).HasDatabaseName("ix_personnel_file_salary_items__tenant_file_start_active");
    }
}

internal sealed class PersonnelFileAdditionalBenefitConfiguration : IEntityTypeConfiguration<PersonnelFileAdditionalBenefit>
{
    public void Configure(EntityTypeBuilder<PersonnelFileAdditionalBenefit> builder)
    {
        builder.ToTable("personnel_file_additional_benefits");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_additional_benefits");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.BenefitTypeCode).HasColumnName("benefit_type_code").HasMaxLength(80);
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_additional_benefits__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_additional_benefits__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.BenefitTypeCode, item.IsActive })
            .HasDatabaseName("ix_personnel_file_additional_benefits__tenant_file_type_active");
    }
}

internal sealed class PersonnelFilePaymentMethodConfiguration : IEntityTypeConfiguration<PersonnelFilePaymentMethod>
{
    public void Configure(EntityTypeBuilder<PersonnelFilePaymentMethod> builder)
    {
        builder.ToTable("personnel_file_payment_methods", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_payment_methods__effective_dates",
                "effective_to_utc is null or effective_to_utc >= effective_from_utc"));

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_payment_methods");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.PaymentMethodCode).HasColumnName("payment_method_code").HasMaxLength(80);
        builder.Property(item => item.BankAccountPublicId).HasColumnName("bank_account_public_id");
        builder.Property(item => item.IsPrimary).HasColumnName("is_primary");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.EffectiveFromUtc).HasColumnName("effective_from_utc");
        builder.Property(item => item.EffectiveToUtc).HasColumnName("effective_to_utc");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_payment_methods__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_payment_methods__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.IsActive, item.IsPrimary })
            .HasDatabaseName("ix_personnel_file_payment_methods__tenant_file_active_primary");
    }
}

internal sealed class PersonnelFileAuthorizationSubstitutionConfiguration : IEntityTypeConfiguration<PersonnelFileAuthorizationSubstitution>
{
    public void Configure(EntityTypeBuilder<PersonnelFileAuthorizationSubstitution> builder)
    {
        builder.ToTable("personnel_file_authorization_substitutions", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_authorization_substitutions__dates",
                "end_date is null or end_date >= start_date"));

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_authorization_substitutions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.SubstitutionTypeCode).HasColumnName("substitution_type_code").HasMaxLength(80);
        builder.Property(item => item.SubstitutePersonnelFilePublicId).HasColumnName("substitute_personnel_file_public_id");
        builder.Property(item => item.SubstitutePositionTitle).HasColumnName("substitute_position_title").HasMaxLength(120);
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_authorization_substitutions__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_authorization_substitutions__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.SubstitutionTypeCode, item.IsActive })
            .HasDatabaseName("ix_personnel_file_authorization_substitutions__tenant_file_type_active");
    }
}

internal sealed class PersonnelFilePersonnelActionConfiguration : IEntityTypeConfiguration<PersonnelFilePersonnelAction>
{
    public void Configure(EntityTypeBuilder<PersonnelFilePersonnelAction> builder)
    {
        builder.ToTable("personnel_file_personnel_actions");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_personnel_actions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.ActionTypeCode).HasColumnName("action_type_code").HasMaxLength(80);
        builder.Property(item => item.ActionStatusCode).HasColumnName("action_status_code").HasMaxLength(80);
        builder.Property(item => item.ActionDateUtc).HasColumnName("action_date_utc");
        builder.Property(item => item.EffectiveFromUtc).HasColumnName("effective_from_utc");
        builder.Property(item => item.EffectiveToUtc).HasColumnName("effective_to_utc");
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(item => item.Reference).HasColumnName("reference").HasMaxLength(120);
        builder.Property(item => item.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code").HasMaxLength(40);
        builder.Property(item => item.IsSystemGenerated).HasColumnName("is_system_generated");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_personnel_actions__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_personnel_actions__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.ActionDateUtc })
            .HasDatabaseName("ix_personnel_file_personnel_actions__tenant_file_action_date");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.ActionTypeCode, item.ActionStatusCode })
            .HasDatabaseName("ix_personnel_file_personnel_actions__tenant_file_type_status");
    }
}

internal sealed class PersonnelFilePayrollTransactionConfiguration : IEntityTypeConfiguration<PersonnelFilePayrollTransaction>
{
    public void Configure(EntityTypeBuilder<PersonnelFilePayrollTransaction> builder)
    {
        builder.ToTable("personnel_file_payroll_transactions");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_payroll_transactions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TransactionTypeCode).HasColumnName("transaction_type_code").HasMaxLength(80);
        builder.Property(item => item.TransactionDateUtc).HasColumnName("transaction_date_utc");
        builder.Property(item => item.PayrollPeriodCode).HasColumnName("payroll_period_code").HasMaxLength(80);
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(item => item.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code").HasMaxLength(40);
        builder.Property(item => item.IsDebit).HasColumnName("is_debit");
        builder.Property(item => item.SourceSystem).HasColumnName("source_system").HasMaxLength(80);
        builder.Property(item => item.SourceReference).HasColumnName("source_reference").HasMaxLength(120);
        builder.Property(item => item.SourceSyncedUtc).HasColumnName("source_synced_utc");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_payroll_transactions__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_payroll_transactions__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.TransactionDateUtc })
            .HasDatabaseName("ix_personnel_file_payroll_transactions__tenant_file_transaction_date");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.TransactionTypeCode })
            .HasDatabaseName("ix_personnel_file_payroll_transactions__tenant_file_type");
    }
}

internal sealed class PersonnelFileAssetAccessConfiguration : IEntityTypeConfiguration<PersonnelFileAssetAccess>
{
    public void Configure(EntityTypeBuilder<PersonnelFileAssetAccess> builder)
    {
        builder.ToTable("personnel_file_assets_accesses");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_assets_accesses");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.AssetTypeCode).HasColumnName("asset_type_code").HasMaxLength(80);
        builder.Property(item => item.AssetOrAccessName).HasColumnName("asset_or_access_name").HasMaxLength(200);
        builder.Property(item => item.AccessLevelCode).HasColumnName("access_level_code").HasMaxLength(80);
        builder.Property(item => item.StartDateUtc).HasColumnName("start_date_utc");
        builder.Property(item => item.EndDateUtc).HasColumnName("end_date_utc");
        builder.Property(item => item.DeliveryDateUtc).HasColumnName("delivery_date_utc");
        builder.Property(item => item.DeliveryStatusCode).HasColumnName("delivery_status_code").HasMaxLength(80);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_assets_accesses__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_assets_accesses__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StartDateUtc, item.IsActive })
            .HasDatabaseName("ix_personnel_file_assets_accesses__tenant_file_start_active");
    }
}

internal sealed class PersonnelFileInsuranceConfiguration : IEntityTypeConfiguration<PersonnelFileInsurance>
{
    public void Configure(EntityTypeBuilder<PersonnelFileInsurance> builder)
    {
        builder.ToTable("personnel_file_insurances");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_insurances");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.InsuranceCode).HasColumnName("insurance_code").HasMaxLength(80);
        builder.Property(item => item.EmployeeContribution).HasColumnName("employee_contribution").HasColumnType("numeric(18,2)");
        builder.Property(item => item.EmployerContribution).HasColumnName("employer_contribution").HasColumnType("numeric(18,2)");
        builder.Property(item => item.RangeCode).HasColumnName("range_code").HasMaxLength(80);
        builder.Property(item => item.PolicyNumber).HasColumnName("policy_number").HasMaxLength(120);
        builder.Property(item => item.InsuredAmount).HasColumnName("insured_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code").HasMaxLength(40);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.StartDateUtc).HasColumnName("start_date_utc");
        builder.Property(item => item.EndDateUtc).HasColumnName("end_date_utc");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_insurances__personnel_file");

        builder.HasMany(item => item.Beneficiaries)
            .WithOne(item => item.Insurance)
            .HasForeignKey(item => item.InsuranceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_insurance_beneficiaries__insurance");

        builder.Navigation(item => item.Beneficiaries).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_insurances__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.IsActive, item.InsuranceCode })
            .HasDatabaseName("ix_personnel_file_insurances__tenant_file_active_code");
    }
}

internal sealed class PersonnelFileInsuranceBeneficiaryConfiguration : IEntityTypeConfiguration<PersonnelFileInsuranceBeneficiary>
{
    public void Configure(EntityTypeBuilder<PersonnelFileInsuranceBeneficiary> builder)
    {
        builder.ToTable("personnel_file_insurance_beneficiaries");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_insurance_beneficiaries");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.InsuranceId).HasColumnName("insurance_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.FullName).HasColumnName("full_name").HasMaxLength(200);
        builder.Property(item => item.DocumentNumber).HasColumnName("document_number").HasMaxLength(80);
        builder.Property(item => item.BirthDate).HasColumnName("birth_date");
        builder.Property(item => item.KinshipCode).HasColumnName("kinship_code").HasMaxLength(80);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_insurance_beneficiaries__public_id");
        builder.HasIndex(item => new { item.TenantId, item.InsuranceId, item.IsActive })
            .HasDatabaseName("ix_personnel_file_insurance_beneficiaries__tenant_insurance_active");
    }
}

internal sealed class PersonnelFileMedicalClaimConfiguration : IEntityTypeConfiguration<PersonnelFileMedicalClaim>
{
    public void Configure(EntityTypeBuilder<PersonnelFileMedicalClaim> builder)
    {
        builder.ToTable("personnel_file_medical_claims");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_medical_claims");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.InsurancePublicId).HasColumnName("insurance_public_id");
        builder.Property(item => item.AccountNumber).HasColumnName("account_number").HasMaxLength(120);
        builder.Property(item => item.ClaimTypeCode).HasColumnName("claim_type_code").HasMaxLength(80);
        builder.Property(item => item.Diagnosis).HasColumnName("diagnosis").HasMaxLength(1000);
        builder.Property(item => item.ClaimAmount).HasColumnName("claim_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code").HasMaxLength(40);
        builder.Property(item => item.PaidAmount).HasColumnName("paid_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.ResponseTimeDays).HasColumnName("response_time_days");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.ClaimDateUtc).HasColumnName("claim_date_utc");
        builder.Property(item => item.SourceSystem).HasColumnName("source_system").HasMaxLength(80);
        builder.Property(item => item.SourceReference).HasColumnName("source_reference").HasMaxLength(120);
        builder.Property(item => item.SourceSyncedUtc).HasColumnName("source_synced_utc");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_medical_claims__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_medical_claims__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.ClaimDateUtc, item.ClaimTypeCode })
            .HasDatabaseName("ix_personnel_file_medical_claims__tenant_file_date_type");
    }
}

internal sealed class PersonnelFilePerformanceEvaluationConfiguration : IEntityTypeConfiguration<PersonnelFilePerformanceEvaluation>
{
    public void Configure(EntityTypeBuilder<PersonnelFilePerformanceEvaluation> builder)
    {
        builder.ToTable("personnel_file_performance_evaluations");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_performance_evaluations");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.EvaluatorName).HasColumnName("evaluator_name").HasMaxLength(200);
        builder.Property(item => item.EvaluationDateUtc).HasColumnName("evaluation_date_utc");
        builder.Property(item => item.Score).HasColumnName("score").HasColumnType("numeric(18,2)");
        builder.Property(item => item.QualitativeScoreCode).HasColumnName("qualitative_score_code").HasMaxLength(80);
        builder.Property(item => item.Comment).HasColumnName("comment").HasMaxLength(2000);
        builder.Property(item => item.SourceSystem).HasColumnName("source_system").HasMaxLength(80);
        builder.Property(item => item.SourceReference).HasColumnName("source_reference").HasMaxLength(120);
        builder.Property(item => item.SourceSyncedUtc).HasColumnName("source_synced_utc");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_performance_evaluations__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_performance_evaluations__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.EvaluationDateUtc })
            .HasDatabaseName("ix_personnel_file_performance_evaluations__tenant_file_date");
    }
}

internal sealed class PersonnelFilePositionCompetencyResultConfiguration : IEntityTypeConfiguration<PersonnelFilePositionCompetencyResult>
{
    public void Configure(EntityTypeBuilder<PersonnelFilePositionCompetencyResult> builder)
    {
        builder.ToTable("personnel_file_position_competency_results");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_position_competency_results");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.CompetencyCode).HasColumnName("competency_code").HasMaxLength(80);
        builder.Property(item => item.DesiredBehaviors).HasColumnName("desired_behaviors").HasMaxLength(2000);
        builder.Property(item => item.ExpectedScore).HasColumnName("expected_score").HasColumnType("numeric(18,2)");
        builder.Property(item => item.AchievedScore).HasColumnName("achieved_score").HasColumnType("numeric(18,2)");
        builder.Property(item => item.GapScore).HasColumnName("gap_score").HasColumnType("numeric(18,2)");
        builder.Property(item => item.EvaluationDateUtc).HasColumnName("evaluation_date_utc");
        builder.Property(item => item.SourceSystem).HasColumnName("source_system").HasMaxLength(80);
        builder.Property(item => item.SourceReference).HasColumnName("source_reference").HasMaxLength(120);
        builder.Property(item => item.SourceSyncedUtc).HasColumnName("source_synced_utc");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_position_competency_results__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_position_competency_results__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.CompetencyCode })
            .HasDatabaseName("ix_personnel_file_position_competency_results__tenant_file_competency");
    }
}

internal sealed class PersonnelFileSelectionContestConfiguration : IEntityTypeConfiguration<PersonnelFileSelectionContest>
{
    public void Configure(EntityTypeBuilder<PersonnelFileSelectionContest> builder)
    {
        builder.ToTable("personnel_file_selection_contests");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_selection_contests");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.ContestCode).HasColumnName("contest_code").HasMaxLength(80);
        builder.Property(item => item.ContestName).HasColumnName("contest_name").HasMaxLength(200);
        builder.Property(item => item.ContestDateUtc).HasColumnName("contest_date_utc");
        builder.Property(item => item.ResultCode).HasColumnName("result_code").HasMaxLength(80);
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.SourceSystem).HasColumnName("source_system").HasMaxLength(80);
        builder.Property(item => item.SourceReference).HasColumnName("source_reference").HasMaxLength(120);
        builder.Property(item => item.SourceSyncedUtc).HasColumnName("source_synced_utc");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_selection_contests__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_selection_contests__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.ContestDateUtc })
            .HasDatabaseName("ix_personnel_file_selection_contests__tenant_file_date");
    }
}

internal sealed class PersonnelFileCurricularCompetencyConfiguration : IEntityTypeConfiguration<PersonnelFileCurricularCompetency>
{
    public void Configure(EntityTypeBuilder<PersonnelFileCurricularCompetency> builder)
    {
        builder.ToTable("personnel_file_curricular_competencies");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_curricular_competencies");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.RequirementTypeCode).HasColumnName("requirement_type_code").HasMaxLength(80);
        builder.Property(item => item.RequirementName).HasColumnName("requirement_name").HasMaxLength(200);
        builder.Property(item => item.CompetencyDomain).HasColumnName("competency_domain").HasMaxLength(120);
        builder.Property(item => item.ExperienceTimeValue).HasColumnName("experience_time_value").HasColumnType("numeric(18,2)");
        builder.Property(item => item.MetricCode).HasColumnName("metric_code").HasMaxLength(80);
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.SourceSystem).HasColumnName("source_system").HasMaxLength(80);
        builder.Property(item => item.SourceReference).HasColumnName("source_reference").HasMaxLength(120);
        builder.Property(item => item.SourceSyncedUtc).HasColumnName("source_synced_utc");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_curricular_competencies__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_personnel_file_curricular_competencies__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.RequirementTypeCode })
            .HasDatabaseName("ix_personnel_file_curricular_competencies__tenant_file_requirement_type");
    }
}
