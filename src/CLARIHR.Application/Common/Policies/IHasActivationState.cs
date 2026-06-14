namespace CLARIHR.Application.Common.Policies;

/// <summary>
/// Optional marker exposing a response DTO's active/inactive state so the
/// AllowedActions resolver can derive activate/inactivate capabilities generically
/// (no per-resource state extractor needed for the common boolean case). Resources
/// whose activation state is computed differently (e.g. from a status enum) provide
/// a custom <c>StateExtractor</c> in the registry instead.
/// </summary>
public interface IHasActivationState
{
    bool IsActive { get; }
}
