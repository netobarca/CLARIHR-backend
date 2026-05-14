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

public sealed record GetJobProfileCompetenciesQuery(Guid JobProfileId)
    : IQuery<IReadOnlyCollection<JobProfileLegacyCompetencyResponse>>;

public sealed record AddJobProfileCompetencyCommand(
    Guid JobProfileId,
    Guid? CatalogItemPublicId,
    string? Name,
    string? ExpectedLevel,
    string? Notes,
    int SortOrder) : ICommand<JobProfileLegacyCompetencyResponse>;

public sealed record UpdateJobProfileCompetencyCommand(
    Guid JobProfileId,
    Guid CompetencyId,
    Guid? CatalogItemPublicId,
    string? Name,
    string? ExpectedLevel,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken) : ICommand<JobProfileLegacyCompetencyResponse>;

public sealed record JobProfileCompetencyPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileCompetencyCommand(
    Guid JobProfileId,
    Guid CompetencyId,
    IReadOnlyCollection<JobProfileCompetencyPatchOperation> Operations) : ICommand<JobProfileLegacyCompetencyResponse>;

public sealed record RemoveJobProfileCompetencyCommand(
    Guid JobProfileId,
    Guid CompetencyId,
    Guid ConcurrencyToken) : ICommand<JobProfileLegacyCompetencyResponse>;

internal sealed class GetJobProfileCompetenciesQueryValidator : AbstractValidator<GetJobProfileCompetenciesQuery>
{
    public GetJobProfileCompetenciesQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class AddJobProfileCompetencyCommandValidator : AbstractValidator<AddJobProfileCompetencyCommand>
{
    public AddJobProfileCompetencyCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemPublicId.HasValue);
        RuleFor(command => command.Name).MaximumLength(300);
        RuleFor(command => command.ExpectedLevel).MaximumLength(150);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateJobProfileCompetencyCommandValidator : AbstractValidator<UpdateJobProfileCompetencyCommand>
{
    public UpdateJobProfileCompetencyCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CompetencyId).NotEmpty();
        RuleFor(command => command.CatalogItemPublicId)
            .NotEqual(Guid.Empty)
            .When(static command => command.CatalogItemPublicId.HasValue);
        RuleFor(command => command.Name).MaximumLength(300);
        RuleFor(command => command.ExpectedLevel).MaximumLength(150);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchJobProfileCompetencyCommandValidator : AbstractValidator<PatchJobProfileCompetencyCommand>
{
    public PatchJobProfileCompetencyCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CompetencyId).NotEmpty();
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

    private static bool ContainsConcurrencyToken(IReadOnlyCollection<JobProfileCompetencyPatchOperation>? operations) =>
        operations is not null &&
        operations.Any(static operation =>
            !string.Equals(operation.Op, "remove", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(operation.Path) &&
            string.Equals(operation.Path.Trim(), "/concurrencyToken", StringComparison.OrdinalIgnoreCase));
}

internal sealed class RemoveJobProfileCompetencyCommandValidator : AbstractValidator<RemoveJobProfileCompetencyCommand>
{
    public RemoveJobProfileCompetencyCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.CompetencyId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetJobProfileCompetenciesQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileCompetenciesQuery, IReadOnlyCollection<JobProfileLegacyCompetencyResponse>>
{
    public async Task<Result<IReadOnlyCollection<JobProfileLegacyCompetencyResponse>>> Handle(GetJobProfileCompetenciesQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IReadOnlyCollection<JobProfileLegacyCompetencyResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<JobProfileLegacyCompetencyResponse>>.Failure(authorizationResult.Error);
        }

        var competencies = await repository.GetLegacyCompetencyResponsesByProfileIdAsync(query.JobProfileId, cancellationToken);
        if (competencies is null)
        {
            return Result<IReadOnlyCollection<JobProfileLegacyCompetencyResponse>>.Failure(
                await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : JobProfileErrors.JobProfileNotFound);
        }

        return Result<IReadOnlyCollection<JobProfileLegacyCompetencyResponse>>.Success(competencies);
    }
}

internal sealed class AddJobProfileCompetencyCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<AddJobProfileCompetencyCommand, JobProfileLegacyCompetencyResponse>
{
    public async Task<Result<JobProfileLegacyCompetencyResponse>> Handle(AddJobProfileCompetencyCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithCompetenciesOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var catalogItem = command.CatalogItemPublicId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemPublicId.Value, cancellationToken)
            : null;
        if (command.CatalogItemPublicId.HasValue && catalogItem is null)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var name = ResolveName(command.Name, catalogItem);
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.CompetencyNameRequired);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var competency = JobProfileCompetency.Create(
                catalogItem?.Id,
                null,
                name,
                command.ExpectedLevel,
                command.Notes,
                command.SortOrder);

            profile.AddCompetency(competency);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetLegacyCompetencyResponseAsync(profile.PublicId, competency.PublicId, cancellationToken)
                ?? competency.ToLegacyResponse(command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added competency to job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileLegacyCompetencyResponse>.Success(response);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileLegacyCompetencyResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string ResolveName(string? name, JobCatalogItem? catalogItem) =>
        string.IsNullOrWhiteSpace(name) ? catalogItem?.Name ?? string.Empty : name;
}

internal sealed class UpdateJobProfileCompetencyCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<UpdateJobProfileCompetencyCommand, JobProfileLegacyCompetencyResponse>
{
    public async Task<Result<JobProfileLegacyCompetencyResponse>> Handle(UpdateJobProfileCompetencyCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithCompetenciesOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var competency = profile.Competencies.FirstOrDefault(item => item.PublicId == command.CompetencyId);
        if (competency is null)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.CompetencyNotFound);
        }

