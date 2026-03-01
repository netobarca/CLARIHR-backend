using CLARIHR.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Companies;

internal sealed class PlanEntitlementConfiguration : IEntityTypeConfiguration<PlanEntitlement>
{
    public void Configure(EntityTypeBuilder<PlanEntitlement> builder)
    {
        builder.ToTable("plan_entitlements");

        builder.HasKey(entitlement => entitlement.Id)
            .HasName("pk_plan_entitlements");

        builder.Property(entitlement => entitlement.Id)
            .HasColumnName("id");

        builder.Property(entitlement => entitlement.PlanCode)
            .HasColumnName("plan_code")
            .HasMaxLength(40);

        builder.Property(entitlement => entitlement.ModuleKey)
            .HasColumnName("module_key")
            .HasMaxLength(60);

        builder.Property(entitlement => entitlement.IsEnabled)
            .HasColumnName("is_enabled");

        builder.Property(entitlement => entitlement.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(entitlement => entitlement.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(entitlement => new { entitlement.PlanCode, entitlement.ModuleKey })
            .IsUnique()
            .HasDatabaseName("uq_plan_entitlements__plan_module");
    }
}
