using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.PositionSlots;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record FinalizePersonnelFileResponse(
    PersonnelFileResponse PersonnelFile,
    CompanyUserResponse? User,
    DateTime? InvitationExpiresUtc);

public sealed record FinalizePersonnelFileCommand(
    Guid PersonnelFileId,
    Guid ConcurrencyToken,
    bool CreateUserAccount) : ICommand<FinalizePersonnelFileResponse>;

public sealed record FinalizePersonnelFilePreviewIssueResponse(
    string Code,
    string Message,
    string Section,
    string FieldKey,
    string NavigationKey,
    bool IsBlocking);

public sealed record FinalizePersonnelFilePreviewResponse(
    Guid PersonnelFileId,
    bool CreateUserAccount,
    bool IsEligible,
    IReadOnlyCollection<FinalizePersonnelFilePreviewIssueResponse> Issues);

public sealed record PreviewFinalizePersonnelFileQuery(
    Guid PersonnelFileId,
    bool CreateUserAccount) : IQuery<FinalizePersonnelFilePreviewResponse>;

internal sealed class FinalizePersonnelFileCommandValidator : AbstractValidator<FinalizePersonnelFileCommand>
{
    public FinalizePersonnelFileCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PreviewFinalizePersonnelFileQueryValidator : AbstractValidator<PreviewFinalizePersonnelFileQuery>
{
    public PreviewFinalizePersonnelFileQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class FinalizePersonnelFileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPositionSlotRepository positionSlotRepository,
    ICompanyUserProvisioningService companyUserProvisioningService,
    IUserRepository userRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<FinalizePersonnelFileCommand, FinalizePersonnelFileResponse>
{
    public async Task<Result<FinalizePersonnelFileResponse>> Handle(
        FinalizePersonnelFileCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await personnelFileRepository.GetByIdAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(
                await personnelFileRepository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var validation = await FinalizePersonnelFileValidationResolver.ValidateAsync(
            tenantId,
            personnelFile,
            command.CreateUserAccount,
            includeRelatedResourceTenantMismatch: true,
            authorizationService,
            positionSlotRepository,
            personnelFileRepository,
            userRepository,
            cancellationToken);
        if (!validation.IsEligible)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(validation.PrimaryError);
        }

        var before = await personnelFileRepository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file response could not be resolved before finalization.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            CompanyUserResponse? userResponse = null;
            DateTime? invitationExpiresUtc = null;
            if (command.CreateUserAccount)
            {
                var provisioningResult = await companyUserProvisioningService.ProvisionAsync(
                    new CompanyUserProvisioningRequest(
                        tenantId,
                        personnelFile.InstitutionalEmail!,
                        personnelFile.FirstName,
                        personnelFile.LastName,
                        validation.ResolvedRoleId!.Value,
                        Country: null,
                        Source: "personnel-file-finalization",
                        AllowExistingMembershipReuse: true),
                    cancellationToken);
                if (provisioningResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<FinalizePersonnelFileResponse>.Failure(provisioningResult.Error);
                }

                personnelFile.Complete(provisioningResult.Value.User.PublicId);
                userResponse = provisioningResult.Value.UserResponse;
                invitationExpiresUtc = provisioningResult.Value.InvitationExpiresUtc;
            }
            else
            {
                personnelFile.CompleteWithoutLinkedUser();
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await personnelFileRepository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file response could not be resolved after finalization.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileCompleted,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Completed personnel file {personnelFile.FullName}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<FinalizePersonnelFileResponse>.Success(
                new FinalizePersonnelFileResponse(
                    after,
                    userResponse,
                    invitationExpiresUtc));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PreviewFinalizePersonnelFileQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPositionSlotRepository positionSlotRepository,
    IUserRepository userRepository,
    ITenantContext tenantContext)
    : IQueryHandler<PreviewFinalizePersonnelFileQuery, FinalizePersonnelFilePreviewResponse>
{
    public async Task<Result<FinalizePersonnelFilePreviewResponse>> Handle(
        PreviewFinalizePersonnelFileQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<FinalizePersonnelFilePreviewResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<FinalizePersonnelFilePreviewResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await personnelFileRepository.GetByIdAsync(query.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<FinalizePersonnelFilePreviewResponse>.Failure(
                await personnelFileRepository.ExistsOutsideTenantAsync(query.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var validation = await FinalizePersonnelFileValidationResolver.ValidateAsync(
            tenantId,
            personnelFile,
            query.CreateUserAccount,
            includeRelatedResourceTenantMismatch: false,
            authorizationService,
            positionSlotRepository,
            personnelFileRepository,
            userRepository,
            cancellationToken);

        return Result<FinalizePersonnelFilePreviewResponse>.Success(
            new FinalizePersonnelFilePreviewResponse(
                personnelFile.PublicId,
                query.CreateUserAccount,
                validation.IsEligible,
                validation.Issues
                    .Select(static issue => new FinalizePersonnelFilePreviewIssueResponse(
                        issue.Error.Code,
                        issue.Error.Message,
                        issue.Section,
                        issue.FieldKey,
                        issue.NavigationKey,
                        issue.IsBlocking))
                    .ToArray()));
    }
}

internal sealed record FinalizePersonnelFileValidationResult(
    IReadOnlyCollection<FinalizePersonnelFileValidationIssue> Issues,
    Guid? ResolvedRoleId)
{
    public bool IsEligible => Issues.Count == 0;

    public Error PrimaryError => Issues.Count == 0 ? Error.None : Issues.First().Error;
}

internal sealed record FinalizePersonnelFileValidationIssue(
    Error Error,
    string Section,
    string FieldKey,
    string NavigationKey,
    bool IsBlocking = true);

internal static class FinalizePersonnelFileNavigationKeys
{
    public const string PersonnelFiles = "personnel-files";
    public const string PersonalInfo = "personal-info";
    public const string EmployeeProfile = "employee-profile";
}

internal static class FinalizePersonnelFileValidationResolver
{
    public static async Task<FinalizePersonnelFileValidationResult> ValidateAsync(
        Guid tenantId,
        PersonnelFile personnelFile,
        bool createUserAccount,
        bool includeRelatedResourceTenantMismatch,
        IPersonnelFileAuthorizationService authorizationService,
        IPositionSlotRepository positionSlotRepository,
        IPersonnelFileRepository personnelFileRepository,
        IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        var issues = new List<FinalizePersonnelFileValidationIssue>();
        if (personnelFile.RecordType != PersonnelFileRecordType.Employee)
        {
            issues.Add(CreateIssue(PersonnelFileErrors.FinalizeOnlyEmployee, "personnel-file", "recordType"));
            return new FinalizePersonnelFileValidationResult(issues, ResolvedRoleId: null);
        }

        if (personnelFile.LifecycleStatus != PersonnelFileLifecycleStatus.Draft || personnelFile.LinkedUserPublicId.HasValue)
        {
            issues.Add(CreateIssue(PersonnelFileErrors.StateRuleViolation, "personnel-file", "lifecycleStatus"));
            return new FinalizePersonnelFileValidationResult(issues, ResolvedRoleId: null);
        }

        if (string.IsNullOrWhiteSpace(personnelFile.InstitutionalEmail))
        {
            issues.Add(CreateIssue(PersonnelFileErrors.FinalizeRequiresInstitutionalEmail, "personnel-file", "institutionalEmail"));
        }

        if (!personnelFile.AssignedPositionSlotPublicId.HasValue)
        {
            issues.Add(CreateIssue(PersonnelFileErrors.FinalizeRequiresAssignedPositionSlot, "employment", "assignedPositionSlotPublicId"));
            return new FinalizePersonnelFileValidationResult(issues, ResolvedRoleId: null);
        }

        var slotPublicId = personnelFile.AssignedPositionSlotPublicId.Value;
        var slot = await positionSlotRepository.GetByIdAsync(slotPublicId, cancellationToken);
        if (slot is null)
        {
            if (includeRelatedResourceTenantMismatch &&
                await positionSlotRepository.ExistsOutsideTenantAsync(slotPublicId, cancellationToken))
            {
                issues.Add(CreateIssue(authorizationService.TenantMismatch(RbacPermissionAction.Update), "employment", "assignedPositionSlotPublicId"));
            }
            else
            {
                issues.Add(CreateIssue(PositionSlotErrors.PositionSlotNotFound, "employment", "assignedPositionSlotPublicId"));
            }

            return new FinalizePersonnelFileValidationResult(issues, ResolvedRoleId: null);
        }

        Guid? resolvedRoleId = null;
        if (createUserAccount)
        {
            var slotResponse = await positionSlotRepository.GetResponseByIdAsync(slot.PublicId, cancellationToken);
            if (slotResponse?.RoleId is null)
            {
                issues.Add(CreateIssue(PersonnelFileErrors.FinalizeRequiresPositionSlotRole, "employment", "assignedPositionSlotPublicId"));
            }
            else
            {
                resolvedRoleId = slotResponse.RoleId.Value;
            }

            if (!string.IsNullOrWhiteSpace(personnelFile.InstitutionalEmail))
            {
                var existingUser = await userRepository.GetByEmailAsync(personnelFile.InstitutionalEmail, cancellationToken);
                if (existingUser is not null)
                {
                    var linkedPersonnelFile = await personnelFileRepository.GetByLinkedUserIdAsync(tenantId, existingUser.PublicId, cancellationToken);
                    if (linkedPersonnelFile is not null && linkedPersonnelFile.PublicId != personnelFile.PublicId)
                    {
                        issues.Add(CreateIssue(PersonnelFileErrors.LinkedUserConflict, "personnel-file", "institutionalEmail"));
                    }
                }
            }
        }

        return new FinalizePersonnelFileValidationResult(issues, resolvedRoleId);
    }

    private static FinalizePersonnelFileValidationIssue CreateIssue(Error error, string section, string fieldKey) =>
        new(error, section, fieldKey, ResolveNavigationKey(section, fieldKey));

    private static string ResolveNavigationKey(string section, string fieldKey)
    {
        var normalizedFieldKey = fieldKey.Trim();
        if (normalizedFieldKey.Equals("institutionalEmail", StringComparison.OrdinalIgnoreCase))
        {
            return FinalizePersonnelFileNavigationKeys.PersonnelFiles;
        }

        if (normalizedFieldKey.Equals("assignedPositionSlotPublicId", StringComparison.OrdinalIgnoreCase))
        {
            return FinalizePersonnelFileNavigationKeys.PersonalInfo;
        }

        if (normalizedFieldKey.Equals("recordType", StringComparison.OrdinalIgnoreCase) ||
            normalizedFieldKey.Equals("lifecycleStatus", StringComparison.OrdinalIgnoreCase))
        {
            return FinalizePersonnelFileNavigationKeys.PersonnelFiles;
        }

        var normalizedSection = section.Trim();
        if (normalizedSection.Equals("personnel-file", StringComparison.OrdinalIgnoreCase))
        {
            return FinalizePersonnelFileNavigationKeys.PersonnelFiles;
        }

        return FinalizePersonnelFileNavigationKeys.EmployeeProfile;
    }
}
