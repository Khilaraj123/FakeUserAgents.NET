using Dotnet.FakeUserAgents;

Console.WriteLine("=================================================");
Console.WriteLine(" Dotnet.FakeUserAgents - Scraper & Crawler Demo");
Console.WriteLine("=================================================\n");

// 1. Basic Generation & Metadata
Console.WriteLine("--- 1. Quick Generation ---");
var ua = new UserAgent();
var randomUa = ua.GetRandom();
Console.WriteLine($"Selected UA: {randomUa.UserAgent}");
Console.WriteLine($"Browser:     {randomUa.Browser} {randomUa.BrowserVersion}");
Console.WriteLine($"Platform:    {randomUa.Os} ({randomUa.DeviceType})\n");

// 2. High-Performance Indexed Filtering
Console.WriteLine("--- 2. High-Performance Indexed Filtering (O(1)) ---");
var chromeDesktop = ua.Get(Browser.Chrome, DeviceType.Desktop, OperatingSystemType.Windows);
Console.WriteLine($"Chrome Windows: {chromeDesktop.UserAgent}");

var safariIos = ua.Get(Browser.Safari, DeviceType.Mobile, OperatingSystemType.IOS);
Console.WriteLine($"Safari iPhone:  {safariIos.UserAgent}\n");

// 3. UserAgent Rotation
Console.WriteLine("--- 3. UserAgent Rotation Strategies ---");
var rotator = new UserAgentRotator(
    strategy: RotationStrategy.Shuffled,
    browser: Browser.Chrome,
    device: DeviceType.Desktop);

Console.WriteLine($"Rotating across {rotator.AvailableCount} matching candidates using Shuffled strategy:");
for (int i = 1; i <= 3; i++)
{
    Console.WriteLine($"  [Request #{i}] {rotator.NextUserAgent()}");
}
Console.WriteLine();

// 4. Session Affinity Simulation
Console.WriteLine("--- 4. Session Affinity for Web Crawlers ---");
Console.WriteLine("Ensuring multiple requests for the same session keep identical User-Agent profiles:");

var sessionManager = new UserAgentSessionManager(
    slidingExpiration: TimeSpan.FromMinutes(15),
    maxSessions: 1_000);

for (int step = 1; step <= 2; step++)
{
    var uaClientA = sessionManager.GetOrCreate("session-cookie-alice");
    var uaClientB = sessionManager.GetOrCreate("session-cookie-bob");

    Console.WriteLine($"  [Step {step}] Alice -> {uaClientA.Browser} on {uaClientA.Os} ({uaClientA.UserAgent[..45]}...)");
    Console.WriteLine($"  [Step {step}] Bob   -> {uaClientB.Browser} on {uaClientB.Os} ({uaClientB.UserAgent[..45]}...)");
}
Console.WriteLine();

// 5. HttpClient Integration Example
Console.WriteLine("--- 5. HttpClient Integration ---");
Console.WriteLine("Demonstrating HttpRequestMessage header injection:");

using var httpClient = new HttpClient();
var crawlerRotator = new UserAgentRotator(RotationStrategy.NoImmediateRepeat);

for (int req = 1; req <= 3; req++)
{
    string activeUserAgent = crawlerRotator.NextUserAgent();

    using var request = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/headers");
    request.Headers.TryAddWithoutValidation("User-Agent", activeUserAgent);

    Console.WriteLine($"  [HTTP Dispatch #{req}] Attached User-Agent: {activeUserAgent[..65]}...");
}

Console.WriteLine("\nDemo completed successfully.");
