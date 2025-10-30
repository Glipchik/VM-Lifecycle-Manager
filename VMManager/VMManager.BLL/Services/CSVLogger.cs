using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VMManager.BLL.Interfaces;
using VMManager.BLL.Models;
using VMManager.BLL.Configuration;
using System.Globalization;

namespace VMManager.BLL.Services;

public class CSVLogger : ICSVLogger
{
    private readonly ILogger<CSVLogger> _logger;
    private readonly string _csvFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public CSVLogger(ILogger<CSVLogger> logger, IOptions<VMManagerOptions> options)
    {
        _logger = logger;
        var csvFilePath = options.Value.CsvFilePath;
        
        _csvFilePath = Path.IsPathRooted(csvFilePath) 
            ? csvFilePath 
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), csvFilePath);
    }

    public async Task InitializeCSVFileAsync(CancellationToken ct)
    {
        await _fileLock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_csvFilePath))
            {
                _logger.LogInformation("Creating new CSV file: {FilePath}", _csvFilePath);
                
                await using var writer = new StreamWriter(_csvFilePath);
                await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                
                csv.WriteField("Timestamp");
                csv.WriteField("Subscription ID");
                csv.WriteField("Resource Group");
                csv.WriteField("Computer Name");
                csv.WriteField("Power State");
                csv.WriteField("Has Autoshutdown Tag");
                csv.WriteField("Last Start Time");
                csv.WriteField("VM ID");
                csv.WriteField("Location");
                csv.WriteField("VM Size");
                csv.WriteField("Tags");
                await csv.NextRecordAsync();
                await csv.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing CSV file");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task LogVMDataAsync(List<VMData> vmData, CancellationToken ct)
    {
        if (vmData.Count == 0)
        {
            _logger.LogWarning("No VM data to log");
            return;
        }

        await _fileLock.WaitAsync(ct);
        try
        {
            _logger.LogInformation("Logging {Count} VM records to CSV file", vmData.Count);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false
            };

            await using var writer = new StreamWriter(_csvFilePath, append: true);
            await using var csv = new CsvWriter(writer, config);

            foreach (var vm in vmData)
            {
                await WriteVMDataToCSVAsync(csv, vm);
            }

            await csv.FlushAsync();
            _logger.LogInformation("Successfully logged VM data to CSV file");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing VM data to CSV file");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static async Task WriteVMDataToCSVAsync(CsvWriter csv, VMData vm)
    {
        csv.WriteField(vm.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC"));
        csv.WriteField(vm.SubscriptionId);
        csv.WriteField(vm.ResourceGroup);
        csv.WriteField(vm.ComputerName);
        csv.WriteField(vm.PowerState);
        csv.WriteField(vm.HasAutoshutdownTag ? "Yes" : "No");
        csv.WriteField(vm.LastStartTime?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "Unknown");
        csv.WriteField(vm.VMId);
        csv.WriteField(vm.Location);
        csv.WriteField(vm.VMSize);
        
        var tagsJson = System.Text.Json.JsonSerializer.Serialize(vm.Tags);
        csv.WriteField(tagsJson);
        
        await csv.NextRecordAsync();
    }
}
