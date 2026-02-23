# Feature Flags Implementation Guide

This document details the Feature Flags implementation in the PoC project using the `PoC.FeatureFlags` library and Unleash as the provider.

## 1. Overview

The Feature Flag system allows decoupling deployment from release, enabling safer rollouts, A/B testing, and quick rollbacks without redeploying code.
In this project, we use **Unleash** as the centralized Feature Flag Management System.

## 2. Infrastructure Requirements

To run the Feature Flag system locally, the following Docker containers are required (defined in `docker-compose.yml`):

1.  **Unleash Server** (`unleashorg/unleash-server`): The management UI and API.
    *   Port: `4242`
    *   Dashboard: [http://localhost:4242](http://localhost:4242)
    *   Credentials: `admin` / `password` (or `admin2` / `password` if using backup).
2.  **PostgreSQL** (`postgres:15.5-alpine`): The database backend for Unleash.
3.  **Unleash Init** (`badouralix/curl-jq`): A helper container that runs `scripts/init-unleash.sh` to automatically configure default flags and strategies on startup.

## 3. Project Structure: `PoC.FeatureFlags`

The `PoC.FeatureFlags` project encapsulates all logic related to feature management. It provides a clean abstraction over the Unleash Client SDK.

### Key Components

*   **`Configuration/FeatureFlagOptions.cs`**:
    *   Defines configuration properties like API URL, API Key, and Polling Interval.
    *   Default Polling Interval: **15 seconds**.
*   **`Extensions/FeatureFlagsExtensions.cs`**:
    *   `AddPoCFeatureFlags`: Extension method to register the Unleash Client in the DI container.
    *   Handles synchronous initialization (blocks startup until flags are fetched to ensure consistency).
*   **`Extensions/FeatureGateExtensions.cs`**:
    *   `WithFeatureGate(flagKey)`: A Minimal API filter to protect endpoints. Returns `404 Not Found` if the flag is disabled.
*   **`Extensions/FakeUnleash.cs`**:
    *   A fallback implementation for testing or when the real Unleash server is unreachable/disabled (e.g., via "fake" URL).

## 4. Configuration

Services consume `PoC.FeatureFlags` by configuring it in `Program.cs`. Configuration is driven by `appsettings.json` or Environment Variables.

### `appsettings.json` Example

```json
"FeatureFlags": {
  "UnleashApiUrl": "http://localhost:4242/api/",
  "UnleashApiKey": "*:development.unleash-insecure-api-token",
  "UnleashAppName": "PoC-Service-Name",
  "UnleashInstanceId": "service-instance-id",
  "FetchTogglesIntervalSeconds": 15
}
```

### Environment Variables (Override)

*   `FeatureFlags__UnleashApiUrl`
*   `FeatureFlags__UnleashApiKey`
*   `FeatureFlags__FetchTogglesIntervalSeconds`

## 5. How to Adopt in a New Project

1.  **Add Reference**: Add a project reference to `PoC.FeatureFlags`.
2.  **Register Services**: In `Program.cs`, call `AddPoCFeatureFlags`.

    ```csharp
    builder.Services.AddPoCFeatureFlags(options =>
    {
        // Bind from configuration
        builder.Configuration.GetSection("FeatureFlags").Bind(options);
        
        // Optional: Set defaults if config is missing
        options.UnleashAppName = "My-New-Service";
    });
    ```

3.  **Protect Endpoints (Minimal API)**:

    ```csharp
    app.MapPost("/api/v1/new-feature", () =>Results.Ok("Feature is ON"))
       .WithFeatureGate("new-feature-flag");
    ```

4.  **Check Flags in Code (Manual Check)**:
    Inject `IUnleash` into your service/handler.

    ```csharp
    public class MyService(IUnleash unleash)
    {
        public void DoWork()
        {
            if (unleash.IsEnabled("my-feature-flag"))
            {
                // New logic
            }
        }
    }
    ```

## 6. Unleash Details

For deep technical details on how Unleash works, including propagation timing and architecture, see:
[[unleash-details]]

## 7. Troubleshooting

*   **Flag not updating?** Check the `FetchTogglesIntervalSeconds`. The default is 15s, so it may take up to 15s for a change in the UI to propagate to the service.
*   **Connection Refused?** Ensure the `unleash` container is running and healthy (`docker ps`).
*   **"Feature Disabled" log?** The `FeatureGate` filter logs a warning when blocking a request. Check your logs/Observability stack.
