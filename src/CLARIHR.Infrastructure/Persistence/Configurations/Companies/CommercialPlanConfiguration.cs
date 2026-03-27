using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Companies;

internal sealed class CommercialPlanConfiguration : IEntityTypeConfiguration<CommercialPlan>
{
    public void Configure(EntityTypeBuilder<CommercialPlan> builder)
    {
        builder.ToTable("commercial_plans");

        builder.HasKey(plan => plan.Id)
            .HasName("pk_commercial_plans");

        builder.Property(plan => plan.Id)
            .HasColumnName("id");

        builder.Property(plan => plan.PublicId)
            .HasColumnName("public_id");

        builder.Property(plan => plan.Code)
            .HasColumnName("code")
            .HasMaxLength(40);

        builder.Property(plan => plan.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(40);

        builder.Property(plan => plan.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(plan => plan.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(plan => plan.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(plan => plan.BaseMonthlyFee)
            .HasColumnName("base_monthly_fee")
            .HasPrecision(18, 2);

        builder.Property(plan => plan.PricePerActiveEmployee)
            .HasColumnName("price_per_active_employee")
            .HasPrecision(18, 2);

        builder.Property(plan => plan.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(plan => plan.IsSystemPlan)
            .HasColumnName("is_system_plan");

        builder.Property(plan => plan.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(plan => plan.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(plan => plan.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(plan => plan.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_commercial_plans__public_id");

        builder.HasIndex(plan => plan.NormalizedCode)
            .IsUnique()
            .HasDatabaseName("uq_commercial_plans__normalized_code");

        builder.HasIndex(plan => plan.NormalizedName)
            .HasDatabaseName("ix_commercial_plans__normalized_name");

        builder.HasIndex(plan => plan.Status)
            .HasDatabaseName("ix_commercial_plans__status");

        builder.HasMany(plan => plan.Limits)
            .WithOne()
            .HasForeignKey(limit => limit.CommercialPlanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_commercial_plan_limits__commercial_plans");

        builder.Navigation(plan => plan.Limits).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(GlobalCatalogSeedData.GetCommercialPlans());
    }
}

internal sealed class CommercialPlanLimitConfiguration : IEntityTypeConfiguration<CommercialPlanLimit>
{
    public void Configure(EntityTypeBuilder<CommercialPlanLimit> builder)
    {
        builder.ToTable("commercial_plan_limits");

        builder.HasKey(limit => limit.Id)
            .HasName("pk_commercial_plan_limits");

        builder.Property(limit => limit.Id)
            .HasColumnName("id");

        builder.Property(limit => limit.CommercialPlanId)
            .HasColumnName("commercial_plan_id");

        builder.Property(limit => limit.LimitCode)
            .HasColumnName("limit_code")
            .HasMaxLength(80);

        builder.Property(limit => limit.NormalizedLimitCode)
            .HasColumnName("normalized_limit_code")
            .HasMaxLength(80);

        builder.Property(limit => limit.Value)
            .HasColumnName("value")
            .HasPrecision(18, 2);

        builder.HasIndex(limit => new { limit.CommercialPlanId, limit.NormalizedLimitCode })
            .IsUnique()
            .HasDatabaseName("uq_commercial_plan_limits__plan_limit_code");
    }
}
