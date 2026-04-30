using CLARIHR.Domain.EducationCatalogs;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.Banks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileConfiguration : IEntityTypeConfiguration<PersonnelFile>
{
    public void Configure(EntityTypeBuilder<PersonnelFile> builder)
    {
        builder.ToTable("personnel_files");

        builder.HasKey(file => file.Id)
            .HasName("pk_personnel_files");

        builder.Property(file => file.Id).HasColumnName("id");
        builder.Property(file => file.PublicId).HasColumnName("public_id");
        builder.Property(file => file.TenantId).HasColumnName("tenant_id");
        builder.Property(file => file.RecordType)
            .HasColumnName("record_type")
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(file => file.LifecycleStatus)
            .HasColumnName("lifecycle_status")
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(file => file.FirstName).HasColumnName("first_name").HasMaxLength(100);
        builder.Property(file => file.LastName).HasColumnName("last_name").HasMaxLength(100);
        builder.Property(file => file.FullName).HasColumnName("full_name").HasMaxLength(201);
        builder.Property(file => file.NormalizedFullName).HasColumnName("normalized_full_name").HasMaxLength(201);
        builder.Property(file => file.BirthDate).HasColumnName("birth_date");
        builder.Property(file => file.MaritalStatus).HasColumnName("marital_status").HasMaxLength(80);
        builder.Property(file => file.Profession).HasColumnName("profession").HasMaxLength(120);
        builder.Property(file => file.Nationality).HasColumnName("nationality").HasMaxLength(120);
        builder.Property(file => file.PersonalEmail).HasColumnName("personal_email").HasMaxLength(320);
        builder.Property(file => file.InstitutionalEmail).HasColumnName("institutional_email").HasMaxLength(320);
        builder.Property(file => file.PersonalPhone).HasColumnName("personal_phone").HasMaxLength(40);
        builder.Property(file => file.InstitutionalPhone).HasColumnName("institutional_phone").HasMaxLength(40);
        builder.Property(file => file.BirthCountry).HasColumnName("birth_country").HasMaxLength(120);
        builder.Property(file => file.BirthDepartment).HasColumnName("birth_department").HasMaxLength(120);
        builder.Property(file => file.BirthMunicipality).HasColumnName("birth_municipality").HasMaxLength(120);
        builder.Property(file => file.PhotoUrl).HasColumnName("photo_url").HasMaxLength(1000);
        builder.Property(file => file.OrgUnitPublicId).HasColumnName("org_unit_public_id");
        builder.Property(file => file.AssignedPositionSlotPublicId).HasColumnName("assigned_position_slot_public_id");
        builder.Property(file => file.LinkedUserPublicId).HasColumnName("linked_user_public_id");
        builder.Property(file => file.CustomDataJson).HasColumnName("custom_data").HasColumnType("jsonb");
        builder.Property(file => file.IsActive).HasColumnName("is_active");
        builder.Property(file => file.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(file => file.CreatedUtc).HasColumnName("created_utc");
        builder.Property(file => file.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(file => file.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_files__public_id");

        builder.HasIndex(file => new { file.TenantId, file.NormalizedFullName })
            .HasDatabaseName("ix_personnel_files__tenant_name");

        builder.HasIndex(file => new { file.TenantId, file.RecordType, file.IsActive })
            .HasDatabaseName("ix_personnel_files__tenant_type_active");

        builder.HasIndex(file => new { file.TenantId, file.LifecycleStatus, file.RecordType })
            .HasDatabaseName("ix_personnel_files__tenant_lifecycle_type");

        builder.HasIndex(file => new { file.TenantId, file.CreatedUtc, file.PublicId })
            .HasDatabaseName("ix_personnel_files__tenant_created_public");

        builder.HasIndex(file => new { file.TenantId, file.BirthDate, file.PublicId })
            .HasDatabaseName("ix_personnel_files__tenant_birth_public");

        builder.HasIndex(file => new { file.TenantId, file.LifecycleStatus, file.RecordType, file.OrgUnitPublicId })
            .HasDatabaseName("ix_personnel_files__tenant_lifecycle_type_org_unit");

        builder.HasIndex(file => new { file.TenantId, file.OrgUnitPublicId })
            .HasDatabaseName("ix_personnel_files__tenant_org_unit");

        builder.HasIndex(file => new { file.TenantId, file.AssignedPositionSlotPublicId })
            .HasDatabaseName("ix_personnel_files__tenant_assigned_position_slot");

        builder.HasIndex(file => new { file.TenantId, file.LinkedUserPublicId })
            .IsUnique()
            .HasFilter("linked_user_public_id is not null")
            .HasDatabaseName("uq_personnel_files__tenant_linked_user");

        builder.HasMany(file => file.Identifications)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_identifications__personnel_file");

        builder.HasMany(file => file.Addresses)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_addresses__personnel_file");

        builder.HasMany(file => file.EmergencyContacts)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_emergency_contacts__personnel_file");

        builder.HasMany(file => file.FamilyMembers)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_family_members__personnel_file");

        builder.HasMany(file => file.Hobbies)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_hobbies__personnel_file");

        builder.HasMany(file => file.EmployeeRelations)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_employee_relations__personnel_file");

        builder.HasMany(file => file.BankAccounts)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_bank_accounts__personnel_file");

        builder.HasMany(file => file.Associations)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_associations__personnel_file");

        builder.HasMany(file => file.Educations)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_educations__personnel_file");

        builder.HasMany(file => file.Languages)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_languages__personnel_file");

        builder.HasMany(file => file.Trainings)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_trainings__personnel_file");

        builder.HasMany(file => file.PreviousEmployments)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_previous_employments__personnel_file");

        builder.HasMany(file => file.References)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_references__personnel_file");

        builder.HasMany(file => file.Documents)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_documents__personnel_file");

        builder.HasMany(file => file.Observations)
            .WithOne(item => item.PersonnelFile)
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_observations__personnel_file");

        builder.Navigation(file => file.Identifications).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.Addresses).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.EmergencyContacts).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.FamilyMembers).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.Hobbies).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.EmployeeRelations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.BankAccounts).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.Associations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.Educations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.Languages).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.Trainings).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.PreviousEmployments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.References).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(file => file.Observations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PersonnelFileIdentificationConfiguration : IEntityTypeConfiguration<PersonnelFileIdentification>
{
    public void Configure(EntityTypeBuilder<PersonnelFileIdentification> builder)
    {
        builder.ToTable("personnel_file_identifications", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_identifications__issued_expiry",
                "expiry_date is null or issued_date is null or expiry_date >= issued_date"));

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_identifications");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.IdentificationType).HasColumnName("identification_type").HasMaxLength(80);
        builder.Property(item => item.IdentificationNumber).HasColumnName("identification_number").HasMaxLength(80);
        builder.Property(item => item.NormalizedIdentificationNumber).HasColumnName("normalized_identification_number").HasMaxLength(80);
        builder.Property(item => item.IssuedDate).HasColumnName("issued_date");
        builder.Property(item => item.ExpiryDate).HasColumnName("expiry_date");
        builder.Property(item => item.Issuer).HasColumnName("issuer").HasMaxLength(200);
        builder.Property(item => item.IsPrimary).HasColumnName("is_primary");
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_identifications__public_id");

        builder.HasIndex(item => new { item.TenantId, item.IdentificationType, item.NormalizedIdentificationNumber })
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_identifications__tenant_type_number");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_identifications__tenant_file");
    }
}

