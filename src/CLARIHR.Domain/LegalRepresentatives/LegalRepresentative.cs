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
        LegalRepresentativeDocumentType documentType,
        string documentNumber,
        string positionTitle,
        LegalRepresentativeRepresentationType representationType,
        string? authorityDescription,
        string? appointmentInstrument,
        DateTime? appointmentDateUtc,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
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
        AppointmentDateUtc = appointmentDateUtc;
        SetEffectiveDates(effectiveFromUtc, effectiveToUtc);
        Email = LegalRepresentativeNormalization.CleanOptional(email);
        Phone = LegalRepresentativeNormalization.CleanOptional(phone);
        IsPrimary = isPrimary;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid PublicId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string NormalizedFullName { get; private set; } = string.Empty;

    public LegalRepresentativeDocumentType DocumentType { get; private set; }

    public string DocumentNumber { get; private set; } = string.Empty;

    public string NormalizedDocumentNumber { get; private set; } = string.Empty;

    public string PositionTitle { get; private set; } = string.Empty;

    public LegalRepresentativeRepresentationType RepresentationType { get; private set; }

    public string? AuthorityDescription { get; private set; }

    public string? AppointmentInstrument { get; private set; }

    public DateTime? AppointmentDateUtc { get; private set; }

    public DateTime EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public bool IsPrimary { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static LegalRepresentative Create(
        string firstName,
        string lastName,
        LegalRepresentativeDocumentType documentType,
        string documentNumber,
        string positionTitle,
        LegalRepresentativeRepresentationType representationType,
        string? authorityDescription,
        string? appointmentInstrument,
        DateTime? appointmentDateUtc,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
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
            appointmentDateUtc,
            effectiveFromUtc,
            effectiveToUtc,
            email,
            phone,
            isPrimary);

    public void Update(
        string firstName,
        string lastName,
        LegalRepresentativeDocumentType documentType,
        string documentNumber,
        string positionTitle,
        LegalRepresentativeRepresentationType representationType,
        string? authorityDescription,
        string? appointmentInstrument,
        DateTime? appointmentDateUtc,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
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
        AppointmentDateUtc = appointmentDateUtc;
        SetEffectiveDates(effectiveFromUtc, effectiveToUtc);
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

    private void SetDocument(LegalRepresentativeDocumentType documentType, string documentNumber)
    {
        DocumentType = documentType;
        DocumentNumber = LegalRepresentativeNormalization.Clean(documentNumber, nameof(documentNumber));
        NormalizedDocumentNumber = LegalRepresentativeNormalization.NormalizeDocumentNumber(documentNumber);
    }

    private void SetEffectiveDates(DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        var from = effectiveFromUtc.Date;
        var to = effectiveToUtc?.Date;

        if (to.HasValue && to.Value < from)
        {
            throw new InvalidOperationException("EffectiveToUtc cannot be earlier than EffectiveFromUtc.");
        }

        EffectiveFromUtc = from;
        EffectiveToUtc = to;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
