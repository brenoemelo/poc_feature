using Microsoft.Extensions.Configuration;
using RestSharp;

namespace PoC.E2E.Common;

public abstract class ApiTestBase
{
    protected RestClient Client { get; set; }
    protected IConfiguration Config { get; }

    protected ApiTestBase()
    {
        Config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json")
            .AddEnvironmentVariables()
            .Build();

        var baseUrl = Config["BaseUrl"] ?? throw new InvalidOperationException("BaseUrl not found");
        
        var options = new RestClientOptions(baseUrl)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        
        Client = new RestClient(options);
    }
}
