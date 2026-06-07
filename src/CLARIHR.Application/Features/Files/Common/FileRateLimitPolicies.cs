namespace CLARIHR.Application.Features.Files.Common;

/// <summary>
/// Canonical rate-limit policy names for the Files endpoints. Single source of truth shared by the
/// <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the <c>[EnableRateLimiting]</c>
/// attributes on <c>FilesController</c>, and the <c>FileRateLimitingGovernanceTests</c> guardrail —
/// so the limiter cannot drift from the endpoints it protects (mirrors the
/// <see cref="CLARIHR.Application.Features.PersonnelFiles.Common.PersonnelFileRateLimitPolicies"/>
/// pattern). All partitioned per user + tenant.
/// </summary>
public static class FileRateLimitPolicies
{
    /// <summary>Per-user+tenant limiter for the direct-upload session creator (reserves a row + mints a write SAS).</summary>
    public const string Upload = "files-upload";

    /// <summary>Generous per-user+tenant limiter for the owned-file read-url (mints a read SAS).</summary>
    public const string Read = "files-read";

    /// <summary>Per-user+tenant limiter for the file lifecycle mutations (complete, delete).</summary>
    public const string Lifecycle = "files-lifecycle";
}
