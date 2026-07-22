using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Activities;
using SFA.DAS.FundingRuleBridge.Jobs.Core;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.UnitTests.Activities;

public class WhenSendingValidationRequest
{
    [Test, MoqAutoData]
    public async Task Then_The_Request_Is_Sent(
        Mock<FunctionContext> fakeContext,
        ValidationRequestMessage result)
    {
        // arrange
        var sender = new Mock<ServiceBusSender>();
        var client = new Mock<ServiceBusClient>();

        client
            .Setup(x => x.CreateSender(QueueConstants.ValidationRequestsQueue))
            .Returns(sender.Object);

        ServiceBusMessage? capturedMessage = null;
        sender
            .Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((x, _) => capturedMessage = x)
            .Returns(Task.CompletedTask);
        
        // act
        await new SendValidationRequestActivity(client.Object).Run(result, fakeContext.Object);

        // assert
        capturedMessage.Should().NotBeNull();
        var expectedMessage = JsonSerializer.Deserialize<ValidationRequestMessage>(capturedMessage.Body.ToString());
        expectedMessage.Should().BeEquivalentTo(result);
    }
}