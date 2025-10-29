using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using VMManager.BLL.Constants;
using VMManager.BLL.Interfaces;
using VMManager.BLL.Models;

namespace VMManager.BLL.Services;

public class AzureVMService : IAzureVMService
{
    private readonly ArmClient _armClient;
    private readonly ILogger<AzureVMService> _logger;
    private readonly IVMStartTimeTracker _startTimeTracker;

    public AzureVMService(ILogger<AzureVMService> logger, IVMStartTimeTracker startTimeTracker)
    {
        _logger = logger;
        _startTimeTracker = startTimeTracker;
        
        var credential = new DefaultAzureCredential();
        _armClient = new ArmClient(credential);
    }

    public async Task<List<VMData>> CollectVMDataAsync(CancellationToken ct)
    {
        var allVmData = new List<VMData>();
        var timestamp = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting collection of VM data from all subscriptions");

            var subscriptions = await GetAllSubscriptionsAsync(ct);
            _logger.LogInformation("Found {Count} subscriptions", subscriptions.Count);

            var tasks = subscriptions.Select(subscription => 
                CollectVMDataFromSubscriptionAsync(subscription, timestamp, ct));
            
            var results = await Task.WhenAll(tasks);

            allVmData.AddRange(results.SelectMany(r => r).ToList());

            _logger.LogInformation("Collected data for {Count} VMs across all subscriptions", allVmData.Count);
            return allVmData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while collecting VM data");
            throw;
        }
    }

    private async Task<List<SubscriptionResource>> GetAllSubscriptionsAsync(CancellationToken ct)
    {
        var subscriptions = new List<SubscriptionResource>();
        
        try
        {
            await foreach (var subscription in _armClient.GetSubscriptions())
            {
                subscriptions.Add(subscription);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscriptions");
            throw;
        }

        return subscriptions;
    }

    private async Task<List<VMData>> CollectVMDataFromSubscriptionAsync(
        SubscriptionResource subscription, 
        DateTime timestamp,
        CancellationToken ct)
    {
        var vmDataList = new List<VMData>();

        try
        {
            _logger.LogDebug("Collecting VM data from subscription: {SubscriptionId}", subscription.Id);

            var resourceGroups = subscription.GetResourceGroups();
            List<Task<List<VMData>>> tasks = [];
            
            await foreach (var resourceGroup in resourceGroups)
            {
                tasks.Add(CollectVMDataFromResourceGroupAsync(subscription, resourceGroup, timestamp, ct));
            }

            var results = await Task.WhenAll(tasks);
            
            foreach (var result in results)
            {
                vmDataList.AddRange(result);
            }

            _logger.LogDebug("Collected {Count} VMs from subscription {SubscriptionId}", 
                vmDataList.Count, subscription.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting VM data from subscription {SubscriptionId}", subscription.Id);
        }

        return vmDataList;
    }

    private async Task<List<VMData>> CollectVMDataFromResourceGroupAsync(
        SubscriptionResource subscription, 
        ResourceGroupResource resourceGroup, 
        DateTime timestamp,
        CancellationToken ct)
    {
        var vmDataList = new List<VMData>();

        try
        {
            var vms = resourceGroup.GetVirtualMachines();
            
            foreach (var vm in vms)
            {
                try
                {
                    var vmData = await CreateVMDataAsync(vm, subscription, resourceGroup, timestamp, ct);
                    vmDataList.Add(vmData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing VM {VmName} in resource group {ResourceGroup}",
                        vm.Data.Name, resourceGroup.Data.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting VMs from resource group {ResourceGroup}", 
                resourceGroup.Data.Name);
        }

        return vmDataList;
    }

    private async Task<VMData> CreateVMDataAsync(
        VirtualMachineResource vm, 
        SubscriptionResource subscription, 
        ResourceGroupResource resourceGroup, 
        DateTime timestamp,
        CancellationToken ct)
    {
        var vmData = new VMData
        {
            Timestamp = timestamp,
            SubscriptionId = subscription.Id.SubscriptionId 
                             ?? throw new ArgumentNullException(subscription.Id.SubscriptionId),
            ResourceGroup = resourceGroup.Data.Name,
            ComputerName = vm.Data.Name,
            VMId = vm.Id.ToString(),
            Location = vm.Data.Location.Name,
            VMSize = vm.Data.HardwareProfile?.VmSize ?? VMConstants.Unknown,
            Tags = vm.Data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>(),
            HasAutoshutdownTag = vm.Data.Tags?.ContainsKey(VMConstants.Autoshutdown) == true && 
                                 vm.Data.Tags[VMConstants.Autoshutdown] == "1"
        };

        try
        {
            var data = await vm.InstanceViewAsync(ct);
            var vmPowerStateAndTime = GetPowerStateAndTimeFromInstanceView(data);
            vmData.PowerState = vmPowerStateAndTime.Item1;
            
            _startTimeTracker.UpdateVMStartTime(vmData.VMId, vmData.PowerState, vmPowerStateAndTime.Item2);
            
            vmData.LastStartTime = _startTimeTracker.GetVMStartTime(vmData.VMId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve instance view for VM {VmName}", vm.Data.Name);
            vmData.PowerState = VMConstants.Unknown;
        }

        return vmData;
    }

    private static (string, DateTimeOffset?) GetPowerStateAndTimeFromInstanceView(
        VirtualMachineInstanceView instanceView)
    {
        var statuses = instanceView.Statuses;
        if (statuses == null) return (VMConstants.Unknown, null);

        foreach (var status in statuses)
        {
            if (status.Code?.StartsWith("PowerState/") == true)
            {
                return (status.Code.Replace("PowerState/", ""), status.Time);
            }
        }

        return (VMConstants.Unknown, null);
    }

    public async Task ApplyPowerManagementRulesAsync(
        List<VMData> vmData,
        CancellationToken ct)
    {
        var autoshutdownVMs = vmData.Where(vm => vm.HasAutoshutdownTag).ToList();
        
        if (autoshutdownVMs.Count == 0)
        {
            _logger.LogInformation("No VMs with Autoshutdown tag found");
            return;
        }

        _logger.LogInformation("Applying power management rules to {Count} VMs with Autoshutdown tag", 
            autoshutdownVMs.Count);

        var tasks = autoshutdownVMs.Select(vm => ApplyPowerManagementRuleToVMAsync(vm, ct)).ToList();

        await Task.WhenAll(tasks);
    }

    private async Task ApplyPowerManagementRuleToVMAsync(
        VMData vm,
        CancellationToken ct)
    {
        try
        {
            switch (vm.PowerState.ToLowerInvariant())
            {
                case "running":
                    if (ShouldShutdownVM(vm))
                    {
                        _logger.LogInformation("Shutting down VM {VmName} - running for more than 8 hours", 
                            vm.ComputerName);
                        await ShutdownVMAsync(vm.SubscriptionId, vm.ResourceGroup, vm.ComputerName, ct);
                    }
                    break;

                case "stopped":
                    _logger.LogInformation("Deallocating VM {VmName} - Windows is shutdown", 
                        vm.ComputerName);
                    await DeallocateVMAsync(vm.SubscriptionId, vm.ResourceGroup, vm.ComputerName, ct);
                    break;

                case "deallocated":
                    _logger.LogDebug("VM {VmName} is already deallocated", vm.ComputerName);
                    break;

                default:
                    _logger.LogDebug("VM {VmName} is in state {PowerState} - no action needed", 
                        vm.ComputerName, vm.PowerState);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying power management rule to VM {VmName}", vm.ComputerName);
        }
    }

    private bool ShouldShutdownVM(VMData vm)
    {
        return _startTimeTracker.ShouldShutdownVM(vm.VMId, 8);
    }

    public async Task ShutdownVMAsync(
        string subscriptionId,
        string resourceGroup,
        string vmName,
        CancellationToken ct)
    {
        try
        {
            var subscription = _armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId));
            var resourceGroupResource = await subscription.GetResourceGroupAsync(resourceGroup, ct);
            var vm = await resourceGroupResource.Value.GetVirtualMachineAsync(vmName, cancellationToken: ct);

            if (vm.Value?.Id is null)
            {
                _logger.LogWarning("VM {VmName} not found in resource group {ResourceGroup}", vmName, resourceGroup);
                return;
            }

            await vm.Value.PowerOffAsync(WaitUntil.Completed, cancellationToken: ct);
            _startTimeTracker.UpdateVMStartTime(vm.Value.Id, VMConstants.StoppedState, DateTimeOffset.Now);
            _logger.LogInformation("Successfully shut down VM {VmName}", vmName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error shutting down VM {VmName}", vmName);
        }
    }

    public async Task DeallocateVMAsync(
        string subscriptionId, 
        string resourceGroup, 
        string vmName,
        CancellationToken ct)
    {
        try
        {
            var subscription = _armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId));
            var resourceGroupResource = await subscription.GetResourceGroupAsync(resourceGroup, ct);
            var vm = await resourceGroupResource.Value.GetVirtualMachineAsync(vmName, cancellationToken: ct);

            if (vm.Value?.Id is null)
            {
                _logger.LogWarning("VM {VmName} not found in resource group {ResourceGroup}", vmName, resourceGroup);
                return;
            }

            await vm.Value.DeallocateAsync(WaitUntil.Completed, cancellationToken: ct);
            _startTimeTracker.UpdateVMStartTime(vm.Value.Id, VMConstants.DeallocatedState, DateTimeOffset.Now);
            _logger.LogInformation("Successfully deallocated VM {VmName}", vmName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deallocating VM {VmName}", vmName);
        }
    }
}
