using ESFA.DC.JobContextManager.Interface;
using ESFA.DC.JobContextManager.Model;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace SFA.DAS.FundingRuleBridge.Jobs.Core
{
    public class SLDMessagingService : BackgroundService
    {
        private readonly IJobContextManager<JobContextMessage> jobContextManager;

        public SLDMessagingService(IJobContextManager<JobContextMessage> jobContextManager) 
        {
            this.jobContextManager = jobContextManager;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            bool initiated = false;
            try
            {
                jobContextManager.OpenAsync(cancellationToken);
                initiated = true;
                await Task.Delay(Timeout.Infinite, cancellationToken);

            }
            catch (Exception ex)
            {
                // Handle exception
            }
            finally
            {
                if (initiated)
                {
                    await jobContextManager.CloseAsync();
                }                        
            }
        }
    }
}
