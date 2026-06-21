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

public sealed record PersonnelFileEmploymentAssignmentResponse(
    Guid EmploymentAssignmentPublicId,
    string AssignmentTypeCode,
    string? ContractTypeCode,
    string? WorkdayCode,
    string? PayrollTypeCode,
    Guid? PositionSlotId,
    Guid? OrgUnitId,
    Guid? WorkCenterId,
    Guid? CostCenterId,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPrimary,
    bool IsActive,
    string? Notes,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => EmploymentAssignmentPublicId;
}

public sealed record EmploymentAssignmentInput(
    string AssignmentTypeCode,
    string? ContractTypeCode,
    string? WorkdayCode,
    string? PayrollTypeCode,
    Guid? PositionSlotId,
    Guid? OrgUnitId,
    Guid? WorkCenterId,
    Guid? CostCenterId,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPrimary,
    bool IsActive,
    string? Notes);

public sealed record AddPersonnelFileEmploymentAssignmentCommand(
    Guid PersonnelFileId,
    EmploymentAssignmentInput Item)
    : ICommand<PersonnelFileEmploymentAssignmentResponse>;

public sealed record UpdatePersonnelFileEmploymentAssignmentCommand(
    Guid PersonnelFileId,
    Guid EmploymentAssignmentPublicId,
    EmploymentAssignmentInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEmploymentAssignmentResponse>;

public sealed record DeletePersonnelFileEmploymentAssignmentCommand(
    Guid PersonnelFileId,
    Guid EmploymentAssignmentPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileEmploymentAssignmentPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileEmploymentAssignmentCommand(
    Guid PersonnelFileId,
    Guid EmploymentAssignmentPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileEmploymentAssignmentPatchOperation> Operations)
    : ICommand<PersonnelFileEmploymentAssignmentResponse>;

public sealed record GetPersonnelFileEmploymentAssignmentsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>;

public sealed record GetPersonnelFileEmploymentAssignmentByIdQuery(Guid PersonnelFileId, Guid EmploymentAssignmentPublicId)
    : IQuery<PersonnelFileEmploymentAssignmentResponse>;

internal sealed class EmploymentAssignmentInputValidator : AbstractValidator<EmploymentAssignmentInput>
{
    public EmploymentAssignmentInputValidator()
    {
        RuleFor(input => input.AssignmentTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.ContractTypeCode).MaximumLength(80).When(input => !string.IsNullOrWhiteSpace(input.ContractTypeCode));
        RuleFor(input => input.WorkdayCode).MaximumLength(80).When(input => !string.IsNullOrWhiteSpace(input.WorkdayCode));
        RuleFor(input => input.PayrollTypeCode).MaximumLength(80).When(input => !string.IsNullOrWhiteSpace(input.PayrollTypeCode));
        RuleFor(input => input.PositionSlotId).NotNull();
        RuleFor(input => input.StartDate).LessThanOrEqualTo(input => input.EndDate!.Value).When(input => input.EndDate.HasValue);
    }
}

internal sealed class AddPersonnelFileEmploymentAssignmentCommandValidator : AbstractValidator<AddPersonnelFileEmploymentAssignmentCommand>
{
    public AddPersonnelFileEmploymentAssignmentCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new EmploymentAssignmentInputValidator());
    }
}

internal sealed class UpdatePersonnelFileEmploymentAssignmentCommandValidator : AbstractValidator<UpdatePersonnelFileEmploymentAssignmentCommand>
{
    public UpdatePersonnelFileEmploymentAssignmentCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.EmploymentAssignmentPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new EmploymentAssignmentInputValidator());
    }
}

internal sealed class DeletePersonnelFileEmploymentAssignmentCommandValidator : AbstractValidator<DeletePersonnelFileEmploymentAssignmentCommand>
{
    public DeletePersonnelFileEmploymentAssignmentCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.EmploymentAssignmentPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileEmploymentAssignmentCommandValidator : AbstractValidator<PatchPersonnelFileEmploymentAssignmentCommand>
{
    public PatchPersonnelFileEmploymentAssignmentCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.EmploymentAssignmentPublicId).NotEmpty();
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

internal sealed class GetPersonnelFileEmploymentAssignmentsQueryValidator : AbstractValidator<GetPersonnelFileEmploymentAssignmentsQuery>
{
    public GetPersonnelFileEmploymentAssignmentsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEmploymentAssignmentByIdQueryValidator : AbstractValidator<GetPersonnelFileEmploymentAssignmentByIdQuery>
{
    public GetPersonnelFileEmploymentAssignmentByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.EmploymentAssignmentPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFileEmploymentAssignmentPatchState
{
    public string AssignmentTypeCode { get; set; } = string.Empty;
    public string? ContractTypeCode { get; set; }
    public string? WorkdayCode { get; set; }
    public string? PayrollTypeCode { get; set; }
    public Guid? PositionSlotId { get; set; }
    public Guid? OrgUnitId { get; set; }
    public Guid? WorkCenterId { get; set; }
    public Guid? CostCenterId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileEmploymentAssignmentPatchState From(PersonnelFileEmploymentAssignmentResponse response) =>
        new()
        {
            AssignmentTypeCode = response.AssignmentTypeCode,
            ContractTypeCode = response.ContractTypeCode,
            WorkdayCode = response.WorkdayCode,
            PayrollTypeCode = response.PayrollTypeCode,
            PositionSlotId = response.PositionSlotId,
            OrgUnitId = response.OrgUnitId,
            WorkCenterId = response.WorkCenterId,
            CostCenterId = response.CostCenterId,
            StartDate = response.StartDate,
            EndDate = response.EndDate,
            IsPrimary = response.IsPrimary,
            Notes = response.Notes,
            IsActive = response.IsActive
        };

    public EmploymentAssignmentInput ToInput() =>
        new(
            AssignmentTypeCode,
            ContractTypeCode,
            WorkdayCode,
            PayrollTypeCode,
            PositionSlotId,
            OrgUnitId,
            WorkCenterId,
            CostCenterId,
            StartDate,
            EndDate,
            IsPrimary,
            IsActive,
            Notes);
}