internal sealed class PersonnelFileAddressConfiguration : IEntityTypeConfiguration<PersonnelFileAddress>
{
    public void Configure(EntityTypeBuilder<PersonnelFileAddress> builder)
    {
        builder.ToTable("personnel_file_addresses");

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_addresses");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.AddressLine).HasColumnName("address_line").HasMaxLength(500);
        builder.Property(item => item.Country).HasColumnName("country").HasMaxLength(120);
        builder.Property(item => item.Department).HasColumnName("department").HasMaxLength(120);
        builder.Property(item => item.Municipality).HasColumnName("municipality").HasMaxLength(120);
        builder.Property(item => item.PostalCode).HasColumnName("postal_code").HasMaxLength(40);
        builder.Property(item => item.IsCurrent).HasColumnName("is_current");
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_addresses__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_addresses__tenant_file");
    }
}

internal sealed class PersonnelFileEmergencyContactConfiguration : IEntityTypeConfiguration<PersonnelFileEmergencyContact>
{
    public void Configure(EntityTypeBuilder<PersonnelFileEmergencyContact> builder)
    {
        builder.ToTable("personnel_file_emergency_contacts");

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_emergency_contacts");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(150);
        builder.Property(item => item.Relationship).HasColumnName("relationship").HasMaxLength(80);
        builder.Property(item => item.Phone).HasColumnName("phone").HasMaxLength(40);
        builder.Property(item => item.Address).HasColumnName("address").HasMaxLength(500);
        builder.Property(item => item.Workplace).HasColumnName("workplace").HasMaxLength(200);
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_emergency_contacts__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_emergency_contacts__tenant_file");
    }
}

