using System.Text.Json.Serialization;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationStatus
{
    Passed,
    Failed
}