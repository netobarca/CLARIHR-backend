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
/// Employee lactation periods ("periodos de lactancia") — part of the incapacities module. Kept in a dedicated
/// controller so it runs under its own authn-only policy set (<see cref="PersonnelFilePolicies.ViewIncapacities"/>
/// / <see cref="PersonnelFilePolicies.ManageIncapacities"/>); the writes are HR-only (D-18: lactation is
/// HR-registered, there is no self-service), while the reads are ViewIncapacities OR the owner employee — the
/// precise permission-or-self decision is enforced by the lactation handler gates.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewIncapacities, PersonnelFilePolicies.ManageIncapacities)]
public sealed class PersonnelFileLactationController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/lactation-periods")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileLactationPeriodResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's lactation periods",
        Description = """
            Returns every lactation period recorded for the specified personnel file with its ordered daily-permit
            schedules. Access requires the `ViewIncapacities` permission or the employee reading their own
            lactation periods.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileLactationPeriodResponse>>> GetLactationPeriods(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileLactationPeriodsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/lactation-periods/{lactationPeriodPublicId:guid}", Name = "GetLactationPeriodById")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileLactationPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a lactation period by id")]
    public async Task<ActionResult<PersonnelFileLactationPeriodResponse>> GetLactationPeriodById(
        Guid publicId,
        Guid lactationPeriodPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileLactationPeriodByIdQuery(publicId, lactationPeriodPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/lactation-periods")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileLactationPeriodResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register a lactation period",
        Description = """
            Registers a lactation period tied to the active LACTANCIA incapacity type together with its
            daily-permit schedules (each contained in the period and non-overlapping) and journals the LACTANCIA
            personnel action. HR-only (`ManageIncapacities`, D-18). A schedule outside the period range → 422
            `LACTATION_SCHEDULE_OUT_OF_RANGE`; overlapping schedules → 422 `LACTATION_SCHEDULE_OVERLAP`.
            """)]
    public async Task<ActionResult<PersonnelFileLactationPeriodResponse>> AddLactationPeriod(
        Guid publicId,
        [FromBody] LactationPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileLactationPeriodCommand(publicId, request.ToInput()), cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetLactationPeriodById),
            value => new { publicId, lactationPeriodPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/lactation-periods/{lactationPeriodPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileLactationPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Edit a lactation period and replace its schedules",
        Description = """
            Updates the lactation period dates and notes and replaces the full daily-permit schedule set (each
            schedule must be contained in the new range and must not overlap). Requires the `If-Match` header
            with the current `concurrencyToken`; the new token is returned in the `ETag` header. HR-only.
            """)]
    public async Task<ActionResult<PersonnelFileLactationPeriodResponse>> UpdateLactationPeriod(
        Guid publicId,
        Guid lactationPeriodPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] LactationPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileLactationPeriodCommand(publicId, lactationPeriodPublicId, request.ToInput(), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/lactation-periods/{lactationPeriodPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileLactationPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul a lactation period",
        Description = """
            Annuls a REGISTRADA lactation period (terminal); the reason is mandatory. Requires the `If-Match`
            header with the current `concurrencyToken`. HR-only.
            """)]
    public async Task<ActionResult<PersonnelFileLactationPeriodResponse>> AnnulLactationPeriod(
        Guid publicId,
        Guid lactationPeriodPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulLactationPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulPersonnelFileLactationPeriodCommand(publicId, lactationPeriodPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}
