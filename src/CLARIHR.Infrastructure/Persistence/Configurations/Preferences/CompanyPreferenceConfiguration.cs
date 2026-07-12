using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Preferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Preferences;

internal sealed class CompanyPreferenceConfiguration : IEntityTypeConfiguration<CompanyPreference>
{
    public void Configure(EntityTypeBuilder<CompanyPreference> builder)
    {
        builder.ToTable("company_preferences");

        builder.HasKey(preference => preference.Id)
            .HasName("pk_company_preferences");

        builder.Property(preference => preference.Id)
            .HasColumnName("id");

        builder.Property(preference => preference.PublicId)
            .HasColumnName("public_id");

        builder.Property(preference => preference.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(preference => preference.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3);

        builder.Property(preference => preference.TimeZone)
            .HasColumnName("time_zone")
            .HasMaxLength(100);

        builder.Property(preference => preference.HrFunctionalAreaCode)
            .HasColumnName("hr_functional_area_code")
            .HasMaxLength(80);

        builder.Property(preference => preference.FileUpToDateThresholdMonths)
            .HasColumnName("file_up_to_date_threshold_months");

        builder.Property(preference => preference.MinimumSeniorityMonthsForEconomicAid)
            .HasColumnName("minimum_seniority_months_for_economic_aid");

        // Vacation & incapacity parametrization (D-20/D-24/D-26/D-27); all nullable (null = legal default).
        builder.Property(preference => preference.AnnualVacationDaysDefault)
            .HasColumnName("annual_vacation_days_default");

        builder.Property(preference => preference.AdditionalVacationBenefitDaysDefault)
            .HasColumnName("additional_vacation_benefit_days_default");

        builder.Property(preference => preference.AllowVacationStartOnHoliday)
            .HasColumnName("allow_vacation_start_on_holiday");

        builder.Property(preference => preference.AllowVacationEndOnHoliday)
            .HasColumnName("allow_vacation_end_on_holiday");

        builder.Property(preference => preference.AllowVacationStartOnRestDay)
            .HasColumnName("allow_vacation_start_on_rest_day");

        builder.Property(preference => preference.DefaultUseAnniversary)
            .HasColumnName("default_use_anniversary");

        builder.Property(preference => preference.CompanyRestDayOfWeek)
            .HasColumnName("company_rest_day_of_week");

        builder.Property(preference => preference.EmployerCoveredIncapacityDaysPerYear)
            .HasColumnName("employer_covered_incapacity_days_per_year");

        builder.Property(preference => preference.AdditionalIncapacityBenefitDaysPerYear)
            .HasColumnName("additional_incapacity_benefit_days_per_year");

        builder.Property(preference => preference.IncapacityRequiresDocument)
            .HasColumnName("incapacity_requires_document");

        // Compensatory-time parametrization (REQ-002 P-10/P-11/P-15); all nullable (null = default).
        builder.Property(preference => preference.CompensatoryTimeStandardDailyHours)
            .HasColumnName("compensatory_time_standard_daily_hours")
            .HasColumnType("numeric(4,2)");

        builder.Property(preference => preference.CompensatoryTimeMaxBalanceHours)
            .HasColumnName("compensatory_time_max_balance_hours")
            .HasColumnType("numeric(6,2)");

        builder.Property(preference => preference.CompensatoryTimeCreditRequiresDocument)
            .HasColumnName("compensatory_time_credit_requires_document");

        builder.Property(preference => preference.CompensatoryTimeSettlementRateFactor)
            .HasColumnName("compensatory_time_settlement_rate_factor")
            .HasColumnType("numeric(5,2)");

        // Overtime parametrization (REQ-007 P-01/P-05); both nullable (null = self-service off / no cap).
        builder.Property(preference => preference.OvertimeSelfServiceEnabled)
            .HasColumnName("overtime_self_service_enabled");

        builder.Property(preference => preference.OvertimeMaxDailyMinutes)
            .HasColumnName("overtime_max_daily_minutes");

        // Recurring-deduction parametrization (REQ-008 P-03); nullable (null = no default rate on the form).
        builder.Property(preference => preference.RecurringDeductionDefaultInterestRatePercent)
            .HasColumnName("recurring_deduction_default_interest_rate_percent")
            .HasColumnType("numeric(9,4)");

        // Indebtedness ceiling (REQ-010 D-16); nullable (null = the company has NO indebtedness control).
        builder.Property(preference => preference.MaxIndebtednessPercent)
            .HasColumnName("max_indebtedness_percent")
            .HasColumnType("numeric(9,4)");

        builder.Property(preference => preference.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(preference => preference.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(preference => preference.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(preference => preference.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_company_preferences__public_id");

        builder.HasIndex(preference => preference.TenantId)
            .IsUnique()
            .HasDatabaseName("uq_company_preferences__tenant_id");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(preference => preference.TenantId)
            .HasPrincipalKey(company => company.PublicId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_preferences__companies");
    }
}
