using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using VMManager.Console.Jobs;
using VMManager.BLL.Interfaces;
using VMManager.BLL.Services;
using VMManager.BLL.Configuration;
using VMManager.BLL.DI;

var builder = Host.CreateApplicationBuilder(args);
IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false)
    .Build();

builder.Services.AddSingleton(configuration);

builder.Services.Configure<VMManagerOptions>(builder.Configuration.GetSection(VMManagerOptions.SectionName));
    
var ct = CancellationToken.None;

builder.Services.AddBllDependencies();

builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("VMPollingJob");
    
    q.AddJob<VMPollingJob>(opts => opts.WithIdentity(jobKey));
    
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("VMPollingJob-trigger")
        .WithSimpleSchedule(x => x
            .WithInterval(TimeSpan.FromMinutes(5))
            .RepeatForever())
        .StartNow());
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var csvLogger = scope.ServiceProvider.GetRequiredService<ICSVLogger>();
    var startTimeTracker = scope.ServiceProvider.GetRequiredService<IVMStartTimeTracker>();
    
    await csvLogger.InitializeCSVFileAsync(ct);
    await startTimeTracker.LoadStartTimesAsync(ct);
}

await host.RunAsync();