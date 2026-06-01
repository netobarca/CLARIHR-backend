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

public sealed record PersonnelFileTrainingResponse(
    Guid TrainingPublicId,
    string TrainingName,
    string TrainingTypeCode,
    string? Description,
    string? Topic,
    string? Institution,
    string? Instructors,
    decimal? Score,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsInternal,
    bool IsLocal,
    string CountryCode,
    decimal DurationValue,
    string DurationUnitCode,
    decimal? CostAmount,
    string? CostCurrencyCode,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => TrainingPublicId;
}

public sealed record GetPersonnelFileTrainingsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileTrainingResponse>>;

public sealed record GetPersonnelFileTrainingByIdQuery(Guid PersonnelFileId, Guid TrainingPublicId)
    : IQuery<PersonnelFileTrainingResponse>;

public sealed record AddPersonnelFileTrainingCommand(
    Guid PersonnelFileId,
    TrainingInput Training)
    : ICommand<PersonnelFileTrainingResponse>;

public sealed record UpdatePersonnelFileTrainingCommand(
    Guid PersonnelFileId,
    Guid TrainingPublicId,
    TrainingInput Training,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileTrainingResponse>;

public sealed record DeletePersonnelFileTrainingCommand(
    Guid PersonnelFileId,
    Guid TrainingPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileTrainingPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileTrainingCommand(
    Guid PersonnelFileId,
    Guid TrainingPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileTrainingPatchOperation> Operations)
    : ICommand<PersonnelFileTrainingResponse>;

public sealed record TrainingInput(
    string TrainingName,
    string TrainingTypeCode,
    string? Description,
    string? Topic,
    string? Institution,
    string? Instructors,
    decimal? Score,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsInternal,
    bool IsLocal,
    string CountryCode,
    decimal DurationValue,
    string DurationUnitCode,
    decimal? CostAmount,
    string? CostCurrencyCode);

internal sealed class GetPersonnelFileTrainingsQueryValidator : AbstractValidator<GetPersonnelFileTrainingsQuery>
{
    public GetPersonnelFileTrainingsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileTrainingByIdQueryValidator : AbstractValidator<GetPersonnelFileTrainingByIdQuery>
{
    public GetPersonnelFileTrainingByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.TrainingPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileTrainingCommandValidator : AbstractValidator<AddPersonnelFileTrainingCommand>
{
    public AddPersonnelFileTrainingCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Training).SetValidator(new TrainingInputValidator());
    }
}

internal sealed class UpdatePersonnelFileTrainingCommandValidator : AbstractValidator<UpdatePersonnelFileTrainingCommand>
{
    public UpdatePersonnelFileTrainingCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.TrainingPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Training).SetValidator(new TrainingInputValidator());
    }
}

internal sealed class DeletePersonnelFileTrainingCommandValidator : AbstractValidator<DeletePersonnelFileTrainingCommand>
{
    public DeletePersonnelFileTrainingCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.TrainingPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileTrainingCommandValidator : AbstractValidator<PatchPersonnelFileTrainingCommand>
{
    public PatchPersonnelFileTrainingCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.TrainingPublicId).NotEmpty();
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

internal sealed class TrainingInputValidator : AbstractValidator<TrainingInput>
{
    public TrainingInputValidator()
    {
        RuleFor(input => input.TrainingName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.TrainingTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.Description).MaximumLength(1000);
        RuleFor(input => input.Topic).MaximumLength(200);
        RuleFor(input => input.Institution).MaximumLength(200);
        RuleFor(input => input.Instructors).MaximumLength(500);
        RuleFor(input => input.CountryCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.DurationValue).GreaterThan(0);
        RuleFor(input => input.DurationUnitCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.CostAmount).GreaterThanOrEqualTo(0).When(static input => input.CostAmount.HasValue);
        RuleFor(input => input.CostCurrencyCode)
            .NotEmpty()
            .MaximumLength(40);
        RuleFor(input => input)
            .Must(static input => !input.EndDate.HasValue || input.EndDate.Value.Date >= input.StartDate.Date)
            .WithMessage(PersonnelFileErrors.EffectiveDatesInvalid.Message);
    }
}

internal sealed class PersonnelFileTrainingPatchState
{
    public string TrainingName { get; set; } = string.Empty;
    public string TrainingTypeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Topic { get; set; }
    public string? Institution { get; set; }
    public string? Instructors { get; set; }
    public decimal? Score { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsInternal { get; set; }
    public bool IsLocal { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public decimal DurationValue { get; set; }
    public string DurationUnitCode { get; set; } = string.Empty;
    public decimal? CostAmount { get; set; }
    public string? CostCurrencyCode { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileTrainingPatchState From(PersonnelFileTrainingResponse response) =>
        new()
        {
            TrainingName = response.TrainingName,
            TrainingTypeCode = response.TrainingTypeCode,
            Description = response.Description,
            Topic = response.Topic,
            Institution = response.Institution,
            Instructors = response.Instructors,
            Score = response.Score,
            StartDate = response.StartDate,
            EndDate = response.EndDate,
            IsInternal = response.IsInternal,
            IsLocal = response.IsLocal,
            CountryCode = response.CountryCode,
            DurationValue = response.DurationValue,
            DurationUnitCode = response.DurationUnitCode,
            CostAmount = response.CostAmount,
            CostCurrencyCode = response.CostCurrencyCode
        };

    public TrainingInput ToInput() =>
        new(
            TrainingName,
            TrainingTypeCode,
            Description,
            Topic,
            Institution,
            Instructors,
            Score,
            StartDate,
            EndDate,
            IsInternal,
            IsLocal,
            CountryCode,
            DurationValue,
            DurationUnitCode,
            CostAmount,
            CostCurrencyCode);
}

