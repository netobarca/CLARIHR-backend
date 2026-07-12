using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.Preferences.Company;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/companies/{companyId:guid}/preferences")]
[Tags("Company Preferences")]
public sealed class CompanyPreferencesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CompanyPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get the company preferences",
        Description = """
            Returns the singleton preference record (currency and time zone) for the company. The
            record is provisioned with the company, so it always exists for an authorized caller; a
            missing record yields `404`. The current `concurrencyToken` is included in the body for
            use in the `If-Match` header of a subsequent update.
            """)]
    public async Task<ActionResult<CompanyPreferenceResponse>> Get(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCompanyPreferencesQuery(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut]
    [ProducesResponseType<CompanyPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace the company preferences",
        Description = """
            Replaces the editable fields: currency code, time zone, the optional HR-dashboard and
            economic-aid parametrization, and the optional vacation/incapacity parametrization
            (nullable — `null` means the legal default, resolved when the policy is consumed).
            Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`,
            stale → `409`). The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CompanyPreferenceResponse>> Update(
        Guid companyId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCompanyPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompanyPreferencesCommand(
                companyId,
                request.CurrencyCode,
                request.TimeZone,
                request.HrFunctionalAreaCode,
                request.FileUpToDateThresholdMonths,
                request.MinimumSeniorityMonthsForEconomicAid,
                request.AnnualVacationDaysDefault,
                request.AdditionalVacationBenefitDaysDefault,
                request.AllowVacationStartOnHoliday,
                request.AllowVacationEndOnHoliday,
                request.AllowVacationStartOnRestDay,
                request.DefaultUseAnniversary,
                request.CompanyRestDayOfWeek,
                request.EmployerCoveredIncapacityDaysPerYear,
                request.AdditionalIncapacityBenefitDaysPerYear,
                request.IncapacityRequiresDocument,
                request.CompensatoryTimeStandardDailyHours,
                request.CompensatoryTimeMaxBalanceHours,
                request.CompensatoryTimeCreditRequiresDocument,
                request.CompensatoryTimeSettlementRateFactor,
                request.OvertimeSelfServiceEnabled,
                request.OvertimeMaxDailyMinutes,
                request.RecurringDeductionDefaultInterestRatePercent,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<CompanyPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch the company preferences",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902), media type
            `application/json-patch+json`. Patchable paths: `/currencyCode` (exactly 3 characters)
            and `/timeZone` (max 100 characters); both are required and cannot be removed. Requires
            the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale →
            `409`). The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CompanyPreferenceResponse>> Patch(
        Guid companyId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchCompanyPreferencesRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchCompanyPreferencesCommand(
                companyId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new CompanyPreferencePatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record UpdateCompanyPreferencesRequest(
        string CurrencyCode,
        string TimeZone,
        // HR analytics dashboard parametrization (optional): the FunctionalArea code that marks the HR area
        // (D-06) and the "expediente actualizado" window in months (D-08).
        string? HrFunctionalAreaCode = null,
        int? FileUpToDateThresholdMonths = null,
        // Economic-aid eligibility (D-08): minimum seniority in months to request economic aid (optional).
        int? MinimumSeniorityMonthsForEconomicAid = null,
        // Vacation & incapacity parametrization (D-20/D-24/D-26/D-27), all optional: null = the legal
        // default (15 / 0 / no / yes / no / yes / Sunday / 9 / 0 / yes), resolved when the policy is
        // consumed — it is never stored. Day counts 0-365; rest day 0-6 with Sunday = 0.
        int? AnnualVacationDaysDefault = null,
        int? AdditionalVacationBenefitDaysDefault = null,
        bool? AllowVacationStartOnHoliday = null,
        bool? AllowVacationEndOnHoliday = null,
        bool? AllowVacationStartOnRestDay = null,
        bool? DefaultUseAnniversary = null,
        int? CompanyRestDayOfWeek = null,
        int? EmployerCoveredIncapacityDaysPerYear = null,
        int? AdditionalIncapacityBenefitDaysPerYear = null,
        bool? IncapacityRequiresDocument = null,
        // Compensatory-time parametrization (REQ-002 P-10/P-11/P-15), all optional: null = the default
        // (8 h/day / no cap / document required / rate 1.00), resolved when consumed — never stored.
        // Numeric factors must be > 0 when provided.
        decimal? CompensatoryTimeStandardDailyHours = null,
        decimal? CompensatoryTimeMaxBalanceHours = null,
        bool? CompensatoryTimeCreditRequiresDocument = null,
        decimal? CompensatoryTimeSettlementRateFactor = null,
        // Overtime parametrization (REQ-007 P-01/P-05), all optional: null = self-service off / no daily
        // cap. The daily-minutes cap must be > 0 when provided.
        bool? OvertimeSelfServiceEnabled = null,
        int? OvertimeMaxDailyMinutes = null,
        // Recurring-deduction parametrization (REQ-008 P-03), optional: the nominal ANNUAL rate pre-loaded on
        // the credit form when it uses compound interest. Null = no default. Must be in (0, 100] when
        // provided. The rate that governs a credit is always the one persisted on that credit.
        decimal? RecurringDeductionDefaultInterestRatePercent = null);

    public sealed class PatchCompanyPreferencesRequest
    {
        public string CurrencyCode { get; set; } = string.Empty;

        public string TimeZone { get; set; } = string.Empty;
    }
}
