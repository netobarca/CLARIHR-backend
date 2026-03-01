using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.IdentityAccess.Roles;
using FluentValidation;

namespace CLARIHR.Application.Features.IdentityAccess.Rbac;

public sealed record GetRbacResourcesQuery() : IQuery<RbacResourcesResponse>;

public sealed record GetRolePermissionsQuery(Guid RoleId) : IQuery<RbacRolePermissionsResponse>;

public sealed record UpsertRolePermissionsCommand(
    Guid RoleId,
    IReadOnlyCollection<RoleResourcePermissionUpdate> Permissions) : ICommand<RbacRolePermissionsResponse>;

public sealed record RoleResourcePermissionUpdate(
    string ResourceKey,
    bool HasAccess,
    bool CanRead,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete);

public sealed record GetPermissionAuditQuery(
    Guid? RoleId,
    string? ResourceKey,
    DateTime? FromUtc,
    DateTime? ToUtc) : IQuery<RbacPermissionAuditListResponse>;

internal sealed class GetRolePermissionsQueryValidator : AbstractValidator<GetRolePermissionsQuery>
{
    public GetRolePermissionsQueryValidator()
    {
        RuleFor(query => query.RoleId)
            .NotEmpty();
    }
}

internal sealed class UpsertRolePermissionsCommandValidator : AbstractValidator<UpsertRolePermissionsCommand>
{
    public UpsertRolePermissionsCommandValidator()
    {
        RuleFor(command => command.RoleId)
            .NotEmpty();

        RuleFor(command => command.Permissions)
            .NotNull()
            .Must(static permissions => permissions.Count > 0)
            .WithMessage("At least one permission update must be provided.");

        RuleForEach(command => command.Permissions)
            .SetValidator(new RoleResourcePermissionUpdateValidator());
    }
}

internal sealed class RoleResourcePermissionUpdateValidator : AbstractValidator<RoleResourcePermissionUpdate>
{
    public RoleResourcePermissionUpdateValidator()
    {
        RuleFor(permission => permission.ResourceKey)
            .NotEmpty()
            .MaximumLength(100)
            .Must(static resourceKey => PermissionMatrixCatalog.TryGet(resourceKey, out _))
            .WithMessage("Unknown resource key.");
    }
}

internal sealed class GetPermissionAuditQueryValidator : AbstractValidator<GetPermissionAuditQuery>
{
    public GetPermissionAuditQueryValidator()
    {
        RuleFor(query => query.RoleId)
            .NotEqual(Guid.Empty)
            .When(static query => query.RoleId.HasValue);

        RuleFor(query => query.ResourceKey)
            .MaximumLength(100)
            .Must(static resourceKey => string.IsNullOrWhiteSpace(resourceKey) || PermissionMatrixCatalog.TryGet(resourceKey, out _))
            .WithMessage("Unknown resource key.");

        RuleFor(query => query)
            .Must(static query => !query.FromUtc.HasValue || !query.ToUtc.HasValue || query.FromUtc.Value <= query.ToUtc.Value)
            .WithMessage("The 'from' date must be less than or equal to 'to'.");
    }
}

internal sealed class GetRbacResourcesQueryHandler(
    IIamAdministrationAuthorizationService authorizationService,
    IIamAdministrationRepository repository)
    : IQueryHandler<GetRbacResourcesQuery, RbacResourcesResponse>
{
    public async Task<Result<RbacResourcesResponse>> Handle(
        GetRbacResourcesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RbacResourcesResponse>.Failure(authorizationResult.Error);
        }

        var resources = await repository.GetActiveRbacResourcesAsync(cancellationToken);
        var items = resources.Count > 0
            ? resources
                .Select(static resource => new RbacResourceResponse(resource.ResourceKey, resource.DisplayName))
                .ToArray()
            : PermissionMatrixCatalog.Screens
                .Select(static definition => new RbacResourceResponse(definition.ResourceKey, definition.DisplayName))
                .ToArray();

        return Result<RbacResourcesResponse>.Success(new RbacResourcesResponse(items));
    }
}

