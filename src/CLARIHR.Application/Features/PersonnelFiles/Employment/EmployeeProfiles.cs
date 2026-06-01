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

public sealed record PersonnelFileEmployeeProfileResponse(
    Guid Id,
    string EmployeeCode,
    string EmploymentStatusCode,
    bool IsEmploymentActive,
    string ContractTypeCode,
    DateTime HireDate,
    string? RetirementCategoryCode,
    string? RetirementReasonCode,
    string? RetirementNotes,
    DateTime? RetirementDate,
    string? WorkdayCode,
    string? PayrollTypeCode,
    Guid? PositionSlotId,
    Guid? JobProfileId,
    Guid? OrgUnitId,
    Guid? WorkCenterId,
    Guid? CostCenterId,
    DateTime? ContractStartDate,
    DateTime? ContractEndDate,
    string? VacationConfigurationJson,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record UpdatePersonnelFileEmployeeProfileCommand(
    Guid PersonnelFileId,
    string EmployeeCode,
    string EmploymentStatusCode,
    bool IsEmploymentActive,
    string ContractTypeCode,
    DateTime HireDate,
    string? RetirementCategoryCode,
    string? RetirementReasonCode,
    string? RetirementNotes,
    DateTime? RetirementDate,
    string? WorkdayCode,
    string? PayrollTypeCode,
    Guid? PositionSlotId,
    Guid? JobProfileId,
    Guid? OrgUnitId,
    Guid? WorkCenterId,
    Guid? CostCenterId,
    DateTime? ContractStartDate,
    DateTime? ContractEndDate,
    string? VacationConfigurationJson,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileEmployeeProfileResponse>;

public sealed record GetPersonnelFileEmployeeProfileQuery(Guid PersonnelFileId)
    : IQuery<PersonnelFileEmployeeProfileResponse>;

internal sealed class UpdatePersonnelFileEmployeeProfileCommandValidator : AbstractValidator<UpdatePersonnelFileEmployeeProfileCommand>
{
    public UpdatePersonnelFileEmployeeProfileCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.EmployeeCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.EmploymentStatusCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ContractTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileEmployeeProfileQueryValidator : AbstractValidator<GetPersonnelFileEmployeeProfileQuery>
{
    public GetPersonnelFileEmployeeProfileQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class UpdatePersonnelFileEmployeeProfileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileEmployeeProfileCommand, PersonnelFileEmployeeProfileResponse>
{
    public async Task<Result<PersonnelFileEmployeeProfileResponse>> Handle(
        UpdatePersonnelFileEmployeeProfileCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageAsync<PersonnelFileEmployeeProfileResponse>(
            command.PersonnelFileId,
            Guid.Empty,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileEmployeeProfileResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        // employee-profile is a 1:1 upsert: enforce optimistic concurrency only when a profile already exists.
        var existing = await employeeRepository.GetEmployeeProfileAsync(personnelFile.PublicId, cancellationToken);
        if (existing is not null && existing.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileEmployeeProfileResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var entity = PersonnelFileEmployeeProfile.Create(
            command.EmployeeCode,
            command.EmploymentStatusCode,
            command.IsEmploymentActive,
            command.ContractTypeCode,
            command.HireDate,
            command.RetirementCategoryCode,
            command.RetirementReasonCode,
            command.RetirementNotes,
            command.RetirementDate,
            command.WorkdayCode,
            command.PayrollTypeCode,
            command.PositionSlotId,
            command.JobProfileId,
            command.OrgUnitId,
            command.WorkCenterId,
            command.CostCenterId,
            command.ContractStartDate,
            command.ContractEndDate,
            command.VacationConfigurationJson);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var response = await employeeRepository.UpsertEmployeeProfileAsync(entity, cancellationToken);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService,
                personnelFile,
                $"Updated employee profile for {personnelFile.FullName}.",
                response,
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileEmployeeProfileResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFileEmployeeProfileQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileEmployeeProfileQuery, PersonnelFileEmployeeProfileResponse>
{
    public async Task<Result<PersonnelFileEmployeeProfileResponse>> Handle(
        GetPersonnelFileEmployeeProfileQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<PersonnelFileEmployeeProfileResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetEmployeeProfileAsync(personnelFile!.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Employee profile could not be resolved for a completed employee personnel file.");

        return Result<PersonnelFileEmployeeProfileResponse>.Success(response);
    }
}

