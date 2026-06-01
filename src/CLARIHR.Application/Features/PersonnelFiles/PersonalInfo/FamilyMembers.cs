using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;
using FluentValidation.Results;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileFamilyMemberResponse(
    Guid FamilyMemberPublicId,
    string FirstName,
    string LastName,
    string FullName,
    string KinshipCode,
    string? Nationality,
    DateTime? BirthDate,
    PersonnelFamilyMemberSex Sex,
    string? MaritalStatus,
    string? Occupation,
    string? DocumentType,
    string? DocumentNumber,
    string? Phone,
    bool IsStudying,
    string? StudyPlace,
    string? AcademicLevel,
    bool IsBeneficiary,
    bool IsWorking,
    string? Workplace,
    string? JobTitle,
    string? WorkPhone,
    decimal? Salary,
    bool IsDeceased,
    DateTime? DeceasedDate,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => FamilyMemberPublicId;
}

public sealed record GetPersonnelFileFamilyMembersQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>;

public sealed record GetPersonnelFileFamilyMemberByIdQuery(Guid PersonnelFileId, Guid FamilyMemberPublicId)
    : IQuery<PersonnelFileFamilyMemberResponse>;

public sealed record AddPersonnelFileFamilyMemberCommand(
    Guid PersonnelFileId,
    FamilyMemberInput FamilyMember)
    : ICommand<PersonnelFileFamilyMemberResponse>;

public sealed record UpdatePersonnelFileFamilyMemberCommand(
    Guid PersonnelFileId,
    Guid FamilyMemberPublicId,
    FamilyMemberInput FamilyMember,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileFamilyMemberResponse>;

public sealed record DeletePersonnelFileFamilyMemberCommand(
    Guid PersonnelFileId,
    Guid FamilyMemberPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileFamilyMemberPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileFamilyMemberCommand(
    Guid PersonnelFileId,
    Guid FamilyMemberPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileFamilyMemberPatchOperation> Operations)
    : ICommand<PersonnelFileFamilyMemberResponse>;

public sealed record FamilyMemberInput(
    string FirstName,
    string LastName,
    string KinshipCode,
    string? Nationality,
    DateTime? BirthDate,
    PersonnelFamilyMemberSex Sex,
    string? MaritalStatus,
    string? Occupation,
    string? DocumentType,
    string? DocumentNumber,
    string? Phone,
    bool IsStudying,
    string? StudyPlace,
    string? AcademicLevel,
    bool IsBeneficiary,
    bool IsWorking,
    string? Workplace,
    string? JobTitle,
    string? WorkPhone,
    decimal? Salary,
    bool IsDeceased,
    DateTime? DeceasedDate);

internal sealed class GetPersonnelFileFamilyMembersQueryValidator : AbstractValidator<GetPersonnelFileFamilyMembersQuery>
{
    public GetPersonnelFileFamilyMembersQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileFamilyMemberByIdQueryValidator : AbstractValidator<GetPersonnelFileFamilyMemberByIdQuery>
{
    public GetPersonnelFileFamilyMemberByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.FamilyMemberPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileFamilyMemberCommandValidator : AbstractValidator<AddPersonnelFileFamilyMemberCommand>
{
    public AddPersonnelFileFamilyMemberCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.FamilyMember).SetValidator(new FamilyMemberInputValidator());
    }
}

internal sealed class UpdatePersonnelFileFamilyMemberCommandValidator : AbstractValidator<UpdatePersonnelFileFamilyMemberCommand>
{
    public UpdatePersonnelFileFamilyMemberCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.FamilyMemberPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.FamilyMember).SetValidator(new FamilyMemberInputValidator());
    }
}

