namespace CLARIHR.Application.UnitTests;

public sealed class ReportExportGovernanceTests
{
    private static readonly string[] ForbiddenControllerPatterns =
    [
        "BuildCsv",
        "BuildXlsx",
        "BuildSimpleXlsx",
        "Encoding.UTF8.GetBytes(csv)"
    ];

    private static readonly HashSet<string> ExemptControllerFiles = new(StringComparer.Ordinal)
    {
        "JobProfilesController.cs"
    };

    [Fact]
    public void Controllers_ShouldNotReintroduceManualTableExportBuildersOutsideExemptions()
    {
        var controllersRoot = Path.Combine(FindRepositoryRoot(), "src", "CLARIHR.Api", "Controllers");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(controllersRoot, "*.cs", SearchOption.TopDirectoryOnly))
        {
            if (ExemptControllerFiles.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (ForbiddenControllerPatterns.Any(pattern => lines[index].Contains(pattern, StringComparison.Ordinal)))
                {
                    violations.Add($"{Path.GetFileName(file)}:{index + 1}");
                }
            }
        }

        Assert.Empty(violations);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "CLARIHR.Api")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test output path.");
    }
}
