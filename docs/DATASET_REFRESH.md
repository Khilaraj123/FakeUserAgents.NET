# User-Agent Dataset Refresh Process

This document defines the methodology, standards, and workflow for maintaining the embedded `useragents.json` dataset in **FakeUserAgents.NET**.

---

## 1. Why Dataset Freshness and Weighting Matter

In real-world web traffic:
- **Recency**: Over 90% of web clients auto-update within 2–4 weeks of a major release. A crawler sending User-Agent headers that are several major versions behind (e.g. Chrome 124 when Chrome 145 is current) produces an immediate behavioral anomaly for anti-bot heuristics.
- **Market Weighting**: Real web traffic is **not** uniformly distributed. Picking equally among all browsers causes Opera and Edge to appear on ~35% of requests, which is statistically improbable. True global traffic is approximately:
  - **Google Chrome**: ~60–65%
  - **Apple Safari**: ~18–20%
  - **Microsoft Edge**: ~5–6%
  - **Mozilla Firefox**: ~3–4%
  - **Opera**: ~2–3%
- **Chromium User-Agent Reduction**: Starting in Chrome 101–110, Chromium standardized User-Agent reduction to prevent cross-site fingerprinting. Real Chrome browsers on Android freeze the OS version as `Android 10; K` and minor build numbers as `.0.0.0`. Sending granular hardware models like `Android 14; Pixel 8 Pro` with non-reduced build numbers stands out against genuine Chromium traffic.

---

## 2. Standards & Header Formats

### Chromium Desktop (Windows, macOS, Linux, ChromeOS)
- **Windows**: `Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/<major>.0.0.0 Safari/537.36`
- **macOS**: `Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/<major>.0.0.0 Safari/537.36`
- **Linux**: `Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/<major>.0.0.0 Safari/537.36`
- **ChromeOS**: `Mozilla/5.0 (X11; CrOS x86_64 14541.0.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/<major>.0.0.0 Safari/537.36`

### Chromium Android (Mobile & Tablet)
- **Mobile (Phone)**: `Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/<major>.0.0.0 Mobile Safari/537.36`
- **Tablet**: `Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/<major>.0.0.0 Safari/537.36`
- *Note*: Device model is frozen at `K`, and OS is frozen at `Android 10`.

### Apple Safari (macOS & iOS)
- **macOS**: `Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/<safari_version> Safari/605.1.15`
- **iOS Mobile**: `Mozilla/5.0 (iPhone; CPU iPhone OS <ios_version> like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/<safari_version> Mobile/15E148 Safari/604.1`
- **iOS Tablet (iPad)**: `Mozilla/5.0 (iPad; CPU OS <ios_version> like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/<safari_version> Mobile/15E148 Safari/604.1`

### Mozilla Firefox
- **Desktop Windows**: `Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:<version>) Gecko/20100101 Firefox/<version>`
- **Desktop macOS**: `Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:<version>) Gecko/20100101 Firefox/<version>`
- **Desktop Linux**: `Mozilla/5.0 (X11; Linux x86_64; rv:<version>) Gecko/20100101 Firefox/<version>`
- **Android**: `Mozilla/5.0 (Android 14; Mobile; rv:<version>) Gecko/<version> Firefox/<version>`

---

## 3. Weighting Guidelines

Each entry in `useragents.json` includes a positive floating-point `weight`:
- **Total Browser Share**:
  - Chrome: Sum of weights ~ 65.0
  - Safari: Sum of weights ~ 19.0
  - Edge: Sum of weights ~ 5.5
  - Firefox: Sum of weights ~ 3.0
  - Opera: Sum of weights ~ 2.5
- **Version Decay Factor**:
  - Current Stable (`N`): ~60% of browser weight
  - Previous Stable (`N - 1`): ~25% of browser weight
  - Older (`N - 2`): ~10% of browser weight
  - Legacy (`N - 3`): ~5% of browser weight

---

## 4. Step-by-Step Refresh Workflow

When a new browser major version is released:

1. **Check Release Status**:
   - Chromium Releases: [Chromium Dash](https://chromiumdash.appspot.com/releases?platform=Windows)
   - Firefox Releases: [Mozilla Release Notes](https://www.mozilla.org/en-US/firefox/releases/)
   - Safari Releases: [Apple Developer Documentation](https://developer.apple.com/documentation/safari-release-notes)

2. **Update Dataset**:
   - Run the helper script `scripts/refresh-dataset.ps1` with the new target major versions.
   - Or manually update `src/FakeUserAgents.NET/Data/useragents.json`.

3. **Validate Dataset Integrity**:
   - Run the test suite:
     ```bash
     dotnet test tests/FakeUserAgents.NET.Tests/FakeUserAgents.NET.Tests.csproj
     ```
   - The test `AllDatabaseEntries_PassStrongValidation` verifies tokens, device/OS consistency, and format rules.
   - The test `Dataset_NewestChromeVersion_IsNotStale` automatically guards against shipping outdated versions in CI.
   - The test `Dataset_Weights_ReflectMarketShareDistribution` verifies market share ratios.
   - The test `Dataset_SupportedCombinations_HavePoolDepthForRotation` ensures every pool has at least 3 distinct versions.

---

## 5. Automated CI Guardrail

In `tests/FakeUserAgents.NET.Tests/UaDatabaseTests.cs`, the following test runs on every pull request and build:

```csharp
[Fact]
public void Dataset_NewestChromeVersion_IsNotStale()
{
    int maxChromeMajor = UaDatabase.Entries
        .Where(e => e.Browser == Browser.Chrome)
        .Select(e => int.TryParse(e.BrowserVersion.Split('.')[0], out int major) ? major : 0)
        .Max();

    Assert.True(maxChromeMajor >= 144,
        $"Newest Chrome version ({maxChromeMajor}) is too stale. Expected at least Chrome 144.");
}
```

If the dataset is not refreshed when Chrome advances past the threshold, continuous integration fails immediately.