internal sealed class DeletePersonnelFileFamilyMemberCommandValidator : AbstractValidator<DeletePersonnelFileFamilyMemberCommand>
{
    public DeletePersonnelFileFamilyMemberCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.FamilyMemberPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileFamilyMemberCommandValidator : AbstractValidator<PatchPersonnelFileFamilyMemberCommand>
{
    public PatchPersonnelFileFamilyMemberCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.FamilyMemberPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class FamilyMemberInputValidator : AbstractValidator<FamilyMemberInput>
{
    public FamilyMemberInputValidator()
    {
        RuleFor(input => input.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(PersonnelFileValidationRules.IsValidName)
            .WithMessage("FirstName format is invalid.");
        RuleFor(input => input.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(PersonnelFileValidationRules.IsValidName)
            .WithMessage("LastName format is invalid.");
        RuleFor(input => input.KinshipCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.DocumentType).MaximumLength(80);
        RuleFor(input => input.DocumentNumber).MaximumLength(80);
        RuleFor(input => input.Phone).MaximumLength(40);
        RuleFor(input => input.WorkPhone).MaximumLength(40);
        RuleFor(input => input.Salary).GreaterThanOrEqualTo(0).When(static input => input.Salary.HasValue);

        RuleFor(input => input)
            .Must(static input => !input.IsStudying || (!string.IsNullOrWhiteSpace(input.StudyPlace) && !string.IsNullOrWhiteSpace(input.AcademicLevel)))
            .WithMessage(PersonnelFileErrors.FamilyMemberRuleViolation.Message);

        RuleFor(input => input)
            .Must(static input => !input.IsWorking || (!string.IsNullOrWhiteSpace(input.Workplace) && !string.IsNullOrWhiteSpace(input.JobTitle)))
            .WithMessage(PersonnelFileErrors.FamilyMemberRuleViolation.Message);

        RuleFor(input => input)
            .Must(static input => !input.IsDeceased || input.DeceasedDate.HasValue)
            .WithMessage(PersonnelFileErrors.FamilyMemberRuleViolation.Message);
    }
}

internal sealed class PersonnelFileFamilyMemberPatchState
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string KinshipCode { get; set; } = string.Empty;
    public string? Nationality { get; set; }
    public DateTime? BirthDate { get; set; }
    public PersonnelFamilyMemberSex Sex { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Occupation { get; set; }
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Phone { get; set; }
    public bool IsStudying { get; set; }
    public string? StudyPlace { get; set; }
    public string? AcademicLevel { get; set; }
    public bool IsBeneficiary { get; set; }
    public bool IsWorking { get; set; }
    public string? Workplace { get; set; }
    public string? JobTitle { get; set; }
    public string? WorkPhone { get; set; }
    public decimal? Salary { get; set; }
    public bool IsDeceased { get; set; }
    public DateTime? DeceasedDate { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileFamilyMemberPatchState From(PersonnelFileFamilyMemberResponse response) =>
        new()
        {
            FirstName = response.FirstName,
            LastName = response.LastName,
            KinshipCode = response.KinshipCode,
            Nationality = response.Nationality,
            BirthDate = response.BirthDate,
            Sex = response.Sex,
            MaritalStatus = response.MaritalStatus,
            Occupation = response.Occupation,
            DocumentType = response.DocumentType,
            DocumentNumber = response.DocumentNumber,
            Phone = response.Phone,
            IsStudying = response.IsStudying,
            StudyPlace = response.StudyPlace,
            AcademicLevel = response.AcademicLevel,
            IsBeneficiary = response.IsBeneficiary,
            IsWorking = response.IsWorking,
            Workplace = response.Workplace,
            JobTitle = response.JobTitle,
            WorkPhone = response.WorkPhone,
            Salary = response.Salary,
            IsDeceased = response.IsDeceased,
            DeceasedDate = response.DeceasedDate
        };

    public FamilyMemberInput ToInput() =>
        new(
            FirstName,
            LastName,
            KinshipCode,
            Nationality,
            BirthDate,
            Sex,
            MaritalStatus,
            Occupation,
            DocumentType,
            DocumentNumber,
            Phone,
            IsStudying,
            StudyPlace,
            AcademicLevel,
            IsBeneficiary,
            IsWorking,
            Workplace,
            JobTitle,
            WorkPhone,
            Salary,
            IsDeceased,
            DeceasedDate);
}

