using Azure.Data.Tables;
using SFA.DAS.FundingRuleBridge.Jobs.Core;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;

namespace SFA.DAS.FundingRuleBridge.Jobs.Data.TableStorage;

public class TableStorageRulesRepository(TableServiceClient tableServiceClient) : IRulesRepository
{
    public async Task<List<FundingRule>> GetActiveRulesForDatesAsync(List<DateTime> dates, CancellationToken cancellationToken = default)
    {
        var rules = await FetchRulesAsync(dates, cancellationToken);
        return rules.Select(x => x.ToDomain()).ToList();
    }

    private async Task<List<FundingRuleTableEntity>> FetchRulesAsync(List<DateTime> dates, CancellationToken cancellationToken)
    {
        var ruleClient = tableServiceClient.GetTableClient(GlobalConstants.FundingRulesTableName);

        var filters = dates.Select(date => $"(EffectiveFrom le datetime'{date:o}' and EffectiveTo ge datetime'{date:o}')");
        var filter = $"({string.Join(" or ", filters)}) and Enabled eq true";

        var rulesPages = ruleClient.QueryAsync<FundingRuleTableEntity>(filter, cancellationToken: cancellationToken);
        var rules = new List<FundingRuleTableEntity>();
        await foreach (var page in rulesPages.AsPages().WithCancellation(cancellationToken))
        {
            rules.AddRange(page.Values);
        }

        return rules;
    }
}
