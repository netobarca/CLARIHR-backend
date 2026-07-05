using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
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
    ICommandDispatcher commandDispatcher,
    IDocumentModelRenderer documentModelRenderer,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/settlements/{settlementPublicId:guid}/document")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Download the settlement document (boleta PDF or sectioned Excel/CSV/JSON)",
        Description = """
            Returns the individual settlement document (RF-007, D-19 — PDF ratified for Fase 1):
            `format=pdf` renders the boleta de liquidación through the shared document pipeline (standard
            layout: header, applied parameters, the three line sections, the reserve/provision and the
            summary); `format=xlsx|csv|json` downloads the same content as sectioned rows
            (ENCABEZADO / INGRESOS / DESCUENTOS / PAGOS PATRONALES / RESUMEN). A scenario document is
            always marked `SIMULACIÓN — SIN EFECTOS` (R-10).
            """)]
    public async Task<IActionResult> GetDocument(
        Guid publicId,
        Guid settlementPublicId,
        [FromQuery] string format = "pdf",
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetSettlementDocumentDataQuery(publicId, settlementPublicId), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        var data = result.Value;
        var shortId = settlementPublicId.ToString("N")[..8];
        if (string.Equals(format?.Trim(), "pdf", StringComparison.OrdinalIgnoreCase))
        {
            var document = SettlementDocumentMapper.Map(data.Settlement, data.EmployeeFullName);
            using var buffer = new MemoryStream();
            await documentModelRenderer.RenderAsync(document, buffer, cancellationToken);
            return File(buffer.ToArray(), "application/pdf", $"liquidacion-{shortId}.pdf");
        }

        var rows = SettlementDocumentRowComposer.Compose(data.Settlement, data.EmployeeFullName);
        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            rows,
            format ?? "xlsx",
            $"liquidacion-{shortId}",
            "Liquidacion",
            AuditEntityTypes.PersonnelFile,
            "SETTLEMENT_DOCUMENT",
            "Exported settlement document.",
            new { publicId, settlementPublicId },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

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

    [HttpPost("api/v1/personnel-files/{publicId:guid}/settlements")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileSettlementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a real settlement from the executed retirement",
        Description = """
            Creates the real settlement (BORRADOR) of ONE plaza of a RETIRED employee (per-plaza
            granularity, D-10). The retirement facts — effective date, category and reason — are inherited
            read-only from the employee's EXECUTED retirement request (D-03), and the plaza must be one of
            the assignments that retirement closed. At most one live settlement per retirement × plaza
            (annul it to redo, D-16). The engine suggests and computes the five sections immediately; the
            minimum wage is read from the employee's record (supply `minimumMonthlyWage` only when the —
            locked — record has none). The requester can only be HR (D-06). Nothing is paid or written to
            the external payroll: the settlement calculates and documents.
            """)]
    public async Task<ActionResult<PersonnelFileSettlementResponse>> AddSettlement(
        Guid publicId,
        [FromBody] AddSettlementRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddSettlementCommand(
                publicId,
                new SettlementCreateInput(
                    request.AssignedPositionPublicId,
                    request.RequestDate,
                    request.RequesterFilePublicId,
                    request.Notes,
                    request.MinimumMonthlyWage)),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/settlements/{settlementPublicId:guid}/issuance")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileSettlementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Issue the settlement",
        Description = """
            Transitions the settlement BORRADOR → EMITIDA (D-15): the document freezes (immutable — correct
            by annulling and recreating), the issuer and timestamp are recorded and the append-only
            `LIQUIDACION` action lands in the employee's personnel-actions journal. Requires at least one
            included income line; a negative net pay requires `confirmNegativeNet: true`. Issuing is a
            documental act: no payment is executed and the external payroll is never written (FA-1).
            """)]
    public async Task<ActionResult<PersonnelFileSettlementResponse>> IssueSettlement(
        Guid publicId,
        Guid settlementPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] IssueSettlementRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new IssueSettlementCommand(publicId, settlementPublicId, concurrencyToken, request.ConfirmNegativeNet),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/settlements/{settlementPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileSettlementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul the settlement",
        Description = """
            Annuls a real settlement (terminal, history preserved): from BORRADOR the reason is optional;
            from EMITIDA it is mandatory. Annulling frees the (retirement × plaza) slot so a corrected
            settlement can be created (D-16).
            """)]
    public async Task<ActionResult<PersonnelFileSettlementResponse>> AnnulSettlement(
        Guid publicId,
        Guid settlementPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulSettlementRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulSettlementCommand(publicId, settlementPublicId, concurrencyToken, request.Reason),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
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
