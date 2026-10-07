using SFA.DAS.FundingRuleBridge.Jobs.Domain;

namespace SFA.DAS.FundingRuleBridge.Jobs.Data;

public class StaticRulesRepository : IRulesRepository
{
    private static readonly IReadOnlyList<FundingRule> Rules =
    [
        new()
        {
            Id = Guid.Parse("6a513839-cb68-428d-b8c4-e5f097a668e0"),
            RuleName = CourseAgeRuleCheck.RuleName,
            IlrRuleName = "AppSerAgeEligibility_01",
            IlrRuleDescription = "Funding Rules Exception- The rule description will go here",
            EffectiveFrom = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = new DateTime(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Parameters = """{"MinimumAge":16,"MaximumAge":24}""",
            CourseIds = ["838"]
        }
    ];

    public Task<List<FundingRule>> GetActiveRulesForDatesAsync(List<DateTime> dates, CancellationToken cancellationToken = default)
    {
        var rules = Rules
            .Where(rule => dates.Any(date => rule.EffectiveFrom <= date && rule.EffectiveTo >= date))
            .ToList();

        return Task.FromResult(rules);
    }
}
