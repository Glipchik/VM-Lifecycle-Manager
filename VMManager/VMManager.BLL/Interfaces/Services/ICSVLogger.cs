using VMManager.BLL.Models;

namespace VMManager.BLL.Interfaces;

public interface ICSVLogger
{
    Task LogVMDataAsync(List<VMModel> vmData, CancellationToken ct);
    Task InitializeCSVFileAsync(CancellationToken ct);
}
