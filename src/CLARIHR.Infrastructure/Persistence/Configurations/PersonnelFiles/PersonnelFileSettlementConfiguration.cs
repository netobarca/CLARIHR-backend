using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileSettlementConfiguration : IEntityTypeConfiguration<PersonnelFileSettlement>
{
    public void Configure(EntityTypeBuilder<PersonnelFileSettlement> builder)
    {
        builder.ToTable("personnel_file_settlements");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_settlements");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.RetirementRequestId).HasColumnName("retirement_request_id");
        builder.Property(item => item.RetirementRequestPublicId).HasColumnName("retirement_request_public_id");
        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");
        builder.Property(item => item.PositionNameSnapshot).HasColumnName("position_name_snapshot").HasMaxLength(300);
        builder.Property(item => item.PlazaStartDate).HasColumnName("plaza_start_date");
        builder.Property(item => item.CostCenterPublicId).HasColumnName("cost_center_public_id");
        builder.Property(item => item.CostCenterNameSnapshot).HasColumnName("cost_center_name_snapshot").HasMaxLength(300);
        builder.Property(item => item.RetirementDate).HasColumnName("retirement_date");
        builder.Property(item => item.RetirementCategoryCode).HasColumnName("retirement_category_code").HasMaxLength(80);
        builder.Property(item => item.RetirementCategoryNameSnapshot).HasColumnName("retirement_category_name_snapshot").HasMaxLength(200);
        builder.Property(item => item.RetirementReasonCode).HasColumnName("retirement_reason_code").HasMaxLength(80);
        builder.Property(item => item.RetirementReasonNameSnapshot).HasColumnName("retirement_reason_name_snapshot").HasMaxLength(200);
        builder.Property(item => item.RequesterFilePublicId).HasColumnName("requester_file_public_id");
        builder.Property(item => item.RequesterNameSnapshot).HasColumnName("requester_name_snapshot").HasMaxLength(300);
        builder.Property(item => item.RequestDate).HasColumnName("request_date");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(item => item.StatusCode).HasColumnName("status_code").HasMaxLength(80);
        builder.Property(item => item.MinimumMonthlyWage).HasColumnName("minimum_monthly_wage").HasColumnType("numeric(18,2)");
        builder.Property(item => item.IndemnityCapMultiplier).HasColumnName("indemnity_cap_multiplier").HasColumnType("numeric(11,8)");
        builder.Property(item => item.ResignationCapMultiplier).HasColumnName("resignation_cap_multiplier").HasColumnType("numeric(11,8)");
        builder.Property(item => item.VacationDays).HasColumnName("vacation_days").HasColumnType("numeric(9,4)");
        builder.Property(item => item.VacationPremiumPercent).HasColumnName("vacation_premium_percent").HasColumnType("numeric(11,8)");
        builder.Property(item => item.AguinaldoDays).HasColumnName("aguinaldo_days").HasColumnType("numeric(9,4)");
        builder.Property(item => item.ResignationBenefitDays).HasColumnName("resignation_benefit_days").HasColumnType("numeric(9,4)");
        builder.Property(item => item.ResignationMinimumServiceYears).HasColumnName("resignation_minimum_service_years");
        builder.Property(item => item.AguinaldoExemptionMultiplier).HasColumnName("aguinaldo_exemption_multiplier").HasColumnType("numeric(11,8)");
        builder.Property(item => item.MonthDivisorDays).HasColumnName("month_divisor_days");
        builder.Property(item => item.YearDivisorDays).HasColumnName("year_divisor_days");
        builder.Property(item => item.MonthlyBaseSalary).HasColumnName("monthly_base_salary").HasColumnType("numeric(18,2)");
        builder.Property(item => item.SeniorityYears).HasColumnName("seniority_years");
        builder.Property(item => item.SeniorityDays).HasColumnName("seniority_days");
        builder.Property(item => item.CappedMonthlySalaryIndemnity).HasColumnName("capped_monthly_salary_indemnity").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CappedMonthlySalaryResignation).HasColumnName("capped_monthly_salary_resignation").HasColumnType("numeric(18,2)");
        builder.Property(item => item.TotalIncomes).HasColumnName("total_incomes").HasColumnType("numeric(18,2)");
        builder.Property(item => item.TotalDeductions).HasColumnName("total_deductions").HasColumnType("numeric(18,2)");
        builder.Property(item => item.NetPay).HasColumnName("net_pay").HasColumnType("numeric(18,2)");
        builder.Property(item => item.TotalEmployerCharges).HasColumnName("total_employer_charges").HasColumnType("numeric(18,2)");
        builder.Property(item => item.ProvisionTotal).HasColumnName("provision_total").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
        builder.Property(item => item.IssuedByUserId).HasColumnName("issued_by_user_id");
        builder.Property(item => item.IssuedAtUtc).HasColumnName("issued_at_utc");
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledAtUtc).HasColumnName("annulled_at_utc");
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason").HasMaxLength(2000);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_settlements__personnel_file");

        builder.HasMany(item => item.Lines)
            .WithOne(line => line.Settlement)
            .HasForeignKey(line => line.SettlementId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_settlement_lines__settlement");

        builder.Navigation(item => item.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_settlements__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.Kind })
            .HasDatabaseName("ix_personnel_file_settlements__tenant_file_kind");
        builder.HasIndex(item => new { item.TenantId, item.Kind, item.StatusCode, item.RequestDate })
            .HasDatabaseName("ix_personnel_file_settlements__tenant_kind_status_date");

        // DB backup of D-16 (at most ONE live real settlement per retirement × plaza): filtered unique
        // index — the handler check remains the primary guard, this closes the concurrency race
        // (precedent: uq_personnel_file_retirement_requests__tenant_file_open).
        builder.HasIndex(item => new { item.TenantId, item.RetirementRequestId, item.AssignedPositionPublicId })
            .IsUnique()
            .HasFilter("kind = 'Liquidacion' and status_code <> 'ANULADA' and is_active")
            .HasDatabaseName("uq_personnel_file_settlements__tenant_retirement_position");
    }
}

