using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PositionDescriptionCatalogs;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PositionDescriptionCatalogsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/position-function-types")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchPositionFunctionTypes(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.PositionFunctionType, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/position-function-types/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetPositionFunctionTypeById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/position-function-types")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreatePositionFunctionType(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.PositionFunctionType, request, cancellationToken);

    [HttpPut("api/v1/position-function-types/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdatePositionFunctionType(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/position-function-types/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivatePositionFunctionType(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/position-function-types/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivatePositionFunctionType(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/position-contract-types")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchPositionContractTypes(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.PositionContractType, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/position-contract-types/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetPositionContractTypeById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/position-contract-types")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreatePositionContractType(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.PositionContractType, request, cancellationToken);

    [HttpPut("api/v1/position-contract-types/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdatePositionContractType(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/position-contract-types/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivatePositionContractType(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/position-contract-types/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivatePositionContractType(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/strategic-objectives")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchStrategicObjectives(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.StrategicObjective, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/strategic-objectives/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetStrategicObjectiveById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/strategic-objectives")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateStrategicObjective(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.StrategicObjective, request, cancellationToken);

    [HttpPut("api/v1/strategic-objectives/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateStrategicObjective(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/strategic-objectives/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateStrategicObjective(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/strategic-objectives/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateStrategicObjective(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/frequencies")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchFrequencies(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.Frequency, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/frequencies/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetFrequencyById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/frequencies")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateFrequency(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.Frequency, request, cancellationToken);

    [HttpPut("api/v1/frequencies/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateFrequency(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/frequencies/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateFrequency(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/frequencies/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateFrequency(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/requirement-types")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchRequirementTypes(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.RequirementType, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/requirement-types/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetRequirementTypeById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/requirement-types")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateRequirementType(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.RequirementType, request, cancellationToken);

    [HttpPut("api/v1/requirement-types/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateRequirementType(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/requirement-types/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateRequirementType(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/requirement-types/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateRequirementType(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/requirements")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchRequirements(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.Requirement, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/requirements/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetRequirementById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/requirements")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateRequirement(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.Requirement, request, cancellationToken);

    [HttpPut("api/v1/requirements/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateRequirement(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/requirements/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateRequirement(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/requirements/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateRequirement(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/general-functions")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchGeneralFunctions(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.GeneralFunction, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/general-functions/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetGeneralFunctionById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/general-functions")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateGeneralFunction(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.GeneralFunction, request, cancellationToken);

    [HttpPut("api/v1/general-functions/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateGeneralFunction(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/general-functions/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateGeneralFunction(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/general-functions/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateGeneralFunction(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/salary-classes")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchSalaryClasses(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.SalaryClass, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/salary-classes/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetSalaryClassById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/salary-classes")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateSalaryClass(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.SalaryClass, request, cancellationToken);

    [HttpPut("api/v1/salary-classes/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateSalaryClass(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/salary-classes/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateSalaryClass(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/salary-classes/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateSalaryClass(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/work-equipments")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchWorkEquipments(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.WorkEquipment, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/work-equipments/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetWorkEquipmentById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/work-equipments")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateWorkEquipment(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.WorkEquipment, request, cancellationToken);

    [HttpPut("api/v1/work-equipments/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateWorkEquipment(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/work-equipments/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateWorkEquipment(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/work-equipments/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateWorkEquipment(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/responsibilities-catalog")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchResponsibilitiesCatalog(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.Responsibility, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/responsibilities-catalog/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetResponsibilityById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/responsibilities-catalog")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateResponsibility(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.Responsibility, request, cancellationToken);

    [HttpPut("api/v1/responsibilities-catalog/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateResponsibility(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/responsibilities-catalog/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateResponsibility(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/responsibilities-catalog/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateResponsibility(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/benefits-catalog")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchBenefitsCatalog(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.Benefit, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/benefits-catalog/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetBenefitById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/benefits-catalog")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateBenefit(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.Benefit, request, cancellationToken);

    [HttpPut("api/v1/benefits-catalog/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateBenefit(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/benefits-catalog/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateBenefit(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/benefits-catalog/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateBenefit(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/work-condition-types")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchWorkConditionTypes(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.WorkConditionType, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/work-condition-types/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetWorkConditionTypeById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/work-condition-types")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateWorkConditionType(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.WorkConditionType, request, cancellationToken);

    [HttpPut("api/v1/work-condition-types/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateWorkConditionType(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/work-condition-types/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateWorkConditionType(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/work-condition-types/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateWorkConditionType(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/work-conditions")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchWorkConditions(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        SearchSimpleCatalog(companyId, PositionDescriptionCatalogType.WorkCondition, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/work-conditions/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetWorkConditionById(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSimpleCatalogItem(id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/work-conditions")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateWorkCondition(
        Guid companyId,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleCatalogItem(companyId, PositionDescriptionCatalogType.WorkCondition, request, cancellationToken);

    [HttpPut("api/v1/work-conditions/{id:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateWorkCondition(
        Guid id,
        [FromBody] UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/work-conditions/{id:guid}/activate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateWorkCondition(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        ActivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpPatch("api/v1/work-conditions/{id:guid}/inactivate")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateWorkCondition(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        InactivateSimpleCatalogItem(id, request, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/position-category-classifications")]
    [ProducesResponseType<PagedResponse<PositionCategoryClassificationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PositionCategoryClassificationResponse>>> SearchPositionCategoryClassifications(
        Guid companyId,
        [FromQuery] Guid? positionFunctionTypeId,
        [FromQuery] Guid? positionContractTypeId,
        [FromQuery] Guid? orgUnitTypeId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPositionCategoryClassificationsQuery(
                companyId,
                positionFunctionTypeId,
                positionContractTypeId,
                orgUnitTypeId,
                isActive,
                search,
                page,
                pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/position-category-classifications/{id:guid}")]
    [ProducesResponseType<PositionCategoryClassificationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PositionCategoryClassificationResponse>> GetPositionCategoryClassificationById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPositionCategoryClassificationByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/position-category-classifications")]
    [ProducesResponseType<PositionCategoryClassificationResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PositionCategoryClassificationResponse>> CreatePositionCategoryClassification(
        Guid companyId,
        [FromBody] UpsertPositionCategoryClassificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePositionCategoryClassificationCommand(
                companyId,
                request.Code,
                request.Name,
                request.Description,
                request.PositionFunctionTypeId,
                request.PositionContractTypeId,
                request.OrgUnitTypeId,
                request.SortOrder),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PositionCategoryClassificationResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/position-category-classifications/{id:guid}")]
    [ProducesResponseType<PositionCategoryClassificationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PositionCategoryClassificationResponse>> UpdatePositionCategoryClassification(
        Guid id,
        [FromBody] UpdatePositionCategoryClassificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePositionCategoryClassificationCommand(
                id,
                request.Code,
                request.Name,
                request.Description,
                request.PositionFunctionTypeId,
                request.PositionContractTypeId,
                request.OrgUnitTypeId,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/position-category-classifications/{id:guid}/activate")]
    [ProducesResponseType<PositionCategoryClassificationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PositionCategoryClassificationResponse>> ActivatePositionCategoryClassification(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivatePositionCategoryClassificationCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/position-category-classifications/{id:guid}/inactivate")]
    [ProducesResponseType<PositionCategoryClassificationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PositionCategoryClassificationResponse>> InactivatePositionCategoryClassification(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivatePositionCategoryClassificationCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/position-categories")]
    [ProducesResponseType<PagedResponse<PositionCategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PositionCategoryResponse>>> SearchPositionCategories(
        Guid companyId,
        [FromQuery] Guid? classificationId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPositionCategoriesQuery(companyId, classificationId, isActive, search, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/position-categories/{id:guid}")]
    [ProducesResponseType<PositionCategoryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PositionCategoryResponse>> GetPositionCategoryById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPositionCategoryByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/position-categories")]
    [ProducesResponseType<PositionCategoryResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PositionCategoryResponse>> CreatePositionCategory(
        Guid companyId,
        [FromBody] UpsertPositionCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePositionCategoryCommand(
                companyId,
                request.Code,
                request.Name,
                request.Description,
                request.ClassificationId,
                request.SortOrder),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PositionCategoryResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/position-categories/{id:guid}")]
    [ProducesResponseType<PositionCategoryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PositionCategoryResponse>> UpdatePositionCategory(
        Guid id,
        [FromBody] UpdatePositionCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePositionCategoryCommand(
                id,
                request.Code,
                request.Name,
                request.Description,
                request.ClassificationId,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/position-categories/{id:guid}/activate")]
    [ProducesResponseType<PositionCategoryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PositionCategoryResponse>> ActivatePositionCategory(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivatePositionCategoryCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/position-categories/{id:guid}/inactivate")]
    [ProducesResponseType<PositionCategoryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PositionCategoryResponse>> InactivatePositionCategory(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivatePositionCategoryCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private async Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> SearchSimpleCatalog(
        Guid companyId,
        PositionDescriptionCatalogType catalogType,
        bool? isActive,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPositionDescriptionCatalogItemsQuery(companyId, catalogType, isActive, search, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private async Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetSimpleCatalogItem(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new GetPositionDescriptionCatalogItemByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    private async Task<ActionResult<PositionDescriptionCatalogItemResponse>> CreateSimpleCatalogItem(
        Guid companyId,
        PositionDescriptionCatalogType catalogType,
        UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePositionDescriptionCatalogItemCommand(
                companyId,
                catalogType,
                request.Code,
                request.Name,
                request.Description,
                request.SortOrder),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PositionDescriptionCatalogItemResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    private async Task<ActionResult<PositionDescriptionCatalogItemResponse>> UpdateSimpleCatalogItem(
        Guid id,
        UpdatePositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePositionDescriptionCatalogItemCommand(
                id,
                request.Code,
                request.Name,
                request.Description,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private async Task<ActionResult<PositionDescriptionCatalogItemResponse>> ActivateSimpleCatalogItem(
        Guid id,
        ConcurrencyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivatePositionDescriptionCatalogItemCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private async Task<ActionResult<PositionDescriptionCatalogItemResponse>> InactivateSimpleCatalogItem(
        Guid id,
        ConcurrencyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivatePositionDescriptionCatalogItemCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record UpsertPositionDescriptionCatalogItemRequest(
        string Code,
        string Name,
        string? Description,
        int SortOrder);

    public sealed record UpdatePositionDescriptionCatalogItemRequest(
        string Code,
        string Name,
        string? Description,
        int SortOrder,
        Guid ConcurrencyToken);

    public sealed record UpsertPositionCategoryClassificationRequest(
        string Code,
        string Name,
        string? Description,
        Guid PositionFunctionTypeId,
        Guid PositionContractTypeId,
        Guid OrgUnitTypeId,
        int SortOrder);

    public sealed record UpdatePositionCategoryClassificationRequest(
        string Code,
        string Name,
        string? Description,
        Guid PositionFunctionTypeId,
        Guid PositionContractTypeId,
        Guid OrgUnitTypeId,
        int SortOrder,
        Guid ConcurrencyToken);

    public sealed record UpsertPositionCategoryRequest(
        string Code,
        string Name,
        string? Description,
        Guid ClassificationId,
        int SortOrder);

    public sealed record UpdatePositionCategoryRequest(
        string Code,
        string Name,
        string? Description,
        Guid ClassificationId,
        int SortOrder,
        Guid ConcurrencyToken);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}
