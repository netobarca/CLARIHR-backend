using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit;
using CLARIHR.Application.Features.IdentityAccess.Common;
using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Route("api/audit/logs")]
public sealed class AuditController(
    IQueryDispatcher queryDispatcher,
    ILogger<AuditController> logger) : ControllerBase
{
    [AuthorizeResource("AUDIT_LOGS", RbacPermissionAction.Read)]
    [HttpGet]
    [ProducesResponseType<PagedResponse<AuditLogSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<AuditLogSummaryResponse>>> List(
        [FromQuery] GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = NormalizeListQuery(query, Request.Query);
        if (!Equals(query, normalizedQuery))
        {
            logger.LogDebug(
                "Audit log list query normalized from raw query string. ActorUserId {ActorUserId}, EntityId {EntityId}, EntityType {EntityType}.",
                normalizedQuery.ActorUserId,
                normalizedQuery.EntityId,
                normalizedQuery.EntityType);
        }

        var result = await queryDispatcher.SendAsync(normalizedQuery, cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("AUDIT_LOGS", RbacPermissionAction.Read)]
    [HttpGet("{auditLogId:guid}")]
    [ProducesResponseType<AuditLogDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuditLogDetailResponse>> GetById(Guid auditLogId, CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetAuditLogDetailQuery(auditLogId), cancellationToken);
        return this.ToActionResult(result);
    }

    internal static GetAuditLogsQuery NormalizeListQuery(GetAuditLogsQuery query, IQueryCollection requestQuery)
    {
        var actorUserId = ResolveGuidFilter(query.ActorUserId, requestQuery, "ActorUserId", "ActorUserPublicId");
        var entityId = ResolveGuidFilter(query.EntityId, requestQuery, "EntityId", "EntityPublicId");

        return actorUserId == query.ActorUserId && entityId == query.EntityId
            ? query
            : query with
            {
                ActorUserId = actorUserId,
                EntityId = entityId
            };
    }

    private static Guid? ResolveGuidFilter(
        Guid? currentValue,
        IQueryCollection requestQuery,
        params string[] acceptedKeys)
    {
        if (currentValue.HasValue)
        {
            return currentValue;
        }

        foreach (var key in acceptedKeys)
        {
            if (!requestQuery.TryGetValue(key, out StringValues values))
            {
                continue;
            }

            foreach (var value in values)
            {
                if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
                {
                    return parsed;
                }
            }
        }

        return null;
    }
}
