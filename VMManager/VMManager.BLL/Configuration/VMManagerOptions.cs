namespace VMManager.BLL.Configuration;

public class VMManagerOptions
{
    public const string SectionName = "VMManager";

    public string CsvFilePath { get; set; } = null!;
    public string TrackingFilePath { get; set; } = null!;
    public int PollingIntervalMinutes { get; set; } = 5;
    public int ShutdownThresholdHours { get; set; } = 8;
}
