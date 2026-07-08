using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Leave;

/// <summary>
/// Company-managed master of medical clinics ("clínicas médicas") referenced by incapacity records.
/// The optional <see cref="SectorCode"/> points to the country-scoped clinic-sectors general catalog
/// (ISSS / pública / privada); it is validated against the catalog in the handler — the entity only
/// stores the normalized code.
/// </summary>
public sealed class MedicalClinic : TenantEntity
{
    public const int MaxDescriptionLength = 200;
    public const int MaxSpecialtyLength = 150;
    public const int MaxSectorCodeLength = 80;

    private MedicalClinic()
    {
    }

    private MedicalClinic(
        Guid publicId,
        string description,
        string? specialty,
        string? sectorCode)
    {
        PublicId = publicId;
        SetDescription(description);
        SetSpecialty(specialty);
        SetSectorCode(sectorCode);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Description { get; private set; } = string.Empty;

    public string NormalizedDescription { get; private set; } = string.Empty;

    public string? Specialty { get; private set; }

    public string? SectorCode { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static MedicalClinic Create(
        string description,
        string? specialty,
        string? sectorCode) =>
        new(
            Guid.NewGuid(),
            description,
            specialty,
            sectorCode);

    public void Update(
        string description,
        string? specialty,
        string? sectorCode)
    {
        SetDescription(description);
        SetSpecialty(specialty);
        SetSectorCode(sectorCode);
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
        RefreshConcurrencyToken();
    }

    private void SetDescription(string description)
    {
        Description = LeaveNormalization.Clean(description, nameof(description));
        if (Description.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"Description must be {MaxDescriptionLength} characters or fewer.", nameof(description));
        }

        NormalizedDescription = LeaveNormalization.NormalizeName(Description);
    }

    private void SetSpecialty(string? specialty)
    {
        var cleaned = LeaveNormalization.CleanOptional(specialty);
        if (cleaned is { Length: > MaxSpecialtyLength })
        {
            throw new ArgumentException($"Specialty must be {MaxSpecialtyLength} characters or fewer.", nameof(specialty));
        }

        Specialty = cleaned;
    }

    private void SetSectorCode(string? sectorCode)
    {
        var cleaned = LeaveNormalization.CleanOptional(sectorCode);
        if (cleaned is not null)
        {
            cleaned = cleaned.ToUpperInvariant();
            if (cleaned.Length > MaxSectorCodeLength)
            {
                throw new ArgumentException($"Sector code must be {MaxSectorCodeLength} characters or fewer.", nameof(sectorCode));
            }
        }

        SectorCode = cleaned;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
