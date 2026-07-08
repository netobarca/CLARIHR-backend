using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Employee vacation requests (leave module PR-8, D-13/D-14). Kept in a dedicated controller under the vacation
/// authn-only policy set (<see cref="PersonnelFilePolicies.ViewVacations"/> /
/// <see cref="PersonnelFilePolicies.ManageVacations"/>); the precise permission-or-self decision (create/cancel
/// = manage OR the owner), the manager-only decision/return and the anti-self guard are enforced by the handler
/// gates, which a declarative policy cannot express. Approving consumes the fund (FIFO by default, editable);
/// returns reverse the days to their periods (LIFO by default) — both journal a personnel action.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewVacations, PersonnelFilePolicies.ManageVacations)]
public sealed class PersonnelFileVacationRequestsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/vacation-requests")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileVacationRequestResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's vacation requests",
        Description = """
            Returns the vacation requests of the employee with their fund allocations and returns. Access
            requires the `ViewVacations` permission or the employee reading their own requests (D-18).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileVacationRequestResponse>>> GetVacationRequests(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileVacationRequestsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/vacation-requests/{vacationRequestPublicId:guid}", Name = "GetVacationRequestById")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileVacationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a vacation request by id")]
    public async Task<ActionResult<PersonnelFileVacationRequestResponse>> GetVacationRequestById(
        Guid publicId,
        Guid vacationRequestPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileVacationRequestByIdQuery(publicId, vacationRequestPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/vacation-requests")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileVacationRequestResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a vacation request",
        Description = """
            Creates a vacation request (SOLICITADA). HR (`ManageVacations`) may raise it on any file; the employee
            may self-register on their own file (D-18). The date range is validated against Art. 178
            (`VACATION_START_ON_HOLIDAY_FORBIDDEN` / `VACATION_START_ON_REST_DAY_FORBIDDEN` /
            `VACATION_END_ON_HOLIDAY_FORBIDDEN`), overlaps with a live request (`VACATION_REQUEST_OVERLAP`) or an
            active incapacity (`VACATION_INCAPACITY_OVERLAP`), and the fund availability
            (`VACATION_FUND_INSUFFICIENT`).
            """)]
    public async Task<ActionResult<PersonnelFileVacationRequestResponse>> AddVacationRequest(
        Guid publicId,
        [FromBody] AddVacationRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileVacationRequestCommand(
                publicId,
                new VacationRequestInput(
                    request.StartDate,
                    request.EndDate,
                    request.RequestedDays,
                    request.PlanLinePublicId,
                    request.Notes)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetVacationRequestById),
            value => new { publicId, vacationRequestPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/vacation-requests/{vacationRequestPublicId:guid}/decision")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileVacationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Decide a vacation request (approve / reject)",
        Description = """
            HR decision on a SOLICITADA request. When `approve` is true the request is approved and consumes the
            fund via `allocations` (Σ = requested days; an empty set uses the FIFO suggestion), re-verifying the
            fund inside the transaction (race → `VACATION_FUND_INSUFFICIENT`; Σ mismatch →
            `VACATION_ALLOCATION_MISMATCH`) and journaling the GOCE_VACACIONES personnel action. When false the
            request is rejected. The deciding user must not be the subject employee (anti-self, 403). Requires the
            `If-Match` header.
            """)]
    public async Task<ActionResult<PersonnelFileVacationRequestResponse>> DecideVacationRequest(
        Guid publicId,
        Guid vacationRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] DecideVacationRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DecidePersonnelFileVacationRequestCommand(
                publicId,
                vacationRequestPublicId,
                new VacationDecisionInput(
                    request.Approve,
                    request.Allocations?.Select(item => new VacationAllocationItem(item.VacationPeriodPublicId, item.Days)).ToArray(),
                    request.Notes),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/vacation-requests/{vacationRequestPublicId:guid}/cancellation")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileVacationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Cancel a SOLICITADA vacation request",
        Description = """
            Cancels a SOLICITADA request. HR (`ManageVacations`) or the owner employee may cancel it while it is
            still pending (`VACATION_STATE_RULE_VIOLATION` otherwise). Requires the `If-Match` header.
            """)]
    public async Task<ActionResult<PersonnelFileVacationRequestResponse>> CancelVacationRequest(
        Guid publicId,
        Guid vacationRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CancelPersonnelFileVacationRequestCommand(publicId, vacationRequestPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/vacation-requests/{vacationRequestPublicId:guid}/returns")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileVacationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Return vacation days (total / partial)",
        Description = """
            Registers a total or partial return of enjoyed days of an APROBADA or DEVUELTA_PARCIAL request
            (D-14). The `distribution` reverses the days to their periods of origin (an empty set uses the LIFO
            suggestion); the return cannot exceed the days still consumed (`VACATION_RETURN_EXCEEDS_CONSUMED`).
            When the return exhausts the consumption the request moves to DEVUELTA, otherwise DEVUELTA_PARCIAL.
            Journals the DEVOLUCION_VACACIONES personnel action. The deciding user must not be the subject
            employee (anti-self, 403). Requires the `If-Match` header.
            """)]
    public async Task<ActionResult<PersonnelFileVacationRequestResponse>> AddVacationReturn(
        Guid publicId,
        Guid vacationRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AddVacationReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileVacationReturnCommand(
                publicId,
                vacationRequestPublicId,
                new VacationReturnInput(
                    request.Days,
                    request.Reason,
                    request.Distribution?.Select(item => new VacationReturnDistributionItem(item.VacationPeriodPublicId, item.Days)).ToArray()),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}
