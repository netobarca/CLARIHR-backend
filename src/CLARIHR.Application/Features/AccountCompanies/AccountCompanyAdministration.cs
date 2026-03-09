using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.AccountCompanies.Common;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.AccountCompanies;

internal sealed class GetOwnedCompaniesQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ITenantContext tenantContext)
    : IQueryHandler<GetOwnedCompaniesQuery, PagedResponse<AccountCompanySummaryResponse>>
{
    public async Task<Result<PagedResponse<AccountCompanySummaryResponse>>> Handle(
        GetOwnedCompaniesQuery query,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await AccountCompanyActorResolver.ResolveCurrentUserAsync(
            currentUserService,
            userRepository,
            cancellationToken);
        if (currentUserResult.IsFailure)
        {
            return Result<PagedResponse<AccountCompanySummaryResponse>>.Failure(currentUserResult.Error);
        }

        var companies = await companyRepository.GetOwnedByUserAsync(
            currentUserResult.Value.PublicId,
            new CompanyListFilter(query.Status, query.PageNumber, query.PageSize, tenantContext.TenantId),
            cancellationToken);

        return Result<PagedResponse<AccountCompanySummaryResponse>>.Success(companies);
    }
}

internal sealed class GetOwnedCompanyByIdQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ITenantContext tenantContext)
    : IQueryHandler<GetOwnedCompanyByIdQuery, AccountCompanyDetailResponse>
{
    public async Task<Result<AccountCompanyDetailResponse>> Handle(
        GetOwnedCompanyByIdQuery query,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await AccountCompanyActorResolver.ResolveCurrentUserAsync(
            currentUserService,
            userRepository,
            cancellationToken);
        if (currentUserResult.IsFailure)
        {
            return Result<AccountCompanyDetailResponse>.Failure(currentUserResult.Error);
        }

        var company = await companyRepository.FindOwnedByUserAsync(
            query.CompanyId,
            currentUserResult.Value.PublicId,
            tenantContext.TenantId,
            cancellationToken);
        if (company is not null)
        {
            return Result<AccountCompanyDetailResponse>.Success(company);
        }

        var existingCompany = await companyRepository.FindByPublicIdAsync(query.CompanyId, cancellationToken);
        return existingCompany is null
            ? Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.CompanyNotFound)
            : Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.OwnershipForbidden);
    }
}

