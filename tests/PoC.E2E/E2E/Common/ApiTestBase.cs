using Microsoft.Extensions.Configuration;
using RestSharp;

namespace E2E.Common;

public abstract class ApiTestBase
{
    protected readonly RestClient Client;
    protected readonly IConfiguration Config;

    protected ApiTestBase()
    {
        Config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json")
            .AddEnvironmentVariables() 

        var baseUrl = Config["BaseUrl"] ?? throw new ArgumentNullException("BaseUrl not found");
        
        var options = new RestClientOptions(baseUrl)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        
        Client = new RestClient(options);
    }
}