namespace CLARIHR.Application.Common.Policies;

/// <summary>
/// Marker for response DTOs that can carry an <see cref="AllowedActionsResponse"/>.
/// Most DTOs already expose <c>AllowedActions</c> as an init-only positional record
/// member, so implementing this interface is usually a one-token change
/// (<c>: ISupportsAllowedActions</c>). The centralized AllowedActionsResultFilter
/// populates it (via the init setter) when it is still <c>null</c>; handlers that
/// pre-populate it keep priority (the filter never overwrites a non-null value).
/// </summary>
public interface ISupportsAllowedActions
{
    AllowedActionsResponse? AllowedActions { get; init; }
}
