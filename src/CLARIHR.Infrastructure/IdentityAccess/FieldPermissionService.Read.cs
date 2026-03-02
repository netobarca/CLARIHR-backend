using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed partial class FieldPermissionService
{
    public async Task<Result<ResourceFieldsResponse>> GetResourceFieldsAsync(
        string resourceKey,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await _authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ResourceFieldsResponse>.Failure(authorizationResult.Error);
        }

        if (!TryResolveResource(resourceKey, out var definition))
        {
            return Result<ResourceFieldsResponse>.Failure(CreateResourceValidationError(resourceKey));
        }

        var fields = await GetCatalogAsync(definition.ResourceKey, cancellationToken);
        var response = new ResourceFieldsResponse(
            definition.ResourceKey,
            fields
                .Where(static field => field.IsConfigurable)
                .Select(static field => new ResourceFieldResponse(
                    field.FieldKey,
                    field.PropertyName,
                    field.DisplayName,
                    field.DataType,
                    field.IsConfigurable,
                    field.IsSensitive))
                .ToArray());

        return Result<ResourceFieldsResponse>.Success(response);
    }

    public async Task<Result<RoleFieldPermissionsResponse>> GetRoleFieldPermissionsAsync(
        Guid roleId,
        string resourceKey,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await _authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(authorizationResult.Error);
        }

        if (!TryResolveResource(resourceKey, out var definition))
        {
            return Result<RoleFieldPermissionsResponse>.Failure(CreateResourceValidationError(resourceKey));
        }

        var role = await _dbContext.IamRoles
            .AsNoTracking()
            .Include(candidate => candidate.PermissionAssignments)
            .ThenInclude(assignment => assignment.Permission)
            .SingleOrDefaultAsync(candidate => candidate.PublicId == roleId, cancellationToken);
        if (role is null)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(
                await _dbContext.IamRoles
                    .IgnoreQueryFilters()
                    .AnyAsync(candidate => candidate.PublicId == roleId, cancellationToken)
                    ? AuthorizationErrors.TenantMismatch(definition.ResourceKey, RbacPermissionAction.Read, GetEndpoint())
                    : IdentityAccessErrors.RoleNotFound);
        }

        var screenState = CaptureScreenState(role.PermissionAssignments.Select(static assignment => assignment.Permission), definition.ScreenKey);
        if (!screenState.HasAccess)
        {
            return Result<RoleFieldPermissionsResponse>.Failure(FieldPermissionErrors.ResourceAccessRequired);
        }

        var catalog = await GetCatalogAsync(definition.ResourceKey, cancellationToken);
        var overrides = await GetRoleOverridesAsync(role.Id, definition.ResourceKey, cancellationToken);
        var profile = FieldPermissionEvaluator.BuildProfile(definition.ResourceKey, catalog, overrides, screenState);

        return Result<RoleFieldPermissionsResponse>.Success(Map(role.PublicId, role.Name, profile));
    }
}
