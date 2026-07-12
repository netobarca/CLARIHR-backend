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

    public Task<Result> EnsureCanViewRetirementsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewRetirements.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageRetirementsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageRetirements.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    // Like HasRehireAuthorizationAsync below, PersonnelFiles.Admin is deliberately excluded from the
    // authorize/revert gates (D-12/D-13 — separation of duties); the IAM super-admin remains the fallback.
    public Task<Result> EnsureCanAuthorizeRetirementAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.AuthorizeRetirement.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanRevertRetirementAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.RevertRetirement.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewSettlementsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewSettlements.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageSettlementsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageSettlements.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewRetirementInterviewTrayAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewExitInterviews.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ViewRetirements.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanViewIncapacitiesAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewIncapacities.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageIncapacitiesAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageIncapacities.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewVacationsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewVacations.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageVacationsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageVacations.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewCompensatoryTimeAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewCompensatoryTime.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageCompensatoryTimeAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageCompensatoryTime.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewRecognitionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewRecognitions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageRecognitionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageRecognitions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    // AuthorizeRecognitions deliberately EXCLUDES PersonnelFiles.Admin (separation of duties, mirrors
    // AuthorizeRetirement); only the IAM super-admin (ManageAdministration) remains a universal fallback.
    public Task<Result> EnsureCanAuthorizeRecognitionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.AuthorizeRecognitions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewDisciplinaryActionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewDisciplinaryActions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageDisciplinaryActionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageDisciplinaryActions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    // AuthorizeDisciplinaryActions deliberately EXCLUDES PersonnelFiles.Admin (separation of duties,
    // mirrors AuthorizeRetirement); only the IAM super-admin (ManageAdministration) remains a fallback.
    public Task<Result> EnsureCanAuthorizeDisciplinaryActionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.AuthorizeDisciplinaryActions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewTimeAvailabilityAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewTimeAvailability.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanViewRecurringIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewRecurringIncomes.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageRecurringIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageRecurringIncomes.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    // AuthorizeRecurringIncomes deliberately EXCLUDES PersonnelFiles.Admin (separation of duties, mirrors
    // AuthorizeRetirement); only the IAM super-admin (ManageAdministration) remains a universal fallback.
    public Task<Result> EnsureCanAuthorizeRecurringIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.AuthorizeRecurringIncomes.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewOneTimeDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewOneTimeDeductions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageOneTimeDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageOneTimeDeductions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    // AuthorizeOneTimeDeductions deliberately EXCLUDES PersonnelFiles.Admin (separation of duties); only the
    // IAM super-admin (ManageAdministration) remains a universal fallback.
    public Task<Result> EnsureCanAuthorizeOneTimeDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.AuthorizeOneTimeDeductions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewRecurringDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewRecurringDeductions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanViewIndebtednessAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewIndebtedness.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageIndebtednessParametersAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageIndebtednessParameters.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanManageRecurringDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageRecurringDeductions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    // AuthorizeRecurringDeductions deliberately EXCLUDES PersonnelFiles.Admin (separation of duties, mirrors
    // AuthorizeRecurringIncomes); only the IAM super-admin (ManageAdministration) remains a universal fallback.
    public Task<Result> EnsureCanAuthorizeRecurringDeductionsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.AuthorizeRecurringDeductions.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewOneTimeIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewOneTimeIncomes.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageOneTimeIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageOneTimeIncomes.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    // AuthorizeOneTimeIncomes deliberately EXCLUDES PersonnelFiles.Admin (separation of duties, mirrors
    // AuthorizeRetirement); only the IAM super-admin (ManageAdministration) remains a universal fallback.
    public Task<Result> EnsureCanAuthorizeOneTimeIncomesAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.AuthorizeOneTimeIncomes.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    public Task<Result> EnsureCanViewOvertimeRecordsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ViewOvertimeRecords.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Read,
            cancellationToken);

    public Task<Result> EnsureCanManageOvertimeRecordsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.ManageOvertimeRecords.ToUpperInvariant(),
                PersonnelFilePermissionCodes.Admin.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
            cancellationToken);

    // AuthorizeOvertimeRecords deliberately EXCLUDES PersonnelFiles.Admin (separation of duties, mirrors
    // AuthorizeRetirement); only the IAM super-admin (ManageAdministration) remains a universal fallback.
    public Task<Result> EnsureCanAuthorizeOvertimeRecordsAsync(Guid companyId, CancellationToken cancellationToken) =>
        EnsureHasAnyClaimAsync(
            companyId,
            new[]
            {
                PersonnelFilePermissionCodes.AuthorizeOvertimeRecords.ToUpperInvariant(),
                PersonnelFilePermissionCodes.ManageAdministration.ToUpperInvariant()
            },
            RbacPermissionAction.Update,
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
