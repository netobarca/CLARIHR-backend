using CLARIHR.Domain.Files;

namespace CLARIHR.Application.Abstractions.Files;

public interface IFileStorageProviderResolver
{
    IFileStorageProvider Resolve(StorageProvider provider);
}
