using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Application.Features.Leave.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Medical Clinics")]
[AuthorizationPolicySet(LeaveConfigurationPolicies.Read, LeaveConfigurationPolicies.Manage)]
[ResourceActions(LeaveConfigurationPermissionCodes.MedicalClinicsResourceKey)]
public sealed class MedicalClinicsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/medical-clinics")]
    [ProducesResponseType<PagedResponse<MedicalClinicListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List medical clinics for a company",
        Description = """
            Returns a paginated list of the company's medical clinics, filterable by
            `isActive`, `sectorCode` (clinic-sectors catalog code) and free-text `q` over the
            description. The owning company is validated against the authenticated tenant.
            Set `includeAllowedActions=true` to receive per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<MedicalClinicListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] string? sectorCode,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, LeaveConfigurationValidationRules.MaxPageSize)] int pageSize = LeaveConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchMedicalClinicsQuery(companyId, isActive, sectorCode, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("medical-clinics/{id:guid}")]
    [ProducesResponseType<MedicalClinicResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a medical clinic by id",
        Description = """
            Returns a single medical clinic by its public id. The owning company is resolved
            from the authenticated tenant; a non-existent id yields `404`, while an id that
            belongs to another tenant yields `403 TENANT_MISMATCH`. The current
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<MedicalClinicResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetMedicalClinicByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/medical-clinics")]
    [ProducesResponseType<MedicalClinicResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a medical clinic",
        Description = """
            Creates a medical clinic under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. The optional `sectorCode` must be an active code of
            the country-scoped clinic-sectors catalog (`422` otherwise). A duplicate
            description yields `409`.
            """)]
    public async Task<ActionResult<MedicalClinicResponse>> Create(
        Guid companyId,
        [FromBody] CreateMedicalClinicRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateMedicalClinicCommand(
                companyId,
                request.Description,
                request.Specialty,
                request.SectorCode),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`) or
        // link generation fails. Mirrors CostCentersController's POST.
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("medical-clinics/{id:guid}")]
    [ProducesResponseType<MedicalClinicResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a medical clinic",
        Description = """
            Replaces the editable fields of a medical clinic (description, specialty, sector
            code). The optional `sectorCode` must be an active code of the country-scoped
            clinic-sectors catalog (`422` otherwise). Requires the current `concurrencyToken`
            in the `If-Match` header; a missing/malformed header yields `400` and a stale
            token yields `409 CONCURRENCY_CONFLICT`. A duplicate description yields `409`.
            The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<MedicalClinicResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateMedicalClinicRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateMedicalClinicCommand(
                id,
                request.Description,
                request.Specialty,
                request.SectorCode,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("medical-clinics/{id:guid}/activate")]
    [ProducesResponseType<MedicalClinicResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a medical clinic",
        Description = """
            Reactivates an inactive medical clinic. Requires the current `concurrencyToken`
            in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<MedicalClinicResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateMedicalClinicCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("medical-clinics/{id:guid}/inactivate")]
    [ProducesResponseType<MedicalClinicResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a medical clinic",
        Description = """
            Deactivates (soft-delete) a medical clinic. Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<MedicalClinicResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateMedicalClinicCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateMedicalClinicRequest(
        string Description,
        string? Specialty,
        string? SectorCode);

    public sealed record UpdateMedicalClinicRequest(
        string Description,
        string? Specialty,
        string? SectorCode);
}
