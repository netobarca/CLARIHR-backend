using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation.TestHelper;

namespace CLARIHR.Application.UnitTests;

public sealed class PersonnelFileAdministrationValidationTests
{
    [Fact]
    public void CreateValidator_WhenEmployeeMissingAssignedPositionSlot_ShouldAttachErrorToPublicFieldKey()
    {
        var validator = new CreatePersonnelFileCommandValidator();
        var command = CreateCreateCommand(PersonnelFileRecordType.Employee, assignedPositionSlotId: null);

        var result = validator.TestValidate(command);

        Assert.Contains(
            result.Errors,
            static error =>
                error.PropertyName == "assignedPositionSlotPublicId" &&
                error.ErrorMessage == "AssignedPositionSlotPublicId is required for employee personnel files.");
        Assert.DoesNotContain(result.Errors, static error => string.IsNullOrWhiteSpace(error.PropertyName));
    }

    [Fact]
    public void CreateValidator_WhenCandidateProvidesAssignedPositionSlot_ShouldAttachErrorToPublicFieldKey()
    {
        var validator = new CreatePersonnelFileCommandValidator();
        var command = CreateCreateCommand(PersonnelFileRecordType.Candidate, assignedPositionSlotId: Guid.NewGuid());

        var result = validator.TestValidate(command);

        Assert.Contains(
            result.Errors,
            static error =>
                error.PropertyName == "assignedPositionSlotPublicId" &&
                error.ErrorMessage == "AssignedPositionSlotPublicId is not allowed for candidate personnel files.");
        Assert.DoesNotContain(result.Errors, static error => string.IsNullOrWhiteSpace(error.PropertyName));
    }

    [Fact]
    public void CreateValidator_WhenIdentificationsAreRemovedFromCreate_ShouldRemainValid()
    {
        var validator = new CreatePersonnelFileCommandValidator();
        var command = CreateCreateCommand(PersonnelFileRecordType.Candidate, assignedPositionSlotId: null);

        var result = validator.TestValidate(command);

        Assert.DoesNotContain(result.Errors, static error => error.PropertyName == "Identifications");
    }

    [Fact]
    public void UpdatePersonalInfoValidator_WhenEmployeeMissingAssignedPositionSlot_ShouldAttachErrorToPublicFieldKey()
    {
        var validator = new UpdatePersonnelFilePersonalInfoCommandValidator();
        var command = CreateUpdateCommand(PersonnelFileRecordType.Employee, assignedPositionSlotId: null);

        var result = validator.TestValidate(command);

        Assert.Contains(
            result.Errors,
            static error =>
                error.PropertyName == "assignedPositionSlotPublicId" &&
                error.ErrorMessage == "AssignedPositionSlotPublicId is required for employee personnel files.");
        Assert.DoesNotContain(result.Errors, static error => string.IsNullOrWhiteSpace(error.PropertyName));
    }

    [Fact]
    public void EmployeeRelationInputValidator_WhenRelatedEmployeePublicIdIsEmpty_ShouldAttachError()
    {
        var validator = new EmployeeRelationInputValidator();

        var result = validator.TestValidate(new EmployeeRelationInput(Guid.Empty, "Sibling"));

        Assert.Contains(
            result.Errors,
            static error => error.PropertyName == "RelatedEmployeePublicId");
    }

    [Fact]
    public void AddEmployeeRelationValidator_WhenRelationshipIsMissing_ShouldAttachFieldError()
    {
        var validator = new AddPersonnelFileEmployeeRelationCommandValidator();
        var command = new AddPersonnelFileEmployeeRelationCommand(
            Guid.NewGuid(),
            new EmployeeRelationInput(Guid.NewGuid(), string.Empty),
            Guid.NewGuid());

        var result = validator.TestValidate(command);

        Assert.Contains(
            result.Errors,
            static error =>
                error.PropertyName == "Relation.Relationship" &&
                error.ErrorMessage == "'Relationship' must not be empty.");
    }

    private static CreatePersonnelFileCommand CreateCreateCommand(
        PersonnelFileRecordType recordType,
        Guid? assignedPositionSlotId)
    {
        return new CreatePersonnelFileCommand(
            CompanyId: Guid.NewGuid(),
            RecordType: recordType,
            FirstName: "ElNombre",
            LastName: "ElApellido",
            BirthDate: new DateTime(1990, 1, 1),
            MaritalStatusCode: null,
            ProfessionCode: null,
            Nationality: null,
            PersonalEmail: null,
            InstitutionalEmail: null,
            PersonalPhone: null,
            InstitutionalPhone: null,
            BirthCountryCode: null,
            BirthDepartmentCode: null,
            BirthMunicipalityCode: null,
            PhotoUrl: null,
            OrgUnitId: null,
            AssignedPositionSlotId: assignedPositionSlotId);
    }

    private static UpdatePersonnelFilePersonalInfoCommand CreateUpdateCommand(
        PersonnelFileRecordType recordType,
        Guid? assignedPositionSlotId)
    {
        return new UpdatePersonnelFilePersonalInfoCommand(
            PersonnelFileId: Guid.NewGuid(),
            RecordType: recordType,
            FirstName: "ElNombre",
            LastName: "ElApellido",
            BirthDate: new DateTime(1990, 1, 1),
            MaritalStatusCode: null,
            ProfessionCode: null,
            Nationality: null,
            PersonalEmail: null,
            InstitutionalEmail: null,
            PersonalPhone: null,
            InstitutionalPhone: null,
            BirthCountryCode: null,
            BirthDepartmentCode: null,
            BirthMunicipalityCode: null,
            PhotoUrl: null,
            OrgUnitId: null,
            AssignedPositionSlotId: assignedPositionSlotId,
            ConcurrencyToken: Guid.NewGuid());
    }
}
