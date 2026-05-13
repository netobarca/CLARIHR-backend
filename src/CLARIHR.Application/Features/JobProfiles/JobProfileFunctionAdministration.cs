using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using FluentValidation;
using static CLARIHR.Application.Features.JobProfiles.JobProfileFunctionCommandSupport;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record GetJobProfileFunctionsQuery(Guid JobProfileId)
    : IQuery<IReadOnlyCollection<JobProfileFunctionResponse>>;

public sealed record AddJobProfileFunctionCommand(
    Guid JobProfileId,
    JobFunctionType FunctionType,
    Guid? FrequencyCatalogItemPublicId,
    string Description,
    int SortOrder) : ICommand<JobProfileFunctionResponse>;

public sealed record UpdateJobProfileFunctionCommand(
    Guid JobProfileId,
    Guid FunctionId,
    JobFunctionType FunctionType,
    Guid? FrequencyCatalogItemPublicId,
    string Description,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileFunctionResponse>;

public sealed record JobProfileFunctionPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileFunctionCommand(
    Guid JobProfileId,
    Guid FunctionId,
    IReadOnlyCollection<JobProfileFunctionPatchOperation> Operations) : ICommand<JobProfileFunctionResponse>;

public sealed record RemoveJobProfileFunctionCommand(
    Guid JobProfileId,
    Guid FunctionId,
    Guid ConcurrencyToken) : ICommand<JobProfileFunctionResponse>;

internal sealed class GetJobProfileFunctionsQueryValidator : AbstractValidator<GetJobProfileFunctionsQuery>
{
    public GetJobProfileFunctionsQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class AddJobProfileFunctionCommandValidator : AbstractValidator<AddJobProfileFunctionCommand>
{
    public AddJobProfileFunctionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.FunctionType).IsInEnum();
        RuleFor(command => command.FrequencyCatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.FrequencyCatalogItemPublicId.HasValue);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(2000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateJobProfileFunctionCommandValidator : AbstractValidator<UpdateJobProfileFunctionCommand>
{
    public UpdateJobProfileFunctionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.FunctionId).NotEmpty();
        RuleFor(command => command.FunctionType).IsInEnum();
        RuleFor(command => command.FrequencyCatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.FrequencyCatalogItemPublicId.HasValue);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(2000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchJobProfileFunctionCommandValidator : AbstractValidator<PatchJobProfileFunctionCommand>
{
    public PatchJobProfileFunctionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.FunctionId).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(ContainsConcurrencyToken)
            .WithMessage("Patch document must include a non-remove operation for /concurrencyToken.");
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }

    private static bool ContainsConcurrencyToken(IReadOnlyCollection<JobProfileFunctionPatchOperation>? operations) =>
        operations is not null &&
        operations.Any(static operation =>
            !string.Equals(operation.Op, "remove", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(operation.Path) &&
            string.Equals(operation.Path.Trim(), "/concurrencyToken", StringComparison.OrdinalIgnoreCase));
}

internal sealed class RemoveJobProfileFunctionCommandValidator : AbstractValidator<RemoveJobProfileFunctionCommand>
{
    public RemoveJobProfileFunctionCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.FunctionId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetJobProfileFunctionsQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileFunctionsQuery, IReadOnlyCollection<JobProfileFunctionResponse>>
{
    public async Task<Result<IReadOnlyCollection<JobProfileFunctionResponse>>> Handle(GetJobProfileFunctionsQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IReadOnlyCollection<JobProfileFunctionResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<JobProfileFunctionResponse>>.Failure(authorizationResult.Error);
        }

        var functions = await repository.GetFunctionResponsesByProfileIdAsync(query.JobProfileId, cancellationToken);
        if (functions is null)
        {
            return Result<IReadOnlyCollection<JobProfileFunctionResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.JobProfileNotFound);
        }

        return Result<IReadOnlyCollection<JobProfileFunctionResponse>>.Success(functions);
    }
}

internal sealed class AddJobProfileFunctionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository)
    : ICommandHandler<AddJobProfileFunctionCommand, JobProfileFunctionResponse>
{
    public async Task<Result<JobProfileFunctionResponse>> Handle(AddJobProfileFunctionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileFunctionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileFunctionResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithFunctionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileFunctionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var frequencyInternalIdResult = await ResolveFrequencyInternalIdAsync(
            tenantContext.TenantId.Value,
            command.FrequencyCatalogItemPublicId,
            positionDescriptionCatalogRepository,
            cancellationToken);

        if (frequencyInternalIdResult.IsFailure)
        {
            return Result<JobProfileFunctionResponse>.Failure(frequencyInternalIdResult.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var function = JobProfileFunction.Create(
                command.FunctionType,
                frequencyInternalIdResult.Value,
                command.Description,
                command.SortOrder);

            profile.AddFunction(function);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetFunctionResponseAsync(profile.PublicId, function.PublicId, cancellationToken)
                ?? function.ToResponse(command.FrequencyCatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added function to job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileFunctionResponse>.Success(response);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileFunctionResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobProfileFunctionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository)
    : ICommandHandler<UpdateJobProfileFunctionCommand, JobProfileFunctionResponse>
{
    public async Task<Result<JobProfileFunctionResponse>> Handle(UpdateJobProfileFunctionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileFunctionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileFunctionResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithFunctionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileFunctionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var function = profile.Functions.FirstOrDefault(item => item.PublicId == command.FunctionId);
        if (function is null)
        {
            return Result<JobProfileFunctionResponse>.Failure(JobProfileErrors.FunctionNotFound);
        }

        if (function.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileFunctionResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var frequencyInternalIdResult = await ResolveFrequencyInternalIdAsync(
            tenantContext.TenantId.Value,
            command.FrequencyCatalogItemPublicId,
            positionDescriptionCatalogRepository,
            cancellationToken);

        if (frequencyInternalIdResult.IsFailure)
        {
            return Result<JobProfileFunctionResponse>.Failure(frequencyInternalIdResult.Error);
        }

        var before = await repository.GetFunctionResponseAsync(profile.PublicId, function.PublicId, cancellationToken)
            ?? function.ToResponse(command.FrequencyCatalogItemPublicId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            function.Update(
                command.FunctionType,
                frequencyInternalIdResult.Value,
                command.Description,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetFunctionResponseAsync(profile.PublicId, function.PublicId, cancellationToken)
                ?? function.ToResponse(command.FrequencyCatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated function in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileFunctionResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileFunctionResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchJobProfileFunctionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository)
    : ICommandHandler<PatchJobProfileFunctionCommand, JobProfileFunctionResponse>
{
    public async Task<Result<JobProfileFunctionResponse>> Handle(PatchJobProfileFunctionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileFunctionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileFunctionResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithFunctionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileFunctionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var function = profile.Functions.FirstOrDefault(item => item.PublicId == command.FunctionId);
        if (function is null)
        {
            return Result<JobProfileFunctionResponse>.Failure(JobProfileErrors.FunctionNotFound);
        }

        var before = await repository.GetFunctionResponseAsync(profile.PublicId, function.PublicId, cancellationToken)
            ?? function.ToResponse(null);
        var patchState = JobProfileFunctionPatchState.From(before);
        var patchApplication = JobProfileFunctionPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<JobProfileFunctionResponse>.Failure(patchApplication.Error);
        }

        var validation = JobProfileFunctionPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<JobProfileFunctionResponse>.Failure(validation.Error);
        }

        if (patchState.ConcurrencyToken != function.ConcurrencyToken)
        {
            return Result<JobProfileFunctionResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        if (!patchState.HasMutation)
        {
            return Result<JobProfileFunctionResponse>.Success(before);
        }

        var frequencyInternalIdResult = await ResolveFrequencyInternalIdAsync(
            tenantContext.TenantId.Value,
            patchState.FrequencyCatalogItemPublicId,
            positionDescriptionCatalogRepository,
            cancellationToken);

        if (frequencyInternalIdResult.IsFailure)
        {
            return Result<JobProfileFunctionResponse>.Failure(frequencyInternalIdResult.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            function.Update(
                patchState.FunctionType,
                frequencyInternalIdResult.Value,
                patchState.Description,
                patchState.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetFunctionResponseAsync(profile.PublicId, function.PublicId, cancellationToken)
                ?? function.ToResponse(patchState.FrequencyCatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Patched function in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileFunctionResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileFunctionResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobProfileFunctionCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileFunctionCommand, JobProfileFunctionResponse>
{
    public async Task<Result<JobProfileFunctionResponse>> Handle(RemoveJobProfileFunctionCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileFunctionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileFunctionResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithFunctionsOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileFunctionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var function = profile.Functions.FirstOrDefault(item => item.PublicId == command.FunctionId);
        if (function is null)
        {
            return Result<JobProfileFunctionResponse>.Failure(JobProfileErrors.FunctionNotFound);
        }

        if (function.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileFunctionResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetFunctionResponseAsync(profile.PublicId, function.PublicId, cancellationToken)
            ?? function.ToResponse(null);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.RemoveFunction(function);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed function from job profile {profile.Code}.",
                    Before: before),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileFunctionResponse>.Success(before);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileFunctionResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class JobProfileFunctionPatchState
{
    public JobFunctionType FunctionType { get; set; }
    public Guid? FrequencyCatalogItemPublicId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public bool ConcurrencyTokenTouched { get; set; }
    public bool HasMutation { get; set; }

    public static JobProfileFunctionPatchState From(JobProfileFunctionResponse response) =>
        new()
        {
            FunctionType = response.FunctionType,
            FrequencyCatalogItemPublicId = response.FrequencyCatalogItemPublicId,
            Description = response.Description,
            SortOrder = response.SortOrder,
            ConcurrencyToken = response.ConcurrencyToken
        };
}

internal static class JobProfileFunctionPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfileFunctionPatchOperation> operations, JobProfileFunctionPatchState state)
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
                return ValidationFailure(operation.Path, "Only root function properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (JobProfilePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(JobProfileFunctionPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!state.ConcurrencyTokenTouched)
        {
            errors["concurrencyToken"] = ["ConcurrencyToken is required."];
        }
        else if (state.ConcurrencyToken == Guid.Empty)
        {
            errors["concurrencyToken"] = ["ConcurrencyToken must be a valid UUID."];
        }

        if (state.FrequencyCatalogItemPublicId == Guid.Empty)
        {
            errors["frequencyCatalogItemPublicId"] = ["FrequencyCatalogItemPublicId must be a valid UUID."];
        }

        if (string.IsNullOrWhiteSpace(state.Description))
        {
            errors["description"] = ["Description is required."];
        }
        else if (state.Description.Length > 2000)
        {
            errors["description"] = ["Description must be 2000 characters or fewer."];
        }

        if (state.SortOrder < 0)
        {
            errors["sortOrder"] = ["SortOrder must be greater than or equal to 0."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        JobProfileFunctionPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "functionType"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "FunctionType cannot be removed.");
            }

            state.FunctionType = ReadFunctionType(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsAnySegment(property, "frequencyCatalogItemPublicId", "frequencyCatalogItemId"))
        {
            state.FrequencyCatalogItemPublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "description"))
        {
            state.Description = isRemove ? string.Empty : ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "sortOrder"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "SortOrder cannot be removed.");
            }

            state.SortOrder = ReadInt(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "concurrencyToken"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "ConcurrencyToken cannot be removed.");
            }

            state.ConcurrencyToken = ReadRequiredGuid(value, path);
            state.ConcurrencyTokenTouched = true;
            return Result.Success();
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static JobFunctionType ReadFunctionType(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new JobProfilePatchValueException(path, "FunctionType is required.");
        }

        if (value!.Value.ValueKind == JsonValueKind.String)
        {
            var raw = value.Value.GetString();
            return Enum.TryParse<JobFunctionType>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(typeof(JobFunctionType), parsed)
                ? parsed
                : throw new JobProfilePatchValueException(path, $"FunctionType '{raw}' is not a valid value.");
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var numeric))
        {
            var parsed = (JobFunctionType)numeric;
            return Enum.IsDefined(typeof(JobFunctionType), parsed)
                ? parsed
                : throw new JobProfilePatchValueException(path, $"FunctionType '{numeric}' is not a valid value.");
        }

        throw new JobProfilePatchValueException(path, "FunctionType value must be a string or integer.");
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

    private static bool IsAnySegment(string actual, params string[] expected) =>
        expected.Any(item => IsSegment(actual, item));

    private static string ReadRequiredString(JsonElement? value, string path) =>
        ReadNullableString(value, path) ?? string.Empty;

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new JobProfilePatchValueException(path, "Value must be a string or null.");
    }

    private static Guid ReadRequiredGuid(JsonElement? value, string path) =>
        ReadNullableGuid(value, path) ?? Guid.Empty;

    private static Guid? ReadNullableGuid(JsonElement? value, string path)
    {
        var raw = ReadNullableString(value, path);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return Guid.TryParse(raw, out var parsed)
            ? parsed
            : throw new JobProfilePatchValueException(path, "Value must be a valid UUID.");
    }

    private static int ReadInt(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new JobProfilePatchValueException(path, "Value must be an integer.");
        }

        if (value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var parsed))
        {
            return parsed;
        }

        var raw = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out parsed))
        {
            return parsed;
        }

        throw new JobProfilePatchValueException(path, "Value must be an integer.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

internal static class JobProfileFunctionCommandSupport
{
    public static async Task<Result<long?>> ResolveFrequencyInternalIdAsync(
        Guid tenantId,
        Guid? frequencyCatalogItemPublicId,
        IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
        CancellationToken cancellationToken) =>
        await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            tenantId,
            frequencyCatalogItemPublicId,
            PositionDescriptionCatalogType.Frequency,
            PositionDescriptionCatalogErrors.FrequencyNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);
}
