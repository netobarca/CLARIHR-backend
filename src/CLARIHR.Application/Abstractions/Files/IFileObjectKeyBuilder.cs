using CLARIHR.Domain.Files;

namespace CLARIHR.Application.Abstractions.Files;

public interface IFileObjectKeyBuilder
{
    string Build(FilePurpose purpose, Guid tenantId, Guid userId, Guid fileId, string extension);
}
