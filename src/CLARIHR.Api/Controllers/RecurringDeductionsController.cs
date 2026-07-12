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
/// Recurring deductions of a personnel file ("planilla — descuentos cíclicos", REQ-008): a credit the company
/// discounts from the employee in installments, either on a plain segment plan or with French-system compound
/// interest. This controller carries the CRUD + the HR (Manage) lifecycle (suspend/resume, manual closure of an
/// indefinite credit, annulment of an EN_REVISION draft); the authorizer resolution/revocation lives in
/// <see cref="RecurringDeductionResolutionController"/> because those writes must map to the dedicated
/// <c>AuthorizeRecurringDeductions</c> grant. HR-only, no self-service in Fase 1.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewRecurringDeductions, PersonnelFilePolicies.ManageRecurringDeductions)]
public sealed class RecurringDeductionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/recurring-deductions")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<RecurringDeductionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's recurring deductions",
        Description = """
            Returns the recurring deductions (credits) of the specified personnel file. Requires the
            `ViewRecurringDeductions` permission (HR-only, no self-service). Each item carries its plan segments,
            its derived `installmentCount` / `totalAmount` and its own `concurrencyToken` for the `If-Match` header
            of subsequent writes.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<RecurringDeductionResponse>>> GetRecurringDeductions(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileRecurringDeductionsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file recurring deduction by id",
        Description = "Returns a single recurring deduction. The `concurrencyToken` is required in the `If-Match` header of subsequent writes.")]
    public async Task<ActionResult<RecurringDeductionResponse>> GetRecurringDeductionById(
        Guid publicId,
        Guid recurringDeductionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileRecurringDeductionByIdQuery(publicId, recurringDeductionPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/recurring-deductions")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register a recurring deduction",
        Description = """
            Creates a credit in status `EN_REVISION` and returns it with `201 Created`. HR-only
            (`ManageRecurringDeductions`). The deduction type, payroll type and BOTH frequencies are validated
            against the country catalogs; the compensation concept must be an ACTIVE, NON-STATUTORY `Egreso`
            concept (ISSS/AFP/Renta are payroll law, not credits → 422) and the `financialInstitution` is MANDATORY
            when that concept is EXTERNAL. The plaza is optional (the principal plaza is used when omitted).
            `effectiveDate` MAY be in the future — the credit is registered and authorized, but no installment can
            be charged until the date is reached. The plan is normalized: WITHOUT interest it comes from the
            contiguous `segments`; WITH `usesCompoundInterest` it is DERIVED from principal + nominal annual rate +
            planned installments (and carries no segments, and cannot be indefinite). The application frequency
            must divide the installment frequency; `DESCONTAR_SALDO` is invalid for an indefinite plan. The `ETag`
            header carries the initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringDeductionResponse>> AddRecurringDeduction(
        Guid publicId,
        [FromBody] AddRecurringDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileRecurringDeductionCommand(publicId, ToInput(request)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetRecurringDeductionById),
            value => new { publicId, recurringDeductionPublicId = value.RecurringDeductionPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a recurring deduction's header + plan",
        Description = """
            Replaces the business fields (header + installment plan) of an `EN_REVISION` credit; an authorized one
            is no longer editable. The `segments` are REPLACE-ALL (the previous plan is discarded). HR-only.
            Requires the `If-Match` header with the current `concurrencyToken`; the new token is returned in the
            `ETag` header.
            """)]
    public async Task<ActionResult<RecurringDeductionResponse>> UpdateRecurringDeduction(
        Guid publicId,
        Guid recurringDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateRecurringDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileRecurringDeductionCommand(publicId, recurringDeductionPublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Discard (soft-delete) an EN_REVISION recurring-deduction draft",
        Description = """
            Soft-deletes an `EN_REVISION` draft (sets it inactive; no physical removal). Only a draft can be
            discarded — an authorized credit is revoked or closed. HR-only. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteRecurringDeduction(
        Guid publicId,
        Guid recurringDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileRecurringDeductionCommand(publicId, recurringDeductionPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}/suspension")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Suspend or resume a recurring deduction",
        Description = """
            Suspends a `VIGENTE` credit (`suspend` = true; the note is optional) or resumes a `SUSPENDIDO` one
            (`suspend` = false). A suspended credit charges no installments and takes no extraordinary payments.
            HR-only. Requires the `If-Match` header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringDeductionResponse>> SetRecurringDeductionSuspension(
        Guid publicId,
        Guid recurringDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] SetRecurringDeductionSuspensionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new SetPersonnelFileRecurringDeductionSuspensionCommand(
                publicId, recurringDeductionPublicId, request.Suspend, request.Note, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}/closure")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Close an indefinite VIGENTE recurring deduction by hand",
        Description = """
            Closes an INDEFINITE `VIGENTE` credit (→ `FINALIZADO`); the `reason` is mandatory. A finite credit is
            NOT closed this way — it ends when its plan completes or its balance is paid off. HR-only. Requires the
            `If-Match` header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringDeductionResponse>> CloseRecurringDeduction(
        Guid publicId,
        Guid recurringDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] CloseRecurringDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ClosePersonnelFileRecurringDeductionCommand(publicId, recurringDeductionPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul an EN_REVISION recurring deduction",
        Description = """
            Annuls an `EN_REVISION` credit (→ `ANULADO`, terminal); the `reason` is mandatory. Revoking an
            AUTHORIZED credit is the authorizer's action (see the resolution controller) — this endpoint is the HR
            branch and only touches drafts. Requires the `If-Match` header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringDeductionResponse>> AnnulRecurringDeduction(
        Guid publicId,
        Guid recurringDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulRecurringDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulPersonnelFileRecurringDeductionCommand(publicId, recurringDeductionPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}/schedule")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get the derived schedule of a recurring deduction",
        Description = """
            Returns the DERIVED plan of the credit (never persisted): one row per CHARGE — the rows advance by the
            APPLICATION cadence, so a monthly quota charged fortnightly yields two rows of half the quota, and the
            exception months are skipped (the plan is pushed forward, not shortened). A compound-interest credit
            carries the capital/interest split per row (French-system amortization). Applied rows are flagged; an
            unapplied row whose due date is past is `isOverdue`. It also carries the derived totals the business
            asked for: `totalCharged` ("total cobrado"), `totalOutstanding` ("total no cobrado") and
            `outstandingBalance` — which for an interest-bearing credit is the outstanding CAPITAL (the payoff), a
            SMALLER figure than the sum of the remaining quotas, because paying early does not owe future interest.
            """)]
    public async Task<ActionResult<RecurringDeductionScheduleResponse>> GetRecurringDeductionSchedule(
        Guid publicId,
        Guid recurringDeductionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetRecurringDeductionScheduleQuery(publicId, recurringDeductionPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}/installments")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionInstallmentHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List the charge history of a recurring deduction",
        Description = """
            Returns the applied and annulled charges (REGULAR and EXTRAORDINARIA alike), most recent activity
            first, with their currency / payroll-type / payroll-period snapshots and their capital/interest split.
            """)]
    public async Task<ActionResult<RecurringDeductionInstallmentHistoryResponse>> GetRecurringDeductionInstallments(
        Guid publicId,
        Guid recurringDeductionPublicId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetRecurringDeductionInstallmentsQuery(publicId, recurringDeductionPublicId, pageNumber, pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}/installments")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionInstallmentApplicationResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Apply the next charge of a recurring deduction",
        Description = """
            Applies the NEXT charge of a `VIGENTE` credit. The number, the amount and the capital/interest split
            are derived by the rules and are NOT editable. The credit's `effectiveDate` must have been REACHED (a
            future-dated credit → 422) and the sequence is strict (it fills any annulled gap first). When the
            charge completes the plan — or the balance reaches zero — the credit is FINALIZED in the same
            transaction. Serialized by an advisory lock. Requires the `If-Match` header with the credit's current
            `concurrencyToken`; the refreshed token comes back in the `ETag`.
            """)]
    public async Task<ActionResult<RecurringDeductionInstallmentApplicationResult>> ApplyRecurringDeductionInstallment(
        Guid publicId,
        Guid recurringDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ApplyRecurringDeductionInstallmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyRecurringDeductionInstallmentCommand(
                publicId, recurringDeductionPublicId, request.AppliedDate, request.PayrollPeriodPublicId, request.Notes, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.RecurringDeductionConcurrencyToken);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}/extraordinary-installments")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionInstallmentApplicationResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Apply an extraordinary payment (abono) to a recurring deduction",
        Description = """
            Registers an out-of-sequence payment against a `VIGENTE` credit. The amount goes 100 % against CAPITAL
            and SHORTENS THE TERM — the quota is untouched (P-04), so the derived schedule simply ends earlier.
            Paying exactly the `outstandingBalance` is a PAYOFF: the credit finalizes in the same transaction.
            Rejected on a `SUSPENDIDO` credit, above the outstanding balance, or on an indefinite plan (422).
            Requires the `If-Match` header with the credit's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringDeductionInstallmentApplicationResult>> ApplyRecurringDeductionExtraordinary(
        Guid publicId,
        Guid recurringDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ApplyRecurringDeductionExtraordinaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyRecurringDeductionExtraordinaryCommand(
                publicId, recurringDeductionPublicId, request.Amount, request.AppliedDate, request.PayrollPeriodPublicId, request.Notes, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.RecurringDeductionConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}/installments/{installmentPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionInstallmentApplicationResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul an applied charge of a recurring deduction",
        Description = """
            Annuls an APLICADA charge — regular or extraordinary; the `reason` is mandatory. The number is freed
            and can be re-applied. If the credit had FINALIZED and the plan is no longer complete after the
            annulment, it REOPENS to `VIGENTE` in the same transaction. Requires the `If-Match` header with the
            credit's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringDeductionInstallmentApplicationResult>> AnnulRecurringDeductionInstallment(
        Guid publicId,
        Guid recurringDeductionPublicId,
        Guid installmentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulRecurringDeductionInstallmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulRecurringDeductionInstallmentCommand(
                publicId, recurringDeductionPublicId, installmentPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.RecurringDeductionConcurrencyToken);
    }

    private static RecurringDeductionInput ToInput(AddRecurringDeductionRequest request) =>
        new(
            request.EffectiveDate,
            request.Reference,
            request.RecurringDeductionTypeCode,
            request.ConceptTypeCode,
            request.FinancialInstitution,
            request.Observations,
            request.AssignedPositionPublicId,
            request.InstallmentStartDate,
            request.ExceptionMonths,
            request.CurrencyCode,
            request.PayrollTypeCode,
            request.InstallmentFrequencyCode,
            request.ApplicationFrequencyCode,
            request.IsIndefinite,
            request.UsesCompoundInterest,
            request.PrincipalAmount,
            request.InterestRatePercent,
            request.PlannedInstallments,
            ToSegments(request.Segments),
            request.SettlementActionCode);

    private static RecurringDeductionInput ToInput(UpdateRecurringDeductionRequest request) =>
        new(
            request.EffectiveDate,
            request.Reference,
            request.RecurringDeductionTypeCode,
            request.ConceptTypeCode,
            request.FinancialInstitution,
            request.Observations,
            request.AssignedPositionPublicId,
            request.InstallmentStartDate,
            request.ExceptionMonths,
            request.CurrencyCode,
            request.PayrollTypeCode,
            request.InstallmentFrequencyCode,
            request.ApplicationFrequencyCode,
            request.IsIndefinite,
            request.UsesCompoundInterest,
            request.PrincipalAmount,
            request.InterestRatePercent,
            request.PlannedInstallments,
            ToSegments(request.Segments),
            request.SettlementActionCode);

    private static IReadOnlyCollection<RecurringDeductionSegmentInput>? ToSegments(
        IReadOnlyCollection<RecurringDeductionSegmentRequest>? segments) =>
        segments?
            .Select(segment => new RecurringDeductionSegmentInput(
                segment.FromInstallment,
                segment.ToInstallment,
                segment.InstallmentValue))
            .ToArray();
}
