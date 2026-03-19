using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Domain.LegalRepresentatives;
using FluentValidation;

namespace CLARIHR.Application.Features.LegalRepresentatives;

public sealed record ActiveLegalRepresentativeSummary(
    Guid Id,
    string FullName,
    LegalRepresentativeRepresentationType RepresentationType,
    string PositionTitle,
    bool IsPrimary);

public sealed record LegalRepresentativeListItemResponse(
    Guid Id,
    Guid CompanyId,
    string FirstName,
    string LastName,
    string FullName,
    LegalRepresentativeDocumentType DocumentType,
    string DocumentNumber,
    string PositionTitle,
    LegalRepresentativeRepresentationType RepresentationType,
    bool IsPrimary,
    bool IsActive,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record LegalRepresentativeResponse(
    Guid Id,
    Guid CompanyId,
    string FirstName,
    string LastName,
    string FullName,
    LegalRepresentativeDocumentType DocumentType,
    string DocumentNumber,
    string PositionTitle,
    LegalRepresentativeRepresentationType RepresentationType,
    string? AuthorityDescription,
    string? AppointmentInstrument,
    DateTime? AppointmentDateUtc,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Email,
    string? Phone,
    bool IsPrimary,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record LegalRepresentativeUsageResponse(
    Guid LegalRepresentativeId,
    int ActiveDocumentReferencesCount,
    bool CanInactivate);

public sealed record LegalRepresentativeDocumentTypeCatalogItemResponse(
    int Id,
    string Code,
    string Name,
    int SortOrder);

public sealed record LegalRepresentativePositionTitleCatalogItemResponse(
    int Id,
    string Code,
    string Name,
    int SortOrder);

public sealed record LegalRepresentativeRepresentationTypeCatalogItemResponse(
    int Id,
    string Code,
    string Name,
    int SortOrder);

public sealed record LegalRepresentativeExportRow(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    LegalRepresentativeDocumentType DocumentType,
    string DocumentNumber,
    string PositionTitle,
    LegalRepresentativeRepresentationType RepresentationType,
    string? AuthorityDescription,
    string? AppointmentInstrument,
    DateTime? AppointmentDateUtc,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Email,
    string? Phone,
    bool IsPrimary,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record SearchLegalRepresentativesQuery(
    Guid CompanyId,
    bool? IsActive,
    bool? IsPrimary,
    LegalRepresentativeRepresentationType? RepresentationType,
    string? Search,
    int PageNumber = 1,
    int PageSize = LegalRepresentativeValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<LegalRepresentativeListItemResponse>>;

public sealed record GetLegalRepresentativeByIdQuery(Guid LegalRepresentativeId)
    : IQuery<LegalRepresentativeResponse>;

public sealed record GetLegalRepresentativeUsageQuery(Guid LegalRepresentativeId)
    : IQuery<LegalRepresentativeUsageResponse>;

public sealed record GetLegalRepresentativeDocumentTypesQuery()
    : IQuery<IReadOnlyCollection<LegalRepresentativeDocumentTypeCatalogItemResponse>>;

public sealed record GetLegalRepresentativePositionTitlesQuery()
    : IQuery<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>>;

public sealed record GetLegalRepresentativeRepresentationTypesQuery()
    : IQuery<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>>;

public sealed record ExportLegalRepresentativesQuery(
    Guid CompanyId,
    bool? IsActive,
    bool? IsPrimary,
    LegalRepresentativeRepresentationType? RepresentationType,
    string? Search)
    : IQuery<IReadOnlyCollection<LegalRepresentativeExportRow>>;

public sealed record CreateLegalRepresentativeCommand(
    Guid CompanyId,
    string FirstName,
    string LastName,
    LegalRepresentativeDocumentType DocumentType,
    string DocumentNumber,
    string PositionTitle,
    LegalRepresentativeRepresentationType RepresentationType,
    string? AuthorityDescription,
    string? AppointmentInstrument,
    DateTime? AppointmentDateUtc,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Email,
    string? Phone,
    bool IsPrimary)
    : ICommand<LegalRepresentativeResponse>;

public sealed record UpdateLegalRepresentativeCommand(
    Guid LegalRepresentativeId,
    string FirstName,
    string LastName,
    LegalRepresentativeDocumentType DocumentType,
    string DocumentNumber,
    string PositionTitle,
    LegalRepresentativeRepresentationType RepresentationType,
    string? AuthorityDescription,
    string? AppointmentInstrument,
    DateTime? AppointmentDateUtc,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Email,
    string? Phone,
    bool IsPrimary,
    Guid ConcurrencyToken)
    : ICommand<LegalRepresentativeResponse>;

public sealed record ActivateLegalRepresentativeCommand(Guid LegalRepresentativeId, Guid ConcurrencyToken)
    : ICommand<LegalRepresentativeResponse>;

public sealed record InactivateLegalRepresentativeCommand(Guid LegalRepresentativeId, Guid ConcurrencyToken)
    : ICommand<LegalRepresentativeResponse>;

public sealed record SetPrimaryLegalRepresentativeCommand(Guid LegalRepresentativeId, Guid ConcurrencyToken)
    : ICommand<LegalRepresentativeResponse>;

internal sealed class SearchLegalRepresentativesQueryValidator : AbstractValidator<SearchLegalRepresentativesQuery>
{
    public SearchLegalRepresentativesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, LegalRepresentativeValidationRules.MaxPageSize);
    }
}

internal sealed class GetLegalRepresentativeByIdQueryValidator : AbstractValidator<GetLegalRepresentativeByIdQuery>
{
    public GetLegalRepresentativeByIdQueryValidator()
    {
        RuleFor(query => query.LegalRepresentativeId).NotEmpty();
    }
}

internal sealed class GetLegalRepresentativeUsageQueryValidator : AbstractValidator<GetLegalRepresentativeUsageQuery>
{
    public GetLegalRepresentativeUsageQueryValidator()
    {
        RuleFor(query => query.LegalRepresentativeId).NotEmpty();
    }
}

internal sealed class ExportLegalRepresentativesQueryValidator : AbstractValidator<ExportLegalRepresentativesQuery>
{
    public ExportLegalRepresentativesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
    }
}

internal sealed class CreateLegalRepresentativeCommandValidator : AbstractValidator<CreateLegalRepresentativeCommand>
{
    public CreateLegalRepresentativeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(LegalRepresentativeValidationRules.IsValidName)
            .WithMessage("FirstName format is invalid.");
        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(LegalRepresentativeValidationRules.IsValidName)
            .WithMessage("LastName format is invalid.");
        RuleFor(command => command.DocumentNumber)
            .NotEmpty()
            .MaximumLength(80)
            .Must(LegalRepresentativeValidationRules.IsValidDocumentNumber)
            .WithMessage("DocumentNumber format is invalid.");
        RuleFor(command => command.PositionTitle)
            .NotEmpty()
            .MaximumLength(150)
            .Must(LegalRepresentativeValidationRules.IsValidPositionTitle)
            .WithMessage("PositionTitle format is invalid.");
        RuleFor(command => command.AuthorityDescription).MaximumLength(500);
        RuleFor(command => command.AppointmentInstrument).MaximumLength(500);
        RuleFor(command => command.EffectiveFromUtc).NotEmpty();
        RuleFor(command => command.EffectiveToUtc)
            .Must((command, to) => !to.HasValue || to.Value.Date >= command.EffectiveFromUtc.Date)
            .WithMessage(LegalRepresentativeErrors.EffectiveDatesInvalid.Message);
        RuleFor(command => command.Email)
            .EmailAddress()
            .MaximumLength(320)
            .When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Phone).MaximumLength(40);
    }
}

