using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using PoC.FeatureFlags.Extensions;
using Unleash;
using Xunit;

namespace PoC.Shared.Tests;

public class FeatureFlagTests
{
    [Fact]
    public void AddPoCFeatureFlags_ShouldRegisterFakeUnleash_WhenUrlContainsFake()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddPoCFeatureFlags(options =>
        {
            options.UnleashApiUrl = "http://fake-unleash/api/";
            options.UnleashApiKey = "some-key";
            options.UnleashAppName = "TestApp";
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var unleash = serviceProvider.GetService<IUnleash>();
        unleash.Should().NotBeNull();
        unleash.GetType().Name.Should().Be("FakeUnleash");
    }

    [Fact]
    public async Task FeatureGate_ShouldBlock_WhenFlagIsDisabledAsync()
    {
        // Arrange
        var mockUnleash = new Mock<IUnleash>();
        mockUnleash.Setup(u => u.IsEnabled("test-flag")).Returns(false);

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(mockUnleash.Object);
                        services.AddRouting();
                        services.AddLogging();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/test", () => "Allowed")
                                .WithFeatureGate("test-flag");
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/test");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FeatureGate_ShouldAllow_WhenFlagIsEnabledAsync()
    {
        // Arrange
        var mockUnleash = new Mock<IUnleash>();
        mockUnleash.Setup(u => u.IsEnabled("test-flag")).Returns(true);

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(mockUnleash.Object);
                        services.AddRouting();
                        services.AddLogging();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/test", () => "Allowed")
                                .WithFeatureGate("test-flag");
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/test");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Allowed");
    }
}