internal sealed class PersonnelFileFamilyMemberConfiguration : IEntityTypeConfiguration<PersonnelFileFamilyMember>
{
    public void Configure(EntityTypeBuilder<PersonnelFileFamilyMember> builder)
    {
        builder.ToTable("personnel_file_family_members", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_family_members__deceased_date",
                "is_deceased = false or deceased_date is not null"));

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_family_members");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.FirstName).HasColumnName("first_name").HasMaxLength(100);
        builder.Property(item => item.LastName).HasColumnName("last_name").HasMaxLength(100);
        builder.Property(item => item.FullName).HasColumnName("full_name").HasMaxLength(201);
        builder.Property(item => item.KinshipCode).HasColumnName("kinship_code").HasMaxLength(80);
        builder.Property(item => item.Nationality).HasColumnName("nationality").HasMaxLength(120);
        builder.Property(item => item.BirthDate).HasColumnName("birth_date");
        builder.Property(item => item.Sex)
            .HasColumnName("sex")
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(item => item.MaritalStatus).HasColumnName("marital_status").HasMaxLength(80);
        builder.Property(item => item.Occupation).HasColumnName("occupation").HasMaxLength(120);
        builder.Property(item => item.DocumentType).HasColumnName("document_type").HasMaxLength(80);
        builder.Property(item => item.DocumentNumber).HasColumnName("document_number").HasMaxLength(80);
        builder.Property(item => item.Phone).HasColumnName("phone").HasMaxLength(40);
        builder.Property(item => item.IsStudying).HasColumnName("is_studying");
        builder.Property(item => item.StudyPlace).HasColumnName("study_place").HasMaxLength(200);
        builder.Property(item => item.AcademicLevel).HasColumnName("academic_level").HasMaxLength(120);
        builder.Property(item => item.IsBeneficiary).HasColumnName("is_beneficiary");
        builder.Property(item => item.IsWorking).HasColumnName("is_working");
        builder.Property(item => item.Workplace).HasColumnName("workplace").HasMaxLength(200);
        builder.Property(item => item.JobTitle).HasColumnName("job_title").HasMaxLength(120);
        builder.Property(item => item.WorkPhone).HasColumnName("work_phone").HasMaxLength(40);
        builder.Property(item => item.Salary)
            .HasColumnName("salary")
            .HasPrecision(18, 2);
        builder.Property(item => item.IsDeceased).HasColumnName("is_deceased");
        builder.Property(item => item.DeceasedDate).HasColumnName("deceased_date");
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_family_members__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_family_members__tenant_file");
    }
}

