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
/// Company-level certificate settings (D-17): the configurable letterhead/logo, issuing city, signatory and
/// footer merged into every generated certificate PDF. Dedicated controller running under the certificate
/// policy set (read = ViewCertificateRequests, write = ManageCertificateRequests).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewCertificateRequests, PersonnelFilePolicies.ManageCertificateRequests)]
public sealed class CompanyCertificateSettingsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/certificate-settings")]
    [Produces("application/json")]
    [ProducesResponseType<CompanyCertificateSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get the company certificate settings",
        Description = """
            Returns the configurable letterhead/logo, issuing city, signatory and footer used when generating a
            certificate PDF. If the company has not configured them yet, returns empty values with an empty
            `concurrencyToken` (send it in the `If-Match` header of the first update).
            """)]
    public async Task<ActionResult<CompanyCertificateSettingsResponse>> Get(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCompanyCertificateSettingsQuery(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/companies/{companyId:guid}/certificate-settings")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<CompanyCertificateSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace the company certificate settings",
        Description = """
            Replaces the letterhead/logo, issuing city, signatory and footer. HR-only
            (`ManageCertificateRequests`). The logo, when supplied, must be an active `CompanyLogo` file of the
            company. Requires the `If-Match` header with the current `concurrencyToken` (the empty token on first
            configuration); the refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CompanyCertificateSettingsResponse>> Update(
        Guid companyId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCompanyCertificateSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompanyCertificateSettingsCommand(
                companyId,
                request.LogoFilePublicId,
                request.IssuingCity,
                request.SignatoryName,
                request.SignatoryTitle,
                request.FooterText,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}
