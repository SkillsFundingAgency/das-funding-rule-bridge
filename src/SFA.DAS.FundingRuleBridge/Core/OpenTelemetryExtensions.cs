using System.Diagnostics.CodeAnalysis;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;

namespace SFA.DAS.FundingRuleBridge.Jobs.Core;

[ExcludeFromCodeCoverage]
public static class OpenTelemetryExtensions
{
    public static void AddOpenTelemetryRegistration(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            options.IncludeScopes = true;
        });

        services.AddOpenTelemetry().UseFunctionsWorkerDefaults();
        services.AddOpenTelemetry().UseAzureMonitor(opt => opt.ConnectionString = connectionString);
    }
}