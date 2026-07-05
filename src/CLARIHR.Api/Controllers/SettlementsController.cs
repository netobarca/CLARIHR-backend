using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Settlements ("liquidación de personal") of one personnel file — per-plaza records in two modes
// (LIQUIDACION / ESCENARIO, D-02/D-10). Class-level policy set (View / Manage supersets); the precise
// HR-only gates live in the handlers (D-20). No [ResourceActions] — same cut as the retirement module.
[ApiController]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewSettlements, PersonnelFilePolicies.ManageSettlements)]
public sealed class SettlementsController(
    IQueryDispatcher queryDispatcher,
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/settlements")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileSettlementResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's settlements and scenarios",
        Description = """
            Returns every active settlement (kind `Liquidacion`) and scenario (kind `Escenario`) of the
            personnel file, lines included, newest first. Reading settlements exposes salary data, so the
            precise gate is the dedicated `PersonnelFiles.ViewSettlements` permission (or Admin).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileSettlementResponse>>> GetSettlements(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetSettlementsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/settlements/{settlementPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileSettlementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get one settlement (five-section detail)",
        Description = """
            Returns the settlement with its parameters snapshot, derived bases, detail lines (group them
            client-side by `conceptClass`: `Ingreso`, `Descuento`, `PagoPatronal`), the five-section totals
            (incomes, deductions, net pay, employer charges and the reserve/provision charged to the plaza's
            cost center) and the non-blocking `warnings`.
            """)]
    public async Task<ActionResult<PersonnelFileSettlementResponse?>> GetSettlement(
        Guid publicId,
        Guid settlementPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetSettlementQuery(publicId, settlementPublicId), cancellationToken);
        return this.ToActionResultWithETag(result, value => value?.ConcurrencyToken ?? Guid.Empty);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/settlements/scenarios")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileSettlementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a settlement scenario (simulation)",
        Description = """
            Creates a side-effect-free settlement SCENARIO over an ACTIVE plaza of an active employee
            (D-05): estimated retirement date, hypothetical category/reason (retirement catalogs) and the
            legal parameters (the minimum wage is read from the employee's employment information; supply
            `minimumMonthlyWage` only when the record has none). The engine suggests and computes the five
            sections immediately. The requester can only be HR (D-06): omit `requesterFilePublicId` to
            default to the registering manager. Nothing is written to the employee, journal or payroll.
            """)]
    public async Task<ActionResult<PersonnelFileSettlementResponse>> AddScenario(
        Guid publicId,
        [FromBody] AddSettlementScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddSettlementScenarioCommand(
                publicId,
                new SettlementScenarioInput(
                    request.AssignedPositionPublicId,
                    request.EstimatedRetirementDate,
                    request.RetirementCategoryCode,
                    request.RetirementReasonCode,
                    request.RequestDate,
                    request.RequesterFilePublicId,
                    request.Notes,
                    request.MinimumMonthlyWage)),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/settlements/{settlementPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileSettlementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a settlement's header and parameters",
        Description = """
            Edits the requester (HR only), request date, notes and the legal-parameters snapshot, then
            recalculates every non-overridden line server-side. On a SCENARIO the estimated date and the
            hypothetical category/reason can also change; a real settlement inherits them read-only from the
            executed retirement. Only a BORRADOR settlement (or a scenario) is editable. Requires `If-Match`.
            """)]
    public async Task<ActionResult<PersonnelFileSettlementResponse>> UpdateSettlement(
        Guid publicId,
        Guid settlementPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateSettlementRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateSettlementCommand(
                publicId,
                settlementPublicId,
                concurrencyToken,
                request.RequesterFilePublicId,
                request.RequestDate,
                request.Notes,
                request.EstimatedRetirementDate,
                request.RetirementCategoryCode,
                request.RetirementReasonCode,
                request.Parameters.ToModel()),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/settlements/{settlementPublicId:guid}/lines/{linePublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileSettlementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Adjust one settlement line",
        Description = """
            Applies a line adjustment and recalculates: include/exclude the line ("eliminar la información
            que no aplica" while keeping it re-includable), fix the days/factor input (`unitsOrDays`) or
            release it (`clearUnitsOverride`), set an audited manual override (`overrideAmount` +
            mandatory `overrideReason` — the computed amount stays visible, D-14) or clear it, and edit a
            manual line's description/amount. `If-Match` carries the SETTLEMENT's token (parent-token
            convention for child mutations).
            """)]
    public async Task<ActionResult<PersonnelFileSettlementResponse>> UpdateLine(
        Guid publicId,
        Guid settlementPublicId,
        Guid linePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateSettlementLineRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateSettlementLineCommand(
                publicId,
                settlementPublicId,
                linePublicId,
                concurrencyToken,
                request.IsIncluded,
                request.UnitsOrDays,
                request.ClearUnitsOverride,
                request.OverrideAmount,
                request.OverrideReason,
                request.ClearOverride,
                request.Description,
                request.ManualAmount),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/settlements/{settlementPublicId:guid}/lines")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileSettlementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Append a manual line",
        Description = """
            Adds a manual-concept line (e.g. `OTRO_INGRESO`, `OTRO_DESCUENTO`, `HORAS_EXTRAS_PENDIENTES`)
            with its description and amount, then recalculates. The concept must be an active manual concept
            of the `settlement-concepts` catalog. `If-Match` carries the settlement's token.
            """)]
    public async Task<ActionResult<PersonnelFileSettlementResponse>> AddManualLine(
        Guid publicId,
        Guid settlementPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AddSettlementManualLineRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddSettlementManualLineCommand(
                publicId, settlementPublicId, concurrencyToken, request.ConceptCode, request.Description, request.Amount),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/settlements/{settlementPublicId:guid}/lines/{linePublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileSettlementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Remove one settlement line",
        Description = """
            Removes the line ("eliminar la información que no aplica") and recalculates; an engine line can
            be re-created later via the regenerate action. `If-Match` carries the settlement's token.
            """)]
    public async Task<ActionResult<PersonnelFileSettlementResponse>> RemoveLine(
        Guid publicId,
        Guid settlementPublicId,
        Guid linePublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RemoveSettlementLineCommand(publicId, settlementPublicId, linePublicId, concurrencyToken),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/settlements/{settlementPublicId:guid}/lines/regenerate")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileSettlementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Regenerate the suggested lines",
        Description = """
            Explicitly re-reads the plaza's configuration (salary, pending bonus/commission, external
            installments, rates and brackets) and rebuilds the suggested lines from scratch — the conscious
            path to refresh a stale snapshot (inputs are otherwise frozen at creation). Manual lines,
            exclusions and overrides are DISCARDED by design. `If-Match` carries the settlement's token.
            """)]
    public async Task<ActionResult<PersonnelFileSettlementResponse>> RegenerateLines(
        Guid publicId,
        Guid settlementPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RegenerateSettlementLinesCommand(publicId, settlementPublicId, concurrencyToken),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/settlements/{settlementPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Delete a settlement scenario",
        Description = """
            Soft-deletes a SCENARIO (it disappears from the listings; nothing else changes — it never had
            effects). A real settlement is never deleted: it is annulled through its lifecycle. `If-Match`
            carries the scenario's token.
            """)]
    public async Task<ActionResult> DeleteScenario(
        Guid publicId,
        Guid settlementPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeleteSettlementScenarioCommand(publicId, settlementPublicId, concurrencyToken),
            cancellationToken);
        return result.IsFailure
            ? this.ToActionResult(result).Result!
            : NoContent();
    }
}
