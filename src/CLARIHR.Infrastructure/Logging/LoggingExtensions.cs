using Microsoft.Extensions.Logging;

namespace CLARIHR.Infrastructure.Logging;

/// <summary>
/// Provides helper scopes for enriching logs in handlers and services.
/// This keeps tenant, user, and operation context consistent across entries.
/// </summary>
internal static class LoggingExtensions
{
    /// <summary>
    /// Creates a scope that enriches enclosed log entries with tenant and user information.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="operationName">A descriptive operation name.</param>
    /// <returns>A disposable scope for the current operation.</returns>
    public static IDisposable? BeginOperationScope(
        this ILogger logger,
        Guid? tenantId,
        object? userId,
        string operationName)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            { "OperationName", operationName },
            { "TenantId", tenantId?.ToString() ?? "system" },
            { "UserId", userId?.ToString() ?? "anonymous" }
        });
    }

    /// <summary>
    /// Creates a scope for operations that do not run under a tenant context.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="operationName">A descriptive operation name.</param>
    /// <returns>A disposable scope for the current operation.</returns>
    public static IDisposable? BeginSystemOperationScope(
        this ILogger logger,
        string operationName)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            { "OperationName", operationName },
            { "Scope", "system" }
        });
    }
}
