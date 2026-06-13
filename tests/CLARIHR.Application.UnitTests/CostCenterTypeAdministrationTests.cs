using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CostCenters;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.CostCenters.Types;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.CostCenters;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Handler-level unit coverage for the CostCenterTypes catalog (mirror of
/// <see cref="WorkCenterTypeAdministrationTests"/>). Pins the business-rule 409s that matter most —
/// the duplicate-code conflict (including the concurrent unique-violation → 409 catch, which a 500
/// regression would silently break) and the inactivate-while-in-use referential guard — plus the
/// catalog resolution rules the CostCenter create handler now enforces (unknown type → 404,
/// inactive type → 409).
/// </summary>
public sealed class CostCenterTypeAdministrationTests
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Create_WhenCodeAlreadyExists_ShouldReturnConflict()
    {
        var unitOfWork = new CountingUnitOfWork();
        var handler = new CreateCostCenterTypeCommandHandler(
            new AllowCostCenterAuthorizationService(),
            new TestCostCenterTypeRepository { CodeExists = true },
            new NoOpAuditService(),
            unitOfWork);

        var result = await handler.Handle(
            new CreateCostCenterTypeCommand(CompanyId, "SALARY-EXPENSE", "Gasto salarial", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CostCenterErrors.CostCenterTypeCodeConflict.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Create_WhenConcurrentInsertTripsUniqueIndex_ShouldReturnConflictNot500()
    {
        // The up-front CodeExistsAsync probe passes, but a concurrent create committed the same code
        // first → the (TenantId, NormalizedCode) unique index rejects this insert. The handler must map
        // the UniqueConstraintViolationException to a clean 409, not let the 23505 escape as a 500.
        var handler = new CreateCostCenterTypeCommandHandler(
            new AllowCostCenterAuthorizationService(),
            new TestCostCenterTypeRepository { CodeExists = false },
            new NoOpAuditService(),
            new ThrowingUnitOfWork(CostCenterValidationRules.CostCenterTypeCodeUniqueConstraintName));

        var result = await handler.Handle(
            new CreateCostCenterTypeCommand(CompanyId, "SALARY-EXPENSE", "Gasto salarial", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CostCenterErrors.CostCenterTypeCodeConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task Inactivate_WhenActiveCostCentersStillUseTheType_ShouldReturnConflict()
    {
        var costCenterType = CostCenterType.Create("SALARY-EXPENSE", "Gasto salarial", null);
        costCenterType.SetTenantId(CompanyId);
        var repository = new TestCostCenterTypeRepository { HasActiveCostCenters = true };
        repository.Seed(costCenterType);
        var unitOfWork = new CountingUnitOfWork();

        var handler = new InactivateCostCenterTypeCommandHandler(
            new AllowCostCenterAuthorizationService(),
            repository,
            new NoOpAuditService(),
            new FixedTenantContext(CompanyId),
            unitOfWork);

        var result = await handler.Handle(
            new InactivateCostCenterTypeCommand(costCenterType.PublicId, costCenterType.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CostCenterErrors.CostCenterTypeInUse.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateCostCenter_WhenTypeNotFound_ShouldReturnNotFound()
    {
        var unitOfWork = new CountingUnitOfWork();
        var handler = new CreateCostCenterCommandHandler(
            new AllowCostCenterAuthorizationService(),
            new NoWriteCostCenterRepository(),
            new TestCostCenterTypeRepository(),
            new NoOpAuditService(),
            unitOfWork);

        var result = await handler.Handle(
            new CreateCostCenterCommand(CompanyId, "CC-001", "Centro", Guid.NewGuid(), null, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CostCenterErrors.CostCenterTypeNotFound.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateCostCenter_WhenTypeIsInactive_ShouldReturnConflict()
    {
        var costCenterType = CostCenterType.Create("SALARY-EXPENSE", "Gasto salarial", null);
        costCenterType.SetTenantId(CompanyId);
        costCenterType.Inactivate();
        var typeRepository = new TestCostCenterTypeRepository();
        typeRepository.Seed(costCenterType);
        var unitOfWork = new CountingUnitOfWork();

        var handler = new CreateCostCenterCommandHandler(
            new AllowCostCenterAuthorizationService(),
            new NoWriteCostCenterRepository(),
            typeRepository,
            new NoOpAuditService(),
            unitOfWork);

        var result = await handler.Handle(
            new CreateCostCenterCommand(CompanyId, "CC-001", "Centro", costCenterType.PublicId, null, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CostCenterErrors.CostCenterTypeInactive.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }
}

file sealed class AllowCostCenterAuthorizationService : ICostCenterAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

    public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

    public Error TenantMismatch(RbacPermissionAction action) => CostCenterErrors.TenantMismatch(action);
}

file sealed class TestCostCenterTypeRepository : ICostCenterTypeRepository
{
    private readonly Dictionary<Guid, CostCenterType> _byPublicId = [];

    public bool CodeExists { get; init; }

    public bool HasActiveCostCenters { get; init; }

    public void Seed(CostCenterType costCenterType) => _byPublicId[costCenterType.PublicId] = costCenterType;

    public void Add(CostCenterType costCenterType) => _byPublicId[costCenterType.PublicId] = costCenterType;

    public Task<CostCenterType?> GetByIdAsync(Guid costCenterTypeId, CancellationToken cancellationToken) =>
        Task.FromResult(_byPublicId.GetValueOrDefault(costCenterTypeId));

    public Task<bool> ExistsOutsideTenantAsync(Guid costCenterTypeId, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingCostCenterTypeId, CancellationToken cancellationToken) =>
        Task.FromResult(CodeExists);

    public Task<PagedResponse<CostCenterTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> HasActiveCostCentersAsync(long costCenterTypeId, CancellationToken cancellationToken) =>
        Task.FromResult(HasActiveCostCenters);
}

file sealed class NoWriteCostCenterRepository : ICostCenterRepository
{
    public void Add(CostCenter costCenter) => throw new NotSupportedException();

    public Task<CostCenter?> GetByIdAsync(Guid costCenterId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> ExistsOutsideTenantAsync(Guid costCenterId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingCostCenterId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> ExistsActiveByCodeAsync(Guid tenantId, string normalizedCode, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<PagedResponse<CostCenterListItemResponse>> SearchAsync(
        Guid tenantId,
        Guid? typeId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CostCenterResponse?> GetResponseByIdAsync(Guid costCenterId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CostCenterUsageResponse?> GetUsageByIdAsync(Guid costCenterId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> HasActiveUsageAsync(Guid tenantId, string normalizedCode, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyCollection<CostCenterExportRow>> GetExportRowsAsync(
        Guid tenantId,
        Guid? typeId,
        bool? isActive,
        string? search,
        int? maxRows,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

file sealed class NoOpAuditService : IAuditService
{
    public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
}

file sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
{
    public Guid? TenantId { get; } = tenantId;
}

file sealed class CountingUnitOfWork : IUnitOfWork
{
    public int SaveChangesCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new NoOpUnitOfWorkTransaction());
}

file sealed class ThrowingUnitOfWork(string constraintName) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new UniqueConstraintViolationException(constraintName, new InvalidOperationException("simulated 23505"));

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new NoOpUnitOfWorkTransaction());
}

file sealed class NoOpUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
