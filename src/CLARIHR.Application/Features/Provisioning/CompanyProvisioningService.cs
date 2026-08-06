using System.Text;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.EmployeeRelations;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.Overtime;
using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.Preferences;

namespace CLARIHR.Application.Features.Provisioning;

internal sealed class CompanyProvisioningService(
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    IUserCompanyRepository userCompanyRepository,
    IIamAdministrationRepository iamRepository,
    ILegalRepresentativeRepository legalRepresentativeRepository,
    ICountryCatalogRepository countryCatalogRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    ILocationSeedService locationSeedService,
    ILeaveTemplateSeeder leaveTemplateSeeder,
    IOvertimeTemplateSeeder overtimeTemplateSeeder,
    IWorkScheduleTemplateSeeder workScheduleTemplateSeeder,
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

        await planEntitlementService.EnsureSystemPlanDefaultsAsync(cancellationToken);

        var freePlan = await commercialPlanRepository.GetByNormalizedCodeAsync(
            ProvisioningConstants.FreePlanCode.ToUpperInvariant(),
            cancellationToken);
        if (freePlan is null || freePlan.Status != CommercialPlanStatus.Active)
        {
            return Result<ProvisionedCompanyResult>.Failure(ProvisioningErrors.DefaultPlanUnavailable);
        }

        var country = await countryCatalogRepository.GetActiveByCodeAsync(request.CountryCode, cancellationToken);
        if (country is null)
        {
            return Result<ProvisionedCompanyResult>.Failure(ProvisioningErrors.CountryNotFound(request.CountryCode));
        }

        var companyName = request.ProvisionAsInitialCompany
            ? DeriveCompanyName(request.CompanyName, user.FirstName, user.Email)
            : request.CompanyName!.Trim();

        var company = Company.Create(
            companyName,
            await GenerateUniqueSlugAsync(companyName, cancellationToken),
            user.PublicId,
            country.Code,
            country.InternalId,
            request.CompanyTypeCatalogItemId);
        companyRepository.Add(company);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var companyPreference = CompanyPreference.CreateDefault();
        companyPreference.SetTenantId(company.PublicId);
        companyPreferenceRepository.Add(companyPreference);

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
            freePlan,
            dateTimeProvider.UtcNow));

        var tenantPermissions = await EnsureOwnerPermissionCatalogAsync(company.PublicId, cancellationToken);

        var adminRole = IamRole.Create(
            ProvisioningConstants.CompanyAdminRoleName,
            "Administrador inicial del tenant.",
            isSystemRole: true);
        adminRole.SetTenantId(company.PublicId);
        adminRole.SyncPermissions(tenantPermissions);
        StampTenant(adminRole.PermissionAssignments, company.PublicId);

        var standardRole = IamRole.Create(
            ProvisioningConstants.StandardUserRoleName,
            "Rol base para usuarios del tenant.",
            isSystemRole: true);
        standardRole.SetTenantId(company.PublicId);

        iamRepository.AddRole(adminRole);
        iamRepository.AddRole(standardRole);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var iamUser = await iamRepository.FindUserByTenantAndLinkedUserPublicIdAsync(
            company.PublicId,
            user.PublicId,
            includeRoles: true,
            cancellationToken);
        if (iamUser is null)
        {
            iamUser = IamUser.CreateLinked(
                user.PublicId,
                user.FirstName,
                user.LastName,
                user.Email,
            isActive: true);
            iamUser.SetTenantId(company.PublicId);
            iamRepository.AddUser(iamUser);
        }

        iamUser.SyncRoles([adminRole]);
        StampTenant(iamUser.RoleAssignments, company.PublicId);
        userCompanyRepository.Add(UserCompanyMembership.Create(user.Id, company.Id, adminRole.Id, request.MakePrimary));

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        // A new company is seeded ONLY with what the law or the geography fixes — never with a guess about
        // how this particular company is organised. Nothing here can be deleted afterwards (these catalogs
        // expose activate/inactivate, no DELETE), so seeding a guess leaves permanent noise in every tenant
        // that does not happen to match it.
        //
        // Seeded automatically (the company cannot invent these):
        //   · country location hierarchy
        //   · incapacity types + risks and national holidays (ISSS / Código de Trabajo)
        //   · the 4 overtime types with their legal factors (Art. 168/169/171/175 CT)
        //   · the 44 h ordinary work schedule (Art. 161 CT)
        //
        // Deliberately NOT seeded — the company defines its own from scratch. There is no example to load:
        // how a company divides itself, why it authorises overtime or how it recognises and sanctions its
        // people is not something the system can guess, and none of these catalogs can be deleted afterwards
        // (they only support activate/inactivate), so a seeded guess would be permanent noise.
        //   · org-unit types and functional areas
        //   · position-description catalogs and the position category/classification tree
        //   · recognitions, disciplinary actions and their causes
        //   · overtime justification types
        //   · competency framework — a 1–5 discrete scale is as much a guess as 1–4 or A–E. The company
        //     creates its own; until it does, `GET .../competency-rating-scale` reports isConfigured = false,
        //     which is the honest answer, not a defect to paper over with a default.
        await locationSeedService.InitializeDefaultsAsync(company.PublicId, country.Code, country.Name, cancellationToken);
        _ = await leaveTemplateSeeder.ApplyTemplateAsync(company.PublicId, dateTimeProvider.UtcNow.Year, cancellationToken);
        _ = await overtimeTemplateSeeder.ApplyTemplateAsync(company.PublicId, cancellationToken);
        _ = await workScheduleTemplateSeeder.ApplyTemplateAsync(company.PublicId, cancellationToken);

        return Result<ProvisionedCompanyResult>.Success(new ProvisionedCompanyResult(
            company.PublicId,
            company.Name,
            company.Slug,
            freePlan.Code,
            company.CreatedUtc));
    }

    public async Task<Result> EnsureOwnerAdministrationAsync(
        Guid ownerUserPublicId,
        Guid companyPublicId,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByPublicIdAsync(ownerUserPublicId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(ProvisioningErrors.UserNotFound);
        }

        var company = await companyRepository.FindByPublicIdAsync(companyPublicId, cancellationToken);
        if (company is null)
        {
            return Result.Failure(ProvisioningErrors.ProvisioningFailed);
        }

        if (company.CreatedByUserPublicId != ownerUserPublicId)
        {
            return Result.Success();
        }

        var tenantPermissions = await EnsureOwnerPermissionCatalogAsync(company.PublicId, cancellationToken);
        var adminRole = await EnsureCompanyAdminRoleAsync(company.PublicId, tenantPermissions, cancellationToken);
        await EnsureOwnerIamUserAsync(user, company.PublicId, adminRole, cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
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

    private async Task<IReadOnlyList<IamPermission>> EnsureOwnerPermissionCatalogAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var expectedPermissions = OwnerPermissionCatalog.CreateDefaultOwnerPermissions(tenantId);
        var expectedCodes = expectedPermissions
            .Select(static permission => permission.NormalizedCode)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var existingPermissions = await iamRepository.GetPermissionsByTenantAndNormalizedCodesAsync(
            tenantId,
            expectedCodes,
            cancellationToken);
        var existingCodes = existingPermissions
            .Select(static permission => permission.NormalizedCode)
            .ToHashSet(StringComparer.Ordinal);
        var missingPermissions = expectedPermissions
            .Where(permission => !existingCodes.Contains(permission.NormalizedCode))
            .ToArray();

        foreach (var permission in missingPermissions)
        {
            iamRepository.AddPermission(permission);
        }

        if (missingPermissions.Length > 0)
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await iamRepository.GetPermissionsByTenantAsync(tenantId, cancellationToken);
    }

    private async Task<IamRole> EnsureCompanyAdminRoleAsync(
        Guid tenantId,
        IReadOnlyList<IamPermission> tenantPermissions,
        CancellationToken cancellationToken)
    {
        var adminRole = await iamRepository.FindSystemRoleByTenantAndNormalizedNameAsync(
            tenantId,
            Normalize(ProvisioningConstants.CompanyAdminRoleName),
            includePermissions: true,
            cancellationToken);

        if (adminRole is null)
        {
            adminRole = IamRole.Create(
                ProvisioningConstants.CompanyAdminRoleName,
                "Administrador inicial del tenant.",
                isSystemRole: true);
            adminRole.SetTenantId(tenantId);
            iamRepository.AddRole(adminRole);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        adminRole.SyncPermissions(tenantPermissions);
        StampTenant(adminRole.PermissionAssignments, tenantId);

        return adminRole;
    }

    private async Task EnsureOwnerIamUserAsync(
        CLARIHR.Domain.Auth.User user,
        Guid tenantId,
        IamRole adminRole,
        CancellationToken cancellationToken)
    {
        var iamUser = await iamRepository.FindUserByTenantAndLinkedUserPublicIdAsync(
            tenantId,
            user.PublicId,
            includeRoles: true,
            cancellationToken);
        if (iamUser is null)
        {
            iamUser = IamUser.CreateLinked(
                user.PublicId,
                user.FirstName,
                user.LastName,
                user.Email,
                isActive: true);
            iamUser.SetTenantId(tenantId);
            iamRepository.AddUser(iamUser);
        }

        iamUser.EnsureRole(adminRole);
        StampTenant(iamUser.RoleAssignments, tenantId);
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

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
