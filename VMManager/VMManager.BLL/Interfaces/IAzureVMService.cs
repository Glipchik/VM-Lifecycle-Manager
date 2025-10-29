using VMManager.BLL.Models;

namespace VMManager.BLL.Interfaces;

public interface IAzureVMService
{
    Task<List<VMData>> CollectVMDataAsync(CancellationToken ct);
    Task ApplyPowerManagementRulesAsync(List<VMData> vmData, CancellationToken ct);
    Task<bool> ShutdownVMAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken ct);
    Task<bool> DeallocateVMAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken ct);
}
