# Azure VM Lifecycle Manager

## Purpose
A .NET 9 console-based solution to periodically manage and log the power state of Azure VMs across multiple subscriptions in a tenant. Target use case: automatic stopping/deallocating of VMs with an `Autoshutdown` tag, and audit logging to CSV.

## Key Features & Architecture

- **Quartz Scheduler:**
  - Uses Quartz.NET to run the polling job (VMPollingJob) every 5 minutes automatically.

- **File Write Synchronization:**
  - CSV writes (logging VM data) are protected by a `SemaphoreSlim` to ensure thread-safe, non-overlapping access and prevent concurrent file corruption.

- **VM Start Time Tracking:**
  - Each VM's (with `Autoshutdown` tag) start time is tracked in-memory in a dictionary, *and* saved to a separate file (usually on the Desktop) in JSON format for process restarts.

- **Services & Layers:**
  - **Console/Jobs:** Entrypoint and recurring job scheduling via Quartz.
  - **AzureVMService:** Core logic for fetching VM status, applying power rules (stop/deallocate), batching, and parallel processing.
  - **CSVLogger:** Manages append-only CSV logging of all discovered VMs and their states.
  - **VMStartTimeTracker:** Memory+file-backed storage of VM start dates for 8-hour rule enforcement, ensures consistency through application lifecycles.
  - All resource-intensive or shared state actions are protected via async locks and careful cancellation token handling.

- **Configuration:**
  - Most file paths, polling intervals, and thresholds are configurable via `appsettings.json`.
- **Authentication**
  - Uses `DefaultAzureCredential` for authentication with Azure.

- **Parallel & Async:**
  - Asynchronously collects VMs info across subscriptions and resource groups in parallel for maximum efficiency.
  - Actual Azure SDK calls and internal operations support `CancellationToken` for graceful shutdown.

## How to Use
1. **Build with [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) installed.**
2. Configure your desired options in `VMManager.Console/appsettings.json` (CSV and state tracking file locations, etc.).
3. Authenticate to Azure (e.g., `az login` or Managed Identity in cloud).
4. Run the console app: `dotnet run --project VMManager.Console`.
5. Find log/output files (by default on Desktop, or per your config). CSV for VM inventory, JSON for start times.

## Additional Notes
- All VM scan results (with state, resourceGroup, etc) are appended to a CSV for audit/history.
- VM "has been running too long" is enforced using both state file (persistent) and runtime in-memory checks.
- Handles shutdown, exceptions, and errors gracefully thanks to async and cancellation token propagation.
