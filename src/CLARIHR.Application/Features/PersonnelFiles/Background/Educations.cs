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

public sealed record PersonnelFileEducationResponse(
    Guid EducationPublicId,
    PersonnelEducationCatalogReferenceResponse Status,
    string? DegreeTitle,
    PersonnelEducationCatalogReferenceResponse StudyType,
    PersonnelEducationCatalogReferenceResponse Career,
    string Institution,
    string CountryCode,
    string? Specialty,
    bool IsCurrentlyStudying,
    DateTime StartDate,
    DateTime? EndDate,
    PersonnelEducationCatalogReferenceResponse? Shift,
    PersonnelEducationCatalogReferenceResponse? Modality,
    int? TotalSubjects,
    int? ApprovedSubjects,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => EducationPublicId;
}

public sealed record GetPersonnelFileEducationsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileEducationResponse>>;

public sealed record GetPersonnelFileEducationByIdQuery(Guid PersonnelFileId, Guid EducationPublicId)
    : IQuery<PersonnelFileEducationResponse>;

public sealed record AddPersonnelFileEducationCommand(
    Guid PersonnelFileId,
    EducationInput Education)
    : ICommand<PersonnelFileEducationResponse>;

public sealed record UpdatePersonnelFileEducationCommand(
    Guid PersonnelFileId,
    Guid EducationPublicId,
    EducationInput Education,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEducationResponse>;

public sealed record DeletePersonnelFileEducationCommand(
    Guid PersonnelFileId,
    Guid EducationPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileEducationPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileEducationCommand(
    Guid PersonnelFileId,
    Guid EducationPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileEducationPatchOperation> Operations)
    : ICommand<PersonnelFileEducationResponse>;

public sealed record EducationInput(
    Guid StatusPublicId,
    string? DegreeTitle,
    Guid StudyTypePublicId,
    Guid CareerPublicId,
    string Institution,
    string CountryCode,
    string? Specialty,
    bool IsCurrentlyStudying,
    DateTime StartDate,
    DateTime? EndDate,
    Guid? ShiftPublicId,
    Guid? ModalityPublicId,
    int? TotalSubjects,
    int? ApprovedSubjects);

internal sealed class GetPersonnelFileEducationsQueryValidator : AbstractValidator<GetPersonnelFileEducationsQuery>
{
    public GetPersonnelFileEducationsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEducationByIdQueryValidator : AbstractValidator<GetPersonnelFileEducationByIdQuery>
{
    public GetPersonnelFileEducationByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.EducationPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileEducationCommandValidator : AbstractValidator<AddPersonnelFileEducationCommand>
{
    public AddPersonnelFileEducationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Education).SetValidator(new EducationInputValidator());
    }
}

internal sealed class UpdatePersonnelFileEducationCommandValidator : AbstractValidator<UpdatePersonnelFileEducationCommand>
{
    public UpdatePersonnelFileEducationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EducationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Education).SetValidator(new EducationInputValidator());
    }
}

internal sealed class DeletePersonnelFileEducationCommandValidator : AbstractValidator<DeletePersonnelFileEducationCommand>
{
    public DeletePersonnelFileEducationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EducationPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileEducationCommandValidator : AbstractValidator<PatchPersonnelFileEducationCommand>
{
    public PatchPersonnelFileEducationCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EducationPublicId).NotEmpty();
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

internal sealed class EducationInputValidator : AbstractValidator<EducationInput>
{
    public EducationInputValidator()
    {
        RuleFor(input => input.StatusPublicId).NotEmpty();
        RuleFor(input => input.DegreeTitle).MaximumLength(200);
        RuleFor(input => input.StudyTypePublicId).NotEmpty();
        RuleFor(input => input.CareerPublicId).NotEmpty();
        RuleFor(input => input.Institution).NotEmpty().MaximumLength(200);
        RuleFor(input => input.CountryCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.Specialty).MaximumLength(200);
        RuleFor(input => input.TotalSubjects).GreaterThanOrEqualTo(0).When(static input => input.TotalSubjects.HasValue);
        RuleFor(input => input.ApprovedSubjects).GreaterThanOrEqualTo(0).When(static input => input.ApprovedSubjects.HasValue);
        RuleFor(input => input)
            .Must(static input => input.IsCurrentlyStudying || input.EndDate.HasValue)
            .WithMessage("EndDate is required when IsCurrentlyStudying is false.");
        RuleFor(input => input)
            .Must(static input => !input.EndDate.HasValue || input.EndDate.Value.Date >= input.StartDate.Date)
            .WithMessage(PersonnelFileErrors.EffectiveDatesInvalid.Message);
        RuleFor(input => input)
            .Must(static input => !input.TotalSubjects.HasValue || !input.ApprovedSubjects.HasValue || input.ApprovedSubjects.Value <= input.TotalSubjects.Value)
            .WithMessage("ApprovedSubjects cannot be greater than TotalSubjects.");
    }
}

internal sealed class PersonnelFileEducationPatchState
{
    public Guid StatusPublicId { get; set; }
    public string? DegreeTitle { get; set; }
    public Guid StudyTypePublicId { get; set; }
    public Guid CareerPublicId { get; set; }
    public string Institution { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public bool IsCurrentlyStudying { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? ShiftPublicId { get; set; }
    public Guid? ModalityPublicId { get; set; }
    public int? TotalSubjects { get; set; }
    public int? ApprovedSubjects { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileEducationPatchState From(PersonnelFileEducationResponse response) =>
        new()
        {
            StatusPublicId = response.Status.Id,
            DegreeTitle = response.DegreeTitle,
            StudyTypePublicId = response.StudyType.Id,
            CareerPublicId = response.Career.Id,
            Institution = response.Institution,
            CountryCode = response.CountryCode,
            Specialty = response.Specialty,
            IsCurrentlyStudying = response.IsCurrentlyStudying,
            StartDate = response.StartDate,
            EndDate = response.EndDate,
            ShiftPublicId = response.Shift?.Id,
            ModalityPublicId = response.Modality?.Id,
            TotalSubjects = response.TotalSubjects,
            ApprovedSubjects = response.ApprovedSubjects
        };

    public EducationInput ToInput() =>
        new(
            StatusPublicId,
            DegreeTitle,
            StudyTypePublicId,
            CareerPublicId,
            Institution,
            CountryCode,
            Specialty,
            IsCurrentlyStudying,
            StartDate,
            EndDate,
            ShiftPublicId,
            ModalityPublicId,
            TotalSubjects,
            ApprovedSubjects);
}