        if (competency.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var catalogItem = command.CatalogItemPublicId.HasValue
            ? await catalogRepository.GetByIdAsync(command.CatalogItemPublicId.Value, cancellationToken)
            : null;
        if (command.CatalogItemPublicId.HasValue && catalogItem is null)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var name = string.IsNullOrWhiteSpace(command.Name) ? catalogItem?.Name ?? string.Empty : command.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.CompetencyNameRequired);
        }

        var before = await repository.GetLegacyCompetencyResponseAsync(profile.PublicId, competency.PublicId, cancellationToken)
            ?? competency.ToLegacyResponse(command.CatalogItemPublicId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            competency.Update(
                catalogItem?.Id,
                null,
                name,
                command.ExpectedLevel,
                command.Notes,
                command.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetLegacyCompetencyResponseAsync(profile.PublicId, competency.PublicId, cancellationToken)
                ?? competency.ToLegacyResponse(command.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated competency in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileLegacyCompetencyResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileLegacyCompetencyResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchJobProfileCompetencyCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IJobCatalogRepository catalogRepository)
    : ICommandHandler<PatchJobProfileCompetencyCommand, JobProfileLegacyCompetencyResponse>
{
    public async Task<Result<JobProfileLegacyCompetencyResponse>> Handle(PatchJobProfileCompetencyCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithCompetenciesOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var competency = profile.Competencies.FirstOrDefault(item => item.PublicId == command.CompetencyId);
        if (competency is null)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.CompetencyNotFound);
        }

        var before = await repository.GetLegacyCompetencyResponseAsync(profile.PublicId, competency.PublicId, cancellationToken)
            ?? competency.ToLegacyResponse(null);
        var patchState = JobProfileCompetencyPatchState.From(before);
        var patchApplication = JobProfileCompetencyPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(patchApplication.Error);
        }

        var validation = JobProfileCompetencyPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(validation.Error);
        }

        if (patchState.ConcurrencyToken != competency.ConcurrencyToken)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        if (!patchState.HasMutation)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Success(before);
        }

        var catalogItem = patchState.CatalogItemPublicId.HasValue
            ? await catalogRepository.GetByIdAsync(patchState.CatalogItemPublicId.Value, cancellationToken)
            : null;
        if (patchState.CatalogItemPublicId.HasValue && catalogItem is null)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var name = string.IsNullOrWhiteSpace(patchState.Name) ? catalogItem?.Name ?? string.Empty : patchState.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.CompetencyNameRequired);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            competency.Update(
                catalogItem?.Id,
                null,
                name,
                patchState.ExpectedLevel,
                patchState.Notes,
                patchState.SortOrder);

            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetLegacyCompetencyResponseAsync(profile.PublicId, competency.PublicId, cancellationToken)
                ?? competency.ToLegacyResponse(patchState.CatalogItemPublicId);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Patched competency in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileLegacyCompetencyResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileLegacyCompetencyResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobProfileCompetencyCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileCompetencyCommand, JobProfileLegacyCompetencyResponse>
{
    public async Task<Result<JobProfileLegacyCompetencyResponse>> Handle(RemoveJobProfileCompetencyCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetWithCompetenciesOnlyAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        var competency = profile.Competencies.FirstOrDefault(item => item.PublicId == command.CompetencyId);
        if (competency is null)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.CompetencyNotFound);
        }

        if (competency.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileLegacyCompetencyResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetLegacyCompetencyResponseAsync(profile.PublicId, competency.PublicId, cancellationToken)
            ?? competency.ToLegacyResponse(null);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.RemoveCompetency(competency);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed competency from job profile {profile.Code}.",
                    Before: before),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileLegacyCompetencyResponse>.Success(before);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileLegacyCompetencyResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class JobProfileCompetencyPatchState
{
    public Guid? CatalogItemPublicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExpectedLevel { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public bool ConcurrencyTokenTouched { get; set; }
    public bool HasMutation { get; set; }

    public static JobProfileCompetencyPatchState From(JobProfileLegacyCompetencyResponse response) =>
        new()
        {
            CatalogItemPublicId = response.CatalogItemPublicId,
            Name = response.Name,
            ExpectedLevel = response.ExpectedLevel,
            Notes = response.Notes,
            SortOrder = response.SortOrder,
            ConcurrencyToken = response.ConcurrencyToken
        };
}

internal static class JobProfileCompetencyPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfileCompetencyPatchOperation> operations, JobProfileCompetencyPatchState state)
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
                return ValidationFailure(operation.Path, "Only root competency properties can be patched.");
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

    public static Result Validate(JobProfileCompetencyPatchState state)
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

        if (state.CatalogItemPublicId == Guid.Empty)
        {
            errors["catalogItemPublicId"] = ["CatalogItemPublicId must be a valid UUID."];
        }

        if (state.Name is { Length: > 300 })
        {
            errors["name"] = ["Name must be 300 characters or fewer."];
        }

        if (state.ExpectedLevel is { Length: > 150 })
        {
            errors["expectedLevel"] = ["ExpectedLevel must be 150 characters or fewer."];
        }

        if (state.Notes is { Length: > 1000 })
        {
            errors["notes"] = ["Notes must be 1000 characters or fewer."];
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
        JobProfileCompetencyPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsAnySegment(property, "catalogItemPublicId", "catalogItemId"))
        {
            state.CatalogItemPublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "name"))
        {
            state.Name = isRemove ? string.Empty : ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "expectedLevel"))
        {
            state.ExpectedLevel = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "notes"))
        {
            state.Notes = isRemove ? null : ReadNullableString(value, path);
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
