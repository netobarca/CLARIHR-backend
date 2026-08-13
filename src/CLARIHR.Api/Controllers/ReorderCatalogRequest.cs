namespace CLARIHR.Api.Controllers;

/// <summary>
/// H-11 — the body of every bulk-reorder endpoint of the section. One shape for the three of them, on purpose:
/// the inconsistency between these catalogs is the finding, so a new capability arriving with three different
/// request shapes would have made it worse.
/// <para>
/// It carries no order numbers. The caller states the desired sequence and the server assigns `10`, `20`, `30`,
/// … which is what makes a collision with the occupational pyramid's unique rank impossible to express.
/// </para>
/// </summary>
public sealed class ReorderCatalogRequest
{
    /// <summary>
    /// Every id of the collection being reordered, exactly once, in the desired order. A partial or duplicated
    /// list is rejected with the resource's `*_ORDER_SET_INCOMPLETE` code rather than partially applied.
    /// </summary>
    public IReadOnlyList<Guid> OrderedPublicIds { get; set; } = [];
}
