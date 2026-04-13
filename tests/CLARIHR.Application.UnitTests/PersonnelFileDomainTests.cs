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
            relationship: "Brother",
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
            statusCode: "GRADUATED",
            degreeTitle: "Degree",
            studyTypeCode: "BACHELOR",
            career: "Engineering",
            institution: "University",
            countryCode: "SV",
            specialty: null,
            isCurrentlyStudying: false,
            startDate: new DateTime(2020, 1, 1),
            endDate: new DateTime(2024, 1, 1),
            shiftCode: null,
            modalityCode: null,
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
}
