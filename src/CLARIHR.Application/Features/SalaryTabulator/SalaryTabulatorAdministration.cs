using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.SalaryTabulator;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.SalaryTabulator.Common;
using CLARIHR.Domain.SalaryTabulator;
using FluentValidation;

namespace CLARIHR.Application.Features.SalaryTabulator;

public sealed record SalaryTabulatorLineListItemResponse(
    Guid Id,
    Guid? SalaryClassId,
    string SalaryScaleCode,
    string CurrencyCode,
    decimal BaseAmount,
    decimal? MinAmount,
    decimal? MaxAmount,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive,
    int Version,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record SalaryTabulatorLineResponse(
    Guid Id,
    Guid CompanyId,
    Guid? SalaryClassId,
    string SalaryScaleCode,
    string CurrencyCode,
    decimal BaseAmount,
    decimal? MinAmount,
    decimal? MaxAmount,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive,
    int Version,
    string? Notes,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record SalaryTabulatorLineExportRow(
    Guid Id,
    Guid? SalaryClassId,
    string SalaryScaleCode,
    string CurrencyCode,
    decimal BaseAmount,
    decimal? MinAmount,
    decimal? MaxAmount,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive,
    int Version,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record SalaryTabulatorChangeRequestItemResponse(
    Guid Id,
    Guid? SalaryClassId,
    string SalaryScaleCode,
    string CurrencyCode,
    SalaryTabulatorChangeType ChangeType,
    decimal? CurrentBaseAmount,
    decimal? ProposedBaseAmount,
    decimal? CurrentMinAmount,
    decimal? ProposedMinAmount,
    decimal? CurrentMaxAmount,
    decimal? ProposedMaxAmount,
    string? Notes);

public sealed record SalaryTabulatorChangeRequestResponse(
    Guid Id,
    Guid CompanyId,
    string RequestNumber,
    string Reason,
    SalaryTabulatorChangeRequestStatus Status,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    Guid RequestedByUserId,
    DateTime? SubmittedAtUtc,
    Guid? DecidedByUserId,
    DateTime? DecidedAtUtc,
    string? DecisionComment,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    IReadOnlyCollection<SalaryTabulatorChangeRequestItemResponse> Items,
    AllowedActionsResponse? AllowedActions = null);

public sealed record SalaryTabulatorChangeRequestListItemResponse(
    Guid Id,
    string RequestNumber,
    SalaryTabulatorChangeRequestStatus Status,
    DateTime EffectiveFromUtc,
    Guid RequestedByUserId,
    DateTime? SubmittedAtUtc,
    Guid? DecidedByUserId,
    DateTime? DecidedAtUtc,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    int ItemCount,
    AllowedActionsResponse? AllowedActions = null);

public sealed record SalaryTabulatorChangeRequestImpactItemResponse(
    Guid ItemId,
    Guid? SalaryClassId,
    string SalaryScaleCode,
    SalaryTabulatorChangeType ChangeType,
    decimal? CurrentBaseAmount,
    decimal? ProposedBaseAmount,
    decimal BaseDelta);

public sealed record SalaryTabulatorChangeRequestImpactResponse(
    Guid RequestId,
    string RequestNumber,
    SalaryTabulatorChangeRequestStatus Status,
    DateTime EffectiveFromUtc,
    int TotalItems,
    decimal TotalMonthlyDelta,
    decimal EstimatedAnnualDelta,
    IReadOnlyCollection<SalaryTabulatorChangeRequestImpactItemResponse> Items);

public sealed record SalaryTabulatorLineSnapshot(
    Guid Id,
    string CurrencyCode,
    decimal BaseAmount,
    decimal? MinAmount,
    decimal? MaxAmount,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc);

public sealed record SalaryTabulatorChangeRequestItemInput(
    Guid SalaryClassId,
    string SalaryScaleCode,
    string CurrencyCode,
    SalaryTabulatorChangeType ChangeType,
    decimal? ProposedBaseAmount,
    decimal? ProposedMinAmount,
    decimal? ProposedMaxAmount,
    string? Notes);

public sealed record SearchSalaryTabulatorLinesQuery(
    Guid CompanyId,
    Guid? SalaryClassId,
    string? SalaryScaleCode,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = SalaryTabulatorValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<SalaryTabulatorLineListItemResponse>>;

public sealed record GetSalaryTabulatorLineByIdQuery(Guid LineId) : IQuery<SalaryTabulatorLineResponse>;

public sealed record ExportSalaryTabulatorLinesQuery(
    Guid CompanyId,
    Guid? SalaryClassId,
    string? SalaryScaleCode,
    bool? IsActive,
    string? Search,
    int? MaxRows = null)
    : IQuery<IReadOnlyCollection<SalaryTabulatorLineExportRow>>;

public sealed record SearchSalaryTabulatorChangeRequestsQuery(
    Guid CompanyId,
    SalaryTabulatorChangeRequestStatus? Status,
    Guid? RequestedByUserId,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    int PageNumber = 1,
    int PageSize = SalaryTabulatorValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>>;

public sealed record GetSalaryTabulatorChangeRequestByIdQuery(Guid RequestId) : IQuery<SalaryTabulatorChangeRequestResponse>;

public sealed record GetSalaryTabulatorChangeRequestImpactQuery(Guid RequestId) : IQuery<SalaryTabulatorChangeRequestImpactResponse>;

public sealed record CreateSalaryTabulatorChangeRequestCommand(
    Guid CompanyId,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    IReadOnlyCollection<SalaryTabulatorChangeRequestItemInput> Items)
    : ICommand<SalaryTabulatorChangeRequestResponse>;

public sealed record UpdateSalaryTabulatorChangeRequestCommand(
    Guid RequestId,
    string Reason,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    IReadOnlyCollection<SalaryTabulatorChangeRequestItemInput> Items,
    Guid ConcurrencyToken)
    : ICommand<SalaryTabulatorChangeRequestResponse>;

public sealed record SubmitSalaryTabulatorChangeRequestCommand(
    Guid RequestId,
    Guid ConcurrencyToken)
    : ICommand<SalaryTabulatorChangeRequestResponse>;

public sealed record ApproveSalaryTabulatorChangeRequestCommand(
    Guid RequestId,
    string DecisionComment,
    Guid ConcurrencyToken)
    : ICommand<SalaryTabulatorChangeRequestResponse>;

public sealed record RejectSalaryTabulatorChangeRequestCommand(
    Guid RequestId,
    string DecisionComment,
    Guid ConcurrencyToken)
    : ICommand<SalaryTabulatorChangeRequestResponse>;

public sealed record CancelSalaryTabulatorChangeRequestCommand(
    Guid RequestId,
    Guid ConcurrencyToken)
    : ICommand<SalaryTabulatorChangeRequestResponse>;

internal sealed class SearchSalaryTabulatorLinesQueryValidator : AbstractValidator<SearchSalaryTabulatorLinesQuery>
{
    public SearchSalaryTabulatorLinesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.SalaryClassId)
            .NotEqual(Guid.Empty)
            .When(static query => query.SalaryClassId.HasValue);
        RuleFor(query => query.SalaryScaleCode)
            .MaximumLength(50)
            .Must(static value => value is null || SalaryTabulatorValidationRules.IsValidCode(value));
        RuleFor(query => query.Search)
            .MaximumLength(SalaryTabulatorValidationRules.MaxSearchLength)
            .Must(SalaryTabulatorValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {SalaryTabulatorValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, SalaryTabulatorValidationRules.MaxPageSize);
    }
}

internal sealed class GetSalaryTabulatorLineByIdQueryValidator : AbstractValidator<GetSalaryTabulatorLineByIdQuery>
{
    public GetSalaryTabulatorLineByIdQueryValidator() => RuleFor(query => query.LineId).NotEmpty();
}

internal sealed class ExportSalaryTabulatorLinesQueryValidator : AbstractValidator<ExportSalaryTabulatorLinesQuery>
{
    public ExportSalaryTabulatorLinesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.SalaryClassId)
            .NotEqual(Guid.Empty)
            .When(static query => query.SalaryClassId.HasValue);
        RuleFor(query => query.SalaryScaleCode)
            .MaximumLength(50)
            .Must(static value => value is null || SalaryTabulatorValidationRules.IsValidCode(value));
        RuleFor(query => query.Search)
            .MaximumLength(SalaryTabulatorValidationRules.MaxSearchLength)
            .Must(SalaryTabulatorValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {SalaryTabulatorValidationRules.MinSearchLength} characters when provided.");
    }
}

internal sealed class SearchSalaryTabulatorChangeRequestsQueryValidator : AbstractValidator<SearchSalaryTabulatorChangeRequestsQuery>
{
    public SearchSalaryTabulatorChangeRequestsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.RequestedByUserId)
            .NotEqual(Guid.Empty)
            .When(static query => query.RequestedByUserId.HasValue);
        RuleFor(query => query)
            .Must(static query => !query.EffectiveFromUtc.HasValue || !query.EffectiveToUtc.HasValue || query.EffectiveFromUtc <= query.EffectiveToUtc)
            .WithMessage("EffectiveFromUtc cannot be greater than EffectiveToUtc.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, SalaryTabulatorValidationRules.MaxPageSize);
    }
}

internal sealed class GetSalaryTabulatorChangeRequestByIdQueryValidator : AbstractValidator<GetSalaryTabulatorChangeRequestByIdQuery>
{
    public GetSalaryTabulatorChangeRequestByIdQueryValidator() => RuleFor(query => query.RequestId).NotEmpty();
}

internal sealed class GetSalaryTabulatorChangeRequestImpactQueryValidator : AbstractValidator<GetSalaryTabulatorChangeRequestImpactQuery>
{
    public GetSalaryTabulatorChangeRequestImpactQueryValidator() => RuleFor(query => query.RequestId).NotEmpty();
}

internal sealed class CreateSalaryTabulatorChangeRequestCommandValidator : AbstractValidator<CreateSalaryTabulatorChangeRequestCommand>
{
    public CreateSalaryTabulatorChangeRequestCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.EffectiveFromUtc).NotEqual(default(DateTime));
        RuleFor(command => command)
            .Must(static command => !command.EffectiveToUtc.HasValue || command.EffectiveToUtc.Value.Date >= command.EffectiveFromUtc.Date)
            .WithMessage("EffectiveToUtc cannot be less than EffectiveFromUtc.");
        RuleFor(command => command.Items).NotEmpty();
        RuleForEach(command => command.Items).SetValidator(new SalaryTabulatorChangeRequestItemInputValidator());
    }
}

