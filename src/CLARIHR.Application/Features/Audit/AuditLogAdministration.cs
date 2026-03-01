using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.Audit;

public sealed record AuditLogSummaryResponse(
    Guid Id,
    DateTime CreatedAtUtc,
    Guid ActorUserId,
    string? ActorEmail,
    string EventType,
    string EntityType,
    Guid? EntityId,
    string? EntityKey,
    string Action,
    string Summary,
    JsonElement? Diff);

public sealed record AuditLogDetailResponse(
    Guid Id,
    Guid CompanyId,
    DateTime CreatedAtUtc,
    Guid ActorUserId,
    string? ActorEmail,
    string EventType,
    string EntityType,
    Guid? EntityId,
    string? EntityKey,
    string Action,
    string Summary,
    JsonElement? Before,
    JsonElement? After,
    JsonElement? Diff,
    string? IpAddress,
    string? UserAgent);

public sealed record GetAuditLogsQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Guid? ActorUserId = null,
    string? EntityType = null,
    string? EventType = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResponse<AuditLogSummaryResponse>>;

public sealed record GetAuditLogDetailQuery(Guid AuditLogId) : IQuery<AuditLogDetailResponse>;

internal sealed class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.ActorUserId)
            .NotEqual(Guid.Empty)
            .When(static query => query.ActorUserId.HasValue);

        RuleFor(query => query.EntityType)
            .MaximumLength(50)
            .Must(static entityType => string.IsNullOrWhiteSpace(entityType) || AuditEntityTypes.TryNormalize(entityType, out _))
            .WithMessage("Unknown entity type.");

        RuleFor(query => query.EventType)
            .MaximumLength(100)
            .Must(static eventType => string.IsNullOrWhiteSpace(eventType) || AuditEventTypes.TryNormalize(eventType, out _))
            .WithMessage("Unknown event type.");

        RuleFor(query => query.Search)
            .MaximumLength(200)
            .When(static query => !string.IsNullOrWhiteSpace(query.Search));

        RuleFor(query => query)
            .Must(static query => !query.FromUtc.HasValue || !query.ToUtc.HasValue || query.FromUtc.Value <= query.ToUtc.Value)
            .WithMessage("The 'from' date must be less than or equal to 'to'.");
    }
}

internal sealed class GetAuditLogDetailQueryValidator : AbstractValidator<GetAuditLogDetailQuery>
{
    public GetAuditLogDetailQueryValidator()
    {
        RuleFor(query => query.AuditLogId)
            .NotEmpty();
    }
}

internal sealed class GetAuditLogsQueryHandler(
    IRbacAuthorizationService authorizationService,
    IAuditLogRepository repository)
    : IQueryHandler<GetAuditLogsQuery, PagedResponse<AuditLogSummaryResponse>>
{
    public async Task<Result<PagedResponse<AuditLogSummaryResponse>>> Handle(
        GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(
            "AUDIT_LOGS",
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<AuditLogSummaryResponse>>.Failure(authorizationResult.Error);
        }

        var logs = await repository.GetLogsAsync(
            query.FromUtc,
            query.ToUtc,
            query.ActorUserId,
            NormalizeEntityType(query.EntityType),
            NormalizeEventType(query.EventType),
            query.Search,
            query.Page,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<AuditLogSummaryResponse>>.Success(
            new PagedResponse<AuditLogSummaryResponse>(
                logs.Items.Select(AuditLogMapping.MapSummary).ToArray(),
                logs.PageNumber,
                logs.PageSize,
                logs.TotalCount));
    }

    private static string? NormalizeEntityType(string? entityType) =>
        AuditEntityTypes.TryNormalize(entityType, out var normalized) ? normalized : null;

    private static string? NormalizeEventType(string? eventType) =>
        AuditEventTypes.TryNormalize(eventType, out var normalized) ? normalized : null;
}

internal sealed class GetAuditLogDetailQueryHandler(
    IRbacAuthorizationService authorizationService,
    IAuditLogRepository repository)
    : IQueryHandler<GetAuditLogDetailQuery, AuditLogDetailResponse>
{
    public async Task<Result<AuditLogDetailResponse>> Handle(
        GetAuditLogDetailQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(
            "AUDIT_LOGS",
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<AuditLogDetailResponse>.Failure(authorizationResult.Error);
        }

        var auditLog = await repository.GetByPublicIdAsync(query.AuditLogId, cancellationToken);
        if (auditLog is null)
        {
            return Result<AuditLogDetailResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(query.AuditLogId, cancellationToken)
                    ? AuthorizationErrors.TenantMismatch("AUDIT_LOGS", RbacPermissionAction.Read)
                    : AuditErrors.LogNotFound);
        }

        return Result<AuditLogDetailResponse>.Success(
            new AuditLogDetailResponse(
                auditLog.PublicId,
                auditLog.TenantId,
                auditLog.CreatedUtc,
                auditLog.ActorUserId,
                auditLog.ActorEmail,
                auditLog.EventType,
                auditLog.EntityType,
                auditLog.EntityId,
                auditLog.EntityKey,
                auditLog.Action,
                auditLog.Summary,
                AuditLogMapping.ParseJson(auditLog.BeforeJson),
                AuditLogMapping.ParseJson(auditLog.AfterJson),
                AuditLogMapping.ParseJson(auditLog.DiffJson),
                auditLog.IpAddress,
                auditLog.UserAgent));
    }
}

internal static class AuditLogMapping
{
    public static AuditLogSummaryResponse MapSummary(CLARIHR.Domain.Auditing.AuditLog auditLog) =>
        new(
            auditLog.PublicId,
            auditLog.CreatedUtc,
            auditLog.ActorUserId,
            auditLog.ActorEmail,
            auditLog.EventType,
            auditLog.EntityType,
            auditLog.EntityId,
            auditLog.EntityKey,
            auditLog.Action,
            auditLog.Summary,
            ParseJson(auditLog.DiffJson));

    public static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
