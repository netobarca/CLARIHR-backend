using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit;
using CLARIHR.Application.Features.IdentityAccess.Common;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Route("api/audit/logs")]
public sealed class AuditController(IQueryDispatcher queryDispatcher) : ControllerBase
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
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
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
}
