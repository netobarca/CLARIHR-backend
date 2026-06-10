using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.AccountCompanies.Common;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.AccountCompanies;

internal sealed class GetOwnedCompaniesQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ITenantContext tenantContext,
    ICompanyOwnershipPolicy companyOwnershipPolicy,
    IUserCompanyRepository userCompanyRepository,
    IResourceActionPolicyService resourceActionPolicyService)
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

        if (query.IncludeAllowedActions)
        {
            var primaryCompanyId = await userCompanyRepository.GetPrimaryCompanyPublicIdAsync(currentUserResult.Value.Id, cancellationToken);
            var hasCapacityForAnotherActiveCompany = await companyOwnershipPolicy.HasCapacityForAnotherActiveCompanyAsync(
                currentUserResult.Value.PublicId,
                cancellationToken);

            var enrichedItems = companies.Items
                .Select(company =>
                {
                    var canArchive = company.Status != CompanyStatus.Archived &&
                                     !company.IsActiveContext &&
                                     (!primaryCompanyId.HasValue || primaryCompanyId.Value != company.PublicId);
                    var canReactivate = company.Status != CompanyStatus.Active && hasCapacityForAnotherActiveCompany;

                    return company with
                    {
                        AllowedActions = resourceActionPolicyService.Evaluate(
                            new ResourceActionContext(
                                ResourceKey: "AccountCompanies",
                                State: company.Status.ToString(),
                                IsActive: company.Status == CompanyStatus.Active,
                                SupportsEdit: true,
                                EditAllowed: company.IsOwnedByCurrentUser,
                                SupportsArchive: true,
                                ArchiveAllowed: canArchive,
                                SupportsActivate: true,
                                ActivateAllowed: canReactivate))
                    };
                })
                .ToArray();

            companies = new PagedResponse<AccountCompanySummaryResponse>(
                enrichedItems,
                companies.PageNumber,
                companies.PageSize,
                companies.TotalCount);
        }

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

        // AC-7: boolean existence probe (404 unknown vs 403 owned-by-another) without loading the aggregate.
        var exists = await companyRepository.ExistsByPublicIdAsync(query.CompanyId, cancellationToken);
        return exists
            ? Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.OwnershipForbidden)
            : Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.CompanyNotFound);
    }
}

internal sealed class GetAvailableCompanyTypesQueryHandler(
    IOrgStructureCatalogRepository orgStructureCatalogRepository)
    : IQueryHandler<GetAvailableCompanyTypesQuery, IReadOnlyCollection<CompanyTypeCatalogItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<CompanyTypeCatalogItemResponse>>> Handle(
        GetAvailableCompanyTypesQuery query,
        CancellationToken cancellationToken)
    {
        var items = await orgStructureCatalogRepository.GetActiveCompanyTypesByCountryCodeAsync(query.CountryCode, cancellationToken);
        return Result<IReadOnlyCollection<CompanyTypeCatalogItemResponse>>.Success(items);
    }
}

