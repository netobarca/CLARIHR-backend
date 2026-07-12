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
/// One-time deductions of a personnel file ("planilla — descuentos eventuales", REQ-009): a compensation concept
/// the company charges the employee a single time. This controller carries the CRUD, the annulment of a draft and
/// the RE-IMPUTATION of the payroll destination; the authorizer resolution/revocation lives in
/// <see cref="OneTimeDeductionResolutionController"/> because those writes must map to the dedicated
/// <c>AuthorizeOneTimeDeductions</c> grant. HR-only.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewOneTimeDeductions, PersonnelFilePolicies.ManageOneTimeDeductions)]
public sealed class OneTimeDeductionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/one-time-deductions")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<OneTimeDeductionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's one-time deductions",
        Description = """
            Returns the one-off deductions of the specified personnel file, with their value (fixed or the
            components it was DERIVED from), their requester and their payroll destination. Requires the
            `ViewOneTimeDeductions` permission. Each item carries its own `concurrencyToken`.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<OneTimeDeductionResponse>>> GetOneTimeDeductions(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileOneTimeDeductionsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/one-time-deductions/{oneTimeDeductionPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file one-time deduction by id",
        Description = "Returns a single one-time deduction. The `concurrencyToken` is required in the `If-Match` header of subsequent writes.")]
    public async Task<ActionResult<OneTimeDeductionResponse>> GetOneTimeDeductionById(
        Guid publicId,
        Guid oneTimeDeductionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileOneTimeDeductionByIdQuery(publicId, oneTimeDeductionPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/one-time-deductions")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register a one-time deduction",
        Description = """
            Creates a one-off deduction in status `EN_REVISION` and returns it with `201 Created`. HR-only
            (`ManageOneTimeDeductions`). The compensation concept must be an ACTIVE, NON-STATUTORY `Egreso`
            concept (ISSS/AFP/Renta → `422`); the payroll type is validated against the country catalog; the plaza
            is optional (the principal one is used when omitted — a deduction carries NO cost center).
            <br/><br/>
            <b>The amount belongs to the server.</b> A fixed value sends `amount` and no components. A computed
            value sends its `calculationMethod` and components, and the server DERIVES the amount: `amount` may be
            omitted, and if it IS sent but does not follow from the components the request is rejected with `422`
            `ONE_TIME_DEDUCTION_AMOUNT_MISMATCH` carrying the expected figure.
            """)]
    public async Task<ActionResult<OneTimeDeductionResponse>> AddOneTimeDeduction(
        Guid publicId,
        [FromBody] AddOneTimeDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileOneTimeDeductionCommand(publicId, ToInput(request)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetOneTimeDeductionById),
            value => new { publicId, oneTimeDeductionPublicId = value.OneTimeDeductionPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/one-time-deductions/{oneTimeDeductionPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a one-time deduction's business fields",
        Description = """
            Replaces the business fields of an `EN_REVISION` deduction; an authorized one is no longer editable
            (its payroll destination can still be re-targeted, see the `period` endpoint). HR-only. Requires the
            `If-Match` header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<OneTimeDeductionResponse>> UpdateOneTimeDeduction(
        Guid publicId,
        Guid oneTimeDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateOneTimeDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileOneTimeDeductionCommand(publicId, oneTimeDeductionPublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/one-time-deductions/{oneTimeDeductionPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Discard (soft-delete) an EN_REVISION one-time-deduction draft",
        Description = """
            Soft-deletes an `EN_REVISION` draft. Only a draft can be discarded — an authorized deduction is
            revoked, never deleted. HR-only. Requires the `If-Match` header.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteOneTimeDeduction(
        Guid publicId,
        Guid oneTimeDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileOneTimeDeductionCommand(publicId, oneTimeDeductionPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/one-time-deductions/{oneTimeDeductionPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul an EN_REVISION one-time deduction",
        Description = """
            Annuls an `EN_REVISION` deduction (→ `ANULADO`, terminal); the `reason` is mandatory. Revoking an
            AUTHORIZED deduction is the authorizer's action (see the resolution controller). Requires `If-Match`.
            """)]
    public async Task<ActionResult<OneTimeDeductionResponse>> AnnulOneTimeDeduction(
        Guid publicId,
        Guid oneTimeDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulOneTimeDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulPersonnelFileOneTimeDeductionCommand(publicId, oneTimeDeductionPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/one-time-deductions/{oneTimeDeductionPublicId:guid}/period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Re-target an AUTORIZADO deduction to another payroll period",
        Description = """
            Moves the deduction to another payroll type / period ("enviar a otro periodo"). Only legal while
            `AUTORIZADO` — once APPLIED it is already in a payroll and must be reverted first (`422`
            `ONE_TIME_DEDUCTION_NOT_RETARGETABLE`). HR-only. Requires `If-Match`.
            """)]
    public async Task<ActionResult<OneTimeDeductionResponse>> RetargetOneTimeDeductionPeriod(
        Guid publicId,
        Guid oneTimeDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RetargetOneTimeDeductionPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RetargetPersonnelFileOneTimeDeductionPeriodCommand(
                publicId,
                oneTimeDeductionPublicId,
                new OneTimeDeductionPeriodInput(
                    request.PayrollTypeCode,
                    request.PayrollPeriodPublicId,
                    request.PayrollPeriodLabel,
                    request.PayrollPeriodEndDate),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    private static OneTimeDeductionInput ToInput(AddOneTimeDeductionRequest request) =>
        new(
            request.DeductionDate,
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

    private static OneTimeDeductionInput ToInput(UpdateOneTimeDeductionRequest request) =>
        new(
            request.DeductionDate,
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
