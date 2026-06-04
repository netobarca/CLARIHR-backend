using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
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
    bool? IsPrimary);

public sealed record LegalRepresentativeListItemResponse(
    Guid Id,
    Guid CompanyId,
    string FirstName,
    string LastName,
    string FullName,
    string DocumentType,
    string DocumentNumber,
    string PositionTitle,
    LegalRepresentativeRepresentationType RepresentationType,
    bool? IsPrimary,
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
    string DocumentType,
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
    bool? IsPrimary,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record LegalRepresentativeUsageResponse(
    Guid LegalRepresentativeId,
    int ActiveDocumentReferencesCount,
    bool CanInactivate);

public sealed record LegalRepresentativePositionTitleCatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    int SortOrder);

public sealed record LegalRepresentativeRepresentationTypeCatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    int SortOrder);

public sealed record LegalRepresentativeExportRow(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string DocumentType,
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
    bool? IsPrimary,
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

public sealed record GetLegalRepresentativePositionTitlesQuery()
    : IQuery<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>>;

public sealed record GetLegalRepresentativeRepresentationTypesQuery()
    : IQuery<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>>;

public sealed record ExportLegalRepresentativesQuery(
    Guid CompanyId,
    bool? IsActive,
    bool? IsPrimary,
    LegalRepresentativeRepresentationType? RepresentationType,
    string? Search,
    int? MaxRows = null)
    : IQuery<IReadOnlyCollection<LegalRepresentativeExportRow>>;

public sealed record CreateLegalRepresentativeCommand(
    Guid CompanyId,
    string FirstName,
    string LastName,
    string DocumentType,
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
    string DocumentType,
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

public sealed record LegalRepresentativePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchLegalRepresentativeCommand(
    Guid LegalRepresentativeId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<LegalRepresentativePatchOperation> Operations)
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
        RuleFor(command => command.DocumentType)
            .NotEmpty()
            .MaximumLength(80);
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
        RuleFor(command => command.DocumentType)
            .NotEmpty()
            .MaximumLength(80);
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

internal sealed class PatchLegalRepresentativeCommandValidator : AbstractValidator<PatchLegalRepresentativeCommand>
{
    public PatchLegalRepresentativeCommandValidator()
    {
        RuleFor(command => command.LegalRepresentativeId).NotEmpty();
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

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => LegalRepresentativePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
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
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = LegalRepresentativePolicyAdapter.ApplyAllowedActions(
                response,
                resourceActionPolicyService,
                canManage,
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
            query.MaxRows,
            cancellationToken);

        return Result<IReadOnlyCollection<LegalRepresentativeExportRow>>.Success(rows);
    }
}

internal static class LegalRepresentativePolicyAdapter
{
    public static LegalRepresentativeListItemResponse ApplyAllowedActions(
        LegalRepresentativeListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LegalRepresentativePermissionCodes.ResourceKey,
                response.RepresentationType.ToString(),
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }

    public static LegalRepresentativeResponse ApplyAllowedActions(
        LegalRepresentativeResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage,
        bool canInactivate)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LegalRepresentativePermissionCodes.ResourceKey,
                response.RepresentationType.ToString(),
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));

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

internal sealed class PatchLegalRepresentativeCommandHandler(
    ILegalRepresentativeAuthorizationService authorizationService,
    ILegalRepresentativeRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchLegalRepresentativeCommand, LegalRepresentativeResponse>
{
    public async Task<Result<LegalRepresentativeResponse>> Handle(
        PatchLegalRepresentativeCommand command,
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
            ?? throw new InvalidOperationException("Legal representative response could not be resolved before patch.");

        var state = LegalRepresentativePatchState.From(before);

        var applied = LegalRepresentativePatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<LegalRepresentativeResponse>.Failure(applied.Error);
        }

        var validation = LegalRepresentativePatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<LegalRepresentativeResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<LegalRepresentativeResponse>.Success(before);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Descriptive/contact fields only. The legal identity (document type/number), the
            // effective-date range, the primary flag and activation are NOT patchable here — they
            // carry uniqueness/range/state invariants (use PUT, /set-primary, /activate,
            // /inactivate). They are kept at their current — already valid — values, so the patch
            // re-runs no document-conflict, no date-range and no primary-clearing logic. (IsPrimary
            // is kept current: an already-primary rep stays primary with no other primary to clear.)
            legalRepresentative.Update(
                state.FirstName,
                state.LastName,
                before.DocumentType,
                before.DocumentNumber,
                state.PositionTitle,
                state.RepresentationType,
                state.AuthorityDescription,
                state.AppointmentInstrument,
                state.AppointmentDateUtc,
                before.EffectiveFromUtc,
                before.EffectiveToUtc,
                state.Email,
                state.Phone,
                before.IsPrimary ?? false);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(legalRepresentative.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Legal representative response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LegalRepresentativeUpdated,
                    AuditEntityTypes.LegalRepresentative,
                    legalRepresentative.PublicId,
                    legalRepresentative.FullName,
                    AuditActions.Update,
                    $"Patched legal representative {legalRepresentative.FullName}.",
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

internal sealed class LegalRepresentativePatchState
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public LegalRepresentativeRepresentationType RepresentationType { get; set; }
    public string? AuthorityDescription { get; set; }
    public string? AppointmentInstrument { get; set; }
    public DateTime? AppointmentDateUtc { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool HasMutation { get; set; }

    public static LegalRepresentativePatchState From(LegalRepresentativeResponse response) =>
        new()
        {
            FirstName = response.FirstName,
            LastName = response.LastName,
            PositionTitle = response.PositionTitle,
            RepresentationType = response.RepresentationType,
            AuthorityDescription = response.AuthorityDescription,
            AppointmentInstrument = response.AppointmentInstrument,
            AppointmentDateUtc = response.AppointmentDateUtc,
            Email = response.Email,
            Phone = response.Phone
        };
}

internal sealed class LegalRepresentativePatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class LegalRepresentativePatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<LegalRepresentativePatchOperation> operations, LegalRepresentativePatchState state)
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
                return ValidationFailure(operation.Path, "Only root legal representative properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (LegalRepresentativePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(LegalRepresentativePatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        ValidateName(errors, "firstName", state.FirstName);
        ValidateName(errors, "lastName", state.LastName);

        if (string.IsNullOrWhiteSpace(state.PositionTitle))
        {
            errors["positionTitle"] = ["Position title is required."];
        }
        else if (state.PositionTitle.Length > 150)
        {
            errors["positionTitle"] = ["Position title must be 150 characters or fewer."];
        }
        else if (!LegalRepresentativeValidationRules.IsValidPositionTitle(state.PositionTitle))
        {
            errors["positionTitle"] = ["PositionTitle format is invalid."];
        }

        if (state.AuthorityDescription is { Length: > 500 })
        {
            errors["authorityDescription"] = ["Authority description must be 500 characters or fewer."];
        }

        if (state.AppointmentInstrument is { Length: > 500 })
        {
            errors["appointmentInstrument"] = ["Appointment instrument must be 500 characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(state.Email) && (state.Email.Length > 320 || !IsValidEmail(state.Email)))
        {
            errors["email"] = ["Email format is invalid."];
        }

        if (state.Phone is { Length: > 40 })
        {
            errors["phone"] = ["Phone must be 40 characters or fewer."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static void ValidateName(Dictionary<string, string[]> errors, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[key] = ["Value is required."];
        }
        else if (value.Length > 100)
        {
            errors[key] = ["Value must be 100 characters or fewer."];
        }
        else if (!LegalRepresentativeValidationRules.IsValidName(value))
        {
            errors[key] = ["Value format is invalid."];
        }
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        LegalRepresentativePatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "concurrencyToken"))
        {
            return ValidationFailure(path, "The concurrency token cannot be patched; send the current token in the If-Match header.");
        }

        if (IsSegment(property, "isActive"))
        {
            return ValidationFailure(path, "Activation is not patchable; use the /activate and /inactivate endpoints.");
        }

        if (IsSegment(property, "isPrimary"))
        {
            return ValidationFailure(path, "The primary flag is not patchable; use the /set-primary endpoint.");
        }

        if (IsSegment(property, "documentType") || IsSegment(property, "documentNumber"))
        {
            return ValidationFailure(path, "The legal identity (document) is not patchable here; use PUT (it is uniqueness-checked).");
        }

        if (IsSegment(property, "effectiveFromUtc") || IsSegment(property, "effectiveToUtc"))
        {
            return ValidationFailure(path, "The effective date range is not patchable here; use PUT (the range is validated as a unit).");
        }

        if (IsSegment(property, "firstName"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "First name cannot be removed.");
            }

            state.FirstName = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "lastName"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Last name cannot be removed.");
            }

            state.LastName = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "positionTitle"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Position title cannot be removed.");
            }

            state.PositionTitle = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "representationType"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Representation type cannot be removed.");
            }

            state.RepresentationType = ReadEnum<LegalRepresentativeRepresentationType>(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "authorityDescription"))
        {
            state.AuthorityDescription = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "appointmentInstrument"))
        {
            state.AppointmentInstrument = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "appointmentDateUtc"))
        {
            state.AppointmentDateUtc = isRemove ? null : ReadNullableDateTime(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "email"))
        {
            state.Email = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "phone"))
        {
            state.Phone = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
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

    private static bool IsValidEmail(string email)
    {
        var index = email.IndexOf('@', StringComparison.Ordinal);
        return index > 0 &&
            index < email.Length - 1 &&
            email.IndexOf('@', index + 1) < 0;
    }

    private static string ReadRequiredString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new LegalRepresentativePatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new LegalRepresentativePatchValueException(path, "Value must be a string.");
    }

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new LegalRepresentativePatchValueException(path, "Value must be a string or null.");
    }

    private static DateTime? ReadNullableDateTime(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String && value.Value.TryGetDateTime(out var parsed)
            ? parsed
            : throw new LegalRepresentativePatchValueException(path, "Value must be an ISO-8601 date-time or null.");
    }

    private static TEnum ReadEnum<TEnum>(JsonElement? value, string path)
        where TEnum : struct, Enum
    {
        if (IsNull(value))
        {
            throw new LegalRepresentativePatchValueException(path, "Value is required.");
        }

        if (value!.Value.ValueKind == JsonValueKind.String &&
            Enum.TryParse<TEnum>(value.Value.GetString(), ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new LegalRepresentativePatchValueException(path, $"Value must be a valid {typeof(TEnum).Name}.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}
