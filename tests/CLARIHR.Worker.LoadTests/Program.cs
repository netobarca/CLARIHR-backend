using System.Collections.Concurrent;
using System.Diagnostics;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Infrastructure.Reports.Documents;
using CLARIHR.Worker.LoadTests;
using QuestPDF.Infrastructure;

// §8.2: load test of the worker's PDF render (the CPU-bound work the
// ReportExportJobProcessor does per JOB_PROFILE_PDF job). Drives the renderer
// directly — no HTTP/DB/queue — to measure render throughput (PDFs/sec) and
// latency (p50/p95/p99), and how it differs between an empty and a rich profile.
//
//   dotnet run --project tests/CLARIHR.Worker.LoadTests -- [concurrency] [durationSeconds]
//
// `concurrency` simulates the worker's parallel render slots (Reporting:Performance:
// WorkerBatchSize, default 2). Zero external dependencies (no load-testing framework
// license). Gotenberg (the prod engine) is HTTP-bound and out of scope; this measures
// the in-process QuestPDF path.

QuestPDF.Settings.License = LicenseType.Community;

var concurrency = ParseArg(args, index: 0, defaultValue: 2);
var durationSeconds = ParseArg(args, index: 1, defaultValue: 30);

var renderer = new JobProfilePdfRenderer();

Console.WriteLine($"§8.2 worker PDF-render load test — concurrency={concurrency}, duration={durationSeconds}s per scenario");
Console.WriteLine("(in-process QuestPDF render; concurrency simulates WorkerBatchSize)\n");

await RunScenarioAsync("render_empty_profile_pdf", Payloads.Empty(), renderer, concurrency, durationSeconds);
await RunScenarioAsync("render_rich_profile_pdf", Payloads.Rich(), renderer, concurrency, durationSeconds);

static async Task RunScenarioAsync(
    string name,
    JobProfilePrintResponse payload,
    IDocumentPdfRenderer<JobProfilePrintResponse> renderer,
    int concurrency,
    int durationSeconds)
{
    var deadline = Stopwatch.GetTimestamp() + (long)(durationSeconds * (double)Stopwatch.Frequency);
    var latenciesMs = new ConcurrentBag<double>();
    long ok = 0;
    long fail = 0;

    var wall = Stopwatch.StartNew();
    var workers = Enumerable.Range(0, concurrency).Select(async _ =>
    {
        while (Stopwatch.GetTimestamp() < deadline)
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                await using var stream = new MemoryStream();
                await renderer.RenderAsync(payload, stream, CancellationToken.None);
                latenciesMs.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                if (stream.Length > 0)
                {
                    Interlocked.Increment(ref ok);
                }
                else
                {
                    Interlocked.Increment(ref fail);
                }
            }
            catch
            {
                Interlocked.Increment(ref fail);
            }
        }
    }).ToArray();

    await Task.WhenAll(workers);
    wall.Stop();

    Report(name, concurrency, ok, fail, latenciesMs, wall.Elapsed);
}

static void Report(string name, int concurrency, long ok, long fail, IReadOnlyCollection<double> latenciesMs, TimeSpan wall)
{
    var sorted = latenciesMs.OrderBy(static x => x).ToArray();
    var rps = wall.TotalSeconds > 0 ? ok / wall.TotalSeconds : 0;

    Console.WriteLine($"── {name} ──");
    Console.WriteLine($"   concurrency={concurrency}  wall={wall.TotalSeconds:F1}s");
    Console.WriteLine($"   ok={ok}  fail={fail}  throughput={rps:F1} PDFs/s");
    if (sorted.Length > 0)
    {
        Console.WriteLine(
            $"   latency ms: min={sorted[0]:F1}  mean={sorted.Average():F1}  " +
            $"p50={Percentile(sorted, 50):F1}  p95={Percentile(sorted, 95):F1}  " +
            $"p99={Percentile(sorted, 99):F1}  max={sorted[^1]:F1}");
    }

    Console.WriteLine();
}

static double Percentile(double[] sorted, double percentile)
{
    if (sorted.Length == 0)
    {
        return 0;
    }

    var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
}

static int ParseArg(string[] args, int index, int defaultValue) =>
    args.Length > index && int.TryParse(args[index], out var value) && value > 0 ? value : defaultValue;
