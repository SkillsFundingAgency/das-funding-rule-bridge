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
    public async Task Then_If_The_Download_Throws_Then_Fail_The_Job(ProcessJobMessage message, JobInfo jobInfo)
    {
        // arrange
        _context
            .Setup(x => x.GetInput<JobInfo>())
            .Returns(jobInfo);
        
        _context
            .Setup(x => x.CallActivityAsync<List<LearnerSummary>>(nameof(DownloadAndParseIlrActivity), It.IsAny<JobInfo>(), It.IsAny<TaskOptions?>()))
            .ThrowsAsync(new TaskFailedException(nameof(DownloadAndParseIlrActivity), 777, new Exception()));

        // act
        var result = await ProcessJobOrchestrator.RunOrchestrator(_context.Object);

        // assert
        result.Should().BeFalse();
    }
    
    [Test, MoqAutoData]
    public async Task Then_The_Job_Info_Is_Passed_To_DownloadAndParseIlrActivity(JobInfo jobInfo)
    {
        // arrange
        _context
            .Setup(x => x.GetInput<JobInfo>())
            .Returns(jobInfo);
        
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
        capturedJobInfo.Should().BeEquivalentTo(jobInfo);
    }

    [Test, MoqAutoData]
    public async Task Then_The_Job_Is_Processed_Successfully(LearnerSummary learnerSummary, JobInfo jobInfo)
    {
        // arrange
        var validationSummary = new ValidationSummary("Uln", ValidationStatus.Passed, [], []);

        _context
            .Setup(x => x.GetInput<JobInfo>())
            .Returns(jobInfo);
        
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