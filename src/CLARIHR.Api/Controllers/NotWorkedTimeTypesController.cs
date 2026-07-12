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
/// The company's not-worked-time TYPE master (REQ-011 D-18): the catalogue of absences, suspensions-with-discount
/// and late arrivals, each with its counting flags and its discount percent.
///
/// <para><b>There is no DELETE</b> (molde CostCenter): the removal is logical (<c>PATCH …/inactivate</c>), because a
/// type already stamped on a record must stay readable.</para>
///
/// <para>Deliberately NOT annotated with [AuthorizationPolicySet]: read and write carry different dedicated
/// permissions and both gate per handler (<c>ViewNotWorkedTimes</c> / <c>ManageNotWorkedTimeTypes</c>).</para>
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class NotWorkedTimeTypesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/not-worked-time-types")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<NotWorkedTimeTypeResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List the company's not-worked-time types",
        Description = "`isActive` narrows the list. A type with `discountPercent: 0` is a paid absence: it is recorded, and the money is not touched.")]
    public async Task<ActionResult<IReadOnlyCollection<NotWorkedTimeTypeResponse>>> Get(
        Guid companyId,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetNotWorkedTimeTypesQuery(companyId, isActive), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/not-worked-time-types")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<NotWorkedTimeTypeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a not-worked-time type",
        Description = """
            `countsHoliday` / `countsSaturday` / `countsRestDay` drive the day scan (a day that does not count is
            not discounted). **`countsSeventhDayPenalty`** adds ONE extra full day per affected week — the paid day
            of rest the employee forfeits.

            A `discountPercent` greater than 0 REQUIRES `deductionConceptTypeCode`
            (`422 NOT_WORKED_TIME_TYPE_DEDUCTION_CONCEPT_REQUIRED`): a discount with nowhere to land would never
            reach the payroll input.
            """)]
    public async Task<ActionResult<NotWorkedTimeTypeResponse>> Create(
        Guid companyId,
        [FromBody] NotWorkedTimeTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddNotWorkedTimeTypeCommand(companyId, ToInput(request)),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById token `{id}` to `{publicId}`, so the Location route
        // value MUST be keyed `publicId` or link generation fails (molde CostCentersController).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { companyId, publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/not-worked-time-types/{id:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<NotWorkedTimeTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get one not-worked-time type")]
    public async Task<ActionResult<NotWorkedTimeTypeResponse>> GetById(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetNotWorkedTimeTypeByIdQuery(companyId, id), cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/companies/{companyId:guid}/not-worked-time-types/{notWorkedTimeTypePublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<NotWorkedTimeTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(Summary = "Update a not-worked-time type")]
    public async Task<ActionResult<NotWorkedTimeTypeResponse>> Update(
        Guid companyId,
        Guid notWorkedTimeTypePublicId,
        [FromBody] NotWorkedTimeTypeRequest request,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateNotWorkedTimeTypeCommand(companyId, notWorkedTimeTypePublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/companies/{companyId:guid}/not-worked-time-types/{notWorkedTimeTypePublicId:guid}/activation")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<NotWorkedTimeTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate or inactivate a not-worked-time type",
        Description = "The master has **no DELETE**: a type already stamped on a record must stay readable, so the removal is logical.")]
    public async Task<ActionResult<NotWorkedTimeTypeResponse>> SetActivation(
        Guid companyId,
        Guid notWorkedTimeTypePublicId,
        [FromBody] SetNotWorkedTimeTypeActivationRequest request,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new SetNotWorkedTimeTypeActivationCommand(companyId, notWorkedTimeTypePublicId, request.IsActive, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/not-worked-time-configuration/load-template")]
    [Produces("application/json")]
    [ProducesResponseType<NotWorkedTimeTemplateResultResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Load the not-worked-time type template into the company",
        Description = """
            Creates the four F1 types (`AUSENCIA_SIN_GOCE`, `AUSENCIA_CON_GOCE`, `SUSPENSION_CON_DESCUENTO`,
            `LLEGADA_TARDIA`). **Idempotent**: a code the company already has is SKIPPED, never overwritten — even
            if it was edited or inactivated. Safe to call repeatedly; the response says how many were created and
            how many were skipped.
            """)]
    public async Task<ActionResult<NotWorkedTimeTemplateResultResponse>> LoadTemplate(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new LoadNotWorkedTimeTemplateCommand(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    private static NotWorkedTimeTypeInput ToInput(NotWorkedTimeTypeRequest request) =>
        new(
            request.Code,
            request.Name,
            request.AppliesToPermission,
            request.UsesWorkSchedule,
            request.CountsHoliday,
            request.CountsSaturday,
            request.CountsRestDay,
            request.CountsSeventhDayPenalty,
            request.DiscountPercent,
            request.DeductionConceptTypeCode,
            request.IncomeConceptTypeCode);
}

public sealed record NotWorkedTimeTypeRequest(
    string Code,
    string Name,
    bool AppliesToPermission,
    bool UsesWorkSchedule,
    bool CountsHoliday,
    bool CountsSaturday,
    bool CountsRestDay,
    bool CountsSeventhDayPenalty,
    decimal DiscountPercent,
    string? DeductionConceptTypeCode,
    string? IncomeConceptTypeCode);

public sealed record SetNotWorkedTimeTypeActivationRequest(bool IsActive);
