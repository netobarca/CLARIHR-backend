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

        builder.Property(subscription => subscription.PlanCode)
            .HasColumnName("plan_code")
            .HasMaxLength(40);

        builder.Property(subscription => subscription.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(subscription => subscription.StartDateUtc)
            .HasColumnName("start_date_utc");

        builder.Property(subscription => subscription.EndDateUtc)
            .HasColumnName("end_date_utc");

        builder.Property(subscription => subscription.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(subscription => subscription.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(subscription => new { subscription.CompanyId, subscription.Status })
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName("uq_company_subscriptions__company_active");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(subscription => subscription.CompanyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_company_subscriptions__companies");
    }
}
