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
/// Recurring incomes of a personnel file ("planilla — ingresos cíclicos", REQ-005): a compensation concept paid in
/// installments (finite or open-ended). This controller carries the CRUD + the HR (Manage) lifecycle
/// (suspend/resume, manual closure of an indefinite income, annulment of an EN_REVISION draft); the authorizer
/// resolution/revocation lives in <see cref="RecurringIncomeResolutionController"/> because those writes must map
/// to the dedicated <c>AuthorizeRecurringIncomes</c> grant. HR-only, no self-service in Fase 1 (P-11).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewRecurringIncomes, PersonnelFilePolicies.ManageRecurringIncomes)]
public sealed class RecurringIncomesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/recurring-incomes")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<RecurringIncomeResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's recurring incomes",
        Description = """
            Returns the recurring incomes of the specified personnel file. Requires the `ViewRecurringIncomes`
            permission (HR-only, no self-service — P-11). Each item carries its own `concurrencyToken` for the
            `If-Match` header of subsequent writes.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<RecurringIncomeResponse>>> GetRecurringIncomes(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileRecurringIncomesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file recurring income by id",
        Description = "Returns a single recurring income. The `concurrencyToken` is required in the `If-Match` header of subsequent writes.")]
    public async Task<ActionResult<RecurringIncomeResponse>> GetRecurringIncomeById(
        Guid publicId,
        Guid recurringIncomePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileRecurringIncomeByIdQuery(publicId, recurringIncomePublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/recurring-incomes")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register a recurring income",
        Description = """
            Creates a recurring income in status `EN_REVISION` and returns it with `201 Created`. HR-only
            (`ManageRecurringIncomes`). The income type, compensation concept (Nature = Ingreso), payroll type and
            installment frequency are validated against the country catalogs; the plaza is optional (the principal
            plaza is used when omitted) and its cost center is derived (a plaza without a cost center → 422). The
            plan is normalized (indefinite ⇒ no count/total; finite ⇒ the missing one is derived); `PAGAR_SALDO`
            is invalid for an indefinite plan. The `ETag` header carries the initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringIncomeResponse>> AddRecurringIncome(
        Guid publicId,
        [FromBody] AddRecurringIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileRecurringIncomeCommand(publicId, ToInput(request)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetRecurringIncomeById),
            value => new { publicId, recurringIncomePublicId = value.RecurringIncomePublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a recurring income's header + plan",
        Description = """
            Replaces the business fields (header + installment plan) of an `EN_REVISION` recurring income (RN-02;
            an authorized income is no longer editable). HR-only. Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<RecurringIncomeResponse>> UpdateRecurringIncome(
        Guid publicId,
        Guid recurringIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateRecurringIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileRecurringIncomeCommand(publicId, recurringIncomePublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Discard (soft-delete) an EN_REVISION recurring income draft",
        Description = """
            Soft-deletes an `EN_REVISION` draft (sets it inactive; no physical removal). Only a draft can be
            discarded — an authorized income is revoked or closed. HR-only. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteRecurringIncome(
        Guid publicId,
        Guid recurringIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileRecurringIncomeCommand(publicId, recurringIncomePublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}/suspension")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Suspend or resume a recurring income",
        Description = """
            Suspends a `VIGENTE` income (`suspend` = true, note optional — P-03) or resumes a `SUSPENDIDO` one
            (`suspend` = false). HR-only. Requires the `If-Match` header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringIncomeResponse>> SetRecurringIncomeSuspension(
        Guid publicId,
        Guid recurringIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] SetRecurringIncomeSuspensionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new SetPersonnelFileRecurringIncomeSuspensionCommand(publicId, recurringIncomePublicId, request.Suspend, request.Note, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}/closure")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Close an indefinite recurring income manually",
        Description = """
            Closes an INDEFINITE `VIGENTE` income by hand (→ `FINALIZADO`); the `reason` is mandatory (P-06). A
            finite income finalizes on its own when the plan completes. HR-only. Requires the `If-Match` header.
            """)]
    public async Task<ActionResult<RecurringIncomeResponse>> CloseRecurringIncome(
        Guid publicId,
        Guid recurringIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] CloseRecurringIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ClosePersonnelFileRecurringIncomeCommand(publicId, recurringIncomePublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul an EN_REVISION recurring income",
        Description = """
            Annuls an `EN_REVISION` income (→ `ANULADO`, terminal); the `reason` is mandatory. HR-only. A `VIGENTE`
            income is revoked by the authorizer instead. Requires the `If-Match` header with the current
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringIncomeResponse>> AnnulRecurringIncome(
        Guid publicId,
        Guid recurringIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulRecurringIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulPersonnelFileRecurringIncomeCommand(publicId, recurringIncomePublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}/schedule")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a recurring income's theoretical installment schedule",
        Description = """
            Returns the DERIVED installment schedule of the income (never persisted, D-07): the theoretical
            installments (applied + projected + overdue) with amounts, the running balance and completion. Requires
            the `ViewRecurringIncomes` permission (HR-only).
            """)]
    public async Task<ActionResult<RecurringIncomeScheduleResponse>> GetRecurringIncomeSchedule(
        Guid publicId,
        Guid recurringIncomePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetRecurringIncomeScheduleQuery(publicId, recurringIncomePublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}/installments")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeInstallmentHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a recurring income's installment history",
        Description = """
            Returns the paginated installment history of the income (both `APLICADA` and `ANULADA`, most recent
            activity first). Requires the `ViewRecurringIncomes` permission (HR-only).
            """)]
    public async Task<ActionResult<RecurringIncomeInstallmentHistoryResponse>> GetRecurringIncomeInstallments(
        Guid publicId,
        Guid recurringIncomePublicId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetRecurringIncomeInstallmentsQuery(publicId, recurringIncomePublicId, pageNumber, pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}/installments")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeInstallmentApplicationResult>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Apply the next installment of a recurring income",
        Description = """
            Applies the NEXT installment of a `VIGENTE` income (RF-006). The installment number and amount are
            derived by the rules and are NOT editable (P-04); the plan finalizes (`FINALIZADO`) when the last
            installment lands. HR-only (`ManageRecurringIncomes`). Requires the `If-Match` header with the income's
            current `concurrencyToken`; the income's refreshed token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<RecurringIncomeInstallmentApplicationResult>> ApplyRecurringIncomeInstallment(
        Guid publicId,
        Guid recurringIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ApplyRecurringIncomeInstallmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyRecurringIncomeInstallmentCommand(
                publicId, recurringIncomePublicId, request.AppliedDate, request.PayrollPeriodPublicId, request.Notes, concurrencyToken),
            cancellationToken);

        return this.ToCreatedResult(
            result,
            _ => $"/api/v1/personnel-files/{publicId}/recurring-incomes/{recurringIncomePublicId}/installments",
            value => value.RecurringIncomeConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}/installments/{installmentPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeInstallmentApplicationResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul an applied installment",
        Description = """
            Annuls an applied installment (`APLICADA` → `ANULADA`); the `reason` is mandatory (RF-008). When the
            annulment leaves a finite plan incomplete, the income reopens (`FINALIZADO` → `VIGENTE`) so the number
            can be re-applied. HR-only. Requires the `If-Match` header with the income's current `concurrencyToken`;
            the refreshed token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<RecurringIncomeInstallmentApplicationResult>> AnnulRecurringIncomeInstallment(
        Guid publicId,
        Guid recurringIncomePublicId,
        Guid installmentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulRecurringIncomeInstallmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulRecurringIncomeInstallmentCommand(
                publicId, recurringIncomePublicId, installmentPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.RecurringIncomeConcurrencyToken);
    }

    private static RecurringIncomeInput ToInput(AddRecurringIncomeRequest request) =>
        new(
            request.RegistrationDate,
            request.Reference,
            request.RecurringIncomeTypeCode,
            request.ConceptTypeCode,
            request.Observations,
            request.AssignedPositionPublicId,
            request.InstallmentStartDate,
            request.CurrencyCode,
            request.PayrollTypeCode,
            request.InstallmentFrequencyCode,
            request.IsIndefinite,
            request.InstallmentValue,
            request.InstallmentCount,
            request.TotalAmount,
            request.SettlementActionCode);

    private static RecurringIncomeInput ToInput(UpdateRecurringIncomeRequest request) =>
        new(
            request.RegistrationDate,
            request.Reference,
            request.RecurringIncomeTypeCode,
            request.ConceptTypeCode,
            request.Observations,
            request.AssignedPositionPublicId,
            request.InstallmentStartDate,
            request.CurrencyCode,
            request.PayrollTypeCode,
            request.InstallmentFrequencyCode,
            request.IsIndefinite,
            request.InstallmentValue,
            request.InstallmentCount,
            request.TotalAmount,
            request.SettlementActionCode);
}
