using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.Auditing;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Auditing;

internal sealed class AuditService(
    IAuditLogRepository auditLogRepository,
    IAuditSanitizer auditSanitizer,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor,
    ApplicationDbContext dbContext) : IAuditService
{
    public async Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            throw new InvalidOperationException("Audit logging requires a tenant context.");
        }

        await LogInternalAsync(tenantContext.TenantId.Value, entry, cancellationToken);
    }

    public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken) =>
        LogInternalAsync(tenantId, entry, cancellationToken);

    private async Task LogInternalAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken)
    {
        var actorUserId = Guid.TryParse(currentUserService.UserId, out var parsedUserId)
            ? parsedUserId
            : Guid.Empty;

        var actorEmail = actorUserId == Guid.Empty
            ? null
            : await dbContext.AuthUsers
                .AsNoTracking()
                .Where(user => user.PublicId == actorUserId)
                .Select(user => user.Email)
                .SingleOrDefaultAsync(cancellationToken);

        var auditLog = AuditLog.Create(
            actorUserId,
            actorEmail,
            entry.EventType,
            entry.EntityType,
            entry.EntityId,
            entry.EntityKey,
            entry.Action,
            entry.Summary,
            auditSanitizer.SanitizeToJson(entry.Before),
            auditSanitizer.SanitizeToJson(entry.After),
            auditSanitizer.SanitizeToJson(entry.Diff),
            httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString());

        auditLog.SetTenantId(tenantId);
        auditLogRepository.Add(auditLog);
    }
}
