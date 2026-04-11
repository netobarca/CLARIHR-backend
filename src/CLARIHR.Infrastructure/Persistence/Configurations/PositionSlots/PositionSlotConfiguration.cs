using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PositionSlots;

internal sealed class PositionSlotConfiguration : IEntityTypeConfiguration<PositionSlot>
{
    public void Configure(EntityTypeBuilder<PositionSlot> builder)
    {
        builder.ToTable("position_slots");

        builder.HasKey(slot => slot.Id)
            .HasName("pk_position_slots");

        builder.Property(slot => slot.Id)
            .HasColumnName("id");

        builder.Property(slot => slot.PublicId)
            .HasColumnName("public_id");

        builder.Property(slot => slot.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(slot => slot.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(slot => slot.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(slot => slot.Title)
            .HasColumnName("title")
            .HasMaxLength(180);

        builder.Property(slot => slot.JobProfileId)
            .HasColumnName("job_profile_id");

        builder.Property(slot => slot.RoleId)
            .HasColumnName("role_id");

        builder.Property(slot => slot.WorkCenterId)
            .HasColumnName("work_center_id");

        builder.Property(slot => slot.DirectDependencyPositionSlotId)
            .HasColumnName("direct_dependency_position_slot_id");

        builder.Property(slot => slot.FunctionalDependencyPositionSlotId)
            .HasColumnName("functional_dependency_position_slot_id");

        builder.Property(slot => slot.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(slot => slot.MaxEmployees)
            .HasColumnName("max_employees");

        builder.Property(slot => slot.OccupiedEmployees)
            .HasColumnName("occupied_employees");

        builder.Property(slot => slot.IsFixedTerm)
            .HasColumnName("is_fixed_term");

        builder.Property(slot => slot.EffectiveFromUtc)
            .HasColumnName("effective_from_utc");

        builder.Property(slot => slot.EffectiveToUtc)
            .HasColumnName("effective_to_utc");

        builder.Property(slot => slot.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.Property(slot => slot.IsActive)
            .HasColumnName("is_active");

        builder.Property(slot => slot.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(slot => slot.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(slot => slot.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(slot => slot.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_position_slots__public_id");

        builder.HasIndex(slot => new { slot.TenantId, slot.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_position_slots__tenant_code");

        builder.HasIndex(slot => new { slot.TenantId, slot.Status })
            .HasDatabaseName("ix_position_slots__tenant_status");

        builder.HasIndex(slot => new { slot.TenantId, slot.JobProfileId })
            .HasDatabaseName("ix_position_slots__tenant_job_profile");

        builder.HasIndex(slot => new { slot.TenantId, slot.RoleId })
            .HasDatabaseName("ix_position_slots__tenant_role");

        builder.HasIndex(slot => new { slot.TenantId, slot.WorkCenterId })
            .HasDatabaseName("ix_position_slots__tenant_work_center");

        builder.HasIndex(slot => new { slot.TenantId, slot.DirectDependencyPositionSlotId })
            .HasDatabaseName("ix_position_slots__tenant_direct_dependency");

        builder.HasIndex(slot => new { slot.TenantId, slot.FunctionalDependencyPositionSlotId })
            .HasDatabaseName("ix_position_slots__tenant_functional_dependency");

        builder.HasOne<JobProfile>()
            .WithMany()
            .HasForeignKey(slot => slot.JobProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_position_slots__job_profile");

        builder.HasOne<IamRole>()
            .WithMany()
            .HasForeignKey(slot => slot.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_position_slots__role");

        builder.HasOne<WorkCenter>()
            .WithMany()
            .HasForeignKey(slot => slot.WorkCenterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_position_slots__work_center");

        builder.HasOne<PositionSlot>()
            .WithMany()
            .HasForeignKey(slot => slot.DirectDependencyPositionSlotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_position_slots__direct_dependency");

        builder.HasOne<PositionSlot>()
            .WithMany()
            .HasForeignKey(slot => slot.FunctionalDependencyPositionSlotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_position_slots__functional_dependency");
    }
}
