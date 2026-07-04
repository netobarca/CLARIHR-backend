using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Authorizer resolution of a retirement request (RF-004): AUTORIZADA (note optional) or RECHAZADA (note
/// mandatory — RN-004.3). Requires the dedicated AuthorizeRetirement grant (Admin excluded, D-12) and both
/// separation-of-duties rules (D-13, ratified): the SUBJECT employee and the REQUESTER cannot authorize.
/// </summary>
public sealed record ResolveRetirementRequestCommand(
    Guid PersonnelFileId,
    Guid RetirementRequestPublicId,
    string TargetStatusCode,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileRetirementRequestResponse>;

/// <summary>
/// Authorizer annulment of an AUTORIZADA request (RN-005.1): terminal ANULADA + archives the employee's
/// pending exit-interview submissions (RN-005.3, ratified clarification #3 — draft AND submitted, the baja
/// "did not happen"). Annulment of a SOLICITADA is the manager's <c>PATCH …/cancel</c>.
/// </summary>
public sealed record AnnulAuthorizedRetirementRequestCommand(
    Guid PersonnelFileId,
    Guid RetirementRequestPublicId,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileRetirementRequestResponse>;

internal sealed class ResolveRetirementRequestCommandValidator : AbstractValidator<ResolveRetirementRequestCommand>
{
    public ResolveRetirementRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.RetirementRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.TargetStatusCode).NotEmpty().MaximumLength(80);
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}

internal sealed class AnnulAuthorizedRetirementRequestCommandValidator : AbstractValidator<AnnulAuthorizedRetirementRequestCommand>
{
    public AnnulAuthorizedRetirementRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.RetirementRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}

/// <summary>Shared separation-of-duties checks for the authorizer actions (D-13, both rules ratified).</summary>
internal static class RetirementAuthorizerGuards
{
    public static async Task<Error?> CheckAsync(
        PersonnelFile personnelFile,
        PersonnelFileRetirementRequest request,
        Guid actingUserId,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        // The subject employee never authorizes/annuls their own retirement.
        if (personnelFile.LinkedUserPublicId is { } subjectUserId && subjectUserId == actingUserId)
        {
            return RetirementErrors.SelfActionForbidden;
        }

        // The requester never authorizes the retirement they asked for (ratified hardening of D-13). When
        // the habitual authorizer is the one retiring, their superior authorizes. The rule is evaluated over
        // login identities: a requester file without a linked login cannot trip it (documented behavior).
        var requester = await employeeRepository.GetRetirementRequesterLookupAsync(
            request.RequesterFilePublicId, personnelFile.TenantId, cancellationToken);
        if (requester?.LinkedUserPublicId is { } requesterUserId && requesterUserId == actingUserId)
        {
            return RetirementErrors.RequesterCannotAuthorize;
        }

        return null;
    }
}

internal sealed class ResolveRetirementRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ResolveRetirementRequestCommand, PersonnelFileRetirementRequestResponse>
{
    public async Task<Result<PersonnelFileRetirementRequestResponse>> Handle(
        ResolveRetirementRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeRetirementAsync<PersonnelFileRetirementRequestResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetRetirementRequestEntityAsync(
            personnelFile!.PublicId, command.RetirementRequestPublicId, personnelFile.TenantId, includeClosedRecords: false, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.RequestStatusCode != RetirementRequestStatuses.Solicitada)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.StateRuleViolation);
        }

        var normalizedTarget = command.TargetStatusCode.Trim().ToUpperInvariant();
        if (!RetirementRequestStatuses.ResolutionTargets.Contains(normalizedTarget))
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.ResolutionTargetInvalid);
        }

        if (normalizedTarget == RetirementRequestStatuses.Rechazada && string.IsNullOrWhiteSpace(command.Notes))
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.ResolutionNotesRequired);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var decidedByUserId);
        var guardError = await RetirementAuthorizerGuards.CheckAsync(personnelFile, entity, decidedByUserId, employeeRepository, cancellationToken);
        if (guardError is not null)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(guardError);
        }

        // RN-004.4: authorizing re-verifies the employee is still eligible (active and not retired by then).
        if (normalizedTarget == RetirementRequestStatuses.Autorizada)
        {
            var profile = await employeeRepository.GetEmployeeProfileAsync(personnelFile.PublicId, cancellationToken);
            if (profile is null
                || !RetirementRequestRules.IsEligibleForRequest(personnelFile.IsCompletedEmployee, personnelFile.IsActive, profile.RetirementDate))
            {
                return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.EmployeeNotEligible);
            }
        }

        entity.Resolve(normalizedTarget, decidedByUserId, dateTimeProvider.UtcNow, command.Notes);
        var response = RetirementRequestMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            var summary = normalizedTarget == RetirementRequestStatuses.Autorizada
                ? $"Authorized retirement request for {personnelFile.FullName}."
                : $"Rejected retirement request for {personnelFile.FullName}.";
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, summary, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileRetirementRequestResponse>.Success(response);
    }
}

internal sealed class AnnulAuthorizedRetirementRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IExitInterviewRepository exitInterviewRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulAuthorizedRetirementRequestCommand, PersonnelFileRetirementRequestResponse>
{
    public async Task<Result<PersonnelFileRetirementRequestResponse>> Handle(
        AnnulAuthorizedRetirementRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeRetirementAsync<PersonnelFileRetirementRequestResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetRetirementRequestEntityAsync(
            personnelFile!.PublicId, command.RetirementRequestPublicId, personnelFile.TenantId, includeClosedRecords: false, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.RequestStatusCode != RetirementRequestStatuses.Autorizada)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var canceledByUserId);
        var guardError = await RetirementAuthorizerGuards.CheckAsync(personnelFile, entity, canceledByUserId, employeeRepository, cancellationToken);
        if (guardError is not null)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(guardError);
        }

        entity.Cancel(canceledByUserId, dateTimeProvider.UtcNow, command.Notes);
        var response = RetirementRequestMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // RN-005.3 (ratified #3): the baja will not happen — archive ALL of the employee's non-archived
            // exit-interview submissions (draft and submitted), same bulk mechanism the rehire uses.
            _ = await exitInterviewRepository.ArchiveSubmissionsForFileAsync(personnelFile.TenantId, personnelFile.Id, cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Annulled authorized retirement request for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileRetirementRequestResponse>.Success(response);
    }
}
