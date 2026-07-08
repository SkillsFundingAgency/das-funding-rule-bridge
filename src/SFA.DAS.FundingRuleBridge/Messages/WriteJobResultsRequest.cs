using ESFA.DC.ILR.IO.Model.Validation;

namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public class WriteJobResultsRequest
{
    public long JobId { get; set; }
    public required string ContainerName { get; set; }
    public required List<ValidationError> ValidationErrors { get; set; }
}