internal sealed class CreateAccountCompanyCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyOwnershipPolicy companyOwnershipPolicy,
    ICompanyProvisioningService companyProvisioningService,
    IOrgStructureCatalogRepository orgStructureCatalogRepository,
    ICompanyRepository companyRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    ILogger<CreateAccountCompanyCommandHandler> logger)
    : ICommandHandler<CreateAccountCompanyCommand, AccountCompanyDetailResponse>
{
    public async Task<Result<AccountCompanyDetailResponse>> Handle(
        CreateAccountCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await AccountCompanyActorResolver.ResolveCurrentUserAsync(
            currentUserService,
            userRepository,
            cancellationToken);
        if (currentUserResult.IsFailure)
        {
            return Result<AccountCompanyDetailResponse>.Failure(currentUserResult.Error);
        }

        if (!await companyOwnershipPolicy.HasCapacityForAnotherActiveCompanyAsync(currentUserResult.Value.PublicId, cancellationToken))
        {
            return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.CompanyLimitReached);
        }

        CatalogReferenceLookup? companyType = null;
        if (command.CompanyTypeId.HasValue)
        {
            companyType = await orgStructureCatalogRepository.GetActiveCompanyTypeLookupAsync(
                currentUserResult.Value.PublicId,
                command.CompanyTypeId.Value,
                cancellationToken);
            if (companyType is null)
            {
                return Result<AccountCompanyDetailResponse>.Failure(OrgStructureCatalogErrors.CompanyTypeNotFound);
            }
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var provisioningResult = await companyProvisioningService.ProvisionAsync(
                new ProvisionCompanyRequest(
                    currentUserResult.Value.PublicId,
                    command.Name,
                    command.InitialLegalRepresentative,
                    MakePrimary: false,
                    ProvisioningConstants.FreePlanCode,
                    ProvisionAsInitialCompany: false,
                    companyType?.InternalId),
                cancellationToken);
            if (provisioningResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<AccountCompanyDetailResponse>.Failure(provisioningResult.Error);
            }

            var response = await companyRepository.FindOwnedByUserAsync(
                provisioningResult.Value.CompanyId,
                currentUserResult.Value.PublicId,
                tenantContext.TenantId,
                cancellationToken)
                ?? throw new InvalidOperationException("Company response could not be resolved after creation.");

            await auditService.LogForTenantAsync(
                provisioningResult.Value.CompanyId,
                new AuditLogEntry(
                    AuditEventTypes.CompanyCreated,
                    AuditEntityTypes.Company,
                    provisioningResult.Value.CompanyId,
                    provisioningResult.Value.Slug,
                    AuditActions.Create,
                    $"Created company {provisioningResult.Value.CompanyName}.",
                    After: response),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Account company {CompanyPublicId} created by user {UserPublicId}",
                provisioningResult.Value.CompanyId,
                currentUserResult.Value.PublicId);

            return Result<AccountCompanyDetailResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateAccountCompanyCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IOrgStructureCatalogRepository orgStructureCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateAccountCompanyCommand, AccountCompanyDetailResponse>
{
    public async Task<Result<AccountCompanyDetailResponse>> Handle(
        UpdateAccountCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await AccountCompanyActorResolver.ResolveCurrentUserAsync(
            currentUserService,
            userRepository,
            cancellationToken);
        if (currentUserResult.IsFailure)
        {
            return Result<AccountCompanyDetailResponse>.Failure(currentUserResult.Error);
        }

        var companyResult = await AccountCompanyActorResolver.ResolveOwnedCompanyAsync(
            companyRepository,
            command.CompanyId,
            currentUserResult.Value.PublicId,
            cancellationToken);
        if (companyResult.IsFailure)
        {
            return Result<AccountCompanyDetailResponse>.Failure(companyResult.Error);
        }

        var company = companyResult.Value;

        CatalogReferenceLookup? companyType = null;
        if (command.CompanyTypeId.HasValue)
        {
            companyType = await orgStructureCatalogRepository.GetActiveCompanyTypeLookupAsync(
                currentUserResult.Value.PublicId,
                command.CompanyTypeId.Value,
                cancellationToken);
            if (companyType is null)
            {
                return Result<AccountCompanyDetailResponse>.Failure(OrgStructureCatalogErrors.CompanyTypeNotFound);
            }
        }

        var before = await companyRepository.FindOwnedByUserAsync(
            company.PublicId,
            currentUserResult.Value.PublicId,
            tenantContext.TenantId,
            cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            company.Rename(command.Name);
            company.SetCompanyType(companyType?.InternalId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await companyRepository.FindOwnedByUserAsync(
                company.PublicId,
                currentUserResult.Value.PublicId,
                tenantContext.TenantId,
                cancellationToken);

            await auditService.LogForTenantAsync(
                company.PublicId,
                new AuditLogEntry(
                    AuditEventTypes.CompanyUpdated,
                    AuditEntityTypes.Company,
                    company.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Updated company {company.Name}.",
                    Before: before,
                    After: after),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<AccountCompanyDetailResponse>.Success(after!);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ArchiveAccountCompanyCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IUserCompanyRepository userCompanyRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveAccountCompanyCommand, AccountCompanyDetailResponse>
{
    public async Task<Result<AccountCompanyDetailResponse>> Handle(
        ArchiveAccountCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await AccountCompanyActorResolver.ResolveCurrentUserAsync(
            currentUserService,
            userRepository,
            cancellationToken);
        if (currentUserResult.IsFailure)
        {
            return Result<AccountCompanyDetailResponse>.Failure(currentUserResult.Error);
        }

        var companyResult = await AccountCompanyActorResolver.ResolveOwnedCompanyAsync(
            companyRepository,
            command.CompanyId,
            currentUserResult.Value.PublicId,
            cancellationToken);
        if (companyResult.IsFailure)
        {
            return Result<AccountCompanyDetailResponse>.Failure(companyResult.Error);
        }

        var company = companyResult.Value;
        if (company.Status == CompanyStatus.Archived)
        {
            return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.CompanyAlreadyArchived);
        }

        var primaryCompanyId = await userCompanyRepository.GetPrimaryCompanyPublicIdAsync(currentUserResult.Value.Id, cancellationToken);
        if ((tenantContext.TenantId.HasValue && tenantContext.TenantId.Value == company.PublicId) ||
            (primaryCompanyId.HasValue && primaryCompanyId.Value == company.PublicId))
        {
            return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.ActiveCompanyArchiveForbidden);
        }

        var before = await companyRepository.FindOwnedByUserAsync(
            company.PublicId,
            currentUserResult.Value.PublicId,
            tenantContext.TenantId,
            cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            company.Archive();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await companyRepository.FindOwnedByUserAsync(
                company.PublicId,
                currentUserResult.Value.PublicId,
                tenantContext.TenantId,
                cancellationToken);

            await auditService.LogForTenantAsync(
                company.PublicId,
                new AuditLogEntry(
                    AuditEventTypes.CompanyArchived,
                    AuditEntityTypes.Company,
                    company.PublicId,
                    company.Slug,
                    AuditActions.Archive,
                    $"Archived company {company.Name}.",
                    Before: before,
                    After: after),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<AccountCompanyDetailResponse>.Success(after!);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ReactivateAccountCompanyCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICompanyOwnershipPolicy companyOwnershipPolicy,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReactivateAccountCompanyCommand, AccountCompanyDetailResponse>
{
    public async Task<Result<AccountCompanyDetailResponse>> Handle(
        ReactivateAccountCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await AccountCompanyActorResolver.ResolveCurrentUserAsync(
            currentUserService,
            userRepository,
            cancellationToken);
        if (currentUserResult.IsFailure)
        {
            return Result<AccountCompanyDetailResponse>.Failure(currentUserResult.Error);
        }

        var companyResult = await AccountCompanyActorResolver.ResolveOwnedCompanyAsync(
            companyRepository,
            command.CompanyId,
            currentUserResult.Value.PublicId,
            cancellationToken);
        if (companyResult.IsFailure)
        {
            return Result<AccountCompanyDetailResponse>.Failure(companyResult.Error);
        }

        var company = companyResult.Value;
        if (company.Status == CompanyStatus.Active)
        {
            return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.CompanyAlreadyActive);
        }

        if (!await companyOwnershipPolicy.HasCapacityForAnotherActiveCompanyAsync(currentUserResult.Value.PublicId, cancellationToken))
        {
            return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.CompanyReactivationLimitReached);
        }

        var before = await companyRepository.FindOwnedByUserAsync(
            company.PublicId,
            currentUserResult.Value.PublicId,
            tenantContext.TenantId,
            cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            company.Reactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await companyRepository.FindOwnedByUserAsync(
                company.PublicId,
                currentUserResult.Value.PublicId,
                tenantContext.TenantId,
                cancellationToken);

            await auditService.LogForTenantAsync(
                company.PublicId,
                new AuditLogEntry(
                    AuditEventTypes.CompanyReactivated,
                    AuditEntityTypes.Company,
                    company.PublicId,
                    company.Slug,
                    AuditActions.Reactivate,
                    $"Reactivated company {company.Name}.",
                    Before: before,
                    After: after),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<AccountCompanyDetailResponse>.Success(after!);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class SwitchActiveCompanyCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IUserCompanyRepository userCompanyRepository,
    ITokenService tokenService,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SwitchActiveCompanyCommand, SwitchActiveCompanyResponse>
{
    public async Task<Result<SwitchActiveCompanyResponse>> Handle(
        SwitchActiveCompanyCommand command,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await AccountCompanyActorResolver.ResolveCurrentUserAsync(
            currentUserService,
            userRepository,
            cancellationToken);
        if (currentUserResult.IsFailure)
        {
            return Result<SwitchActiveCompanyResponse>.Failure(currentUserResult.Error);
        }

        var companyResult = await AccountCompanyActorResolver.ResolveOwnedCompanyAsync(
            companyRepository,
            command.CompanyId,
            currentUserResult.Value.PublicId,
            cancellationToken);
        if (companyResult.IsFailure)
        {
            return Result<SwitchActiveCompanyResponse>.Failure(companyResult.Error);
        }

        var company = companyResult.Value;
        if (company.Status != CompanyStatus.Active)
        {
            return Result<SwitchActiveCompanyResponse>.Failure(AccountCompanyErrors.ActiveCompanySwitchForbidden);
        }

        if (!await userCompanyRepository.HasActiveMembershipAsync(currentUserResult.Value.Id, company.PublicId, cancellationToken))
        {
            return Result<SwitchActiveCompanyResponse>.Failure(AccountCompanyErrors.ActiveCompanySwitchForbidden);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await userCompanyRepository.SetPrimaryCompanyAsync(currentUserResult.Value.Id, company.PublicId, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var tokenResult = await tokenService.GenerateForTenantAsync(currentUserResult.Value, company.PublicId, cancellationToken);
            if (tokenResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<SwitchActiveCompanyResponse>.Failure(tokenResult.Error);
            }

            await auditService.LogForTenantAsync(
                company.PublicId,
                new AuditLogEntry(
                    AuditEventTypes.ActiveCompanySwitched,
                    AuditEntityTypes.Company,
                    company.PublicId,
                    company.Slug,
                    AuditActions.Switch,
                    $"Switched active company to {company.Name}.",
                    After: new ActiveCompanyDto(company.PublicId, company.Name, company.Slug, company.Status)),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<SwitchActiveCompanyResponse>.Success(new SwitchActiveCompanyResponse(
                tokenResult.Value.AccessToken,
                tokenResult.Value.RefreshToken,
                tokenResult.Value.ExpiresIn,
                new ActiveCompanyDto(company.PublicId, company.Name, company.Slug, company.Status)));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class AccountCompanyActorResolver
{
    public static async Task<Result<User>> ResolveCurrentUserAsync(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUserService.UserId, out var currentUserPublicId))
        {
            return Result<User>.Failure(AccountCompanyErrors.InvalidCurrentUser);
        }

        var user = await userRepository.GetByPublicIdAsync(currentUserPublicId, cancellationToken);
        return user is null
            ? Result<User>.Failure(AccountCompanyErrors.UserNotFound)
            : Result<User>.Success(user);
    }

    public static async Task<Result<Company>> ResolveOwnedCompanyAsync(
        ICompanyRepository companyRepository,
        Guid companyId,
        Guid ownerUserPublicId,
        CancellationToken cancellationToken)
    {
        var company = await companyRepository.FindByPublicIdAsync(companyId, cancellationToken);
        if (company is null)
        {
            return Result<Company>.Failure(AccountCompanyErrors.CompanyNotFound);
        }

        return company.CreatedByUserPublicId == ownerUserPublicId
            ? Result<Company>.Success(company)
            : Result<Company>.Failure(AccountCompanyErrors.OwnershipForbidden);
    }
}
