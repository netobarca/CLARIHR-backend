using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Domain.Locations;

namespace CLARIHR.Infrastructure.Locations;

internal sealed class LocationSeedService(
    ILocationHierarchyRepository hierarchyRepository,
    ILocationGroupRepository groupRepository,
    IUnitOfWork unitOfWork) : ILocationSeedService
{
    public async Task InitializeDefaultsAsync(Guid tenantId, string countryCode, CancellationToken cancellationToken)
    {
        if (await hierarchyRepository.GetConfigAsync(tenantId, cancellationToken) is not null)
        {
            return;
        }

        if (!CountryLocationTemplateRegistry.TryGet(countryCode, out var template))
        {
            throw new InvalidOperationException($"Unsupported location template country code '{countryCode}'.");
        }

        var config = LocationHierarchyConfig.Create(
            isMultiLevel: true,
            LocationValidationRules.DefaultGroupCode,
            LocationValidationRules.DefaultGroupName);
        config.SetTenantId(tenantId);
        hierarchyRepository.AddConfig(config);

        var countryLevel = LocationLevel.Create(
            levelOrder: 1,
            LocationValidationRules.CountryLevelDisplayName,
            isActive: true,
            isRequired: true,
            allowsWorkCenters: false);
        countryLevel.SetTenantId(tenantId);
        hierarchyRepository.AddLevel(countryLevel);

        var departmentLevel = LocationLevel.Create(
            levelOrder: 2,
            LocationValidationRules.DepartmentLevelDisplayName,
            isActive: true,
            isRequired: false,
            allowsWorkCenters: false);
        departmentLevel.SetTenantId(tenantId);
        hierarchyRepository.AddLevel(departmentLevel);

        var municipalityLevel = LocationLevel.Create(
            levelOrder: 3,
            LocationValidationRules.MunicipalityLevelDisplayName,
            isActive: true,
            isRequired: false,
            allowsWorkCenters: true);
        municipalityLevel.SetTenantId(tenantId);
        hierarchyRepository.AddLevel(municipalityLevel);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        await CreateGroupsAsync(template.Root, tenantId, parentId: null, levelOrder: 1, cancellationToken);
    }

    private async Task CreateGroupsAsync(
        CountryLocationNode node,
        Guid tenantId,
        long? parentId,
        int levelOrder,
        CancellationToken cancellationToken)
    {
        var group = LocationGroup.Create(
            levelOrder,
            node.Code,
            node.Name,
            parentId,
            node.Description,
            isDefault: false);
        group.SetTenantId(tenantId);
        groupRepository.Add(group);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var child in node.Children)
        {
            await CreateGroupsAsync(child, tenantId, group.Id, levelOrder + 1, cancellationToken);
        }
    }
}
