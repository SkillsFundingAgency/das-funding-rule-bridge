using ESFA.DC.ILR.IO.Model.Validation;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;

namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public class WriteJobResultsRequest
{
    public required JobInfo Job { get; set; }
    public List<string> InvalidLearnerRefs { get; set; } = [];
    public required List<ValidationError> ValidationErrors { get; set; }
    public List<RuleDescriptionLookup> RuleDescriptions { get; set; } = [];
}