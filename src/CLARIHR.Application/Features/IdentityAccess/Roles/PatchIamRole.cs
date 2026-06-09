using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using FluentValidation;

namespace CLARIHR.Application.Features.IdentityAccess.Roles;

public sealed record IamRolePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchIamRoleCommand(
    Guid RoleId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<IamRolePatchOperation> Operations) : ICommand<IamRoleResponse>;

internal sealed class PatchIamRoleCommandValidator : AbstractValidator<PatchIamRoleCommand>
{
    public PatchIamRoleCommandValidator()
    {
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class PatchIamRoleCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    IAuditService auditService)
    : ICommandHandler<PatchIamRoleCommand, IamRoleResponse>
{
    public async Task<Result<IamRoleResponse>> Handle(PatchIamRoleCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Roles,
            RbacPermissionAction.Update,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IamRoleResponse>.Failure(authorizationResult.Error);
        }

        var role = await repository.FindRoleByPublicIdAsync(command.RoleId, includePermissions: true, cancellationToken);
        if (role is null)
        {
            return Result<IamRoleResponse>.Failure(await RoleAdministrationErrors.ResolveRoleLookupErrorAsync(
                repository,
                command.RoleId,
                RbacPermissionAction.Update,
                cancellationToken));
        }

        if (role.IsSystemRole)
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.ProtectedRoleModificationForbidden);
        }

        // Optimistic concurrency: the strong token travels in the If-Match header (validator enforces
        // NotEmpty, so a stale/blank token never silently bypasses this guard).
        if (role.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.ConcurrencyConflict);
        }

        var state = new IamRolePatchState();
        var applied = IamRolePatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<IamRoleResponse>.Failure(applied.Error);
        }

        var validation = IamRolePatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<IamRoleResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            // No patchable field was touched: skip the write so the token is not rotated and no spurious
            // audit entry is emitted (mirrors the canonical AccountCompany PATCH handler).
            var current = await repository.GetRoleAsync(role.PublicId, cancellationToken);
            return current is null
                ? Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleNotFound)
                : Result<IamRoleResponse>.Success(current);
        }

        // IamRole.UpdateDetails is a combined setter, so resolve the effective values: a field the patch
        // did not touch must round-trip its current value rather than being wiped.
        var effectiveName = state.NameSet ? state.Name : role.Name;
        var effectiveDescription = state.DescriptionSet ? state.Description : role.Description;

        var normalizedName = Normalize(effectiveName);
        if (!string.Equals(role.NormalizedName, normalizedName, StringComparison.Ordinal) &&
            await repository.RoleNameExistsAsync(normalizedName, cancellationToken))
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleAlreadyExists);
        }

        var beforeName = role.Name;
        var beforeDescription = role.Description;
        role.UpdateDetails(effectiveName, effectiveDescription);
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.RoleUpdated,
                AuditEntityTypes.Role,
                role.PublicId,
                EntityKey: role.Name,
                AuditActions.Update,
                $"Updated role {role.Name}.",
                IdentityAccessAuditMapper.CreateRoleSnapshot(
                    role.PublicId,
                    beforeName,
                    beforeDescription,
                    role.IsSystemRole,
                    role.PermissionAssignments.Select(static assignment => assignment.Permission)),
                IdentityAccessAuditMapper.CreateRoleSnapshot(
                    role,
                    role.PermissionAssignments.Select(static assignment => assignment.Permission)),
                IdentityAccessAuditMapper.CreateRoleDiff(beforeName, role.Name, beforeDescription, role.Description)),
            cancellationToken);
        _ = await repository.SaveChangesAsync(cancellationToken);

        var updatedRole = await repository.GetRoleAsync(role.PublicId, cancellationToken);
        return updatedRole is null
            ? Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleNotFound)
            : Result<IamRoleResponse>.Success(updatedRole);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}

internal sealed class IamRolePatchState
{
    public string Name { get; set; } = string.Empty;
    public bool NameSet { get; set; }
    public string? Description { get; set; }
    public bool DescriptionSet { get; set; }

    public bool HasMutation => NameSet || DescriptionSet;
}

internal sealed class IamRolePatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class IamRolePatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<IamRolePatchOperation> operations, IamRolePatchState state)
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
                return ValidationFailure(operation.Path, "Only root role properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (IamRolePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(IamRolePatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Mirror the PUT validator + the domain normalizer (IamRole.UpdateDetails → Clean trims then
        // requires non-empty). The DB column is varchar(100), so a name that trims longer than 100 would
        // otherwise throw on save → HTTP 500; validate the trimmed length here as a 400.
        if (state.NameSet)
        {
            if (string.IsNullOrWhiteSpace(state.Name))
            {
                errors["name"] = ["Name is required."];
            }
            else if (state.Name.Trim().Length > 100)
            {
                errors["name"] = ["Name must be 100 characters or fewer."];
            }
        }

        if (state.DescriptionSet &&
            !string.IsNullOrEmpty(state.Description) &&
            state.Description.Trim().Length > 500)
        {
            errors["description"] = ["Description must be 500 characters or fewer."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        IamRolePatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "concurrencyToken"))
        {
            return ValidationFailure(path, "The concurrency token cannot be patched; send the current token in the If-Match header.");
        }

        // System-role protection and grant changes go through the dedicated guards / grants endpoint,
        // not this patch.
        if (IsSegment(property, "id") || IsSegment(property, "publicId") ||
            IsSegment(property, "isSystemRole") || IsSegment(property, "userCount") ||
            IsSegment(property, "grants") || IsSegment(property, "permissions") ||
            IsSegment(property, "permissionIds"))
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

        if (IsSegment(property, "description"))
        {
            // The description is optional, so a remove (or an explicit null) clears it.
            state.Description = isRemove ? null : ReadOptionalString(value, path);
            state.DescriptionSet = true;
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
            throw new IamRolePatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new IamRolePatchValueException(path, "Value must be a string.");
    }

    private static string? ReadOptionalString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new IamRolePatchValueException(path, "Value must be a string or null.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}
