namespace CLARIHR.Application.Common.CQRS;

/// <summary>
/// H-34 — the result of deleting a CHILD of an aggregate: nothing. The endpoints used to answer
/// <c>{ parentConcurrencyToken }</c>, documented as the parent's "updated" token so the caller could keep mutating
/// without another round-trip. Across the 53 endpoints that returned it the promise held in 29 and was false in 24
/// — the job-profile aggregate never rotates its token on a child write, and 14 of the personnel-file sections do
/// not either — so the same field meant two different things depending on which module answered.
/// <para>
/// Concurrency in these modules is per CHILD: the DELETE's own <c>If-Match</c> carries the CHILD's token, and the
/// parent's was never needed. So the endpoints answer <c>204 No Content</c> and this type carries no data; it
/// exists only because the command dispatcher is generic over a response type.
/// </para>
/// </summary>
public sealed record ChildDeletionResult
{
    public static readonly ChildDeletionResult Instance = new();
}
