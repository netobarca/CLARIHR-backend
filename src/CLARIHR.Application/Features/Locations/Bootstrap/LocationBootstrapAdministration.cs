using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.Locations.Groups;
using CLARIHR.Application.Features.Locations.Hierarchy;
using CLARIHR.Domain.Locations;
using FluentValidation;

namespace CLARIHR.Application.Features.Locations.Bootstrap;

public sealed record LocationBootstrapTreeResponse(
    LocationHierarchyConfigResponse Hierarchy,
    IReadOnlyCollection<LocationLevelResponse> Levels,
    IReadOnlyCollection<LocationGroupTreeNodeResponse> Locations);

public sealed record LocationBootstrapTreeNodeInput(
    string Code,
    string Name,
    string? Description,
    IReadOnlyCollection<LocationBootstrapTreeNodeInput> Children);

public sealed record BootstrapLocationTreeCommand(
    Guid CompanyId,
    LocationBootstrapTreeNodeInput? Root) : ICommand<LocationBootstrapTreeResponse>;

internal sealed class BootstrapLocationTreeCommandValidator : AbstractValidator<BootstrapLocationTreeCommand>
{
    private const int MaxDepth = 3;

    public BootstrapLocationTreeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Root).NotNull();

        RuleFor(command => command).Custom((command, context) =>
        {
            if (command.Root is null)
            {
                return;
            }

            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ValidateNode(command.Root, depth: 1, path: "root", codes, context);
        });
    }

    private static void ValidateNode(
        LocationBootstrapTreeNodeInput node,
        int depth,
        string path,
        HashSet<string> codes,
        ValidationContext<BootstrapLocationTreeCommand> context)
    {
        if (depth > MaxDepth)
        {
            context.AddFailure(path, "Location tree depth cannot exceed 3 levels.");
            return;
        }

        if (string.IsNullOrWhiteSpace(node.Code))
        {
            context.AddFailure($"{path}.code", "Code is required.");
        }
        else
        {
            var cleanedCode = node.Code.Trim();
            if (cleanedCode.Length > 50)
            {
                context.AddFailure($"{path}.code", "Code must not exceed 50 characters.");
            }

            if (!LocationValidationRules.IsValidCode(cleanedCode))
            {
                context.AddFailure($"{path}.code", "Code format is invalid.");
            }

            if (!codes.Add(cleanedCode.ToUpperInvariant()))
            {
                context.AddFailure($"{path}.code", "Codes must be unique within the location tree.");
            }
        }

        if (string.IsNullOrWhiteSpace(node.Name))
        {
            context.AddFailure($"{path}.name", "Name is required.");
        }
        else if (node.Name.Trim().Length > 150)
        {
            context.AddFailure($"{path}.name", "Name must not exceed 150 characters.");
        }

        if (node.Description is not null && node.Description.Trim().Length > 500)
        {
            context.AddFailure($"{path}.description", "Description must not exceed 500 characters.");
        }

        var children = node.Children ?? [];
        if (depth == MaxDepth && children.Count > 0)
        {
            context.AddFailure(path, "Municipio nodes cannot contain children.");
        }

        var index = 0;
        foreach (var child in children)
        {
            ValidateNode(child, depth + 1, $"{path}.children[{index}]", codes, context);
            index++;
        }
    }
}

