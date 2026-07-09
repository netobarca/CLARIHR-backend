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
/// One-time incomes of a personnel file ("planilla — ingresos eventuales", REQ-006): a compensation concept paid a
/// single time (fixed amount or computed by factors). This controller carries the CRUD + the HR (Manage) lifecycle
/// (annulment of an EN_REVISION draft, re-imputation of an AUTORIZADO income to another payroll period); the
/// authorizer resolution/revocation lives in <see cref="OneTimeIncomeResolutionController"/> because those writes
/// must map to the dedicated <c>AuthorizeOneTimeIncomes</c> grant. HR-only, no self-service in Fase 1 (P-10).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewOneTimeIncomes, PersonnelFilePolicies.ManageOneTimeIncomes)]
public sealed class OneTimeIncomesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/one-time-incomes")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<OneTimeIncomeResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's one-time incomes",
        Description = """
            Returns the one-time incomes of the specified personnel file. Requires the `ViewOneTimeIncomes`
            permission (HR-only, no self-service — P-10). Each item carries its own `concurrencyToken` for the
            `If-Match` header of subsequent writes.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<OneTimeIncomeResponse>>> GetOneTimeIncomes(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileOneTimeIncomesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/one-time-incomes/{oneTimeIncomePublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file one-time income by id",
        Description = "Returns a single one-time income. The `concurrencyToken` is required in the `If-Match` header of subsequent writes.")]
    public async Task<ActionResult<OneTimeIncomeResponse>> GetOneTimeIncomeById(
        Guid publicId,
        Guid oneTimeIncomePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileOneTimeIncomeByIdQuery(publicId, oneTimeIncomePublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/one-time-incomes")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register a one-time income",
        Description = """
            Creates a one-time income in status `EN_REVISION` and returns it with `201 Created`. HR-only
            (`ManageOneTimeIncomes`). The value is either fixed (amount > 0, no method/components) or computed by
            factors (`CANTIDAD_POR_VALOR` = quantity × unit value × multiplier, or `PORCENTAJE_SOBRE_BASE` =
            percentage% × base); the server resolves the amount and cross-checks a supplied one (mismatch → 422).
            The compensation concept must be an active income concept (Nature = Ingreso, not base salary); the
            payroll type is validated against the catalog; the plaza is optional (the principal plaza is used when
            omitted) and its cost center is derived (a plaza without a cost center → 422); the requester file (the
            trío) is snapshotted; the currency defaults to the company preference when omitted. The `ETag` header
            carries the initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<OneTimeIncomeResponse>> AddOneTimeIncome(
        Guid publicId,
        [FromBody] AddOneTimeIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileOneTimeIncomeCommand(publicId, ToInput(request)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetOneTimeIncomeById),
            value => new { publicId, oneTimeIncomePublicId = value.OneTimeIncomePublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/one-time-incomes/{oneTimeIncomePublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a one-time income's header + value",
        Description = """
            Replaces the business fields (header + value + plaza + requester + destination) of an `EN_REVISION`
            one-time income (RN-02; an authorized income is no longer editable). HR-only. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<OneTimeIncomeResponse>> UpdateOneTimeIncome(
        Guid publicId,
        Guid oneTimeIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateOneTimeIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileOneTimeIncomeCommand(publicId, oneTimeIncomePublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/one-time-incomes/{oneTimeIncomePublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Discard (soft-delete) an EN_REVISION one-time income draft",
        Description = """
            Soft-deletes an `EN_REVISION` draft (sets it inactive; no physical removal). Only a draft can be
            discarded — an authorized income is revoked or annulled. HR-only. Requires the `If-Match` header with
            the current `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteOneTimeIncome(
        Guid publicId,
        Guid oneTimeIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileOneTimeIncomeCommand(publicId, oneTimeIncomePublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/one-time-incomes/{oneTimeIncomePublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul an EN_REVISION one-time income",
        Description = """
            Annuls (retiro) an `EN_REVISION` income (→ `ANULADO`, terminal); the `reason` is mandatory. HR-only. An
            `AUTORIZADO` income is revoked by the authorizer instead. Requires the `If-Match` header with the
            current `concurrencyToken`.
            """)]
    public async Task<ActionResult<OneTimeIncomeResponse>> AnnulOneTimeIncome(
        Guid publicId,
        Guid oneTimeIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulOneTimeIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulPersonnelFileOneTimeIncomeCommand(publicId, oneTimeIncomePublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/one-time-incomes/{oneTimeIncomePublicId:guid}/period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Re-target an AUTORIZADO one-time income to another payroll period",
        Description = """
            Re-imputes ("enviar a otro periodo", RF-005) the payroll destination (payroll type + period + label +
            end date) of an `AUTORIZADO` income WITHOUT touching its amount/calculation. HR-only. Only an
            `AUTORIZADO` income can be re-targeted. Requires the `If-Match` header with the current
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<OneTimeIncomeResponse>> RetargetOneTimeIncomePeriod(
        Guid publicId,
        Guid oneTimeIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RetargetOneTimeIncomePeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = new OneTimeIncomePeriodInput(
            request.PayrollTypeCode,
            request.PayrollPeriodPublicId,
            request.PayrollPeriodLabel,
            request.PayrollPeriodEndDate);

        var result = await commandDispatcher.SendAsync(
            new RetargetPersonnelFileOneTimeIncomePeriodCommand(publicId, oneTimeIncomePublicId, period, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    private static OneTimeIncomeInput ToInput(AddOneTimeIncomeRequest request) =>
        new(
            request.IncomeDate,
            request.Reference,
            request.ConceptTypeCode,
            request.Observations,
            request.IsFixedValue,
            request.CalculationMethod,
            request.Quantity,
            request.UnitValue,
            request.Multiplier,
            request.Percentage,
            request.BaseAmount,
            request.Amount,
            request.CurrencyCode,
            request.AssignedPositionPublicId,
            request.RequesterFilePublicId,
            request.PayrollTypeCode,
            request.PayrollPeriodPublicId,
            request.PayrollPeriodLabel,
            request.PayrollPeriodEndDate);

    private static OneTimeIncomeInput ToInput(UpdateOneTimeIncomeRequest request) =>
        new(
            request.IncomeDate,
            request.Reference,
            request.ConceptTypeCode,
            request.Observations,
            request.IsFixedValue,
            request.CalculationMethod,
            request.Quantity,
            request.UnitValue,
            request.Multiplier,
            request.Percentage,
            request.BaseAmount,
            request.Amount,
            request.CurrencyCode,
            request.AssignedPositionPublicId,
            request.RequesterFilePublicId,
            request.PayrollTypeCode,
            request.PayrollPeriodPublicId,
            request.PayrollPeriodLabel,
            request.PayrollPeriodEndDate);
}
