using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.Reports;
using CLARIHR.Infrastructure.Reports;
using CLARIHR.Infrastructure.Reports.Handlers;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Regression coverage for the strategy-pattern refactor of report-export
/// generation (technical-debt doc 01 §2.1). Locks in that every supported
/// resource keeps exactly one handler and that the dispatcher preserves the
/// routing / failure contract the former switch provided.
/// </summary>
public sealed class ReportExportJobGeneratorDispatchTests
{
    private static readonly string[] AllSupportedResources =
    [
        ReportExportResources.PersonnelFiles,
        ReportExportResources.PersonnelFilePersonnelActions,
        ReportExportResources.PersonnelFilePayrollTransactions,
        ReportExportResources.OrgUnits,
        ReportExportResources.PositionSlots,
        ReportExportResources.SalaryTabulator,
        ReportExportResources.CostCenters,
        ReportExportResources.LegalRepresentatives,
        ReportExportResources.JobProfileCompetencyMatrix,
        ReportExportResources.JobProfilePdf,
    ];

    [Fact]
    public void Infrastructure_DeclaresExactlyOneHandlerPerSupportedResource()
    {
        var handlerKeys = typeof(ReportExportJobGenerator).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                && typeof(IReportExportHandler).IsAssignableFrom(type))
            .Select(type => ((IReportExportHandler)RuntimeHelpers.GetUninitializedObject(type)).ResourceKey)
            .ToArray();

        Assert.Equal(AllSupportedResources.Length, handlerKeys.Length);
        Assert.Equal(handlerKeys.Length, handlerKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            AllSupportedResources.OrderBy(static k => k, StringComparer.Ordinal),
            handlerKeys.OrderBy(static k => k, StringComparer.Ordinal));
    }

    [Fact]
    public async Task GenerateAsync_RoutesEachSupportedResourceToItsHandler()
    {
        var handlers = AllSupportedResources
            .Select(key => new StubHandler(key))
            .Cast<IReportExportHandler>()
            .ToArray();
        var generator = new ReportExportJobGenerator(handlers);

        foreach (var resource in AllSupportedResources)
        {
            var job = CreateJob(resource);

            var result = await generator.GenerateAsync(job, Stream.Null, CancellationToken.None);

            Assert.Equal($"file-for-{resource}", result.FileName);
        }
    }

    [Fact]
    public async Task GenerateAsync_UnknownResource_ThrowsNotSupportedWithStableMessage()
    {
        var generator = new ReportExportJobGenerator([new StubHandler(ReportExportResources.OrgUnits)]);
        var job = CreateJob("TOTALLY_UNKNOWN");

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => generator.GenerateAsync(job, Stream.Null, CancellationToken.None));

        Assert.Equal("Report resource 'TOTALLY_UNKNOWN' is not supported.", exception.Message);
    }

    [Fact]
    public void Constructor_DuplicateHandlerForSameResource_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ReportExportJobGenerator(
        [
            new StubHandler(ReportExportResources.OrgUnits),
            new StubHandler(ReportExportResources.OrgUnits),
        ]));

        Assert.Contains(ReportExportResources.OrgUnits, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_InvalidParametersJson_ThrowsBeforeReachingHandler()
    {
        var handler = new StubHandler(ReportExportResources.OrgUnits);
        var generator = new ReportExportJobGenerator([handler]);
        var job = CreateJob(ReportExportResources.OrgUnits, parametersJson: "not-json");

        await Assert.ThrowsAsync<ReportExportInvalidParametersException>(
            () => generator.GenerateAsync(job, Stream.Null, CancellationToken.None));

        Assert.False(handler.WasInvoked);
    }

    private static ReportExportJob CreateJob(string resourceKey, string parametersJson = "{}") =>
        ReportExportJob.Create(
            Guid.NewGuid(),
            resourceKey,
            "xlsx",
            parametersJson,
            "user-1",
            new DateTime(2026, 5, 16, 10, 0, 0, DateTimeKind.Utc));

    private sealed class StubHandler(string resourceKey) : IReportExportHandler
    {
        public string ResourceKey => resourceKey;

        public bool WasInvoked { get; private set; }

        public Task<ReportExportGeneratedFile> GenerateAsync(
            ReportExportJob job,
            Stream destination,
            JsonElement parameters,
            CancellationToken cancellationToken)
        {
            WasInvoked = true;
            return Task.FromResult(new ReportExportGeneratedFile(
                RowCount: 0,
                FileName: $"file-for-{ResourceKey}",
                ContentType: "application/octet-stream"));
        }
    }
}
