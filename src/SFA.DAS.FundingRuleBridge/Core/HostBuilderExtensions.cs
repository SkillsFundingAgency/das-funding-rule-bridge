using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using ESFA.DC.Auditing.Interface;
using ESFA.DC.JobContext.Interface;
using ESFA.DC.JobContextManager;
using ESFA.DC.JobContextManager.Interface;
using ESFA.DC.JobContextManager.Model;
using ESFA.DC.JobStatus.Interface;
using ESFA.DC.Queueing;
using ESFA.DC.Queueing.Interface.Configuration;
using ESFA.DC.Serialization.Interfaces;
using ESFA.DC.Serialization.Xml;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SFA.DAS.FundingRuleBridge.Jobs.Handlers;
using SFA.DAS.FundingRuleBridge.Jobs.Infrastructure;
using System.Diagnostics.CodeAnalysis;

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
                .RegisterSLDQueueingDependencies();
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
                QueueConstants.ExternalBusKey,
                (sp, _) => new ServiceBusClient(sp.GetRequiredService<IConfiguration>()[QueueConstants.ExternalServiceBusConnectionString]));
            
            services.AddKeyedSingleton(
                typeof(ServiceBusClient),
                QueueConstants.InternalBusKey,
                (sp, _) => new ServiceBusClient(sp.GetRequiredService<IConfiguration>()[QueueConstants.InternalServiceBusConnectionString]));
            
            services.AddSingleton<IIlrBlobStorageClient>(sp => new IlrBlobStorageClient(sp.GetRequiredService<IConfiguration>()["IlrBlobStorageConnection"]!));
            return builder;
        }

        private FunctionsApplicationBuilder RegisterJobContextDependencies()
        {
            return builder;
        }

        private FunctionsApplicationBuilder RegisterSLDQueueingDependencies()
        {
            var sldConfig = builder.Configuration.GetSection("SLDTopic");

            var t = TimeSpan.Parse(sldConfig["MaxCallbackTimeSpan"] ?? "01:00:00");

            var sldTopicConfig = new TopicConfiguration(
                builder.Configuration["IncomingServiceBusConnection"],
                sldConfig["TopicName"],
                sldConfig["SubscriptionName"] ?? "ASFundingValidation",
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

            builder.Services.AddTransient<ESFA.DC.Logging.Interfaces.ILogger, SLDLoggerAdapter>();
            builder.Services.AddTransient<IJsonSerializationService, ESFA.DC.Serialization.Json.JsonSerializationService>();
            builder.Services.AddTransient<ISerializationService, ESFA.DC.Serialization.Json.JsonSerializationService>();
            builder.Services.AddTransient<IXmlSerializationService, XmlSerializationService>();
            builder.Services.AddSingleton<JobContextManager<JobContextMessage>>();
            builder.Services.AddTransient<ESFA.DC.Queueing.Interface.ITopicSubscriptionService<JobContextDto>>(
                sp => new TopicSubscriptionSevice<JobContextDto>(sldTopicConfig, sp.GetRequiredService<IJsonSerializationService>(), sp.GetRequiredService<ESFA.DC.Logging.Interfaces.ILogger>()));
            builder.Services.AddTransient<ESFA.DC.Queueing.Interface.ITopicPublishService<JobContextDto>>(
                sp => new TopicPublishService<JobContextDto>(sldTopicConfig, sp.GetRequiredService<IJsonSerializationService>()));
            builder.Services.AddTransient<ESFA.DC.JobContextManager.Interface.IMapper<JobContextMessage, JobContextMessage>>(
                sp => new DefaultJobContextMessageMapper<JobContextMessage>());
            builder.Services.AddTransient<ESFA.DC.Queueing.Interface.IQueuePublishService<JobStatusDto>>(sp => new QueuePublishService<JobStatusDto>(jobStatusQueueConfig, sp.GetRequiredService<IJsonSerializationService>()));
            builder.Services.AddTransient<ESFA.DC.Queueing.Interface.IQueuePublishService<AuditingDto>>(sp => new QueuePublishService<AuditingDto>(auditQueueConfig, sp.GetRequiredService<IJsonSerializationService>()));
            builder.Services.AddTransient<IMessageHandler<JobContextMessage>, JobContextMessageHandler>();
                


            builder.Services.AddSingleton(jobStatusQueueConfig);
            builder.Services.AddSingleton(auditQueueConfig);
            builder.Services.AddSingleton(sldTopicConfig);
            return builder;
        }


        private ITopicConfiguration BuildSLDTopicConfiguration(IConfigurationSection sldConfig)
        {        

            var t = TimeSpan.Parse(sldConfig["MaxCallbackTimeSpan"] ?? "01:00:00");

            return new TopicConfiguration(
                builder.Configuration["IncomingServiceBusConnection"],
                sldConfig["TopicName"],
                sldConfig["SubscriptionName"] ?? "ASFundingValidation",
                int.Parse(sldConfig["MaxConcurrentCalls"] ?? "1"),
                maximumCallbackTimeSpan: t);
        }

        private IQueueConfiguration BuildJobStatusQueueConfiguration(IConfigurationSection sldConfig)
        {
            return new QueueConfiguration(
                sldConfig["ServiceBusConnection"],
                sldConfig["JobStatusQueueName"],
                int.Parse(sldConfig["JobStatusMaxConcurrentCalls"] ?? "1"));
        }

        private IQueueConfiguration BuildAuditQueueConfiguration(IConfigurationSection sldConfig)
        {
            return new QueueConfiguration(
                sldConfig["ServiceBusConnection"],
                sldConfig["AuditQueueName"],
                int.Parse(sldConfig["AuditMaxConcurrentCalls"] ?? "1"));
        }
    }
}