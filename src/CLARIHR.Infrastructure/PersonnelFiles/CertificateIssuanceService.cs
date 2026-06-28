using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Infrastructure.Reports.Documents;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

/// <summary>
/// Generates the certificate PDF (D-15), uploads it server-side to blob storage and registers it as the issued
/// <see cref="CertificateRequestDocument"/> (system-generated). A previous system-generated document for the
/// same request is deactivated (re-issue keeps history). Entities are added to the unit of work; the calling
/// handler commits them with the status transition.
/// </summary>
internal sealed class CertificateIssuanceService(
    ApplicationDbContext dbContext,
    IFileStorageProviderResolver providerResolver,
    IFilePurposeRuleProvider ruleProvider,
    ICertificateDocumentRenderer renderer) : ICertificateIssuanceService
{
    public async Task GenerateAndStoreAsync(
        Guid tenantId,
        long certificateRequestInternalId,
        Guid certificateRequestPublicId,
        CertificatePrintPayload payload,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        var logoBytes = await ResolveLogoBytesAsync(payload.LogoFilePublicId, cancellationToken);

        using var pdfStream = new MemoryStream();
        await renderer.RenderAsync(payload, logoBytes, pdfStream, cancellationToken);
        pdfStream.Position = 0;

        var rule = ruleProvider.GetRule(FilePurpose.CertificateRequestDocument)
            ?? throw new InvalidOperationException("CertificateRequestDocument file purpose is not configured.");
        var container = rule.ContainerOverride ?? "clarihr-files";
        var fileName = $"constancia-{certificateRequestPublicId:D}.pdf";
        var objectKey = $"tenants/{tenantId:D}/certificate-requests/{certificateRequestPublicId:D}/{fileName}";

        var provider = providerResolver.Resolve(rule.DefaultProvider);
        var artifact = await provider.UploadStreamAsync(container, objectKey, "application/pdf", pdfStream, cancellationToken);

        var stored = StoredFile.Create(
            fileName,
            "application/pdf",
            artifact.SizeBytes,
            ".pdf",
            rule.DefaultProvider,
            container,
            objectKey,
            FilePurpose.CertificateRequestDocument,
            FileUploadType.ServerSideUpload,
            createdByUserId,
            entityId: certificateRequestPublicId);
        stored.MarkActive(artifact.SizeBytes, "application/pdf");
        await dbContext.Set<StoredFile>().AddAsync(stored, cancellationToken);

        // Re-issue keeps history: deactivate the previous system-generated document, if any.
        var previous = await dbContext.Set<CertificateRequestDocument>()
            .Where(document => document.CertificateRequestId == certificateRequestInternalId && document.IsActive && document.IsSystemGenerated)
            .ToListAsync(cancellationToken);
        foreach (var document in previous)
        {
            document.Inactivate();
        }

        var issuedDocument = CertificateRequestDocument.Create(
            Guid.NewGuid(),
            isSystemGenerated: true,
            stored.PublicId,
            fileName,
            "application/pdf",
            (int)artifact.SizeBytes,
            observations: null);
        issuedDocument.BindToCertificateRequest(certificateRequestInternalId);
        issuedDocument.SetTenantId(tenantId);
        await dbContext.Set<CertificateRequestDocument>().AddAsync(issuedDocument, cancellationToken);
    }

    private async Task<byte[]?> ResolveLogoBytesAsync(Guid? logoFilePublicId, CancellationToken cancellationToken)
    {
        if (logoFilePublicId is not { } logoId || logoId == Guid.Empty)
        {
            return null;
        }

        var logo = await dbContext.Set<StoredFile>()
            .AsNoTracking()
            .Where(file => file.PublicId == logoId && file.Status == FileStatus.Active)
            .Select(file => new { file.Provider, file.ContainerName, file.ObjectKey })
            .FirstOrDefaultAsync(cancellationToken);
        if (logo is null)
        {
            return null;
        }

        var provider = providerResolver.Resolve(logo.Provider);
        await using var stream = await provider.OpenReadStreamAsync(logo.ContainerName, logo.ObjectKey, cancellationToken);
        if (stream is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
