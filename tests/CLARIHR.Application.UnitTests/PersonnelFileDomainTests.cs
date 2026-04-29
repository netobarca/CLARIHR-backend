using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

public sealed class PersonnelFileDomainTests
{
    [Fact]
    public void PersonnelFile_Create_ShouldNormalizeFullName()
    {
        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Candidate,
            "  Ana ",
            " Mendoza ",
            new DateTime(1990, 1, 1),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: null,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoUrl: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: null,
            customDataJson: null,
            identifications:
            [
                PersonnelFileIdentification.Create("DUI", "01234567-8", null, null, null, true)
            ]);

        Assert.Equal("Ana", file.FirstName);
        Assert.Equal("Mendoza", file.LastName);
        Assert.Equal("Ana Mendoza", file.FullName);
        Assert.Equal("ANA MENDOZA", file.NormalizedFullName);
    }

    [Fact]
    public void PersonnelFile_UpdatePersonalInfo_ShouldRefreshConcurrencyToken()
    {
        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Candidate,
            "Ana",
            "Mendoza",
            new DateTime(1990, 1, 1),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: null,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoUrl: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: null,
            customDataJson: null,
            identifications:
            [
                PersonnelFileIdentification.Create("DUI", "01234567-8", null, null, null, true)
            ]);

        var initialToken = file.ConcurrencyToken;

        file.UpdatePersonalInfo(
            PersonnelFileRecordType.Employee,
            "Ana",
            "Mendoza",
            new DateTime(1990, 1, 1),
            maritalStatus: "Single",
            profession: "Engineer",
            nationality: "SV",
            personalEmail: "ana@test.com",
            institutionalEmail: null,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoUrl: null,
            orgUnitPublicId: Guid.NewGuid(),
            assignedPositionSlotPublicId: Guid.NewGuid(),
            customDataJson: "{\"shirt_size\":\"M\"}");

        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFile_Create_ShouldNormalizeReferenceCatalogCodes()
    {
        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Candidate,
            "Sara",
            "Mendoza",
            new DateTime(1991, 2, 2),
            maritalStatus: " soltero_a ",
            profession: " analista_de_datos ",
            nationality: "SV",
            personalEmail: null,
            institutionalEmail: null,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: " sv ",
            birthDepartment: " san_salvador ",
            birthMunicipality: " san_salvador_centro ",
            photoUrl: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: null,
            customDataJson: null,
            identifications:
            [
                PersonnelFileIdentification.Create(" dui ", "01234567-8", null, null, null, true)
            ]);

        Assert.Equal("SOLTERO_A", file.MaritalStatus);
        Assert.Equal("ANALISTA_DE_DATOS", file.Profession);
        Assert.Equal("SV", file.BirthCountry);
        Assert.Equal("SAN_SALVADOR", file.BirthDepartment);
        Assert.Equal("SAN_SALVADOR_CENTRO", file.BirthMunicipality);
        Assert.Equal("DUI", Assert.Single(file.Identifications).IdentificationType);
    }

    [Fact]
    public void PersonnelFile_ReplaceIdentifications_ShouldRemovePreviousRows()
    {
        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Candidate,
            "Oscar",
            "Ruiz",
            new DateTime(1993, 3, 3),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: null,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoUrl: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: null,
            customDataJson: null,
            identifications:
            [
                PersonnelFileIdentification.Create("DUI", "01234567-8", null, null, null, true)
            ]);
        file.SetTenantId(Guid.NewGuid());

        file.ReplaceIdentifications(
        [
            PersonnelFileIdentification.Create("NIT", "0614-123456-101-1", null, null, null, true)
        ]);

        var replacement = Assert.Single(file.Identifications);
        Assert.Equal("NIT", replacement.IdentificationType);
        Assert.Equal("0614-123456-101-1", replacement.IdentificationNumber);
    }

    [Fact]
    public void PersonnelFile_AddIdentification_ShouldAppendRowAndRefreshConcurrencyToken()
    {
        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Candidate,
            "Oscar",
            "Ruiz",
            new DateTime(1993, 3, 3),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: null,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoUrl: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: null,
            customDataJson: null);
        var tenantId = Guid.NewGuid();
        file.SetTenantId(tenantId);
        var initialToken = file.ConcurrencyToken;

        var identification = PersonnelFileIdentification.Create("DUI", "01234567-8", null, null, null, true);

        file.AddIdentification(identification);

        var stored = Assert.Single(file.Identifications);
        Assert.Equal("DUI", stored.IdentificationType);
        Assert.Equal("01234567-8", stored.IdentificationNumber);
        Assert.Equal(tenantId, stored.TenantId);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFileIdentification_Create_WithInvalidDates_ShouldThrow()
    {
        _ = Assert.Throws<InvalidOperationException>(() => PersonnelFileIdentification.Create(
            "DUI",
            "01234567-8",
            new DateTime(2026, 1, 10),
            new DateTime(2026, 1, 9),
            issuer: null,
            isPrimary: false));
    }

    [Fact]
    public void PersonnelFile_UpdateIdentification_ShouldUpdateRowAndRefreshConcurrencyToken()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Jose", "Gomez");
        var identification = PersonnelFileIdentification.Create("DUI", "01234567-8", null, null, null, true);
        file.AddIdentification(identification);
        var initialToken = file.ConcurrencyToken;

        file.UpdateIdentification(
            identification.PublicId,
            "NIT",
            "0614-123456-101-1",
            new DateTime(2020, 1, 1),
            new DateTime(2030, 1, 1),
            "Issuer Name",
            false);

        var stored = Assert.Single(file.Identifications);
        Assert.Equal("NIT", stored.IdentificationType);
        Assert.Equal("0614-123456-101-1", stored.IdentificationNumber);
        Assert.Equal(new DateTime(2020, 1, 1), stored.IssuedDate);
        Assert.Equal(new DateTime(2030, 1, 1), stored.ExpiryDate);
        Assert.Equal("Issuer Name", stored.Issuer);
        Assert.False(stored.IsPrimary);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFile_UpdateIdentification_WhenNotFound_ShouldThrow()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Jose", "Gomez");

        Assert.Throws<InvalidOperationException>(() => file.UpdateIdentification(
            Guid.NewGuid(),
            "NIT",
            "0614-123456-101-1",
            null,
            null,
            null,
            false));
    }

    [Fact]
    public void PersonnelFile_AddAddress_ShouldAppendRowAndRefreshConcurrencyToken()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Gomez");
        var tenantId = Guid.NewGuid();
        file.SetTenantId(tenantId);
        var initialToken = file.ConcurrencyToken;
        var address = PersonnelFileAddress.Create("Calle 1", "SV", "SAN_SALVADOR", "SAN_SALVADOR_CENTRO", "1101", true);

        file.AddAddress(address);

        var stored = Assert.Single(file.Addresses);
        Assert.Equal("Calle 1", stored.AddressLine);
        Assert.Equal(tenantId, stored.TenantId);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFile_UpdateAddress_ShouldUpdateRowAndRefreshConcurrencyToken()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Gomez");
        var address = PersonnelFileAddress.Create("Calle 1", "SV", null, null, null, true);
        file.AddAddress(address);
        var initialToken = file.ConcurrencyToken;

        file.UpdateAddress(address.PublicId, "Avenida 2", "GT", "GUATEMALA", "MIXCO", "01057", false);

        var stored = Assert.Single(file.Addresses);
        Assert.Equal("Avenida 2", stored.AddressLine);
        Assert.Equal("GT", stored.Country);
        Assert.False(stored.IsCurrent);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFile_RemoveAddress_ShouldRemoveRowAndRefreshConcurrencyToken()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Gomez");
        var address = PersonnelFileAddress.Create("Calle 1", "SV", null, null, null, true);
        file.AddAddress(address);
        var initialToken = file.ConcurrencyToken;

        file.RemoveAddress(address.PublicId);

        Assert.Empty(file.Addresses);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFile_AddEmergencyContact_ShouldAppendRowAndRefreshConcurrencyToken()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Gomez");
        var tenantId = Guid.NewGuid();
        file.SetTenantId(tenantId);
        var initialToken = file.ConcurrencyToken;
        var emergencyContact = PersonnelFileEmergencyContact.Create("Maria", "Madre", "+50370000001", "Colonia", "Empresa");

        file.AddEmergencyContact(emergencyContact);

        var stored = Assert.Single(file.EmergencyContacts);
        Assert.Equal("Maria", stored.Name);
        Assert.Equal(tenantId, stored.TenantId);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFile_UpdateEmergencyContact_ShouldUpdateRowAndRefreshConcurrencyToken()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Gomez");
        var emergencyContact = PersonnelFileEmergencyContact.Create("Maria", "Madre", "+50370000001", null, null);
        file.AddEmergencyContact(emergencyContact);
        var initialToken = file.ConcurrencyToken;

        file.UpdateEmergencyContact(emergencyContact.PublicId, "Jose", "Padre", "+50370000002", "Centro", "Oficina");

        var stored = Assert.Single(file.EmergencyContacts);
        Assert.Equal("Jose", stored.Name);
        Assert.Equal("Padre", stored.Relationship);
        Assert.Equal("+50370000002", stored.Phone);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFile_RemoveEmergencyContact_ShouldRemoveRowAndRefreshConcurrencyToken()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Gomez");
        var emergencyContact = PersonnelFileEmergencyContact.Create("Maria", "Madre", "+50370000001", null, null);
        file.AddEmergencyContact(emergencyContact);
        var initialToken = file.ConcurrencyToken;

        file.RemoveEmergencyContact(emergencyContact.PublicId);

        Assert.Empty(file.EmergencyContacts);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFile_AddFamilyMember_ShouldAppendRowAndRefreshConcurrencyToken()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Gomez");
        var tenantId = Guid.NewGuid();
        file.SetTenantId(tenantId);
        var initialToken = file.ConcurrencyToken;
        var familyMember = PersonnelFileFamilyMember.Create(
            "Luis",
            "Gomez",
            "HERMANO_A",
            "SV",
            new DateTime(2000, 1, 1),
            PersonnelFamilyMemberSex.Male,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            false,
            false,
            null,
            null,
            null,
            null,
            false,
            null);

        file.AddFamilyMember(familyMember);

        var stored = Assert.Single(file.FamilyMembers);
        Assert.Equal("Luis Gomez", stored.FullName);
        Assert.Equal(tenantId, stored.TenantId);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFile_UpdateFamilyMember_ShouldUpdateRowAndRefreshConcurrencyToken()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Gomez");
        var familyMember = PersonnelFileFamilyMember.Create(
            "Luis",
            "Gomez",
            "HERMANO_A",
            "SV",
            null,
            PersonnelFamilyMemberSex.Male,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            false,
            false,
            null,
            null,
            null,
            null,
            false,
            null);
        file.AddFamilyMember(familyMember);
        var initialToken = file.ConcurrencyToken;

        file.UpdateFamilyMember(
            familyMember.PublicId,
            "Lucia",
            "Gomez",
            "HERMANA",
            "SV",
            new DateTime(2001, 2, 3),
            PersonnelFamilyMemberSex.Female,
            "SOLTERA",
            "Estudiante",
            null,
            null,
            null,
            true,
            "UES",
            "UNIVERSITARIO",
            true,
            false,
            null,
            null,
            null,
            null,
            false,
            null);

        var stored = Assert.Single(file.FamilyMembers);
        Assert.Equal("Lucia Gomez", stored.FullName);
        Assert.Equal("HERMANA", stored.KinshipCode);
        Assert.True(stored.IsStudying);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFile_RemoveFamilyMember_ShouldRemoveRowAndRefreshConcurrencyToken()
    {
        var file = CreatePersonnelFile(PersonnelFileRecordType.Candidate, "Ana", "Gomez");
        var familyMember = PersonnelFileFamilyMember.Create(
            "Luis",
            "Gomez",
            "HERMANO_A",
            null,
            null,
            PersonnelFamilyMemberSex.Male,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            false,
            false,
            null,
            null,
            null,
            null,
            false,
            null);
        file.AddFamilyMember(familyMember);
        var initialToken = file.ConcurrencyToken;

        file.RemoveFamilyMember(familyMember.PublicId);

        Assert.Empty(file.FamilyMembers);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
    }

    [Fact]
    public void PersonnelFile_Complete_ShouldSetLifecycleAndLinkedUser()
    {
        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            "Ana",
            "Mendoza",
            new DateTime(1990, 1, 1),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: "ana@clarihr.test",
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoUrl: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: Guid.NewGuid(),
            customDataJson: null);

        var linkedUserId = Guid.NewGuid();

        file.Complete(linkedUserId);

        Assert.Equal(PersonnelFileLifecycleStatus.Completed, file.LifecycleStatus);
        Assert.Equal(linkedUserId, file.LinkedUserPublicId);
        Assert.True(file.IsCompletedEmployee);
    }

    [Fact]
    public void PersonnelFile_CompleteWithoutLinkedUser_ShouldSetLifecycleAndKeepLinkedUserNull()
    {
        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            "Ana",
            "Mendoza",
            new DateTime(1990, 1, 1),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: "ana@clarihr.test",
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoUrl: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: Guid.NewGuid(),
            customDataJson: null);

        var initialToken = file.ConcurrencyToken;

        file.CompleteWithoutLinkedUser();

        Assert.Equal(PersonnelFileLifecycleStatus.Completed, file.LifecycleStatus);
        Assert.Null(file.LinkedUserPublicId);
        Assert.NotEqual(initialToken, file.ConcurrencyToken);
        Assert.True(file.IsCompletedEmployee);
    }

    [Fact]
    public void PersonnelFile_UpdatePersonalInfo_WhenCompletedChangesProvisioningFields_ShouldThrow()
    {
        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            "Ana",
            "Mendoza",
            new DateTime(1990, 1, 1),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: "ana@clarihr.test",
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoUrl: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: Guid.NewGuid(),
            customDataJson: null);
        file.Complete(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => file.UpdatePersonalInfo(
            PersonnelFileRecordType.Employee,
            "Ana",
            "Mendoza",
            new DateTime(1990, 1, 1),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: "other@clarihr.test",
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoUrl: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: Guid.NewGuid(),
            customDataJson: null));
    }

    [Fact]
    public void PersonnelFileFamilyMember_Create_WithConditionalFieldsInvalid_ShouldThrow()
    {
        _ = Assert.Throws<ArgumentException>(() => PersonnelFileFamilyMember.Create(
            firstName: "Luis",
            lastName: "Mendoza",
            kinshipCode: "HERMANO_A",
            nationality: null,
            birthDate: null,
            sex: PersonnelFamilyMemberSex.Male,
            maritalStatus: null,
            occupation: null,
            documentType: null,
            documentNumber: null,
            phone: null,
            isStudying: true,
            studyPlace: null,
            academicLevel: null,
            isBeneficiary: false,
            isWorking: false,
            workplace: null,
            jobTitle: null,
            workPhone: null,
            salary: null,
            isDeceased: false,
            deceasedDate: null));
    }

    [Fact]
    public void PersonnelFileValidationRules_ValidateCustomData_ShouldFailWhenRequiredFieldMissing()
    {
        var definitions = new[]
        {
            new PersonnelCustomFieldDefinitionResponse(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "shirt_size",
                "Shirt Size",
                PersonnelCustomFieldType.String,
                IsRequired: true,
                IsActive: true,
                OptionsJson: null,
                SortOrder: 1,
                ConcurrencyToken: Guid.NewGuid(),
                CreatedAtUtc: DateTime.UtcNow,
                ModifiedAtUtc: null)
        };

        var error = PersonnelFileValidationRules.ValidateCustomData(definitions, "{\"another\":\"value\"}");

        Assert.NotEqual(Error.None, error);
        Assert.Equal("common.validation", error.Code);
    }

    [Fact]
    public void PersonnelFileEducation_Create_WithApprovedSubjectsOverTotal_ShouldThrow()
    {
        _ = Assert.Throws<InvalidOperationException>(() => PersonnelFileEducation.Create(
            educationStatusCatalogItemId: 1,
            degreeTitle: "Degree",
            educationStudyTypeCatalogItemId: 2,
            educationCareerCatalogItemId: 3,
            institution: "University",
            countryCode: "SV",
            specialty: null,
            isCurrentlyStudying: false,
            startDate: new DateTime(2020, 1, 1),
            endDate: new DateTime(2024, 1, 1),
            educationShiftCatalogItemId: null,
            educationModalityCatalogItemId: null,
            totalSubjects: 10,
            approvedSubjects: 11));
    }

    [Fact]
    public void PersonnelFileLanguage_Create_WithoutSkills_ShouldThrow()
    {
        _ = Assert.Throws<InvalidOperationException>(() => PersonnelFileLanguage.Create(
            languageCode: "ENGLISH",
            levelCode: "ADVANCED",
            speaks: false,
            writes: false,
            reads: false));
    }

    [Fact]
    public void PersonnelFileTraining_Create_WithNegativeCost_ShouldThrow()
    {
        _ = Assert.Throws<InvalidOperationException>(() => PersonnelFileTraining.Create(
            trainingName: "Workshop",
            trainingTypeCode: "WORKSHOP",
            description: null,
            topic: null,
            institution: "Academy",
            instructors: null,
            score: null,
            startDate: new DateTime(2025, 1, 1),
            endDate: new DateTime(2025, 1, 2),
            isInternal: false,
            isLocal: true,
            countryCode: "SV",
            durationValue: 2,
            durationUnitCode: "DAY",
            costAmount: -1,
            costCurrencyCode: "USD"));
    }

    [Fact]
    public void PersonnelFilePreviousEmployment_Create_WithNegativeSalary_ShouldThrow()
    {
        _ = Assert.Throws<InvalidOperationException>(() => PersonnelFilePreviousEmployment.Create(
            institution: "Company",
            place: "San Salvador",
            lastPosition: "Analyst",
            managerName: "Boss",
            entryDate: new DateTime(2020, 1, 1),
            retirementDate: new DateTime(2021, 1, 1),
            companyPhone: "+50370000000",
            exitReason: null,
            firstSalaryAmount: -100,
            lastSalaryAmount: null,
            averageCommissionAmount: null,
            currencyCode: "USD"));
    }

    [Fact]
    public void PersonnelFileReference_Create_WithNegativeKnownTime_ShouldThrow()
    {
        _ = Assert.Throws<InvalidOperationException>(() => PersonnelFileReference.Create(
            personName: "Reference Person",
            address: null,
            phone: "+50370000000",
            referenceTypeCode: "PERSONAL",
            occupation: null,
            workplace: null,
            workPhone: null,
            knownTimeYears: -1));
    }

    private static PersonnelFile CreatePersonnelFile(PersonnelFileRecordType recordType, string firstName, string lastName)
    {
        var file = PersonnelFile.Create(
            recordType,
            firstName,
            lastName,
            new DateTime(1990, 1, 1),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: null,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoUrl: null,
            orgUnitPublicId: null,
            assignedPositionSlotPublicId: recordType == PersonnelFileRecordType.Employee ? Guid.NewGuid() : null,
            customDataJson: null);
        file.SetTenantId(Guid.NewGuid());
        return file;
    }
}
