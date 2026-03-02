namespace CLARIHR.Infrastructure.Configuration;

public sealed class CompanyOwnershipOptions
{
    public const string SectionName = "Companies:Ownership";

    public int MaxOwnedActiveCompanies { get; set; } = 2;
}
