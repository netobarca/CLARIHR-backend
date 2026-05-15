namespace CLARIHR.Application.Abstractions.Persistence;

public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException(string? constraintName, Exception innerException)
        : base($"Unique constraint '{constraintName ?? "<unknown>"}' was violated.", innerException)
    {
        ConstraintName = constraintName;
    }

    public string? ConstraintName { get; }
}
