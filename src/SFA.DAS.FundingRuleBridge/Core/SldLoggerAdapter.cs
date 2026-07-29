using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace SFA.DAS.FundingRuleBridge.Jobs.Core;

public class SldLoggerAdapter(ILoggerFactory loggerFactory) : ESFA.DC.Logging.Interfaces.ILogger
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("SLDQueueing");

    public void LogDebug(string message, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        _logger.LogDebug(message, parameters);
    }

    public void LogError(string message, Exception exception = null, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        _logger.LogError(exception, message, parameters);
    }

    public void LogFatal(string message, Exception exception = null, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        _logger.LogCritical(exception, message, parameters);
    }

    public void LogInfo(string message, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        _logger.LogInformation(message, parameters);
    }

    public void LogVerbose(string message, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        _logger.LogTrace(message, parameters);
    }

    public void LogWarning(string message, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        _logger.LogWarning(message, parameters);
    }

    public void Dispose() {}
}