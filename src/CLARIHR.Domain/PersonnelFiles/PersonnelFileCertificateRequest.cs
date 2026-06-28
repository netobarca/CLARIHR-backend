using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Canonical certificate-request status codes. Hybrid model (D-04/D-08): the status is validated against the
/// country-scoped <c>CertificateRequestStatus</c> catalog, but the linear lifecycle reasons over these
/// constants (SOLICITADA → EN_PROCESO → EMITIDA → ENTREGADA, plus RECHAZADA / ANULADA).
/// </summary>
public static class CertificateRequestStatuses
{
    public const string Solicitada = "SOLICITADA";
    public const string EnProceso = "EN_PROCESO";
    public const string Emitida = "EMITIDA";
    public const string Entregada = "ENTREGADA";
    public const string Rechazada = "RECHAZADA";
    public const string Anulada = "ANULADA";

    /// <summary>Pending states from which a request may be processed, issued, rejected or cancelled.</summary>
    public static readonly IReadOnlyCollection<string> Pending = new[] { Solicitada, EnProceso };
}

/// <summary>
/// Canonical certificate type codes (seeded for SV). The two salary-printing types (salario, embajada) require
/// the <c>ViewCompensation</c> permission at issuance and a salary value in the merge data (D-20).
/// </summary>
public static class CertificateTypes
{
    public const string Salario = "CONSTANCIA_SALARIO";
    public const string Laboral = "CONSTANCIA_LABORAL";
    public const string Embajada = "CONSTANCIA_EMBAJADA";
    public const string TiempoLaborado = "CONSTANCIA_TIEMPO_LABORADO";
    public const string NoDescuento = "CONSTANCIA_NO_DESCUENTO";
    public const string Recomendacion = "CARTA_RECOMENDACION";

    /// <summary>Types whose generated certificate prints salary → require ViewCompensation + salary data (D-20).</summary>
    public static readonly IReadOnlyCollection<string> PrintsSalary = new[] { Salario, Embajada };
}

/// <summary>
/// Employee certificate request ("solicitud de constancia": salario / laboral / embajada / tiempo laborado /
/// no descuento / recomendación). Self-service intake (D-02); RR. HH. processes and issues the generated PDF
/// (D-04/D-15). No money is involved (D-03). The status is a country-scoped catalog code (hybrid with the
/// canonical codes in <see cref="CertificateRequestStatuses"/>).
/// </summary>
public sealed class PersonnelFileCertificateRequest : TenantEntity
{
    private PersonnelFileCertificateRequest()
    {
    }

    private PersonnelFileCertificateRequest(
        string certificateTypeCode,
        string? typeNameSnapshot,
        string purposeCode,
        string? addressedTo,
        string deliveryMethodCode,
        string languageCode,
        int copies,
        DateTime requestDateUtc,
        DateTime? neededByDateUtc,
        Guid requestedByUserId)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        RequestStatusCode = CertificateRequestStatuses.Solicitada;
        RequestDateUtc = PersonnelFileNormalization.NormalizeDate(requestDateUtc);
        NeededByDateUtc = neededByDateUtc.HasValue ? PersonnelFileNormalization.NormalizeDate(neededByDateUtc.Value) : null;
        Copies = copies < 1 ? 1 : copies;
        LanguageCode = NormalizeLanguage(languageCode);
        RequestedByUserId = requestedByUserId;
        ApplyRequestFields(certificateTypeCode, typeNameSnapshot, purposeCode, addressedTo, deliveryMethodCode);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public string CertificateTypeCode { get; private set; } = string.Empty;

    public string? TypeNameSnapshot { get; private set; }

    public string RequestStatusCode { get; private set; } = CertificateRequestStatuses.Solicitada;

    public string PurposeCode { get; private set; } = string.Empty;

    public string? AddressedTo { get; private set; }

    public string DeliveryMethodCode { get; private set; } = string.Empty;

    public string LanguageCode { get; private set; } = "es";

    public int Copies { get; private set; } = 1;

    public DateTime RequestDateUtc { get; private set; }

