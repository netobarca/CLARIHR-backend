using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed class RbacAuthorizationService(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    IPlanEntitlementService planEntitlementService,
    IFieldAccessProfileService fieldAccessProfileService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<RbacAuthorizationService> logger) : IRbacAuthorizationService
{
    public async Task<Result> AuthorizeAsync(
        string resourceKey,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return Deny(AuthorizationErrors.Unauthenticated, resourceKey, action);
        }

        if (!tenantContext.TenantId.HasValue)
        {
            return Deny(AuthorizationErrors.TenantMismatch(resourceKey, action, GetEndpoint()), resourceKey, action);
        }

        if (!PermissionMatrixCatalog.TryGet(resourceKey, out var definition))
        {
            return Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
            {
                [nameof(resourceKey)] = [$"Unknown resource key '{resourceKey}'."]
            }));
        }

        var moduleEnabled = await planEntitlementService.IsModuleEnabledAsync(
            tenantContext.TenantId.Value,
            definition.PlanModuleKey,
            cancellationToken);
        if (!moduleEnabled)
        {
            return Deny(AuthorizationErrors.Denied(definition.ResourceKey, action, GetEndpoint()), definition.ResourceKey, action);
        }

        if (currentUserService.Roles.Contains("platform_admin", StringComparer.OrdinalIgnoreCase) ||
            currentUserService.Permissions.Contains(IdentityPermissionCodes.ManageAdministration, StringComparer.OrdinalIgnoreCase) ||
            currentUserService.Permissions.Contains(definition.ManagePermissionCode, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Success();
        }

        if (RbacAuthorizationEvaluator.IsAllowed(currentUserService.Permissions, definition.ScreenKey, action))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(currentUserService.UserId, out var userPublicId))
        {
            return Deny(AuthorizationErrors.Unauthenticated, definition.ResourceKey, action);
        }

        var grantedCodes = await dbContext.IamUsers
            .AsNoTracking()
            .Where(user => user.LinkedUserPublicId == userPublicId && user.IsActive)
            .SelectMany(user => user.RoleAssignments)
            .SelectMany(assignment => assignment.Role.PermissionAssignments)
            .Select(assignment => assignment.Permission.NormalizedCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (grantedCodes.Contains(IdentityPermissionCodes.ManageAdministration.ToUpperInvariant(), StringComparer.Ordinal) ||
            grantedCodes.Contains(definition.ManagePermissionCode.ToUpperInvariant(), StringComparer.Ordinal))
        {
            return Result.Success();
        }

        return RbacAuthorizationEvaluator.IsAllowed(grantedCodes, definition.ScreenKey, action)
            ? Result.Success()
            : Deny(AuthorizationErrors.Denied(definition.ResourceKey, action, GetEndpoint()), definition.ResourceKey, action);
    }

    public async Task<Result> AuthorizeFieldsAsync(
        string resourceKey,
        RbacPermissionAction action,
        IReadOnlyCollection<string> fieldKeys,
        CancellationToken cancellationToken)
    {
        if (fieldKeys.Count == 0)
        {
            return Result.Success();
        }

        var authorizationResult = await AuthorizeAsync(resourceKey, action, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return authorizationResult;
        }

        var fieldAccessResult = await fieldAccessProfileService.GetCurrentUserAccessProfileAsync(resourceKey, cancellationToken);
        if (fieldAccessResult.IsFailure)
        {
            return fieldAccessResult;
        }

        var deniedFields = fieldKeys
            .Where(fieldKey => !fieldAccessResult.Value.CanWrite(fieldKey, action))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return deniedFields.Length == 0
            ? Result.Success()
            : Deny(
                AuthorizationErrors.FieldEditForbidden(resourceKey, action, deniedFields, GetEndpoint()),
                resourceKey,
                action,
                deniedFields);
    }

    private Result Deny(Error error, string resourceKey, RbacPermissionAction action, IReadOnlyCollection<string>? fieldKeys = null)
    {
        logger.LogWarning(
            "Authorization denied. Code {Code} UserId {UserId} TenantId {TenantId} ResourceKey {ResourceKey} Action {Action} Endpoint {Endpoint} FieldKeys {FieldKeys}",
            error.Code,
            currentUserService.UserId,
            tenantContext.TenantId,
            resourceKey,
            action,
            GetEndpoint(),
            fieldKeys ?? Array.Empty<string>());

        return Result.Failure(error);
    }

    private string? GetEndpoint() => httpContextAccessor.HttpContext?.Request.Path.Value;
}
