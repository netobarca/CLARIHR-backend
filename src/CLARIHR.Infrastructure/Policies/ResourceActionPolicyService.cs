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
            reasons.Add("The current user is not authorized to edit this record.");
        }
        else if (context.IsSystem)
        {
            canEdit = false;
            reasons.Add("System-managed records cannot be edited.");
        }
        else if (normalizedState is not null && nonEditableStates.Contains(normalizedState))
        {
            canEdit = false;
            reasons.Add($"Records in state '{context.State}' cannot be edited.");
        }

        var canDelete = context.SupportsDelete && context.DeleteAllowed && !context.IsSystem && !context.HasDependencies;
        if (!canDelete)
        {
            if (!context.SupportsDelete)
            {
                reasons.Add("Soft delete policy is enforced for this resource.");
            }
            else if (!context.DeleteAllowed)
            {
                reasons.Add("The current user is not authorized to delete this record.");
            }
            else if (context.IsSystem)
            {
                reasons.Add("System-managed records cannot be deleted.");
            }
            else if (context.HasDependencies)
            {
                reasons.Add("The record cannot be deleted because it has active dependencies.");
            }
        }

        var canArchive = context.SupportsArchive && context.ArchiveAllowed && context.IsActive;
        if (!canArchive && context.SupportsArchive)
        {
            reasons.Add(!context.ArchiveAllowed
                ? "The current user is not authorized to archive this record."
                : context.IsActive
                    ? "The record cannot be archived due to current restrictions."
                    : "The record is already inactive/archived.");
        }

        var canActivate = context.SupportsActivate && context.ActivateAllowed && !context.IsActive;
        if (!canActivate && context.SupportsActivate)
        {
            reasons.Add(!context.ActivateAllowed
                ? "The current user is not authorized to activate this record."
                : context.IsActive
                    ? "The record is already active."
                    : "The record cannot be activated due to current restrictions.");
        }

        var canInactivate = context.SupportsInactivate && context.InactivateAllowed && context.IsActive && !context.HasDependencies;
        if (!canInactivate && context.SupportsInactivate)
        {
            if (!context.InactivateAllowed)
            {
                reasons.Add("The current user is not authorized to inactivate this record.");
            }
            else if (!context.IsActive)
            {
                reasons.Add("The record is already inactive.");
            }
            else if (context.HasDependencies)
            {
                reasons.Add("The record cannot be inactivated because it has active dependencies.");
            }
            else
            {
                reasons.Add("The record cannot be inactivated due to current restrictions.");
            }
        }

        return new AllowedActionsResponse(
            canEdit,
            canDelete,
            canArchive,
            canActivate,
            canInactivate,
            reasons
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }
}
