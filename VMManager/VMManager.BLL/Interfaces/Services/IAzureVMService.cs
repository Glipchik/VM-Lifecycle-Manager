using VMManager.BLL.Models;

namespace VMManager.BLL.Interfaces;

public interface IAzureVMService
{
    Task<List<VMModel>> CollectVMDataAsync(CancellationToken ct);
    Task ApplyPowerManagementRulesAsync(List<VMModel> vmData, CancellationToken ct);
    Task ShutdownVMAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken ct);
    Task DeallocateVMAsync(string subscriptionId, string resourceGroup, string vmName, CancellationToken ct);
}
