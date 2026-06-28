using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Authorization;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

internal sealed class PersonnelFileAuthorizationService(
    ICurrentUserService currentUserService,
    CLARIHR.Application.Abstractions.Tenancy.ITenantContext tenantContext,
    IPlanEntitlementService planEntitlementService,
    ApplicationDbContext dbContext) : IPersonnelFileAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: false, cancellationToken);

    public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureAuthorizedAsync(companyId, manageRequired: true, cancellationToken);

    public Task<Result> EnsureCanViewCompensationAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewCompensation.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanViewInsuranceAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewInsurance.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageSubstitutionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageSubstitutions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewMedicalClaimsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewMedicalClaims.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageMedicalClaimsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageMedicalClaims.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewCompetenciesAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewCompetencies.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageCompetenciesAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageCompetencies.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewOffPayrollTransactionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewOffPayrollTransactions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageOffPayrollTransactionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageOffPayrollTransactions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewEconomicAidRequestsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewEconomicAidRequests.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageEconomicAidRequestsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageEconomicAidRequests.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewCertificateRequestsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewCertificateRequests.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageCertificateRequestsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageCertificateRequests.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanManageExitInterviewFormsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageExitInterviewForms.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewExitInterviewsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewExitInterviews.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageExitInterviewsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageExitInterviews.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewReportsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewReports.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Read.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public async Task<bool> HasRehireAuthorizationAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !tenantContext.TenantId.HasValue || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return false;
        }

        if (tenantContext.TenantId.Value != companyId)
        {
            return false;
        }

        // The override requires the dedicated AuthorizeRehire grant (D-10). PersonnelFiles.Admin —
        // the regular "manage" permission a rehire analyst already holds — is deliberately excluded
        // so it never implies authorization; the IAM super-admin remains a universal fallback.
        var requiredClaims = new[]
        {
            PersonnelFilePermissionCodes.AuthorizeRehire.ToUpperInvariant(),
            PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
        };

        var normalizedClaims = currentUserService.Permissions
            .Select(static permission => permission.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (requiredClaims.Any(normalizedClaims.Contains))
        {
            return true;
        }

        if (!Guid.TryParse(currentUserService.UserId, out var currentUserPublicId))
        {
            return false;
        }

        return await TenantPermissionGrantEvaluator.HasAnyRequiredPermissionAsync(
            dbContext,
            companyId,
            currentUserPublicId,
            requiredClaims,
            cancellationToken);
    }

    public Error TenantMismatch(RbacPermissionAction action) => PersonnelFileErrors.TenantMismatch(action);

    private async Task<Result> EnsureHasAnyClaimAsync(
        Guid companyId,
        string[] requiredClaims,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !tenantContext.TenantId.HasValue || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != companyId)
        {
            return Result.Failure(PersonnelFileErrors.TenantMismatch(action));
        }

        if (!await planEntitlementService.IsModuleEnabledAsync(companyId, CommercialModuleKeys.PersonnelFiles, cancellationToken))
        {
            return Result.Failure(PersonnelFileErrors.Forbidden);
        }

        var normalizedClaims = currentUserService.Permissions
            .Select(static permission => permission.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (requiredClaims.Any(normalizedClaims.Contains))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(currentUserService.UserId, out var currentUserPublicId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        var isAuthorized = await TenantPermissionGrantEvaluator.HasAnyRequiredPermissionAsync(
            dbContext,
            companyId,
            currentUserPublicId,
            requiredClaims,
            cancellationToken);

        return isAuthorized
            ? Result.Success()
            : Result.Failure(PersonnelFileErrors.Forbidden);
    }

    private async Task<Result> EnsureAuthorizedAsync(Guid companyId, bool manageRequired, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !tenantContext.TenantId.HasValue || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (tenantContext.TenantId.Value != companyId)
        {
            return Result.Failure(PersonnelFileErrors.TenantMismatch(manageRequired ? RbacPermissionAction.Update : RbacPermissionAction.Read));
        }

        if (!await planEntitlementService.IsModuleEnabledAsync(companyId, CommercialModuleKeys.PersonnelFiles, cancellationToken))
        {
            return Result.Failure(PersonnelFileErrors.Forbidden);
        }

        var normalizedClaims = currentUserService.Permissions
            .Select(static permission => permission.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredClaims = manageRequired
            ? new[]
            {
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            }
            : new[]
            {
                PersonnelFilePermissionCodes.Read.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            };

        if (requiredClaims.Any(normalizedClaims.Contains))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(currentUserService.UserId, out var currentUserPublicId))
        {
            return Result.Failure(AuthorizationErrors.Unauthenticated);
        }

        var isAuthorized = await TenantPermissionGrantEvaluator.HasAnyRequiredPermissionAsync(
            dbContext,
            companyId,
            currentUserPublicId,
            requiredClaims,
            cancellationToken);

        return isAuthorized
            ? Result.Success()
            : Result.Failure(PersonnelFileErrors.Forbidden);
    }
}
