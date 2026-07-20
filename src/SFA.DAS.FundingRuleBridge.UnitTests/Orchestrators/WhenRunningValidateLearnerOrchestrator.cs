using Microsoft.DurableTask;
using Microsoft.Extensions.Logging.Testing;
using SFA.DAS.FundingRuleBridge.Jobs.Activities;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;

namespace SFA.DAS.FundingRuleBridge.UnitTests.Orchestrators;

public class WhenRunningValidateLearnerOrchestrator
{
    private const string InstanceId = "777";
    private Mock<TaskOrchestrationContext> _context;
    private FakeLogger<ValidateLearnerOrchestrator> _fakeLogger;
    
    [SetUp]
    public void Setup()
    {
        _context = new Mock<TaskOrchestrationContext>();
        _fakeLogger = new FakeLogger<ValidateLearnerOrchestrator>();
        _context
            .Setup(x => x.CreateReplaySafeLogger<ValidateLearnerOrchestrator>())
            .Returns(_fakeLogger);
        _context
            .Setup(x => x.InstanceId)
            .Returns(InstanceId);
    }
    
    [Test, MoqAutoData]
    public async Task Then_A_Successful_Validation_Returns_Ok(
        ValidateLearnerMessage message)
    {
        // arrange
        _context
            .Setup(x => x.GetInput<ValidateLearnerMessage>())
            .Returns(message);
        
        _context
            .Setup(x => x.CallActivityAsync(nameof(SendValidationRequestActivity), It.IsAny<List<DateTime>>(), It.IsAny<TaskOptions?>()))
            .Returns(Task.CompletedTask);
        
        _context
            .Setup(x => x.WaitForExternalEvent<ValidateLearnerResult>("ValidationComplete", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateLearnerResult(
                message.CorrelationId,
                InstanceId,
                message.Ukprn,
                message.Uln,
                ValidationStatus.Passed,
                []));

        // act
        var result = await ValidateLearnerOrchestrator.RunOrchestrator(_context.Object);

        // assert
        result.Status.Should().Be(ValidationStatus.Passed);
        result.Uln.Should().Be(message.Uln);
        result.RuleDescriptions.Should().BeEmpty();
        result.ValidationErrors.Should().BeEmpty();
    }
    
    [Test, MoqAutoData]
    public async Task Then_If_Sending_Validation_Request_Throws_The_Result_Is_Failure(
        ValidateLearnerMessage message)
    {
        // arrange
        _context
            .Setup(x => x.GetInput<ValidateLearnerMessage>())
            .Returns(message);

        _context
            .Setup(x => x.CallActivityAsync(nameof(SendValidationRequestActivity), It.IsAny<List<DateTime>>(), It.IsAny<TaskOptions?>()))
            .ThrowsAsync(new TaskFailedException(nameof(SendValidationRequestActivity), 1, new Exception()));

        // act
        var result = await ValidateLearnerOrchestrator.RunOrchestrator(_context.Object);

        // assert
        result.Status.Should().Be(ValidationStatus.SystemError);
        result.Uln.Should().Be(message.Uln);
        result.RuleDescriptions.Should().BeEmpty();
        result.ValidationErrors.Should().BeEmpty();
    }
    
    [Test, MoqAutoData]
    public async Task Then_If_Waiting_For_Event_Timesout_The_Result_Is_Failure(
        string instanceId,
        ValidateLearnerMessage message)
    {
        // arrange
        _context
            .Setup(x => x.GetInput<ValidateLearnerMessage>())
            .Returns(message);

        _context
            .Setup(x => x.CallActivityAsync(nameof(SendValidationRequestActivity), It.IsAny<List<DateTime>>(), It.IsAny<TaskOptions?>()))
            .Returns(Task.CompletedTask);
        
        _context
            .Setup(x => x.WaitForExternalEvent<ValidateLearnerResult>("ValidationComplete", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException());

        // act
        var result = await ValidateLearnerOrchestrator.RunOrchestrator(_context.Object);

        // assert
        result.Status.Should().Be(ValidationStatus.SystemError);
        result.Uln.Should().Be(message.Uln);
        result.RuleDescriptions.Should().BeEmpty();
        result.ValidationErrors.Should().BeEmpty();
    }
    
    [Test, MoqAutoData]
    public async Task Then_Validation_Failures_Are_Returned(
        ValidateLearnerMessage message)
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
        
        var ruleDescriptions = ruleOutcomes
            .Where(x => x.Outcome != RuleOutcome.Success && x.Outcome != RuleOutcome.Warning)
            .Select(x => new RuleDescriptionLookup(x.RuleName, x.RuleDescription)).ToList();
        
        _context
            .Setup(x => x.GetInput<ValidateLearnerMessage>())
            .Returns(message);
        
        _context
            .Setup(x => x.CallActivityAsync(nameof(SendValidationRequestActivity), It.IsAny<List<DateTime>>(), It.IsAny<TaskOptions?>()))
            .Returns(Task.CompletedTask);
        
        _context
            .Setup(x => x.WaitForExternalEvent<ValidateLearnerResult>("ValidationComplete", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateLearnerResult(
                message.CorrelationId,
                InstanceId,
                message.Ukprn,
                message.Uln,
                ValidationStatus.Failed,
                ruleOutcomes));

        // act
        var result = await ValidateLearnerOrchestrator.RunOrchestrator(_context.Object);

        // assert
        result.Status.Should().Be(ValidationStatus.Failed);
        result.Uln.Should().Be(message.Uln);
        result.RuleDescriptions.Should().BeEquivalentTo(ruleDescriptions);
        result.ValidationErrors.Should().HaveCount(2);
    }
}