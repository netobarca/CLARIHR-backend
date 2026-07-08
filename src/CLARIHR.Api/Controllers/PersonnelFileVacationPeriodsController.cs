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
/// Employee vacation fund periods (leave module PR-7, D-05). Kept in a dedicated controller under the
/// vacation authn-only policy set (<see cref="PersonnelFilePolicies.ViewVacations"/> /
/// <see cref="PersonnelFilePolicies.ManageVacations"/>); the precise permission-or-self read decision (fund
/// legible with ViewVacations OR the owner) and the manage-only writes are enforced by the handler gates.
/// The fund balances (enjoyed/pending) and the financial provision are served by the vacation-fund endpoint.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewVacations, PersonnelFilePolicies.ManageVacations)]
public sealed class PersonnelFileVacationPeriodsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/vacation-periods")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileVacationPeriodResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's vacation fund periods",
        Description = """
            Returns the active vacation fund periods of the employee (yearly grants + derived bounds). Access
            requires the `ViewVacations` permission or the employee reading their own fund (D-18).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileVacationPeriodResponse>>> GetVacationPeriods(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileVacationPeriodsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/vacation-periods/{vacationPeriodPublicId:guid}", Name = "GetVacationPeriodById")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileVacationPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a vacation fund period by id")]
    public async Task<ActionResult<PersonnelFileVacationPeriodResponse>> GetVacationPeriodById(
        Guid publicId,
        Guid vacationPeriodPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileVacationPeriodByIdQuery(publicId, vacationPeriodPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/vacation-periods")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileVacationPeriodResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a vacation fund period",
        Description = """
            Creates a manual vacation fund period for the employee and year. The bounds are derived from the
            anniversary flag (defaulting to the company preference) and the employee's primary-plaza anniversary
            or the calendar year; the grants default to the company preference. Rejected when an active period
            already exists for the year (`VACATION_PERIOD_DUPLICATE`) or the employee has not completed one year
            of service at the start of the period (`VACATION_ELIGIBILITY_NOT_MET`). Manager-only.
            """)]
    public async Task<ActionResult<PersonnelFileVacationPeriodResponse>> AddVacationPeriod(
        Guid publicId,
        [FromBody] AddVacationPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileVacationPeriodCommand(
                publicId,
                new VacationPeriodInput(
                    request.PeriodYear,
                    request.UseAnniversary,
                    request.LegalDaysGranted,
                    request.BenefitDaysGranted,
                    request.GeneratesEnjoymentDays)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetVacationPeriodById),
            value => new { publicId, vacationPeriodPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/vacation-periods/{vacationPeriodPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileVacationPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Edit a vacation fund period's granted days",
        Description = """
            Updates the legal/benefit granted days of a period. Requires the `If-Match` header with the current
            `concurrencyToken`. Rejected when the period already has enjoyed days
            (`VACATION_PERIOD_HAS_CONSUMPTION`). Manager-only.
            """)]
    public async Task<ActionResult<PersonnelFileVacationPeriodResponse>> UpdateVacationPeriod(
        Guid publicId,
        Guid vacationPeriodPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateVacationPeriodGrantsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileVacationPeriodCommand(
                publicId,
                vacationPeriodPublicId,
                new VacationPeriodGrantsInput(request.LegalDaysGranted, request.BenefitDaysGranted),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/vacation-periods/{vacationPeriodPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a vacation fund period",
        Description = """
            Soft-deletes a vacation fund period. Requires the `If-Match` header with the period's current
            `concurrencyToken`. Rejected when the period already has enjoyed days
            (`VACATION_PERIOD_HAS_CONSUMPTION`). Manager-only.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteVacationPeriod(
        Guid publicId,
        Guid vacationPeriodPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileVacationPeriodCommand(publicId, vacationPeriodPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/vacation-fund")]
    [Produces("application/json")]
    [ProducesResponseType<VacationFundResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an employee's vacation fund detail",
        Description = """
            Returns the vacation fund detail: per active period the granted/enjoyed/pending days plus the
            financial provision (pending × daily × 1.30, D-25) — the daily salary is the base salary over 30.
            Access requires `ViewVacations` or the owner employee.
            """)]
    public async Task<ActionResult<VacationFundResponse>> GetVacationFund(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileVacationFundQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }
}
