using System.Reflection;
using CLARIHR.Application.Features.Reports;
using CLARIHR.Application.Features.Reports.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Drift-proof structural guardrail for technical-debt doc 01 §5.3.
/// <para>
/// The resource↔format compatibility rule lives in a declarative dictionary
/// (<see cref="ReportExportResourceFormatCompatibility"/>) instead of an
/// inline "document vs tabular" boolean. This test pins the invariant: every
/// public resource constant declared in <see cref="ReportExportResources"/>
/// MUST appear as a key in the compatibility table. If a future commit adds
/// a new resource constant but forgets to register its allowed formats, this
/// test fails — instead of the bug shipping as "resource silently rejects
/// every format" or "resource silently accepts the wrong format".
/// </para>
/// </summary>
public sealed class ReportExportResourceFormatCompatibilityGuardrailsTests
{
    [Fact]
    public void EveryDeclaredResourceKey_MustBeRegisteredInCompatibilityTable()
    {
        var declaredResourceKeys = GetDeclaredResourceKeys();

        Assert.NotEmpty(declaredResourceKeys);

        var unregistered = declaredResourceKeys
            .Where(static key => !ReportExportResourceFormatCompatibility.IsRegistered(key))
            .ToArray();

        Assert.True(
            unregistered.Length == 0,
            "§5.3: every ReportExportResources constant must be registered in "
                + "ReportExportResourceFormatCompatibility with its allowed format set. "
                + $"Missing: {string.Join(", ", unregistered)}.");
    }

    [Fact]
    public void IsCompatible_ForUnknownResource_ShouldReturnFalse()
    {
        Assert.False(
            ReportExportResourceFormatCompatibility.IsCompatible(
                "UNKNOWN_RESOURCE_FOR_GUARDRAIL",
                ReportExportFormats.Csv));
    }

    private static IReadOnlyCollection<string> GetDeclaredResourceKeys()
    {
        return typeof(ReportExportResources)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(static field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();
    }
}
