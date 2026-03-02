using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Locations;

public sealed class WorkCenter : TenantEntity
{
    private WorkCenter()
    {
    }

    private WorkCenter(
        Guid publicId,
        string code,
        string name,
        long workCenterTypeId,
        long locationGroupId,
        string? address,
        decimal? geoLat,
        decimal? geoLong,
        string? phone,
        string? email,
        string? notes)
    {
        if (workCenterTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workCenterTypeId), "Work center type id must be greater than zero.");
        }

        if (locationGroupId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(locationGroupId), "Location group id must be greater than zero.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        WorkCenterTypeId = workCenterTypeId;
        LocationGroupId = locationGroupId;
        Address = LocationNormalization.CleanOptional(address);
        GeoLat = geoLat;
        GeoLong = geoLong;
        Phone = LocationNormalization.CleanOptional(phone);
        Email = LocationNormalization.NormalizeOptionalEmail(email);
        Notes = LocationNormalization.CleanOptional(notes);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid PublicId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public long WorkCenterTypeId { get; private set; }

    public long LocationGroupId { get; private set; }

    public string? Address { get; private set; }

    public decimal? GeoLat { get; private set; }

    public decimal? GeoLong { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static WorkCenter Create(
        string code,
        string name,
        long workCenterTypeId,
        long locationGroupId,
        string? address,
        decimal? geoLat,
        decimal? geoLong,
        string? phone,
        string? email,
        string? notes) =>
        new(Guid.NewGuid(), code, name, workCenterTypeId, locationGroupId, address, geoLat, geoLong, phone, email, notes);

    public void Update(
        string code,
        string name,
        long workCenterTypeId,
        long locationGroupId,
        string? address,
        decimal? geoLat,
        decimal? geoLong,
        string? phone,
        string? email,
        string? notes)
    {
        if (workCenterTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workCenterTypeId), "Work center type id must be greater than zero.");
        }

        if (locationGroupId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(locationGroupId), "Location group id must be greater than zero.");
        }

        SetCode(code);
        SetName(name);
        WorkCenterTypeId = workCenterTypeId;
        LocationGroupId = locationGroupId;
        Address = LocationNormalization.CleanOptional(address);
        GeoLat = geoLat;
        GeoLong = geoLong;
        Phone = LocationNormalization.CleanOptional(phone);
        Email = LocationNormalization.NormalizeOptionalEmail(email);
        Notes = LocationNormalization.CleanOptional(notes);
        RefreshConcurrencyToken();
    }

    public void ReassignGroup(long locationGroupId)
    {
        if (locationGroupId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(locationGroupId), "Location group id must be greater than zero.");
        }

        LocationGroupId = locationGroupId;
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

    private void SetCode(string code)
    {
        Code = LocationNormalization.Clean(code, nameof(code));
        NormalizedCode = LocationNormalization.NormalizeCode(code);
    }

    private void SetName(string name)
    {
        Name = LocationNormalization.Clean(name, nameof(name));
        NormalizedName = LocationNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
