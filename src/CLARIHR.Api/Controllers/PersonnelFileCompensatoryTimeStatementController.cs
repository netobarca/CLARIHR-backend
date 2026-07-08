using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Estado de cuenta of the compensatory-time fund (REQ-002 PR-4, §3.9): the credit + absence movements with a
/// running balance and the fund totals. Runs under the authn-only compensatory-time policy set; the
/// permission-or-self read decision is enforced by the handler gate (`ViewCompensatoryTime` OR the owner
/// employee). The running balance carries the accumulated offset per page (R-T9); with no filters the balance
/// equals the profile's `compensatoryTimeHoursAvailable` by construction.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewCompensatoryTime, PersonnelFilePolicies.ManageCompensatoryTime)]
public sealed class PersonnelFileCompensatoryTimeStatementController(
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/compensatory-time-statement")]
    [Produces("application/json")]
    [ProducesResponseType<CompensatoryTimeStatementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file's compensatory-time estado de cuenta",
        Description = """
            Returns the paginated fund movements (credits and absences) with a running balance plus the fund
            totals (`totalCredited` / `totalDebited` / `availableBalance`). Filterable by date range (`fromDate` /
            `toDate` over the movement date), `compensatoryTimeTypePublicId` and `statusCode`; `includeAnnulled`
            (default false) adds the ANULADA movements (which never count toward the balance). Access requires the
            `ViewCompensatoryTime` permission or the employee reading their own fund.
            """)]
    public async Task<ActionResult<CompensatoryTimeStatementResponse>> GetStatement(
        Guid publicId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] Guid? compensatoryTimeTypePublicId,
        [FromQuery] string? statusCode,
        [FromQuery] bool includeAnnulled = false,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, PersonnelFileValidationRules.MaxPageSize)] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCompensatoryTimeStatementQuery(
                publicId, fromDate, toDate, compensatoryTimeTypePublicId, statusCode, includeAnnulled, page, pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }
}
