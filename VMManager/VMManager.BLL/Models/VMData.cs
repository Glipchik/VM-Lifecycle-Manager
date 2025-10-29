using Azure.ResourceManager.Compute.Models;

namespace VMManager.BLL.Models;

public class VMData
{
    public DateTime Timestamp { get; set; }
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string PowerState { get; set; } = string.Empty;
    public bool HasAutoshutdownTag { get; set; }
    public DateTime? LastStartTime { get; set; }
    public string VMId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public VirtualMachineSizeType VMSize { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}
