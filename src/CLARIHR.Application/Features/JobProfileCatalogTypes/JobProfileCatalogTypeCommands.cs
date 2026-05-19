using CLARIHR.Application.Abstractions.CatalogTypes;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.JobProfileCatalogTypes.Common;
using CLARIHR.Domain.CatalogTypes;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfileCatalogTypes;

// ─── Commands ────────────────────────────────────────────────────────────────

public sealed record CreateJobProfileCatalogTypeCommand(
    string Code,
    string Name,
    int SortOrder)
    : ICommand<JobProfileCatalogTypeResponse>;

// Code is intentionally absent: the key is immutable once created (Q3). The
// handler always re-applies the persisted code so it cannot be changed via update.
public sealed record UpdateJobProfileCatalogTypeCommand(
    Guid Id,
    string Name,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<JobProfileCatalogTypeResponse>;

public sealed record ActivateJobProfileCatalogTypeCommand(
    Guid Id,
    Guid ConcurrencyToken)
    : ICommand<JobProfileCatalogTypeResponse>;

public sealed record InactivateJobProfileCatalogTypeCommand(
    Guid Id,
    Guid ConcurrencyToken)
    : ICommand<JobProfileCatalogTypeResponse>;

// ─── Validators ──────────────────────────────────────────────────────────────

internal sealed class CreateJobProfileCatalogTypeCommandValidator
    : AbstractValidator<CreateJobProfileCatalogTypeCommand>
{
    public CreateJobProfileCatalogTypeCommandValidator()
    {
        RuleFor(c => c.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Must(JobProfileCatalogTypeValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateJobProfileCatalogTypeCommandValidator
    : AbstractValidator<UpdateJobProfileCatalogTypeCommand>
{
    public UpdateJobProfileCatalogTypeCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateJobProfileCatalogTypeCommandValidator
    : AbstractValidator<ActivateJobProfileCatalogTypeCommand>
{
    public ActivateJobProfileCatalogTypeCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateJobProfileCatalogTypeCommandValidator
    : AbstractValidator<InactivateJobProfileCatalogTypeCommand>
{
    public InactivateJobProfileCatalogTypeCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

// ─── Handlers ────────────────────────────────────────────────────────────────

internal sealed class CreateJobProfileCatalogTypeCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICatalogTypeDescriptorRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateJobProfileCatalogTypeCommand, JobProfileCatalogTypeResponse>
{
    public async Task<Result<JobProfileCatalogTypeResponse>> Handle(
        CreateJobProfileCatalogTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(authResult.Error);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(normalizedCode, excludingId: null, cancellationToken))
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.CodeConflict);
        }

        var entity = CatalogTypeDescriptor.Create(command.Code, command.Name, command.SortOrder);
        repository.Add(entity);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        repository.Invalidate();

        var response = await repository.GetResponseByIdAsync(entity.PublicId, cancellationToken);
        return response is null
            ? Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.NotFound)
            : Result<JobProfileCatalogTypeResponse>.Success(response);
    }
}

internal sealed class UpdateJobProfileCatalogTypeCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICatalogTypeDescriptorRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateJobProfileCatalogTypeCommand, JobProfileCatalogTypeResponse>
{
    public async Task<Result<JobProfileCatalogTypeResponse>> Handle(
        UpdateJobProfileCatalogTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.ConcurrencyConflict);
        }

        // Key (Code) is immutable: re-apply the persisted code so it cannot change.
        entity.Update(entity.Code, command.Name, command.SortOrder);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        repository.Invalidate();

        var response = await repository.GetResponseByIdAsync(command.Id, cancellationToken);
        return response is null
            ? Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.NotFound)
            : Result<JobProfileCatalogTypeResponse>.Success(response);
    }
}

internal sealed class ActivateJobProfileCatalogTypeCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICatalogTypeDescriptorRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateJobProfileCatalogTypeCommand, JobProfileCatalogTypeResponse>
{
    public async Task<Result<JobProfileCatalogTypeResponse>> Handle(
        ActivateJobProfileCatalogTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.ConcurrencyConflict);
        }

        entity.Activate();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        repository.Invalidate();

        var response = await repository.GetResponseByIdAsync(command.Id, cancellationToken);
        return response is null
            ? Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.NotFound)
            : Result<JobProfileCatalogTypeResponse>.Success(response);
    }
}

internal sealed class InactivateJobProfileCatalogTypeCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICatalogTypeDescriptorRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateJobProfileCatalogTypeCommand, JobProfileCatalogTypeResponse>
{
    public async Task<Result<JobProfileCatalogTypeResponse>> Handle(
        InactivateJobProfileCatalogTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.ConcurrencyConflict);
        }

        entity.Inactivate();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        repository.Invalidate();

        var response = await repository.GetResponseByIdAsync(command.Id, cancellationToken);
        return response is null
            ? Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.NotFound)
            : Result<JobProfileCatalogTypeResponse>.Success(response);
    }
}
