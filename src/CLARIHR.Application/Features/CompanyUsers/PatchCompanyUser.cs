using System.Text.Json;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.CompanyUsers;

// ─── Command ─────────────────────────────────────────────────────────────────

public sealed record CompanyUserPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchCompanyUserCommand(
    Guid UserId,
    IReadOnlyCollection<CompanyUserPatchOperation> Operations,
    string? ExpectedETag = null) : ICommand<CompanyUserResponse>;

internal sealed class PatchCompanyUserCommandValidator : AbstractValidator<PatchCompanyUserCommand>
{
    public PatchCompanyUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
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

// ─── Handler ─────────────────────────────────────────────────────────────────

// The CompanyUser resource is a read projection over three aggregates (User/auth_users +
// UserCompanyMembership + IamUser) guarded by a WEAK computed ETag (no persisted token). This PATCH
// only resolves the partial change into the full set of editable values and then delegates to the
// canonical Update path: the dispatcher re-validates the merged command with
// UpdateCompanyUserCommandValidator and UpdateCompanyUserCommandHandler performs role validation, the
// last-active-administrator guard, field-level authorization, IamUser sync, audit and the weak-ETag
// (re)check + recompute. PUT and PATCH therefore share one mutation core and can never drift.
internal sealed class PatchCompanyUserCommandHandler(
    IUserCompanyRepository userCompanyRepository,
    ITenantContext tenantContext,
    ICommandDispatcher commandDispatcher)
    : ICommandHandler<PatchCompanyUserCommand, CompanyUserResponse>
{
    public async Task<Result<CompanyUserResponse>> Handle(
        PatchCompanyUserCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.TenantContextRequired);
        }

        var companyPublicId = tenantContext.TenantId.Value;
        var current = await userCompanyRepository.GetUserAsync(companyPublicId, command.UserId, cancellationToken);
        if (current is null)
        {
            // Mirror the Update handler's not-found / cross-tenant disambiguation so PATCH and PUT agree.
            return await userCompanyRepository.UserExistsOutsideCompanyAsync(companyPublicId, command.UserId, cancellationToken)
                ? Result<CompanyUserResponse>.Failure(AuthorizationErrors.TenantMismatch(CompanyUserFieldKeys.ResourceKey, RbacPermissionAction.Update))
                : Result<CompanyUserResponse>.Failure(CompanyUserErrors.UserNotFound);
        }

        var state = CompanyUserPatchState.From(current);
        var applied = CompanyUserPatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<CompanyUserResponse>.Failure(applied.Error);
        }

        var update = new UpdateCompanyUserCommand(
            command.UserId,
            state.FirstName,
            state.LastName,
            state.RolePublicIds,
            command.ExpectedETag);

        return await commandDispatcher.SendAsync(update, cancellationToken);
    }
}

// ─── Patch state + applier ─────────────────────────────────────────────────────

internal sealed class CompanyUserPatchState
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<Guid> RolePublicIds { get; set; } = [];

    public static CompanyUserPatchState From(CompanyUserResponse current) =>
        new()
        {
            FirstName = current.FirstName ?? string.Empty,
            LastName = current.LastName ?? string.Empty,
            RolePublicIds = current.Roles.Select(static role => role.Id).ToList()
        };
}

internal sealed class CompanyUserPatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class CompanyUserPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<CompanyUserPatchOperation> operations, CompanyUserPatchState state)
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
                return ValidationFailure(operation.Path, "Only root company-user properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (CompanyUserPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        CompanyUserPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        // Activation state changes go through the dedicated /deactivate and /reactivate actions.
        if (IsSegment(property, "status") || IsSegment(property, "isActive"))
        {
            return ValidationFailure(path, "Use the /deactivate and /reactivate actions to change the active state.");
        }

        // The e-mail address is fixed at invitation time and is not editable.
        if (IsSegment(property, "email"))
        {
            return ValidationFailure(path, "The e-mail address cannot be patched.");
        }

        if (IsSegment(property, "id") || IsSegment(property, "publicId") || IsSegment(property, "roles"))
        {
            return ValidationFailure(path, "This property is read-only and cannot be patched.");
        }

        if (IsSegment(property, "firstName"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "First name cannot be removed.");
            }

            state.FirstName = ReadRequiredString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "lastName"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Last name cannot be removed.");
            }

            state.LastName = ReadRequiredString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "rolePublicIds"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Roles cannot be removed; send the full set with a replace operation.");
            }

            state.RolePublicIds = ReadRequiredGuidArray(value, path);
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
            throw new CompanyUserPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new CompanyUserPatchValueException(path, "Value must be a string.");
    }

    private static List<Guid> ReadRequiredGuidArray(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new CompanyUserPatchValueException(path, "Value is required.");
        }

        if (value!.Value.ValueKind != JsonValueKind.Array)
        {
            throw new CompanyUserPatchValueException(path, "Value must be an array of role ids.");
        }

        var ids = new List<Guid>();
        foreach (var element in value.Value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String || !Guid.TryParse(element.GetString(), out var id))
            {
                throw new CompanyUserPatchValueException(path, "Each role id must be a GUID string.");
            }

            ids.Add(id);
        }

        return ids;
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}
