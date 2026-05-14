using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record JobProfileCompensationItemResponse(
    Guid Id,
    Guid SalaryTabulatorLineId,
    string SalaryClassCode,
    string SalaryScaleCode,
    string CurrencyCode,
    decimal BaseAmount,
    decimal? MinAmount,
    decimal? MaxAmount,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record GetJobProfileCompensationsQuery(Guid JobProfileId)
    : IQuery<IReadOnlyCollection<JobProfileCompensationItemResponse>>;

public sealed record AddJobProfileCompensationCommand(
    Guid JobProfileId,
    Guid SalaryTabulatorLineId,
    string? Notes) : ICommand<JobProfileCompensationItemResponse>;

public sealed record UpdateJobProfileCompensationCommand(
    Guid JobProfileId,
    Guid CompensationId,
    Guid SalaryTabulatorLineId,
    string? Notes,
    Guid ConcurrencyToken) : ICommand<JobProfileCompensationItemResponse>;

public sealed record JobProfileCompensationPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileCompensationCommand(
    Guid JobProfileId,
    Guid CompensationId,
    IReadOnlyCollection<JobProfileCompensationPatchOperation> Operations) : ICommand<JobProfileCompensationItemResponse>;

public sealed record RemoveJobProfileCompensationCommand(
    Guid JobProfileId,
    Guid CompensationId,
    Guid ConcurrencyToken) : ICommand<JobProfileCompensationItemResponse>;

internal sealed class GetJobProfileCompensationsQueryValidator : AbstractValidator<GetJobProfileCompensationsQuery>
{
    public GetJobProfileCompensationsQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class AddJobProfileCompensationCommandValidator : AbstractValidator<AddJobProfileCompensationCommand>
{
    public AddJobProfileCompensationCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.SalaryTabulatorLineId).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(1000);
    }
}

internal sealed class UpdateJobProfileCompensationCommandValidator : AbstractValidator<UpdateJobProfileCompensationCommand>
{
    public UpdateJobProfileCompensationCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CompensationId).NotEmpty();
        RuleFor(command => command.SalaryTabulatorLineId).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchJobProfileCompensationCommandValidator : AbstractValidator<PatchJobProfileCompensationCommand>
{
    public PatchJobProfileCompensationCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CompensationId).NotEmpty();
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

    private static bool ContainsConcurrencyToken(IReadOnlyCollection<JobProfileCompensationPatchOperation>? operations) =>
        operations is not null &&
        operations.Any(static operation =>
            !string.Equals(operation.Op, "remove", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(operation.Path) &&
            string.Equals(operation.Path.Trim(), "/concurrencyToken", StringComparison.OrdinalIgnoreCase));
}

internal sealed class RemoveJobProfileCompensationCommandValidator : AbstractValidator<RemoveJobProfileCompensationCommand>
{
    public RemoveJobProfileCompensationCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CompensationId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetJobProfileCompensationsQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository profileRepository,
    IJobProfileCompensationRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileCompensationsQuery, IReadOnlyCollection<JobProfileCompensationItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<JobProfileCompensationItemResponse>>> Handle(GetJobProfileCompensationsQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IReadOnlyCollection<JobProfileCompensationItemResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<JobProfileCompensationItemResponse>>.Failure(authorizationResult.Error);
        }

        var items = await repository.GetResponsesByProfileIdAsync(query.JobProfileId, cancellationToken);
        if (items is null)
        {
            return Result<IReadOnlyCollection<JobProfileCompensationItemResponse>>.Failure(
                await profileRepository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.JobProfileNotFound);
        }

        return Result<IReadOnlyCollection<JobProfileCompensationItemResponse>>.Success(items);
    }
}

