using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.DocumentTypeCatalogs.Common;
using CLARIHR.Domain.DocumentTypeCatalogs;
using FluentValidation;

namespace CLARIHR.Application.Features.DocumentTypeCatalogs;

// ─── Commands ────────────────────────────────────────────────────────────────

public sealed record CreateDocumentTypeCatalogItemCommand(
    string Code,
    string Name,
    int SortOrder)
    : ICommand<DocumentTypeCatalogItemResponse>;

public sealed record UpdateDocumentTypeCatalogItemCommand(
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<DocumentTypeCatalogItemResponse>;

public sealed record ActivateDocumentTypeCatalogItemCommand(
    Guid Id,
    Guid ConcurrencyToken)
    : ICommand<DocumentTypeCatalogItemResponse>;

public sealed record InactivateDocumentTypeCatalogItemCommand(
    Guid Id,
    Guid ConcurrencyToken)
    : ICommand<DocumentTypeCatalogItemResponse>;

// ─── Validators ──────────────────────────────────────────────────────────────

internal sealed class CreateDocumentTypeCatalogItemCommandValidator
    : AbstractValidator<CreateDocumentTypeCatalogItemCommand>
{
    public CreateDocumentTypeCatalogItemCommandValidator()
    {
        RuleFor(c => c.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Must(DocumentTypeCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateDocumentTypeCatalogItemCommandValidator
    : AbstractValidator<UpdateDocumentTypeCatalogItemCommand>
{
    public UpdateDocumentTypeCatalogItemCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Must(DocumentTypeCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateDocumentTypeCatalogItemCommandValidator
    : AbstractValidator<ActivateDocumentTypeCatalogItemCommand>
{
    public ActivateDocumentTypeCatalogItemCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateDocumentTypeCatalogItemCommandValidator
    : AbstractValidator<InactivateDocumentTypeCatalogItemCommand>
{
    public InactivateDocumentTypeCatalogItemCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

// ─── Handlers ────────────────────────────────────────────────────────────────

internal sealed class CreateDocumentTypeCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    IDocumentTypeCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateDocumentTypeCatalogItemCommand, DocumentTypeCatalogItemResponse>
{
    public async Task<Result<DocumentTypeCatalogItemResponse>> Handle(
        CreateDocumentTypeCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(authResult.Error);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(normalizedCode, excludingId: null, cancellationToken))
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.CodeConflict);
        }

        var entity = DocumentTypeCatalogItem.Create(command.Code, command.Name, command.SortOrder);
        repository.Add(entity);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(entity.PublicId, cancellationToken);
        return response is null
            ? Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.NotFound)
            : Result<DocumentTypeCatalogItemResponse>.Success(response);
    }
}

internal sealed class UpdateDocumentTypeCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    IDocumentTypeCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateDocumentTypeCatalogItemCommand, DocumentTypeCatalogItemResponse>
{
    public async Task<Result<DocumentTypeCatalogItemResponse>> Handle(
        UpdateDocumentTypeCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.ConcurrencyConflict);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(normalizedCode, entity.Id, cancellationToken))
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.CodeConflict);
        }

        entity.Update(command.Code, command.Name, command.SortOrder);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.Id, cancellationToken);
        return response is null
            ? Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.NotFound)
            : Result<DocumentTypeCatalogItemResponse>.Success(response);
    }
}

internal sealed class ActivateDocumentTypeCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    IDocumentTypeCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateDocumentTypeCatalogItemCommand, DocumentTypeCatalogItemResponse>
{
    public async Task<Result<DocumentTypeCatalogItemResponse>> Handle(
        ActivateDocumentTypeCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.ConcurrencyConflict);
        }

        entity.Activate();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.Id, cancellationToken);
        return response is null
            ? Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.NotFound)
            : Result<DocumentTypeCatalogItemResponse>.Success(response);
    }
}

internal sealed class InactivateDocumentTypeCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    IDocumentTypeCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateDocumentTypeCatalogItemCommand, DocumentTypeCatalogItemResponse>
{
    public async Task<Result<DocumentTypeCatalogItemResponse>> Handle(
        InactivateDocumentTypeCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.ConcurrencyConflict);
        }

        if (await repository.IsInUseAsync(entity.Id, cancellationToken))
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.CatalogItemInUse);
        }

        entity.Inactivate();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.Id, cancellationToken);
        return response is null
            ? Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.NotFound)
            : Result<DocumentTypeCatalogItemResponse>.Success(response);
    }
}
