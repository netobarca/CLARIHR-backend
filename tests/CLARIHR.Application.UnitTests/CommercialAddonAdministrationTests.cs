using System.Reflection;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CommercialAddons;
using CLARIHR.Application.Features.CommercialAddons.Common;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Companies;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Application.UnitTests;

public sealed class CommercialAddonAdministrationTests
{
    [Fact]
    public async Task Create_WhenAuthorizationFails_ShouldReturnForbidden()
    {
        var handler = new CreateCommercialAddonCommandHandler(
            new TestPlatformAuthorizationService(readResult: Result.Success(), manageResult: Result.Failure(CommercialAddonErrors.Forbidden)),
            new TestCommercialAddonRepository(),
            new TestPlatformAuditService(),
            new TestCurrentUserService(),
            new TestUnitOfWork(),
            NullLogger<CreateCommercialAddonCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateCommercialAddonCommand(
                "ADDON-ATTENDANCE",
                "Attendance",
                null,
                CommercialAddonType.Massive,
                1.2m,
                null,
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Draft),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialAddonErrors.Forbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_WhenCodeAlreadyExists_ShouldReturnConflict()
    {
        var repository = new TestCommercialAddonRepository();
        repository.Add(CreateAddon("ADDON-ATTENDANCE", "Attendance", CommercialAddonStatus.Active, id: 10));

        var handler = new CreateCommercialAddonCommandHandler(
            new TestPlatformAuthorizationService(Result.Success(), Result.Success()),
            repository,
            new TestPlatformAuditService(),
            new TestCurrentUserService(),
            new TestUnitOfWork(),
            NullLogger<CreateCommercialAddonCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateCommercialAddonCommand(
                " addon-attendance ",
                "Attendance Plus",
                null,
                CommercialAddonType.Massive,
                1.5m,
                25m,
                CommercialAddonPeriodicity.Monthly,
                CommercialAddonStatus.Draft),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialAddonErrors.CodeConflict.Code, result.Error.Code);
    }

    [Fact]
    public void CreateValidator_WhenTypeIsNotMassive_ShouldRejectCommand()
    {
        var validator = new CreateCommercialAddonCommandValidator();
        var command = new CreateCommercialAddonCommand(
            "ADDON-ATTENDANCE",
            "Attendance",
            null,
            (CommercialAddonType)99,
            1.2m,
            null,
            CommercialAddonPeriodicity.Monthly,
            CommercialAddonStatus.Draft);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public async Task GetById_WhenAddonDoesNotExist_ShouldReturnNotFound()
    {
        var handler = new GetCommercialAddonByIdQueryHandler(
            new TestPlatformAuthorizationService(Result.Success(), Result.Success()),
            new TestCommercialAddonRepository());

        var result = await handler.Handle(new GetCommercialAddonByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialAddonErrors.NotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_WhenConcurrencyTokenDoesNotMatch_ShouldReturnConflict()
    {
        var repository = new TestCommercialAddonRepository();
        var addon = CreateAddon("ADDON-ATTENDANCE", "Attendance", CommercialAddonStatus.Draft, id: 20);
        repository.Add(addon);

        var handler = new UpdateCommercialAddonCommandHandler(
            new TestPlatformAuthorizationService(Result.Success(), Result.Success()),
            repository,
            new TestPlatformAuditService(),
            new TestCurrentUserService(),
            new TestUnitOfWork(),
            NullLogger<UpdateCommercialAddonCommandHandler>.Instance);

        var result = await handler.Handle(
            new UpdateCommercialAddonCommand(
                addon.PublicId,
                "ADDON-ATTENDANCE",
                "Attendance Updated",
                "Updated",
                CommercialAddonType.Massive,
                1.5m,
                30m,
                CommercialAddonPeriodicity.Annual,
                Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialAddonErrors.ConcurrencyConflict.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_ShouldRefreshConcurrencyToken_AndEditableFields()
    {
        var repository = new TestCommercialAddonRepository();
        var addon = CreateAddon("ADDON-ATTENDANCE", "Attendance", CommercialAddonStatus.Draft, id: 30);
        repository.Add(addon);

        var originalToken = addon.ConcurrencyToken;

        var handler = new UpdateCommercialAddonCommandHandler(
            new TestPlatformAuthorizationService(Result.Success(), Result.Success()),
            repository,
            new TestPlatformAuditService(),
            new TestCurrentUserService(),
            new TestUnitOfWork(),
            NullLogger<UpdateCommercialAddonCommandHandler>.Instance);

        var result = await handler.Handle(
            new UpdateCommercialAddonCommand(
                addon.PublicId,
                "ADDON-ATTENDANCE",
                "Attendance Plus",
                "Updated description",
                CommercialAddonType.Massive,
                1.7m,
                null,
                CommercialAddonPeriodicity.Annual,
                originalToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Attendance Plus", addon.Name);
        Assert.Equal("Updated description", addon.Description);
        Assert.Equal(1.7m, addon.PricePerActiveEmployee);
        Assert.Null(addon.MinimumMonthlyFee);
        Assert.Equal(CommercialAddonPeriodicity.Annual, addon.Periodicity);
        Assert.NotEqual(originalToken, addon.ConcurrencyToken);
        Assert.Equal(addon.ConcurrencyToken, result.Value.ConcurrencyToken);
    }

    [Fact]
    public async Task Activate_WhenAddonAlreadyActive_ShouldReturnConflict()
    {
        var repository = new TestCommercialAddonRepository();
        var addon = CreateAddon("ADDON-ATTENDANCE", "Attendance", CommercialAddonStatus.Active, id: 40);
        repository.Add(addon);

        var handler = new ActivateCommercialAddonCommandHandler(
            new TestPlatformAuthorizationService(Result.Success(), Result.Success()),
            repository,
            new TestPlatformAuditService(),
            new TestCurrentUserService(),
            new TestUnitOfWork(),
            NullLogger<ActivateCommercialAddonCommandHandler>.Instance);

        var result = await handler.Handle(
            new ActivateCommercialAddonCommand(addon.PublicId, addon.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialAddonErrors.AlreadyActive.Code, result.Error.Code);
    }

    [Fact]
    public async Task Search_ShouldApplyStatusSearchAndPagination()
    {
        var repository = new TestCommercialAddonRepository();
        repository.Add(CreateAddon("ADDON-ATTENDANCE", "Attendance", CommercialAddonStatus.Active, id: 50));
        repository.Add(CreateAddon("ADDON-PAYROLL", "Payroll", CommercialAddonStatus.Active, id: 51));
        repository.Add(CreateAddon("ADDON-PERFORMANCE", "Performance", CommercialAddonStatus.Draft, id: 52));

        var handler = new SearchCommercialAddonsQueryHandler(
            new TestPlatformAuthorizationService(Result.Success(), Result.Success()),
            repository);

        var result = await handler.Handle(
            new SearchCommercialAddonsQuery(CommercialAddonStatus.Active, "addON", PageNumber: 1, PageSize: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("ADDON-ATTENDANCE", item.Code);
        Assert.Equal("Attendance", item.Name);
    }

    private static CommercialAddon CreateAddon(
        string code,
        string name,
        CommercialAddonStatus status,
        long id)
    {
        var addon = CommercialAddon.Create(
            code,
            name,
            $"{name} description",
            CommercialAddonType.Massive,
            1.2m,
            25m,
            CommercialAddonPeriodicity.Monthly,
            status);

        SetEntityId(addon, id);
        return addon;
    }

    private static void SetEntityId(Entity entity, long id)
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(entity, [id]);
    }

    private sealed class TestPlatformAuthorizationService(Result readResult, Result manageResult) : IPlatformAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(readResult);

        public Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken) =>
            Task.FromResult(manageResult);
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

    private sealed class TestCommercialAddonRepository : ICommercialAddonRepository
    {
        private long _nextId = 1000;

        public List<CommercialAddon> Items { get; } = [];

        public void Add(CommercialAddon addon)
        {
            if (addon.Id == 0)
            {
                SetEntityId(addon, _nextId++);
            }

            Items.Add(addon);
        }

        public Task<CommercialAddon?> GetByIdAsync(Guid commercialAddonId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(addon => addon.PublicId == commercialAddonId));

        public Task<bool> CodeExistsAsync(string normalizedCode, long? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(addon =>
                addon.NormalizedCode == normalizedCode &&
                (!excludingId.HasValue || addon.Id != excludingId.Value)));

        public Task<PagedResponse<CommercialAddonSummaryResponse>> SearchAsync(
            CommercialAddonStatus? status,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            IEnumerable<CommercialAddon> query = Items;

            if (status.HasValue)
            {
                query = query.Where(addon => addon.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToUpperInvariant();
                query = query.Where(addon =>
                    addon.NormalizedCode.Contains(normalizedSearch, StringComparison.Ordinal) ||
                    addon.NormalizedName.Contains(normalizedSearch, StringComparison.Ordinal));
            }

            var ordered = query
                .OrderBy(addon => addon.Name, StringComparer.Ordinal)
                .ThenBy(addon => addon.Code, StringComparer.Ordinal)
                .ToList();

            var items = ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(addon => new CommercialAddonSummaryResponse(
                    addon.PublicId,
                    addon.Code,
                    addon.Name,
                    addon.Description,
                    addon.Type,
                    addon.PricePerActiveEmployee,
                    addon.MinimumMonthlyFee,
                    addon.Periodicity,
                    addon.Status,
                    addon.CreatedUtc,
                    addon.ModifiedUtc))
                .ToArray();

            return Task.FromResult(new PagedResponse<CommercialAddonSummaryResponse>(items, pageNumber, pageSize, ordered.Count));
        }
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
