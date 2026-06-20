using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation.TestHelper;

namespace CLARIHR.Application.UnitTests;

public sealed class PersonnelFileAdministrationValidationTests
{
    [Fact]
    public void CreateValidator_WhenIdentificationsAreRemovedFromCreate_ShouldRemainValid()
    {
        var validator = new CreatePersonnelFileCommandValidator();
        var command = CreateCreateCommand(PersonnelFileRecordType.Candidate);

        var result = validator.TestValidate(command);

        Assert.DoesNotContain(result.Errors, static error => error.PropertyName == "Identifications");
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
            new EmployeeRelationInput(Guid.NewGuid(), string.Empty));

        var result = validator.TestValidate(command);

        Assert.Contains(
            result.Errors,
            static error =>
                error.PropertyName == "Relation.Relationship" &&
                error.ErrorMessage == "'Relationship' must not be empty.");
    }

    private static CreatePersonnelFileCommand CreateCreateCommand(
        PersonnelFileRecordType recordType)
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
            PhotoFilePublicId: null,
            OrgUnitId: null);
    }
}
