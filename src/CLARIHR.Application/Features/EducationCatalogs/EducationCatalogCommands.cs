using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Domain.EducationCatalogs;
using FluentValidation;

namespace CLARIHR.Application.Features.EducationCatalogs;

// ─── Commands ────────────────────────────────────────────────────────────────

public sealed record CreateEducationCatalogItemCommand(
    EducationCatalogType CatalogType,
    string Code,
    string Name,
    int SortOrder)
    : ICommand<EducationCatalogItemResponse>;

public sealed record UpdateEducationCatalogItemCommand(
    EducationCatalogType CatalogType,
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<EducationCatalogItemResponse>;

public sealed record ActivateEducationCatalogItemCommand(
    EducationCatalogType CatalogType,
    Guid Id,
    Guid ConcurrencyToken)
    : ICommand<EducationCatalogItemResponse>;

public sealed record InactivateEducationCatalogItemCommand(
    EducationCatalogType CatalogType,
    Guid Id,
    Guid ConcurrencyToken)
    : ICommand<EducationCatalogItemResponse>;

// ─── Validators ──────────────────────────────────────────────────────────────

internal sealed class CreateEducationCatalogItemCommandValidator
    : AbstractValidator<CreateEducationCatalogItemCommand>
{
    public CreateEducationCatalogItemCommandValidator()
    {
        RuleFor(c => c.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Must(EducationCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateEducationCatalogItemCommandValidator
    : AbstractValidator<UpdateEducationCatalogItemCommand>
{
    public UpdateEducationCatalogItemCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Must(EducationCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateEducationCatalogItemCommandValidator
    : AbstractValidator<ActivateEducationCatalogItemCommand>
{
    public ActivateEducationCatalogItemCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateEducationCatalogItemCommandValidator
    : AbstractValidator<InactivateEducationCatalogItemCommand>
{
    public InactivateEducationCatalogItemCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

// ─── Handlers ────────────────────────────────────────────────────────────────

internal sealed class CreateEducationCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    IEducationCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateEducationCatalogItemCommand, EducationCatalogItemResponse>
{
    public async Task<Result<EducationCatalogItemResponse>> Handle(
        CreateEducationCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<EducationCatalogItemResponse>.Failure(authResult.Error);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(command.CatalogType, normalizedCode, excludingId: null, cancellationToken))
        {
            return Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.CodeConflict);
        }

        var entity = EducationCatalogFactory.Create(command.CatalogType, command.Code, command.Name, command.SortOrder);
        repository.Add(entity);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.CatalogType, entity.PublicId, cancellationToken);
        return response is null
            ? Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.NotFound)
            : Result<EducationCatalogItemResponse>.Success(response);
    }
}

internal sealed class UpdateEducationCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    IEducationCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateEducationCatalogItemCommand, EducationCatalogItemResponse>
{
    public async Task<Result<EducationCatalogItemResponse>> Handle(
        UpdateEducationCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<EducationCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.CatalogType, command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.ConcurrencyConflict);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(command.CatalogType, normalizedCode, entity.Id, cancellationToken))
        {
            return Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.CodeConflict);
        }

        entity.Update(command.Code, command.Name, command.SortOrder);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.CatalogType, command.Id, cancellationToken);
        return response is null
            ? Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.NotFound)
            : Result<EducationCatalogItemResponse>.Success(response);
    }
}

internal sealed class ActivateEducationCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    IEducationCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateEducationCatalogItemCommand, EducationCatalogItemResponse>
{
    public async Task<Result<EducationCatalogItemResponse>> Handle(
        ActivateEducationCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<EducationCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.CatalogType, command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.ConcurrencyConflict);
        }

        entity.Activate();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.CatalogType, command.Id, cancellationToken);
        return response is null
            ? Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.NotFound)
            : Result<EducationCatalogItemResponse>.Success(response);
    }
}

internal sealed class InactivateEducationCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    IEducationCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateEducationCatalogItemCommand, EducationCatalogItemResponse>
{
    public async Task<Result<EducationCatalogItemResponse>> Handle(
        InactivateEducationCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<EducationCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.CatalogType, command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.ConcurrencyConflict);
        }

        if (await repository.IsInUseAsync(command.CatalogType, entity.Id, cancellationToken))
        {
            return Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.CatalogItemInUse);
        }

        entity.Inactivate();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.CatalogType, command.Id, cancellationToken);
        return response is null
            ? Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.NotFound)
            : Result<EducationCatalogItemResponse>.Success(response);
    }
}

// ─── Factory ─────────────────────────────────────────────────────────────────

internal static class EducationCatalogFactory
{
    public static EducationCatalogItem Create(
        EducationCatalogType catalogType,
        string code,
        string name,
        int sortOrder) =>
        catalogType switch
        {
            EducationCatalogType.EducationStatus => EducationStatusCatalogItem.Create(code, name, sortOrder),
            EducationCatalogType.StudyType => EducationStudyTypeCatalogItem.Create(code, name, sortOrder),
            EducationCatalogType.Career => EducationCareerCatalogItem.Create(code, name, sortOrder),
            EducationCatalogType.Shift => EducationShiftCatalogItem.Create(code, name, sortOrder),
            EducationCatalogType.Modality => EducationModalityCatalogItem.Create(code, name, sortOrder),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported education catalog type.")
        };
}
