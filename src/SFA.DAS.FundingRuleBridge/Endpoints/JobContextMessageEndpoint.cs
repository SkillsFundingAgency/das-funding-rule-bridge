using System.Text;
using Azure.Messaging.ServiceBus;
using ESFA.DC.JobContext.Interface;
using ESFA.DC.Serialization.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Core;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;

namespace SFA.DAS.FundingRuleBridge.Jobs.Endpoints;

public class JobContextMessageEndpoint(
    IMessageHandler messageHandler,
    ISerializationService serializationService,
    ILogger<JobContextMessageEndpoint> logger)
{
    [Function(nameof(JobContextMessageEndpoint))]
    public async Task RunAsync(
        [ServiceBusTrigger(QueueConstants.JobContextMessageTopicName, QueueConstants.JobContextMessageSubscriptionName,
            Connection = QueueConstants.InternalServiceBusConnectionString, AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        [DurableClient] DurableTaskClient durableClient,
        ServiceBusMessageActions messageActions,
        FunctionContext executionContext)
    {
        var messageId = message.MessageId ?? "unknown";
        try
        {
            logger.LogInformation("Received JobContextMessage: {MessageId}", messageId);
            var dto  = serializationService.Deserialize<JobContextDto>(Encoding.UTF8.GetString(message.Body));
            var result = await messageHandler.HandleAsync(durableClient, dto, executionContext.CancellationToken);
            if (result.Result)
            {
                await messageActions.CompleteMessageAsync(message, executionContext.CancellationToken);
                logger.LogInformation("JobContextMessage completed: {MessageId}", messageId);
            }
            else
            {
                var dictionary = message.ApplicationProperties?.ToDictionary() ?? [];
                var messageProperties = GetProperties(dictionary, result.Exception);
                await messageActions.AbandonMessageAsync(message, messageProperties, executionContext.CancellationToken);
                logger.LogInformation("JobContextMessage failed: {MessageId}", messageId);
            }
        }
        catch (Exception ex)
        {
            var dictionary = message.ApplicationProperties?.ToDictionary() ?? [];
            var messageProperties = GetProperties(dictionary, ex);
            await messageActions.AbandonMessageAsync(message, messageProperties, executionContext.CancellationToken);
            logger.LogError(ex, "Unhandled exception occured, marked message as failed {MessageId}", messageId);
        }
    }
    
    private static Dictionary<string, object> GetProperties(IDictionary<string, object> applicationProperties, Exception? ex)
    {
        if (ex is null)
        {
            return [];
        }

        object value;
        if (applicationProperties.TryGetValue("Exceptions", out var obj1))
        {
            value = (object)$"{obj1}:{ex.GetType().Name}";
        }
        else
        {
            value = ex.GetType().Name;
        }

        return new Dictionary<string, object> { { "Exceptions", value } };
    }
}