using Azure;
using Azure.Data.Tables;

namespace SFA.DAS.FundingRuleBridge.Jobs.Data.TableStorage;

public class FundingRuleTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;
    public string RuleName { get; set; } = default!;
    public string IlrRuleName { get; set; } = default!;
    public string IlrRuleDescription { get; set; } = default!;
    public bool Enabled { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    public string Parameters { get; set; } = default!;
    public string Courses { get; set; } = default!;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
