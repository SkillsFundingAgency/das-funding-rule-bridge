using ESFA.DC.ILR.IO.Model.Validation;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public record ValidationSummary(string Uln, ValidationStatus Status, List<ValidationError> ValidationErrors, List<RuleDescriptionLookup> RuleDescriptions);