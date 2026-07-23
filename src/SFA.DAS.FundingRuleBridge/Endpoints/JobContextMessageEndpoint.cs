using System.Text;
using Azure.Messaging.ServiceBus;
using ESFA.DC.JobContext.Interface;
using ESFA.DC.Serialization.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using SFA.DAS.FundingRuleBridge.Jobs.Core;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;

namespace SFA.DAS.FundingRuleBridge.Jobs.Endpoints;

public class JobContextMessageEndpoint(
    IMessageHandler handler,
    ISerializationService serializationService)
{
    [Function(nameof(JobContextMessageEndpoint))]
    public async Task ValidateLearnerCallbackTrigger(
        [ServiceBusTrigger(QueueConstants.JobContextMessageTopicName, QueueConstants.JobContextMessageSubscriptionName,
            Connection = QueueConstants.InternalServiceBusConnectionString, AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        [DurableClient] DurableTaskClient durableClient,
        ServiceBusMessageActions messageActions,
        FunctionContext executionContext)
    {
        try
        {
            var dto  = serializationService.Deserialize<JobContextDto>(Encoding.UTF8.GetString(message.Body));
            var result = await handler.HandleAsync(dto, executionContext.CancellationToken);
            if (result.Result)
            {
                await messageActions.CompleteMessageAsync(message, executionContext.CancellationToken);
            }
            else
            {
                var dictionary = message.ApplicationProperties?.ToDictionary() ?? [];
                var messageProperties = GetProperties(dictionary, result.Exception);
                await messageActions.AbandonMessageAsync(message, messageProperties, executionContext.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            var dictionary = message.ApplicationProperties?.ToDictionary() ?? [];
            var messageProperties = GetProperties(dictionary, ex);
            await messageActions.AbandonMessageAsync(message, messageProperties, executionContext.CancellationToken);
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