using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Common.Policies;

namespace CLARIHR.Infrastructure.Policies;

internal sealed class ResourceActionPolicyService : IResourceActionPolicyService
{
    public AllowedActionsResponse Evaluate(ResourceActionContext context)
    {
        var reasons = new List<string>();
        var nonEditableStates = (context.NonEditableStates ?? [])
            .Select(static state => state.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedState = context.State?.Trim().ToUpperInvariant();

        var canEdit = context.SupportsEdit && context.EditAllowed;
        if (context.SupportsEdit && !context.EditAllowed)
        {
            reasons.Add(AllowedActionReasonCodes.NotAuthorized);
        }
        else if (context.IsSystem)
        {
            canEdit = false;
            reasons.Add(AllowedActionReasonCodes.SystemRecord);
        }
        else if (normalizedState is not null && nonEditableStates.Contains(normalizedState))
        {
            canEdit = false;
            reasons.Add(AllowedActionReasonCodes.NonEditableState);
        }

        var canDelete = context.SupportsDelete && context.DeleteAllowed && !context.IsSystem && !context.HasDependencies;
        if (!canDelete)
        {
            if (!context.SupportsDelete)
            {
                reasons.Add(AllowedActionReasonCodes.SoftDeleteEnforced);
            }
            else if (!context.DeleteAllowed)
            {
                reasons.Add(AllowedActionReasonCodes.NotAuthorized);
            }
            else if (context.IsSystem)
            {
                reasons.Add(AllowedActionReasonCodes.SystemRecord);
            }
            else if (context.HasDependencies)
            {
                reasons.Add(AllowedActionReasonCodes.HasDependencies);
            }
        }

        var canArchive = context.SupportsArchive && context.ArchiveAllowed && context.IsActive;
        if (!canArchive && context.SupportsArchive)
        {
            reasons.Add(!context.ArchiveAllowed
                ? AllowedActionReasonCodes.NotAuthorized
                : context.IsActive
                    ? AllowedActionReasonCodes.ActionRestricted
                    : AllowedActionReasonCodes.AlreadyInactive);
        }

        var canActivate = context.SupportsActivate && context.ActivateAllowed && !context.IsActive;
        if (!canActivate && context.SupportsActivate)
        {
            reasons.Add(!context.ActivateAllowed
                ? AllowedActionReasonCodes.NotAuthorized
                : context.IsActive
                    ? AllowedActionReasonCodes.AlreadyActive
                    : AllowedActionReasonCodes.ActionRestricted);
        }

        var canInactivate = context.SupportsInactivate && context.InactivateAllowed && context.IsActive && !context.HasDependencies;
        if (!canInactivate && context.SupportsInactivate)
        {
            if (!context.InactivateAllowed)
            {
                reasons.Add(AllowedActionReasonCodes.NotAuthorized);
            }
            else if (!context.IsActive)
            {
                reasons.Add(AllowedActionReasonCodes.AlreadyInactive);
            }
            else if (context.HasDependencies)
            {
                reasons.Add(AllowedActionReasonCodes.HasDependencies);
            }
            else
            {
                reasons.Add(AllowedActionReasonCodes.ActionRestricted);
            }
        }

        var canView = context.SupportsView && context.ViewAllowed;
        var canCreate = context.SupportsCreate && context.CreateAllowed;

        // Publish is gated by the manage permission (PublishAllowed) AND, when PublishableStates is
        // provided, by the record being in one of those states (e.g. a JobProfile can only be
        // published from Draft). An empty set means the state does not constrain publishing.
        var publishableStates = (context.PublishableStates ?? [])
            .Select(static state => state.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var canPublish = context.SupportsPublish
            && context.PublishAllowed
            && (publishableStates.Count == 0 || (normalizedState is not null && publishableStates.Contains(normalizedState)));
        if (context.SupportsPublish && !canPublish)
        {
            reasons.Add(context.PublishAllowed
                ? AllowedActionReasonCodes.ActionRestricted
                : AllowedActionReasonCodes.NotAuthorized);
        }

        return new AllowedActionsResponse(
            canEdit,
            canDelete,
            canArchive,
            canActivate,
            canInactivate,
            reasons
                .Distinct(StringComparer.Ordinal)
                .ToArray())
        {
            CanView = canView,
            CanCreate = canCreate,
            CanPublish = canPublish,
        };
    }
}
