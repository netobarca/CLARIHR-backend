namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §LG1 (audit doc 12) drift-proof guardrail. Every Locations admin handler that writes an audit entry
/// MUST <c>SaveChangesAsync</c> it BEFORE committing the transaction: <c>IAuditService.LogAsync</c> only
/// <c>Add</c>s the <c>AuditLog</c> (deferred) and the unit-of-work <c>CommitAsync</c> does NOT flush, so
/// an <c>auditService.LogAsync(...)</c> followed by <c>transaction.CommitAsync(...)</c> with no
/// intervening <c>SaveChangesAsync</c> silently drops the audit trail (the original §LG1 bug, present in
/// all four mutating Locations admins). This scans the Locations feature source with a line-by-line
/// state machine — LogAsync arms a pending flag, SaveChangesAsync clears it, a CommitAsync while armed is
/// a violation — so it catches the bug AND any future drift, including new Locations controllers.
/// </summary>
public sealed class LocationAuditPersistenceGuardrailTests
{
    private static readonly string LocationsFeaturePath = Path.Combine(
        ResolveRepositoryRoot(), "src", "CLARIHR.Application", "Features", "Locations");

    [Fact]
    public void EveryLocationAuditLog_IsSavedBeforeTheTransactionCommits()
    {
        var files = Directory.GetFiles(LocationsFeaturePath, "*Administration.cs", SearchOption.AllDirectories);

        // Zero-match sentinel: a moved/renamed feature folder must fail loudly, not pass on an empty scan.
        Assert.NotEmpty(files);

        var violations = new List<string>();

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            var pendingAuditLine = -1;

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];

                if (line.Contains("auditService.LogAsync", StringComparison.Ordinal))
                {
                    pendingAuditLine = index;
                }
                else if (line.Contains("SaveChangesAsync", StringComparison.Ordinal))
                {
                    pendingAuditLine = -1;
                }
                else if (line.Contains("transaction.CommitAsync", StringComparison.Ordinal) && pendingAuditLine >= 0)
                {
                    violations.Add(
                        $"{Path.GetFileName(file)}: auditService.LogAsync at line {pendingAuditLine + 1} reaches " +
                        $"transaction.CommitAsync at line {index + 1} with no SaveChangesAsync between — the audit " +
                        "row would never be flushed (the AuditLog Add is deferred and CommitAsync does not save).");
                    pendingAuditLine = -1;
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "§LG1: every auditService.LogAsync in a Locations admin must be SaveChanges'd before " +
            "transaction.CommitAsync, or the audit trail is silently lost. Offending:\n  " +
            string.Join("\n  ", violations));
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root for the Locations audit guardrail.");
    }
}
