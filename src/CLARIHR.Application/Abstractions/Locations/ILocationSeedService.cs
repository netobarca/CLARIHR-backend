namespace CLARIHR.Application.Abstractions.Locations;

public interface ILocationSeedService
{
    Task InitializeDefaultsAsync(Guid tenantId, string countryCode, CancellationToken cancellationToken);
}
