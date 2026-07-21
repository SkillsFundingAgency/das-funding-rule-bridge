using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace SFA.DAS.FundingRuleBridge.Jobs.Infrastructure
{
    public class SLDLoggerAdapter : ESFA.DC.Logging.Interfaces.ILogger
    {
        private ILogger logger;
        public SLDLoggerAdapter(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger("SLDQueueing");
        }

        public void Dispose()
        {
            
        }

        public void LogDebug(string message, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            logger.LogDebug(message, parameters);
        }

        public void LogError(string message, Exception exception = null, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            logger.LogError(exception, message, parameters);
        }

        public void LogFatal(string message, Exception exception = null, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            logger.LogCritical(exception, message, parameters);
        }

        public void LogInfo(string message, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            logger.LogInformation(message, parameters);
        }

        public void LogVerbose(string message, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            logger.LogTrace(message, parameters);
        }

        public void LogWarning(string message, object[] parameters = null, long jobIdOverride = -1, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            logger.LogWarning(message, parameters);
        }
    }
}
