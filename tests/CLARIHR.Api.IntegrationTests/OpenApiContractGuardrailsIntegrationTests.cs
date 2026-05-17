using System.Net;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// §J3 integration guardrail (mirrors PublicContractGuardrailsIntegrationTests). Asserts
/// the generated OpenAPI doc: (a) no operation anywhere is tagged "JobCatalogs"/"Job
/// Catalogs" — the orphan group this finding removed (§6.1 regression catcher, scanned
/// globally); (b) every JobCatalogs operation (the §J3 target) is tagged "Job Profiles"
/// (§6.1) and has a non-empty summary (§6.2). The per-controller §6.1/§6.2 guarantee for
/// the whole JobProfile/JobCatalog family is enforced structurally by the unit test
/// (OpenApiContractGuardrailsTests); this test pins the rendered contract for the
/// JobCatalogs target and catches a Swashbuckle config regression reflection cannot see.
/// Scope is intentionally the /job-catalogs paths only: e.g. competency-matrix endpoints
/// under /job-profiles/{} belong to a different controller and are intentionally tagged
/// otherwise — out of §J3 scope.
/// </summary>
public sealed class OpenApiContractGuardrailsIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private const string ExpectedTag = "Job Profiles";
    private const string JobCatalogsPathFragment = "/job-catalogs";
    private static readonly string[] OrphanTags = ["JobCatalogs", "Job Catalogs"];

    [Fact]
    public async Task Swagger_JobCatalogs_IsConsolidatedUnderJobProfilesTag_AndDocumented()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        var jobCatalogsSeen = 0;
        var missingTag = new List<string>();
        var missingSummary = new List<string>();
        var orphanTagged = new List<string>();

        foreach (var path in paths.EnumerateObject())
        {
            var isJobCatalogs = path.Name.Contains(JobCatalogsPathFragment, StringComparison.Ordinal);

            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!IsHttpMethod(operation.Name))
                {
                    continue;
                }

                var tags = operation.Value.TryGetProperty("tags", out var t)
                    ? t.EnumerateArray().Select(static x => x.GetString()).ToArray()
                    : [];
                var location = $"{operation.Name.ToUpperInvariant()} {path.Name}";

                if (tags.Any(tag => OrphanTags.Contains(tag, StringComparer.Ordinal)))
                {
                    orphanTagged.Add($"{location} -> [{string.Join(", ", tags)}]");
                }

                if (!isJobCatalogs)
                {
                    continue;
                }

                jobCatalogsSeen++;
                if (!tags.Contains(ExpectedTag, StringComparer.Ordinal))
                {
                    missingTag.Add($"{location} -> [{string.Join(", ", tags)}]");
                }

                var summary = operation.Value.TryGetProperty("summary", out var s)
                    ? s.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(summary))
                {
                    missingSummary.Add(location);
                }
            }
        }

        Assert.True(jobCatalogsSeen > 0,
            "No /job-catalogs operations in swagger.json — the path filter drifted and the " +
            "§J3 integration guardrail would silently pass.");
        Assert.True(orphanTagged.Count == 0,
            "§J3 (§6.1 regression): no operation may be tagged JobCatalogs/Job Catalogs " +
            "(the orphan group this finding removed). Offending:\n  " +
            string.Join("\n  ", orphanTagged.OrderBy(static v => v, StringComparer.Ordinal)));
        Assert.True(missingTag.Count == 0,
            "§J3 (§6.1): every JobCatalogs operation must be tagged " +
            $"\"{ExpectedTag}\" so it is consolidated into the Job Profiles group. Offending:\n  " +
            string.Join("\n  ", missingTag.OrderBy(static v => v, StringComparer.Ordinal)));
        Assert.True(missingSummary.Count == 0,
            "§J3 (§6.2): every JobCatalogs operation must expose a non-empty summary. " +
            "Offending:\n  " +
            string.Join("\n  ", missingSummary.OrderBy(static v => v, StringComparer.Ordinal)));
    }

    private static bool IsHttpMethod(string name) => name switch
    {
        "get" or "put" or "post" or "delete" or "patch" or "options" or "head" or "trace" => true,
        _ => false,
    };
}
