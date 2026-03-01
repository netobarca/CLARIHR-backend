using CLARIHR.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Auth;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("auth_users");

        builder.HasKey(user => user.Id)
            .HasName("pk_auth_users");

        builder.Property(user => user.Id)
            .HasColumnName("id");

        builder.Property(user => user.PublicId)
            .HasColumnName("public_id");

        builder.Property(user => user.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100);

        builder.Property(user => user.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100);

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(320);

        builder.Property(user => user.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(320);

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(512);

        builder.Property(user => user.AuthProvider)
            .HasColumnName("auth_provider")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(user => user.ProviderUserId)
            .HasColumnName("provider_user_id")
            .HasMaxLength(200);

        builder.Property(user => user.Country)
            .HasColumnName("country")
            .HasMaxLength(100);

        builder.Property(user => user.Source)
            .HasColumnName("source")
            .HasMaxLength(100);

        builder.Property(user => user.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(user => user.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(user => user.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(user => user.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_auth_users__public_id");

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("uq_auth_users__normalized_email");

        builder.HasIndex(user => new { user.AuthProvider, user.ProviderUserId })
            .IsUnique()
            .HasFilter("provider_user_id is not null")
            .HasDatabaseName("uq_auth_users__provider_link");
    }
}
