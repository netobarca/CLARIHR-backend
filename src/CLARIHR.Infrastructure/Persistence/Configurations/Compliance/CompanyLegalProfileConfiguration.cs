using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Compliance;

internal sealed class CompanyLegalProfileConfiguration : IEntityTypeConfiguration<CompanyLegalProfile>
{
    public void Configure(EntityTypeBuilder<CompanyLegalProfile> builder)
    {
        builder.ToTable("company_legal_profiles");

        builder.HasKey(profile => profile.Id)
            .HasName("pk_company_legal_profiles");

        builder.Property(profile => profile.Id)
            .HasColumnName("id");

        builder.Property(profile => profile.PublicId)
            .HasColumnName("public_id");

        builder.Property(profile => profile.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(profile => profile.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(200);

        builder.Property(profile => profile.EmployerNitNumber)
            .HasColumnName("employer_nit_number")
            .HasMaxLength(20);

        builder.Property(profile => profile.IsssEmployerRegistrationNumber)
            .HasColumnName("isss_employer_registration_number")
            .HasMaxLength(20);

        builder.Property(profile => profile.FiscalAddress)
            .HasColumnName("fiscal_address")
            .HasMaxLength(500);

        builder.Property(profile => profile.EconomicActivityDescription)
            .HasColumnName("economic_activity_description")
            .HasMaxLength(200);

        builder.Property(profile => profile.LegalRepresentativePublicId)
            .HasColumnName("legal_representative_public_id");

        builder.Property(profile => profile.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(profile => profile.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(profile => profile.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(profile => profile.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_company_legal_profiles__public_id");

        builder.HasIndex(profile => profile.TenantId)
            .IsUnique()
            .HasDatabaseName("uq_company_legal_profiles__tenant_id");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(profile => profile.TenantId)
            .HasPrincipalKey(company => company.PublicId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_legal_profiles__companies");
    }
}
