using ESFA.DC.ILR.Model.Loose;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public static class ValidationErrorParameterPropertyNameMapper
{
    public static string Map(string propertyName)
    {
        return propertyName switch
        {
            nameof(Course.AgeAtStartOfCourse) => nameof(MessageLearner.DateOfBirth),
            _ => "Unknown"
        };
    }
}