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
            Replaces the editable fields (currency code and time zone). Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
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
        string TimeZone);

    public sealed class PatchCompanyPreferencesRequest
    {
        public string CurrencyCode { get; set; } = string.Empty;

        public string TimeZone { get; set; } = string.Empty;
    }
}