internal sealed class UpdateSalaryTabulatorChangeRequestCommandValidator : AbstractValidator<UpdateSalaryTabulatorChangeRequestCommand>
{
    public UpdateSalaryTabulatorChangeRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.EffectiveFromUtc).NotEqual(default(DateTime));
        RuleFor(command => command)
            .Must(static command => !command.EffectiveToUtc.HasValue || command.EffectiveToUtc.Value.Date >= command.EffectiveFromUtc.Date)
            .WithMessage("EffectiveToUtc cannot be less than EffectiveFromUtc.");
        RuleFor(command => command.Items).NotEmpty();
        RuleForEach(command => command.Items).SetValidator(new SalaryTabulatorChangeRequestItemInputValidator());
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SubmitSalaryTabulatorChangeRequestCommandValidator : AbstractValidator<SubmitSalaryTabulatorChangeRequestCommand>
{
    public SubmitSalaryTabulatorChangeRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ApproveSalaryTabulatorChangeRequestCommandValidator : AbstractValidator<ApproveSalaryTabulatorChangeRequestCommand>
{
    public ApproveSalaryTabulatorChangeRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.DecisionComment).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class RejectSalaryTabulatorChangeRequestCommandValidator : AbstractValidator<RejectSalaryTabulatorChangeRequestCommand>
{
    public RejectSalaryTabulatorChangeRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.DecisionComment).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class CancelSalaryTabulatorChangeRequestCommandValidator : AbstractValidator<CancelSalaryTabulatorChangeRequestCommand>
{
    public CancelSalaryTabulatorChangeRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SalaryTabulatorChangeRequestItemInputValidator : AbstractValidator<SalaryTabulatorChangeRequestItemInput>
{
    public SalaryTabulatorChangeRequestItemInputValidator()
    {
        RuleFor(item => item.SalaryClassId).NotEmpty();
        RuleFor(item => item.SalaryScaleCode)
            .NotEmpty()
            .MaximumLength(50)
            .Must(SalaryTabulatorValidationRules.IsValidCode)
            .WithMessage("SalaryScaleCode format is invalid.");
        RuleFor(item => item.CurrencyCode)
            .NotEmpty()
            .Must(SalaryTabulatorValidationRules.IsValidCurrency)
            .WithMessage("CurrencyCode must be a 3-letter ISO code.");

        RuleFor(item => item.ProposedBaseAmount)
            .NotNull()
            .When(static item => item.ChangeType != SalaryTabulatorChangeType.Inactivate);
        RuleFor(item => item.ProposedBaseAmount)
            .GreaterThan(0)
            .When(static item => item.ChangeType != SalaryTabulatorChangeType.Inactivate && item.ProposedBaseAmount.HasValue);
        RuleFor(item => item)
            .Must(static item => !item.ProposedMinAmount.HasValue || !item.ProposedMaxAmount.HasValue || item.ProposedMinAmount <= item.ProposedMaxAmount)
            .WithMessage("ProposedMinAmount cannot be greater than ProposedMaxAmount.");
        RuleFor(item => item)
            .Must(static item => !item.ProposedMinAmount.HasValue || !item.ProposedBaseAmount.HasValue || item.ProposedBaseAmount >= item.ProposedMinAmount)
            .WithMessage("ProposedBaseAmount cannot be less than ProposedMinAmount.");
        RuleFor(item => item)
            .Must(static item => !item.ProposedMaxAmount.HasValue || !item.ProposedBaseAmount.HasValue || item.ProposedBaseAmount <= item.ProposedMaxAmount)
            .WithMessage("ProposedBaseAmount cannot be greater than ProposedMaxAmount.");
        RuleFor(item => item.Notes).MaximumLength(1000);
    }
}

internal sealed class SearchSalaryTabulatorLinesQueryHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    IPositionCatalogLookup positionDescriptionCatalogRepository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchSalaryTabulatorLinesQuery, PagedResponse<SalaryTabulatorLineListItemResponse>>
{
    public async Task<Result<PagedResponse<SalaryTabulatorLineListItemResponse>>> Handle(
        SearchSalaryTabulatorLinesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<SalaryTabulatorLineListItemResponse>>.Failure(authorizationResult.Error);
        }

        string? salaryClassCode = null;
        if (query.SalaryClassId.HasValue)
        {
            salaryClassCode = await positionDescriptionCatalogRepository.ResolveSalaryClassCodeByCatalogIdAsync(
                query.CompanyId,
                query.SalaryClassId.Value,
                cancellationToken);
            if (salaryClassCode is null)
            {
                return Result<PagedResponse<SalaryTabulatorLineListItemResponse>>.Success(
                    new PagedResponse<SalaryTabulatorLineListItemResponse>([], query.PageNumber, query.PageSize, 0));
            }
        }

        var response = await repository.SearchLinesAsync(
            query.CompanyId,
            salaryClassCode,
            query.SalaryScaleCode,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<SalaryTabulatorLineListItemResponse>>.Success(response);
        }

        var canRequest = (await authorizationService.EnsureCanRequestAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => SalaryTabulatorPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canRequest))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<SalaryTabulatorLineListItemResponse>>.Success(response);
    }
}

