using System.Text.Json;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;

namespace SFA.DAS.FundingRuleBridge.Jobs.Data.TableStorage;

public static class DomainMappers
{
    extension(FundingRuleTableEntity entity)
    {
        public FundingRule ToDomain()
        {
            var courseIds = JsonSerializer.Deserialize<List<string>>(entity.Courses) ?? [];
            return new FundingRule
            {
                Id = Guid.Parse(entity.RowKey),
                RuleName = entity.RuleName,
                IlrRuleName = entity.IlrRuleName,
                IlrRuleDescription = entity.IlrRuleDescription,
                EffectiveFrom = entity.EffectiveFrom,
                EffectiveTo = entity.EffectiveTo,
                Parameters = entity.Parameters,
                CourseIds = courseIds.ToHashSet()
            };
        }
    }
}
