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
    }

    [Test, MoqAutoData]
    public async Task Then_If_The_Download_Throws_Then_Fail_The_Job(ProcessJobMessage message)
    {
        // arrange
        _context
            .Setup(x => x.GetInput<ProcessJobMessage>())
            .Returns(message);
        
        _context
            .Setup(x => x.CallActivityAsync<List<LearnerSummary>>(nameof(DownloadAndParseIlrActivity), It.IsAny<JobInfo>(), It.IsAny<TaskOptions?>()))
            .ThrowsAsync(new TaskFailedException(nameof(DownloadAndParseIlrActivity), 777, new Exception()));

        // act
        await ProcessJobOrchestrator.RunOrchestrator(_context.Object);

        // assert
        // TODO: this should test specific failure when we know the message format
        _context.Verify(x => x.CallActivityAsync(nameof(SendJobCompleteActivity), It.IsAny<JobCompleteMessage>(), It.IsAny<TaskOptions?>()), Times.Once());
    }
    
    [Test, MoqAutoData]
    public async Task Then_The_Job_Info_Is_Passed_To_DownloadAndParseIlrActivity(ProcessJobMessage message)
    {
        // arrange
        _context
            .Setup(x => x.GetInput<ProcessJobMessage>())
            .Returns(message);
        
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
        capturedJobInfo.JobId.Should().Be(message.JobId);
        capturedJobInfo.Ukprn.Should().Be(message.KeyValuePairs.Ukprn);
        capturedJobInfo.Container.Should().Be(message.KeyValuePairs.Container);
        capturedJobInfo.ValidIlrXmlFilename.Should().Be(message.KeyValuePairs.Filename);
        capturedJobInfo.InvalidLearnerRefsFilename.Should().Be(message.KeyValuePairs.InvalidLearnRefNumbers);
    }

    [Test, MoqAutoData]
    public async Task Then_The_Job_Is_Processed_Successfully(
        ProcessJobMessage message,
        LearnerSummary learnerSummary)
    {
        // arrange
        var validationSummary = new ValidationSummary("Uln", ValidationStatus.Passed, [], []);

        _context
            .Setup(x => x.GetInput<ProcessJobMessage>())
            .Returns(message);
        
        _context
            .Setup(x => x.CallActivityAsync<List<LearnerSummary>>(nameof(DownloadAndParseIlrActivity), It.IsAny<JobInfo>(), It.IsAny<TaskOptions?>()))
            .ReturnsAsync([learnerSummary]);
        
        _context
            .Setup(x => x.CallSubOrchestratorAsync<ValidationSummary>(nameof(ValidateLearnerOrchestrator), It.IsAny<ValidateLearnerMessage>(), It.IsAny<TaskOptions?>()))
            .ReturnsAsync(validationSummary);
        
        // act
        await ProcessJobOrchestrator.RunOrchestrator(_context.Object);

        // assert
        _context.Verify(x => x.CallActivityAsync(nameof(WriteJobsResultsActivity), It.IsAny<WriteJobResultsRequest>(), It.IsAny<TaskOptions?>()), Times.Never());
        // TODO: this should test specific pass when we know the message format
        _context.Verify(x => x.CallActivityAsync(nameof(SendJobCompleteActivity), It.IsAny<JobCompleteMessage>(), It.IsAny<TaskOptions?>()), Times.Once());
    }
}