internal sealed class GetSalaryTabulatorLineByIdQueryHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetSalaryTabulatorLineByIdQuery, SalaryTabulatorLineResponse>
{
    public async Task<Result<SalaryTabulatorLineResponse>> Handle(
        GetSalaryTabulatorLineByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<SalaryTabulatorLineResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SalaryTabulatorLineResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetLineResponseByIdAsync(query.LineId, cancellationToken);
        if (response is not null)
        {
            var canRequest = (await authorizationService.EnsureCanRequestAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = SalaryTabulatorPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canRequest);
            return Result<SalaryTabulatorLineResponse>.Success(response);
        }

        return Result<SalaryTabulatorLineResponse>.Failure(
            await repository.LineExistsOutsideTenantAsync(query.LineId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : SalaryTabulatorErrors.LineNotFound);
    }
}

internal sealed class ExportSalaryTabulatorLinesQueryHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    IPositionCatalogLookup positionDescriptionCatalogRepository)
    : IQueryHandler<ExportSalaryTabulatorLinesQuery, IReadOnlyCollection<SalaryTabulatorLineExportRow>>
{
    public async Task<Result<IReadOnlyCollection<SalaryTabulatorLineExportRow>>> Handle(
        ExportSalaryTabulatorLinesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<SalaryTabulatorLineExportRow>>.Failure(authorizationResult.Error);
        }

        string? salaryClassCode = null;
        if (query.SalaryClassId.HasValue)
        {
            salaryClassCode = await positionDescriptionCatalogRepository.ResolveSalaryClassCodeByCatalogIdAsync(
                query.CompanyId,
                query.SalaryClassId.Value,
                cancellationToken);
            if (salaryClassCode is null)
            {
                return Result<IReadOnlyCollection<SalaryTabulatorLineExportRow>>.Success([]);
            }
        }

        var rows = await repository.GetLineExportRowsAsync(
            query.CompanyId,
            salaryClassCode,
            query.SalaryScaleCode,
            query.IsActive,
            query.Search,
            query.MaxRows,
            cancellationToken);

        return Result<IReadOnlyCollection<SalaryTabulatorLineExportRow>>.Success(rows);
    }
}

