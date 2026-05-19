namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Drift-proof structural guardrail for technical-debt doc 01 §N3.
/// <para>
/// The §N3 bypass was caused by the salary gate resolving its server-controlled
/// flag through the shared <c>ReportExportParameters.ReadBool</c> /
/// <c>TryGetProperty</c> helper, which is case-insensitive and first-match
/// (order-dependent). The gate must instead use the exact, case-sensitive
/// <c>ReadBoolExact</c>. This test pins the security-sensitive file against the
/// named dangerous helpers via a source scan (the same mechanism as
/// <see cref="ReportExportGovernanceTests"/>): if a future edit reintroduces a
/// case-insensitive read in the gate, the build fails. It deliberately does not
/// enumerate allowed callers — there is nothing to hand-maintain.
/// </para>
/// </summary>
public sealed class JobProfileCompensationGateGuardrailsTests
{
    private static readonly string[] ForbiddenGatePatterns =
    [
        "ReadBool(",
        "TryGetProperty"
    ];

    [Fact]
    public void JobProfileCompensationGate_MustNotUseCaseInsensitiveParameterReads()
    {
        var gateFile = Path.Combine(
            FindRepositoryRoot(),
            "src", "CLARIHR.Infrastructure", "Reports", "Handlers", "JobProfileCompensationGate.cs");

        Assert.True(
            File.Exists(gateFile),
            $"§N3 guardrail target not found: {gateFile}. If the gate moved, update this guardrail.");

        var lines = File.ReadAllLines(gateFile);
        var violations = new List<string>();
        for (var index = 0; index < lines.Length; index++)
        {
            if (ForbiddenGatePatterns.Any(pattern => lines[index].Contains(pattern, StringComparison.Ordinal)))
            {
                violations.Add($"{Path.GetFileName(gateFile)}:{index + 1}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "§N3: JobProfileCompensationGate must read the server-controlled flag with the "
                + "exact, case-sensitive ReportExportParameters.ReadBoolExact — not the "
                + "case-insensitive / first-match ReadBool / TryGetProperty helper. "
                + $"Offending lines: {string.Join(", ", violations)}");
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
