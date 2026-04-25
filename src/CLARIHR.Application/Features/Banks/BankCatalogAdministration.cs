using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Banks.Common;
using CLARIHR.Domain.Banks;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.Banks;

public sealed record BankCatalogItemResponse(
    Guid PublicId,
    string CountryCode,
    string Code,
    string Name,
    string? Alias,
    string? SwiftCode,
    string? RoutingCode,
    bool IsActive,
    int SortOrder,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record CompanyBankCatalogItemResponse(
    Guid PublicId,
    string Code,
    string Name,
    string? Alias,
    string? SwiftCode,
    string? RoutingCode,
    int SortOrder);

public sealed record BankCatalogLookup(
    long InternalId,
    Guid PublicId,
    string CountryCode,
    string Code,
    string Name,
    string? Alias,
    string? SwiftCode,
    string? RoutingCode,
    bool IsActive);

public sealed record SearchBankCatalogItemsQuery(
    string CountryCode,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = BankCatalogValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<BankCatalogItemResponse>>;

public sealed record GetBankCatalogItemByIdQuery(Guid BankPublicId) : IQuery<BankCatalogItemResponse>;

public sealed record CreateBankCatalogItemCommand(
    string CountryCode,
    string Code,
    string Name,
    string? Alias,
    string? SwiftCode,
    string? RoutingCode,
    int SortOrder)
    : ICommand<BankCatalogItemResponse>;

public sealed record UpdateBankCatalogItemCommand(
    Guid BankPublicId,
    string CountryCode,
    string Code,
    string Name,
    string? Alias,
    string? SwiftCode,
    string? RoutingCode,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<BankCatalogItemResponse>;

public sealed record ActivateBankCatalogItemCommand(Guid BankPublicId, Guid ConcurrencyToken)
    : ICommand<BankCatalogItemResponse>;

public sealed record InactivateBankCatalogItemCommand(Guid BankPublicId, Guid ConcurrencyToken)
    : ICommand<BankCatalogItemResponse>;

public sealed record SearchCompanyBankCatalogQuery(
    Guid CompanyId,
    string? Search,
    int PageNumber = 1,
    int PageSize = BankCatalogValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<CompanyBankCatalogItemResponse>>;

internal sealed class SearchBankCatalogItemsQueryValidator : AbstractValidator<SearchBankCatalogItemsQuery>
{
    public SearchBankCatalogItemsQueryValidator()
    {
        RuleFor(query => query.CountryCode)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$");
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, BankCatalogValidationRules.MaxPageSize);
    }
}

internal sealed class GetBankCatalogItemByIdQueryValidator : AbstractValidator<GetBankCatalogItemByIdQuery>
{
    public GetBankCatalogItemByIdQueryValidator()
    {
        RuleFor(query => query.BankPublicId).NotEmpty();
    }
}

internal sealed class CreateBankCatalogItemCommandValidator : AbstractValidator<CreateBankCatalogItemCommand>
{
    public CreateBankCatalogItemCommandValidator()
    {
        RuleFor(command => command.CountryCode)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$");
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Must(BankCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Alias).MaximumLength(120);
        RuleFor(command => command.SwiftCode).MaximumLength(40);
        RuleFor(command => command.RoutingCode).MaximumLength(40);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateBankCatalogItemCommandValidator : AbstractValidator<UpdateBankCatalogItemCommand>
{
    public UpdateBankCatalogItemCommandValidator()
    {
        RuleFor(command => command.BankPublicId).NotEmpty();
        RuleFor(command => command.CountryCode)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$");
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Must(BankCatalogValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Alias).MaximumLength(120);
        RuleFor(command => command.SwiftCode).MaximumLength(40);
        RuleFor(command => command.RoutingCode).MaximumLength(40);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateBankCatalogItemCommandValidator : AbstractValidator<ActivateBankCatalogItemCommand>
{
    public ActivateBankCatalogItemCommandValidator()
    {
        RuleFor(command => command.BankPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateBankCatalogItemCommandValidator : AbstractValidator<InactivateBankCatalogItemCommand>
{
    public InactivateBankCatalogItemCommandValidator()
    {
        RuleFor(command => command.BankPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchCompanyBankCatalogQueryValidator : AbstractValidator<SearchCompanyBankCatalogQuery>
{
    public SearchCompanyBankCatalogQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, BankCatalogValidationRules.MaxPageSize);
    }
}

internal sealed class SearchBankCatalogItemsQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICountryCatalogRepository countryCatalogRepository,
    IBankCatalogRepository repository)
    : IQueryHandler<SearchBankCatalogItemsQuery, PagedResponse<BankCatalogItemResponse>>
{
    public async Task<Result<PagedResponse<BankCatalogItemResponse>>> Handle(
        SearchBankCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<BankCatalogItemResponse>>.Failure(authorizationResult.Error);
        }

        var country = await countryCatalogRepository.GetActiveByCodeAsync(query.CountryCode, cancellationToken);
        if (country is null)
        {
            return Result<PagedResponse<BankCatalogItemResponse>>.Failure(BankCatalogErrors.CountryNotFound(query.CountryCode));
        }

        var response = await repository.SearchAsync(
            country.InternalId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<BankCatalogItemResponse>>.Success(response);
    }
}

internal sealed class GetBankCatalogItemByIdQueryHandler(
    IPlatformAuthorizationService authorizationService,
    IBankCatalogRepository repository)
    : IQueryHandler<GetBankCatalogItemByIdQuery, BankCatalogItemResponse>
{
    public async Task<Result<BankCatalogItemResponse>> Handle(
        GetBankCatalogItemByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<BankCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.BankPublicId, cancellationToken);
        return response is null
            ? Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.NotFound)
            : Result<BankCatalogItemResponse>.Success(response);
    }
}

internal sealed class CreateBankCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICountryCatalogRepository countryCatalogRepository,
    IBankCatalogRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<CreateBankCatalogItemCommandHandler> logger)
    : ICommandHandler<CreateBankCatalogItemCommand, BankCatalogItemResponse>
{
    public async Task<Result<BankCatalogItemResponse>> Handle(
        CreateBankCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<BankCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var country = await countryCatalogRepository.GetActiveByCodeAsync(command.CountryCode, cancellationToken);
        if (country is null)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.CountryNotFound(command.CountryCode));
        }

        if (await repository.ExistsByCodeAsync(country.InternalId, command.Code.Trim().ToUpperInvariant(), excludingId: null, cancellationToken))
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.CodeConflict);
        }

        var bank = BankCatalogItem.Create(
            country.InternalId,
            country.Code,
            command.Code,
            command.Name,
            command.Alias,
            command.SwiftCode,
            command.RoutingCode,
            isActive: true,
            command.SortOrder);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(bank);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(bank.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Bank catalog response could not be resolved after creation.");

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.BankCatalogItemCreated,
                    AuditEntityTypes.BankCatalogItem,
                    bank.PublicId,
                    bank.Code,
                    AuditActions.Create,
                    $"Created bank catalog item {bank.Code}.",
                    After: response),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Bank catalog item {BankCode} created by user {UserId}.",
                bank.Code,
                currentUserService.UserId);

            return Result<BankCatalogItemResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateBankCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICountryCatalogRepository countryCatalogRepository,
    IBankCatalogRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<UpdateBankCatalogItemCommandHandler> logger)
    : ICommandHandler<UpdateBankCatalogItemCommand, BankCatalogItemResponse>
{
    public async Task<Result<BankCatalogItemResponse>> Handle(
        UpdateBankCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<BankCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var bank = await repository.GetByIdAsync(command.BankPublicId, cancellationToken);
        if (bank is null)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.NotFound);
        }

        if (bank.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.ConcurrencyConflict);
        }

        if (!string.Equals(bank.CountryCode, command.CountryCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.CountryChangeForbidden);
        }

        var country = await countryCatalogRepository.GetActiveByCodeAsync(command.CountryCode, cancellationToken);
        if (country is null)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.CountryNotFound(command.CountryCode));
        }

        if (await repository.ExistsByCodeAsync(country.InternalId, command.Code.Trim().ToUpperInvariant(), bank.Id, cancellationToken))
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.CodeConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = await repository.GetResponseByIdAsync(bank.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Bank catalog response could not be resolved before update.");

            bank.Update(
                country.InternalId,
                country.Code,
                command.Code,
                command.Name,
                command.Alias,
                command.SwiftCode,
                command.RoutingCode,
                command.SortOrder);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(bank.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Bank catalog response could not be resolved after update.");

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.BankCatalogItemUpdated,
                    AuditEntityTypes.BankCatalogItem,
                    bank.PublicId,
                    bank.Code,
                    AuditActions.Update,
                    $"Updated bank catalog item {bank.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Bank catalog item {BankCode} updated by user {UserId}.",
                bank.Code,
                currentUserService.UserId);

            return Result<BankCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateBankCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    IBankCatalogRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<ActivateBankCatalogItemCommandHandler> logger)
    : ICommandHandler<ActivateBankCatalogItemCommand, BankCatalogItemResponse>
{
    public async Task<Result<BankCatalogItemResponse>> Handle(
        ActivateBankCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<BankCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var bank = await repository.GetByIdAsync(command.BankPublicId, cancellationToken);
        if (bank is null)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.NotFound);
        }

        if (bank.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.ConcurrencyConflict);
        }

        if (bank.IsActive)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.AlreadyActive);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = await repository.GetResponseByIdAsync(bank.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Bank catalog response could not be resolved before activation.");

            bank.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(bank.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Bank catalog response could not be resolved after activation.");

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.BankCatalogItemActivated,
                    AuditEntityTypes.BankCatalogItem,
                    bank.PublicId,
                    bank.Code,
                    AuditActions.Reactivate,
                    $"Activated bank catalog item {bank.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Bank catalog item {BankCode} activated by user {UserId}.",
                bank.Code,
                currentUserService.UserId);

            return Result<BankCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateBankCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    IBankCatalogRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<InactivateBankCatalogItemCommandHandler> logger)
    : ICommandHandler<InactivateBankCatalogItemCommand, BankCatalogItemResponse>
{
    public async Task<Result<BankCatalogItemResponse>> Handle(
        InactivateBankCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<BankCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var bank = await repository.GetByIdAsync(command.BankPublicId, cancellationToken);
        if (bank is null)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.NotFound);
        }

        if (bank.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.ConcurrencyConflict);
        }

        if (!bank.IsActive)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.AlreadyInactive);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = await repository.GetResponseByIdAsync(bank.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Bank catalog response could not be resolved before inactivation.");

            bank.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(bank.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Bank catalog response could not be resolved after inactivation.");

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.BankCatalogItemInactivated,
                    AuditEntityTypes.BankCatalogItem,
                    bank.PublicId,
                    bank.Code,
                    AuditActions.Deactivate,
                    $"Inactivated bank catalog item {bank.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Bank catalog item {BankCode} inactivated by user {UserId}.",
                bank.Code,
                currentUserService.UserId);

            return Result<BankCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class SearchCompanyBankCatalogQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IBankCatalogRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<SearchCompanyBankCatalogQuery, PagedResponse<CompanyBankCatalogItemResponse>>
{
    public async Task<Result<PagedResponse<CompanyBankCatalogItemResponse>>> Handle(
        SearchCompanyBankCatalogQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<CompanyBankCatalogItemResponse>>.Failure(authorizationResult.Error);
        }

        if (tenantContext.TenantId != query.CompanyId)
        {
            return Result<PagedResponse<CompanyBankCatalogItemResponse>>.Failure(ErrorCatalog.Forbidden);
        }

        var response = await repository.SearchActiveByCompanyAsync(
            query.CompanyId,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<CompanyBankCatalogItemResponse>>.Success(response);
    }
}
