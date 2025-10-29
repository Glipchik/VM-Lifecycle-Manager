using Microsoft.Extensions.DependencyInjection;
using VMManager.BLL.Interfaces;
using VMManager.BLL.Services;

namespace VMManager.BLL.DI;

public static class BLLConfiguration
{
    public static void AddBllDependencies(this IServiceCollection services)
    {
        services.AddSingleton<IVMStartTimeTracker, VMStartTimeTracker>();
        services.AddScoped<IAzureVMService, AzureVMService>();
        services.AddScoped<ICSVLogger, CSVLogger>();
    }
}