internal sealed class PersonnelFileHobbyConfiguration : IEntityTypeConfiguration<PersonnelFileHobby>
{
    public void Configure(EntityTypeBuilder<PersonnelFileHobby> builder)
    {
        builder.ToTable("personnel_file_hobbies");

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_hobbies");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.HobbyName).HasColumnName("hobby_name").HasMaxLength(120);
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_hobbies__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_hobbies__tenant_file");
    }
}

internal sealed class PersonnelFileEmployeeRelationConfiguration : IEntityTypeConfiguration<PersonnelFileEmployeeRelation>
{
    public void Configure(EntityTypeBuilder<PersonnelFileEmployeeRelation> builder)
    {
        builder.ToTable("personnel_file_employee_relations");

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_employee_relations");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.RelatedPersonnelFileId).HasColumnName("related_personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.Relationship).HasColumnName("relationship").HasMaxLength(80);
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_employee_relations__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_employee_relations__tenant_file");

        builder.HasIndex(item => new { item.TenantId, item.RelatedPersonnelFileId })
            .HasDatabaseName("ix_personnel_file_employee_relations__tenant_related_file");

        builder.HasOne(item => item.RelatedPersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.RelatedPersonnelFileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_employee_relations__related_personnel_file");
    }
}

internal sealed class PersonnelFileBankAccountConfiguration : IEntityTypeConfiguration<PersonnelFileBankAccount>
{
    public void Configure(EntityTypeBuilder<PersonnelFileBankAccount> builder)
    {
        builder.ToTable("personnel_file_bank_accounts");

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_bank_accounts");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.BankCatalogItemId).HasColumnName("bank_catalog_item_id");
        builder.Property(item => item.BankCode).HasColumnName("bank_code").HasMaxLength(80);
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code").HasMaxLength(40);
        builder.Property(item => item.AccountNumber).HasColumnName("account_number").HasMaxLength(80);
        builder.Property(item => item.NormalizedAccountNumber).HasColumnName("normalized_account_number").HasMaxLength(80);
        builder.Property(item => item.AccountTypeCode).HasColumnName("account_type_code").HasMaxLength(80);
        builder.Property(item => item.IsPrimary).HasColumnName("is_primary");
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_bank_accounts__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_bank_accounts__tenant_file");

        builder.HasIndex(item => item.BankCatalogItemId)
            .HasDatabaseName("ix_personnel_file_bank_accounts__bank_catalog_item_id");

        builder.HasOne(item => item.BankCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.BankCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_bank_accounts__bank_catalog_item");
    }
}

internal sealed class PersonnelFileAssociationConfiguration : IEntityTypeConfiguration<PersonnelFileAssociation>
{
    public void Configure(EntityTypeBuilder<PersonnelFileAssociation> builder)
    {
        builder.ToTable("personnel_file_associations", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_associations__joined_left",
                "left_date is null or joined_date is null or left_date >= joined_date"));

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_associations");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.AssociationName).HasColumnName("association_name").HasMaxLength(200);
        builder.Property(item => item.Role).HasColumnName("role").HasMaxLength(120);
        builder.Property(item => item.JoinedDate).HasColumnName("joined_date");
        builder.Property(item => item.LeftDate).HasColumnName("left_date");
        builder.Property(item => item.Payment).HasColumnName("payment").HasPrecision(18, 2);
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_associations__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_associations__tenant_file");
    }
}

