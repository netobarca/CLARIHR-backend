using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Domain.IdentityAccess;
using FluentValidation;

namespace CLARIHR.Application.Features.IdentityAccess.Permissions;

public sealed record CreateIamPermissionCommand(
    string Name,
    string? Description,
    string? Code,
    string Module,
    string Screen,
    IamPermissionKind Kind,
    string? Action,
    string? FieldName,
    IamFieldAccessLevel? FieldAccess) : ICommand<IamPermissionResponse>;

public sealed record GetIamPermissionByIdQuery(Guid PermissionId) : IQuery<IamPermissionResponse>;

public sealed record ListIamPermissionsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null) : IQuery<PagedResponse<IamPermissionSummaryResponse>>;

internal sealed class CreateIamPermissionCommandValidator : AbstractValidator<CreateIamPermissionCommand>
{
    public CreateIamPermissionCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(command => command.Description)
            .MaximumLength(500)
            .When(static command => !string.IsNullOrWhiteSpace(command.Description));

        RuleFor(command => command.Code)
            .MaximumLength(200)
            .When(static command => !string.IsNullOrWhiteSpace(command.Code));

        RuleFor(command => command.Module)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Screen)
            .NotEmpty()
            .MaximumLength(100);

        When(command => command.Kind == IamPermissionKind.ScreenAction, () =>
        {
            RuleFor(command => command.Action)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(command => command.FieldName)
                .Must(static value => string.IsNullOrWhiteSpace(value))
                .WithMessage("FieldName must be empty when the permission kind is ScreenAction.");

            RuleFor(command => command.FieldAccess)
                .Null();
        });

        When(command => command.Kind == IamPermissionKind.Field, () =>
        {
            RuleFor(command => command.FieldName)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(command => command.FieldAccess)
                .NotNull();

            RuleFor(command => command.Action)
                .Must(static value => string.IsNullOrWhiteSpace(value))
                .WithMessage("Action must be empty when the permission kind is Field.");
        });
    }
}

internal sealed class GetIamPermissionByIdQueryValidator : AbstractValidator<GetIamPermissionByIdQuery>
{
    public GetIamPermissionByIdQueryValidator()
    {
        RuleFor(query => query.PermissionId)
            .NotEmpty();
    }
}

internal sealed class ListIamPermissionsQueryValidator : AbstractValidator<ListIamPermissionsQuery>
{
    public ListIamPermissionsQueryValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.Search)
            .MaximumLength(100)
            .When(static query => !string.IsNullOrWhiteSpace(query.Search));
    }
}

internal sealed class CreateIamPermissionCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService)
    : ICommandHandler<CreateIamPermissionCommand, IamPermissionResponse>
{
    public async Task<Result<IamPermissionResponse>> Handle(
        CreateIamPermissionCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Create,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IamPermissionResponse>.Failure(authorizationResult.Error);
        }

        var permissionCode = string.IsNullOrWhiteSpace(command.Code)
            ? BuildPermissionCode(command)
            : command.Code.Trim();

        if (await repository.PermissionCodeExistsAsync(permissionCode.ToUpperInvariant(), cancellationToken))
        {
            return Result<IamPermissionResponse>.Failure(IdentityAccessErrors.PermissionAlreadyExists);
        }

        var permission = command.Kind == IamPermissionKind.ScreenAction
            ? IamPermission.CreateScreenAction(
                permissionCode,
                command.Name,
                command.Description,
                command.Module,
                command.Screen,
                command.Action!)
            : IamPermission.CreateFieldPermission(
                permissionCode,
                command.Name,
                command.Description,
                command.Module,
                command.Screen,
                command.FieldName!,
                command.FieldAccess!.Value);

        repository.AddPermission(permission);
        _ = await repository.SaveChangesAsync(cancellationToken);

        var createdPermission = await repository.GetPermissionAsync(permission.PublicId, cancellationToken);
        return createdPermission is null
            ? Result<IamPermissionResponse>.Failure(IdentityAccessErrors.PermissionNotFound)
            : Result<IamPermissionResponse>.Success(createdPermission);
    }

    private static string BuildPermissionCode(CreateIamPermissionCommand command)
    {
        var module = Slugify(command.Module);
        var screen = Slugify(command.Screen);

        return command.Kind == IamPermissionKind.ScreenAction
            ? $"{module}.{screen}.{Slugify(command.Action!)}"
            : $"{module}.{screen}.{Slugify(command.FieldName!)}.{command.FieldAccess!.Value.ToString().ToLowerInvariant()}";
    }

    private static string Slugify(string value)
    {
        var buffer = new List<char>(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer.Add(character);
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
            {
                continue;
            }

            if (character is ' ' or '-' or '_')
            {
                buffer.Add('-');
                previousWasSeparator = true;
            }
        }

        return new string(buffer.ToArray()).Trim('-');
    }
}

internal sealed class GetIamPermissionByIdQueryHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService)
    : IQueryHandler<GetIamPermissionByIdQuery, IamPermissionResponse>
{
    public async Task<Result<IamPermissionResponse>> Handle(
        GetIamPermissionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IamPermissionResponse>.Failure(authorizationResult.Error);
        }

        var permission = await repository.GetPermissionAsync(query.PermissionId, cancellationToken);
        return permission is null
            ? Result<IamPermissionResponse>.Failure(await PermissionAdministrationErrors.ResolvePermissionLookupErrorAsync(repository, query.PermissionId, RbacPermissionAction.Read, cancellationToken))
            : Result<IamPermissionResponse>.Success(permission);
    }
}

internal sealed class ListIamPermissionsQueryHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService)
    : IQueryHandler<ListIamPermissionsQuery, PagedResponse<IamPermissionSummaryResponse>>
{
    public async Task<Result<PagedResponse<IamPermissionSummaryResponse>>> Handle(
        ListIamPermissionsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<IamPermissionSummaryResponse>>.Failure(authorizationResult.Error);
        }

        var permissions = await repository.GetPermissionsAsync(query.PageNumber, query.PageSize, query.Search, cancellationToken);
        return Result<PagedResponse<IamPermissionSummaryResponse>>.Success(permissions);
    }

}

internal static class PermissionAdministrationErrors
{
    public static async Task<Error> ResolvePermissionLookupErrorAsync(
        IIamAdministrationRepository repository,
        Guid permissionId,
        RbacPermissionAction action,
        CancellationToken cancellationToken) =>
        await repository.PermissionPublicIdExistsAsync(permissionId, cancellationToken)
            ? AuthorizationErrors.TenantMismatch(PermissionMatrixCatalog.Get(RbacPermissionScreen.Permissions).ResourceKey, action)
            : IdentityAccessErrors.PermissionNotFound;
}
