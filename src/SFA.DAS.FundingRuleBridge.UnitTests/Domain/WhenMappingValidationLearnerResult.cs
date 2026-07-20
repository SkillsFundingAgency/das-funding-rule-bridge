using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.UnitTests.Domain;

public class WhenMappingValidationLearnerResult
{
    [Test, MoqAutoData]
    public void Then_The_ValidationSummary_Contains_The_Failed_RuleDescriptions()
    {
        // arrange
        List<RuleCourseOutcome> ruleOutcomes = [
            new(Guid.NewGuid(), "Rule 1", "Rule 1 Description", "CourseId1", 1, RuleOutcome.Error, [
                new FundingRestriction("AgeAtStartOfCourse", "24")
            ]),
            new(Guid.NewGuid(), "Rule 2", "Rule 2 Description", "CourseId2", 1, RuleOutcome.Success, []),
            new(Guid.NewGuid(), "Rule 3", "Rule 3 Description", "CourseId3", 1, RuleOutcome.Error, [
                new FundingRestriction("RestrictionName1", "RestrictedValue1"),
                new FundingRestriction("RestrictionName2", "RestrictedValue2"),
            ])
        ];

        var validationResult = new ValidateLearnerResult("CorrelationId", "WaitingInstanceId", "Ukprn", "Uln", ValidationStatus.Failed, ruleOutcomes);

        // act
        var result = validationResult.ToValidationSummary("Uln");

        // assert
        result.RuleDescriptions.Should().HaveCount(2);
        result.RuleDescriptions[0].RuleName.Should().Be("Rule 1");
        result.RuleDescriptions[1].RuleName.Should().Be("Rule 3");
        result.RuleDescriptions[0].Message.Should().Be("Rule 1 Description");
        result.RuleDescriptions[1].Message.Should().Be("Rule 3 Description");
    }
    
    [Test, MoqAutoData]
    public void Then_The_ValidationSummary_Contains_The_Failed_Rules()
    {
        // arrange
        List<RuleCourseOutcome> ruleOutcomes = [
            new(Guid.NewGuid(), "Rule 1", "Rule 1 Description", "CourseId1", 1, RuleOutcome.Error, [
                new FundingRestriction("AgeAtStartOfCourse", "24")
            ]),
            new(Guid.NewGuid(), "Rule 2", "Rule 2 Description", "CourseId2", 1, RuleOutcome.Success, []),
            new(Guid.NewGuid(), "Rule 3", "Rule 3 Description", "CourseId3", 1, RuleOutcome.Error, [
                new FundingRestriction("RestrictionName1", "RestrictedValue1"),
                new FundingRestriction("RestrictionName2", "RestrictedValue2"),
            ])
        ];

        var validationResult = new ValidateLearnerResult("CorrelationId", "WaitingInstanceId", "Ukprn", "Uln", ValidationStatus.Failed, ruleOutcomes);

        // act
        var result = validationResult.ToValidationSummary("Uln");

        // assert
        result.Status.Should().Be(ValidationStatus.Failed);
        result.Uln.Should().Be("Uln");
        result.ValidationErrors.Should().HaveCount(2);
        result.ValidationErrors.Should().AllSatisfy(x => {
            x.LearnerReferenceNumber.Should().Be("Uln");
            x.AimSequenceNumber.Should().Be(1);
            x.RuleName.Should().BeOneOf("Rule 1", "Rule 3");
            x.Severity.Should().BeOneOf("E", "W");
        });
    }
}