internal sealed class PersonnelFileSettlementLineConfiguration : IEntityTypeConfiguration<PersonnelFileSettlementLine>
{
    public void Configure(EntityTypeBuilder<PersonnelFileSettlementLine> builder)
    {
        builder.ToTable("personnel_file_settlement_lines");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_settlement_lines");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.SettlementId).HasColumnName("settlement_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.ConceptClass).HasColumnName("concept_class").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ConceptCode).HasColumnName("concept_code").HasMaxLength(80);
        builder.Property(item => item.ConceptNameSnapshot).HasColumnName("concept_name_snapshot").HasMaxLength(200);
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(300);
        builder.Property(item => item.IsSystemCalculated).HasColumnName("is_system_calculated");
        builder.Property(item => item.CalculationBase).HasColumnName("calculation_base").HasColumnType("numeric(18,2)");
        builder.Property(item => item.UnitsOrDays).HasColumnName("units_or_days").HasColumnType("numeric(12,4)");
        builder.Property(item => item.CalculatedAmount).HasColumnName("calculated_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.ExemptAmount).HasColumnName("exempt_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.TaxableExcessAmount).HasColumnName("taxable_excess_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.OverrideAmount).HasColumnName("override_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.OverrideReason).HasColumnName("override_reason").HasMaxLength(500);
        builder.Property(item => item.FinalAmount).HasColumnName("final_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.IsIncluded).HasColumnName("is_included");
        builder.Property(item => item.IsZeroByLaw).HasColumnName("is_zero_by_law");
        builder.Property(item => item.ZeroReasonCode).HasColumnName("zero_reason_code").HasMaxLength(80);
        builder.Property(item => item.CalculationDetail).HasColumnName("calculation_detail").HasMaxLength(500);
        builder.Property(item => item.CounterpartyName).HasColumnName("counterparty_name").HasMaxLength(200);
        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_settlement_lines__public_id");
        builder.HasIndex(item => new { item.TenantId, item.SettlementId, item.SortOrder })
            .HasDatabaseName("ix_personnel_file_settlement_lines__tenant_settlement_sort");
    }
}
