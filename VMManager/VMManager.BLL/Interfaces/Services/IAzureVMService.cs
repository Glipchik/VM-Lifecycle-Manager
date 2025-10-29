using VMManager.BLL.Models;

namespace VMManager.BLL.Interfaces;

public interface IAzureVMService
{
    Task<List<VMData>> CollectVMDataAsync(CancellationToken ct);
    Task ApplyPowerManagementRulesAsync(List<VMData> vmData, CancellationToken ct);
    Task ShutdownVMAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken ct);
    Task DeallocateVMAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken ct);
}
