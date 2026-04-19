namespace CLARIHR.Application.UnitTests;

public sealed class IgnoreQueryFiltersGovernanceTests
{
    [Fact]
    public void InfrastructureIgnoreQueryFilters_ShouldDocumentEveryIntentionalBypass()
    {
        var infrastructureRoot = Path.Combine(FindRepositoryRoot(), "src", "CLARIHR.Infrastructure");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(infrastructureRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].Contains(".IgnoreQueryFilters(", StringComparison.Ordinal))
                {
                    continue;
                }

                var previousLine = index > 0 ? lines[index - 1] : string.Empty;
                if (!previousLine.Contains("Intentional tenant filter bypass:", StringComparison.Ordinal))
                {
                    violations.Add($"{Path.GetRelativePath(infrastructureRoot, file)}:{index + 1}");
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
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "CLARIHR.Infrastructure")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test output path.");
    }
}