internal sealed class CreateAccountCompanyCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyOwnershipPolicy companyOwnershipPolicy,
    ICompanyProvisioningService companyProvisioningService,
    ICountryCatalogRepository countryCatalogRepository,
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

        var owner = currentUserResult.Value;

        CatalogReferenceLookup? companyType = null;
        if (command.CompanyTypeId.HasValue)
        {
            var country = await countryCatalogRepository.GetActiveByCodeAsync(command.CountryCode, cancellationToken);
            if (country is null)
            {
                return Result<AccountCompanyDetailResponse>.Failure(ProvisioningErrors.CountryNotFound(command.CountryCode));
            }

            companyType = await orgStructureCatalogRepository.GetActiveCompanyTypeLookupAsync(
                country.InternalId,
                command.CompanyTypeId.Value,
                cancellationToken);
            if (companyType is null)
            {
                return Result<AccountCompanyDetailResponse>.Failure(OrgStructureCatalogErrors.CompanyTypeNotFound);
            }
        }

        // AC-3/AC-4: serialize the capacity check per owner with a transaction-scoped advisory lock (closing
        // the quota TOCTOU), and retry once on a duplicate-slug race. The slug is server-generated, so a
        // 23505 is an internal retry — regenerating picks the next suffix — not a client-facing 409.
        const int maxAttempts = 2;
        for (var attempt = 1; ; attempt++)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await companyRepository.AcquireOwnerCapacityLockAsync(owner.PublicId, cancellationToken);

                if (!await companyOwnershipPolicy.HasCapacityForAnotherActiveCompanyAsync(owner.PublicId, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.CompanyLimitReached);
                }

                var provisioningResult = await companyProvisioningService.ProvisionAsync(
                    new ProvisionCompanyRequest(
                        owner.PublicId,
                        command.Name,
                        command.CountryCode,
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
                    owner.PublicId,
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
                    owner.PublicId);

                return Result<AccountCompanyDetailResponse>.Success(response);
            }
            catch (UniqueConstraintViolationException ex)
                when (attempt < maxAttempts && AccountCompanyConstraintViolations.IsSlugConflict(ex.ConstraintName))
            {
                // Duplicate-slug race: roll back, clear the failed attempt's tracked entities, and retry on a
                // fresh transaction (the colliding slug now exists, so a new suffix is chosen).
                await transaction.RollbackAsync(cancellationToken);
                unitOfWork.ClearTracked();
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
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
        if (company.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.ConcurrencyConflict);
        }

        CatalogReferenceLookup? companyType = null;
        if (command.CompanyTypeId.HasValue)
        {
            companyType = await orgStructureCatalogRepository.GetActiveCompanyTypeLookupAsync(
                company.CountryCatalogItemId,
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

internal sealed class PatchAccountCompanyCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IOrgStructureCatalogRepository orgStructureCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchAccountCompanyCommand, AccountCompanyDetailResponse>
{
    public async Task<Result<AccountCompanyDetailResponse>> Handle(
        PatchAccountCompanyCommand command,
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
        if (company.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.ConcurrencyConflict);
        }

        var state = new AccountCompanyPatchState();
        var applied = AccountCompanyPatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<AccountCompanyDetailResponse>.Failure(applied.Error);
        }

        var validation = AccountCompanyPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<AccountCompanyDetailResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            // No patchable field was touched: skip the write so the token is not rotated and no
            // spurious audit entry is emitted (mirrors the canonical Preferences PATCH handlers).
            var current = await companyRepository.FindOwnedByUserAsync(
                company.PublicId,
                currentUserResult.Value.PublicId,
                tenantContext.TenantId,
                cancellationToken);
            return Result<AccountCompanyDetailResponse>.Success(current!);
        }

        // Resolve the (optional) company type only when the patch touched it, mirroring the PUT path.
        CatalogReferenceLookup? companyType = null;
        if (state.CompanyTypeSet && state.CompanyTypePublicId.HasValue)
        {
            companyType = await orgStructureCatalogRepository.GetActiveCompanyTypeLookupAsync(
                company.CountryCatalogItemId,
                state.CompanyTypePublicId.Value,
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
            if (state.NameSet)
            {
                company.Rename(state.Name);
            }

            if (state.CompanyTypeSet)
            {
                company.SetCompanyType(companyType?.InternalId);
            }

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

internal sealed class AccountCompanyPatchState
{
    public string Name { get; set; } = string.Empty;
    public bool NameSet { get; set; }
    public Guid? CompanyTypePublicId { get; set; }
    public bool CompanyTypeSet { get; set; }

    public bool HasMutation => NameSet || CompanyTypeSet;
}

internal sealed class AccountCompanyPatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class AccountCompanyPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<AccountCompanyPatchOperation> operations, AccountCompanyPatchState state)
    {
        foreach (var operation in operations)
        {
            var op = operation.Op.Trim();
            if (!SupportedOperations.Contains(op))
            {
                return ValidationFailure(operation.Path, $"Unsupported JSON Patch operation '{operation.Op}'.");
            }

            var segments = ParsePath(operation.Path);
            if (segments.Length != 1)
            {
                return ValidationFailure(operation.Path, "Only root company properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (AccountCompanyPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(AccountCompanyPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Mirror the PUT validator + the domain normalizer (Company.Rename → Clean trims then requires
        // non-empty). The DB column is varchar(150), so a name that trims longer than 150 would otherwise
        // throw on save → HTTP 500; validate the trimmed length here as a 400.
        if (state.NameSet)
        {
            if (string.IsNullOrWhiteSpace(state.Name))
            {
                errors["name"] = ["Name is required."];
            }
            else if (state.Name.Trim().Length > 150)
            {
                errors["name"] = ["Name must be 150 characters or fewer."];
            }
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        AccountCompanyPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "concurrencyToken"))
        {
            return ValidationFailure(path, "The concurrency token cannot be patched; send the current token in the If-Match header.");
        }

        // Status transitions go through the dedicated /archive and /reactivate actions, not this patch.
        if (IsSegment(property, "id") || IsSegment(property, "publicId") || IsSegment(property, "slug") ||
            IsSegment(property, "countryCode") || IsSegment(property, "status") || IsSegment(property, "planCode") ||
            IsSegment(property, "isActiveContext") || IsSegment(property, "isOwnedByCurrentUser") ||
            IsSegment(property, "createdAtUtc") || IsSegment(property, "modifiedAtUtc") ||
            IsSegment(property, "activeLegalRepresentatives") || IsSegment(property, "companyType"))
        {
            return ValidationFailure(path, "This property is read-only and cannot be patched.");
        }

        if (IsSegment(property, "name"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Name cannot be removed.");
            }

            state.Name = ReadRequiredString(value, path);
            state.NameSet = true;
            return Result.Success();
        }

        if (IsSegment(property, "companyTypePublicId"))
        {
            // The company type is optional, so a remove (or an explicit null) clears it.
            state.CompanyTypePublicId = isRemove ? null : ReadOptionalGuid(value, path);
            state.CompanyTypeSet = true;
            return Result.Success();
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static string[] ParsePath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(UnescapeJsonPointerSegment)
            .ToArray();

    private static string UnescapeJsonPointerSegment(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

    private static bool IsNull(JsonElement? value) =>
        !value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    private static bool IsSegment(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static string ReadRequiredString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new AccountCompanyPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new AccountCompanyPatchValueException(path, "Value must be a string.");
    }

    private static Guid? ReadOptionalGuid(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        if (value!.Value.ValueKind != JsonValueKind.String)
        {
            throw new AccountCompanyPatchValueException(path, "Value must be a string GUID or null.");
        }

        return Guid.TryParse(value.Value.GetString(), out var parsed)
            ? parsed
            : throw new AccountCompanyPatchValueException(path, "Value must be a valid GUID.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

internal sealed class ArchiveAccountCompanyCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IUserCompanyRepository userCompanyRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
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
        if (company.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.ConcurrencyConflict);
        }

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

            // AC-1: revoke the members' refresh tokens so the archived company cannot keep being operated via
            // an existing session (the access token still lives out its ≤15-min TTL; the 14-day refresh token
            // is killed here). Mirrors DeactivateCompanyUser. A member who also belongs to another active
            // company is signed out of it too — re-login re-issues a session against their active primary.
            var memberUserIds = await userCompanyRepository.GetMemberUserIdsAsync(company.PublicId, cancellationToken);
            await refreshTokenRepository.RevokeUsersTokensAsync(
                memberUserIds,
                AuthClientType.Core,
                dateTimeProvider.UtcNow,
                "company-archived",
                cancellationToken);

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
        if (company.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.ConcurrencyConflict);
        }

        if (company.Status == CompanyStatus.Active)
        {
            return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.CompanyAlreadyActive);
        }

        var before = await companyRepository.FindOwnedByUserAsync(
            company.PublicId,
            currentUserResult.Value.PublicId,
            tenantContext.TenantId,
            cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // AC-3: serialize the reactivation capacity check per owner with a transaction-scoped advisory
            // lock, closing the check-then-act race that could push the owner past the active-company quota.
            await companyRepository.AcquireOwnerCapacityLockAsync(currentUserResult.Value.PublicId, cancellationToken);
            if (!await companyOwnershipPolicy.HasCapacityForAnotherActiveCompanyAsync(currentUserResult.Value.PublicId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<AccountCompanyDetailResponse>.Failure(AccountCompanyErrors.CompanyReactivationLimitReached);
            }

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
    IIamAdministrationRepository iamRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICommercialAddonRepository commercialAddonRepository,
    IPlanEntitlementService planEntitlementService,
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

            var accessContext = await AccountCompanyAccessContextBuilder.BuildAsync(
                currentUserResult.Value,
                company,
                iamRepository,
                subscriptionRepository,
                commercialPlanRepository,
                commercialAddonRepository,
                planEntitlementService,
                cancellationToken);
            if (accessContext is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<SwitchActiveCompanyResponse>.Failure(AccountCompanyErrors.SubscriptionContextUnavailable);
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
                    After: new ActiveCompanyDto(company.PublicId, company.Name, company.Slug, company.CountryCode, company.Status)),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<SwitchActiveCompanyResponse>.Success(new SwitchActiveCompanyResponse(
                tokenResult.Value.AccessToken,
                tokenResult.Value.RefreshToken,
                tokenResult.Value.ExpiresIn,
                new ActiveCompanyDto(company.PublicId, company.Name, company.Slug, company.CountryCode, company.Status),
                accessContext));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetOwnedCompanyAccessContextQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IIamAdministrationRepository iamRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICommercialAddonRepository commercialAddonRepository,
    IPlanEntitlementService planEntitlementService)
    : IQueryHandler<GetOwnedCompanyAccessContextQuery, AccountCompanyAccessContextResponse>
{
    public async Task<Result<AccountCompanyAccessContextResponse>> Handle(
        GetOwnedCompanyAccessContextQuery query,
        CancellationToken cancellationToken)
    {
        var ownershipResult = await AccountCompanySubscriptionHelper.ResolveOwnershipAsync(
            currentUserService,
            userRepository,
            companyRepository,
            query.CompanyId,
            cancellationToken);

        if (ownershipResult.IsFailure)
        {
            return Result<AccountCompanyAccessContextResponse>.Failure(ownershipResult.Error);
        }

        var accessContext = await AccountCompanyAccessContextBuilder.BuildAsync(
            ownershipResult.Value.Owner,
            ownershipResult.Value.Company,
            iamRepository,
            subscriptionRepository,
            commercialPlanRepository,
            commercialAddonRepository,
            planEntitlementService,
            cancellationToken);

        return accessContext is null
            ? Result<AccountCompanyAccessContextResponse>.Failure(AccountCompanyErrors.SubscriptionContextUnavailable)
            : Result<AccountCompanyAccessContextResponse>.Success(accessContext);
    }
}

internal sealed class GetOwnedCompanyRoleBuilderCatalogQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IIamAdministrationRepository iamRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICommercialAddonRepository commercialAddonRepository,
    IPlanEntitlementService planEntitlementService)
    : IQueryHandler<GetOwnedCompanyRoleBuilderCatalogQuery, AccountCompanyRoleBuilderCatalogResponse>
{
    private static readonly string[] SupportedAccessStates = ["hidden", "masked", "readonly", "editable"];

    public async Task<Result<AccountCompanyRoleBuilderCatalogResponse>> Handle(
        GetOwnedCompanyRoleBuilderCatalogQuery query,
        CancellationToken cancellationToken)
    {
        var ownershipResult = await AccountCompanySubscriptionHelper.ResolveOwnershipAsync(
            currentUserService,
            userRepository,
            companyRepository,
            query.CompanyId,
            cancellationToken);

        if (ownershipResult.IsFailure)
        {
            return Result<AccountCompanyRoleBuilderCatalogResponse>.Failure(ownershipResult.Error);
        }

        var accessContext = await AccountCompanyAccessContextBuilder.BuildAsync(
            ownershipResult.Value.Owner,
            ownershipResult.Value.Company,
            iamRepository,
            subscriptionRepository,
            commercialPlanRepository,
            commercialAddonRepository,
            planEntitlementService,
            cancellationToken);

        if (accessContext is null)
        {
            return Result<AccountCompanyRoleBuilderCatalogResponse>.Failure(AccountCompanyErrors.SubscriptionContextUnavailable);
        }

        var fieldPoliciesCatalog = FieldCatalogRegistry.Definitions
            .OrderBy(static definition => definition.ResourceKey, StringComparer.Ordinal)
            .ThenBy(static definition => definition.DisplayName, StringComparer.Ordinal)
            .Select(static definition => new AccountCompanyFieldPolicyCatalogItemResponse(
                definition.ResourceKey,
                definition.FieldKey,
                definition.DisplayName,
                definition.DataType,
                definition.IsSensitive,
                SupportedAccessStates))
            .ToArray();

        var scopeTypes = AuthorizationScopeCatalog.All
            .Select(static definition => new AccountCompanyAccessScopeTypeResponse(
                definition.ScopeType,
                definition.DisplayName,
                definition.Description))
            .ToArray();

        return Result<AccountCompanyRoleBuilderCatalogResponse>.Success(
            new AccountCompanyRoleBuilderCatalogResponse(
                accessContext.EffectiveModules,
                accessContext.EffectiveCapabilities,
                accessContext.CurrentUserAccess.Permissions.Where(static permission => !permission.IsDormant).ToArray(),
                fieldPoliciesCatalog,
                scopeTypes));
    }
}

internal sealed class GetOwnedCompanyResourcePolicyQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ITenantContext tenantContext,
    IFieldPermissionService fieldPermissionService,
    IRbacAuthorizationService rbacAuthorizationService)
    : IQueryHandler<GetOwnedCompanyResourcePolicyQuery, AccountCompanyResourcePolicyResponse>
{
    public async Task<Result<AccountCompanyResourcePolicyResponse>> Handle(
        GetOwnedCompanyResourcePolicyQuery query,
        CancellationToken cancellationToken)
    {
        var ownershipResult = await AccountCompanySubscriptionHelper.ResolveOwnershipAsync(
            currentUserService,
            userRepository,
            companyRepository,
            query.CompanyId,
            cancellationToken);

        if (ownershipResult.IsFailure)
        {
            return Result<AccountCompanyResourcePolicyResponse>.Failure(ownershipResult.Error);
        }

        if (!tenantContext.TenantId.HasValue || tenantContext.TenantId.Value != query.CompanyId)
        {
            return Result<AccountCompanyResourcePolicyResponse>.Failure(AccountCompanyErrors.ActiveCompanyContextRequired);
        }

        if (!PermissionMatrixCatalog.TryGet(query.ResourceKey, out _))
        {
            return Result<AccountCompanyResourcePolicyResponse>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    [nameof(query.ResourceKey)] = [$"Unknown resource key '{query.ResourceKey}'."]
                }));
        }

        var fieldProfileResult = await fieldPermissionService.GetCurrentUserAccessProfileAsync(query.ResourceKey, cancellationToken);
        if (fieldProfileResult.IsFailure)
        {
            return Result<AccountCompanyResourcePolicyResponse>.Failure(fieldProfileResult.Error);
        }

        var actionPolicy = new AccountCompanyActionPolicyResponse(
            CanAccess: (await rbacAuthorizationService.AuthorizeAsync(query.ResourceKey, RbacPermissionAction.Access, cancellationToken)).IsSuccess,
            CanRead: (await rbacAuthorizationService.AuthorizeAsync(query.ResourceKey, RbacPermissionAction.Read, cancellationToken)).IsSuccess,
            CanCreate: (await rbacAuthorizationService.AuthorizeAsync(query.ResourceKey, RbacPermissionAction.Create, cancellationToken)).IsSuccess,
            CanUpdate: (await rbacAuthorizationService.AuthorizeAsync(query.ResourceKey, RbacPermissionAction.Update, cancellationToken)).IsSuccess,
            CanDelete: (await rbacAuthorizationService.AuthorizeAsync(query.ResourceKey, RbacPermissionAction.Delete, cancellationToken)).IsSuccess);

        var fieldStates = FieldCatalogRegistry.GetResourceFields(query.ResourceKey)
            .OrderBy(static field => field.DisplayName, StringComparer.Ordinal)
            .Select(field =>
            {
                var rule = fieldProfileResult.Value.GetRule(field.FieldKey);
                var access = !rule.IsVisible
                    ? "hidden"
                    : rule.IsMasked
                        ? "masked"
                        : rule.IsEditable
                            ? "editable"
                            : "readonly";

                return new AccountCompanyFieldPolicyStateResponse(
                    field.FieldKey,
                    field.PropertyName,
                    field.DisplayName,
                    access,
                    rule.IsRequired,
                    field.IsSensitive);
            })
            .ToArray();

        return Result<AccountCompanyResourcePolicyResponse>.Success(
            new AccountCompanyResourcePolicyResponse(
                query.ResourceKey,
                actionPolicy,
                fieldStates));
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

internal static class AccountCompanyAccessContextBuilder
{
    private static readonly string[] CompanyScopeTypes = [AuthorizationScopeTypes.Company];

    public static async Task<AccountCompanyAccessContextResponse?> BuildAsync(
        User actor,
        Company company,
        IIamAdministrationRepository iamRepository,
        ICompanySubscriptionRepository subscriptionRepository,
        ICommercialPlanRepository commercialPlanRepository,
        ICommercialAddonRepository commercialAddonRepository,
        IPlanEntitlementService planEntitlementService,
        CancellationToken cancellationToken)
    {
        var currentSubscription = await subscriptionRepository.GetActiveByCompanyIdAsync(company.Id, cancellationToken);
        if (currentSubscription is null)
        {
            return null;
        }

        var currentPlan = await commercialPlanRepository.GetByInternalIdAsync(
            currentSubscription.CommercialPlanId,
            cancellationToken);
        if (currentPlan is null)
        {
            return null;
        }

        var activeAddons = await AccountCompanySubscriptionHelper.GetActiveAddonsAsync(
            company,
            subscriptionRepository,
            commercialAddonRepository,
            cancellationToken);

        var effectiveCapabilities = await planEntitlementService.GetEffectiveCapabilitiesAsync(company.PublicId, cancellationToken);
        var effectiveModules = await planEntitlementService.GetEffectiveModulesAsync(company.PublicId, cancellationToken);
        var effectiveCapabilityResponses = effectiveCapabilities
            .Select(MapEffectiveCapability)
            .ToArray();
        var effectiveModuleResponses = effectiveModules
            .Select(MapEffectiveModule)
            .ToArray();

        var effectiveCapabilityCodes = effectiveCapabilities
            .Select(static capability => capability.CapabilityCode)
            .ToHashSet(StringComparer.Ordinal);

        var iamUser = await iamRepository.FindUserByTenantAndLinkedUserPublicIdAsync(
            company.PublicId,
            actor.PublicId,
            includeRoles: true,
            cancellationToken);

        var roles = iamUser?.RoleAssignments
            .OrderBy(static assignment => assignment.Role.Name, StringComparer.Ordinal)
            .Select(static assignment => new AccountCompanyAccessRoleResponse(
                assignment.Role.PublicId,
                assignment.Role.Name,
                assignment.Role.Description,
                assignment.Role.IsSystemRole))
            .DistinctBy(static role => role.Id)
            .ToArray() ?? Array.Empty<AccountCompanyAccessRoleResponse>();

        var permissions = iamUser?.RoleAssignments
            .SelectMany(static assignment => assignment.Role.PermissionAssignments)
            .Select(static assignment => assignment.Permission)
            .GroupBy(static permission => permission.PublicId)
            .Select(group => MapPermission(group.First(), effectiveCapabilityCodes))
            .OrderBy(static permission => permission.Code, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<AccountCompanyAccessPermissionResponse>();

        var scopes = permissions
            .Where(static permission => !permission.IsDormant)
            .Select(permission => new AccountCompanyPermissionScopeResponse(
                permission.Code,
                AuthorizationScopeTypes.Company,
                [company.PublicId.ToString()],
                IsImplicit: true))
            .ToArray();

        var commercialContext = new AccountCompanyCommercialContextResponse(
            new AccountCompanyAccessSubscriptionContextResponse(
                currentPlan.PublicId,
                currentPlan.Code,
                currentPlan.Name,
                currentPlan.Description,
                currentPlan.Entitlements
                    .Where(static entitlement => entitlement.IsEnabled)
                    .Select(static entitlement => ResolveCapabilityCode(entitlement.CapabilityCode, entitlement.ModuleKey))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static capabilityCode => capabilityCode, StringComparer.Ordinal)
                    .ToArray()),
            activeAddons
                .Select(addon => new AccountCompanyAccessAddonContextResponse(
                    addon.CommercialAddonId,
                    addon.Code,
                    addon.Name,
                    addon.Description,
                    addon.ModuleKeys
                        .Select(static moduleKey => CommercialCapabilityCatalog.GetByModuleKey(moduleKey).Code)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(static capabilityCode => capabilityCode, StringComparer.Ordinal)
                        .ToArray()))
                .ToArray());

        return new AccountCompanyAccessContextResponse(
            new ActiveCompanyDto(company.PublicId, company.Name, company.Slug, company.CountryCode, company.Status),
            commercialContext,
            effectiveCapabilityResponses,
            effectiveModuleResponses,
            new AccountCompanyCurrentUserAccessResponse(roles, permissions, scopes));
    }

    private static AccountCompanyEffectiveCapabilityResponse MapEffectiveCapability(EffectiveCommercialCapabilityGrant grant)
    {
        var definition = CommercialCapabilityCatalog.Get(grant.CapabilityCode);
        var source = grant.GrantedByPlan && grant.GrantedByAddon
            ? "plan+addon"
            : grant.GrantedByPlan
                ? "plan"
                : "addon";

        return new AccountCompanyEffectiveCapabilityResponse(
            grant.CapabilityCode,
            grant.ModuleKey,
            definition.DisplayName,
            definition.Description,
            source,
            grant.GrantedByPlan,
            grant.GrantedByAddon);
    }

    private static AccountCompanyEffectiveModuleResponse MapEffectiveModule(EffectiveCommercialModuleGrant grant)
    {
        var definition = CommercialModuleCatalog.Get(grant.ModuleKey);
        var source = grant.GrantedByPlan && grant.GrantedByAddon
            ? "plan+addon"
            : grant.GrantedByPlan
                ? "plan"
                : "addon";

        return new AccountCompanyEffectiveModuleResponse(
            grant.ModuleKey,
            definition.DisplayName,
            definition.Description,
            source,
            grant.GrantedByPlan,
            grant.GrantedByAddon);
    }

    private static AccountCompanyAccessPermissionResponse MapPermission(
        IamPermission permission,
        IReadOnlySet<string> effectiveCapabilityCodes)
    {
        var capabilityCodes = ResolveCapabilityCodes(permission);
        var isDormant = capabilityCodes.Count > 0 && capabilityCodes.All(capabilityCode => !effectiveCapabilityCodes.Contains(capabilityCode));

        return new AccountCompanyAccessPermissionResponse(
            permission.PublicId,
            permission.Code,
            permission.Name,
            permission.Description,
            permission.Module,
            permission.Screen,
            permission.Kind,
            permission.Action,
            permission.FieldName,
            permission.FieldAccess,
            capabilityCodes,
            isDormant,
            CompanyScopeTypes);
    }

    private static IReadOnlyCollection<string> ResolveCapabilityCodes(IamPermission permission)
    {
        var capabilityCodes = new HashSet<string>(StringComparer.Ordinal);

        if (CommercialModuleCatalog.IsKnown(permission.Module))
        {
            capabilityCodes.Add(CommercialCapabilityCatalog.GetByModuleKey(permission.Module).Code);
        }

        if (PermissionMatrixCatalog.TryGet(permission.Screen, out var definition))
        {
            capabilityCodes.Add(CommercialCapabilityCatalog.GetByModuleKey(definition.PlanModuleKey).Code);
        }

        if (string.Equals(permission.NormalizedCode, IdentityPermissionCodes.ManageAdministration.ToUpperInvariant(), StringComparison.Ordinal))
        {
            capabilityCodes.Add(CommercialCapabilityCatalog.GetByModuleKey(CommercialModuleKeys.Rbac).Code);
        }

        return capabilityCodes
            .OrderBy(static capabilityCode => capabilityCode, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveCapabilityCode(string? capabilityCode, string moduleKey) =>
        string.IsNullOrWhiteSpace(capabilityCode)
            ? CommercialCapabilityCatalog.GetByModuleKey(moduleKey).Code
            : CommercialCapabilityCatalog.NormalizeKnownCode(capabilityCode);
}
