using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Core;

namespace SFA.DAS.FundingRuleBridge.Jobs.Infrastructure;

public class ServiceBusQueueInitialiser(ServiceBusAdministrationClient adminClient, ILogger<ServiceBusQueueInitialiser> logger) : IHostedService
{
    private static readonly string[] QueuesToCreate =
    [
        QueueConstants.ValidationRequestsQueue,
        QueueConstants.ValidationCallbackQueue
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var options = new CreateQueueOptions(string.Empty)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                DefaultMessageTimeToLive = TimeSpan.FromDays(14),
                DeadLetteringOnMessageExpiration = true,
                MaxDeliveryCount = 5,
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10),
                RequiresDuplicateDetection = false,
                RequiresSession = false
            };

            foreach (var queueName in QueuesToCreate)
            {
                if (await adminClient.QueueExistsAsync(queueName, cancellationToken))
                    continue;

                options.Name = queueName;
                await adminClient.CreateQueueAsync(options, cancellationToken);
                logger.LogInformation("Created Service Bus queue '{QueueName}'", queueName);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating Service Bus queues");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
