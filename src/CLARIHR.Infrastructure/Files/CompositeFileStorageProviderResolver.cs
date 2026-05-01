using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Domain.Files;

namespace CLARIHR.Infrastructure.Files;

internal sealed class CompositeFileStorageProviderResolver(
    IEnumerable<IFileStorageProvider> providers) : IFileStorageProviderResolver
{
    private readonly Dictionary<StorageProvider, IFileStorageProvider> _providerMap =
        providers.ToDictionary(p => p.ProviderType);

    public IFileStorageProvider Resolve(StorageProvider provider)
    {
        if (_providerMap.TryGetValue(provider, out var storageProvider))
        {
            return storageProvider;
        }

        throw new InvalidOperationException(
            $"No storage provider registered for '{provider}'. " +
            $"Available providers: {string.Join(", ", _providerMap.Keys)}.");
    }
}
