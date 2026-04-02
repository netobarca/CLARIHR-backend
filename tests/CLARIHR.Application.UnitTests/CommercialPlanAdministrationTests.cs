using CLARIHR.Application.Abstractions.Auditing;
using System.Reflection;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CommercialPlans;
using CLARIHR.Application.Features.CommercialPlans.Common;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Companies;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Application.UnitTests;

public sealed class CommercialPlanAdministrationTests
{
    [Fact]
    public async Task Create_WhenAuthorizationFails_ShouldReturnForbidden()
    {
        var handler = new CreateCommercialPlanCommandHandler(
            new TestPlatformAuthorizationService(Result.Failure(CommercialPlanErrors.Forbidden)),
            new TestCommercialPlanRepository(),
            new TestPlatformAuditService(),
            new TestCurrentUserService(),
            new FixedDateTimeProvider(DateTime.Parse("2026-04-02T12:00:00Z").ToUniversalTime()),
            new TestUnitOfWork(),
            NullLogger<CreateCommercialPlanCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateCommercialPlanCommand(
                "PRO",
                "Professional",
                null,
                120m,
                3m,
                CommercialPlanStatus.Draft,
                []),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialPlanErrors.Forbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_WhenCodeAlreadyExists_ShouldReturnConflict()
    {
        var repository = new TestCommercialPlanRepository();
        repository.Add(CreatePlan("PRO", "Professional", CommercialPlanStatus.Active, id: 10));

        var handler = new CreateCommercialPlanCommandHandler(
            new TestPlatformAuthorizationService(Result.Success()),
            repository,
            new TestPlatformAuditService(),
            new TestCurrentUserService(),
            new FixedDateTimeProvider(DateTime.Parse("2026-04-02T12:00:00Z").ToUniversalTime()),
            new TestUnitOfWork(),
            NullLogger<CreateCommercialPlanCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateCommercialPlanCommand(
                " pro ",
                "Professional Plus",
                null,
                180m,
                4m,
                CommercialPlanStatus.Draft,
                []),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialPlanErrors.CodeConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetById_WhenPlanDoesNotExist_ShouldReturnNotFound()
    {
        var handler = new GetCommercialPlanByIdQueryHandler(
            new TestPlatformAuthorizationService(Result.Success()),
            new TestCommercialPlanRepository());

        var result = await handler.Handle(new GetCommercialPlanByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialPlanErrors.NotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_WhenConcurrencyTokenDoesNotMatch_ShouldReturnConflict()
    {
        var repository = new TestCommercialPlanRepository();
        var plan = CreatePlan("PRO", "Professional", CommercialPlanStatus.Draft, id: 20);
        repository.Add(plan);

        var handler = new UpdateCommercialPlanCommandHandler(
            new TestPlatformAuthorizationService(Result.Success()),
            repository,
            new TestPlatformAuditService(),
            new TestCurrentUserService(),
            new FixedDateTimeProvider(DateTime.Parse("2026-04-02T12:00:00Z").ToUniversalTime()),
            new TestUnitOfWork(),
            NullLogger<UpdateCommercialPlanCommandHandler>.Instance);

        var result = await handler.Handle(
            new UpdateCommercialPlanCommand(
                plan.PublicId,
                "PRO",
                "Professional Updated",
                null,
                180m,
                4m,
                [new CommercialPlanLimitInput("work_centers", 10m)],
                Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialPlanErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_ShouldReplaceLimitsAndRefreshConcurrencyToken()
    {
        var repository = new TestCommercialPlanRepository();
        var plan = CreatePlan(
            "PRO",
            "Professional",
            CommercialPlanStatus.Draft,
            id: 30,
            limits: [("employees", 25m)]);
        repository.Add(plan);

        var originalToken = plan.ConcurrencyToken;

        var handler = new UpdateCommercialPlanCommandHandler(
            new TestPlatformAuthorizationService(Result.Success()),
            repository,
            new TestPlatformAuditService(),
            new TestCurrentUserService(),
            new FixedDateTimeProvider(DateTime.Parse("2026-04-02T12:00:00Z").ToUniversalTime()),
            new TestUnitOfWork(),
            NullLogger<UpdateCommercialPlanCommandHandler>.Instance);

        var result = await handler.Handle(
            new UpdateCommercialPlanCommand(
                plan.PublicId,
                "PRO",
                "Professional Plus",
                "Updated description",
                180m,
                4m,
                [new CommercialPlanLimitInput("work_centers", 10m)],
                originalToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Professional Plus", plan.Name);
        Assert.Equal("Updated description", plan.Description);
        Assert.Equal(180m, plan.BaseMonthlyFee);
        Assert.Equal(4m, plan.PricePerActiveEmployee);
        Assert.NotEqual(originalToken, plan.ConcurrencyToken);

        var limit = Assert.Single(plan.Limits);
        Assert.Equal("WORK_CENTERS", limit.LimitCode);
        Assert.Equal(10m, limit.Value);
        Assert.Equal(plan.ConcurrencyToken, result.Value.ConcurrencyToken);
    }

    [Fact]
    public async Task Update_WhenSystemPlanCodeOrNameChanges_ShouldReturnConflict()
    {
        var repository = new TestCommercialPlanRepository();
        var plan = CreatePlan("FREE", "Free", CommercialPlanStatus.Active, isSystemPlan: true, id: 40);
        repository.Add(plan);

        var handler = new UpdateCommercialPlanCommandHandler(
            new TestPlatformAuthorizationService(Result.Success()),
            repository,
            new TestPlatformAuditService(),
            new TestCurrentUserService(),
            new FixedDateTimeProvider(DateTime.Parse("2026-04-02T12:00:00Z").ToUniversalTime()),
            new TestUnitOfWork(),
            NullLogger<UpdateCommercialPlanCommandHandler>.Instance);

        var result = await handler.Handle(
            new UpdateCommercialPlanCommand(
                plan.PublicId,
                "FREE",
                "Free Updated",
                "Protected plan",
                0m,
                0m,
                [],
                plan.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialPlanErrors.SystemPlanRenameForbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task Inactivate_WhenPlanIsSystemPlan_ShouldReturnConflict()
    {
        var repository = new TestCommercialPlanRepository();
        var plan = CreatePlan("FREE", "Free", CommercialPlanStatus.Active, isSystemPlan: true, id: 50);
        repository.Add(plan);

        var handler = new InactivateCommercialPlanCommandHandler(
            new TestPlatformAuthorizationService(Result.Success()),
            repository,
            new TestPlatformAuditService(),
            new TestCurrentUserService(),
            new TestUnitOfWork(),
            NullLogger<InactivateCommercialPlanCommandHandler>.Instance);

        var result = await handler.Handle(
            new InactivateCommercialPlanCommand(plan.PublicId, plan.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialPlanErrors.SystemPlanInactivationForbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task Search_ShouldApplyStatusSearchAndPagination()
    {
        var repository = new TestCommercialPlanRepository();
        repository.Add(CreatePlan("PRO", "Professional", CommercialPlanStatus.Active, id: 60));
        repository.Add(CreatePlan("PROVIDER", "Provider", CommercialPlanStatus.Active, id: 61));
        repository.Add(CreatePlan("PROPOSAL", "Proposal", CommercialPlanStatus.Draft, id: 62));

        var handler = new SearchCommercialPlansQueryHandler(
            new TestPlatformAuthorizationService(Result.Success()),
            repository);

        var result = await handler.Handle(
            new SearchCommercialPlansQuery(CommercialPlanStatus.Active, "pro", PageNumber: 1, PageSize: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("PRO", item.Code);
        Assert.Equal("Professional", item.Name);
    }

    private static CommercialPlan CreatePlan(
        string code,
        string name,
        CommercialPlanStatus status,
        long id,
        bool isSystemPlan = false,
        IEnumerable<(string LimitCode, decimal Value)>? limits = null)
    {
        var plan = CommercialPlan.Create(
            code,
            name,
            $"{name} description",
            100m,
            2m,
            status,
            isSystemPlan,
            limits ?? []);

        SetEntityId(plan, id);
        return plan;
    }

    private static void SetEntityId(Entity entity, long id)
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(entity, [id]);
    }

    private sealed class TestPlatformAuthorizationService(Result result) : IPlatformAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);

        public Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class TestPlatformAuditService : IPlatformAuditService
    {
        public List<AuditLogEntry> Entries { get; } = [];

        public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public string? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111").ToString();

        public IReadOnlyCollection<string> Roles => [];

        public IReadOnlyCollection<string> Permissions => [];
    }

    private sealed class TestCommercialPlanRepository : ICommercialPlanRepository
    {
        private long _nextId = 1000;
        private long _nextVersionId = 5000;

        public List<CommercialPlan> Items { get; } = [];

        public void Add(CommercialPlan plan)
        {
            if (plan.Id == 0)
            {
                SetEntityId(plan, _nextId++);
            }

            foreach (var version in plan.Versions.Where(version => version.Id == 0))
            {
                SetEntityId(version, _nextVersionId++);
            }

            Items.Add(plan);
        }

        public Task<CommercialPlan?> GetByIdAsync(Guid commercialPlanId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(plan => plan.PublicId == commercialPlanId));

        public Task<CommercialPlanVersion?> GetEffectiveVersionAsync(
            Guid commercialPlanId,
            DateTime effectiveAtUtc,
            CancellationToken cancellationToken)
        {
            var version = Items
                .SingleOrDefault(plan => plan.PublicId == commercialPlanId)?
                .Versions
                .SingleOrDefault(item => item.IsEffectiveOn(effectiveAtUtc));

            return Task.FromResult(version);
        }

        public Task<CommercialPlan?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(plan => plan.NormalizedCode == normalizedCode));

        public Task<bool> IsSystemPlanAsync(long commercialPlanId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Single(plan => plan.Id == commercialPlanId).IsSystemPlan);

        public Task<bool> CodeExistsAsync(string normalizedCode, long? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(plan =>
                plan.NormalizedCode == normalizedCode &&
                (!excludingId.HasValue || plan.Id != excludingId.Value)));

        public Task<PagedResponse<CommercialPlanSummaryResponse>> SearchAsync(
            CommercialPlanStatus? status,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            IEnumerable<CommercialPlan> query = Items;

            if (status.HasValue)
            {
                query = query.Where(plan => plan.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToUpperInvariant();
                query = query.Where(plan =>
                    plan.NormalizedCode.Contains(normalizedSearch, StringComparison.Ordinal) ||
                    plan.NormalizedName.Contains(normalizedSearch, StringComparison.Ordinal));
            }

            var ordered = query
                .OrderBy(plan => plan.Name, StringComparer.Ordinal)
                .ThenBy(plan => plan.Code, StringComparer.Ordinal)
                .ToList();

            var items = ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(plan => new CommercialPlanSummaryResponse(
                    plan.PublicId,
                    plan.Code,
                    plan.Name,
                    plan.Description,
                    plan.BaseMonthlyFee,
                    plan.PricePerActiveEmployee,
                    plan.GetCurrentVersion().VersionNumber,
                    plan.GetCurrentVersion().CurrencyCode,
                    plan.Status,
                    plan.IsSystemPlan,
                    plan.CreatedUtc,
                    plan.ModifiedUtc))
                .ToArray();

            return Task.FromResult(new PagedResponse<CommercialPlanSummaryResponse>(items, pageNumber, pageSize, ordered.Count));
        }
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IUnitOfWorkTransaction>(new TestUnitOfWorkTransaction());
    }

    private sealed class TestUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
