using ESFA.DC.ILR.Model;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.UnitTests.Domain;

public class WhenMappingRestrictionPropertyNames
{
    [TestCase(nameof(Course.AgeAtStartOfCourse), nameof(MessageLearner.DateOfBirth))]
    [TestCase("Unknown name", "Unknown name")]
    public void The_Property_Name_Is_Mapped(string sourceName, string expectedName)
    {
        // act
        var result = RestrictionPropertyNameMapper.Map(sourceName);

        // assert
        result.Should().Be(expectedName);
    }
}