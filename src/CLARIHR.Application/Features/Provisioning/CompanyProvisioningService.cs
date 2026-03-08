using System.Text;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.LegalRepresentatives;

namespace CLARIHR.Application.Features.Provisioning;

internal sealed class CompanyProvisioningService(
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    IUserCompanyRepository userCompanyRepository,
    IIamAdministrationRepository iamRepository,
    ILegalRepresentativeRepository legalRepresentativeRepository,
    ILocationSeedService locationSeedService,
    IPlanEntitlementService planEntitlementService,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : ICompanyProvisioningService
{
    public async Task<Result<ProvisionedCompanyResult>> ProvisionAsync(
        ProvisionCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByPublicIdAsync(request.OwnerUserPublicId, cancellationToken);
        if (user is null)
        {
            return Result<ProvisionedCompanyResult>.Failure(ProvisioningErrors.UserNotFound);
        }

        await planEntitlementService.EnsureFreePlanDefaultsAsync(cancellationToken);

        var companyName = request.ProvisionAsInitialCompany
            ? DeriveCompanyName(request.CompanyName, user.FirstName, user.Email)
            : request.CompanyName!.Trim();

        var company = Company.Create(
            companyName,
            await GenerateUniqueSlugAsync(companyName, cancellationToken),
            user.PublicId);
        companyRepository.Add(company);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var initialLegalRepresentative = request.InitialLegalRepresentative;
        var legalRepresentative = LegalRepresentative.Create(
            initialLegalRepresentative.FirstName,
            initialLegalRepresentative.LastName,
            initialLegalRepresentative.DocumentType,
            initialLegalRepresentative.DocumentNumber,
            initialLegalRepresentative.PositionTitle,
            initialLegalRepresentative.RepresentationType,
            initialLegalRepresentative.AuthorityDescription,
            initialLegalRepresentative.AppointmentInstrument,
            initialLegalRepresentative.AppointmentDateUtc,
            initialLegalRepresentative.EffectiveFromUtc,
            initialLegalRepresentative.EffectiveToUtc,
            initialLegalRepresentative.Email,
            initialLegalRepresentative.Phone,
            initialLegalRepresentative.IsPrimary);
        legalRepresentative.SetTenantId(company.PublicId);
        legalRepresentativeRepository.Add(legalRepresentative);

        subscriptionRepository.Add(CompanySubscription.Activate(
            company.Id,
            request.PlanCode,
            dateTimeProvider.UtcNow));

        var permissions = CreateAdminPermissions(company.PublicId);
        foreach (var permission in permissions)
        {
            iamRepository.AddPermission(permission);
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var adminRole = IamRole.Create(
            ProvisioningConstants.CompanyAdminRoleName,
            "Administrador inicial del tenant.",
            isSystemRole: true);
        adminRole.SetTenantId(company.PublicId);
        adminRole.SyncPermissions(permissions);
        StampTenant(adminRole.PermissionAssignments, company.PublicId);

        var standardRole = IamRole.Create(
            ProvisioningConstants.StandardUserRoleName,
            "Rol base para usuarios del tenant.",
            isSystemRole: true);
        standardRole.SetTenantId(company.PublicId);

        iamRepository.AddRole(adminRole);
        iamRepository.AddRole(standardRole);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var iamUser = IamUser.CreateLinked(
            user.PublicId,
            user.FirstName,
            user.LastName,
            user.Email,
            isActive: true);
        iamUser.SetTenantId(company.PublicId);
        iamUser.SyncRoles([adminRole]);
        StampTenant(iamUser.RoleAssignments, company.PublicId);

        iamRepository.AddUser(iamUser);
        userCompanyRepository.Add(UserCompanyMembership.Create(user.Id, company.Id, adminRole.Id, request.MakePrimary));

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        await locationSeedService.InitializeDefaultsAsync(company.PublicId, cancellationToken);

        return Result<ProvisionedCompanyResult>.Success(new ProvisionedCompanyResult(
            company.PublicId,
            company.Name,
            company.Slug,
            request.PlanCode,
            company.CreatedUtc));
    }

    private async Task<string> GenerateUniqueSlugAsync(string companyName, CancellationToken cancellationToken)
    {
        var baseSlug = CreateSlug(companyName);
        var candidate = baseSlug;

        for (var suffix = 2; suffix <= 100; suffix++)
        {
            if (!await companyRepository.SlugExistsAsync(candidate, cancellationToken))
            {
                return candidate;
            }

            candidate = $"{baseSlug}-{suffix}";
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(120, baseSlug.Length + 9)];
    }

    private static string DeriveCompanyName(string? requestedCompanyName, string firstName, string email)
    {
        if (!string.IsNullOrWhiteSpace(requestedCompanyName))
        {
            return requestedCompanyName.Trim();
        }

        var emailDomain = email.Split('@').Skip(1).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(emailDomain) &&
            !emailDomain.Equals("gmail.com", StringComparison.OrdinalIgnoreCase) &&
            !emailDomain.Equals("outlook.com", StringComparison.OrdinalIgnoreCase) &&
            !emailDomain.Equals("hotmail.com", StringComparison.OrdinalIgnoreCase))
        {
            var companySegment = emailDomain.Split('.').FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(companySegment))
            {
                return Capitalize(companySegment);
            }
        }

        return $"Empresa de {firstName.Trim()}";
    }

    private static IReadOnlyList<IamPermission> CreateAdminPermissions(Guid tenantId)
    {
        var permissions = ProvisioningConstants.CompanyAdminPermissions
            .Select(definition =>
            {
                var permission = IamPermission.CreateScreenAction(
                    definition.Code,
                    definition.Name,
                    definition.Description,
                    definition.Module,
                    definition.Screen,
                    definition.Action);
                permission.SetTenantId(tenantId);
                return permission;
            })
            .ToArray();

        return permissions;
    }

    private static void StampTenant<TTenantEntity>(IEnumerable<TTenantEntity> entities, Guid tenantId)
        where TTenantEntity : TenantEntity
    {
        foreach (var entity in entities)
        {
            entity.SetTenantId(tenantId);
        }
    }

    private static string Capitalize(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0
            ? "Empresa"
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    private static string CreateSlug(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(trimmed.Length);
        var previousWasSeparator = false;

        foreach (var character in trimmed)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (builder.Length == 0 || previousWasSeparator)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "company" : slug;
    }
}
