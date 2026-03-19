namespace CLARIHR.Domain.LegalRepresentatives;

public static class LegalRepresentativePositionTitleCatalog
{
    public static IReadOnlyList<LegalRepresentativePositionTitleCatalogDefinition> Items { get; } =
    [
        new(1, "OWNER", "OWNER", 1),
        new(2, "CEO", "CEO", 2),
        new(3, "EXECUTIVE_MANAGEMENT", "Executive Management", 3),
        new(4, "HUMAN_RESOURCES", "Human Resources", 4),
        new(5, "FINANCE", "Finance", 5),
        new(6, "ACCOUNTING", "Accounting", 6),
        new(7, "OPERATIONS", "Operations", 7),
        new(8, "PROCUREMENT", "Procurement", 8),
        new(9, "SALES", "Sales", 9),
        new(10, "MARKETING", "Marketing", 10),
        new(11, "CUSTOMER_SERVICE", "Customer Service", 11),
        new(12, "INFORMATION_TECHNOLOGY", "Information Technology", 12),
        new(13, "SOFTWARE_DEVELOPMENT", "Software Development", 13),
        new(14, "INFRASTRUCTURE_DEVOPS", "Infrastructure / DevOps", 14),
        new(15, "DATA_ANALYTICS", "Data & Analytics", 15),
        new(16, "LEGAL", "Legal", 16),
        new(17, "ADMINISTRATION", "Administration", 17),
        new(18, "LOGISTICS", "Logistics", 18),
        new(19, "MAINTENANCE", "Maintenance", 19),
        new(20, "SECURITY", "Security", 20)
    ];
}

public sealed record LegalRepresentativePositionTitleCatalogDefinition(
    long Id,
    string Code,
    string Name,
    int SortOrder);