internal sealed class AddJobProfileCompensationCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository profileRepository,
    IJobProfileCompensationRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddJobProfileCompensationCommand, JobProfileCompensationItemResponse>
{
    public async Task<Result<JobProfileCompensationItemResponse>> Handle(AddJobProfileCompensationCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(authorizationResult.Error);
        }

        var profileInternalId = await repository.ResolveJobProfileInternalIdAsync(tenantContext.TenantId.Value, command.JobProfileId, cancellationToken);
        if (!profileInternalId.HasValue)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(
                await profileRepository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (await repository.ProfileHasCompensationAsync(profileInternalId.Value, cancellationToken))
        {
            return Result<JobProfileCompensationItemResponse>.Failure(JobProfileErrors.CompensationAlreadyExists);
        }

        var line = await repository.ResolveSalaryTabulatorLineAsync(tenantContext.TenantId.Value, command.SalaryTabulatorLineId, cancellationToken);
        if (line is null)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(JobProfileErrors.SalaryTabulatorLineNotFoundForCompensation);
        }

        if (!line.IsActive)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(JobProfileErrors.SalaryTabulatorLineInactiveForCompensation);
        }

        var compensation = JobProfileCompensation.Create(profileInternalId.Value, line.Id, command.Notes);
        compensation.SetTenantId(tenantContext.TenantId.Value);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(compensation);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseAsync(command.JobProfileId, compensation.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile compensation response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileCompensationCreated,
                    AuditEntityTypes.JobProfileCompensation,
                    compensation.PublicId,
                    line.SalaryClassCode,
                    AuditActions.Create,
                    $"Added compensation to job profile {command.JobProfileId:D}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCompensationItemResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobProfileCompensationCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository profileRepository,
    IJobProfileCompensationRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateJobProfileCompensationCommand, JobProfileCompensationItemResponse>
{
    public async Task<Result<JobProfileCompensationItemResponse>> Handle(UpdateJobProfileCompensationCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(authorizationResult.Error);
        }

        var compensation = await repository.GetByPublicIdAsync(command.JobProfileId, command.CompensationId, cancellationToken);
        if (compensation is null)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(
                await profileRepository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.CompensationNotFound);
        }

        if (compensation.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var line = await repository.ResolveSalaryTabulatorLineAsync(tenantContext.TenantId.Value, command.SalaryTabulatorLineId, cancellationToken);
        if (line is null)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(JobProfileErrors.SalaryTabulatorLineNotFoundForCompensation);
        }

        if (!line.IsActive)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(JobProfileErrors.SalaryTabulatorLineInactiveForCompensation);
        }

        var before = await repository.GetResponseAsync(command.JobProfileId, compensation.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            compensation.Update(line.Id, command.Notes);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseAsync(command.JobProfileId, compensation.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile compensation response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileCompensationUpdated,
                    AuditEntityTypes.JobProfileCompensation,
                    compensation.PublicId,
                    line.SalaryClassCode,
                    AuditActions.Update,
                    $"Updated compensation in job profile {command.JobProfileId:D}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCompensationItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchJobProfileCompensationCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository profileRepository,
    IJobProfileCompensationRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchJobProfileCompensationCommand, JobProfileCompensationItemResponse>
{
    public async Task<Result<JobProfileCompensationItemResponse>> Handle(PatchJobProfileCompensationCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(authorizationResult.Error);
        }

        var compensation = await repository.GetByPublicIdAsync(command.JobProfileId, command.CompensationId, cancellationToken);
        if (compensation is null)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(
                await profileRepository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.CompensationNotFound);
        }

        var before = await repository.GetResponseAsync(command.JobProfileId, compensation.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile compensation response could not be resolved before patch.");
        var patchState = JobProfileCompensationPatchState.From(before);
        var patchApplication = JobProfileCompensationPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(patchApplication.Error);
        }

        var validation = JobProfileCompensationPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(validation.Error);
        }

        if (patchState.ConcurrencyToken != compensation.ConcurrencyToken)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        if (!patchState.HasMutation)
        {
            return Result<JobProfileCompensationItemResponse>.Success(before);
        }

        var line = await repository.ResolveSalaryTabulatorLineAsync(tenantContext.TenantId.Value, patchState.SalaryTabulatorLinePublicId, cancellationToken);
        if (line is null)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(JobProfileErrors.SalaryTabulatorLineNotFoundForCompensation);
        }

        if (!line.IsActive)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(JobProfileErrors.SalaryTabulatorLineInactiveForCompensation);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            compensation.Update(line.Id, patchState.Notes);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseAsync(command.JobProfileId, compensation.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile compensation response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileCompensationUpdated,
                    AuditEntityTypes.JobProfileCompensation,
                    compensation.PublicId,
                    line.SalaryClassCode,
                    AuditActions.Update,
                    $"Patched compensation in job profile {command.JobProfileId:D}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCompensationItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobProfileCompensationCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository profileRepository,
    IJobProfileCompensationRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileCompensationCommand, JobProfileCompensationItemResponse>
{
    public async Task<Result<JobProfileCompensationItemResponse>> Handle(RemoveJobProfileCompensationCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(authorizationResult.Error);
        }

        var compensation = await repository.GetByPublicIdAsync(command.JobProfileId, command.CompensationId, cancellationToken);
        if (compensation is null)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(
                await profileRepository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.CompensationNotFound);
        }

        if (compensation.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileCompensationItemResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseAsync(command.JobProfileId, compensation.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile compensation response could not be resolved before delete.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Remove(compensation);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileCompensationDeleted,
                    AuditEntityTypes.JobProfileCompensation,
                    compensation.PublicId,
                    before.SalaryClassCode,
                    AuditActions.Update,
                    $"Removed compensation from job profile {command.JobProfileId:D}.",
                    Before: before),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCompensationItemResponse>.Success(before);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class JobProfileCompensationPatchState
{
    public Guid SalaryTabulatorLinePublicId { get; set; }
    public string? Notes { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public bool ConcurrencyTokenTouched { get; set; }
    public bool HasMutation { get; set; }

    public static JobProfileCompensationPatchState From(JobProfileCompensationItemResponse response) =>
        new()
        {
            SalaryTabulatorLinePublicId = response.SalaryTabulatorLineId,
            Notes = response.Notes,
            ConcurrencyToken = response.ConcurrencyToken
        };
}

internal static class JobProfileCompensationPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfileCompensationPatchOperation> operations, JobProfileCompensationPatchState state)
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
                return ValidationFailure(operation.Path, "Only root compensation properties can be patched.");
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

    public static Result Validate(JobProfileCompensationPatchState state)
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

        if (state.SalaryTabulatorLinePublicId == Guid.Empty)
        {
            errors["salaryTabulatorLineId"] = ["SalaryTabulatorLineId is required."];
        }

        if (state.Notes is not null && state.Notes.Length > 1000)
        {
            errors["notes"] = ["Notes must be 1000 characters or fewer."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        JobProfileCompensationPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsAnySegment(property, "salaryTabulatorLineId", "salaryTabulatorLinePublicId"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "SalaryTabulatorLineId cannot be removed.");
            }

            state.SalaryTabulatorLinePublicId = ReadRequiredGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "notes"))
        {
            state.Notes = isRemove ? null : ReadNullableString(value, path);
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

    private static Guid ReadRequiredGuid(JsonElement? value, string path)
    {
        var raw = ReadNullableString(value, path);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JobProfilePatchValueException(path, "Value must be a valid UUID.");
        }

        return Guid.TryParse(raw, out var parsed)
            ? parsed
            : throw new JobProfilePatchValueException(path, "Value must be a valid UUID.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}
