using SFA.DAS.FundingRuleBridge.Jobs.Domain;

namespace SFA.DAS.FundingRuleBridge.Jobs.Data;

public interface IRulesRepository
{
    Task<List<FundingRule>> GetActiveRulesForDatesAsync(List<DateTime> dates, CancellationToken cancellationToken = default);
}
