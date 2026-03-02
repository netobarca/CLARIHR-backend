namespace CLARIHR.Infrastructure.Configuration;

public sealed class FieldPermissionCacheOptions
{
    public const string SectionName = "Caching:FieldPermissions";

    public FieldPermissionCacheMode Mode { get; init; } = FieldPermissionCacheMode.MemoryOnly;

    public int EntryTtlMinutes { get; init; } = 10;
}

public enum FieldPermissionCacheMode
{
    MemoryOnly = 0,
    Distributed = 1
}
