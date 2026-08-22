using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.LegalRepresentatives;

public sealed class LegalRepresentative : TenantEntity
{
    private LegalRepresentative()
    {
    }

    private LegalRepresentative(
        Guid publicId,
        string firstName,
        string lastName,
        string documentType,
        string documentNumber,
        string positionTitle,
        LegalRepresentativeRepresentationType representationType,
        string? authorityDescription,
        string? appointmentInstrument,
        DateOnly? appointmentDate,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        string? email,
        string? phone,
        bool isPrimary)
    {
        PublicId = publicId;
        SetName(firstName, lastName);
        SetDocument(documentType, documentNumber);
        PositionTitle = LegalRepresentativeNormalization.Clean(positionTitle, nameof(positionTitle));
        RepresentationType = representationType;
        AuthorityDescription = LegalRepresentativeNormalization.CleanOptional(authorityDescription);
        AppointmentInstrument = LegalRepresentativeNormalization.CleanOptional(appointmentInstrument);
        AppointmentDate = appointmentDate;
        SetEffectiveDates(effectiveFrom, effectiveTo);
        Email = LegalRepresentativeNormalization.CleanOptional(email);
        Phone = LegalRepresentativeNormalization.CleanOptional(phone);
        IsPrimary = isPrimary;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string NormalizedFullName { get; private set; } = string.Empty;

    public string DocumentType { get; private set; } = string.Empty;

    public string DocumentNumber { get; private set; } = string.Empty;

    public string NormalizedDocumentNumber { get; private set; } = string.Empty;

    public string PositionTitle { get; private set; } = string.Empty;

    public LegalRepresentativeRepresentationType RepresentationType { get; private set; }

    public string? AuthorityDescription { get; private set; }

    public string? AppointmentInstrument { get; private set; }

    public DateOnly? AppointmentDate { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public bool IsPrimary { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static LegalRepresentative Create(
        string firstName,
        string lastName,
        string documentType,
        string documentNumber,
        string positionTitle,
        LegalRepresentativeRepresentationType representationType,
        string? authorityDescription,
        string? appointmentInstrument,
        DateOnly? appointmentDate,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        string? email,
        string? phone,
        bool isPrimary) =>
        new(
            Guid.NewGuid(),
            firstName,
            lastName,
            documentType,
            documentNumber,
            positionTitle,
            representationType,
            authorityDescription,
            appointmentInstrument,
            appointmentDate,
            effectiveFrom,
            effectiveTo,
            email,
            phone,
            isPrimary);

    public void Update(
        string firstName,
        string lastName,
        string documentType,
        string documentNumber,
        string positionTitle,
        LegalRepresentativeRepresentationType representationType,
        string? authorityDescription,
        string? appointmentInstrument,
        DateOnly? appointmentDate,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        string? email,
        string? phone,
        bool isPrimary)
    {
        SetName(firstName, lastName);
        SetDocument(documentType, documentNumber);
        PositionTitle = LegalRepresentativeNormalization.Clean(positionTitle, nameof(positionTitle));
        RepresentationType = representationType;
        AuthorityDescription = LegalRepresentativeNormalization.CleanOptional(authorityDescription);
        AppointmentInstrument = LegalRepresentativeNormalization.CleanOptional(appointmentInstrument);
        AppointmentDate = appointmentDate;
        SetEffectiveDates(effectiveFrom, effectiveTo);
        Email = LegalRepresentativeNormalization.CleanOptional(email);
        Phone = LegalRepresentativeNormalization.CleanOptional(phone);
        IsPrimary = isPrimary;
        RefreshConcurrencyToken();
    }

    public void SetPrimary()
    {
        IsPrimary = true;
        RefreshConcurrencyToken();
    }

    public void ClearPrimary()
    {
        if (!IsPrimary)
        {
            return;
        }

        IsPrimary = false;
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        IsActive = false;
        IsPrimary = false;
        RefreshConcurrencyToken();
    }

    private void SetName(string firstName, string lastName)
    {
        FirstName = LegalRepresentativeNormalization.Clean(firstName, nameof(firstName));
        LastName = LegalRepresentativeNormalization.Clean(lastName, nameof(lastName));
        FullName = $"{FirstName} {LastName}";
        NormalizedFullName = LegalRepresentativeNormalization.NormalizeName(FullName);
    }

    private void SetDocument(string documentType, string documentNumber)
    {
        DocumentType = LegalRepresentativeNormalization.Clean(documentType, nameof(documentType)).ToUpperInvariant();
        DocumentNumber = LegalRepresentativeNormalization.Clean(documentNumber, nameof(documentNumber));
        NormalizedDocumentNumber = LegalRepresentativeNormalization.NormalizeDocumentNumber(documentNumber);
    }

    /// <summary>
    /// B-02 — las tres fechas son <see cref="DateOnly"/>: un día no tiene hora ni zona, así que aquí ya no hay
    /// nada que normalizar. Antes había que truncar con <c>.Date</c> y etiquetar el <c>Kind</c>, y era donde se
    /// colaba el corrimiento de F-03. El tipo lo vuelve imposible por construcción.
    /// </summary>
    private void SetEffectiveDates(DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        if (effectiveTo.HasValue && effectiveTo.Value < effectiveFrom)
        {
            throw new InvalidOperationException("EffectiveTo cannot be earlier than EffectiveFrom.");
        }

        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
