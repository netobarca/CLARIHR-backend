using CLARIHR.Application.Abstractions.Time;

namespace CLARIHR.Infrastructure.Time;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
