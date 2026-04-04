using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Auditing;
using CLARIHR.Infrastructure.Auditing;

namespace CLARIHR.Application.UnitTests;

public sealed class AuditAdministrationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void SanitizeToJson_WhenPayloadContainsSensitiveFields_ShouldRemoveThem()
    {
        var sanitizer = new AuditSanitizer();

        var json = sanitizer.SanitizeToJson(new
        {
            Email = "admin@acme.test",
            PasswordHash = "super-secret-hash",
            RefreshTokens = new[] { "abc", "def" },
            Profile = new
            {
                FirstName = "Ana",
                RawToken = "token-value"
            }
        });

        Assert.NotNull(json);
        Assert.Contains("admin@acme.test", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshTokens", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RawToken", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAuditLogsQuery_WhenRepositoryContainsMultipleTenants_ShouldReturnCurrentTenantOnly()
    {
        var currentLog = CreateAuditLog(TenantId, AuditEventTypes.RoleCreated, AuditEntityTypes.Role, "Created role Supervisor.");
        var otherLog = CreateAuditLog(OtherTenantId, AuditEventTypes.UserUpdated, AuditEntityTypes.User, "Updated user Bruno.");
        var repository = new TestAuditLogRepository(TenantId, [currentLog, otherLog]);
        var handler = new GetAuditLogsQueryHandler(new AllowAuditAuthorizationService(), repository);

        var result = await handler.Handle(new GetAuditLogsQuery(Page: 1, PageSize: 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(currentLog.PublicId, item.Id);
    }

    [Fact]
    public async Task GetAuditLogsQuery_WhenFilteringByEntityIdAndType_ShouldReturnOnlyMatchingItemsAndCount()
    {
        var matchingEntityId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var otherEntityId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var matchingLogA = CreateAuditLog(
            TenantId,
            AuditEventTypes.UserUpdated,
            AuditEntityTypes.User,
            "Updated user profile.",
            matchingEntityId);
        var matchingLogB = CreateAuditLog(
            TenantId,
            AuditEventTypes.UserCreated,
            AuditEntityTypes.User,
            "Created user profile.",
            matchingEntityId);
        var differentTypeSameEntity = CreateAuditLog(
            TenantId,
            AuditEventTypes.RoleUpdated,
            AuditEntityTypes.Role,
            "Updated role assignment.",
            matchingEntityId);
        var differentEntitySameType = CreateAuditLog(
            TenantId,
            AuditEventTypes.UserUpdated,
            AuditEntityTypes.User,
            "Updated another user.",
            otherEntityId);
        var repository = new TestAuditLogRepository(
            TenantId,
            [matchingLogA, matchingLogB, differentTypeSameEntity, differentEntitySameType]);
        var handler = new GetAuditLogsQueryHandler(new AllowAuditAuthorizationService(), repository);

        var result = await handler.Handle(
            new GetAuditLogsQuery(
                EntityId: matchingEntityId,
                EntityType: AuditEntityTypes.User,
                Page: 1,
                PageSize: 20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.All(result.Value.Items, item =>
        {
            Assert.Equal(matchingEntityId, item.EntityId);
            Assert.Equal(AuditEntityTypes.User, item.EntityType);
        });
    }

    [Fact]
    public async Task GetAuditLogDetailQuery_WhenLogBelongsToAnotherTenant_ShouldReturnTenantMismatch()
    {
        var otherLog = CreateAuditLog(OtherTenantId, AuditEventTypes.RoleCreated, AuditEntityTypes.Role, "Created role Supervisor.");
        var repository = new TestAuditLogRepository(TenantId, [otherLog]);
        var handler = new GetAuditLogDetailQueryHandler(new AllowAuditAuthorizationService(), repository);

        var result = await handler.Handle(new GetAuditLogDetailQuery(otherLog.PublicId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TENANT_MISMATCH", result.Error.Code);
    }

    private static AuditLog CreateAuditLog(
        Guid tenantId,
        string eventType,
        string entityType,
        string summary,
        Guid? entityId = null)
    {
        var log = AuditLog.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "admin@acme.test",
            eventType,
            entityType,
            entityId ?? Guid.NewGuid(),
            "entity-key",
            AuditActions.Update,
            summary,
            beforeJson: null,
            afterJson: null,
            diffJson: null,
            ipAddress: "127.0.0.1",
            userAgent: "xunit");
        log.SetTenantId(tenantId);
        log.MarkCreated(DateTime.Parse("2026-03-01T10:00:00Z").ToUniversalTime());
        return log;
    }

    private sealed class AllowAuditAuthorizationService : IRbacAuthorizationService
    {
        public Task<Result> AuthorizeAsync(
            string resourceKey,
            RbacPermissionAction action,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result> AuthorizeFieldsAsync(
            string resourceKey,
            RbacPermissionAction action,
            IReadOnlyCollection<string> fieldKeys,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }

    private sealed class TestAuditLogRepository(Guid currentTenantId, IReadOnlyCollection<AuditLog> logs) : IAuditLogRepository
    {
        public void Add(AuditLog auditLog) => throw new NotSupportedException();

        public Task<PagedResponse<AuditLog>> GetLogsAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            Guid? actorUserId,
            Guid? entityId,
            string? entityType,
            string? eventType,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var query = logs
                .Where(log => log.TenantId == currentTenantId)
                .AsEnumerable();

            if (fromUtc.HasValue)
            {
                query = query.Where(log => log.CreatedUtc >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(log => log.CreatedUtc <= toUtc.Value);
            }

            if (actorUserId.HasValue)
            {
                query = query.Where(log => log.ActorUserId == actorUserId.Value);
            }

            if (entityId.HasValue)
            {
                query = query.Where(log => log.EntityId == entityId.Value);
            }

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                query = query.Where(log => log.EntityType == entityType);
            }

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                query = query.Where(log => log.EventType == eventType);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(log => log.Summary.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var totalCount = query.Count();
            var items = query
                .OrderByDescending(log => log.CreatedUtc)
                .ThenByDescending(log => log.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            return Task.FromResult(new PagedResponse<AuditLog>(items, pageNumber, pageSize, totalCount));
        }

        public Task<AuditLog?> GetByPublicIdAsync(Guid auditLogId, CancellationToken cancellationToken) =>
            Task.FromResult(logs.SingleOrDefault(log => log.PublicId == auditLogId && log.TenantId == currentTenantId));

        public Task<bool> ExistsOutsideTenantAsync(Guid auditLogId, CancellationToken cancellationToken) =>
            Task.FromResult(logs.Any(log => log.PublicId == auditLogId && log.TenantId != currentTenantId));
    }
}
