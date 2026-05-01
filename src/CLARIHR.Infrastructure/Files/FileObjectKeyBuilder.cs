using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Domain.Files;

namespace CLARIHR.Infrastructure.Files;

internal sealed class FileObjectKeyBuilder : IFileObjectKeyBuilder
{
    public string Build(FilePurpose purpose, Guid tenantId, Guid userId, Guid fileId, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        if (fileId == Guid.Empty)
        {
            throw new ArgumentException("FileId cannot be empty.", nameof(fileId));
        }

        // Guard against path traversal
        var normalizedExtension = extension.Replace("..", string.Empty).Replace("/", string.Empty).Replace("\\", string.Empty);

        var purposeSegment = purpose switch
        {
            FilePurpose.ProfileImage => "profile",
            FilePurpose.PersonnelDocument => "documents",
            FilePurpose.ReportExport => "reports",
            FilePurpose.CompanyLogo => "logos",
            FilePurpose.Attachment => "attachments",
            _ => "general"
        };

        return $"tenants/{tenantId:D}/users/{userId:D}/{purposeSegment}/{fileId:D}{normalizedExtension}";
    }
}
