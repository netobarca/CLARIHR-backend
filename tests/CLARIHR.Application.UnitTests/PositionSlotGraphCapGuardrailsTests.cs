using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.PositionSlots;

namespace CLARIHR.Application.UnitTests;

// §PS4 guardrail: GetPositionSlotGraphQueryHandler must reject before loading
// the wide 8-table join when the tenant slot count exceeds MaxNodes.
// Locks the ordering: count → fail-fast → fetch — so a future refactor that
// fetches first cannot silently regress the cap into post-load defense only.
public sealed class PositionSlotGraphCapGuardrailsTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_WhenSlotCountExceedsMaxNodes_ShouldFail_WithoutLoadingGraph()
    {
        var repository = new CountingRepository(slotCount: 5_001);
        var handler = new GetPositionSlotGraphQueryHandler(new AllowAccess(), repository);

        var result = await handler.Handle(
            new GetPositionSlotGraphQuery(
                CompanyId: TenantId,
                RootId: null,
                Depth: null,
                IncludeFunctional: true,
                MaxNodes: 5_000),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ReportPolicyErrors.ExportLimitExceeded.Code, result.Error.Code);
        Assert.False(repository.GraphLoaded);
    }

    [Fact]
    public async Task Handle_WhenSlotCountWithinMaxNodes_ShouldLoadGraph()
    {
        var repository = new CountingRepository(slotCount: 10);
        var handler = new GetPositionSlotGraphQueryHandler(new AllowAccess(), repository);

        var result = await handler.Handle(
            new GetPositionSlotGraphQuery(
                CompanyId: TenantId,
                RootId: null,
                Depth: null,
                IncludeFunctional: true,
                MaxNodes: 5_000),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(repository.GraphLoaded);
    }

    [Fact]
    public async Task Handle_AtMaxNodesBoundary_ShouldLoadGraph()
    {
        var repository = new CountingRepository(slotCount: 5_000);
        var handler = new GetPositionSlotGraphQueryHandler(new AllowAccess(), repository);

        var result = await handler.Handle(
            new GetPositionSlotGraphQuery(
                CompanyId: TenantId,
                RootId: null,
                Depth: null,
                IncludeFunctional: true,
                MaxNodes: 5_000),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(repository.GraphLoaded);
    }

    [Fact]
    public void Validator_WhenMaxNodesIsZeroOrNegative_ShouldFailValidation()
    {
        var validator = new GetPositionSlotGraphQueryValidator();
        var invalid = new GetPositionSlotGraphQuery(TenantId, null, null, true, MaxNodes: 0);

        var result = validator.Validate(invalid);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetPositionSlotGraphQuery.MaxNodes));
    }

    private sealed class AllowAccess : IPositionSlotAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result<PositionSlotAccess>> EvaluateAccessAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Result<PositionSlotAccess>.Success(new PositionSlotAccess(CanRead: true, CanManage: true)));

        public Error TenantMismatch(RbacPermissionAction action) => PositionSlotErrors.Forbidden;
    }

    private sealed class CountingRepository(int slotCount) : IPositionSlotRepository
    {
        public bool GraphLoaded { get; private set; }

        public Task<int> CountSlotsAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(slotCount);

        public Task<IReadOnlyCollection<PositionSlotGraphNodeData>> GetGraphNodesAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            GraphLoaded = true;
            return Task.FromResult<IReadOnlyCollection<PositionSlotGraphNodeData>>(Array.Empty<PositionSlotGraphNodeData>());
        }

        public Task<IReadOnlyCollection<PositionSlotDependencyAdjacency>> GetDependencyAdjacencyAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<PositionSlotDependencyAdjacency>>(Array.Empty<PositionSlotDependencyAdjacency>());

        public Task AcquireDependencyMutationLockAsync(Guid tenantId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> ExistsOutsideTenantAsync(Guid slotId, CancellationToken cancellationToken) => Task.FromResult(false);

        // ----- rest of the interface — not exercised in these tests -----

        public void Add(PositionSlot slot) => throw new NotSupportedException();
        public Task<PositionSlot?> GetByIdAsync(Guid slotId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingSlotId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> JobProfileExistsOutsideTenantAsync(Guid jobProfileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long?> ResolveWorkCenterIdAsync(Guid tenantId, Guid workCenterId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> WorkCenterExistsOutsideTenantAsync(Guid workCenterId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long?> ResolvePositionSlotIdAsync(Guid tenantId, Guid slotId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedResponse<PositionSlotListItemResponse>> SearchAsync(Guid tenantId, PositionSlotStatus? status, Guid? jobProfileId, Guid? orgUnitId, Guid? workCenterId, Guid? contractTypeId, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PositionSlotResponse?> GetResponseByIdAsync(Guid slotId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PositionSlotExportRow>> GetExportRowsAsync(Guid tenantId, PositionSlotStatus? status, Guid? jobProfileId, Guid? orgUnitId, Guid? workCenterId, Guid? contractTypeId, string? search, int? maxRows, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PositionSlotJobProfileLookup?> GetJobProfileLookupAsync(Guid tenantId, Guid jobProfileId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
