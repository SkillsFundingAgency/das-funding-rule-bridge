using ESFA.DC.ILR.IO.Model.Validation;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public record ValidationSummary(int PassedCount, List<ValidationError> ValidationErrors);