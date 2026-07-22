using System.Text;
using System.Text.Json;
using Azure.Core.Serialization;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFA.DAS.FundingRuleBridge.Jobs.Endpoints;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using SFA.DAS.FundingRuleBridge.Jobs.Orchestrators;

namespace SFA.DAS.FundingRuleBridge.UnitTests.Endpoints;

public class WhenRunningProcessJobServiceBusTrigger
{
    [Test, MoqAutoData]
    public async Task Then_Exception_Is_Thrown_If_Invalid_Message_Is_Received(
        Mock<FunctionContext> fakeContext,
        [Greedy] ProcessJobEndpoint sut)
    {
        // arrange
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage();

        // act
        var action = async () => await sut.ProcessJobTrigger(message, null!, fakeContext.Object);

        // assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }
    
    [Test, MoqAutoData]
    public async Task Then_A_New_Funding_Rules_Orchestration_Is_Scheduled(
        string instanceId,
        ProcessJobMessage command,
        Mock<DurableTaskClient> durableClient,
        Mock<IOptions<WorkerOptions>> workerOptions,
        Mock<ObjectSerializer> objectSerializer,
        Mock<FunctionContext> functionContext,
        [Greedy] ProcessJobEndpoint sut)
    {
        // arrange
        string? capturedTaskName = null;
        ProcessJobMessage? capturedCommand = null;
        durableClient
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                It.IsAny<object>(),
                It.IsAny<StartOrchestrationOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<TaskName, object?, StartOrchestrationOptions, CancellationToken>((taskName, data, _, _) =>
            {
                capturedTaskName = taskName.Name;
                capturedCommand = data as ProcessJobMessage;
            })
            .ReturnsAsync(instanceId);

        // for serializing the response
        workerOptions
            .Setup(x => x.Value)
            .Returns(new WorkerOptions { Serializer = objectSerializer.Object });
        functionContext
            .Setup(x => x.InstanceServices.GetService(typeof(IOptions<WorkerOptions>)))
            .Returns(workerOptions.Object);
        
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(new BinaryData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command))));
        
        // act
        await sut.ProcessJobTrigger(message, durableClient.Object, functionContext.Object);

        // assert
        capturedTaskName.Should().Be(nameof(ProcessJobOrchestrator));
        capturedCommand.Should().NotBeNull();
        capturedCommand.Should().BeEquivalentTo(command);
    }
}