internal sealed class GetRolePermissionsQueryHandler(IQueryDispatcher queryDispatcher)
    : IQueryHandler<GetRolePermissionsQuery, RbacRolePermissionsResponse>
{
    public async Task<Result<RbacRolePermissionsResponse>> Handle(
        GetRolePermissionsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetRolePermissionMatrixQuery(query.RoleId), cancellationToken);
        return result.IsFailure
            ? Result<RbacRolePermissionsResponse>.Failure(result.Error)
            : Result<RbacRolePermissionsResponse>.Success(RbacContractsMapper.Map(result.Value));
    }
}

internal sealed class UpsertRolePermissionsCommandHandler(ICommandDispatcher commandDispatcher)
    : ICommandHandler<UpsertRolePermissionsCommand, RbacRolePermissionsResponse>
{
    public async Task<Result<RbacRolePermissionsResponse>> Handle(
        UpsertRolePermissionsCommand command,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateRolePermissionMatrixCommand(
                command.RoleId,
                command.Permissions
                    .Select(static permission => new RolePermissionMatrixScreenUpdate(
                        permission.ResourceKey,
                        permission.HasAccess,
                        permission.CanRead,
                        permission.CanCreate,
                        permission.CanUpdate,
                        permission.CanDelete))
                    .ToArray()),
            cancellationToken);

        return result.IsFailure
            ? Result<RbacRolePermissionsResponse>.Failure(result.Error)
            : Result<RbacRolePermissionsResponse>.Success(RbacContractsMapper.Map(result.Value));
    }
}

internal sealed class GetPermissionAuditQueryHandler(
    IIamAdministrationAuthorizationService authorizationService,
    IIamAdministrationRepository repository)
    : IQueryHandler<GetPermissionAuditQuery, RbacPermissionAuditListResponse>
{
    public async Task<Result<RbacPermissionAuditListResponse>> Handle(
        GetPermissionAuditQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RbacPermissionAuditListResponse>.Failure(authorizationResult.Error);
        }

        var normalizedResourceKey = ResolveNormalizedResourceKey(query.ResourceKey);
        var auditEntries = await repository.GetPermissionAuditLogsAsync(
            query.RoleId,
            normalizedResourceKey,
            query.FromUtc,
            query.ToUtc,
            cancellationToken);

        var items = auditEntries
            .Select(static entry =>
            {
                var before = RbacPermissionChangeTracker.Deserialize(entry.BeforeJson);
                var after = RbacPermissionChangeTracker.Deserialize(entry.AfterJson);

                return new RbacPermissionAuditEntryResponse(
                    entry.Id,
                    entry.TenantId,
                    entry.RolePublicId,
                    entry.ResourceKey,
                    entry.ChangedByUserId,
                    entry.ChangeType.ToString(),
                    new RbacPermissionAuditStateResponse(
                        before.HasAccess,
                        before.CanRead,
                        before.CanCreate,
                        before.CanUpdate,
                        before.CanDelete),
                    new RbacPermissionAuditStateResponse(
                        after.HasAccess,
                        after.CanRead,
                        after.CanCreate,
                        after.CanUpdate,
                        after.CanDelete),
                    entry.ChangedAtUtc);
            })
            .ToArray();

        return Result<RbacPermissionAuditListResponse>.Success(new RbacPermissionAuditListResponse(items));
    }

    private static string? ResolveNormalizedResourceKey(string? resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return null;
        }

        return PermissionMatrixCatalog.TryGet(resourceKey, out var definition)
            ? definition.ResourceKey.ToUpperInvariant()
            : resourceKey.Trim().ToUpperInvariant();
    }
}

internal static class RbacContractsMapper
{
    public static RbacRolePermissionsResponse Map(RolePermissionMatrixResponse response)
    {
        var permissions = response.Screens
            .Select(static screen => new RbacRolePermissionResponse(
                screen.ResourceKey,
                screen.DisplayName,
                screen.Access.Granted,
                screen.Read.Granted,
                screen.Create.Granted,
                screen.Update.Granted,
                screen.Delete.Granted))
            .ToArray();

        return new RbacRolePermissionsResponse(
            response.RoleId,
            response.RoleName,
            response.IsSystemRole,
            permissions);
    }
}