internal sealed class SearchSalaryTabulatorChangeRequestsQueryHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    IResourceActionPolicyService resourceActionPolicyService,
    ICurrentUserService currentUserService)
    : IQueryHandler<SearchSalaryTabulatorChangeRequestsQuery, PagedResponse<SalaryTabulatorChangeRequestListItemResponse>>
{
    public async Task<Result<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>>> Handle(
        SearchSalaryTabulatorChangeRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchChangeRequestsAsync(
            query.CompanyId,
            query.Status,
            query.RequestedByUserId,
            query.EffectiveFromUtc,
            query.EffectiveToUtc,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>>.Success(response);
        }

        var canRequest = (await authorizationService.EnsureCanRequestAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var canApprove = (await authorizationService.EnsureCanApproveAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var currentUserId = SalaryTabulatorPolicyAdapter.ResolveCurrentUserId(currentUserService);
        var items = response.Items
            .Select(item => SalaryTabulatorPolicyAdapter.ApplyAllowedActions(
                item,
                resourceActionPolicyService,
                canRequest,
                canApprove,
                currentUserId))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>>.Success(response);
    }
}

internal sealed class GetSalaryTabulatorChangeRequestByIdQueryHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService,
    ICurrentUserService currentUserService)
    : IQueryHandler<GetSalaryTabulatorChangeRequestByIdQuery, SalaryTabulatorChangeRequestResponse>
{
    public async Task<Result<SalaryTabulatorChangeRequestResponse>> Handle(
        GetSalaryTabulatorChangeRequestByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetChangeRequestResponseByIdAsync(query.RequestId, cancellationToken);
        if (response is not null)
        {
            var canRequest = (await authorizationService.EnsureCanRequestAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            var canApprove = (await authorizationService.EnsureCanApproveAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            var currentUserId = SalaryTabulatorPolicyAdapter.ResolveCurrentUserId(currentUserService);
            response = SalaryTabulatorPolicyAdapter.ApplyAllowedActions(
                response,
                resourceActionPolicyService,
                canRequest,
                canApprove,
                currentUserId);
            return Result<SalaryTabulatorChangeRequestResponse>.Success(response);
        }

        return Result<SalaryTabulatorChangeRequestResponse>.Failure(
            await repository.ChangeRequestExistsOutsideTenantAsync(query.RequestId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : SalaryTabulatorErrors.ChangeRequestNotFound);
    }
}

internal sealed class GetSalaryTabulatorChangeRequestImpactQueryHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetSalaryTabulatorChangeRequestImpactQuery, SalaryTabulatorChangeRequestImpactResponse>
{
    public async Task<Result<SalaryTabulatorChangeRequestImpactResponse>> Handle(
        GetSalaryTabulatorChangeRequestImpactQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<SalaryTabulatorChangeRequestImpactResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SalaryTabulatorChangeRequestImpactResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetChangeRequestImpactByIdAsync(query.RequestId, cancellationToken);
        if (response is not null)
        {
            return Result<SalaryTabulatorChangeRequestImpactResponse>.Success(response);
        }

        return Result<SalaryTabulatorChangeRequestImpactResponse>.Failure(
            await repository.ChangeRequestExistsOutsideTenantAsync(query.RequestId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : SalaryTabulatorErrors.ChangeRequestNotFound);
    }
}

internal static class SalaryTabulatorPolicyAdapter
{
    private const string ActionEdit = "edit";
    private const string ActionInactivate = "inactivate";
    private const string ActionSubmit = "submit";
    private const string ActionApprove = "approve";
    private const string ActionReject = "reject";
    private const string ActionCancel = "cancel";

    public static Guid? ResolveCurrentUserId(ICurrentUserService currentUserService) =>
        Guid.TryParse(currentUserService.UserId, out var currentUserId)
            ? currentUserId
            : null;

    public static SalaryTabulatorLineListItemResponse ApplyAllowedActions(
        SalaryTabulatorLineListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canRequest)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                SalaryTabulatorPermissionCodes.ResourceKey,
                response.IsActive ? "Active" : "Inactive",
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canRequest,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: false,
                SupportsInactivate: true,
                InactivateAllowed: canRequest,
                NonEditableStates: ["Inactive"]));

        allowedActions = allowedActions with
        {
            ActionPermissions =
            [
                CreateActionPermission(
                    ActionEdit,
                    SalaryTabulatorPermissionCodes.Request,
                    allowedActions.CanEdit,
                    GetEditReasons(canRequest, response.IsActive, response.IsActive ? "Active" : "Inactive")),
                CreateActionPermission(
                    ActionInactivate,
                    SalaryTabulatorPermissionCodes.Request,
                    allowedActions.CanInactivate,
                    GetInactivateReasons(canRequest, response.IsActive))
            ]
        };

        return response with { AllowedActions = allowedActions };
    }

    public static SalaryTabulatorLineResponse ApplyAllowedActions(
        SalaryTabulatorLineResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canRequest)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                SalaryTabulatorPermissionCodes.ResourceKey,
                response.IsActive ? "Active" : "Inactive",
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canRequest,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: false,
                SupportsInactivate: true,
                InactivateAllowed: canRequest,
                NonEditableStates: ["Inactive"]));

        allowedActions = allowedActions with
        {
            ActionPermissions =
            [
                CreateActionPermission(
                    ActionEdit,
                    SalaryTabulatorPermissionCodes.Request,
                    allowedActions.CanEdit,
                    GetEditReasons(canRequest, response.IsActive, response.IsActive ? "Active" : "Inactive")),
                CreateActionPermission(
                    ActionInactivate,
                    SalaryTabulatorPermissionCodes.Request,
                    allowedActions.CanInactivate,
                    GetInactivateReasons(canRequest, response.IsActive))
            ]
        };

        return response with { AllowedActions = allowedActions };
    }

    public static SalaryTabulatorChangeRequestListItemResponse ApplyAllowedActions(
        SalaryTabulatorChangeRequestListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canRequest,
        bool canApprove,
        Guid? currentUserId)
    {
        var isActive = response.Status is SalaryTabulatorChangeRequestStatus.Draft or SalaryTabulatorChangeRequestStatus.Submitted;
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                SalaryTabulatorPermissionCodes.ResourceKey,
                response.Status.ToString(),
                isActive,
                SupportsEdit: true,
                EditAllowed: canRequest,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: false,
                SupportsInactivate: false,
                NonEditableStates:
                [
                    SalaryTabulatorChangeRequestStatus.Submitted.ToString(),
                    SalaryTabulatorChangeRequestStatus.Approved.ToString(),
                    SalaryTabulatorChangeRequestStatus.Rejected.ToString(),
                    SalaryTabulatorChangeRequestStatus.Canceled.ToString()
                ]));

        allowedActions = ApplyChangeRequestWorkflowActions(
            allowedActions,
            response.Status,
            response.RequestedByUserId,
            canRequest,
            canApprove,
            currentUserId);

        return response with { AllowedActions = allowedActions };
    }

    public static SalaryTabulatorChangeRequestResponse ApplyAllowedActions(
        SalaryTabulatorChangeRequestResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canRequest,
        bool canApprove,
        Guid? currentUserId)
    {
        var isActive = response.Status is SalaryTabulatorChangeRequestStatus.Draft or SalaryTabulatorChangeRequestStatus.Submitted;
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                SalaryTabulatorPermissionCodes.ResourceKey,
                response.Status.ToString(),
                isActive,
                SupportsEdit: true,
                EditAllowed: canRequest,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: false,
                SupportsInactivate: false,
                NonEditableStates:
                [
                    SalaryTabulatorChangeRequestStatus.Submitted.ToString(),
                    SalaryTabulatorChangeRequestStatus.Approved.ToString(),
                    SalaryTabulatorChangeRequestStatus.Rejected.ToString(),
                    SalaryTabulatorChangeRequestStatus.Canceled.ToString()
                ]));

        allowedActions = ApplyChangeRequestWorkflowActions(
            allowedActions,
            response.Status,
            response.RequestedByUserId,
            canRequest,
            canApprove,
            currentUserId);

        return response with { AllowedActions = allowedActions };
    }

    private static AllowedActionsResponse ApplyChangeRequestWorkflowActions(
        AllowedActionsResponse allowedActions,
        SalaryTabulatorChangeRequestStatus status,
        Guid requestedByUserId,
        bool canRequest,
        bool canApprove,
        Guid? currentUserId)
    {
        var canSubmit = status == SalaryTabulatorChangeRequestStatus.Draft && canRequest;
        var canCancel = status == SalaryTabulatorChangeRequestStatus.Draft && canRequest;
        var canApproveWorkflow =
            status == SalaryTabulatorChangeRequestStatus.Submitted &&
            canApprove &&
            currentUserId.HasValue &&
            currentUserId.Value != requestedByUserId;
        var canReject = status == SalaryTabulatorChangeRequestStatus.Submitted && canApprove && currentUserId.HasValue;

        return allowedActions with
        {
            CanSubmit = canSubmit,
            CanApprove = canApproveWorkflow,
            CanReject = canReject,
            CanCancel = canCancel,
            ActionPermissions =
            [
                CreateActionPermission(
                    ActionEdit,
                    SalaryTabulatorPermissionCodes.Request,
                    allowedActions.CanEdit,
                    GetEditReasons(canRequest, status == SalaryTabulatorChangeRequestStatus.Draft, status.ToString())),
                CreateActionPermission(
                    ActionSubmit,
                    SalaryTabulatorPermissionCodes.Request,
                    canSubmit,
                    GetSubmitReasons(status, canRequest)),
                CreateActionPermission(
                    ActionCancel,
                    SalaryTabulatorPermissionCodes.Request,
                    canCancel,
                    GetCancelReasons(status, canRequest)),
                CreateActionPermission(
                    ActionApprove,
                    SalaryTabulatorPermissionCodes.Approve,
                    canApproveWorkflow,
                    GetApproveReasons(status, canApprove, currentUserId, requestedByUserId)),
                CreateActionPermission(
                    ActionReject,
                    SalaryTabulatorPermissionCodes.Approve,
                    canReject,
                    GetRejectReasons(status, canApprove, currentUserId))
            ]
        };
    }

    private static AllowedActionPermissionResponse CreateActionPermission(
        string action,
        string permissionCode,
        bool allowed,
        IReadOnlyCollection<string> reasons) =>
        new(action, permissionCode, allowed, reasons);

    private static IReadOnlyCollection<string> GetEditReasons(bool hasPermission, bool isEditableState, string state)
    {
        var reasons = new List<string>();
        if (!hasPermission)
        {
            reasons.Add("The current user is not authorized to edit this record.");
        }

        if (!isEditableState)
        {
            reasons.Add($"Records in state '{state}' cannot be edited.");
        }

        return reasons;
    }

    private static IReadOnlyCollection<string> GetInactivateReasons(bool hasPermission, bool isActive)
    {
        var reasons = new List<string>();
        if (!hasPermission)
        {
            reasons.Add("The current user is not authorized to inactivate this record.");
        }

        if (!isActive)
        {
            reasons.Add("The record is already inactive.");
        }

        return reasons;
    }

    private static IReadOnlyCollection<string> GetSubmitReasons(SalaryTabulatorChangeRequestStatus status, bool hasPermission)
    {
        var reasons = new List<string>();
        if (!hasPermission)
        {
            reasons.Add("The current user is not authorized to request salary tabulator changes.");
        }

        if (status != SalaryTabulatorChangeRequestStatus.Draft)
        {
            reasons.Add("Only draft change requests can be submitted.");
        }

        return reasons;
    }

    private static IReadOnlyCollection<string> GetCancelReasons(SalaryTabulatorChangeRequestStatus status, bool hasPermission)
    {
        var reasons = new List<string>();
        if (!hasPermission)
        {
            reasons.Add("The current user is not authorized to request salary tabulator changes.");
        }

        if (status != SalaryTabulatorChangeRequestStatus.Draft)
        {
            reasons.Add("Only draft change requests can be canceled from the workflow action menu.");
        }

        return reasons;
    }

    private static IReadOnlyCollection<string> GetApproveReasons(
        SalaryTabulatorChangeRequestStatus status,
        bool hasPermission,
        Guid? currentUserId,
        Guid requestedByUserId)
    {
        var reasons = new List<string>();
        if (!hasPermission)
        {
            reasons.Add("The current user is not authorized to approve salary tabulator change requests.");
        }

        if (!currentUserId.HasValue)
        {
            reasons.Add("The current user could not be resolved for approval.");
        }
        else if (currentUserId.Value == requestedByUserId)
        {
            reasons.Add("Requester cannot approve their own salary tabulator request.");
        }

        if (status != SalaryTabulatorChangeRequestStatus.Submitted)
        {
            reasons.Add("Only submitted change requests can be approved.");
        }

        return reasons;
    }

    private static IReadOnlyCollection<string> GetRejectReasons(
        SalaryTabulatorChangeRequestStatus status,
        bool hasPermission,
        Guid? currentUserId)
    {
        var reasons = new List<string>();
        if (!hasPermission)
        {
            reasons.Add("The current user is not authorized to reject salary tabulator change requests.");
        }

        if (!currentUserId.HasValue)
        {
            reasons.Add("The current user could not be resolved for rejection.");
        }

        if (status != SalaryTabulatorChangeRequestStatus.Submitted)
        {
            reasons.Add("Only submitted change requests can be rejected.");
        }

        return reasons;
    }
}