internal sealed class PersonnelFileEducationConfiguration : IEntityTypeConfiguration<PersonnelFileEducation>
{
    public void Configure(EntityTypeBuilder<PersonnelFileEducation> builder)
    {
        builder.ToTable("personnel_file_educations", table =>
        {
            table.HasCheckConstraint(
                "ck_personnel_file_educations__dates",
                "end_date is null or end_date >= start_date");
            table.HasCheckConstraint(
                "ck_personnel_file_educations__active_end_date",
                "is_currently_studying = true or end_date is not null");
            table.HasCheckConstraint(
                "ck_personnel_file_educations__total_subjects_non_negative",
                "total_subjects is null or total_subjects >= 0");
            table.HasCheckConstraint(
                "ck_personnel_file_educations__approved_subjects_non_negative",
                "approved_subjects is null or approved_subjects >= 0");
            table.HasCheckConstraint(
                "ck_personnel_file_educations__approved_subjects_range",
                "total_subjects is null or approved_subjects is null or approved_subjects <= total_subjects");
        });

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_educations");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.EducationStatusCatalogItemId).HasColumnName("education_status_catalog_item_id");
        builder.Property(item => item.DegreeTitle).HasColumnName("degree_title").HasMaxLength(200);
        builder.Property(item => item.EducationStudyTypeCatalogItemId).HasColumnName("education_study_type_catalog_item_id");
        builder.Property(item => item.EducationCareerCatalogItemId).HasColumnName("education_career_catalog_item_id");
        builder.Property(item => item.Institution).HasColumnName("institution").HasMaxLength(200);
        builder.Property(item => item.CountryCode).HasColumnName("country_code").HasMaxLength(80);
        builder.Property(item => item.Specialty).HasColumnName("specialty").HasMaxLength(200);
        builder.Property(item => item.IsCurrentlyStudying).HasColumnName("is_currently_studying");
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.EducationShiftCatalogItemId).HasColumnName("education_shift_catalog_item_id");
        builder.Property(item => item.EducationModalityCatalogItemId).HasColumnName("education_modality_catalog_item_id");
        builder.Property(item => item.TotalSubjects).HasColumnName("total_subjects");
        builder.Property(item => item.ApprovedSubjects).HasColumnName("approved_subjects");
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.EducationStatusCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.EducationStatusCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_educations__education_status_catalog_item");

        builder.HasOne(item => item.EducationStudyTypeCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.EducationStudyTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_educations__education_study_type_catalog_item");

        builder.HasOne(item => item.EducationCareerCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.EducationCareerCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_educations__education_career_catalog_item");

        builder.HasOne(item => item.EducationShiftCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.EducationShiftCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_educations__education_shift_catalog_item");

        builder.HasOne(item => item.EducationModalityCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.EducationModalityCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_educations__education_modality_catalog_item");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_educations__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_educations__tenant_file");
    }
}

internal sealed class PersonnelFileLanguageConfiguration : IEntityTypeConfiguration<PersonnelFileLanguage>
{
    public void Configure(EntityTypeBuilder<PersonnelFileLanguage> builder)
    {
        builder.ToTable("personnel_file_languages", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_languages__skills",
                "speaks = true or writes = true or reads = true"));

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_languages");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.LanguageCode).HasColumnName("language_code").HasMaxLength(80);
        builder.Property(item => item.LevelCode).HasColumnName("level_code").HasMaxLength(80);
        builder.Property(item => item.Speaks).HasColumnName("speaks");
        builder.Property(item => item.Writes).HasColumnName("writes");
        builder.Property(item => item.Reads).HasColumnName("reads");
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_languages__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_languages__tenant_file");
    }
}

internal sealed class PersonnelFileTrainingConfiguration : IEntityTypeConfiguration<PersonnelFileTraining>
{
    public void Configure(EntityTypeBuilder<PersonnelFileTraining> builder)
    {
        builder.ToTable("personnel_file_trainings", table =>
        {
            table.HasCheckConstraint(
                "ck_personnel_file_trainings__dates",
                "end_date is null or end_date >= start_date");
            table.HasCheckConstraint(
                "ck_personnel_file_trainings__duration",
                "duration_value > 0");
            table.HasCheckConstraint(
                "ck_personnel_file_trainings__cost_non_negative",
                "cost_amount is null or cost_amount >= 0");
            table.HasCheckConstraint(
                "ck_personnel_file_trainings__cost_currency",
                "cost_amount is null or cost_currency_code is not null");
        });

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_trainings");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TrainingName).HasColumnName("training_name").HasMaxLength(200);
        builder.Property(item => item.TrainingTypeCode).HasColumnName("training_type_code").HasMaxLength(80);
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(item => item.Topic).HasColumnName("topic").HasMaxLength(200);
        builder.Property(item => item.Institution).HasColumnName("institution").HasMaxLength(200);
        builder.Property(item => item.Instructors).HasColumnName("instructors").HasMaxLength(500);
        builder.Property(item => item.Score).HasColumnName("score").HasPrecision(10, 2);
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.IsInternal).HasColumnName("is_internal");
        builder.Property(item => item.IsLocal).HasColumnName("is_local");
        builder.Property(item => item.CountryCode).HasColumnName("country_code").HasMaxLength(80);
        builder.Property(item => item.DurationValue).HasColumnName("duration_value").HasPrecision(10, 2);
        builder.Property(item => item.DurationUnitCode).HasColumnName("duration_unit_code").HasMaxLength(80);
        builder.Property(item => item.CostAmount).HasColumnName("cost_amount").HasPrecision(18, 2);
        builder.Property(item => item.CostCurrencyCode).HasColumnName("cost_currency_code").HasMaxLength(40);
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_trainings__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_trainings__tenant_file");
    }
}

