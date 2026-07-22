using System.Text;
using System.Text.Json;
using Azure.Core.Serialization;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Options;
using SFA.DAS.FundingRuleBridge.Jobs.Endpoints;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.UnitTests.Endpoints;

public class WhenRunningValidateLearnerCallbackServiceBusTrigger
{
    [Test, MoqAutoData]
    public async Task Then_Exception_Is_Thrown_If_Invalid_Message_Is_Received(
        Mock<FunctionContext> fakeContext,
        [Greedy] ValidateLearnerCallbackEndpoint sut)
    {
        // arrange
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage();

        // act
        var action = async () => await sut.ValidateLearnerCallbackTrigger(message, null!, fakeContext.Object);

        // assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }
    
    [Test, MoqAutoData]
    public async Task Then_A_New_Funding_Rules_Orchestration_Is_Scheduled(
        ValidateLearnerResult callback,
        Mock<DurableTaskClient> durableClient,
        Mock<IOptions<WorkerOptions>> workerOptions,
        Mock<ObjectSerializer> objectSerializer,
        Mock<FunctionContext> functionContext,
        [Greedy] ValidateLearnerCallbackEndpoint sut)
    {
        // arrange
        string? capturedInstanceId = null;
        string? capturedEventName = null;
        ValidateLearnerResult? capturedCallback = null;
        durableClient
            .Setup(x => x.RaiseEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, object?, CancellationToken>((instanceId, eventName, eventPayload, _) =>
            {
                capturedInstanceId = instanceId;
                capturedEventName = eventName;
                capturedCallback = eventPayload as ValidateLearnerResult;
            })
            .Returns(Task.CompletedTask);

        // for serializing the response
        workerOptions
            .Setup(x => x.Value)
            .Returns(new WorkerOptions { Serializer = objectSerializer.Object });
        functionContext
            .Setup(x => x.InstanceServices.GetService(typeof(IOptions<WorkerOptions>)))
            .Returns(workerOptions.Object);
        
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(new BinaryData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(callback))));
        
        // act
        await sut.ValidateLearnerCallbackTrigger(message, durableClient.Object, functionContext.Object);

        // assert
        capturedInstanceId.Should().Be(callback.WaitingInstanceId);
        capturedEventName.Should().Be("ValidationComplete");
        capturedCallback.Should().BeEquivalentTo(callback);
    }
}