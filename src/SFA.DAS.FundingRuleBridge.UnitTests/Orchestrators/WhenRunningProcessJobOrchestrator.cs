using Bogus;
using ESFA.DC.JobContext.Interface;
using ESFA.DC.JobContextManager.Model;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging.Testing;
using SFA.DAS.FundingRuleBridge.Jobs.Activities;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;

namespace SFA.DAS.FundingRuleBridge.UnitTests.Orchestrators;

public class WhenRunningProcessJobOrchestrator
{
    private const string InstanceId = "777";
    private Mock<TaskOrchestrationContext> _context;
    private FakeLogger<ProcessJobOrchestrator> _fakeLogger;
    private Faker<JobContextMessage> _messageFaker;

    [SetUp]
    public void Setup()
    {
        _context = new Mock<TaskOrchestrationContext>();
        _fakeLogger = new FakeLogger<ProcessJobOrchestrator>();
        _context
            .Setup(x => x.CreateReplaySafeLogger<ProcessJobOrchestrator>())
            .Returns(_fakeLogger);
        _context
            .Setup(x => x.InstanceId)
            .Returns(InstanceId);

        _messageFaker = new Faker<JobContextMessage>()
            .RuleFor(x => x.JobId, f => f.Random.Long(1, 1000000000))
            .RuleFor(x => x.KeyValuePairs, f => new Dictionary<string, object>()
            {
                [JobContextMessageKey.Container] = $"Container_{f.Random.AlphaNumeric(10)}",
                [JobContextMessageKey.UkPrn] = $"Ukprn_{f.Random.AlphaNumeric(10)}",
                [JobContextMessageKey.Filename] = $"Filename_{f.Random.AlphaNumeric(10)}",
            });
    }

    [Test, MoqAutoData]
    public async Task Then_If_The_Download_Throws_Then_Fail_The_Job(ProcessJobMessage message)
    {
        // arrange
        var jobContextMessage = _messageFaker.Generate(1)[0];
        _context
            .Setup(x => x.GetInput<JobContextMessage>())
            .Returns(jobContextMessage);
        
        _context
            .Setup(x => x.CallActivityAsync<List<LearnerSummary>>(nameof(DownloadAndParseIlrActivity), It.IsAny<JobInfo>(), It.IsAny<TaskOptions?>()))
            .ThrowsAsync(new TaskFailedException(nameof(DownloadAndParseIlrActivity), 777, new Exception()));

        // act
        var result = await ProcessJobOrchestrator.RunOrchestrator(_context.Object);

        // assert
        result.Should().BeFalse();
    }
    
    [Test, MoqAutoData]
    public async Task Then_The_Job_Info_Is_Passed_To_DownloadAndParseIlrActivity()
    {
        // arrange
        var jobContextMessage = _messageFaker.Generate(1)[0];
        _context
            .Setup(x => x.GetInput<JobContextMessage>())
            .Returns(jobContextMessage);
        
        JobInfo? capturedJobInfo = null;
        _context
            .Setup(x => x.CallActivityAsync<List<LearnerSummary>>(nameof(DownloadAndParseIlrActivity), It.IsAny<JobInfo>(), It.IsAny<TaskOptions?>()))
            .Callback<TaskName, object?, TaskOptions?>((_, jobInfo, __) =>
            {
                capturedJobInfo = jobInfo as JobInfo;
            })
            .ThrowsAsync(new TaskFailedException(nameof(DownloadAndParseIlrActivity), 777, new Exception()));

        // act
        await ProcessJobOrchestrator.RunOrchestrator(_context.Object);

        // assert
        capturedJobInfo.Should().NotBeNull();
        capturedJobInfo.JobId.Should().Be(jobContextMessage.JobId);
        capturedJobInfo.Ukprn.Should().Be(jobContextMessage.KeyValuePairs[JobContextMessageKey.UkPrn] as string);
        capturedJobInfo.Container.Should().Be(jobContextMessage.KeyValuePairs[JobContextMessageKey.Container] as string);
        capturedJobInfo.ValidIlrXmlFilename.Should().Be(jobContextMessage.KeyValuePairs[JobContextMessageKey.Filename] as string);
    }

    [Test, MoqAutoData]
    public async Task Then_The_Job_Is_Processed_Successfully(LearnerSummary learnerSummary)
    {
        // arrange
        var validationSummary = new ValidationSummary("Uln", ValidationStatus.Passed, [], []);

        var jobContextMessage = _messageFaker.Generate(1)[0];
        _context
            .Setup(x => x.GetInput<JobContextMessage>())
            .Returns(jobContextMessage);
        
        _context
            .Setup(x => x.CallActivityAsync<List<LearnerSummary>>(nameof(DownloadAndParseIlrActivity), It.IsAny<JobInfo>(), It.IsAny<TaskOptions?>()))
            .ReturnsAsync([learnerSummary]);
        
        _context
            .Setup(x => x.CallSubOrchestratorAsync<ValidationSummary>(nameof(ValidateLearnerOrchestrator), It.IsAny<ValidateLearnerMessage>(), It.IsAny<TaskOptions?>()))
            .ReturnsAsync(validationSummary);
        
        // act
        var result = await ProcessJobOrchestrator.RunOrchestrator(_context.Object);

        // assert
        result.Should().BeTrue();
        _context.Verify(x => x.CallActivityAsync(nameof(WriteJobsResultsActivity), It.IsAny<WriteJobResultsRequest>(), It.IsAny<TaskOptions?>()), Times.Never());
    }
}