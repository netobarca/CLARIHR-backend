using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Domain.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account/companies")]
public sealed class AccountCompaniesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AccountCompanySummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<AccountCompanySummaryResponse>>> List(
        [FromQuery] CompanyStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOwnedCompaniesQuery(status, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{companyId:guid}")]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> GetById(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOwnedCompanyByIdQuery(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> Create(
        [FromBody] CreateAccountCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new CreateAccountCompanyCommand(request.Name), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<AccountCompanyDetailResponse>.Failure(result.Error));
        }

        return CreatedAtAction(nameof(GetById), new { companyId = result.Value.CompanyId }, result.Value);
    }

    [HttpPut("{companyId:guid}")]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> Update(
        Guid companyId,
        [FromBody] UpdateAccountCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateAccountCompanyCommand(companyId, request.Name),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{companyId:guid}/archive")]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> Archive(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new ArchiveAccountCompanyCommand(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{companyId:guid}/reactivate")]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> Reactivate(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new ReactivateAccountCompanyCommand(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{companyId:guid}/switch")]
    [ProducesResponseType<SwitchActiveCompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SwitchActiveCompanyResponse>> Switch(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new SwitchActiveCompanyCommand(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    public sealed record CreateAccountCompanyRequest(string Name);

    public sealed record UpdateAccountCompanyRequest(string Name);
}