internal sealed class PersonnelFilePreviousEmploymentConfiguration : IEntityTypeConfiguration<PersonnelFilePreviousEmployment>
{
    public void Configure(EntityTypeBuilder<PersonnelFilePreviousEmployment> builder)
    {
        builder.ToTable("personnel_file_previous_employments", table =>
        {
            table.HasCheckConstraint(
                "ck_personnel_file_previous_employments__dates",
                "retirement_date is null or retirement_date >= entry_date");
            table.HasCheckConstraint(
                "ck_personnel_file_previous_employments__first_salary_non_negative",
                "first_salary_amount is null or first_salary_amount >= 0");
            table.HasCheckConstraint(
                "ck_personnel_file_previous_employments__last_salary_non_negative",
                "last_salary_amount is null or last_salary_amount >= 0");
            table.HasCheckConstraint(
                "ck_personnel_file_previous_employments__commission_non_negative",
                "average_commission_amount is null or average_commission_amount >= 0");
        });

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_previous_employments");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.Institution).HasColumnName("institution").HasMaxLength(200);
        builder.Property(item => item.Place).HasColumnName("place").HasMaxLength(200);
        builder.Property(item => item.LastPosition).HasColumnName("last_position").HasMaxLength(150);
        builder.Property(item => item.ManagerName).HasColumnName("manager_name").HasMaxLength(150);
        builder.Property(item => item.EntryDate).HasColumnName("entry_date");
        builder.Property(item => item.RetirementDate).HasColumnName("retirement_date");
        builder.Property(item => item.CompanyPhone).HasColumnName("company_phone").HasMaxLength(40);
        builder.Property(item => item.ExitReason).HasColumnName("exit_reason").HasMaxLength(500);
        builder.Property(item => item.FirstSalaryAmount).HasColumnName("first_salary_amount").HasPrecision(18, 2);
        builder.Property(item => item.LastSalaryAmount).HasColumnName("last_salary_amount").HasPrecision(18, 2);
        builder.Property(item => item.AverageCommissionAmount).HasColumnName("average_commission_amount").HasPrecision(18, 2);
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code").HasMaxLength(40);
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_previous_employments__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_previous_employments__tenant_file");
    }
}

