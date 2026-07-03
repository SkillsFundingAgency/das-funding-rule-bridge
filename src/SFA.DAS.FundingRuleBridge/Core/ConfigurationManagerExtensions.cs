using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using SFA.DAS.Configuration.AzureTableStorage;

namespace SFA.DAS.FundingRuleBridge.Jobs.Core;

[ExcludeFromCodeCoverage]
public static class ConfigurationManagerExtensions
{
    public static void AddAzureTableStorageConfiguration(this ConfigurationManager configuration)
    {
        var environmentName = configuration.GetValue<string>("Values:EnvironmentName") ?? configuration.GetValue<string>("EnvironmentName");
        if (!environmentName!.Equals("DEV", StringComparison.CurrentCultureIgnoreCase))
        {
            configuration.AddAzureTableStorage(options =>
            {
                options.ConfigurationNameIncludesVersionNumber = true;
                options.PreFixConfigurationKeys = false;
#if DEBUG
                options.ConfigurationKeys = configuration.GetValue<string>("Values:ConfigNames")!.Split(",");
                options.StorageConnectionString = configuration.GetValue<string>("Values:ConfigurationStorageConnectionString");
                options.EnvironmentName = configuration.GetValue<string>("Values:EnvironmentName");
#else
                options.ConfigurationKeys = configuration.GetValue<string>("ConfigNames")!.Split(",");
                options.StorageConnectionString = configuration.GetValue<string>("ConfigurationStorageConnectionString");
                options.EnvironmentName = configuration.GetValue<string>("EnvironmentName");
#endif
            });
        }
    }
}