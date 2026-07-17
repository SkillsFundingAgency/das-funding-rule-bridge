using System.Diagnostics.CodeAnalysis;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using SFA.DAS.FundingRuleBridge.Jobs.Infrastructure;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
                .RegisterDependencies();
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
                (sp, _) => new ServiceBusClient(sp.GetRequiredService<IConfiguration>()[QueueConstants.InternalServiceBusConnectionString], new DefaultAzureCredential()));
            
            services.AddSingleton<IIlrBlobStorageClient>(sp => new IlrBlobStorageClient(sp.GetRequiredService<IConfiguration>()["IlrBlobStorageConnection"]!));
            return builder;
        }
    }
}