internal sealed class PersonnelFileReferenceConfiguration : IEntityTypeConfiguration<PersonnelFileReference>
{
    public void Configure(EntityTypeBuilder<PersonnelFileReference> builder)
    {
        builder.ToTable("personnel_file_references", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_references__known_time_non_negative",
                "known_time_years >= 0"));

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_references");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.PersonName).HasColumnName("person_name").HasMaxLength(150);
        builder.Property(item => item.Address).HasColumnName("address").HasMaxLength(500);
        builder.Property(item => item.Phone).HasColumnName("phone").HasMaxLength(40);
        builder.Property(item => item.ReferenceTypeCode).HasColumnName("reference_type_code").HasMaxLength(80);
        builder.Property(item => item.Occupation).HasColumnName("occupation").HasMaxLength(120);
        builder.Property(item => item.Workplace).HasColumnName("workplace").HasMaxLength(200);
        builder.Property(item => item.WorkPhone).HasColumnName("work_phone").HasMaxLength(40);
        builder.Property(item => item.KnownTimeYears).HasColumnName("known_time_years").HasPrecision(10, 2);
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_references__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .HasDatabaseName("ix_personnel_file_references__tenant_file");
    }
}

internal sealed class PersonnelFileDocumentConfiguration : IEntityTypeConfiguration<PersonnelFileDocument>
{
    public void Configure(EntityTypeBuilder<PersonnelFileDocument> builder)
    {
        builder.ToTable("personnel_file_documents", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_documents__loan_return",
                "return_date is null or loan_date is null or return_date >= loan_date"));

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_documents");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.DocumentType).HasColumnName("document_type").HasMaxLength(100);
        builder.Property(item => item.Observations).HasColumnName("observations").HasMaxLength(2000);
        builder.Property(item => item.DeliveryDate).HasColumnName("delivery_date");
        builder.Property(item => item.LoanDate).HasColumnName("loan_date");
        builder.Property(item => item.ReturnDate).HasColumnName("return_date");
        builder.Property(item => item.BlobName).HasColumnName("blob_name").HasMaxLength(1000);
        builder.Property(item => item.BlobUrl).HasColumnName("blob_url").HasMaxLength(1000);
        builder.Property(item => item.FileName).HasColumnName("file_name").HasMaxLength(260);
        builder.Property(item => item.ContentType).HasColumnName("content_type").HasMaxLength(200);
        builder.Property(item => item.SizeBytes).HasColumnName("size_bytes");
        builder.Property(item => item.Sha256).HasColumnName("sha256").HasMaxLength(64);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_documents__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.IsActive })
            .HasDatabaseName("ix_personnel_file_documents__tenant_file_active");
    }
}

internal sealed class PersonnelFileCustomFieldDefinitionConfiguration : IEntityTypeConfiguration<PersonnelFileCustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<PersonnelFileCustomFieldDefinition> builder)
    {
        builder.ToTable("personnel_file_custom_field_definitions");

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_custom_field_definitions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.Key).HasColumnName("key").HasMaxLength(80);
        builder.Property(item => item.NormalizedKey).HasColumnName("normalized_key").HasMaxLength(80);
        builder.Property(item => item.Label).HasColumnName("label").HasMaxLength(200);
        builder.Property(item => item.FieldType)
            .HasColumnName("field_type")
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(item => item.IsRequired).HasColumnName("is_required");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.OptionsJson).HasColumnName("options_json").HasColumnType("jsonb");
        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_custom_field_definitions__public_id");

        builder.HasIndex(item => new { item.TenantId, item.NormalizedKey })
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_custom_field_definitions__tenant_key");

        builder.HasIndex(item => new { item.TenantId, item.IsActive, item.SortOrder })
            .HasDatabaseName("ix_personnel_file_custom_field_definitions__tenant_active_sort");
    }
}

internal sealed class PersonnelFileObservationConfiguration : IEntityTypeConfiguration<PersonnelFileObservation>
{
    public void Configure(EntityTypeBuilder<PersonnelFileObservation> builder)
    {
        builder.ToTable("personnel_file_observations");

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_file_observations");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.AuthorUserPublicId).HasColumnName("author_user_public_id");
        builder.Property(item => item.Note).HasColumnName("note").HasMaxLength(4000);
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_observations__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.CreatedUtc })
            .HasDatabaseName("ix_personnel_file_observations__tenant_file_created");
    }
}

