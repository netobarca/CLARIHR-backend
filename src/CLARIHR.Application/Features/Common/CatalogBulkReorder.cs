using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.Common;

/// <summary>
/// H-11 — shared rules for the bulk reorder of the section's catalogs. Reordering used to be N patches, and on
/// the occupational pyramid it was worse than N: its rank is UNIQUE per tenant, so a plain swap could not be
/// expressed one call at a time — setting the first item to the other's number collides — and a client had to
/// invent a temporary value across three calls.
/// <para>
/// The contract removes the whole class of collision: the caller sends the COMPLETE set of ids in the desired
/// order and the server assigns the numbers. There are no numbers in the request, so a client cannot construct
/// a conflict, and the operation is idempotent.
/// </para>
/// </summary>
public static class CatalogBulkReorder
{
    /// <summary>The gap between consecutive positions, so a later single-item insert has room without a reorder.</summary>
    public const int Step = 10;

    /// <summary>
    /// The order value for the <paramref name="index"/>-th item (0-based) of the submitted list: 10, 20, 30…
    /// Never zero, which also satisfies the occupational pyramid's "rank must be positive" invariant.
    /// </summary>
    public static int OrderAt(int index) => (index + 1) * Step;

    /// <summary>
    /// Rejects anything but an exact permutation of the persisted set. A partial list would leave the omitted
    /// rows on their old numbers — which can collide with the ones just assigned — and a duplicated id would
    /// silently drop another row from the ordering, so both are the same mistake and share one error.
    /// </summary>
    public static Result EnsureIsCompletePermutation(
        IReadOnlyCollection<Guid> orderedPublicIds,
        IReadOnlyCollection<Guid> persistedPublicIds,
        Error incompleteError)
    {
        if (orderedPublicIds.Count != persistedPublicIds.Count)
        {
            return Result.Failure(incompleteError);
        }

        var submitted = new HashSet<Guid>(orderedPublicIds);
        if (submitted.Count != orderedPublicIds.Count)
        {
            // A duplicate collapsed the set: the request cannot be a permutation.
            return Result.Failure(incompleteError);
        }

        return submitted.SetEquals(persistedPublicIds)
            ? Result.Success()
            : Result.Failure(incompleteError);
    }

    /// <summary>
    /// The first of the two write phases needed when the order column carries a UNIQUE index. EF issues one
    /// UPDATE per row inside the transaction and a non-deferrable unique index is checked per statement, so
    /// assigning the final numbers directly makes the intermediate state violate it — a straight swap being the
    /// smallest example. This parks every row in a band strictly above both the current maximum and the highest
    /// number about to be assigned, so neither phase can collide.
    /// </summary>
    public static int ResolveStagingBandStart(int currentMaxOrder, int itemCount) =>
        Math.Max(currentMaxOrder, OrderAt(itemCount - 1)) + 1;
}
