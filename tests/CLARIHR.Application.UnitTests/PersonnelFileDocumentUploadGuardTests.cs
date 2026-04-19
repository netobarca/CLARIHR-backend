using CLARIHR.Api.Common;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Http;

namespace CLARIHR.Application.UnitTests;

public sealed class PersonnelFileDocumentUploadGuardTests
{
    [Fact]
    public async Task ValidateAsync_WhenFileIsMissingOrEmpty_ShouldReturnRequiredError()
    {
        var missingResult = await PersonnelFileDocumentUploadGuard.ValidateAsync(null, CancellationToken.None);
        var emptyResult = await PersonnelFileDocumentUploadGuard.ValidateAsync(
            CreateFormFile([], "empty.pdf", "application/pdf"),
            CancellationToken.None);

        Assert.Equal(PersonnelFileErrors.DocumentFileRequired.Code, missingResult.Code);
        Assert.Equal(PersonnelFileErrors.DocumentFileRequired.Code, emptyResult.Code);
    }

    [Fact]
    public async Task ValidateAsync_WhenFileExceedsLimit_ShouldReturnTooLargeErrorWithoutReadingStream()
    {
        var file = new FormFile(
            Stream.Null,
            baseStreamOffset: 0,
            length: PersonnelFileValidationRules.MaxDocumentFileSizeBytes + 1L,
            name: "file",
            fileName: "large.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await PersonnelFileDocumentUploadGuard.ValidateAsync(file, CancellationToken.None);

        Assert.Equal(PersonnelFileErrors.DocumentFileTooLarge.Code, result.Code);
    }

    [Fact]
    public async Task ValidateAsync_WhenExtensionOrMimeTypeIsUnsupported_ShouldReturnUnsupportedError()
    {
        var unsupportedExtension = await PersonnelFileDocumentUploadGuard.ValidateAsync(
            CreateFormFile("%PDF-"u8.ToArray(), "payload.exe", "application/pdf"),
            CancellationToken.None);
        var unsupportedMime = await PersonnelFileDocumentUploadGuard.ValidateAsync(
            CreateFormFile("%PDF-"u8.ToArray(), "document.pdf", "application/octet-stream"),
            CancellationToken.None);

        Assert.Equal(PersonnelFileErrors.DocumentContentTypeUnsupported.Code, unsupportedExtension.Code);
        Assert.Equal(PersonnelFileErrors.DocumentContentTypeUnsupported.Code, unsupportedMime.Code);
    }

    [Fact]
    public async Task ValidateAsync_WhenSignatureDoesNotMatchDeclaredType_ShouldReturnUnsupportedError()
    {
        var result = await PersonnelFileDocumentUploadGuard.ValidateAsync(
            CreateFormFile("not-a-pdf"u8.ToArray(), "document.pdf", "application/pdf"),
            CancellationToken.None);

        Assert.Equal(PersonnelFileErrors.DocumentContentTypeUnsupported.Code, result.Code);
    }

    [Theory]
    [InlineData("document.pdf", "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D })]
    [InlineData("photo.jpg", "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })]
    [InlineData("photo.png", "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    [InlineData("document.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", new byte[] { 0x50, 0x4B, 0x03, 0x04 })]
    public async Task ValidateAsync_WhenAllowedFileMatchesSignature_ShouldSucceed(
        string fileName,
        string contentType,
        byte[] signature)
    {
        var result = await PersonnelFileDocumentUploadGuard.ValidateAsync(
            CreateFormFile(signature, fileName, contentType),
            CancellationToken.None);

        Assert.Equal(Error.None, result);
    }

    private static FormFile CreateFormFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, baseStreamOffset: 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