internal sealed class UpdateLegalRepresentativeCommandValidator : AbstractValidator<UpdateLegalRepresentativeCommand>
{
    public UpdateLegalRepresentativeCommandValidator()
    {
        RuleFor(command => command.LegalRepresentativeId).NotEmpty();
        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(LegalRepresentativeValidationRules.IsValidName)
            .WithMessage("FirstName format is invalid.");
        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(LegalRepresentativeValidationRules.IsValidName)
            .WithMessage("LastName format is invalid.");
        RuleFor(command => command.DocumentNumber)
            .NotEmpty()
            .MaximumLength(80)
            .Must(LegalRepresentativeValidationRules.IsValidDocumentNumber)
            .WithMessage("DocumentNumber format is invalid.");
        RuleFor(command => command.PositionTitle)
            .NotEmpty()
            .MaximumLength(150)
            .Must(LegalRepresentativeValidationRules.IsValidPositionTitle)
            .WithMessage("PositionTitle format is invalid.");
        RuleFor(command => command.AuthorityDescription).MaximumLength(500);
        RuleFor(command => command.AppointmentInstrument).MaximumLength(500);
        RuleFor(command => command.EffectiveFromUtc).NotEmpty();
        RuleFor(command => command.EffectiveToUtc)
            .Must((command, to) => !to.HasValue || to.Value.Date >= command.EffectiveFromUtc.Date)
            .WithMessage(LegalRepresentativeErrors.EffectiveDatesInvalid.Message);
        RuleFor(command => command.Email)
            .EmailAddress()
            .MaximumLength(320)
            .When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Phone).MaximumLength(40);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateLegalRepresentativeCommandValidator : AbstractValidator<ActivateLegalRepresentativeCommand>
{
    public ActivateLegalRepresentativeCommandValidator()
    {
        RuleFor(command => command.LegalRepresentativeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateLegalRepresentativeCommandValidator : AbstractValidator<InactivateLegalRepresentativeCommand>
{
    public InactivateLegalRepresentativeCommandValidator()
    {
        RuleFor(command => command.LegalRepresentativeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SetPrimaryLegalRepresentativeCommandValidator : AbstractValidator<SetPrimaryLegalRepresentativeCommand>
{
    public SetPrimaryLegalRepresentativeCommandValidator()
    {
        RuleFor(command => command.LegalRepresentativeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchLegalRepresentativesQueryHandler(
    ILegalRepresentativeAuthorizationService authorizationService,
    ILegalRepresentativeRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchLegalRepresentativesQuery, PagedResponse<LegalRepresentativeListItemResponse>>
{
    public async Task<Result<PagedResponse<LegalRepresentativeListItemResponse>>> Handle(
        SearchLegalRepresentativesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<LegalRepresentativeListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.IsActive,
            query.IsPrimary,
            query.RepresentationType,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<LegalRepresentativeListItemResponse>>.Success(response);
        }

        var items = response.Items
            .Select(item => LegalRepresentativePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<LegalRepresentativeListItemResponse>>.Success(response);
    }
}

internal sealed class GetLegalRepresentativeByIdQueryHandler(
    ILegalRepresentativeAuthorizationService authorizationService,
    ILegalRepresentativeRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetLegalRepresentativeByIdQuery, LegalRepresentativeResponse>
{
    public async Task<Result<LegalRepresentativeResponse>> Handle(
        GetLegalRepresentativeByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LegalRepresentativeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LegalRepresentativeResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.LegalRepresentativeId, cancellationToken);
        if (response is not null)
        {
            var usage = await repository.GetUsageByIdAsync(query.LegalRepresentativeId, cancellationToken);
            response = LegalRepresentativePolicyAdapter.ApplyAllowedActions(
                response,
                resourceActionPolicyService,
                usage?.CanInactivate ?? true);

            return Result<LegalRepresentativeResponse>.Success(response);
        }

        return Result<LegalRepresentativeResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.LegalRepresentativeId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : LegalRepresentativeErrors.NotFound);
    }
}

internal sealed class GetLegalRepresentativeUsageQueryHandler(
    ILegalRepresentativeAuthorizationService authorizationService,
    ILegalRepresentativeRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetLegalRepresentativeUsageQuery, LegalRepresentativeUsageResponse>
{
    public async Task<Result<LegalRepresentativeUsageResponse>> Handle(
        GetLegalRepresentativeUsageQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LegalRepresentativeUsageResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LegalRepresentativeUsageResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetUsageByIdAsync(query.LegalRepresentativeId, cancellationToken);
        if (response is not null)
        {
            return Result<LegalRepresentativeUsageResponse>.Success(response);
        }

        return Result<LegalRepresentativeUsageResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.LegalRepresentativeId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : LegalRepresentativeErrors.NotFound);
    }
}

internal sealed class GetLegalRepresentativeDocumentTypesQueryHandler(
    ILegalRepresentativeRepository repository)
    : IQueryHandler<GetLegalRepresentativeDocumentTypesQuery, IReadOnlyCollection<LegalRepresentativeDocumentTypeCatalogItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<LegalRepresentativeDocumentTypeCatalogItemResponse>>> Handle(
        GetLegalRepresentativeDocumentTypesQuery query,
        CancellationToken cancellationToken)
    {
        var response = await repository.GetDocumentTypeCatalogItemsAsync(cancellationToken);
        return Result<IReadOnlyCollection<LegalRepresentativeDocumentTypeCatalogItemResponse>>.Success(response);
    }
}

internal sealed class GetLegalRepresentativePositionTitlesQueryHandler(
    ILegalRepresentativeRepository repository)
    : IQueryHandler<GetLegalRepresentativePositionTitlesQuery, IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>>> Handle(
        GetLegalRepresentativePositionTitlesQuery query,
        CancellationToken cancellationToken)
    {
        var response = await repository.GetPositionTitleCatalogItemsAsync(cancellationToken);
        return Result<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>>.Success(response);
    }
}

internal sealed class GetLegalRepresentativeRepresentationTypesQueryHandler(
    ILegalRepresentativeRepository repository)
    : IQueryHandler<GetLegalRepresentativeRepresentationTypesQuery, IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>>> Handle(
        GetLegalRepresentativeRepresentationTypesQuery query,
        CancellationToken cancellationToken)
    {
        var response = await repository.GetRepresentationTypeCatalogItemsAsync(cancellationToken);
        return Result<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>>.Success(response);
    }
}

internal sealed class ExportLegalRepresentativesQueryHandler(
    ILegalRepresentativeAuthorizationService authorizationService,
    ILegalRepresentativeRepository repository)
    : IQueryHandler<ExportLegalRepresentativesQuery, IReadOnlyCollection<LegalRepresentativeExportRow>>
{
    public async Task<Result<IReadOnlyCollection<LegalRepresentativeExportRow>>> Handle(
        ExportLegalRepresentativesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<LegalRepresentativeExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await repository.GetExportRowsAsync(
            query.CompanyId,
            query.IsActive,
            query.IsPrimary,
            query.RepresentationType,
            query.Search,
            cancellationToken);

        return Result<IReadOnlyCollection<LegalRepresentativeExportRow>>.Success(rows);
    }
}

internal static class LegalRepresentativePolicyAdapter
{
    public static LegalRepresentativeListItemResponse ApplyAllowedActions(
        LegalRepresentativeListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LegalRepresentativePermissionCodes.ResourceKey,
                response.RepresentationType.ToString(),
                response.IsActive,
                SupportsEdit: true,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                SupportsInactivate: true));

        return response with { AllowedActions = allowedActions };
    }

    public static LegalRepresentativeResponse ApplyAllowedActions(
        LegalRepresentativeResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canInactivate)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LegalRepresentativePermissionCodes.ResourceKey,
                response.RepresentationType.ToString(),
                response.IsActive,
                SupportsEdit: true,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                SupportsInactivate: true));

        if (response.IsActive && !canInactivate)
        {
            var reasons = allowedActions.Reasons
                .Append("At least one active legal representative is required.")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            allowedActions = allowedActions with
            {
                CanInactivate = false,
                Reasons = reasons
            };
        }

        return response with { AllowedActions = allowedActions };
    }
}

internal sealed class CreateLegalRepresentativeCommandHandler(
    ILegalRepresentativeAuthorizationService authorizationService,
    ILegalRepresentativeRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateLegalRepresentativeCommand, LegalRepresentativeResponse>
{
    public async Task<Result<LegalRepresentativeResponse>> Handle(
        CreateLegalRepresentativeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LegalRepresentativeResponse>.Failure(authorizationResult.Error);
        }

        var normalizedDocumentNumber = command.DocumentNumber.Trim().ToUpperInvariant();
        if (await repository.DocumentExistsAsync(
                command.CompanyId,
                command.DocumentType,
                normalizedDocumentNumber,
                excludingLegalRepresentativeId: null,
                cancellationToken))
        {
            return Result<LegalRepresentativeResponse>.Failure(LegalRepresentativeErrors.DocumentConflict);
        }

        var legalRepresentative = LegalRepresentative.Create(
            command.FirstName,
            command.LastName,
            command.DocumentType,
            command.DocumentNumber,
            command.PositionTitle,
            command.RepresentationType,
            command.AuthorityDescription,
            command.AppointmentInstrument,
            command.AppointmentDateUtc,
            command.EffectiveFromUtc,
            command.EffectiveToUtc,
            command.Email,
            command.Phone,
            command.IsPrimary);
        legalRepresentative.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (command.IsPrimary)
            {
                var currentPrimary = await repository.GetActivePrimaryAsync(command.CompanyId, excludingLegalRepresentativePublicId: null, cancellationToken);
                currentPrimary?.ClearPrimary();
            }

            repository.Add(legalRepresentative);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(legalRepresentative.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Legal representative response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LegalRepresentativeCreated,
                    AuditEntityTypes.LegalRepresentative,
                    legalRepresentative.PublicId,
                    legalRepresentative.FullName,
                    AuditActions.Create,
                    $"Created legal representative {legalRepresentative.FullName}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LegalRepresentativeResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateLegalRepresentativeCommandHandler(
    ILegalRepresentativeAuthorizationService authorizationService,
    ILegalRepresentativeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateLegalRepresentativeCommand, LegalRepresentativeResponse>
{
    public async Task<Result<LegalRepresentativeResponse>> Handle(
        UpdateLegalRepresentativeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LegalRepresentativeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LegalRepresentativeResponse>.Failure(authorizationResult.Error);
        }

        var legalRepresentative = await repository.GetByIdAsync(command.LegalRepresentativeId, cancellationToken);
        if (legalRepresentative is null)
        {
            return Result<LegalRepresentativeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.LegalRepresentativeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LegalRepresentativeErrors.NotFound);
        }

        if (legalRepresentative.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LegalRepresentativeResponse>.Failure(LegalRepresentativeErrors.ConcurrencyConflict);
        }

        if (!legalRepresentative.IsActive && command.IsPrimary)
        {
            return Result<LegalRepresentativeResponse>.Failure(LegalRepresentativeErrors.StateRuleViolation);
        }

        var normalizedDocumentNumber = command.DocumentNumber.Trim().ToUpperInvariant();
        if (await repository.DocumentExistsAsync(
                legalRepresentative.TenantId,
                command.DocumentType,
                normalizedDocumentNumber,
                legalRepresentative.Id,
                cancellationToken))
        {
            return Result<LegalRepresentativeResponse>.Failure(LegalRepresentativeErrors.DocumentConflict);
        }

        var before = await repository.GetResponseByIdAsync(legalRepresentative.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Legal representative response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (command.IsPrimary)
            {
                var currentPrimary = await repository.GetActivePrimaryAsync(
                    legalRepresentative.TenantId,
                    legalRepresentative.PublicId,
                    cancellationToken);
                currentPrimary?.ClearPrimary();
            }

            legalRepresentative.Update(
                command.FirstName,
                command.LastName,
                command.DocumentType,
                command.DocumentNumber,
                command.PositionTitle,
                command.RepresentationType,
                command.AuthorityDescription,
                command.AppointmentInstrument,
                command.AppointmentDateUtc,
                command.EffectiveFromUtc,
                command.EffectiveToUtc,
                command.Email,
                command.Phone,
                command.IsPrimary);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(legalRepresentative.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Legal representative response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LegalRepresentativeUpdated,
                    AuditEntityTypes.LegalRepresentative,
                    legalRepresentative.PublicId,
                    legalRepresentative.FullName,
                    AuditActions.Update,
                    $"Updated legal representative {legalRepresentative.FullName}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LegalRepresentativeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateLegalRepresentativeCommandHandler(
    ILegalRepresentativeAuthorizationService authorizationService,
    ILegalRepresentativeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateLegalRepresentativeCommand, LegalRepresentativeResponse>
{
    public async Task<Result<LegalRepresentativeResponse>> Handle(
        ActivateLegalRepresentativeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LegalRepresentativeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LegalRepresentativeResponse>.Failure(authorizationResult.Error);
        }

        var legalRepresentative = await repository.GetByIdAsync(command.LegalRepresentativeId, cancellationToken);
        if (legalRepresentative is null)
        {
            return Result<LegalRepresentativeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.LegalRepresentativeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LegalRepresentativeErrors.NotFound);
        }

        if (legalRepresentative.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LegalRepresentativeResponse>.Failure(LegalRepresentativeErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(legalRepresentative.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Legal representative response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            legalRepresentative.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(legalRepresentative.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Legal representative response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LegalRepresentativeActivated,
                    AuditEntityTypes.LegalRepresentative,
                    legalRepresentative.PublicId,
                    legalRepresentative.FullName,
                    AuditActions.Reactivate,
                    $"Activated legal representative {legalRepresentative.FullName}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LegalRepresentativeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateLegalRepresentativeCommandHandler(
    ILegalRepresentativeAuthorizationService authorizationService,
    ILegalRepresentativeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateLegalRepresentativeCommand, LegalRepresentativeResponse>
{
    public async Task<Result<LegalRepresentativeResponse>> Handle(
        InactivateLegalRepresentativeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LegalRepresentativeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LegalRepresentativeResponse>.Failure(authorizationResult.Error);
        }

        var legalRepresentative = await repository.GetByIdAsync(command.LegalRepresentativeId, cancellationToken);
        if (legalRepresentative is null)
        {
            return Result<LegalRepresentativeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.LegalRepresentativeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LegalRepresentativeErrors.NotFound);
        }

        if (legalRepresentative.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LegalRepresentativeResponse>.Failure(LegalRepresentativeErrors.ConcurrencyConflict);
        }

        if (legalRepresentative.IsActive)
        {
            var activeCount = await repository.GetActiveCountAsync(legalRepresentative.TenantId, cancellationToken);
            if (activeCount <= 1)
            {
                return Result<LegalRepresentativeResponse>.Failure(LegalRepresentativeErrors.ActiveMinimumRequired);
            }
        }

        var before = await repository.GetResponseByIdAsync(legalRepresentative.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Legal representative response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            legalRepresentative.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(legalRepresentative.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Legal representative response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LegalRepresentativeInactivated,
                    AuditEntityTypes.LegalRepresentative,
                    legalRepresentative.PublicId,
                    legalRepresentative.FullName,
                    AuditActions.Deactivate,
                    $"Inactivated legal representative {legalRepresentative.FullName}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LegalRepresentativeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class SetPrimaryLegalRepresentativeCommandHandler(
    ILegalRepresentativeAuthorizationService authorizationService,
    ILegalRepresentativeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetPrimaryLegalRepresentativeCommand, LegalRepresentativeResponse>
{
    public async Task<Result<LegalRepresentativeResponse>> Handle(
        SetPrimaryLegalRepresentativeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<LegalRepresentativeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LegalRepresentativeResponse>.Failure(authorizationResult.Error);
        }

        var legalRepresentative = await repository.GetByIdAsync(command.LegalRepresentativeId, cancellationToken);
        if (legalRepresentative is null)
        {
            return Result<LegalRepresentativeResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.LegalRepresentativeId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : LegalRepresentativeErrors.NotFound);
        }

        if (legalRepresentative.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<LegalRepresentativeResponse>.Failure(LegalRepresentativeErrors.ConcurrencyConflict);
        }

        if (!legalRepresentative.IsActive)
        {
            return Result<LegalRepresentativeResponse>.Failure(LegalRepresentativeErrors.StateRuleViolation);
        }

        var before = await repository.GetResponseByIdAsync(legalRepresentative.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Legal representative response could not be resolved before primary change.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var currentPrimary = await repository.GetActivePrimaryAsync(
                legalRepresentative.TenantId,
                legalRepresentative.PublicId,
                cancellationToken);
            currentPrimary?.ClearPrimary();

            legalRepresentative.SetPrimary();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(legalRepresentative.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Legal representative response could not be resolved after primary change.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LegalRepresentativeSetPrimary,
                    AuditEntityTypes.LegalRepresentative,
                    legalRepresentative.PublicId,
                    legalRepresentative.FullName,
                    AuditActions.Update,
                    $"Set legal representative {legalRepresentative.FullName} as primary.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LegalRepresentativeResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
