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
    public async Task InitializeDefaultsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (await hierarchyRepository.GetConfigAsync(tenantId, cancellationToken) is not null)
        {
            return;
        }

        var config = LocationHierarchyConfig.Create(
            isMultiLevel: false,
            LocationValidationRules.DefaultGroupCode,
            LocationValidationRules.DefaultGroupName);
        config.SetTenantId(tenantId);
        hierarchyRepository.AddConfig(config);

        var level = LocationLevel.Create(
            levelOrder: 1,
            LocationValidationRules.GeneralLevelDisplayName,
            isActive: true,
            isRequired: true,
            allowsWorkCenters: true);
        level.SetTenantId(tenantId);
        hierarchyRepository.AddLevel(level);

        var group = LocationGroup.Create(
            levelOrder: 1,
            LocationValidationRules.DefaultGroupCode,
            LocationValidationRules.DefaultGroupName,
            parentId: null,
            description: "Default location group.",
            isDefault: true);
        group.SetTenantId(tenantId);
        groupRepository.Add(group);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
