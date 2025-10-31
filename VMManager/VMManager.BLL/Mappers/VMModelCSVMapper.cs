using CsvHelper.Configuration;
using VMManager.BLL.Constants;
using VMManager.BLL.Models;

namespace VMManager.BLL.Mappers;

public sealed class VMModelCSVMapper : ClassMap<VMModel>
{
    public VMModelCSVMapper()
    {
        Map(m => m.Timestamp).Name(CSVConstants.TimestampHeader);
        Map(m => m.SubscriptionId).Name(CSVConstants.SubscriptionIdHeader);
        Map(m => m.ResourceGroup).Name(CSVConstants.ResourceGroupHeader);
        Map(m => m.ComputerName).Name(CSVConstants.ComputerNameHeader);
        Map(m => m.PowerState).Name(CSVConstants.PowerStateHeader);
        Map(m => m.HasAutoshutdownTag).Name(CSVConstants.HasAutoshutdownTagHeader);
        Map(m => m.LastStartTime).Name(CSVConstants.LastStartTimeHeader);
        Map(m => m.VMId).Name(CSVConstants.VMIdHeader);
        Map(m => m.Location).Name(CSVConstants.LocationHeader);
        Map(m => m.VMSize).Name(CSVConstants.VMSizeHeader);
        Map(m => m.Tags).Name(CSVConstants.TagsHeader);
    }
}

