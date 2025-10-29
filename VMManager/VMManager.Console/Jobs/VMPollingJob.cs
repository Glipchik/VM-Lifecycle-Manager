using Microsoft.Extensions.Logging;
using Quartz;
using VMManager.BLL.Interfaces;
using VMManager.BLL.Services;

namespace VMManager.Console.Jobs;

public class VMPollingJob(
    IAzureVMService vmService,
    ICSVLogger csvLogger,
    VMStartTimeTracker startTimeTracker,
    ILogger<VMPollingJob> logger)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            logger.LogInformation("Starting VM polling cycle at {Timestamp}", DateTime.UtcNow);

            var ct = context.CancellationToken;
            ct.ThrowIfCancellationRequested();
            
            var vmData = await vmService.CollectVMDataAsync(ct);
            
            await csvLogger.LogVMDataAsync(vmData, ct);
            
            // await vmService.ApplyPowerManagementRulesAsync(vmData);
            
            await startTimeTracker.SaveStartTimesAsync();
            
            logger.LogInformation("Completed VM polling cycle at {Timestamp}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during VM polling cycle");
        }
    }
}