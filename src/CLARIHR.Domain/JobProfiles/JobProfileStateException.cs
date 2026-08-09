namespace CLARIHR.Domain.JobProfiles;

/// <summary>
/// H-01 — a job profile state invariant was violated: the descriptor is frozen (published) or archived,
/// or an illegal status transition was attempted.
/// <para>
/// Derives from <see cref="InvalidOperationException"/> on purpose so every existing
/// <c>catch (InvalidOperationException)</c> in the application layer keeps working; the distinct type
/// only lets those handlers map it to a precise 422 <c>JOB_PROFILE_STATE_RULE_VIOLATION</c> instead of a
/// generic conflict carrying the raw domain message.
/// </para>
/// </summary>
public sealed class JobProfileStateException(string message) : InvalidOperationException(message);
