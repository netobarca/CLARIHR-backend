using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileSalaryItemResponse(
    Guid SalaryItemPublicId,
    string IncomeTypeCode,
    string SalaryRubricCode,
    string CurrencyCode,
    string PayPeriodCode,
    decimal Amount,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => SalaryItemPublicId;
}

public sealed record SalaryItemInput(
    string IncomeTypeCode,
    string SalaryRubricCode,
    string CurrencyCode,
    string PayPeriodCode,
    decimal Amount,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive);

public sealed record AddPersonnelFileSalaryItemCommand(
    Guid PersonnelFileId,
    SalaryItemInput Item)
    : ICommand<PersonnelFileSalaryItemResponse>;

public sealed record UpdatePersonnelFileSalaryItemCommand(
    Guid PersonnelFileId,
    Guid SalaryItemPublicId,
    SalaryItemInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSalaryItemResponse>;

public sealed record DeletePersonnelFileSalaryItemCommand(
    Guid PersonnelFileId,
    Guid SalaryItemPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileSalaryItemPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileSalaryItemCommand(
    Guid PersonnelFileId,
    Guid SalaryItemPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileSalaryItemPatchOperation> Operations)
    : ICommand<PersonnelFileSalaryItemResponse>;

public sealed record GetPersonnelFileSalaryItemsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>;

public sealed record GetPersonnelFileSalaryItemByIdQuery(Guid PersonnelFileId, Guid SalaryItemPublicId)
    : IQuery<PersonnelFileSalaryItemResponse>;

internal sealed class SalaryItemInputValidator : AbstractValidator<SalaryItemInput>
{
    public SalaryItemInputValidator()
    {
        RuleFor(input => input.IncomeTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.SalaryRubricCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.CurrencyCode).NotEmpty().MaximumLength(40);
        RuleFor(input => input.PayPeriodCode).NotEmpty().MaximumLength(40);
        RuleFor(input => input.Amount).GreaterThanOrEqualTo(0);
    }
}

internal sealed class AddPersonnelFileSalaryItemCommandValidator : AbstractValidator<AddPersonnelFileSalaryItemCommand>
{
    public AddPersonnelFileSalaryItemCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new SalaryItemInputValidator());
    }
}

internal sealed class UpdatePersonnelFileSalaryItemCommandValidator : AbstractValidator<UpdatePersonnelFileSalaryItemCommand>
{
    public UpdatePersonnelFileSalaryItemCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.SalaryItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new SalaryItemInputValidator());
    }
}

internal sealed class DeletePersonnelFileSalaryItemCommandValidator : AbstractValidator<DeletePersonnelFileSalaryItemCommand>
{
    public DeletePersonnelFileSalaryItemCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.SalaryItemPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileSalaryItemCommandValidator : AbstractValidator<PatchPersonnelFileSalaryItemCommand>
{
    public PatchPersonnelFileSalaryItemCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.SalaryItemPublicId).NotEmpty();
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

internal sealed class GetPersonnelFileSalaryItemsQueryValidator : AbstractValidator<GetPersonnelFileSalaryItemsQuery>
{
    public GetPersonnelFileSalaryItemsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileSalaryItemByIdQueryValidator : AbstractValidator<GetPersonnelFileSalaryItemByIdQuery>
{
    public GetPersonnelFileSalaryItemByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.SalaryItemPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFileSalaryItemPatchState
{
    public string IncomeTypeCode { get; set; } = string.Empty;
    public string SalaryRubricCode { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string PayPeriodCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileSalaryItemPatchState From(PersonnelFileSalaryItemResponse response) =>
        new()
        {
            IncomeTypeCode = response.IncomeTypeCode,
            SalaryRubricCode = response.SalaryRubricCode,
            CurrencyCode = response.CurrencyCode,
            PayPeriodCode = response.PayPeriodCode,
            Amount = response.Amount,
            StartDate = response.StartDate,
            EndDate = response.EndDate,
            IsActive = response.IsActive
        };

    public SalaryItemInput ToInput() =>
        new(
            IncomeTypeCode,
            SalaryRubricCode,
            CurrencyCode,
            PayPeriodCode,
            Amount,
            StartDate,
            EndDate,
            IsActive);
}

