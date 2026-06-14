using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.Locations.WorkCenterTypes;
using CLARIHR.Domain.Locations;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// WCT-B: handler-level unit coverage for WorkCenterTypes (previously only the patch applier had unit
/// tests; the handlers leaned on integration). Pins the two business-rule 409s that matter most — the
/// duplicate-code conflict (including the WCT-A concurrent unique-violation → 409 catch, which a 500
/// regression would silently break) and the inactivate-while-in-use referential guard.
/// </summary>
public sealed class WorkCenterTypeAdministrationTests
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Create_WhenCodeAlreadyExists_ShouldReturnConflict()
    {
        var unitOfWork = new CountingUnitOfWork();
        var handler = new CreateWorkCenterTypeCommandHandler(
            new AllowLocationAuthorizationService(),
            new TestWorkCenterTypeRepository { CodeExists = true },
            new NoOpAuditService(),
            unitOfWork);

        var result = await handler.Handle(
            new CreateWorkCenterTypeCommand(CompanyId, "WCT-001", "Agencia", null, true, false, false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LocationErrors.WorkCenterTypeCodeConflict.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Create_WhenConcurrentInsertTripsUniqueIndex_ShouldReturnConflictNot500()
    {
        // WCT-A: the up-front CodeExistsAsync probe passes, but a concurrent create committed the same code
        // first → the (TenantId, NormalizedCode) unique index rejects this insert. The handler must map the
        // UniqueConstraintViolationException to a clean 409, not let the 23505 escape as a 500.
        var handler = new CreateWorkCenterTypeCommandHandler(
            new AllowLocationAuthorizationService(),
            new TestWorkCenterTypeRepository { CodeExists = false },
            new NoOpAuditService(),
            new ThrowingUnitOfWork(LocationValidationRules.WorkCenterTypeCodeUniqueConstraintName));

        var result = await handler.Handle(
            new CreateWorkCenterTypeCommand(CompanyId, "WCT-001", "Agencia", null, true, false, false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LocationErrors.WorkCenterTypeCodeConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_WhenDescriptionProvided_ShouldPersistAndReturnTrimmedDescription()
    {
        // The reported bug: a stored description must round-trip back through the response. Asserts the
        // create handler maps Description into WorkCenterTypeResponse (and normalizes surrounding whitespace).
        var handler = new CreateWorkCenterTypeCommandHandler(
            new AllowLocationAuthorizationService(),
            new TestWorkCenterTypeRepository { CodeExists = false },
            new NoOpAuditService(),
            new CountingUnitOfWork());

        var result = await handler.Handle(
            new CreateWorkCenterTypeCommand(CompanyId, "WCT-010", "Agencia", "  Centros de atención al cliente  ", true, false, false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Centros de atención al cliente", result.Value.Description);
    }

    [Fact]
    public async Task Inactivate_WhenActiveWorkCentersStillUseTheType_ShouldReturnConflict()
    {
        var workCenterType = WorkCenterType.Create("WCT-001", "Agencia", null, true, false, false);
        workCenterType.SetTenantId(CompanyId);
        var repository = new TestWorkCenterTypeRepository();
        repository.Seed(workCenterType);
        var unitOfWork = new CountingUnitOfWork();

        var handler = new InactivateWorkCenterTypeCommandHandler(
            new AllowLocationAuthorizationService(),
            repository,
            new TestLocationDependencyPolicy(Result.Failure(LocationErrors.WorkCenterTypeInUse)),
            new NoOpAuditService(),
            new FixedTenantContext(CompanyId),
            unitOfWork);

        var result = await handler.Handle(
            new InactivateWorkCenterTypeCommand(workCenterType.PublicId, workCenterType.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LocationErrors.WorkCenterTypeInUse.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }
}

file sealed class AllowLocationAuthorizationService : ILocationAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

    public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

    public Error TenantMismatch(RbacPermissionAction action) => LocationErrors.TenantMismatch(action);
}

file sealed class TestWorkCenterTypeRepository : IWorkCenterTypeRepository
{
    private readonly Dictionary<Guid, WorkCenterType> _byPublicId = [];

    public bool CodeExists { get; init; }

    public void Seed(WorkCenterType workCenterType) => _byPublicId[workCenterType.PublicId] = workCenterType;

    public void Add(WorkCenterType workCenterType) => _byPublicId[workCenterType.PublicId] = workCenterType;

    public Task<WorkCenterType?> GetByIdAsync(Guid workCenterTypeId, CancellationToken cancellationToken) =>
        Task.FromResult(_byPublicId.GetValueOrDefault(workCenterTypeId));

    public Task<bool> ExistsOutsideTenantAsync(Guid workCenterTypeId, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingWorkCenterTypeId, CancellationToken cancellationToken) =>
        Task.FromResult(CodeExists);

    public Task<PagedResponse<WorkCenterTypeResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> HasActiveWorkCentersAsync(long workCenterTypeId, CancellationToken cancellationToken) => throw new NotSupportedException();
}

file sealed class TestLocationDependencyPolicy(Result workCenterTypeResult) : ILocationDependencyPolicy
{
    public Task<Result> CanInactivateLocationGroupAsync(Guid locationGroupId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result> CanInactivateWorkCenterTypeAsync(Guid workCenterTypeId, CancellationToken cancellationToken) => Task.FromResult(workCenterTypeResult);

    public Task<Result> CanInactivateWorkCenterAsync(Guid workCenterId, CancellationToken cancellationToken) => throw new NotSupportedException();
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
