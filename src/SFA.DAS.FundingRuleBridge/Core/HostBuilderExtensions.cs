using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;
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
            services.AddSingleton(sp =>
                new ServiceBusClient(sp.GetRequiredService<IConfiguration>()["ServiceBusConnection"]));
            return builder;
        }
    }
}