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

public sealed record PersonnelFileContractHistoryResponse(
    Guid ContractHistoryPublicId,
    string ContractTypeCode,
    DateTime ContractDate,
    DateTime? ContractEndDate,
    Guid? PositionSlotId,
    bool IsActive,
    string? Notes,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => ContractHistoryPublicId;
}

public sealed record ContractHistoryInput(
    string ContractTypeCode,
    DateTime ContractDate,
    DateTime? ContractEndDate,
    Guid? PositionSlotId,
    bool IsActive,
    string? Notes);

public sealed record AddPersonnelFileContractHistoryCommand(
    Guid PersonnelFileId,
    ContractHistoryInput Item)
    : ICommand<PersonnelFileContractHistoryResponse>;

public sealed record UpdatePersonnelFileContractHistoryCommand(
    Guid PersonnelFileId,
    Guid ContractHistoryPublicId,
    ContractHistoryInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileContractHistoryResponse>;

public sealed record PersonnelFileContractHistoryPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileContractHistoryCommand(
    Guid PersonnelFileId,
    Guid ContractHistoryPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileContractHistoryPatchOperation> Operations)
    : ICommand<PersonnelFileContractHistoryResponse>;

public sealed record GetPersonnelFileContractHistoryQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>;

public sealed record GetPersonnelFileContractHistoryByIdQuery(Guid PersonnelFileId, Guid ContractHistoryPublicId)
    : IQuery<PersonnelFileContractHistoryResponse>;

internal sealed class ContractHistoryInputValidator : AbstractValidator<ContractHistoryInput>
{
    public ContractHistoryInputValidator()
    {
        RuleFor(input => input.ContractTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.ContractDate).LessThanOrEqualTo(input => input.ContractEndDate!.Value).When(input => input.ContractEndDate.HasValue);
    }
}

internal sealed class AddPersonnelFileContractHistoryCommandValidator : AbstractValidator<AddPersonnelFileContractHistoryCommand>
{
    public AddPersonnelFileContractHistoryCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new ContractHistoryInputValidator());
    }
}

internal sealed class UpdatePersonnelFileContractHistoryCommandValidator : AbstractValidator<UpdatePersonnelFileContractHistoryCommand>
{
    public UpdatePersonnelFileContractHistoryCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ContractHistoryPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new ContractHistoryInputValidator());
    }
}

internal sealed class PatchPersonnelFileContractHistoryCommandValidator : AbstractValidator<PatchPersonnelFileContractHistoryCommand>
{
    public PatchPersonnelFileContractHistoryCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.ContractHistoryPublicId).NotEmpty();
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

internal sealed class GetPersonnelFileContractHistoryQueryValidator : AbstractValidator<GetPersonnelFileContractHistoryQuery>
{
    public GetPersonnelFileContractHistoryQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileContractHistoryByIdQueryValidator : AbstractValidator<GetPersonnelFileContractHistoryByIdQuery>
{
    public GetPersonnelFileContractHistoryByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.ContractHistoryPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFileContractHistoryPatchState
{
    public string ContractTypeCode { get; set; } = string.Empty;
    public DateTime ContractDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public Guid? PositionSlotId { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileContractHistoryPatchState From(PersonnelFileContractHistoryResponse response) =>
        new()
        {
            ContractTypeCode = response.ContractTypeCode,
            ContractDate = response.ContractDate,
            ContractEndDate = response.ContractEndDate,
            PositionSlotId = response.PositionSlotId,
            Notes = response.Notes,
            IsActive = response.IsActive
        };

    public ContractHistoryInput ToInput() =>
        new(
            ContractTypeCode,
            ContractDate,
            ContractEndDate,
            PositionSlotId,
            IsActive,
            Notes);
}

