using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class Company : AuditableEntity
{
    private Company()
    {
    }

    private Company(
        Guid publicId,
        string name,
        string slug,
        CompanyStatus status,
        Guid createdByUserPublicId)
    {
        if (createdByUserPublicId == Guid.Empty)
        {
            throw new ArgumentException("Created by user id cannot be empty.", nameof(createdByUserPublicId));
        }

        PublicId = publicId;
        Name = CompanyNormalization.Clean(name, nameof(name));
        Slug = CompanyNormalization.NormalizeSlug(slug);
        Status = status;
        CreatedByUserPublicId = createdByUserPublicId;
    }

    public Guid PublicId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public CompanyStatus Status { get; private set; }

    public Guid CreatedByUserPublicId { get; private set; }

    public static Company Create(string name, string slug, Guid createdByUserPublicId) =>
        new(
            Guid.NewGuid(),
            name,
            slug,
            CompanyStatus.Active,
            createdByUserPublicId);
}
