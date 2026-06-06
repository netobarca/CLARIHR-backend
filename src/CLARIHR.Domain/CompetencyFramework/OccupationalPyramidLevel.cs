using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.CompetencyFramework;

public sealed class OccupationalPyramidLevel : TenantEntity
{
    // Domain-owned length invariants (single source of truth): the application-layer validators and
    // JSON Patch appliers reference these, and the EF column lengths mirror them. The guards below are
    // a defensive backstop — the application layer rejects out-of-range input first (400), so they only
    // fire on a layering bug, turning a DB length violation (500) into a clear domain ArgumentException.
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 500;

    private OccupationalPyramidLevel()
    {
    }

    private OccupationalPyramidLevel(Guid publicId, string code, string name, int levelOrder, string? description)
    {
        if (levelOrder <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(levelOrder), "Level order must be greater than zero.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        LevelOrder = levelOrder;
        SetDescription(description);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public int LevelOrder { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static OccupationalPyramidLevel Create(string code, string name, int levelOrder, string? description) =>
        new(Guid.NewGuid(), code, name, levelOrder, description);

    public void Update(string code, string name, int levelOrder, string? description)
    {
        if (levelOrder <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(levelOrder), "Level order must be greater than zero.");
        }

        SetCode(code);
        SetName(name);
        LevelOrder = levelOrder;
        SetDescription(description);
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
        Code = CompetencyFrameworkNormalization.NormalizeCode(code);
        if (Code.Length > MaxCodeLength)
        {
            throw new ArgumentException($"Code must be {MaxCodeLength} characters or fewer.", nameof(code));
        }

        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = CompetencyFrameworkNormalization.Clean(name, nameof(name));
        if (Name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));
        }

        NormalizedName = CompetencyFrameworkNormalization.NormalizeName(name);
    }

    private void SetDescription(string? description)
    {
        Description = CompetencyFrameworkNormalization.CleanOptional(description);
        if (Description is not null && Description.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"Description must be {MaxDescriptionLength} characters or fewer.", nameof(description));
        }
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
