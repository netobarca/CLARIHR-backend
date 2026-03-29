using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.Platform;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Auditing;

internal sealed class PlatformAuditService(
    IAuditSanitizer auditSanitizer,
    ICurrentUserService currentUserService,
    IHttpContextAccessor httpContextAccessor,
    ApplicationDbContext dbContext) : IPlatformAuditService
{
    public async Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken)
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

        var auditLog = PlatformAuditLog.Create(
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

        dbContext.PlatformAuditLogs.Add(auditLog);
    }
}
