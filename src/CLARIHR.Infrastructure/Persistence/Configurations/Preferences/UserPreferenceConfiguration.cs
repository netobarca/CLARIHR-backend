using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Preferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Preferences;

internal sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("user_preferences");

        builder.HasKey(preference => preference.Id)
            .HasName("pk_user_preferences");

        builder.Property(preference => preference.Id)
            .HasColumnName("id");

        builder.Property(preference => preference.PublicId)
            .HasColumnName("public_id");

        builder.Property(preference => preference.UserId)
            .HasColumnName("user_id");

        builder.Property(preference => preference.Language)
            .HasColumnName("locale")
            .HasMaxLength(3);

        builder.Property(preference => preference.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(preference => preference.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(preference => preference.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_user_preferences__public_id");

        builder.HasIndex(preference => preference.UserId)
            .IsUnique()
            .HasDatabaseName("uq_user_preferences__user_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_preferences__auth_users");
    }
}
