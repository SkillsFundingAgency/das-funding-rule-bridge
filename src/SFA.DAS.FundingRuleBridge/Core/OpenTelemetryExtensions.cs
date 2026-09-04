using System.Diagnostics.CodeAnalysis;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;

namespace SFA.DAS.FundingRuleBridge.Jobs.Core;

[ExcludeFromCodeCoverage]
public static class OpenTelemetryExtensions
{
    public static void AddOpenTelemetryRegistration(this FunctionsApplicationBuilder builder, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        builder.Logging.AddOpenTelemetry(opt => opt.IncludeScopes = true);
        builder
            .Services
            .AddOpenTelemetry()
            .UseFunctionsWorkerDefaults()
            .UseAzureMonitorExporter(opt => opt.ConnectionString = connectionString);
    }
}