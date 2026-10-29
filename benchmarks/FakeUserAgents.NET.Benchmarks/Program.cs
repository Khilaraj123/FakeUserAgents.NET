using System.Diagnostics;
using Dotnet.FakeUserAgents;

Console.WriteLine("==========================================================================");
Console.WriteLine(" Dotnet.FakeUserAgents - Generation & Rotation Benchmarks (.NET 10 x64)");
Console.WriteLine("==========================================================================\n");

Console.WriteLine("Executing warmup and statistical benchmark passes...\n");

var results = new List<BenchmarkResult>();

// 1. UserAgent.GetRandom()
results.Add(Benchmark("UserAgent.GetRandom()", "O(1) Indexed", () =>
{
    var ua = new UserAgent();
    return () => ua.GetRandom();
}));

// 2. UserAgent.Get(Browser, Device, OS)
results.Add(Benchmark("UserAgent.Get(B, D, OS)", "O(1) Precomputed", () =>
{
    var ua = new UserAgent();
    return () => ua.Get(Browser.Chrome, DeviceType.Desktop, OperatingSystemType.Windows);
}));

// 3. UserAgentRotator.Next() [RoundRobin]
results.Add(Benchmark("Rotator.Next()", "RoundRobin (Atomic)", () =>
{
    var rotator = new UserAgentRotator(RotationStrategy.RoundRobin);
    return () => rotator.Next();
}));

// 4. UserAgentRotator.Next() [Shuffled]
results.Add(Benchmark("Rotator.Next()", "Shuffled (Amortized)", () =>
{
    var rotator = new UserAgentRotator(RotationStrategy.Shuffled);
    return () => rotator.Next();
}));

// 5. UserAgentRotator.Next() [NoImmediateRepeat]
results.Add(Benchmark("Rotator.Next()", "NoImmediateRepeat", () =>
{
    var rotator = new UserAgentRotator(RotationStrategy.NoImmediateRepeat);
    return () => rotator.Next();
}));

// 6. UserAgentRotator.GetForSession()
results.Add(Benchmark("Rotator.GetForSession()", "Session Affinity Hit", () =>
{
    var rotator = new UserAgentRotator(RotationStrategy.Shuffled);
    rotator.GetForSession("bench-session-id");
    return () => rotator.GetForSession("bench-session-id");
}));

// 7. UserAgentSessionManager.GetOrCreate()
results.Add(Benchmark("SessionManager.GetOrCreate()", "Bounded Cache Hit", () =>
{
    var manager = new UserAgentSessionManager();
    manager.GetOrCreate("bench-client-id");
    return () => manager.GetOrCreate("bench-client-id");
}));

// Print Markdown Table
Console.WriteLine("| Method | Strategy / Mode | Mean Latency | Throughput | Allocated |");
Console.WriteLine("| :--- | :--- | :--- | :--- | :--- |");

foreach (var r in results)
{
    Console.WriteLine($"| `{r.Method}` | {r.Strategy} | **{r.MeanNs:F1} ns** | **{r.OpsPerSec:N0} ops/s** | **{r.BytesPerOp:F1} B/op** |");
}

Console.WriteLine("\nAll benchmarks finished successfully.");

static BenchmarkResult Benchmark(string method, string strategy, Func<Action> setup, int iterations = 300_000)
{
    var action = setup();

    // Warmup
    for (int i = 0; i < 5_000; i++) action();

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    long allocBefore = GC.GetAllocatedBytesForCurrentThread();
    var sw = Stopwatch.StartNew();

    for (int i = 0; i < iterations; i++)
    {
        action();
    }

    sw.Stop();
    long allocAfter = GC.GetAllocatedBytesForCurrentThread();

    double totalNs = sw.Elapsed.TotalMilliseconds * 1_000_000.0;
    double meanNs = totalNs / iterations;
    double opsPerSec = iterations / sw.Elapsed.TotalSeconds;
    double bytesPerOp = Math.Max(0, (double)(allocAfter - allocBefore) / iterations);

    return new BenchmarkResult(method, strategy, meanNs, opsPerSec, bytesPerOp);
}

sealed record BenchmarkResult(string Method, string Strategy, double MeanNs, double OpsPerSec, double BytesPerOp);
