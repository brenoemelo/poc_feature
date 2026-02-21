using System.Reflection;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.APIGatewayEvents;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace PoC.DataHelper;

public class Program
{
    protected Program() { }

    public static async Task Main()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var versionInfo = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Console.WriteLine("===================================================");
        Console.WriteLine("[STARTUP] Executando PoC.DataHelper");
        Console.WriteLine($"[STARTUP] Versão do Build: {versionInfo}");
        Console.WriteLine("===================================================");

        var builder = WebApplication.CreateBuilder();

        builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

        var app = builder.Build();

        app.MapGet("/api/v1/datahelper/version", () => Results.Ok(new { version = versionInfo }));

        await app.RunAsync();
    }
}

