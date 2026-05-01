using CLARIHR.Domain.Files;

namespace CLARIHR.Application.Abstractions.Files;

public interface IFilePurposeRuleProvider
{
    FilePurposeRule? GetRule(FilePurpose purpose);
}

public sealed record FilePurposeRule(
    long MaxSizeBytes,
    IReadOnlyCollection<string> AllowedContentTypes,
    IReadOnlyCollection<string> AllowedExtensions,
    StorageProvider DefaultProvider,
    bool RequiresMalwareScan,
    string? ContainerOverride);
