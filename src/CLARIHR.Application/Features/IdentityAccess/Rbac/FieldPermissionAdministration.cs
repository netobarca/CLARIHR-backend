using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using FluentValidation;

namespace CLARIHR.Application.Features.IdentityAccess.Rbac;

public sealed record GetResourceFieldsQuery(string ResourceKey) : IQuery<ResourceFieldsResponse>;

public sealed record GetRoleFieldPermissionsQuery(Guid RoleId, string ResourceKey) : IQuery<RoleFieldPermissionsResponse>;

public sealed record UpsertRoleFieldPermissionsCommand(
    Guid RoleId,
    string ResourceKey,
    IReadOnlyCollection<RoleFieldPermissionUpdate> Fields) : ICommand<RoleFieldPermissionsResponse>;

public sealed record RoleFieldPermissionUpdate(
    string FieldKey,
    bool IsVisible,
    bool IsEditable,
    bool IsRequired,
    bool IsMasked);

internal sealed class GetResourceFieldsQueryValidator : AbstractValidator<GetResourceFieldsQuery>
{
    public GetResourceFieldsQueryValidator()
    {
        RuleFor(query => query.ResourceKey)
            .NotEmpty()
            .MaximumLength(100);
    }
}

internal sealed class GetRoleFieldPermissionsQueryValidator : AbstractValidator<GetRoleFieldPermissionsQuery>
{
    public GetRoleFieldPermissionsQueryValidator()
    {
        RuleFor(query => query.RoleId)
            .NotEmpty();

        RuleFor(query => query.ResourceKey)
            .NotEmpty()
            .MaximumLength(100);
    }
}

internal sealed class UpsertRoleFieldPermissionsCommandValidator : AbstractValidator<UpsertRoleFieldPermissionsCommand>
{
    public UpsertRoleFieldPermissionsCommandValidator()
    {
        RuleFor(command => command.RoleId)
            .NotEmpty();

        RuleFor(command => command.ResourceKey)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Fields)
            .NotNull()
            .Must(static fields => fields.Count > 0)
            .WithMessage("At least one field update must be provided.");

        RuleForEach(command => command.Fields)
            .SetValidator(new RoleFieldPermissionUpdateValidator());
    }
}

internal sealed class RoleFieldPermissionUpdateValidator : AbstractValidator<RoleFieldPermissionUpdate>
{
    public RoleFieldPermissionUpdateValidator()
    {
        RuleFor(field => field.FieldKey)
            .NotEmpty()
            .MaximumLength(150);
    }
}

internal sealed class GetResourceFieldsQueryHandler(IFieldPermissionService fieldPermissionService)
    : IQueryHandler<GetResourceFieldsQuery, ResourceFieldsResponse>
{
    public Task<Result<ResourceFieldsResponse>> Handle(
        GetResourceFieldsQuery query,
        CancellationToken cancellationToken) =>
        fieldPermissionService.GetResourceFieldsAsync(query.ResourceKey, cancellationToken);
}

internal sealed class GetRoleFieldPermissionsQueryHandler(IFieldPermissionService fieldPermissionService)
    : IQueryHandler<GetRoleFieldPermissionsQuery, RoleFieldPermissionsResponse>
{
    public Task<Result<RoleFieldPermissionsResponse>> Handle(
        GetRoleFieldPermissionsQuery query,
        CancellationToken cancellationToken) =>
        fieldPermissionService.GetRoleFieldPermissionsAsync(query.RoleId, query.ResourceKey, cancellationToken);
}

internal sealed class UpsertRoleFieldPermissionsCommandHandler(IFieldPermissionService fieldPermissionService)
    : ICommandHandler<UpsertRoleFieldPermissionsCommand, RoleFieldPermissionsResponse>
{
    public Task<Result<RoleFieldPermissionsResponse>> Handle(
        UpsertRoleFieldPermissionsCommand command,
        CancellationToken cancellationToken) =>
        fieldPermissionService.UpsertRoleFieldPermissionsAsync(
            command.RoleId,
            command.ResourceKey,
            command.Fields
                .Select(static field => new RoleFieldPermissionUpdateModel(
                    field.FieldKey,
                    field.IsVisible,
                    field.IsEditable,
                    field.IsRequired,
                    field.IsMasked))
                .ToArray(),
            cancellationToken);
}
