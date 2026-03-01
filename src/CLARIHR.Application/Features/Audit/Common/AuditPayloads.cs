namespace CLARIHR.Application.Features.Audit.Common;

public sealed record AuditValueChange<T>(T? Before, T? After);

public static class AuditPayloads
{
    public static AuditValueChange<T> Change<T>(T? before, T? after) => new(before, after);
}
