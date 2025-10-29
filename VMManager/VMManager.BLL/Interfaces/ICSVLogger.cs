using VMManager.BLL.Models;

namespace VMManager.BLL.Interfaces;

public interface ICSVLogger
{
    Task LogVMDataAsync(List<VMData> vmData, CancellationToken ct);
    Task InitializeCSVFileAsync(CancellationToken ct);
}
