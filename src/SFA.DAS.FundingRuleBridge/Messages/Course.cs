using System.Text.Json.Serialization;

namespace SFA.DAS.FundingRuleBridge.Jobs.Messages;

public record Course
{
    public required string Id { get; set; }
    public required int AimSequenceNumber { get; set; }
    public CourseType Type { get; set; }
    public TrainingType TrainingType { get; set; }
    public int? StandardCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public LearnerCourseStatus Status { get; set; }
    public int AgeAtStartOfCourse { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CourseType
{
    Apprenticeship,   // ProgType 25
    FunctionalSkill,  // TODO: confirm ILR field/value mapping
    ShortCourse,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrainingType
{
    Standard,    // ProgType 25
    ShortCourse  // all other ProgTypes
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LearnerCourseStatus
{
    InLearning,
    Completed,
    Withdrawn,
}
