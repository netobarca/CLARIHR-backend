namespace CLARIHR.Application.Common.Policies;

/// <summary>
/// Stable, machine-readable reason codes explaining why an action is not allowed.
/// These are part of the public API contract: the frontend localizes them. Never
/// change an existing code's meaning — add a new one instead.
/// </summary>
public static class AllowedActionReasonCodes
{
    /// <summary>The current user lacks the permission required for the action.</summary>
    public const string NotAuthorized = "NOT_AUTHORIZED";

    /// <summary>System-managed record: cannot be edited or deleted.</summary>
    public const string SystemRecord = "SYSTEM_RECORD";

    /// <summary>The record is in a state that forbids editing.</summary>
    public const string NonEditableState = "NON_EDITABLE_STATE";

    /// <summary>Hard delete is not supported for this resource (soft-delete policy).</summary>
    public const string SoftDeleteEnforced = "SOFT_DELETE_ENFORCED";

    /// <summary>The action is blocked because the record has active dependencies.</summary>
    public const string HasDependencies = "HAS_DEPENDENCIES";

    /// <summary>Cannot activate: the record is already active.</summary>
    public const string AlreadyActive = "ALREADY_ACTIVE";

    /// <summary>Cannot archive/inactivate: the record is already inactive.</summary>
    public const string AlreadyInactive = "ALREADY_INACTIVE";

    /// <summary>The action is restricted by the current resource state (generic).</summary>
    public const string ActionRestricted = "ACTION_RESTRICTED";
}