internal sealed class BootstrapLocationTreeCommandHandler(
    ILocationAuthorizationService authorizationService,
    ILocationHierarchyRepository hierarchyRepository,
    ILocationGroupRepository groupRepository,
    IWorkCenterRepository workCenterRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<BootstrapLocationTreeCommand, LocationBootstrapTreeResponse>
{
    private const string CountryLevelName = "Pais";
    private const string DepartmentLevelName = "Departamento";
    private const string MunicipalityLevelName = "Municipio";

    public async Task<Result<LocationBootstrapTreeResponse>> Handle(
        BootstrapLocationTreeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LocationBootstrapTreeResponse>.Failure(authorizationResult.Error);
        }

        if (command.Root is null)
        {
            return Result<LocationBootstrapTreeResponse>.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
            {
                ["root"] = ["Root is required."]
            }));
        }

        var config = await hierarchyRepository.GetConfigAsync(command.CompanyId, cancellationToken);
        var levels = await hierarchyRepository.GetLevelsForUpdateAsync(command.CompanyId, cancellationToken);
        var groups = await groupRepository.GetGroupsForUpdateAsync(command.CompanyId, cancellationToken);
        var hasAnyActiveWorkCenters = await workCenterRepository.HasAnyActiveWorkCentersAsync(command.CompanyId, cancellationToken);

        if (!IsSeedState(config, levels, groups) || hasAnyActiveWorkCenters)
        {
            return Result<LocationBootstrapTreeResponse>.Failure(LocationErrors.TreeBootstrapNotAllowed);
        }

        var seedGroup = groups[0];
        foreach (var node in Flatten(command.Root))
        {
            if (await groupRepository.CodeExistsAsync(
                    command.CompanyId,
                    node.Code.Trim().ToUpperInvariant(),
                    seedGroup.Id,
                    cancellationToken))
            {
                return Result<LocationBootstrapTreeResponse>.Failure(LocationErrors.GroupCodeConflict);
            }
        }

        var beforeTree = LocationGroupTreeBuilder.Build(await groupRepository.GetTreeAsync(command.CompanyId, cancellationToken));
        var before = new LocationBootstrapTreeResponse(
            LocationHierarchyMapper.Map(config!),
            levels.Select(LocationHierarchyMapper.Map).ToArray(),
            beforeTree);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            config!.Update(isMultiLevel: true, command.Root.Code, command.Root.Name);

            var seedLevel = levels[0];
            seedLevel.Update(CountryLevelName, isActive: true, isRequired: true, allowsWorkCenters: false);

            var departmentLevel = LocationLevel.Create(
                levelOrder: 2,
                displayName: DepartmentLevelName,
                isActive: true,
                isRequired: false,
                allowsWorkCenters: false);
            departmentLevel.SetTenantId(command.CompanyId);
            hierarchyRepository.AddLevel(departmentLevel);

            var municipalityLevel = LocationLevel.Create(
                levelOrder: 3,
                displayName: MunicipalityLevelName,
                isActive: true,
                isRequired: false,
                allowsWorkCenters: true);
            municipalityLevel.SetTenantId(command.CompanyId);
            hierarchyRepository.AddLevel(municipalityLevel);

            groupRepository.Remove(seedGroup);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await CreateTreeAsync(
                command.CompanyId,
                command.Root,
                parentId: null,
                depth: 1,
                cancellationToken);

            var hierarchy = LocationHierarchyMapper.Map(config);
            var persistedLevels = await hierarchyRepository.GetLevelsAsync(command.CompanyId, cancellationToken);
            var persistedTree = LocationGroupTreeBuilder.Build(await groupRepository.GetTreeAsync(command.CompanyId, cancellationToken));
            var response = new LocationBootstrapTreeResponse(
                hierarchy,
                persistedLevels.Select(LocationHierarchyMapper.Map).ToArray(),
                persistedTree);

            await auditService.LogAsync(
                new AuditLogEntry(
                    "LOCATION_TREE_BOOTSTRAPPED",
                    "LocationHierarchy",
                    config.PublicId,
                    LocationPermissionCodes.ResourceKey,
                    AuditActions.Update,
                    $"Bootstrapped location tree for tenant {command.CompanyId}.",
                    Before: before,
                    After: response),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LocationBootstrapTreeResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task CreateTreeAsync(
        Guid tenantId,
        LocationBootstrapTreeNodeInput node,
        long? parentId,
        int depth,
        CancellationToken cancellationToken)
    {
        var group = LocationGroup.Create(
            levelOrder: depth,
            code: node.Code,
            name: node.Name,
            parentId: parentId,
            description: node.Description,
            isDefault: depth == 1);
        group.SetTenantId(tenantId);
        groupRepository.Add(group);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var child in node.Children ?? [])
        {
            await CreateTreeAsync(tenantId, child, group.Id, depth + 1, cancellationToken);
        }
    }

    private static bool IsSeedState(
        LocationHierarchyConfig? config,
        IReadOnlyList<LocationLevel> levels,
        IReadOnlyList<LocationGroup> groups)
    {
        if (config is null ||
            config.IsMultiLevel ||
            !config.DefaultGroupCode.Equals(LocationValidationRules.DefaultGroupCode, StringComparison.OrdinalIgnoreCase) ||
            !config.DefaultGroupName.Equals(LocationValidationRules.DefaultGroupName, StringComparison.Ordinal))
        {
            return false;
        }

        if (levels.Count != 1 || groups.Count != 1)
        {
            return false;
        }

        var level = levels[0];
        if (level.LevelOrder != 1 ||
            !level.DisplayName.Equals(LocationValidationRules.GeneralLevelDisplayName, StringComparison.Ordinal) ||
            !level.IsActive ||
            !level.IsRequired ||
            !level.AllowsWorkCenters)
        {
            return false;
        }

        var group = groups[0];
        return group.LevelOrder == 1 &&
               group.ParentId is null &&
               group.IsActive &&
               group.IsDefault &&
               group.Code.Equals(LocationValidationRules.DefaultGroupCode, StringComparison.OrdinalIgnoreCase) &&
               group.Name.Equals(LocationValidationRules.DefaultGroupName, StringComparison.Ordinal);
    }

    private static IEnumerable<LocationBootstrapTreeNodeInput> Flatten(LocationBootstrapTreeNodeInput root)
    {
        yield return root;

        foreach (var child in root.Children ?? [])
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }
}
