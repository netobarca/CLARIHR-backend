using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewCompensation, PersonnelFilePolicies.Manage)]
public sealed class PersonnelFileCompensationConceptsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/compensation-concepts")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileCompensationConceptResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's compensation concepts",
        Description = """
            Returns every compensation concept (ingreso/egreso) recorded for the specified personnel file.
            Each item carries its own `concurrencyToken`, required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileCompensationConceptResponse>>> GetCompensationConcepts(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileCompensationConceptsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/compensation-concepts/{compensationConceptPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCompensationConceptResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file compensation concept by id",
        Description = """
            Returns a single compensation concept of the specified personnel file. The `concurrencyToken`
            in the response is required in the `If-Match` header of subsequent `PUT`/`PATCH`/`DELETE`
            requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileCompensationConceptResponse>> GetCompensationConceptById(
        Guid publicId,
        Guid compensationConceptPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileCompensationConceptByIdQuery(publicId, compensationConceptPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/compensation-concepts")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCompensationConceptResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a compensation concept to a personnel file",
        Description = """
            Creates a new compensation concept (ingreso/egreso, fixed or percentage) under the specified
            personnel file and returns it with a `201 Created` response. The `Location` header points to
            the created resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileCompensationConceptResponse>> AddCompensationConcept(
        Guid publicId,
        [FromBody] AddCompensationConceptRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileCompensationConceptCommand(publicId, ToInput(request)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetCompensationConceptById),
            value => new { publicId, compensationConceptPublicId = value.CompensationConceptPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/compensation-concepts/{compensationConceptPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCompensationConceptResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file compensation concept",
        Description = """
            Replaces the business fields of an existing compensation concept. The active state is
            preserved (it is mutated exclusively via `PATCH`). Requires the `If-Match` header with the
            current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileCompensationConceptResponse>> UpdateCompensationConcept(
        Guid publicId,
        Guid compensationConceptPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCompensationConceptRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileCompensationConceptCommand(
                publicId,
                compensationConceptPublicId,
                ToInput(request),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/compensation-concepts/{compensationConceptPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileCompensationConceptResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file compensation concept",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type `application/json-patch+json`) to an
            existing compensation concept. Supports the business fields and the `isActive` flag. Requires
            the `If-Match` header with the current `concurrencyToken`; the new token is returned in the
            `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileCompensationConceptResponse>> PatchCompensationConcept(
        Guid publicId,
        Guid compensationConceptPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchCompensationConceptRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileCompensationConceptCommand(
                publicId,
                compensationConceptPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileCompensationConceptPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/compensation-concepts/{compensationConceptPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a compensation concept from a personnel file",
        Description = """
            Deletes the specified compensation concept. Requires the `If-Match` header with the current
            `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token so the
            caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteCompensationConcept(
        Guid publicId,
        Guid compensationConceptPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileCompensationConceptCommand(publicId, compensationConceptPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    private static CompensationConceptInput ToInput(AddCompensationConceptRequest request) =>
        new(
            request.AssignedPositionPublicId,
            request.Nature,
            request.ConceptTypeCode,
            request.DeductionClass,
            request.CalculationType,
            request.Value,
            request.CalculationBaseCode,
            request.EmployerRate,
            request.ContributionCap,
            request.CurrencyCode,
            request.PayPeriodCode,
            request.CounterpartyName,
            request.ExternalReference,
            request.StartDate,
            request.EndDate,
            request.IsActive,
            request.Notes);

    private static CompensationConceptInput ToInput(UpdateCompensationConceptRequest request) =>
        new(
            request.AssignedPositionPublicId,
            request.Nature,
            request.ConceptTypeCode,
            request.DeductionClass,
            request.CalculationType,
            request.Value,
            request.CalculationBaseCode,
            request.EmployerRate,
            request.ContributionCap,
            request.CurrencyCode,
            request.PayPeriodCode,
            request.CounterpartyName,
            request.ExternalReference,
            request.StartDate,
            request.EndDate,
            IsActive: true,
            request.Notes);
}
