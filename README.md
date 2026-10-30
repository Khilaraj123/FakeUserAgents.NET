# FakeUserAgents.NET

A high-performance, lightweight .NET library for generating and rotating realistic browser User-Agent profiles with metadata. Designed for web scrapers, crawlers, integration tests, and API clients requiring clean browser emulation, smart rotation strategies, and session affinity.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)
[![NuGet](https://img.shields.io/nuget/v/FakeUserAgents.NET.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/FakeUserAgents.NET)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

---

> [!NOTE]
> **Scope & Anti-Bot Disclaimer**:
> This library manages realistic User-Agent headers, profile metadata, and rotation patterns. It does **not** bypass modern anti-bot systems on its own. Advanced anti-bot platforms inspect TLS/JA3/JA4 fingerprints, HTTP/2 frame parameters, TCP characteristics, and JavaScript/WebGL/Canvas runtime execution.

---

## Key Architecture & Features

- **Modern 2026 Recency & Standards**: Ships with up-to-date versions (Chrome 145, Firefox 145, Safari 18.3, Edge 145, Opera 124) conforming to modern **Chromium User-Agent Reduction** (`Linux; Android 10; K`, `.0.0.0` frozen builds).
- **Market-Weighted Selection**: Samples proportionally to real-world browser market share (Chrome ~65%, Safari ~19%, Edge ~5.5%, Firefox ~3%, Opera ~2.5%) rather than an artificial uniform distribution that acts as a fingerprint.
- **Deep Combination Pools**: Every supported browser/device/OS combination has multiple distinct entries (at least 3–4), ensuring `Shuffled` and `NoImmediateRepeat` are effective across all filter configurations.
- **Precomputed O(1) Indexes**: Filters and cumulative weights are precomputed on startup using `FrozenDictionary`. Querying avoids database scans and executes in nanoseconds with **0 bytes allocated**.
- **Lock-Free Rotation Hot Paths**:
  - `RotationStrategy.RoundRobin`: Completely lock-free using `Interlocked.Increment`.
  - `RotationStrategy.Random`: Lock-free via `Random.Shared` using market-weighted cumulative binary search.
  - `RotationStrategy.Shuffled`: Amortized lock-free. Serves every candidate in random order before repeating, with synchronization only occurring at cycle boundaries.
  - `RotationStrategy.NoImmediateRepeat`: Guarantees back-to-back requests never receive identical user agents.
- **Race-Free, Bounded Session Affinity**:
  - `rotator.GetForSession(sessionId)` pins user agents to session IDs atomically using `Lazy<T>`.
  - `UserAgentSessionManager` provides bounded memory capacity (`maxSessions`), sliding expiration, and automatic operation-based cleanup without background timer threads.
- **Automated CI Recency Guardrails**: Includes an automated CI check that fails builds if the dataset falls behind modern major releases. See [docs/DATASET_REFRESH.md](docs/DATASET_REFRESH.md).

---

## Repository Structure

```text
FakeUserAgents.NET/
├── src/
│   └── FakeUserAgents.NET/             # Core library (.NET 10)
│       ├── Internal/
│       │   ├── UaDatabase.cs           # Embedded database loader & fallback catalog
│       │   ├── UaIndex.cs              # Precomputed FrozenDictionary index pools
│       │   └── UaJsonContext.cs        # Source-generated AOT JSON serializer
│       ├── Data/
│       │   └── useragents.json         # Curated real-world user agent dataset (weighted)
│       ├── Browser.cs
│       ├── DeviceType.cs
│       ├── OperatingSystemType.cs
│       ├── RotationStrategy.cs
│       ├── UserAgent.cs
│       ├── UserAgentInfo.cs
│       ├── UserAgentRotator.cs
│       ├── UserAgentSessionManager.cs
│       └── UserAgentValidator.cs
├── tests/
│   └── FakeUserAgents.NET.Tests/       # xUnit test suite (concurrency, validation, recency CI)
├── samples/
│   └── FakeUserAgents.NET.Console/     # Crawler & HttpClient usage demo
├── benchmarks/
│   └── FakeUserAgents.NET.Benchmarks/  # Microbenchmark suite
├── scripts/
│   └── refresh-dataset.ps1             # Automated dataset validation & refresh script
├── docs/
│   └── DATASET_REFRESH.md              # Documented dataset maintenance process
├── README.md
├── LICENSE.txt
└── FakeUserAgents.NET.slnx
```

---

## Installation

Add the package via the .NET CLI:

```bash
dotnet add package FakeUserAgents.NET
```

Or via NuGet Package Manager:

```powershell
Install-Package FakeUserAgents.NET
```

---

## Quick Start

```csharp
using FakeUserAgents.NET;

var ua = new UserAgent();

// Market-weighted random User-Agent (O(1)/O(log K), 0 B allocated)
UserAgentInfo info = ua.GetRandom();
Console.WriteLine(info.UserAgent);
// => Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36
```

---

## Usage Guide

### 1. Market-Weighted vs Uniform Sampling

By default, selection is market-weighted to mimic authentic global traffic:

```csharp
var ua = new UserAgent();

// Market-weighted (proportional to Chrome ~65%, Safari ~19%, Edge ~5.5%, Firefox ~3%, Opera ~2.5%)
UserAgentInfo realistic = ua.GetRandom(weighted: true);

// Uniform random (equal probability for all profiles in the pool)
UserAgentInfo uniform = ua.GetRandom(weighted: false);
```

### 2. Pre-Indexed Filtering (O(1))

Query specific browsers, device categories, or operating systems with zero database iteration:

```csharp
var ua = new UserAgent();

// Filter by enum or case-insensitive string
UserAgentInfo chrome = ua.GetByBrowser(Browser.Chrome);
UserAgentInfo firefox = ua.GetByBrowser("firefox");

// Filter by device and OS
UserAgentInfo mobile = ua.GetByDevice(DeviceType.Mobile);
UserAgentInfo mac = ua.GetByOs(OperatingSystemType.MacOS);

// Combined filter with graceful degradation
UserAgentInfo result = ua.Get(
    browser: Browser.Safari,
    os: OperatingSystemType.Windows
); // Automatically relaxes OS to MacOS since Safari does not exist on Windows
```

### 3. Strict Matching (`TryGet`)

When you need exact matching without fallback relaxation:

```csharp
if (ua.TryGet(Browser.Chrome, DeviceType.Desktop, OperatingSystemType.Windows, out var match))
{
    Console.WriteLine($"Found: {match.UserAgent}");
}
```

### 4. Rotating User Agents (`UserAgentRotator`)

`UserAgentRotator` provides thread-safe rotation strategies optimized for concurrent scrapers:

```csharp
// Shuffled: guarantees every profile is used before any repeat
var rotator = new UserAgentRotator(
    strategy: RotationStrategy.Shuffled,
    browser: Browser.Chrome,
    device: DeviceType.Desktop
);

// Lock-free hot path (0 bytes allocated)
string activeUa = rotator.NextUserAgent();
```

Available strategies:
- `RotationStrategy.Shuffled`: Fisher-Yates permutation cycles; visits all entries before repeating.
- `RotationStrategy.NoImmediateRepeat`: Guarantees consecutive calls never yield the identical user agent.
- `RotationStrategy.RoundRobin`: Lock-free atomic sequential cycling.
- `RotationStrategy.Random`: Lock-free market-weighted random selection via `Random.Shared`.

### 5. Session Affinity for Web Crawlers

Web crawlers often need multiple requests with the same session cookie or user ID to retain the identical browser profile.

#### Option A: Built-in Rotator Sessions
```csharp
var rotator = new UserAgentRotator(RotationStrategy.Shuffled);

// Subsequent calls with the same session ID return the identical user agent
UserAgentInfo sessionA = rotator.GetForSession("user-session-alice");
UserAgentInfo sessionB = rotator.GetForSession("user-session-bob");
UserAgentInfo sessionAAgain = rotator.GetForSession("user-session-alice");

Assert.Equal(sessionA.UserAgent, sessionAAgain.UserAgent);
```

#### Option B: Dedicated `UserAgentSessionManager`
```csharp
// Configured with 15-minute sliding expiration and a 10,000 session capacity bound
var sessionManager = new UserAgentSessionManager(
    slidingExpiration: TimeSpan.FromMinutes(15),
    maxSessions: 10_000);

UserAgentInfo userUa = sessionManager.GetOrCreate("client-auth-token");
```

### 6. Integration with `HttpClient`

```csharp
using FakeUserAgents.NET;

var rotator = new UserAgentRotator(RotationStrategy.NoImmediateRepeat);
using var httpClient = new HttpClient();

for (int i = 0; i < 5; i++)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
    request.Headers.TryAddWithoutValidation("User-Agent", rotator.NextUserAgent());

    using var response = await httpClient.SendAsync(request);
    Console.WriteLine($"Status: {response.StatusCode}");
}
```

### 7. Deterministic Testing

Pass a seed to reproduce exact sequences across test runs:

```csharp
var ua1 = new UserAgent(seed: 42);
var ua2 = new UserAgent(seed: 42);

Assert.Equal(ua1.GetRandom().UserAgent, ua2.GetRandom().UserAgent);
```

---

## Dataset Maintenance & CI Guardrails

See [docs/DATASET_REFRESH.md](docs/DATASET_REFRESH.md) for complete details on the refresh process.

- **Chromium UA Reduction Compliance**: Mobile Android entries use the reduced format `(Linux; Android 10; K) AppleWebKit/537.36 ... Chrome/<major>.0.0.0 Mobile Safari/537.36`.
- **Automated CI Staleness Check**: Tests fail if the newest Chrome major version in the dataset is older than the configured threshold (e.g. Chrome 144+).
- **Maintenance Script**: Run `powershell -File ./scripts/refresh-dataset.ps1` (or `pwsh ./scripts/refresh-dataset.ps1`) to inspect current versions and market share distribution.

---

## Memory & Allocation Rules

| Method | Return Type | Heap Allocation | Notes |
| :--- | :--- | :--- | :--- |
| `ua.GetRandom()` | `UserAgentInfo` | **0 B** | Returns existing cached reference |
| `ua.Get(...)` | `UserAgentInfo` | **0 B** | O(1) indexed lookup |
| `rotator.Next()` | `UserAgentInfo` | **0 B** | 0 B for RoundRobin/NoImmediateRepeat; ~4 B/op amortized for Shuffled |
| `rotator.NextUserAgent()` | `string` | **0 B** | Returns existing string reference |
| `sessionManager.GetOrCreate()` | `UserAgentInfo` | **0 B / ~40 B** | Zero-allocation fast hit; bounded cache entry |
| `ua.ToDictionary()` | `Dictionary<string, string>` | **Allocates** | Convenience conversion |
| `rotator.NextAsDictionary()` | `Dictionary<string, string>` | **Allocates** | Convenience conversion |

---

## Performance Benchmarks

Benchmarks are located in `benchmarks/FakeUserAgents.NET.Benchmarks`. To execute them on your hardware:

```bash
dotnet run -c Release --project benchmarks/FakeUserAgents.NET.Benchmarks/FakeUserAgents.NET.Benchmarks.csproj
```

*Results from .NET 10.0 (x64) execution:*

| Method | Strategy / Mode | Mean Latency | Throughput | Allocated |
| :--- | :--- | :--- | :--- | :--- |
| `Rotator.Next()` | `RoundRobin` (Atomic) | **11.8 ns** | **~84,400,000 ops/s** | **0.0 B/op** |
| `Rotator.Next()` | `Shuffled` (Amortized) | **51.9 ns** | **~19,280,000 ops/s** | **4.3 B/op** |
| `Rotator.Next()` | `NoImmediateRepeat` | **68.6 ns** | **~14,580,000 ops/s** | **0.0 B/op** |
| `SessionManager.GetOrCreate()` | Bounded Cache Hit | **88.0 ns** | **~11,370,000 ops/s** | **40.0 B/op** |
| `UserAgent.GetRandom()` | O(1) Indexed | **142.0 ns** | **~7,040,000 ops/s** | **0.0 B/op** |
| `Rotator.GetForSession()` | Session Affinity Hit | **193.1 ns** | **~5,180,000 ops/s** | **64.0 B/op** |
| `UserAgent.Get(B, D, OS)` | O(1) Precomputed Fallback | **518.6 ns** | **~1,930,000 ops/s** | **0.0 B/op** |

---

## Running the Sample Application

A runnable scraper and crawler demonstration is included in `samples/FakeUserAgents.NET.Console`:

```bash
dotnet run --project samples/FakeUserAgents.NET.Console/FakeUserAgents.NET.Console.csproj
```

---

## Supported Options

- **`Browser`**: `Chrome`, `Firefox`, `Safari`, `Edge`, `Opera`
- **`DeviceType`**: `Desktop`, `Mobile`, `Tablet`
- **`OperatingSystemType`**: `Windows`, `MacOS`, `Linux`, `ChromeOS`, `Android`, `IOS`

---

## Contributing

Contributions, bug reports, and suggestions are welcome. Please feel free to submit an issue or pull request.

---

## License

This project is licensed under the [MIT License](LICENSE.txt).