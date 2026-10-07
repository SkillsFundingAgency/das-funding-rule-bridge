using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SFA.DAS.FundingRuleBridge.Jobs.Domain;

public partial class CourseAgeRuleCheck(ILogger<CourseAgeRuleCheck> logger) : IRuleCheck
{
    public const string RuleName = "CourseAgeCheckActivity";

    public string Name => RuleName;

    public List<RuleCourseOutcome> Check(RuleData ruleData)
    {
        var parameters = JsonSerializer.Deserialize<CourseAgeCheckParameters>(ruleData.Rule.Parameters)!;
        return ruleData.Command.Courses
            .Select(x =>
            {
                if (!CanApplyRule(x.TrainingType, x.StandardCode, ruleData.Rule.CourseIds))
                {
                    LogCourseDoesNotApplyToRule(x.Id, x.AimSequenceNumber);
                    return new RuleCourseOutcome(
                        ruleData.Rule.Id,
                        ruleData.Rule.IlrRuleName,
                        ruleData.Rule.IlrRuleDescription,
                        x.Id,
                        x.AimSequenceNumber,
                        RuleOutcome.Success,
                        []);
                }

                if (parameters.MinimumAge > x.AgeAtStartOfCourse || x.AgeAtStartOfCourse > parameters.MaximumAge)
                {
                    LogCourseCheckFailed(x.Id, x.AimSequenceNumber);
                    return new RuleCourseOutcome(
                        ruleData.Rule.Id,
                        ruleData.Rule.IlrRuleName,
                        ruleData.Rule.IlrRuleDescription,
                        x.Id,
                        x.AimSequenceNumber,
                        RuleOutcome.Error,
                        [new FundingRestriction(nameof(Messages.Course.AgeAtStartOfCourse), x.AgeAtStartOfCourse.ToString())]);
                }

                LogCourseCheckPassed(x.Id, x.AimSequenceNumber);
                return new RuleCourseOutcome(
                    ruleData.Rule.Id,
                    ruleData.Rule.IlrRuleName,
                    ruleData.Rule.IlrRuleDescription,
                    x.Id,
                    x.AimSequenceNumber,
                    RuleOutcome.Success,
                    []);
            })
            .ToList();
    }

    private static bool CanApplyRule(Messages.TrainingType trainingType, int? standardCode, HashSet<string> courseIds)
        => trainingType == Messages.TrainingType.Standard && standardCode is not null && courseIds.Contains($"{standardCode}");

    [LoggerMessage(LogLevel.Information, "CourseAgeRuleCheck failed for course {CourseId}-{AimSequenceNumber}")]
    partial void LogCourseCheckFailed(string courseId, int aimSequenceNumber);

    [LoggerMessage(LogLevel.Information, "CourseAgeRuleCheck passed for course {CourseId}-{AimSequenceNumber}")]
    partial void LogCourseCheckPassed(string courseId, int aimSequenceNumber);

    [LoggerMessage(LogLevel.Information, "CourseAgeRuleCheck does not apply to course {CourseId}-{AimSequenceNumber}")]
    partial void LogCourseDoesNotApplyToRule(string courseId, int aimSequenceNumber);
}