    public DateTime? NeededByDateUtc { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public Guid? IssuedByUserId { get; private set; }

    public DateTime? IssuedDateUtc { get; private set; }

    public DateTime? DeliveredDateUtc { get; private set; }

    public string? ResolutionNotes { get; private set; }

    public int? ResponseTimeDays { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ConcurrencyToken = Guid.NewGuid();
    }

    public static PersonnelFileCertificateRequest Create(
        string certificateTypeCode,
        string? typeNameSnapshot,
        string purposeCode,
        string? addressedTo,
        string deliveryMethodCode,
        string languageCode,
        int copies,
        DateTime requestDateUtc,
        DateTime? neededByDateUtc,
        Guid requestedByUserId) =>
        new(certificateTypeCode, typeNameSnapshot, purposeCode, addressedTo, deliveryMethodCode, languageCode, copies, requestDateUtc, neededByDateUtc, requestedByUserId);

    /// <summary>Edits the request's business fields (RR. HH.); does not change status or issuance.</summary>
    public void Update(
        string certificateTypeCode,
        string? typeNameSnapshot,
        string purposeCode,
        string? addressedTo,
        string deliveryMethodCode,
        string languageCode,
        int copies,
        DateTime? neededByDateUtc)
    {
        Copies = copies < 1 ? 1 : copies;
        LanguageCode = NormalizeLanguage(languageCode);
        NeededByDateUtc = neededByDateUtc.HasValue ? PersonnelFileNormalization.NormalizeDate(neededByDateUtc.Value) : null;
        ConcurrencyToken = Guid.NewGuid();
        ApplyRequestFields(certificateTypeCode, typeNameSnapshot, purposeCode, addressedTo, deliveryMethodCode);
    }

    /// <summary>SOLICITADA → EN_PROCESO.</summary>
    public void StartProcessing()
    {
        if (!CertificateRequestStatuses.Pending.Contains(RequestStatusCode))
        {
            throw new InvalidOperationException("Only a pending certificate request can be processed.");
        }

        RequestStatusCode = CertificateRequestStatuses.EnProceso;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Issues the certificate (D-04): a pending request transitions to EMITIDA. The handler generates the PDF and
    /// persists the document BEFORE calling this; here we record the issuer, date and derived response time.
    /// </summary>
    public void Issue(Guid issuedByUserId, DateTime issuedAtUtc, string? notes)
    {
        if (!CertificateRequestStatuses.Pending.Contains(RequestStatusCode))
        {
            throw new InvalidOperationException("Only a pending certificate request can be issued.");
        }

        RequestStatusCode = CertificateRequestStatuses.Emitida;
        IssuedByUserId = issuedByUserId;
        IssuedDateUtc = PersonnelFileNormalization.NormalizeDate(issuedAtUtc);
        ResolutionNotes = PersonnelFileNormalization.CleanOptional(notes);
        ResponseTimeDays = DeriveResponseTimeDays(RequestDateUtc, IssuedDateUtc);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>EMITIDA → ENTREGADA. The coherence check (delivered ≥ issued) is enforced by the handler.</summary>
    public void Deliver(DateTime deliveredAtUtc)
    {
        if (RequestStatusCode != CertificateRequestStatuses.Emitida)
        {
            throw new InvalidOperationException("Only an issued certificate request can be delivered.");
        }

        DeliveredDateUtc = PersonnelFileNormalization.NormalizeDate(deliveredAtUtc);
        RequestStatusCode = CertificateRequestStatuses.Entregada;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Rejects a pending request (RR. HH.).</summary>
    public void Reject(string? notes)
    {
        if (!CertificateRequestStatuses.Pending.Contains(RequestStatusCode))
        {
            throw new InvalidOperationException("Only a pending certificate request can be rejected.");
        }

        RequestStatusCode = CertificateRequestStatuses.Rechazada;
        ResolutionNotes = PersonnelFileNormalization.CleanOptional(notes);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Cancels a still-pending request (self-service for the owner, or RR. HH.).</summary>
    public void Cancel()
    {
        if (!CertificateRequestStatuses.Pending.Contains(RequestStatusCode))
        {
            throw new InvalidOperationException("Only a pending certificate request can be cancelled.");
        }

        RequestStatusCode = CertificateRequestStatuses.Anulada;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Derived response time in days = issued − request (null when unissued or incoherent).</summary>
    public static int? DeriveResponseTimeDays(DateTime requestDateUtc, DateTime? issuedDateUtc) =>
        issuedDateUtc is { } issued && issued.Date >= requestDateUtc.Date
            ? (int)(issued.Date - requestDateUtc.Date).TotalDays
            : null;

    private void ApplyRequestFields(
        string certificateTypeCode,
        string? typeNameSnapshot,
        string purposeCode,
        string? addressedTo,
        string deliveryMethodCode)
    {
        CertificateTypeCode = PersonnelFileNormalization.Clean(certificateTypeCode, nameof(certificateTypeCode)).ToUpperInvariant();
        TypeNameSnapshot = PersonnelFileNormalization.CleanOptional(typeNameSnapshot);
        PurposeCode = PersonnelFileNormalization.Clean(purposeCode, nameof(purposeCode)).ToUpperInvariant();
        AddressedTo = PersonnelFileNormalization.CleanOptional(addressedTo);
        DeliveryMethodCode = PersonnelFileNormalization.Clean(deliveryMethodCode, nameof(deliveryMethodCode)).ToUpperInvariant();
    }

    private static string NormalizeLanguage(string? languageCode) =>
        string.Equals(languageCode?.Trim(), "en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";
}

/// <summary>
/// The issued (or manually uploaded) document of a certificate request (D-05). <see cref="IsSystemGenerated"/>
/// distinguishes the system-generated PDF from a manual override. Reuses the shared file subsystem
/// (<c>StoredFile</c> / <c>IFileStorageProvider</c> / <c>FilePurpose.CertificateRequestDocument</c>) via a loose
/// reference (<see cref="FilePublicId"/>, no FK).
/// </summary>
public sealed class CertificateRequestDocument : TenantEntity
{
    private CertificateRequestDocument()
    {
    }

    private CertificateRequestDocument(
        Guid publicId,
        bool isSystemGenerated,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations)
    {
        if (filePublicId == Guid.Empty)
        {
            throw new ArgumentException("File public id must not be empty.", nameof(filePublicId));
        }

        PublicId = publicId;
        IsSystemGenerated = isSystemGenerated;
        FilePublicId = filePublicId;
        FileName = PersonnelFileNormalization.Clean(fileName, nameof(fileName));
        ContentType = PersonnelFileNormalization.Clean(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        Observations = PersonnelFileNormalization.CleanOptional(observations);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long CertificateRequestId { get; private set; }

    public PersonnelFileCertificateRequest CertificateRequest { get; private set; } = null!;

    public bool IsSystemGenerated { get; private set; }

    public Guid FilePublicId { get; private set; }

    public string? Observations { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToCertificateRequest(long certificateRequestId) => CertificateRequestId = certificateRequestId;

    public static CertificateRequestDocument Create(
        Guid publicId,
        bool isSystemGenerated,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations) =>
        new(publicId, isSystemGenerated, filePublicId, fileName, contentType, sizeBytes, observations);

    public void Inactivate()
    {
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }
}
