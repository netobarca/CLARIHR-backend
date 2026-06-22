using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileCompensationConceptResponse(
    Guid CompensationConceptPublicId,
    Guid? AssignedPositionPublicId,
    CompensationNature Nature,
    string ConceptTypeCode,
    DeductionClass? DeductionClass,
    CompensationCalculationType CalculationType,
    decimal Value,
    string? CalculationBaseCode,
    decimal? EmployerRate,
    decimal? ContributionCap,
    string CurrencyCode,
    string PayPeriodCode,
    string? CounterpartyName,
    string? ExternalReference,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    bool IsSystemSuggested,
    string? Notes,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => CompensationConceptPublicId;
}

public sealed record CompensationConceptInput(
    Guid? AssignedPositionPublicId,
    CompensationNature Nature,
    string ConceptTypeCode,
    DeductionClass? DeductionClass,
    CompensationCalculationType CalculationType,
    decimal Value,
    string? CalculationBaseCode,
    decimal? EmployerRate,
    decimal? ContributionCap,
    string CurrencyCode,
    string PayPeriodCode,
    string? CounterpartyName,
    string? ExternalReference,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes);

public sealed record AddPersonnelFileCompensationConceptCommand(
    Guid PersonnelFileId,
    CompensationConceptInput Item)
    : ICommand<PersonnelFileCompensationConceptResponse>;

public sealed record UpdatePersonnelFileCompensationConceptCommand(
    Guid PersonnelFileId,
    Guid CompensationConceptPublicId,
    CompensationConceptInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCompensationConceptResponse>;

public sealed record DeletePersonnelFileCompensationConceptCommand(
    Guid PersonnelFileId,
    Guid CompensationConceptPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileCompensationConceptPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileCompensationConceptCommand(
    Guid PersonnelFileId,
    Guid CompensationConceptPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileCompensationConceptPatchOperation> Operations)
    : ICommand<PersonnelFileCompensationConceptResponse>;

public sealed record GetPersonnelFileCompensationConceptsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileCompensationConceptResponse>>;

public sealed record GetPersonnelFileCompensationConceptByIdQuery(Guid PersonnelFileId, Guid CompensationConceptPublicId)
    : IQuery<PersonnelFileCompensationConceptResponse>;

internal sealed class CompensationConceptInputValidator : AbstractValidator<CompensationConceptInput>
{
    public CompensationConceptInputValidator()
    {
        RuleFor(input => input.Nature).IsInEnum();
        RuleFor(input => input.CalculationType).IsInEnum();
        RuleFor(input => input.ConceptTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.CurrencyCode).NotEmpty().MaximumLength(40);
        RuleFor(input => input.PayPeriodCode).NotEmpty().MaximumLength(40);
        RuleFor(input => input.Value).GreaterThanOrEqualTo(0);
        RuleFor(input => input.CalculationBaseCode).MaximumLength(40);
        RuleFor(input => input.CounterpartyName).MaximumLength(200);
        RuleFor(input => input.ExternalReference).MaximumLength(120);
        RuleFor(input => input.Notes).MaximumLength(2000);
        RuleFor(input => input.DeductionClass).IsInEnum().When(input => input.DeductionClass.HasValue);
        RuleFor(input => input.EmployerRate).InclusiveBetween(0, 100).When(input => input.EmployerRate.HasValue);
        RuleFor(input => input.ContributionCap).GreaterThanOrEqualTo(0).When(input => input.ContributionCap.HasValue);
    }
}

internal sealed class AddPersonnelFileCompensationConceptCommandValidator : AbstractValidator<AddPersonnelFileCompensationConceptCommand>
{
    public AddPersonnelFileCompensationConceptCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new CompensationConceptInputValidator());
    }
}

internal sealed class UpdatePersonnelFileCompensationConceptCommandValidator : AbstractValidator<UpdatePersonnelFileCompensationConceptCommand>
{
    public UpdatePersonnelFileCompensationConceptCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CompensationConceptPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new CompensationConceptInputValidator());
    }
}

internal sealed class DeletePersonnelFileCompensationConceptCommandValidator : AbstractValidator<DeletePersonnelFileCompensationConceptCommand>
{
    public DeletePersonnelFileCompensationConceptCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CompensationConceptPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileCompensationConceptCommandValidator : AbstractValidator<PatchPersonnelFileCompensationConceptCommand>
{
    public PatchPersonnelFileCompensationConceptCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CompensationConceptPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Operations).NotEmpty();
        RuleFor(c => c.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(c => c.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class GetPersonnelFileCompensationConceptsQueryValidator : AbstractValidator<GetPersonnelFileCompensationConceptsQuery>
{
    public GetPersonnelFileCompensationConceptsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileCompensationConceptByIdQueryValidator : AbstractValidator<GetPersonnelFileCompensationConceptByIdQuery>
{
    public GetPersonnelFileCompensationConceptByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.CompensationConceptPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFileCompensationConceptPatchState
{
    public Guid? AssignedPositionPublicId { get; set; }
    public CompensationNature Nature { get; set; }
    public string ConceptTypeCode { get; set; } = string.Empty;
    public DeductionClass? DeductionClass { get; set; }
    public CompensationCalculationType CalculationType { get; set; }
    public decimal Value { get; set; }
    public string? CalculationBaseCode { get; set; }
    public decimal? EmployerRate { get; set; }
    public decimal? ContributionCap { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string PayPeriodCode { get; set; } = string.Empty;
    public string? CounterpartyName { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileCompensationConceptPatchState From(PersonnelFileCompensationConceptResponse response) =>
        new()
        {
            AssignedPositionPublicId = response.AssignedPositionPublicId,
            Nature = response.Nature,
            ConceptTypeCode = response.ConceptTypeCode,
            DeductionClass = response.DeductionClass,
            CalculationType = response.CalculationType,
            Value = response.Value,
            CalculationBaseCode = response.CalculationBaseCode,
            EmployerRate = response.EmployerRate,
            ContributionCap = response.ContributionCap,
            CurrencyCode = response.CurrencyCode,
            PayPeriodCode = response.PayPeriodCode,
            CounterpartyName = response.CounterpartyName,
            ExternalReference = response.ExternalReference,
            StartDate = response.StartDate,
            EndDate = response.EndDate,
            IsActive = response.IsActive,
            Notes = response.Notes
        };

    public CompensationConceptInput ToInput() =>
        new(
            AssignedPositionPublicId,
            Nature,
            ConceptTypeCode,
            DeductionClass,
            CalculationType,
            Value,
            CalculationBaseCode,
            EmployerRate,
            ContributionCap,
            CurrencyCode,
            PayPeriodCode,
            CounterpartyName,
            ExternalReference,
            StartDate,
            EndDate,
            IsActive,
            Notes);
}
