using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.Compliance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/companies/{companyId:guid}/legal-profile")]
[Tags("Company Legal Profile")]
public sealed class CompanyLegalProfilesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CompanyLegalProfileResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get the company legal profile",
        Description = """
            Returns the employer legal identity (razón social, NIT patronal, registro patronal ISSS,
            dirección fiscal) used as the header of the payroll compliance reports (F-14, Planilla
            Única, Planilla Patronal — REQ-016). Returns `404` when the company has not configured it
            yet; the current `concurrencyToken` is included for use in the `If-Match` header of a
            subsequent update.
            """)]
    public async Task<ActionResult<CompanyLegalProfileResponse>> Get(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCompanyLegalProfileQuery(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<CompanyLegalProfileResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create the company legal profile",
        Description = """
            Creates the employer legal identity for a company that does not have one yet. Fails with
            `409` if the company already has a profile — use the `PUT` endpoint to edit it. Once
            created, this record gates payroll generation for the tenant once the internal payroll
            compliance activation switch is turned on (REQ-016 RF-006, ratified P-03).
            """)]
    public async Task<ActionResult<CompanyLegalProfileResponse>> Create(
        Guid companyId,
        [FromBody] CreateCompanyLegalProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateCompanyLegalProfileCommand(
                companyId,
                request.LegalName,
                request.EmployerNitNumber,
                request.IsssEmployerRegistrationNumber,
                request.FiscalAddress,
                request.EconomicActivityDescription,
                request.LegalRepresentativePublicId),
            cancellationToken);

        return this.ToCreatedAtActionResult(result, nameof(Get), _ => new { companyId }, value => value.ConcurrencyToken);
    }

    [HttpPut]
    [ProducesResponseType<CompanyLegalProfileResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace the company legal profile",
        Description = """
            Replaces the employer legal identity fields. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the
            body and the `ETag` header.
            """)]
    public async Task<ActionResult<CompanyLegalProfileResponse>> Update(
        Guid companyId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCompanyLegalProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompanyLegalProfileCommand(
                companyId,
                request.LegalName,
                request.EmployerNitNumber,
                request.IsssEmployerRegistrationNumber,
                request.FiscalAddress,
                request.EconomicActivityDescription,
                request.LegalRepresentativePublicId,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateCompanyLegalProfileRequest(
        string LegalName,
        string EmployerNitNumber,
        string IsssEmployerRegistrationNumber,
        string FiscalAddress,
        string? EconomicActivityDescription = null,
        Guid? LegalRepresentativePublicId = null);

    public sealed record UpdateCompanyLegalProfileRequest(
        string LegalName,
        string EmployerNitNumber,
        string IsssEmployerRegistrationNumber,
        string FiscalAddress,
        string? EconomicActivityDescription = null,
        Guid? LegalRepresentativePublicId = null);
}
