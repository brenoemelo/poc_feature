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
        var unleashApiUrl = Config["UnleashApiUrl"] ?? "http://localhost:4242/api";
        var unleashAdminToken = Config["UnleashAdminToken"] ?? "*:*.admin-token";

        FeatureManager = new FeatureFlagManager(unleashApiUrl, unleashAdminToken);
        
        // Ensure we start with a clean state if needed, or rely on individual tests
        await Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        if (FeatureManager != null)
        {
            // Revert any flags modified during the test
            await FeatureManager.ResetAllTrackedFlagsAsync();
        }
    }
}
