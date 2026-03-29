using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.CompetencyFramework;

public sealed class OccupationalPyramidLevel : TenantEntity
{
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
        Description = CompetencyFrameworkNormalization.CleanOptional(description);
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
        Description = CompetencyFrameworkNormalization.CleanOptional(description);
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
        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = CompetencyFrameworkNormalization.Clean(name, nameof(name));
        NormalizedName = CompetencyFrameworkNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
