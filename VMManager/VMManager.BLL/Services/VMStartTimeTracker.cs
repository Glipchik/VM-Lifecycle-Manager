using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VMManager.BLL.Configuration;
using VMManager.BLL.Interfaces;

namespace VMManager.BLL.Services;

public class VMStartTimeTracker : IVMStartTimeTracker
{
    private readonly ILogger<IVMStartTimeTracker> _logger;
    private readonly string _trackingFilePath;
    private readonly Dictionary<string, DateTime> _vmStartTimes = new();
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public VMStartTimeTracker(ILogger<IVMStartTimeTracker> logger, IOptions<VMManagerOptions> options)
    {
        _logger = logger;
        var trackingFilePath = options.Value.TrackingFilePath;
        _trackingFilePath = Path.IsPathRooted(trackingFilePath) 
            ? trackingFilePath 
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), trackingFilePath);
        
        _logger.LogInformation("VM start time tracking file will be created at: {FilePath}", _trackingFilePath);
    }

    public async Task LoadStartTimesAsync(CancellationToken ct)
    {
        await _fileLock.WaitAsync(ct);
        try
        {
            if (File.Exists(_trackingFilePath))
            {
                var json = await File.ReadAllTextAsync(_trackingFilePath, ct);
                var data = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);
                if (data != null)
                {
                    _vmStartTimes.Clear();
                    foreach (var kvp in data)
                    {
                        _vmStartTimes[kvp.Key] = kvp.Value;
                    }
                    _logger.LogInformation("Loaded {Count} VM start times from tracking file", _vmStartTimes.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading VM start times from file");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveStartTimesAsync(CancellationToken ct)
    {
        await _fileLock.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(_vmStartTimes, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_trackingFilePath, json, ct);
            _logger.LogDebug("Saved {Count} VM start times to tracking file", _vmStartTimes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving VM start times to file");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void UpdateVMStartTime(string vmId, string powerState, DateTimeOffset? time)
    {
        var stateTime = time?.DateTime;
        var now = DateTime.UtcNow;
        
        if (powerState.Equals("running", StringComparison.OrdinalIgnoreCase))
        {
            if (!_vmStartTimes.TryAdd(vmId, stateTime ?? now)) return;

            _logger.LogDebug("Started tracking VM {VmId} at {StartTime}", vmId, stateTime ?? now);
        }
        else
        {
            if (!_vmStartTimes.Remove(vmId)) return;
            
            _logger.LogDebug("Stopped tracking VM {VmId}", vmId);
        }
    }

    public DateTime? GetVMStartTime(string vmId)
    {
        return _vmStartTimes.TryGetValue(vmId, out var startTime) ? startTime : null;
    }

    public bool ShouldShutdownVM(string vmId, int thresholdHours = 8)
    {
        var startTime = GetVMStartTime(vmId);
        if (startTime is null) return false;

        var runningTime = DateTime.UtcNow - startTime.Value;
        return runningTime.TotalHours >= thresholdHours;
    }
}
