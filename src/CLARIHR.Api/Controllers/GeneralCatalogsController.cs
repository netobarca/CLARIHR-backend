using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class GeneralCatalogsController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/general-catalogs/{catalogKey}")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelCatalogItemResponse>>> GetGeneralCatalogItems(
        Guid companyId,
        string catalogKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapCatalogKey(catalogKey, out var category))
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelCatalogItemResponse>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["catalogKey"] = [$"Catalog key '{catalogKey}' is not supported."]
                })));
        }

        var result = await queryDispatcher.SendAsync(
            new GetPersonnelCatalogItemsQuery(companyId, category),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/reference-catalogs/{catalogKey}")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>> GetReferenceCatalogItems(
        Guid companyId,
        string catalogKey,
        [FromQuery] string? parentCode,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapReferenceCatalogKey(catalogKey, out var category))
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["catalogKey"] = [$"Reference catalog key '{catalogKey}' is not supported."]
                })));
        }

        var result = await queryDispatcher.SendAsync(
            new GetPersonnelReferenceCatalogItemsQuery(companyId, category, parentCode),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private static bool TryMapCatalogKey(string key, out string category)
    {
        category = key.Trim().ToLowerInvariant() switch
        {
            "languages" => "CurriculumLanguage",
            "language-levels" => "CurriculumLanguageLevel",
            "training-types" => "CurriculumTrainingType",
            "duration-units" => "CurriculumDurationUnit",
            "reference-types" => "CurriculumReferenceType",
            "currencies" => "Currency",
            "countries" => "Country",
            "education-statuses" => "CurriculumEducationStatus",
            "education-study-types" => "CurriculumStudyType",
            "education-shifts" => "CurriculumShift",
            "education-modalities" => "CurriculumModality",
            "education-careers" => "CurriculumCareer",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(category);
    }

    private static bool TryMapReferenceCatalogKey(string key, out string category)
    {
        category = key.Trim().ToLowerInvariant() switch
        {
            "professions" => "Profession",
            "marital-statuses" => "MaritalStatus",
            "identification-types" => "IdentificationType",
            "kinships" => "Kinship",
            "departments" => "Department",
            "municipalities" => "Municipality",
            _ => string.Empty
        };
        return !string.IsNullOrWhiteSpace(category);
    }
}
