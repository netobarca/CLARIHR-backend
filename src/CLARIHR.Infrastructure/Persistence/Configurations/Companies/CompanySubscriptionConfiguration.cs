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

        builder.Property(subscription => subscription.ExpiresAtUtc)
            .HasColumnName("expires_at_utc");

        builder.Property(subscription => subscription.EndDateUtc)
            .HasColumnName("end_date_utc");

        builder.Property(subscription => subscription.ActivatedByUserPublicId)
            .HasColumnName("activated_by_user_public_id");

        builder.Property(subscription => subscription.ActivatedAtUtc)
            .HasColumnName("activated_at_utc");

        builder.Property(subscription => subscription.StatusChangedAtUtc)
            .HasColumnName("status_changed_at_utc");

        builder.Property(subscription => subscription.CurrentStatusReasonCode)
            .HasColumnName("current_status_reason_code")
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(subscription => subscription.CurrentStatusObservations)
            .HasColumnName("current_status_observations")
            .HasMaxLength(2000);

        builder.Property(subscription => subscription.CurrentStatusOrigin)
            .HasColumnName("current_status_origin")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(subscription => subscription.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(subscription => subscription.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(
                [nameof(CompanySubscription.CompanyId), nameof(CompanySubscription.Status)],
                "company_subscription_live_status_idx")
            .IsUnique()
            .HasFilter("status IN ('Draft', 'Trial', 'Active', 'Suspended')")
            .HasDatabaseName("uq_company_subscriptions__company_live");

        builder.HasIndex(
                [nameof(CompanySubscription.CompanyId), nameof(CompanySubscription.Status)],
                "company_subscription_scheduled_status_idx")
            .IsUnique()
            .HasFilter("status = 'Scheduled'")
            .HasDatabaseName("uq_company_subscriptions__company_scheduled");

        builder.HasIndex(subscription => subscription.CommercialPlanId)
            .HasDatabaseName("ix_company_subscriptions__commercial_plan_id");

        builder.HasIndex(subscription => subscription.CommercialPlanVersionId)
            .HasDatabaseName("ix_company_subscriptions__commercial_plan_version_id");

        builder.HasIndex(subscription => new { subscription.Status, subscription.StartDateUtc })
            .HasDatabaseName("ix_company_subscriptions__status_start_date");

        builder.HasIndex(subscription => new { subscription.CompanyId, subscription.StatusChangedAtUtc })
            .HasDatabaseName("ix_company_subscriptions__company_status_changed");

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

        builder.HasMany(subscription => subscription.StatusTransitions)
            .WithOne(transition => transition.CompanySubscription)
            .HasForeignKey(transition => transition.CompanySubscriptionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_company_subscription_status_transitions__company_subscriptions");
    }
}

internal sealed class CompanySubscriptionStatusTransitionConfiguration : IEntityTypeConfiguration<CompanySubscriptionStatusTransition>
{
    public void Configure(EntityTypeBuilder<CompanySubscriptionStatusTransition> builder)
    {
        builder.ToTable("company_subscription_status_transitions");

        builder.HasKey(transition => transition.Id)
            .HasName("pk_company_subscription_status_transitions");

        builder.Property(transition => transition.Id)
            .HasColumnName("id");

        builder.Property(transition => transition.CompanySubscriptionId)
            .HasColumnName("company_subscription_id");

        builder.Property(transition => transition.PreviousStatus)
            .HasColumnName("previous_status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(transition => transition.NewStatus)
            .HasColumnName("new_status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(transition => transition.ReasonCode)
            .HasColumnName("reason_code")
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(transition => transition.Observations)
            .HasColumnName("observations")
            .HasMaxLength(2000);

        builder.Property(transition => transition.ChangedAtUtc)
            .HasColumnName("changed_at_utc");

        builder.Property(transition => transition.Origin)
            .HasColumnName("origin")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(transition => transition.ActorUserPublicId)
            .HasColumnName("actor_user_public_id");

        builder.Property(transition => transition.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(transition => transition.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(transition => new { transition.CompanySubscriptionId, transition.ChangedAtUtc })
            .HasDatabaseName("ix_company_subscription_status_transitions__subscription_changed");
    }
}
