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
/// Employee incapacities ("incapacidades" — health data). Kept in a dedicated controller so it runs under its
/// own authn-only policy set (<see cref="PersonnelFilePolicies.ViewIncapacities"/> /
/// <see cref="PersonnelFilePolicies.ManageIncapacities"/>); the precise permission-or-self decision (D-18) and
/// the anti-self confirmation are enforced by the incapacity handler gates, which a declarative policy cannot
/// express. Writes recalculate the day/amount breakdown inline (engine) and return non-blocking `warnings[]`.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewIncapacities, PersonnelFilePolicies.ManageIncapacities)]
public sealed class PersonnelFileIncapacitiesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/incapacities")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileIncapacityResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's incapacities",
        Description = """
            Returns every incapacity recorded for the specified personnel file with its engine breakdown (days
            and referential amounts). Access requires the `ViewIncapacities` permission or the employee reading
            their own incapacities (health data, D-18).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileIncapacityResponse>>> GetIncapacities(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileIncapacitiesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/incapacities/{incapacityPublicId:guid}", Name = "GetIncapacityById")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileIncapacityResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get an incapacity by id")]
    public async Task<ActionResult<PersonnelFileIncapacityResponse>> GetIncapacityById(
        Guid publicId,
        Guid incapacityPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileIncapacityByIdQuery(publicId, incapacityPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/incapacities")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileIncapacityResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register an incapacity",
        Description = """
            Registers an incapacity and computes its day/amount breakdown inline. HR (`ManageIncapacities`)
            registers a REGISTRADA record; the employee may self-register on their own file (EN_REVISION until
            HR confirms — D-18). When the company preference requires it, a `documentFilePublicId` (constancia,
            purpose `IncapacityDocument`) is mandatory and is attached in the same transaction.
            """)]
    public async Task<ActionResult<PersonnelFileIncapacityResponse>> AddIncapacity(
        Guid publicId,
        [FromBody] AddIncapacityRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileIncapacityCommand(publicId, ToInput(request)), cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetIncapacityById),
            value => new { publicId, incapacityPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/incapacities/{incapacityPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileIncapacityResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Edit an incapacity's business fields",
        Description = """
            Updates the master references, dates and notes and recalculates the breakdown. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned in the `ETag`
            header. Editing the dates of a record that already has extensions is rejected (chain locked).
            """)]
    public async Task<ActionResult<PersonnelFileIncapacityResponse>> UpdateIncapacity(
        Guid publicId,
        Guid incapacityPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateIncapacityRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileIncapacityCommand(publicId, incapacityPublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/incapacities/{incapacityPublicId:guid}/confirmation")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileIncapacityResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Confirm a self-registered incapacity (EN_REVISION → REGISTRADA)",
        Description = """
            HR confirmation of an EN_REVISION registration (D-18). Recalculates the breakdown against the
            employer cap available at confirmation time and journals the INCAPACIDAD personnel action. The
            confirming user must not be the subject employee (anti-self, 403). Requires the `If-Match` header.
            """)]
    public async Task<ActionResult<PersonnelFileIncapacityResponse>> ConfirmIncapacity(
        Guid publicId,
        Guid incapacityPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ConfirmPersonnelFileIncapacityCommand(publicId, incapacityPublicId, concurrencyToken), cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/incapacities/{incapacityPublicId:guid}/closure")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileIncapacityResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Close an open-ended incapacity",
        Description = """
            Fixes the end date of an open-ended incapacity (D-11) and computes its final breakdown. Requires the
            `If-Match` header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileIncapacityResponse>> CloseIncapacity(
        Guid publicId,
        Guid incapacityPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] CloseIncapacityRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ClosePersonnelFileIncapacityCommand(publicId, incapacityPublicId, request.EndDate, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/incapacities/{incapacityPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileIncapacityResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul an incapacity",
        Description = """
            Annuls an EN_REVISION or REGISTRADA incapacity (terminal); the reason is mandatory. An annulled
            record no longer consumes the employer cap. A record with live extensions must be annulled
            tail-first (chain locked). Requires the `If-Match` header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileIncapacityResponse>> AnnulIncapacity(
        Guid publicId,
        Guid incapacityPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulIncapacityRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulPersonnelFileIncapacityCommand(publicId, incapacityPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/incapacities/{incapacityPublicId:guid}/extensions")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileIncapacityResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register an extension (prórroga) of an incapacity",
        Description = """
            Registers a new incapacity that extends the source one: its start date is the source end date + 1
            (RN-04) and its tranche numbering continues the chain (RN-03). The source must be a closed,
            non-annulled, already-registered record and the risk must allow extensions. Journals the
            PRORROGA_INCAPACIDAD personnel action. HR-only.
            """)]
    public async Task<ActionResult<PersonnelFileIncapacityResponse>> AddIncapacityExtension(
        Guid publicId,
        Guid incapacityPublicId,
        [FromBody] AddIncapacityExtensionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileIncapacityExtensionCommand(publicId, incapacityPublicId, ToInput(request)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetIncapacityById),
            value => new { publicId, incapacityPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/incapacity-balance")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileIncapacityBalanceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get the employer-cap balance of an employee for a year",
        Description = """
            Returns the yearly employer-cap balance (covered + benefit days, consumed days by REGISTRADA
            incapacities and the remaining days — the same figure as the profile's `disabilityDaysAvailable`).
            Defaults to the current year. Access requires `ViewIncapacities` or the owner employee.
            """)]
    public async Task<ActionResult<PersonnelFileIncapacityBalanceResponse>> GetIncapacityBalance(
        Guid publicId,
        [FromQuery] int? year,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileIncapacityBalanceQuery(publicId, year), cancellationToken);
        return this.ToActionResult(result);
    }

    private static IncapacityInput ToInput(AddIncapacityRequest request) =>
        new(
            request.RiskPublicId,
            request.IncapacityTypePublicId,
            request.MedicalClinicPublicId,
            request.AssignedPositionPublicId,
            request.PayrollTypeCode,
            request.PayrollPeriodDefinitionPublicId,
            request.StartDate,
            request.EndDate,
            request.Notes,
            request.DocumentFilePublicId,
            request.DocumentTypeCatalogItemPublicId,
            request.DocumentObservations);

    private static IncapacityInput ToInput(UpdateIncapacityRequest request) =>
        new(
            request.RiskPublicId,
            request.IncapacityTypePublicId,
            request.MedicalClinicPublicId,
            request.AssignedPositionPublicId,
            request.PayrollTypeCode,
            request.PayrollPeriodDefinitionPublicId,
            request.StartDate,
            request.EndDate,
            request.Notes,
            request.DocumentFilePublicId,
            request.DocumentTypeCatalogItemPublicId,
            request.DocumentObservations);

    private static IncapacityExtensionInput ToInput(AddIncapacityExtensionRequest request) =>
        new(
            request.RiskPublicId,
            request.IncapacityTypePublicId,
            request.MedicalClinicPublicId,
            request.AssignedPositionPublicId,
            request.PayrollTypeCode,
            request.PayrollPeriodDefinitionPublicId,
            request.EndDate,
            request.Notes,
            request.DocumentFilePublicId,
            request.DocumentTypeCatalogItemPublicId,
            request.DocumentObservations);
}
