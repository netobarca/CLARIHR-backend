using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Domain.Files;
using CLARIHR.Infrastructure.Files.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Files;

internal sealed class FilePurposeRuleProvider(
    IOptions<FileStorageOptions> options) : IFilePurposeRuleProvider
{
    private readonly IReadOnlyDictionary<string, FilePurposeOptions> _purposes = options.Value.Purposes;
    private readonly string _defaultProvider = options.Value.DefaultProvider;

    public FilePurposeRule? GetRule(FilePurpose purpose)
    {
        var purposeKey = purpose.ToString();

        if (!_purposes.TryGetValue(purposeKey, out var purposeOptions))
        {
            return null;
        }

        if (!Enum.TryParse<StorageProvider>(
                purposeOptions.DefaultProvider ?? _defaultProvider,
                ignoreCase: true,
                out var provider))
        {
            provider = StorageProvider.AzureBlob;
        }

        return new FilePurposeRule(
            purposeOptions.MaxSizeBytes,
            purposeOptions.AllowedContentTypes,
            purposeOptions.AllowedExtensions,
            provider,
            purposeOptions.RequiresMalwareScan,
            purposeOptions.ContainerOverride);
    }
}
