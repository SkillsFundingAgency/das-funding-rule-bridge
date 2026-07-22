using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using SFA.DAS.FundingRuleBridge.Jobs.Core;

FunctionsApplication
    .CreateBuilder(args)
    .ConfigureFundingRuleBridgeApp()
    .Build()
    .Run();

[ExcludeFromCodeCoverage]
public partial class Program;