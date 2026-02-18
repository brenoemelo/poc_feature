using Microsoft.Extensions.Configuration;
using RestSharp;

namespace PoC.E2E.Common;

public abstract class ApiTestBase : IAsyncLifetime
{
    protected RestClient Client { get; set; }
    protected IConfiguration Config { get; }
    protected FeatureFlagManager FeatureManager { get; private set; } = null!;

    protected ApiTestBase()
    {
        Config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseUrl = Config["BaseUrl"] ?? throw new InvalidOperationException("BaseUrl not found in configuration");
        
        var options = new RestClientOptions(baseUrl)
        {
            Timeout = TimeSpan.FromSeconds(30) // Increased timeout for E2E
        };
        
        Client = new RestClient(options);
    }

    public virtual async Task InitializeAsync()
    {
        var solutionRoot = FindSolutionRoot(AppContext.BaseDirectory);
        if (solutionRoot == null) throw new DirectoryNotFoundException("Could not find solution root");

        var flagsPath = Path.Combine(solutionRoot, "docker", "feature-flags", "flags.yaml");
        if (!File.Exists(flagsPath)) throw new FileNotFoundException($"Flags file not found at {flagsPath}");

        // Use configured URL or default
        var goffUrl = Config["GoFeatureFlagUrl"] ?? "http://localhost:1031";

        FeatureManager = new FeatureFlagManager(flagsPath, goffUrl);
        
        // Ensure we start with a clean state if needed, or rely on individual tests
        // For now, we don't reset all flags here to avoid excessive file I/O on every test start
        // unless we want strict isolation.
        // Let's assume tests will set what they need.
    }

    public virtual async Task DisposeAsync()
    {
        if (FeatureManager != null)
        {
            // Revert any flags modified during the test
            await FeatureManager.ResetAllTrackedFlagsAsync();
        }
    }

    private static string? FindSolutionRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Any())
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
