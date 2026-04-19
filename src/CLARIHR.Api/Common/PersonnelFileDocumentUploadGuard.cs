using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Http;

namespace CLARIHR.Api.Common;

internal static class PersonnelFileDocumentUploadGuard
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    public static async Task<Error> ValidateAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return PersonnelFileErrors.DocumentFileRequired;
        }

        if (file.Length > PersonnelFileValidationRules.MaxDocumentFileSizeBytes)
        {
            return PersonnelFileErrors.DocumentFileTooLarge;
        }

        var safeFileName = GetSafeFileName(file);
        if (string.IsNullOrWhiteSpace(safeFileName) ||
            !PersonnelFileValidationRules.IsAllowedDocumentExtension(safeFileName) ||
            !PersonnelFileValidationRules.IsAllowedDocumentContentType(safeFileName, file.ContentType))
        {
            return PersonnelFileErrors.DocumentContentTypeUnsupported;
        }

        var header = new byte[Math.Min(8, (int)file.Length)];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);

        return HasAllowedSignature(safeFileName, header.AsSpan(0, bytesRead))
            ? Error.None
            : PersonnelFileErrors.DocumentContentTypeUnsupported;
    }

    public static string GetSafeFileName(IFormFile file) =>
        Path.GetFileName(file.FileName).Trim();

    private static bool HasAllowedSignature(string fileName, ReadOnlySpan<byte> header)
    {
        var extension = Path.GetExtension(fileName);
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => header.StartsWith(PdfSignature),
            ".jpg" or ".jpeg" => header.StartsWith(JpegSignature),
            ".png" => header.StartsWith(PngSignature),
            ".docx" => header.StartsWith(ZipSignature),
            _ => false
        };
    }
}
