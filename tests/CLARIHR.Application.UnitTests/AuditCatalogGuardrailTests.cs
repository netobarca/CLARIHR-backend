using System.Reflection;
using CLARIHR.Application.Features.Audit.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Drift-proof guardrail (AU-3): every <c>AuditEventTypes</c> / <c>AuditEntityTypes</c> constant must be
/// registered in its <c>All</c> collection — otherwise <c>TryNormalize</c> (used by the audit-log
/// eventType / entityType filters) would silently reject it and the new event would be unfilterable.
/// Reflection-based so a newly added constant cannot regress the catalog.
/// </summary>
public sealed class AuditCatalogGuardrailTests
{
    [Fact]
    public void EveryAuditEventTypeConstant_IsRegisteredInAll() =>
        AssertAllConstantsRegistered(typeof(AuditEventTypes), AuditEventTypes.All);

    [Fact]
    public void EveryAuditEntityTypeConstant_IsRegisteredInAll() =>
        AssertAllConstantsRegistered(typeof(AuditEntityTypes), AuditEntityTypes.All);

    private static void AssertAllConstantsRegistered(Type catalog, IReadOnlyCollection<string> all)
    {
        var constants = catalog
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        var missing = constants
            .Where(value => !all.Contains(value))
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"Every {catalog.Name} constant must be present in {catalog.Name}.All (else TryNormalize rejects " +
            "it and the audit filter cannot select it). Missing:\n  " + string.Join("\n  ", missing));
    }
}
