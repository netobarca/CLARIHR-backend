using CLARIHR.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Companies;

internal sealed class CompanySubscriptionConfiguration : IEntityTypeConfiguration<CompanySubscription>
{
    public void Configure(EntityTypeBuilder<CompanySubscription> builder)
    {
        builder.ToTable("company_subscriptions");

        builder.HasKey(subscription => subscription.Id)
            .HasName("pk_company_subscriptions");

        builder.Property(subscription => subscription.Id)
            .HasColumnName("id");

        builder.Property(subscription => subscription.CompanyId)
            .HasColumnName("company_id");

        builder.Property(subscription => subscription.CommercialPlanId)
            .HasColumnName("commercial_plan_id");

        builder.Property(subscription => subscription.CommercialPlanVersionId)
            .HasColumnName("commercial_plan_version_id");

        builder.Property(subscription => subscription.PlanCode)
            .HasColumnName("plan_code")
            .HasMaxLength(40);

        builder.Property(subscription => subscription.PlanName)
            .HasColumnName("plan_name")
            .HasMaxLength(150);

        builder.Property(subscription => subscription.PlanVersionNumber)
            .HasColumnName("plan_version_number");

        builder.Property(subscription => subscription.BaseMonthlyFee)
            .HasColumnName("base_monthly_fee")
            .HasPrecision(18, 2);

        builder.Property(subscription => subscription.PricePerActiveEmployee)
            .HasColumnName("price_per_active_employee")
            .HasPrecision(18, 2);

        builder.Property(subscription => subscription.Periodicity)
            .HasColumnName("periodicity")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(subscription => subscription.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3);

        builder.Property(subscription => subscription.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(subscription => subscription.StartDateUtc)
            .HasColumnName("start_date_utc");

        builder.Property(subscription => subscription.EndDateUtc)
            .HasColumnName("end_date_utc");

        builder.Property(subscription => subscription.ActivatedByUserPublicId)
            .HasColumnName("activated_by_user_public_id");

        builder.Property(subscription => subscription.ActivatedAtUtc)
            .HasColumnName("activated_at_utc");

        builder.Property(subscription => subscription.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(subscription => subscription.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(subscription => new { subscription.CompanyId, subscription.Status })
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName("uq_company_subscriptions__company_active");

        builder.HasIndex(subscription => new { subscription.CompanyId, subscription.Status })
            .IsUnique()
            .HasFilter("status = 'Scheduled'")
            .HasDatabaseName("uq_company_subscriptions__company_scheduled");

        builder.HasIndex(subscription => subscription.CommercialPlanId)
            .HasDatabaseName("ix_company_subscriptions__commercial_plan_id");

        builder.HasIndex(subscription => subscription.CommercialPlanVersionId)
            .HasDatabaseName("ix_company_subscriptions__commercial_plan_version_id");

        builder.HasIndex(subscription => new { subscription.Status, subscription.StartDateUtc })
            .HasDatabaseName("ix_company_subscriptions__status_start_date");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(subscription => subscription.CompanyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_company_subscriptions__companies");

        builder.HasOne<CommercialPlan>()
            .WithMany()
            .HasForeignKey(subscription => subscription.CommercialPlanId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_subscriptions__commercial_plans");

        builder.HasOne<CommercialPlanVersion>()
            .WithMany()
            .HasForeignKey(subscription => subscription.CommercialPlanVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_subscriptions__commercial_plan_versions");
    }
}
