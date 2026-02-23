# Unleash Technical Details

This document explains the internal architecture of Unleash and how it propagates feature flag changes to our applications.

## 1. What is Unleash?

[Unleash](https://www.getunleash.io/) is an open-source Feature Management Platform. It allows you to toggle functionality on/off at runtime without redeploying your code. It separates deployment (moving code to production) from release (making features available to users).

## 2. Architecture: Client-Side Evaluation

Unleash uses a **Client-Side Evaluation** architecture. This is a critical distinction from other systems that evaluate flags on the server (API call per request).

### How It Works (The Polling Cycle)

1.  **Background Polling**: The Unleash Client SDK (running inside our .NET microservices) starts a background thread.
2.  **Fetch Configuration**: Every **15 seconds** (default), this thread calls the Unleash Server API to download the *entire* list of feature toggles and strategies.
3.  **Smart Caching (ETag)**:
    *   The SDK sends the `ETag` of the last successful response.
    *   If the configuration hasn't changed on the server, the API returns **`304 Not Modified`** with an empty body.
    *   **Impact**: This makes the network traffic negligible when flags aren't changing.
4.  **Local Memory**: The configuration is stored in the application's RAM (in-memory cache).

#### Evaluation (The Check)

When your code executes:
```csharp
// This line takes nanoseconds
if (unleash.IsEnabled("new-checkout-flow")) 
{
    // ...
}
```
*   **Zero Network Latency**: The check runs purely against the local in-memory cache. It **NEVER** calls the API during the request flow.
*   **Zero Blocking**: Since it's a memory lookup, it doesn't block the thread or wait for I/O.
*   **Privacy Safe**: Context data (User ID, Email, IP) is evaluated locally against the logic in the cache. No user data is ever sent to the Unleash Server.

### Benefits

*   **Performance**: Checking a flag is an in-memory operation (nanoseconds), not a network call.
*   **Resilience**: If the Unleash Server goes down, the client continues to work using the last known configuration (cached locally).
*   **Privacy**: User context (e.g., User ID, IP) never leaves the application; it is evaluated locally.

## 3. Propagation Time (Latency)

Because of the polling architecture, changes made in the Unleash UI are **not instant**.

*   **Polling Interval**: In our PoC, this is configured via `FetchTogglesIntervalSeconds` (default: **15 seconds**).
*   **Propagation Delay**: It can take up to **15 seconds** (or whatever interval is set) for a change in the UI to be reflected in all running service instances.
*   **Consistency**: During this window, some instances might have the old configuration while others have the new one (Eventual Consistency).

### Configuring the Interval

You can adjust the polling interval in `appsettings.json` or via environment variables:

```json
"FeatureFlags": {
  "FetchTogglesIntervalSeconds": 5 // Aggressive polling for dev/test
}
```

> **Warning**: Setting this too low in production can overload the Unleash Server if you have many connected clients.

## 4. Resilience & Fallback

Unleash SDKs are designed to be resilient.

1.  **Startup**: The SDK attempts to fetch the initial configuration. If the server is unreachable, it can use a local backup file (if enabled/available) or default values.
2.  **Runtime**: If the server becomes unreachable during runtime, the SDK simply continues using the last successful configuration from memory.
3.  **Backup File**: The SDK can persist the configuration to a local file on disk to survive application restarts even if the server is down.

## 5. Activation Strategies

Unleash supports powerful activation strategies beyond simple on/off toggles:

*   **Standard**: On/Off for everyone.
*   **UserID**: Enable for specific user IDs.
*   **Gradual Rollout**: Enable for a percentage of users (e.g., 10%, 50%) to test stability (Canary Release).
*   **IP Address**: Enable for specific IP ranges (e.g., internal office network).
*   **Hostname**: Enable for specific server instances.

In our PoC, we primarily use the **Standard** strategy, but the infrastructure supports all of them.

## 6. Official Documentation

*   [Unleash Architecture](https://docs.getunleash.io/topics/architecture)
*   [Client SDKs](https://docs.getunleash.io/sdks)
*   [.NET SDK](https://github.com/Unleash/unleash-client-dotnet)

## 7. Considerations for AWS Lambda & Serverless

Using Unleash (or any background-polling SDK) in a serverless environment like AWS Lambda introduces specific challenges that must be managed.

### 7.1. Warm-up Time Impact (Cold Starts)

*   **Network Latency:** During a "Cold Start" (when a new Lambda instance is created), the SDK must perform an initial HTTP call to the Unleash Server to fetch the configuration.
    *   Since we use `synchronousInitialization: true`, the Lambda **waits** for this call to succeed before processing the user's request.
    *   **Impact:** Adds `Network Latency + JSON Deserialization Time` to the first request.
*   **Mitigation:**
    *   **Provisioned Concurrency:** Keeps Lambda instances initialized (expensive but eliminates cold starts).
    *   **Unleash Proxy:** Using a local Unleash Proxy can reduce the payload size and connection time.

### 7.2. Memory Usage Impact

*   **In-Memory Cache:** The SDK stores a copy of *all* feature flags and strategies in the Lambda's memory.
*   **Size:** For typical usage (hundreds of flags), this is negligible (< 5MB). However, if you have thousands of complex flags, you may need to increase the Lambda's memory allocation.
*   **Cost:** Higher memory allocation increases AWS costs, though it often reduces duration (and thus cost) for compute-bound tasks.

### 7.3. The "Frozen" Execution Context

AWS Lambda "freezes" the execution environment (CPU) between invocations.

*   **Paused Polling:** The background polling thread is paused when the Lambda is idle. It does **not** run every 15 seconds while the function is sleeping.
*   **Stale Data:** If a Lambda wakes up after 10 minutes, its flags might be 10 minutes old. It will process the current request with *old* flags and then trigger a poll update for *subsequent* requests.
*   **Metrics Loss:** Client metrics (usage data) are aggregated and sent periodically. If the Lambda is destroyed before the metrics interval triggers, that usage data is lost.

### 7.4. Connection Exhaustion (Thundering Herd) & Unleash Proxy

In a Serverless architecture, your application can scale from 0 to 1,000+ instances in seconds. This creates a unique challenge for database-backed services like Unleash.

#### The Problem: "Thundering Herd"
If traffic spikes and AWS scales your service to **1,000 concurrent Lambda instances**, all 1,000 instances will attempt to initialize the Unleash Client simultaneously.
*   **Result:** 1,000 simultaneous HTTP requests to the Unleash API and 1,000 database connections.
*   **Risk:** This effectively DDoS's your own infrastructure, causing the Unleash Server or its PostgreSQL database to crash due to connection limits.

#### The Solution: Unleash Proxy (or Unleash Edge)
To solve this, we introduce a middleware component: the **Unleash Proxy** (or its modern successor, **Unleash Edge**).

*   **Role:** It acts as a highly scalable "cache layer" between your Lambdas and the Unleash API.
*   **Architecture:**
    1.  **Lambdas -> Proxy:** All 1,000 Lambdas connect to the Proxy (which is stateless and designed for high concurrency).
    2.  **Proxy -> Unleash API:** The Proxy maintains only **one** (or very few) stable connections to the Unleash API/Database to fetch updates.
*   **Benefits:**
    *   **Protection:** Shields the database from traffic spikes.
    *   **Performance:** The Proxy stores flags in memory and serves them instantly to Lambdas, often reducing initialization time.
    *   **Security:** Keeps the master API keys secure within the Proxy; Lambdas only need a client key.
    *   **Licensing:** Unleash Edge is **Open Source (MIT License)** and free to use. While there is a paid Enterprise version with advanced capabilities, the core caching and proxying features needed to solve the "Thundering Herd" problem are fully available in the free open-source version.

> **Recommendation:** For high-scale production environments, deploying **Unleash Edge** (Rust-based, low footprint) as a sidecar or distinct service (e.g., Fargate) is highly recommended.

## 8. Scenario: Hosting Unleash on Amazon ECS (Inside VPC)

In this scenario, the Unleash Server (and optionally the Database) is hosted in Docker containers managed by **Amazon ECS (Fargate)**, running strictly within your private **VPC**.

### 8.1. Architecture Behavior
*   **Topology:**
    *   **Lambda Functions:** Run in Private Subnets.
    *   **Unleash Service (ECS):** Runs in Private Subnets behind an **Internal Application Load Balancer (ALB)**.
    *   **Database (RDS/Aurora):** Runs in isolated Data Subnets.
*   **Flow:**
    `Lambda` -> `Internal ALB` -> `Unleash Container (ECS)` -> `RDS PostgreSQL`

### 8.2. Gains (Pros)
1.  **Security (Zero Trust):** Traffic never leaves your private network. No public endpoints are exposed. You can restrict access strictly via Security Groups (e.g., "Allow Inbound on Port 4242 only from Lambda Security Group").
2.  **Performance:** Extremely low latency (sub-millisecond) network hops within the AWS Backbone.
3.  **Cost Efficiency:** Avoids NAT Gateway data processing charges for flag updates, as traffic stays internal (unlike reaching out to a SaaS Unleash).

### 8.3. Losses (Cons) & Trade-offs
1.  **Operational Overhead:** You are responsible for patching the OS (if EC2), updating the Unleash Docker image, managing the Database, and scaling the ECS tasks.
2.  **Cold Start Complexity:** Lambdas inside a VPC previously had slower cold starts (ENI creation), though AWS has largely solved this (Hyperplane ENI).
3.  **Fixed Costs:** You pay for the ALB and Fargate tasks 24/7, even if no one uses the system (unlike a pure Serverless approach).

### 8.4. Configuration Recommendations
*   **Service Discovery:** Use AWS Cloud Map or simply the Internal ALB DNS name for the `UnleashApiUrl`.
*   **Scaling:** Configure ECS Auto Scaling based on CPU/Memory to handle traffic spikes.
*   **The Proxy Factor:** Even in ECS, if you have thousands of Lambdas, **you still need the Unleash Proxy/Edge**.
    *   *Deployment:* Run `unleash-edge` as a separate ECS Service or as a Sidecar in the same Task Definition.
    *   *Why?* To protect the ECS Tasks and Database from the "Thundering Herd" of 1,000+ Lambdas connecting at once.

## 9. Licensing & Commercial Use

Unleash offers two primary distributions: **Open Source** and **Enterprise**.

### 9.1. Open Source Edition (Self-Hosted)
*   **License:** [Apache License 2.0](https://github.com/Unleash/unleash/blob/main/LICENSE)
*   **Commercial Use:** **YES**, you can use the Open Source version for commercial purposes, including in production environments for profit-generating applications.
*   **Constraints:**
    *   **Single Environment:** The UI is designed for a single environment (e.g., you can't toggle "Development" vs "Production" tabs easily in the same project view). You typically run separate instances (URLs) for Dev and Prod.
    *   **No SSO:** Does not include Single Sign-On (SAML/OIDC) out of the box (requires Enterprise).
    *   **No RBAC:** Basic user management, lacks granular Role-Based Access Control.
*   **Cost:** Free (forever). You only pay for the infrastructure (AWS/Azure) to host it.

### 9.2. Enterprise Edition
*   **License:** Proprietary / Commercial Subscription.
*   **Features:** Adds SSO, RBAC, Multi-Environment support, Audit Logs, and Premium Support.
*   **Target:** Large organizations with complex compliance and team management needs.

> **Conclusion for this PoC:** We are using the **Open Source** version. It is fully compliant for commercial use and sufficient for our architectural needs (Microservices, Feature Flags, Strategy constraints). The "Single Environment" limitation is mitigated by our Infrastructure-as-Code approach (deploying distinct stacks for Dev/Test/Prod).

## 10. Cost Estimation (AWS - Self-Hosted)

This section provides a monthly cost estimate for hosting the complete Unleash stack on AWS to support the defined workload.

### 10.1. Scenario Definition
*   **Workload:** 200 distinct Lambda functions (Microservices) accessing feature flags.
*   **Traffic Load:** **50 Requests Per Second (RPS)** aggregate load on the Unleash infrastructure (polling/evaluating).
*   **Availability:** **24/7** (Always On).
*   **Region:** `us-east-1` (N. Virginia).

### 10.2. Recommended Architecture (Production)
To handle 50 RPS with high availability and "Thundering Herd" protection, we use:
1.  **Unleash Edge (Proxy Layer):** 2x ECS Fargate Tasks (ARM64/Graviton) for High Availability.
2.  **Load Balancer:** 1x Internal Application Load Balancer (ALB) to distribute traffic from Lambdas to Edge.
3.  **Unleash Server (Management API):** 1x ECS Fargate Task (ARM64) for the UI and Admin API.
4.  **Database:** 1x RDS PostgreSQL `db.t4g.micro` (Single-AZ).

### 10.3. Monthly Cost Breakdown (Estimated)

| Component | Configuration | Est. Cost (Monthly) |
| :--- | :--- | :--- |
| **Compute (Unleash Edge)** | 2 Tasks x 0.25 vCPU / 0.5 GB RAM (ARM64) | ~$14.00 |
| **Compute (Unleash Server)** | 1 Task x 0.5 vCPU / 1 GB RAM (ARM64) | ~$15.00 |
| **Networking (ALB)** | 1 Internal ALB (730 hours + ~2 LCUs*) | ~$28.00 |
| **Database (RDS)** | db.t4g.micro (Single-AZ) + 20GB gp3 Storage | ~$14.00 |
| **Total** | | **~$71.00 / month** |

*\*LCU Calculation: 50 RPS results in approx. 2 LCUs (driven mainly by new connections/sec if Keep-Alive is not perfectly utilized by Lambdas).*

### 10.4. Optimization Tips
*   **Savings Plans:** Committing to a 1 or 3-year Compute Savings Plan can reduce Fargate/EC2 costs by **30-50%**.
*   **ARM64 (Graviton):** We use ARM architecture in the estimate because it is **20% cheaper** and provides better performance for these workloads than x86.
*   **Single Task (Non-Prod):** For Dev/Test environments, you can run a single Task (Unleash Server only, no Edge, no ALB) and expose it via a cheaper method (e.g., Cloud Map), reducing the cost to **<$30/month**.


