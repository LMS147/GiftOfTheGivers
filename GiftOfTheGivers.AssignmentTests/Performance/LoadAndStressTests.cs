using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using GiftOfTheGivers.AssignmentTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
namespace GiftOfTheGivers.AssignmentTests.Performance;

[Collection("Assignment system"), Trait("Category", "Load")]
public class LoadAndStressTests(AssignmentEnvironment environment, ITestOutputHelper output)
{
    private sealed record Phase(string Name, int VirtualUsers, int DurationSeconds);
    private sealed class Settings
    {
        public int WarmupRequests { get; set; }
        public int ThinkTimeMilliseconds { get; set; }
        public int RequestTimeoutSeconds { get; set; }
        public double MinimumSuccessRate { get; set; }
        public double LoadP95LimitMilliseconds { get; set; }
        public string[] Paths { get; set; } = Array.Empty<string>();
        public Phase[] Phases { get; set; } = Array.Empty<Phase>();
    }
    private sealed record Sample(string Phase, int User, string Path, int Status, double Milliseconds);
    private sealed record PhaseResult(string Name, int VirtualUsers, int PeakConcurrentRequests, int Requests, int Successes, int Failures, double SuccessRate, double ElapsedSeconds, double RequestsPerSecond, double MeanMilliseconds, double P95Milliseconds, double MaxMilliseconds);

    [Fact]
    public async Task FiveUserLoadAndTwentyUserStressProduceMeasuredResults()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "Performance", "load-settings.json");
        var settings = JsonSerializer.Deserialize<Settings>(await File.ReadAllTextAsync(settingsPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        using var client = new HttpClient { BaseAddress = new Uri(environment.WebUrl), Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds) };
        for (var i = 0; i < settings.WarmupRequests; i++) (await client.GetAsync(settings.Paths[i % settings.Paths.Length])).EnsureSuccessStatusCode();
        var samples = new ConcurrentBag<Sample>(); var results = new List<PhaseResult>();
        foreach (var phase in settings.Phases)
        {
            var active = 0; var peak = 0; var clock = Stopwatch.StartNew();
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasks = Enumerable.Range(1, phase.VirtualUsers).Select(async user =>
            {
                await start.Task; var iteration = 0;
                while (clock.Elapsed.TotalSeconds < phase.DurationSeconds)
                {
                    var path = settings.Paths[(user + iteration++) % settings.Paths.Length];
                    var running = Interlocked.Increment(ref active);
                    int previous; do { previous = Volatile.Read(ref peak); if (previous >= running) break; } while (Interlocked.CompareExchange(ref peak, running, previous) != previous);
                    var request = Stopwatch.StartNew(); var status = 0;
                    try { using var response = await client.GetAsync(path); status = (int)response.StatusCode; }
                    catch (HttpRequestException) { } catch (TaskCanceledException) { }
                    finally { request.Stop(); Interlocked.Decrement(ref active); }
                    samples.Add(new(phase.Name, user, path, status, request.Elapsed.TotalMilliseconds));
                    await Task.Delay(settings.ThinkTimeMilliseconds);
                }
            }).ToArray();
            start.SetResult(); await Task.WhenAll(tasks); clock.Stop();
            var data = samples.Where(s => s.Phase == phase.Name).ToList();
            var times = data.Select(s => s.Milliseconds).OrderBy(t => t).ToArray();
            var successful = data.Count(s => s.Status is >= 200 and < 400);
            results.Add(new(phase.Name, phase.VirtualUsers, peak, data.Count, successful, data.Count - successful,
                data.Count == 0 ? 0 : successful / (double)data.Count, clock.Elapsed.TotalSeconds, data.Count / clock.Elapsed.TotalSeconds,
                times.Length == 0 ? 0 : times.Average(), times.Length == 0 ? 0 : times[Math.Max(0, (int)Math.Ceiling(times.Length * .95) - 1)], times.Length == 0 ? 0 : times[^1]));
        }
        var report = new { measuredAtUtc = DateTime.UtcNow, target = environment.WebUrl, environment = "Local Kestrel + real Azure Functions Core Tools + isolated SQL Server", settings, results };
        await File.WriteAllTextAsync(Path.Combine(environment.EvidenceDirectory, "load-stress-result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        var csv = new[] { "phase,user,path,status,milliseconds" }.Concat(samples.Select(s => $"{s.Phase},{s.User},{s.Path},{s.Status},{s.Milliseconds.ToString("F3", CultureInfo.InvariantCulture)}"));
        await File.WriteAllLinesAsync(Path.Combine(environment.EvidenceDirectory, "load-stress-samples.csv"), csv);
        foreach (var result in results) output.WriteLine("{0}: users={1}, peak={2}, requests={3}, success={4:P1}, p95={5:F2}ms, rate={6:F2}/s", result.Name, result.VirtualUsers, result.PeakConcurrentRequests, result.Requests, result.SuccessRate, result.P95Milliseconds, result.RequestsPerSecond);
        Assert.All(results, result => Assert.True(result.SuccessRate >= settings.MinimumSuccessRate, $"{result.Name} success rate {result.SuccessRate:P1}; see the measured report."));
        var load = results.Single(r => r.Name == "load"); Assert.Equal(5, load.VirtualUsers); Assert.True(load.Requests > 0);
        Assert.True(load.P95Milliseconds <= settings.LoadP95LimitMilliseconds, "Five-user p95 exceeded the configured limit. Report the result and investigate; do not replace measured values.");
    }
}
