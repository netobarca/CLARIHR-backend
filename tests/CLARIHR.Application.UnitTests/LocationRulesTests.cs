using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.Locations.Groups;
using CLARIHR.Application.Features.Locations.Hierarchy;
using CLARIHR.Application.Features.Locations.WorkCenters;
using CLARIHR.Application.Features.Locations.WorkCenterTypes;
using CLARIHR.Domain.Locations;
using CLARIHR.Infrastructure.Locations;

namespace CLARIHR.Application.UnitTests;

public sealed class LocationRulesTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task WorkCenterRules_ValidateAssignmentAsync_WhenLevelDoesNotAllowWorkCenters_ShouldReturnConflict()
    {
        var hierarchyRepository = new TestLocationHierarchyRepository(
            levels:
            [
                CreateLevel(1, allowsWorkCenters: false)
            ]);

        var workCenterType = WorkCenterType.Create(
            code: "AGENCY",
            name: "Agency",
            requiresAddress: false,
            requiresGeo: false,
            allowsBiometric: true);
        workCenterType.SetTenantId(TenantId);

        var group = CreateGroup(levelOrder: 1, code: "GENERAL", isDefault: true);

        var result = await WorkCenterRules.ValidateAssignmentAsync(
            hierarchyRepository,
            TenantId,
            workCenterType,
            group,
            address: "San Salvador",
            geoLat: null,
            geoLong: null,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER", result.Error.Code);
    }

    [Fact]
    public async Task WorkCenterRules_ValidateAssignmentAsync_WhenAddressIsRequired_ShouldReturnValidationError()
    {
        var hierarchyRepository = new TestLocationHierarchyRepository(
            levels:
            [
                CreateLevel(1, allowsWorkCenters: true)
            ]);

        var workCenterType = WorkCenterType.Create(
            code: "AGENCY",
            name: "Agency",
            requiresAddress: true,
            requiresGeo: false,
            allowsBiometric: true);
        workCenterType.SetTenantId(TenantId);

        var group = CreateGroup(levelOrder: 1, code: "GENERAL", isDefault: true);

        var result = await WorkCenterRules.ValidateAssignmentAsync(
            hierarchyRepository,
            TenantId,
            workCenterType,
            group,
            address: null,
            geoLat: null,
            geoLong: null,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("WORK_CENTER_ADDRESS_REQUIRED", result.Error.Code);
    }

    [Fact]
    public async Task LocationDependencyPolicy_WhenGroupHasActiveWorkCenters_ShouldReturnConflict()
    {
        var group = CreateGroup(levelOrder: 1, code: "WEST", isDefault: false);
        var groupRepository = new TestLocationGroupRepository(group)
        {
            HasActiveWorkCentersValue = true
        };

        var policy = new LocationDependencyPolicy(groupRepository, new TestWorkCenterTypeRepository());

        var result = await policy.CanInactivateLocationGroupAsync(group.PublicId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("LOCATION_GROUP_HAS_ACTIVE_WORK_CENTERS", result.Error.Code);
    }

    [Fact]
    public async Task LocationDependencyPolicy_WhenTypeIsInUse_ShouldReturnConflict()
    {
        var workCenterType = WorkCenterType.Create(
            code: "AGENCY",
            name: "Agency",
            requiresAddress: false,
            requiresGeo: false,
            allowsBiometric: true);
        workCenterType.SetTenantId(TenantId);

        var typeRepository = new TestWorkCenterTypeRepository(workCenterType)
        {
            HasActiveWorkCentersValue = true
        };

        var policy = new LocationDependencyPolicy(new TestLocationGroupRepository(), typeRepository);

        var result = await policy.CanInactivateWorkCenterTypeAsync(workCenterType.PublicId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("WORK_CENTER_TYPE_IN_USE", result.Error.Code);
    }

    private static LocationLevel CreateLevel(int levelOrder, bool allowsWorkCenters)
    {
        var level = LocationLevel.Create(
            levelOrder,
            displayName: $"Level {levelOrder}",
            isActive: true,
            isRequired: levelOrder == 1,
            allowsWorkCenters: allowsWorkCenters);
        level.SetTenantId(TenantId);
        return level;
    }

    private static LocationGroup CreateGroup(int levelOrder, string code, bool isDefault)
    {
        var group = LocationGroup.Create(
            levelOrder,
            code,
            name: code,
            parentId: null,
            description: null,
            isDefault: isDefault);
        group.SetTenantId(TenantId);
        return group;
    }

    private sealed class TestLocationHierarchyRepository(IReadOnlyList<LocationLevel>? levels = null) : ILocationHierarchyRepository
    {
        private readonly IReadOnlyList<LocationLevel> _levels = levels ?? [];

        public void AddConfig(LocationHierarchyConfig config) => throw new NotSupportedException();
        public void AddLevel(LocationLevel level) => throw new NotSupportedException();
        public Task<LocationHierarchyConfig?> GetConfigAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<LocationHierarchyConfig?>(null);
        public Task<LocationHierarchyConfig?> GetConfigByIdAsync(Guid configId, CancellationToken cancellationToken) => Task.FromResult<LocationHierarchyConfig?>(null);
        public Task<bool> ConfigExistsOutsideTenantAsync(Guid configId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<LocationLevel>> GetLevelsAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(_levels);
        public Task<IReadOnlyList<LocationLevel>> GetLevelsForUpdateAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(_levels);
        public Task<LocationLevel?> GetLevelByIdAsync(Guid levelId, CancellationToken cancellationToken) => Task.FromResult<LocationLevel?>(null);
        public Task<bool> LevelExistsOutsideTenantAsync(Guid levelId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> LevelOrderExistsAsync(Guid tenantId, int levelOrder, long? excludingLevelId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<int> CountActiveLevelsAsync(Guid tenantId, long? excludingLevelId, CancellationToken cancellationToken) => Task.FromResult(_levels.Count(static level => level.IsActive));
        public Task<bool> HasAnyActiveWorkCenterLevelAsync(Guid tenantId, long? excludingLevelId, CancellationToken cancellationToken) => Task.FromResult(_levels.Any(static level => level.IsActive && level.AllowsWorkCenters));
        public Task<int?> GetHighestActiveLevelOrderAsync(Guid tenantId, long? excludingLevelId, CancellationToken cancellationToken) => Task.FromResult(_levels.Where(static level => level.IsActive).Select(static level => (int?)level.LevelOrder).DefaultIfEmpty().Max());
    }

    private sealed class TestLocationGroupRepository(LocationGroup? group = null) : ILocationGroupRepository
    {
        private readonly LocationGroup? _group = group;

        public bool HasActiveChildrenValue { get; init; }
        public bool HasActiveWorkCentersValue { get; init; }

        public void Add(LocationGroup group) => throw new NotSupportedException();
        public void Remove(LocationGroup group) => throw new NotSupportedException();
        public Task<LocationGroup?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken) => Task.FromResult(_group is not null && _group.PublicId == groupId ? _group : null);
        public Task<bool> ExistsOutsideTenantAsync(Guid groupId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<LocationGroup?> GetByIdIgnoreFiltersAsync(Guid groupId, CancellationToken cancellationToken) => Task.FromResult<LocationGroup?>(null);
        public Task<IReadOnlyList<LocationGroup>> GetGroupsForUpdateAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocationGroup>>(_group is null ? [] : [_group]);
        public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingGroupId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<LocationGroupTreeNodeData>> GetTreeAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocationGroupTreeNodeData>>([]);
        public Task<PagedResponse<LocationGroupResponse>> SearchAsync(Guid tenantId, int? levelOrder, bool? isActive, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> HasActiveChildrenAsync(long groupId, CancellationToken cancellationToken) => Task.FromResult(HasActiveChildrenValue);
        public Task<bool> HasActiveGroupsAtLevelAsync(Guid tenantId, int levelOrder, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> IsDescendantAsync(long ancestorGroupId, long candidateDescendantId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> HasActiveWorkCentersAsync(long groupId, CancellationToken cancellationToken) => Task.FromResult(HasActiveWorkCentersValue);
    }

    private sealed class TestWorkCenterTypeRepository(WorkCenterType? workCenterType = null) : IWorkCenterTypeRepository
    {
        private readonly WorkCenterType? _workCenterType = workCenterType;

        public bool HasActiveWorkCentersValue { get; init; }

        public void Add(WorkCenterType workCenterType) => throw new NotSupportedException();
        public Task<WorkCenterType?> GetByIdAsync(Guid workCenterTypeId, CancellationToken cancellationToken) => Task.FromResult(_workCenterType is not null && _workCenterType.PublicId == workCenterTypeId ? _workCenterType : null);
        public Task<bool> ExistsOutsideTenantAsync(Guid workCenterTypeId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingWorkCenterTypeId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<PagedResponse<WorkCenterTypeResponse>> SearchAsync(Guid tenantId, bool? isActive, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkCentersAsync(long workCenterTypeId, CancellationToken cancellationToken) => Task.FromResult(HasActiveWorkCentersValue);
    }
}
