using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles.Absences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// The employee's not-worked-time records (REQ-011). There is <b>no decision step</b> (P-16): the absence already
/// happened, so the record is born <c>REGISTRADO</c> and the only transition is the annulment. The discount is
/// <b>computed by the server</b> — the user never types an amount.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class NotWorkedTimesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/not-worked-times")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<NotWorkedTimeResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "List the employee's not-worked-time records")]
    public async Task<ActionResult<IReadOnlyCollection<NotWorkedTimeResponse>>> Get(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetNotWorkedTimesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/not-worked-times")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<NotWorkedTimeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register a not-worked time",
        Description = """
            The **amount is computed by the server**: the type's flags drive a day-by-day scan (excluding the rest
            day, Saturdays and holidays unless the type says they count), and — when the type carries the seventh-day
            penalty — **each affected week adds one extra full day**, the paid rest the employee forfeits. A
            Monday-to-Friday absence therefore discounts **six** days, not five.

            `hours` belongs to the types captured in hours (a late arrival) and ONLY to them
            (`422 NOT_WORKED_TIME_HOURS_REQUIRED` / `NOT_WORKED_TIME_HOURS_NOT_APPLICABLE`).

            `assignedPositionPublicId` is optional: omitted, the employee's primary plaza is used.
            """)]
    public async Task<ActionResult<NotWorkedTimeResponse>> Create(
        Guid publicId,
        [FromBody] AddNotWorkedTimeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddNotWorkedTimeCommand(
                publicId,
                request.TypeCode,
                request.AssignedPositionPublicId,
                request.StartDate,
                request.EndDate,
                request.Hours,
                request.Reason),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(Get),
            value => new { publicId },
            value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/not-worked-times/{notWorkedTimePublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<NotWorkedTimeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul a not-worked-time record",
        Description = "The only transition there is. The record is kept (with its reason); the journal entry is annulled too.")]
    public async Task<ActionResult<NotWorkedTimeResponse>> Annul(
        Guid publicId,
        Guid notWorkedTimePublicId,
        [FromBody] AnnulNotWorkedTimeRequest request,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulNotWorkedTimeCommand(publicId, notWorkedTimePublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}

public sealed record AddNotWorkedTimeRequest(
    string TypeCode,
    Guid? AssignedPositionPublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal? Hours,
    string? Reason);

public sealed record AnnulNotWorkedTimeRequest(string Reason);
