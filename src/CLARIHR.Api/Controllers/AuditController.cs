using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Technical/handler-gated controller: authorization is enforced per-action via [AuthorizeResource]
// ("AUDIT_LOGS", Read) and re-checked in the query handlers, so [AuthorizationPolicySet] does NOT apply
// by design (the Audit family stays out of GovernedFamilyRegex). It carries [Tags("Audit Logs")] +
// per-action [SwaggerOperation] + [ProducesStandardErrors] and is enrolled in the OpenAPI guardrail.
// The audit trail is append-only and read-only: entries are written internally via IAuditService, never
// by the client, so there is intentionally no POST/PUT/PATCH/DELETE (see audit doc 24).
[ApiController]
[Route("api/v1/audit/logs")]
[Tags("Audit Logs")]
public sealed class AuditController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [AuthorizeResource("AUDIT_LOGS", RbacPermissionAction.Read)]
    [HttpGet]
    [EnableRateLimiting(AuditRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<AuditLogSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List audit log entries",
        Description = """
            Returns a paginated, time-ordered (newest first) list of audit log entries for the
            authenticated tenant. Filterable by date range (`fromUtc`/`toUtc`), `actorUserPublicId`,
            `entityPublicId`, `entityType`, `eventType` and free-text `search` (matches actor email and
            summary, minimum 2 characters). Each item carries the sanitized `diff` of the change; the full
            `before`/`after` payloads are available via the detail endpoint. The audit trail is read-only.
            """)]
    public async Task<ActionResult<PagedResponse<AuditLogSummaryResponse>>> List(
        [FromQuery] GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [AuthorizeResource("AUDIT_LOGS", RbacPermissionAction.Read)]
    [HttpGet("{auditLogId:guid}")]
    [ProducesResponseType<AuditLogDetailResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an audit log entry by id",
        Description = """
            Returns a single audit log entry by its public id, including the sanitized `before`, `after`
            and `diff` payloads plus the actor, IP address and user agent. The entry is resolved within
            the authenticated tenant: a non-existent id yields `404`, while an id that belongs to another
            tenant yields `403 TENANT_MISMATCH`. Sensitive fields (PII/secrets) are redacted at write time.
            """)]
    public async Task<ActionResult<AuditLogDetailResponse>> GetById(Guid auditLogId, CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetAuditLogDetailQuery(auditLogId), cancellationToken);
        return this.ToActionResult(result);
    }
}
