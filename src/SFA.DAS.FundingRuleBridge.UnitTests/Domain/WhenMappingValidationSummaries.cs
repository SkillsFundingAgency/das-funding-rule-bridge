using Bogus;
using ESFA.DC.ILR.IO.Model.Validation;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;

namespace SFA.DAS.FundingRuleBridge.UnitTests.Domain;

public class WhenMappingValidationSummaries
{
    private Faker<ValidationErrorParameter>? _paramFaker;
    private Faker<ValidationError>? _errorFaker;

    [SetUp]
    public void Setup()
    {
        _paramFaker = new Faker<ValidationErrorParameter>()
            .RuleFor(x => x.PropertyName, f => $"Rule_{f.Random.Int(1, 10)}")
            .RuleFor(x => x.Value, f => f.Random.AlphaNumeric(10));

        _errorFaker = new Faker<ValidationError>()
            .RuleFor(x => x.RuleName, f => $"Rule_{f.Random.Int(1, 10)}")
            .RuleFor(x => x.AimSequenceNumber, f => f.Random.Int(1, 10))
            .RuleFor(x => x.LearnerReferenceNumber, f => f.Random.AlphaNumeric(10))
            .RuleFor(x => x.Severity, _ => "E")
            .RuleFor(x => x.ValidationErrorParameters, _ => _paramFaker.GenerateBetween(1, 5));
    }
    
    [Test, MoqAutoData]
    public void Then_A_System_Error_Returns_Job_Failed()
    {
        // arrange
        const string uln = "Uln";
        List<ValidationSummary> validationSummaries = [
            new(uln, ValidationStatus.SystemError, [], []) 
        ];
        
        // act
        var result = validationSummaries.ToJobSummary();

        // assert
        result.JobFailure.Should().BeTrue();
    }
    
    [Test, MoqAutoData]
    public void Then_The_ValidationErrors_Are_Mapped_Correctly()
    {
        // arrange
        var ulnFaker = new Faker();
        List<string> ulns = [
            ulnFaker.Random.AlphaNumeric(10),
            ulnFaker.Random.AlphaNumeric(10),
            ulnFaker.Random.AlphaNumeric(10) // for passed, we'll ignore this one
        ];

        var errors = _errorFaker!.Generate(8);
        
        List<ValidationSummary> validationSummaries = [
            new(ulns[2], ValidationStatus.Passed, [], []),
            new(ulns[0], ValidationStatus.Failed, errors.Take(4).ToList(), []),
            new(ulns[2], ValidationStatus.Passed, [], []),
            new(ulns[1], ValidationStatus.Failed, errors.Skip(4).ToList(), []),
        ];
        
        // act
        var result = validationSummaries.ToJobSummary();

        // assert
        result.JobFailure.Should().BeFalse();
        result.Items.Should().HaveCount(4);
        result.Items.Should().BeEquivalentTo(validationSummaries);
        result.InvalidLearnerRefs.Should().BeEquivalentTo(ulns.Take(2));
    }
    
    [Test, MoqAutoData]
    public void Then_The_RuleDescriptions_Are_Mapped_Correctly()
    {
        // arrange
        List<RuleDescriptionLookup> descs = [
            new("Rule01", "Message01"),
            new("Rule02", "Message02"),
            new("Rule01", "Message01"),
        ];
        
        const string uln = "Uln";
        List<ValidationSummary> validationSummaries = [
            new(uln, ValidationStatus.Passed, [], descs.Take(2).ToList()),
            new(uln, ValidationStatus.Passed, [], descs.Skip(2).ToList()),
        ];
        
        // act
        var result = validationSummaries.ToJobSummary();

        // assert
        result.RuleDescriptions.Should().HaveCount(2);
        result.RuleDescriptions.Should().BeEquivalentTo([
            new RuleDescriptionLookup("Rule01", "Message01"),
            new RuleDescriptionLookup("Rule02", "Message02"),
        ]);
    }
}