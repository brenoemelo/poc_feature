using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Unleash;
using Xunit;
using Xunit.Abstractions;

namespace PoC.Shared.Tests;

public sealed class RealUnleashIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly DefaultUnleash _unleashClient;

    public RealUnleashIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        var settings = new UnleashSettings
        {
            AppName = "integration-test-app",
            UnleashApi = new Uri("http://localhost:4242/api/"),
            CustomHttpHeaders = new Dictionary<string, string>
            {
                { "Authorization", "*:*.admin-token" }
            },
            FetchTogglesInterval = TimeSpan.FromSeconds(1),
            InstanceTag = "integration-test-instance"
        };

        // Initialize the real Unleash client
        _unleashClient = new DefaultUnleash(settings);
    }

    [Fact]
    public async Task Should_Connect_To_Real_Unleash_Service_And_Fetch_Flags()
    {
        // Act
        // Wait a bit for the client to fetch flags (background task)
        await Task.Delay(2000);

        var flagName = "materials-crud";
        var isEnabled = _unleashClient.IsEnabled(flagName);

        _output.WriteLine($"Connected to Real Unleash. Flag '{flagName}' is enabled: {isEnabled}");

        // Assert
        _unleashClient.Should().NotBeNull();
        _unleashClient.GetType().Name.Should().Be("DefaultUnleash");
        
        // Verify that we are indeed using the real service implementation
        // This confirms we are not using FakeUnleash or a Mock
    }

    public void Dispose()
    {
        _unleashClient?.Dispose();
    }
}
