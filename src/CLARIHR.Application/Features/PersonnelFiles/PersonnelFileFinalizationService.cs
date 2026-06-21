using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Outcome of the shared finalization core: the provisioned/reused company user (when an account
/// was created) and its invitation expiry.
/// </summary>
internal sealed record PersonnelFileFinalizationOutcome(
    CompanyUserResponse? User,
    DateTime? InvitationExpiresUtc);

/// <summary>
/// Single source of truth for the "provision (or reuse) the user account + transition the file to
/// Completed" step shared by the Finalize (§3.4) and Rehire (§3.3) flows. It mutates the already
/// loaded + validated <see cref="PersonnelFile"/> but performs NO persistence / transaction / audit
/// — the caller owns the unit of work, so both flows keep their own atomic guarantees. Provisioning
/// reuses an existing membership when present (<c>AllowExistingMembershipReuse: true</c>), which is
/// what lets a rehired employee regain their previous account (D-09).
/// </summary>
internal interface IPersonnelFileFinalizationService
{
    Task<Result<PersonnelFileFinalizationOutcome>> ApplyAsync(
        Guid tenantId,
        PersonnelFile personnelFile,
        bool createUserAccount,
        Guid? resolvedRoleId,
        string source,
        CancellationToken cancellationToken);
}

internal sealed class PersonnelFileFinalizationService(
    ICompanyUserProvisioningService companyUserProvisioningService)
    : IPersonnelFileFinalizationService
{
    public async Task<Result<PersonnelFileFinalizationOutcome>> ApplyAsync(
        Guid tenantId,
        PersonnelFile personnelFile,
        bool createUserAccount,
        Guid? resolvedRoleId,
        string source,
        CancellationToken cancellationToken)
    {
        if (!createUserAccount)
        {
            personnelFile.CompleteWithoutLinkedUser();
            return Result<PersonnelFileFinalizationOutcome>.Success(
                new PersonnelFileFinalizationOutcome(null, null));
        }

        var provisioningResult = await companyUserProvisioningService.ProvisionAsync(
            new CompanyUserProvisioningRequest(
                tenantId,
                personnelFile.InstitutionalEmail!,
                personnelFile.FirstName,
                personnelFile.LastName,
                resolvedRoleId!.Value,
                Country: null,
                Source: source,
                AllowExistingMembershipReuse: true),
            cancellationToken);
        if (provisioningResult.IsFailure)
        {
            return Result<PersonnelFileFinalizationOutcome>.Failure(provisioningResult.Error);
        }

        personnelFile.Complete(provisioningResult.Value.User.PublicId);
        return Result<PersonnelFileFinalizationOutcome>.Success(
            new PersonnelFileFinalizationOutcome(
                provisioningResult.Value.UserResponse,
                provisioningResult.Value.InvitationExpiresUtc));
    }
}
