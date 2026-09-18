using Azure;
using Azure.Data.Tables;

namespace MorningBrief;

public sealed class SentStory : ITableEntity
{
    public string PartitionKey { get; set; } = "sent";
    public string RowKey { get; set; } = "";
    public string Tokens { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}