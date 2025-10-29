namespace VMManager.BLL.Interfaces;

public interface IVMStartTimeTracker
{
    Task LoadStartTimesAsync(CancellationToken ct);
    Task SaveStartTimesAsync(CancellationToken ct);
    void UpdateVMStartTime(string vmId, string powerState, DateTimeOffset? time);
    DateTime? GetVMStartTime(string vmId);
    bool ShouldShutdownVM(string vmId, int thresholdHours = 8);
}