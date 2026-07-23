using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using ESFA.DC.Auditing.Interface;
using ESFA.DC.ILR.Model;
using ESFA.DC.JobContext.Interface;
using ESFA.DC.JobContextManager;
using ESFA.DC.JobContextManager.Interface;
using ESFA.DC.JobContextManager.Model;
using ESFA.DC.JobStatus.Interface;
using ESFA.DC.Queueing;
using ESFA.DC.Queueing.Interface;
using ESFA.DC.Queueing.Interface.Configuration;
using ESFA.DC.Serialization.Interfaces;
using ESFA.DC.Serialization.Json;
using ESFA.DC.Serialization.Xml;
using SFA.DAS.FundingRuleBridge.Jobs.Infrastructure;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;
using SFA.DAS.FundingRuleBridge.Jobs.Handlers;
using SFA.DAS.FundingRuleBridge.Jobs.Services;

namespace SFA.DAS.FundingRuleBridge.Jobs.Core;

[ExcludeFromCodeCoverage]
public static class HostBuilderExtensions
{
    extension(FunctionsApplicationBuilder builder)
    {
        public FunctionsApplicationBuilder ConfigureFundingRuleBridgeApp()
        {
            return builder
                .ConfigureFunctionsWebApplication()
                .RegisterConfiguration()
                .RegisterServices()
                .RegisterDependencies()
                .RegisterSldQueueingDependencies();
        }

        private FunctionsApplicationBuilder RegisterConfiguration()
        {
            builder.Configuration
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddEnvironmentVariables()
                .AddJsonFile("local.settings.json", optional: true);

            builder.Configuration.AddAzureTableStorageConfiguration();

            return builder;
        }

        private FunctionsApplicationBuilder RegisterServices()
        {
            builder.Services.AddOpenTelemetryRegistration(builder.Configuration.GetValue<string>("APPLICATIONINSIGHTS_CONNECTION_STRING"));
            return builder;
        }

        private FunctionsApplicationBuilder RegisterDependencies()
        {
            var services = builder.Services;
            
            services.AddKeyedSingleton(
                typeof(ServiceBusClient),
                QueueConstants.InternalBusKey,
                (sp, _) => new ServiceBusClient(sp.GetRequiredService<IConfiguration>()[QueueConstants.InternalServiceBusConnectionString], new DefaultAzureCredential()));
            
            services.AddSingleton<IIlrBlobStorageClient>(sp => new IlrBlobStorageClient(sp.GetRequiredService<IConfiguration>()["IlrBlobStorageConnection"]!));
            services.AddSingleton<XmlSerializer>(_ => new XmlSerializer(typeof(Message), "ESFA/ILR/2025-26"));
            return builder;
        }
        
        private FunctionsApplicationBuilder RegisterSldQueueingDependencies()
        {
            var sldConfig = builder.Configuration.GetSection("SLDTopic");
            var t = TimeSpan.Parse(sldConfig["MaxCallbackTimeSpan"] ?? "01:00:00");

            var sldTopicConfig = new TopicConfiguration(
                builder.Configuration[QueueConstants.ExternalServiceBusConnectionString],
                sldConfig["TopicName"],
                sldConfig["SubscriptionName"] ?? QueueConstants.IncomingJobQueue,
                int.Parse(sldConfig["MaxConcurrentCalls"] ?? "1"),
                maximumCallbackTimeSpan: t);

            var jobStatusQueueConfig = new QueueConfiguration(
                sldConfig["ServiceBusConnection"],
                sldConfig["JobStatusQueueName"],
                int.Parse(sldConfig["JobStatusMaxConcurrentCalls"] ?? "1"));

            var auditQueueConfig = new QueueConfiguration(
                sldConfig["ServiceBusConnection"],
                sldConfig["AuditQueueName"],
                int.Parse(sldConfig["AuditMaxConcurrentCalls"] ?? "1"));

            builder.Services.AddTransient<ESFA.DC.Logging.Interfaces.ILogger, SldLoggerAdapter>();
            builder.Services.AddTransient<IJsonSerializationService, JsonSerializationService>();
            builder.Services.AddTransient<ISerializationService, JsonSerializationService>();
            builder.Services.AddTransient<IXmlSerializationService, XmlSerializationService>();
            
            builder.Services.AddTransient<ITopicPublishService<JobContextDto>>(sp =>
            {
                var serializer = sp.GetRequiredService<IJsonSerializationService>();
                return new TopicPublishService<JobContextDto>(sldTopicConfig, serializer);
            });
            builder.Services.AddTransient<IMapper<JobContextMessage, JobContextMessage>>(_ => new DefaultJobContextMessageMapper<JobContextMessage>());
            builder.Services.AddTransient<IQueuePublishService<JobStatusDto>>(sp =>
            {
                var serializer = sp.GetRequiredService<IJsonSerializationService>();
                return new QueuePublishService<JobStatusDto>(jobStatusQueueConfig, serializer);
            });
            builder.Services.AddTransient<IQueuePublishService<AuditingDto>>(sp =>
            {
                var serializer = sp.GetRequiredService<IJsonSerializationService>();
                return new QueuePublishService<AuditingDto>(auditQueueConfig, serializer);
            });
            builder.Services.AddTransient<IMessageHandler<JobContextMessage>, JobContextMessageHandler>();
            //builder.Services.AddHostedService<SldMessagingService>();
            //builder.Services.AddSingleton<JobContextManager<JobContextMessage>>();
            builder.Services.AddTransient<IMessageHandler, CustomJobContextManager>();
            return builder;
        }
    }
}