internal sealed class CreateSalaryTabulatorChangeRequestCommandHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    IPositionCatalogLookup positionDescriptionCatalogRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateSalaryTabulatorChangeRequestCommand, SalaryTabulatorChangeRequestResponse>
{
    public async Task<Result<SalaryTabulatorChangeRequestResponse>> Handle(
        CreateSalaryTabulatorChangeRequestCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanRequestAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(authorizationResult.Error);
        }

        if (!Guid.TryParse(currentUserService.UserId, out var requestedByUserId))
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var itemResult = await SalaryTabulatorCommandSupport.BuildItemsAsync(
            command.CompanyId,
            command.EffectiveFromUtc,
            command.Items,
            repository,
            positionDescriptionCatalogRepository,
            cancellationToken);
        if (itemResult.IsFailure)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(itemResult.Error);
        }

        SalaryTabulatorChangeRequest request;
        try
        {
            request = SalaryTabulatorChangeRequest.Create(
                SalaryTabulatorCommandSupport.GenerateRequestNumber(dateTimeProvider.UtcNow),
                SalaryTabulatorCommandSupport.DefaultCreateReason,
                command.EffectiveFromUtc,
                command.EffectiveToUtc,
                requestedByUserId,
                itemResult.Value);
        }
        catch (InvalidOperationException exception)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorCommandSupport.MapDomainValidation(exception));
        }

        request.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddChangeRequest(request);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetChangeRequestResponseByIdAsync(request.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Salary tabulator change request could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.SalaryTabulatorRequestCreated,
                    AuditEntityTypes.SalaryTabulatorChangeRequest,
                    request.PublicId,
                    request.RequestNumber,
                    AuditActions.Create,
                    $"Created salary tabulator change request {request.RequestNumber}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<SalaryTabulatorChangeRequestResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateSalaryTabulatorChangeRequestCommandHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    IPositionCatalogLookup positionDescriptionCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateSalaryTabulatorChangeRequestCommand, SalaryTabulatorChangeRequestResponse>
{
    public async Task<Result<SalaryTabulatorChangeRequestResponse>> Handle(
        UpdateSalaryTabulatorChangeRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanRequestAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(authorizationResult.Error);
        }

        var request = await repository.GetChangeRequestByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(
                await repository.ChangeRequestExistsOutsideTenantAsync(command.RequestId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : SalaryTabulatorErrors.ChangeRequestNotFound);
        }

        if (request.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorErrors.ConcurrencyConflict);
        }

        var itemResult = await SalaryTabulatorCommandSupport.BuildItemsAsync(
            request.TenantId,
            command.EffectiveFromUtc,
            command.Items,
            repository,
            positionDescriptionCatalogRepository,
            cancellationToken);
        if (itemResult.IsFailure)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(itemResult.Error);
        }

        var before = await repository.GetChangeRequestResponseByIdAsync(request.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Salary tabulator change request could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            request.UpdateDraft(command.Reason, command.EffectiveFromUtc, command.EffectiveToUtc, itemResult.Value);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetChangeRequestResponseByIdAsync(request.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Salary tabulator change request could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.SalaryTabulatorRequestUpdated,
                    AuditEntityTypes.SalaryTabulatorChangeRequest,
                    request.PublicId,
                    request.RequestNumber,
                    AuditActions.Update,
                    $"Updated salary tabulator change request {request.RequestNumber}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<SalaryTabulatorChangeRequestResponse>.Success(after);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorCommandSupport.MapDomainValidation(exception));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class SubmitSalaryTabulatorChangeRequestCommandHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SubmitSalaryTabulatorChangeRequestCommand, SalaryTabulatorChangeRequestResponse>
{
    public async Task<Result<SalaryTabulatorChangeRequestResponse>> Handle(
        SubmitSalaryTabulatorChangeRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanRequestAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(authorizationResult.Error);
        }

        var request = await repository.GetChangeRequestByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(
                await repository.ChangeRequestExistsOutsideTenantAsync(command.RequestId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : SalaryTabulatorErrors.ChangeRequestNotFound);
        }

        if (request.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorErrors.ConcurrencyConflict);
        }

        var before = await repository.GetChangeRequestResponseByIdAsync(request.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Salary tabulator change request could not be resolved before submit.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            request.Submit(dateTimeProvider.UtcNow);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetChangeRequestResponseByIdAsync(request.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Salary tabulator change request could not be resolved after submit.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.SalaryTabulatorRequestSubmitted,
                    AuditEntityTypes.SalaryTabulatorChangeRequest,
                    request.PublicId,
                    request.RequestNumber,
                    AuditActions.Update,
                    $"Submitted salary tabulator change request {request.RequestNumber}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<SalaryTabulatorChangeRequestResponse>.Success(after);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorCommandSupport.MapDomainValidation(exception));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ApproveSalaryTabulatorChangeRequestCommandHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ApproveSalaryTabulatorChangeRequestCommand, SalaryTabulatorChangeRequestResponse>
{
    public async Task<Result<SalaryTabulatorChangeRequestResponse>> Handle(
        ApproveSalaryTabulatorChangeRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanApproveAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(authorizationResult.Error);
        }

        if (!Guid.TryParse(currentUserService.UserId, out var decidedByUserId))
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var request = await repository.GetChangeRequestByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(
                await repository.ChangeRequestExistsOutsideTenantAsync(command.RequestId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : SalaryTabulatorErrors.ChangeRequestNotFound);
        }

        if (request.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorErrors.ConcurrencyConflict);
        }

        var before = await repository.GetChangeRequestResponseByIdAsync(request.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Salary tabulator change request could not be resolved before approval.");

        var allowSelfApproval = false;
        var lineAuditEvents = new List<SalaryTabulatorLineAuditPayload>();
        var affectedCoverageKeys = request.Items
            .Select(static item => (item.NormalizedSalaryClassCode, item.NormalizedSalaryScaleCode))
            .Distinct()
            .ToArray();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var item in request.Items)
            {
                var applyResult = await SalaryTabulatorCommandSupport.ApplyChangeRequestItemAsync(
                    request.TenantId,
                    request.EffectiveFromUtc.Date,
                    request.EffectiveToUtc,
                    item,
                    repository,
                    cancellationToken);
                if (applyResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<SalaryTabulatorChangeRequestResponse>.Failure(applyResult.Error);
                }

                lineAuditEvents.AddRange(applyResult.Value);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var (normalizedSalaryClassCode, normalizedSalaryScaleCode) in affectedCoverageKeys)
            {
                if (await repository.HasUncoveredJobProfileCompensationReferenceAsync(
                        request.TenantId,
                        normalizedSalaryClassCode,
                        normalizedSalaryScaleCode,
                        dateTimeProvider.UtcNow.Date,
                        cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorErrors.JobProfileCoverageConflict);
                }
            }

            request.Approve(decidedByUserId, dateTimeProvider.UtcNow, command.DecisionComment, allowSelfApproval);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetChangeRequestResponseByIdAsync(request.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Salary tabulator change request could not be resolved after approval.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.SalaryTabulatorRequestApproved,
                    AuditEntityTypes.SalaryTabulatorChangeRequest,
                    request.PublicId,
                    request.RequestNumber,
                    AuditActions.Update,
                    $"Approved salary tabulator change request {request.RequestNumber}.",
                    Before: before,
                    After: after),
                cancellationToken);

            foreach (var lineAudit in lineAuditEvents)
            {
                await auditService.LogAsync(
                    new AuditLogEntry(
                        lineAudit.EventType,
                        AuditEntityTypes.SalaryTabulatorLine,
                        lineAudit.EntityId,
                        lineAudit.EntityKey,
                        AuditActions.Update,
                        lineAudit.Summary,
                        Before: lineAudit.Before,
                        After: lineAudit.After),
                    cancellationToken);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<SalaryTabulatorChangeRequestResponse>.Success(after);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorCommandSupport.MapDomainValidation(exception));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RejectSalaryTabulatorChangeRequestCommandHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RejectSalaryTabulatorChangeRequestCommand, SalaryTabulatorChangeRequestResponse>
{
    public async Task<Result<SalaryTabulatorChangeRequestResponse>> Handle(
        RejectSalaryTabulatorChangeRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanApproveAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(authorizationResult.Error);
        }

        if (!Guid.TryParse(currentUserService.UserId, out var decidedByUserId))
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var request = await repository.GetChangeRequestByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(
                await repository.ChangeRequestExistsOutsideTenantAsync(command.RequestId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : SalaryTabulatorErrors.ChangeRequestNotFound);
        }

        if (request.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorErrors.ConcurrencyConflict);
        }

        var before = await repository.GetChangeRequestResponseByIdAsync(request.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Salary tabulator change request could not be resolved before rejection.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            request.Reject(decidedByUserId, dateTimeProvider.UtcNow, command.DecisionComment);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetChangeRequestResponseByIdAsync(request.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Salary tabulator change request could not be resolved after rejection.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.SalaryTabulatorRequestRejected,
                    AuditEntityTypes.SalaryTabulatorChangeRequest,
                    request.PublicId,
                    request.RequestNumber,
                    AuditActions.Update,
                    $"Rejected salary tabulator change request {request.RequestNumber}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<SalaryTabulatorChangeRequestResponse>.Success(after);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorCommandSupport.MapDomainValidation(exception));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class CancelSalaryTabulatorChangeRequestCommandHandler(
    ISalaryTabulatorAuthorizationService authorizationService,
    ISalaryTabulatorRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CancelSalaryTabulatorChangeRequestCommand, SalaryTabulatorChangeRequestResponse>
{
    public async Task<Result<SalaryTabulatorChangeRequestResponse>> Handle(
        CancelSalaryTabulatorChangeRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanRequestAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(authorizationResult.Error);
        }

        var request = await repository.GetChangeRequestByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(
                await repository.ChangeRequestExistsOutsideTenantAsync(command.RequestId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : SalaryTabulatorErrors.ChangeRequestNotFound);
        }

        if (request.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorErrors.ConcurrencyConflict);
        }

        var before = await repository.GetChangeRequestResponseByIdAsync(request.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Salary tabulator change request could not be resolved before cancellation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            request.Cancel();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetChangeRequestResponseByIdAsync(request.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Salary tabulator change request could not be resolved after cancellation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.SalaryTabulatorRequestCanceled,
                    AuditEntityTypes.SalaryTabulatorChangeRequest,
                    request.PublicId,
                    request.RequestNumber,
                    AuditActions.Update,
                    $"Canceled salary tabulator change request {request.RequestNumber}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<SalaryTabulatorChangeRequestResponse>.Success(after);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<SalaryTabulatorChangeRequestResponse>.Failure(SalaryTabulatorCommandSupport.MapDomainValidation(exception));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed record SalaryTabulatorLineAuditPayload(
    string EventType,
    Guid EntityId,
    string EntityKey,
    string Summary,
    object? Before,
    object? After);

internal static class SalaryTabulatorCommandSupport
{
    public const string DefaultCreateReason = "Salary tabulator line creation request.";

    public static async Task<Result<IReadOnlyCollection<SalaryTabulatorChangeRequestItem>>> BuildItemsAsync(
        Guid tenantId,
        DateTime effectiveFromUtc,
        IReadOnlyCollection<SalaryTabulatorChangeRequestItemInput> inputs,
        ISalaryTabulatorRepository repository,
        IPositionCatalogLookup positionDescriptionCatalogRepository,
        CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            return Result<IReadOnlyCollection<SalaryTabulatorChangeRequestItem>>.Failure(SalaryTabulatorErrors.RequestItemRequired);
        }

        var items = new List<SalaryTabulatorChangeRequestItem>(inputs.Count);
        foreach (var input in inputs)
        {
            var salaryClassCode = await positionDescriptionCatalogRepository.ResolveSalaryClassCodeByCatalogIdAsync(
                tenantId,
                input.SalaryClassId,
                cancellationToken);
            if (salaryClassCode is null)
            {
                return Result<IReadOnlyCollection<SalaryTabulatorChangeRequestItem>>.Failure(SalaryTabulatorErrors.SalaryClassNotFound);
            }

            var snapshot = await repository.GetActiveLineSnapshotAsync(
                tenantId,
                salaryClassCode.Trim().ToUpperInvariant(),
                input.SalaryScaleCode.Trim().ToUpperInvariant(),
                effectiveFromUtc.Date,
                cancellationToken);

            if (input.ChangeType != SalaryTabulatorChangeType.Create && snapshot is null)
            {
                return Result<IReadOnlyCollection<SalaryTabulatorChangeRequestItem>>.Failure(SalaryTabulatorErrors.LineNotFound);
            }

            if (input.ChangeType == SalaryTabulatorChangeType.Create && snapshot is not null)
            {
                return Result<IReadOnlyCollection<SalaryTabulatorChangeRequestItem>>.Failure(SalaryTabulatorErrors.EffectiveDateOverlap);
            }

            try
            {
                items.Add(SalaryTabulatorChangeRequestItem.Create(
                    salaryClassCode,
                    input.SalaryScaleCode,
                    input.CurrencyCode,
                    input.ChangeType,
                    snapshot?.BaseAmount,
                    input.ProposedBaseAmount,
                    snapshot?.MinAmount,
                    input.ProposedMinAmount,
                    snapshot?.MaxAmount,
                    input.ProposedMaxAmount,
                    input.Notes));
            }
            catch (InvalidOperationException exception)
            {
                return Result<IReadOnlyCollection<SalaryTabulatorChangeRequestItem>>.Failure(MapDomainValidation(exception));
            }
        }

        return Result<IReadOnlyCollection<SalaryTabulatorChangeRequestItem>>.Success(items);
    }

    public static async Task<Result<IReadOnlyCollection<SalaryTabulatorLineAuditPayload>>> ApplyChangeRequestItemAsync(
        Guid tenantId,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        SalaryTabulatorChangeRequestItem item,
        ISalaryTabulatorRepository repository,
        CancellationToken cancellationToken)
    {
        var existingLine = await repository.GetActiveLineEntityAsync(
            tenantId,
            item.NormalizedSalaryClassCode,
            item.NormalizedSalaryScaleCode,
            effectiveFromUtc,
            cancellationToken);

        var auditEntries = new List<SalaryTabulatorLineAuditPayload>();
        switch (item.ChangeType)
        {
            case SalaryTabulatorChangeType.Create:
            {
                if (existingLine is not null ||
                    await repository.HasLineWithEffectiveFromOnOrAfterAsync(
                        tenantId,
                        item.NormalizedSalaryClassCode,
                        item.NormalizedSalaryScaleCode,
                        effectiveFromUtc,
                        excludingLineId: null,
                        cancellationToken))
                {
                    return Result<IReadOnlyCollection<SalaryTabulatorLineAuditPayload>>.Failure(SalaryTabulatorErrors.EffectiveDateOverlap);
                }

                var createdLine = SalaryTabulatorLine.Create(
                    item.SalaryClassCode,
                    item.SalaryScaleCode,
                    item.CurrencyCode,
                    item.ProposedBaseAmount!.Value,
                    item.ProposedMinAmount,
                    item.ProposedMaxAmount,
                    effectiveFromUtc,
                    effectiveToUtc,
                    item.Notes);
                createdLine.SetTenantId(tenantId);
                repository.AddLine(createdLine);

                auditEntries.Add(new SalaryTabulatorLineAuditPayload(
                    AuditEventTypes.SalaryTabulatorLineApplied,
                    createdLine.PublicId,
                    $"{createdLine.SalaryClassCode}:{createdLine.SalaryScaleCode}",
                    $"Applied salary tabulator line {createdLine.SalaryClassCode}/{createdLine.SalaryScaleCode}.",
                    Before: null,
                    After: ToLineAuditPayload(createdLine)));
                return Result<IReadOnlyCollection<SalaryTabulatorLineAuditPayload>>.Success(auditEntries);
            }
            case SalaryTabulatorChangeType.Update:
            {
                if (existingLine is null)
                {
                    return Result<IReadOnlyCollection<SalaryTabulatorLineAuditPayload>>.Failure(SalaryTabulatorErrors.LineNotFound);
                }

                if (effectiveFromUtc < existingLine.EffectiveFromUtc)
                {
                    return Result<IReadOnlyCollection<SalaryTabulatorLineAuditPayload>>.Failure(SalaryTabulatorErrors.EffectiveDateOverlap);
                }

                if (effectiveFromUtc == existingLine.EffectiveFromUtc)
                {
                    var before = ToLineAuditPayload(existingLine);
                    existingLine.ApplySameDateUpdate(
                        item.CurrencyCode,
                        item.ProposedBaseAmount!.Value,
                        item.ProposedMinAmount,
                        item.ProposedMaxAmount,
                        item.Notes);

                    auditEntries.Add(new SalaryTabulatorLineAuditPayload(
                        AuditEventTypes.SalaryTabulatorLineApplied,
                        existingLine.PublicId,
                        $"{existingLine.SalaryClassCode}:{existingLine.SalaryScaleCode}",
                        $"Applied salary tabulator update for {existingLine.SalaryClassCode}/{existingLine.SalaryScaleCode}.",
                        Before: before,
                        After: ToLineAuditPayload(existingLine)));
                    return Result<IReadOnlyCollection<SalaryTabulatorLineAuditPayload>>.Success(auditEntries);
                }

                if (await repository.HasLineWithEffectiveFromOnOrAfterAsync(
                        tenantId,
                        item.NormalizedSalaryClassCode,
                        item.NormalizedSalaryScaleCode,
                        effectiveFromUtc,
                        excludingLineId: existingLine.Id,
                        cancellationToken))
                {
                    return Result<IReadOnlyCollection<SalaryTabulatorLineAuditPayload>>.Failure(SalaryTabulatorErrors.EffectiveDateOverlap);
                }

                var beforeExisting = ToLineAuditPayload(existingLine);
                existingLine.EndRange(effectiveFromUtc.AddDays(-1));
                auditEntries.Add(new SalaryTabulatorLineAuditPayload(
                    AuditEventTypes.SalaryTabulatorLineInactivated,
                    existingLine.PublicId,
                    $"{existingLine.SalaryClassCode}:{existingLine.SalaryScaleCode}",
                    $"Closed previous salary tabulator line {existingLine.SalaryClassCode}/{existingLine.SalaryScaleCode}.",
                    Before: beforeExisting,
                    After: ToLineAuditPayload(existingLine)));

                var newLine = SalaryTabulatorLine.Create(
                    item.SalaryClassCode,
                    item.SalaryScaleCode,
                    item.CurrencyCode,
                    item.ProposedBaseAmount!.Value,
                    item.ProposedMinAmount,
                    item.ProposedMaxAmount,
                    effectiveFromUtc,
                    effectiveToUtc,
                    item.Notes);
                newLine.SetTenantId(tenantId);
                repository.AddLine(newLine);
                auditEntries.Add(new SalaryTabulatorLineAuditPayload(
                    AuditEventTypes.SalaryTabulatorLineApplied,
                    newLine.PublicId,
                    $"{newLine.SalaryClassCode}:{newLine.SalaryScaleCode}",
                    $"Applied new salary tabulator line {newLine.SalaryClassCode}/{newLine.SalaryScaleCode}.",
                    Before: null,
                    After: ToLineAuditPayload(newLine)));

                return Result<IReadOnlyCollection<SalaryTabulatorLineAuditPayload>>.Success(auditEntries);
            }
            case SalaryTabulatorChangeType.Inactivate:
            {
                if (existingLine is null)
                {
                    return Result<IReadOnlyCollection<SalaryTabulatorLineAuditPayload>>.Failure(SalaryTabulatorErrors.LineNotFound);
                }

                var before = ToLineAuditPayload(existingLine);
                existingLine.Inactivate(effectiveFromUtc);
                auditEntries.Add(new SalaryTabulatorLineAuditPayload(
                    AuditEventTypes.SalaryTabulatorLineInactivated,
                    existingLine.PublicId,
                    $"{existingLine.SalaryClassCode}:{existingLine.SalaryScaleCode}",
                    $"Inactivated salary tabulator line {existingLine.SalaryClassCode}/{existingLine.SalaryScaleCode}.",
                    Before: before,
                    After: ToLineAuditPayload(existingLine)));

                return Result<IReadOnlyCollection<SalaryTabulatorLineAuditPayload>>.Success(auditEntries);
            }
            default:
                return Result<IReadOnlyCollection<SalaryTabulatorLineAuditPayload>>.Failure(SalaryTabulatorErrors.ChangeRequestStateConflict);
        }
    }

    public static Error MapDomainValidation(InvalidOperationException exception)
    {
        if (exception.Message.Contains("amount", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("BaseAmount", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("MinAmount", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("MaxAmount", StringComparison.OrdinalIgnoreCase))
        {
            return SalaryTabulatorErrors.AmountRuleViolation;
        }

        if (exception.Message.Contains("date", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("Effective", StringComparison.OrdinalIgnoreCase))
        {
            return SalaryTabulatorErrors.EffectiveDatesInvalid;
        }

        if (exception.Message.Contains("approve", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("Requester", StringComparison.OrdinalIgnoreCase))
        {
            return SalaryTabulatorErrors.ApprovalPolicyViolation;
        }

        if (exception.Message.Contains("draft", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("submitted", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("state", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("Finalized", StringComparison.OrdinalIgnoreCase))
        {
            return SalaryTabulatorErrors.ChangeRequestStateConflict;
        }

        return SalaryTabulatorErrors.ChangeRequestStateConflict;
    }

    public static string GenerateRequestNumber(DateTime utcNow) =>
        $"STR-{utcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..32].ToUpperInvariant();

    private static object ToLineAuditPayload(SalaryTabulatorLine line) => new
    {
        line.PublicId,
        line.SalaryClassCode,
        line.SalaryScaleCode,
        line.CurrencyCode,
        line.BaseAmount,
        line.MinAmount,
        line.MaxAmount,
        line.EffectiveFromUtc,
        line.EffectiveToUtc,
        line.IsActive,
        line.Version,
        line.Notes
    };
}
