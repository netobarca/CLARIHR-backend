namespace CLARIHR.Application.Features.PersonnelFiles.Common;

/// <summary>
/// Canonical rate-limit policy names for the Personnel Files endpoints. Single source of
/// truth shared by the <c>AddRateLimiter</c> registration in <c>Program.cs</c>, the
/// <c>[EnableRateLimiting]</c> attributes on the Personnel Files controllers, and the
/// <c>PersonnelFileRateLimitingGovernanceTests</c> guardrail — so the limiter cannot drift
/// from the endpoints it protects (mirrors the
/// <see cref="CLARIHR.Application.Features.PositionSlots.Common.PositionSlotRateLimitPolicies"/>
/// §X-RATE pattern). All four are partitioned per user + tenant.
/// </summary>
public static class PersonnelFileRateLimitPolicies
{
    /// <summary>Per-user+tenant limiter for shell creation.</summary>
    public const string Create = "personnel-files-create";

    /// <summary>Generous per-user+tenant limiter for the paged search / list / advanced
    /// (dynamic-query) reads.</summary>
    public const string Search = "personnel-files-search";

    /// <summary>Per-user+tenant limiter for shell lifecycle (activate / deactivate) mutations.</summary>
    public const string Lifecycle = "personnel-files-lifecycle";

    /// <summary>Tight per-user+tenant limiter for the unbounded-cost reads — row exports and
    /// full-tenant analytics aggregation — i.e. the same abuse class as
    /// <see cref="CLARIHR.Application.Features.PositionSlots.Common.PositionSlotRateLimitPolicies.Export"/>.</summary>
    public const string Export = "personnel-files-export";
}
