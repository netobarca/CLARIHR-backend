using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.SystemCatalogs;
using CLARIHR.Backoffice.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Backoffice.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform/system-catalogs")]
public sealed class SystemCatalogsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    [HttpGet("{catalogKey}")]
    [ProducesResponseType<PagedResponse<SystemCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<SystemCatalogItemResponse>>> Search(
        string catalogKey,
        [FromQuery] string countryCode,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] Guid? parentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapCatalogKey(catalogKey, out var catalogType))
        {
            return this.ToActionResult(Result<PagedResponse<SystemCatalogItemResponse>>.Failure(UnsupportedCatalogKey(catalogKey)));
        }

        var result = await queryDispatcher.SendAsync(
            new SearchSystemCatalogItemsQuery(catalogType, countryCode, isActive, search, parentId, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{catalogKey}/{id:guid}")]
    [ProducesResponseType<SystemCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SystemCatalogItemResponse>> GetById(
        string catalogKey,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapCatalogKey(catalogKey, out var catalogType))
        {
            return this.ToActionResult(Result<SystemCatalogItemResponse>.Failure(UnsupportedCatalogKey(catalogKey)));
        }

        var result = await queryDispatcher.SendAsync(new GetSystemCatalogItemByIdQuery(catalogType, id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{catalogKey}")]
    [ProducesResponseType<SystemCatalogItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SystemCatalogItemResponse>> Create(
        string catalogKey,
        [FromBody] UpsertSystemCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapCatalogKey(catalogKey, out var catalogType))
        {
            return this.ToActionResult(Result<SystemCatalogItemResponse>.Failure(UnsupportedCatalogKey(catalogKey)));
        }

        var result = await commandDispatcher.SendAsync(
            new CreateSystemCatalogItemCommand(
                catalogType,
                request.CountryCode,
                request.Code,
                request.Name,
                request.SortOrder,
                request.ParentId),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<SystemCatalogItemResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{catalogKey}/{id:guid}")]
    [ProducesResponseType<SystemCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SystemCatalogItemResponse>> Update(
        string catalogKey,
        Guid id,
        [FromBody] UpdateSystemCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapCatalogKey(catalogKey, out var catalogType))
        {
            return this.ToActionResult(Result<SystemCatalogItemResponse>.Failure(UnsupportedCatalogKey(catalogKey)));
        }

        var result = await commandDispatcher.SendAsync(
            new UpdateSystemCatalogItemCommand(
                catalogType,
                id,
                request.CountryCode,
                request.Code,
                request.Name,
                request.SortOrder,
                request.ConcurrencyToken,
                request.ParentId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{catalogKey}/{id:guid}/activate")]
    [ProducesResponseType<SystemCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SystemCatalogItemResponse>> Activate(
        string catalogKey,
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapCatalogKey(catalogKey, out var catalogType))
        {
            return this.ToActionResult(Result<SystemCatalogItemResponse>.Failure(UnsupportedCatalogKey(catalogKey)));
        }

        var result = await commandDispatcher.SendAsync(
            new ActivateSystemCatalogItemCommand(catalogType, id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{catalogKey}/{id:guid}/inactivate")]
    [ProducesResponseType<SystemCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SystemCatalogItemResponse>> Inactivate(
        string catalogKey,
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapCatalogKey(catalogKey, out var catalogType))
        {
            return this.ToActionResult(Result<SystemCatalogItemResponse>.Failure(UnsupportedCatalogKey(catalogKey)));
        }

        var result = await commandDispatcher.SendAsync(
            new InactivateSystemCatalogItemCommand(catalogType, id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record UpsertSystemCatalogItemRequest(
        string CountryCode,
        string Code,
        string Name,
        int SortOrder,
        Guid? ParentId = null);

    public sealed record UpdateSystemCatalogItemRequest(
        string CountryCode,
        string Code,
        string Name,
        int SortOrder,
        Guid ConcurrencyToken,
        Guid? ParentId = null);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);

    private static bool TryMapCatalogKey(string catalogKey, out SystemCatalogType catalogType)
    {
        switch (catalogKey.Trim().ToLowerInvariant())
        {
            case "languages":
                catalogType = SystemCatalogType.Language;
                return true;
            case "language-levels":
                catalogType = SystemCatalogType.LanguageLevel;
                return true;
            case "training-types":
                catalogType = SystemCatalogType.TrainingType;
                return true;
            case "duration-units":
                catalogType = SystemCatalogType.DurationUnit;
                return true;
            case "reference-types":
                catalogType = SystemCatalogType.ReferenceType;
                return true;
            case "currencies":
                catalogType = SystemCatalogType.Currency;
                return true;
            case "education-statuses":
                catalogType = SystemCatalogType.EducationStatus;
                return true;
            case "education-study-types":
                catalogType = SystemCatalogType.EducationStudyType;
                return true;
            case "education-careers":
                catalogType = SystemCatalogType.EducationCareer;
                return true;
            case "education-shifts":
                catalogType = SystemCatalogType.EducationShift;
                return true;
            case "education-modalities":
                catalogType = SystemCatalogType.EducationModality;
                return true;
            case "identification-types":
                catalogType = SystemCatalogType.IdentificationType;
                return true;
            case "professions":
                catalogType = SystemCatalogType.Profession;
                return true;
            case "marital-statuses":
                catalogType = SystemCatalogType.MaritalStatus;
                return true;
            case "kinships":
                catalogType = SystemCatalogType.Kinship;
                return true;
            case "departments":
                catalogType = SystemCatalogType.Department;
                return true;
            case "municipalities":
                catalogType = SystemCatalogType.Municipality;
                return true;
            case "personal-titles":
                catalogType = SystemCatalogType.PersonalTitle;
                return true;
            case "address-types":
                catalogType = SystemCatalogType.AddressType;
                return true;
            case "hobbies":
                catalogType = SystemCatalogType.Hobby;
                return true;
            case "associations":
                catalogType = SystemCatalogType.Association;
                return true;
            case "additional-benefit-types":
                catalogType = SystemCatalogType.AdditionalBenefitType;
                return true;
            default:
                catalogType = default;
                return false;
        }
    }

    private static Error UnsupportedCatalogKey(string catalogKey) =>
        ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            ["catalogKey"] = [$"Catalog key '{catalogKey}' is not supported."]
        });
}
