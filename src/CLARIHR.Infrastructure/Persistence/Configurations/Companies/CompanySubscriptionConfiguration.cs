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

internal sealed class CompanySubscriptionStatusChangeRequestConfiguration : IEntityTypeConfiguration<CompanySubscriptionStatusChangeRequest>
{
    public void Configure(EntityTypeBuilder<CompanySubscriptionStatusChangeRequest> builder)
    {
        builder.ToTable("company_subscription_status_change_requests");

        builder.HasKey(request => request.Id)
            .HasName("pk_company_subscription_status_change_requests");

        builder.Property(request => request.Id)
            .HasColumnName("id");

        builder.Property(request => request.PublicId)
            .HasColumnName("public_id");

        builder.Property(request => request.CompanyId)
            .HasColumnName("company_id");

        builder.Property(request => request.CompanySubscriptionId)
            .HasColumnName("company_subscription_id");

        builder.Property(request => request.CurrentStatus)
            .HasColumnName("current_status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(request => request.TargetStatus)
            .HasColumnName("target_status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(request => request.ReasonCode)
            .HasColumnName("reason_code")
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(request => request.RequestedAtUtc)
            .HasColumnName("requested_at_utc");

        builder.Property(request => request.EffectiveDateUtc)
            .HasColumnName("effective_date_utc");

        builder.Property(request => request.RequestedByUserPublicId)
            .HasColumnName("requested_by_user_public_id");

        builder.Property(request => request.Observations)
            .HasColumnName("observations")
            .HasMaxLength(2000);

        builder.Property(request => request.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(request => request.AppliedAtUtc)
            .HasColumnName("applied_at_utc");

        builder.Property(request => request.RejectedAtUtc)
            .HasColumnName("rejected_at_utc");

        builder.Property(request => request.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(2000);

        builder.Property(request => request.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(request => request.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(request => request.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_company_subscription_status_change_requests__public_id");

        builder.HasIndex(request => new { request.Status, request.EffectiveDateUtc })
            .HasDatabaseName("ix_company_subscription_status_change_requests__status_effective_date");

        builder.HasIndex(request => new { request.CompanyId, request.RequestedAtUtc })
            .HasDatabaseName("ix_company_subscription_status_change_requests__company_requested");

        builder.HasIndex(
                new[] { nameof(CompanySubscriptionStatusChangeRequest.CompanySubscriptionId), nameof(CompanySubscriptionStatusChangeRequest.Status) },
                "company_subscription_status_change_requests_pending_status_idx")
            .IsUnique()
            .HasFilter("status = 'Scheduled'")
            .HasDatabaseName("uq_company_subscription_status_change_requests__subscription_scheduled");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(request => request.CompanyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_company_subscription_status_change_requests__companies");

        builder.HasOne<CompanySubscription>()
            .WithMany()
            .HasForeignKey(request => request.CompanySubscriptionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_company_subscription_status_change_requests__company_subscriptions");
    }
}

internal sealed class CompanySubscriptionPlanChangeConfiguration : IEntityTypeConfiguration<CompanySubscriptionPlanChange>
{
    public void Configure(EntityTypeBuilder<CompanySubscriptionPlanChange> builder)
    {
        builder.ToTable("company_subscription_plan_changes");

        builder.HasKey(planChange => planChange.Id)
            .HasName("pk_company_subscription_plan_changes");

        builder.Property(planChange => planChange.Id)
            .HasColumnName("id");

        builder.Property(planChange => planChange.PublicId)
            .HasColumnName("public_id");

        builder.Property(planChange => planChange.CompanyId)
            .HasColumnName("company_id");

        builder.Property(planChange => planChange.CompanySubscriptionId)
            .HasColumnName("company_subscription_id");

        builder.Property(planChange => planChange.CurrentCommercialPlanId)
            .HasColumnName("current_commercial_plan_id");

        builder.Property(planChange => planChange.CurrentCommercialPlanVersionId)
            .HasColumnName("current_commercial_plan_version_id");

        builder.Property(planChange => planChange.CurrentPlanCode)
            .HasColumnName("current_plan_code")
            .HasMaxLength(40);

        builder.Property(planChange => planChange.CurrentPlanName)
            .HasColumnName("current_plan_name")
            .HasMaxLength(150);

        builder.Property(planChange => planChange.CurrentPlanVersionNumber)
            .HasColumnName("current_plan_version_number");

        builder.Property(planChange => planChange.CurrentBaseMonthlyFee)
            .HasColumnName("current_base_monthly_fee")
            .HasPrecision(18, 2);

        builder.Property(planChange => planChange.CurrentPricePerActiveEmployee)
            .HasColumnName("current_price_per_active_employee")
            .HasPrecision(18, 2);

        builder.Property(planChange => planChange.CurrentPeriodicity)
            .HasColumnName("current_periodicity")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(planChange => planChange.CurrentCurrencyCode)
            .HasColumnName("current_currency_code")
            .HasMaxLength(3);

        builder.Property(planChange => planChange.TargetCommercialPlanId)
            .HasColumnName("target_commercial_plan_id");

        builder.Property(planChange => planChange.TargetCommercialPlanVersionId)
            .HasColumnName("target_commercial_plan_version_id");

        builder.Property(planChange => planChange.TargetPlanCode)
            .HasColumnName("target_plan_code")
            .HasMaxLength(40);

        builder.Property(planChange => planChange.TargetPlanName)
            .HasColumnName("target_plan_name")
            .HasMaxLength(150);

        builder.Property(planChange => planChange.TargetPlanVersionNumber)
            .HasColumnName("target_plan_version_number");

        builder.Property(planChange => planChange.TargetBaseMonthlyFee)
            .HasColumnName("target_base_monthly_fee")
            .HasPrecision(18, 2);

        builder.Property(planChange => planChange.TargetPricePerActiveEmployee)
            .HasColumnName("target_price_per_active_employee")
            .HasPrecision(18, 2);

        builder.Property(planChange => planChange.TargetPeriodicity)
            .HasColumnName("target_periodicity")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(planChange => planChange.TargetCurrencyCode)
            .HasColumnName("target_currency_code")
            .HasMaxLength(3);

        builder.Property(planChange => planChange.Mode)
            .HasColumnName("mode")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(planChange => planChange.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(planChange => planChange.ReasonCode)
            .HasColumnName("reason_code")
            .HasConversion<string>()
            .HasMaxLength(60);

        builder.Property(planChange => planChange.RequestedAtUtc)
            .HasColumnName("requested_at_utc");

        builder.Property(planChange => planChange.EffectiveDateUtc)
            .HasColumnName("effective_date_utc");

        builder.Property(planChange => planChange.RequestedByUserPublicId)
            .HasColumnName("requested_by_user_public_id");

        builder.Property(planChange => planChange.Observations)
            .HasColumnName("observations")
            .HasMaxLength(2000);

        builder.Property(planChange => planChange.EstimatedNextCharge)
            .HasColumnName("estimated_next_charge")
            .HasPrecision(18, 2);

        builder.Property(planChange => planChange.ActiveEmployeeCount)
            .HasColumnName("active_employee_count");

        builder.Property(planChange => planChange.AppliedAtUtc)
            .HasColumnName("applied_at_utc");

        builder.Property(planChange => planChange.AppliedSubscriptionPublicId)
            .HasColumnName("applied_subscription_public_id");

        builder.Property(planChange => planChange.CancelledAtUtc)
            .HasColumnName("cancelled_at_utc");

        builder.Property(planChange => planChange.CancelledByUserPublicId)
            .HasColumnName("cancelled_by_user_public_id");

        builder.Property(planChange => planChange.CancellationObservations)
            .HasColumnName("cancellation_observations")
            .HasMaxLength(2000);

        builder.Property(planChange => planChange.RejectedAtUtc)
            .HasColumnName("rejected_at_utc");

        builder.Property(planChange => planChange.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(2000);

        builder.Property(planChange => planChange.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(planChange => planChange.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.Property(planChange => planChange.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.HasIndex(planChange => planChange.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_company_subscription_plan_changes__public_id");

        builder.HasIndex(planChange => new { planChange.CompanyId, planChange.RequestedAtUtc })
            .HasDatabaseName("ix_company_subscription_plan_changes__company_requested");

        builder.HasIndex(planChange => new { planChange.Status, planChange.EffectiveDateUtc })
            .HasDatabaseName("ix_company_subscription_plan_changes__status_effective_date");

        builder.HasIndex(planChange => planChange.CompanySubscriptionId)
            .HasDatabaseName("ix_company_subscription_plan_changes__subscription_id");

        builder.HasIndex(
                new[] { nameof(CompanySubscriptionPlanChange.CompanyId), nameof(CompanySubscriptionPlanChange.Status) },
                "company_subscription_plan_changes_pending_status_idx")
            .IsUnique()
            .HasFilter("status = 'Scheduled'")
            .HasDatabaseName("uq_company_subscription_plan_changes__company_scheduled");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(planChange => planChange.CompanyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_company_subscription_plan_changes__companies");

        builder.HasOne<CompanySubscription>()
            .WithMany()
            .HasForeignKey(planChange => planChange.CompanySubscriptionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_subscription_plan_changes__company_subscriptions");

        builder.HasOne<CommercialPlan>()
            .WithMany()
            .HasForeignKey(planChange => planChange.TargetCommercialPlanId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_subscription_plan_changes__target_commercial_plans");

        builder.HasOne<CommercialPlanVersion>()
            .WithMany()
            .HasForeignKey(planChange => planChange.TargetCommercialPlanVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_subscription_plan_changes__target_plan_versions");
    }
}

internal sealed class CompanyCommercialAddonConfiguration : IEntityTypeConfiguration<CompanyCommercialAddon>
{
    public void Configure(EntityTypeBuilder<CompanyCommercialAddon> builder)
    {
        builder.ToTable("company_commercial_addons");

        builder.HasKey(companyAddon => companyAddon.Id)
            .HasName("pk_company_commercial_addons");

        builder.Property(companyAddon => companyAddon.Id)
            .HasColumnName("id");

        builder.Property(companyAddon => companyAddon.PublicId)
            .HasColumnName("public_id");

        builder.Property(companyAddon => companyAddon.CompanyId)
            .HasColumnName("company_id");

        builder.Property(companyAddon => companyAddon.CompanySubscriptionId)
            .HasColumnName("company_subscription_id");

        builder.Property(companyAddon => companyAddon.CommercialAddonId)
            .HasColumnName("commercial_addon_id");

        builder.Property(companyAddon => companyAddon.AddonCode)
            .HasColumnName("addon_code")
            .HasMaxLength(40);

        builder.Property(companyAddon => companyAddon.AddonName)
            .HasColumnName("addon_name")
            .HasMaxLength(150);

        builder.Property(companyAddon => companyAddon.AddonType)
            .HasColumnName("addon_type")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(companyAddon => companyAddon.BillingModel)
            .HasColumnName("billing_model")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(companyAddon => companyAddon.MeasurementUnit)
            .HasColumnName("measurement_unit")
            .HasMaxLength(80);

        builder.Property(companyAddon => companyAddon.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2);

        builder.Property(companyAddon => companyAddon.MinimumQuantity)
            .HasColumnName("minimum_quantity");

        builder.Property(companyAddon => companyAddon.MinimumMonthlyFee)
            .HasColumnName("minimum_monthly_fee")
            .HasPrecision(18, 2);

        builder.Property(companyAddon => companyAddon.Periodicity)
            .HasColumnName("periodicity")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(companyAddon => companyAddon.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3);

        builder.Property(companyAddon => companyAddon.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(companyAddon => companyAddon.StatusEffectiveDateUtc)
            .HasColumnName("status_effective_date_utc");

        builder.Property(companyAddon => companyAddon.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(companyAddon => companyAddon.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(companyAddon => companyAddon.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_company_commercial_addons__public_id");

        builder.HasIndex(companyAddon => new { companyAddon.CompanyId, companyAddon.CommercialAddonId })
            .IsUnique()
            .HasDatabaseName("uq_company_commercial_addons__company_addon");

        builder.HasIndex(companyAddon => new { companyAddon.CompanyId, companyAddon.Status })
            .HasDatabaseName("ix_company_commercial_addons__company_status");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(companyAddon => companyAddon.CompanyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_company_commercial_addons__companies");

        builder.HasOne<CompanySubscription>()
            .WithMany()
            .HasForeignKey(companyAddon => companyAddon.CompanySubscriptionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_commercial_addons__company_subscriptions");

        builder.HasOne<CommercialAddon>()
            .WithMany()
            .HasForeignKey(companyAddon => companyAddon.CommercialAddonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_commercial_addons__commercial_addons");
    }
}

internal sealed class CompanyCommercialAddonChangeConfiguration : IEntityTypeConfiguration<CompanyCommercialAddonChange>
{
    public void Configure(EntityTypeBuilder<CompanyCommercialAddonChange> builder)
    {
        builder.ToTable("company_commercial_addon_changes");

        builder.HasKey(change => change.Id)
            .HasName("pk_company_commercial_addon_changes");

        builder.Property(change => change.Id)
            .HasColumnName("id");

        builder.Property(change => change.PublicId)
            .HasColumnName("public_id");

        builder.Property(change => change.CompanyId)
            .HasColumnName("company_id");

        builder.Property(change => change.CompanySubscriptionId)
            .HasColumnName("company_subscription_id");

        builder.Property(change => change.CommercialAddonId)
            .HasColumnName("commercial_addon_id");

        builder.Property(change => change.AddonCode)
            .HasColumnName("addon_code")
            .HasMaxLength(40);

        builder.Property(change => change.AddonName)
            .HasColumnName("addon_name")
            .HasMaxLength(150);

        builder.Property(change => change.AddonType)
            .HasColumnName("addon_type")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(change => change.BillingModel)
            .HasColumnName("billing_model")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(change => change.MeasurementUnit)
            .HasColumnName("measurement_unit")
            .HasMaxLength(80);

        builder.Property(change => change.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2);

        builder.Property(change => change.MinimumQuantity)
            .HasColumnName("minimum_quantity");

        builder.Property(change => change.MinimumMonthlyFee)
            .HasColumnName("minimum_monthly_fee")
            .HasPrecision(18, 2);

        builder.Property(change => change.Periodicity)
            .HasColumnName("periodicity")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(change => change.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3);

        builder.Property(change => change.Action)
            .HasColumnName("action")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(change => change.Mode)
            .HasColumnName("mode")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(change => change.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(change => change.ReasonCode)
            .HasColumnName("reason_code")
            .HasConversion<string>()
            .HasMaxLength(60);

        builder.Property(change => change.PreviousStatus)
            .HasColumnName("previous_status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(change => change.ResultingStatus)
            .HasColumnName("resulting_status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(change => change.RequestedAtUtc)
            .HasColumnName("requested_at_utc");

        builder.Property(change => change.EffectiveDateUtc)
            .HasColumnName("effective_date_utc");

        builder.Property(change => change.RequestedByUserPublicId)
            .HasColumnName("requested_by_user_public_id");

        builder.Property(change => change.Observations)
            .HasColumnName("observations")
            .HasMaxLength(2000);

        builder.Property(change => change.QuantityBasis)
            .HasColumnName("quantity_basis");

        builder.Property(change => change.EstimatedNextChargeImpact)
            .HasColumnName("estimated_next_charge_impact")
            .HasPrecision(18, 2);

        builder.Property(change => change.AppliedAtUtc)
            .HasColumnName("applied_at_utc");

        builder.Property(change => change.AppliedSubscriptionPublicId)
            .HasColumnName("applied_subscription_public_id");

        builder.Property(change => change.CancelledAtUtc)
            .HasColumnName("cancelled_at_utc");

        builder.Property(change => change.CancelledByUserPublicId)
            .HasColumnName("cancelled_by_user_public_id");

        builder.Property(change => change.CancellationObservations)
            .HasColumnName("cancellation_observations")
            .HasMaxLength(2000);

        builder.Property(change => change.RejectedAtUtc)
            .HasColumnName("rejected_at_utc");

        builder.Property(change => change.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(2000);

        builder.Property(change => change.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(change => change.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.Property(change => change.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.HasIndex(change => change.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_company_commercial_addon_changes__public_id");

        builder.HasIndex(change => new { change.CompanyId, change.RequestedAtUtc })
            .HasDatabaseName("ix_company_commercial_addon_changes__company_requested");

        builder.HasIndex(change => new { change.Status, change.EffectiveDateUtc })
            .HasDatabaseName("ix_company_commercial_addon_changes__status_effective_date");

        builder.HasIndex(
                new[] { nameof(CompanyCommercialAddonChange.CompanyId), nameof(CompanyCommercialAddonChange.CommercialAddonId), nameof(CompanyCommercialAddonChange.Status) },
                "company_commercial_addon_changes_pending_status_idx")
            .IsUnique()
            .HasFilter("status = 'Scheduled'")
            .HasDatabaseName("uq_company_commercial_addon_changes__company_addon_scheduled");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(change => change.CompanyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_company_commercial_addon_changes__companies");

        builder.HasOne<CompanySubscription>()
            .WithMany()
            .HasForeignKey(change => change.CompanySubscriptionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_commercial_addon_changes__company_subscriptions");

        builder.HasOne<CommercialAddon>()
            .WithMany()
            .HasForeignKey(change => change.CommercialAddonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_commercial_addon_changes__commercial_addons");
    }
}
