using Azure.Storage.Blobs.Models;
using ESFA.DC.ILR.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleBridge.Jobs.Infrastructure;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;
using System.Xml.Serialization;
using Azure.Storage.Blobs;
using SFA.DAS.FundingRuleBridge.Jobs.Domain;

namespace SFA.DAS.FundingRuleBridge.Jobs.Activities;

public class DownloadAndParseIlrActivity(IIlrBlobStorageClient blobServiceClient, XmlSerializer xmlSerializer, ILogger<DownloadAndParseIlrActivity> logger)
{
    [Function(nameof(DownloadAndParseIlrActivity))]
    public async Task<List<LearnerSummary>> Run([ActivityTrigger] JobInfo jobInfo, FunctionContext context)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(jobInfo.Container);
        
        return await FetchLearnersAsync(containerClient, jobInfo, context.CancellationToken);
    }

    private async Task<List<LearnerSummary>> FetchLearnersAsync(BlobContainerClient containerClient, JobInfo jobInfo, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Downloading ILR file '{Filename}' from container '{Container}'.", jobInfo.ValidIlrXmlFilename, jobInfo.Container);
        var blobClient = containerClient.GetBlobClient(jobInfo.ValidIlrXmlFilename);
        var exists = await blobClient.ExistsAsync(cancellationToken);
        if (!exists.Value)
        {
            logger.LogError("ILR file not found in container: {Container}/{Filename}", jobInfo.Container, jobInfo.ValidIlrXmlFilename);
            throw new FileNotFoundException($"ILR file not found in container: {jobInfo.Container}/{jobInfo.ValidIlrXmlFilename}");
        }

        await using var stream = await blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken);
        var message = (Message)xmlSerializer.Deserialize(stream)!;

        var learners = (message.Learner ?? [])
            .Where(l => !string.IsNullOrEmpty(l.LearnRefNumber))
            .Select(l =>
            {
                var dob = DateOnly.FromDateTime(l.DateOfBirth);

                var courses = (l.LearningDelivery ?? [])
                    .Where(x => IsValidApprenticeship(x) || IsValidShortCourse(x))
                    .Select(x => BuildCourse(x, dob))
                    .ToList();

                return new LearnerSummary
                {
                    LearnRefNumber = l.LearnRefNumber,
                    DateOfBirth = dob,
                    Courses = courses
                };
            })
            .Where(l => l.Courses is { Count: > 0 })
            .ToList();

        logger.LogInformation("Parsed {Count} learners from '{Filename}'.", learners.Count, jobInfo.ValidIlrXmlFilename);
        return learners;
    }

    private static bool IsValidShortCourse(MessageLearnerLearningDelivery learningDelivery)
    {
        return learningDelivery is { FundModel: FundingModel.NonFunded, ProgType: ProgrammeType.GrowthAndSkillsOfferApprenticeshipUnits };
    }

    private static bool IsValidApprenticeship(MessageLearnerLearningDelivery learningDelivery)
    {
        return learningDelivery is
               {
                   FundModel: FundingModel.Apprenticeships,
                   ProgType: ProgrammeType.ApprenticeshipStandard,
                   AimType: AimTypes.ProgrammeAim
               }
               || (learningDelivery.LearningDeliveryFAM?.Any(x => x.LearnDelFAMType == LearnDelFamTypes.Restart) ?? false);
    }

    private static Course BuildCourse(MessageLearnerLearningDelivery delivery, DateOnly dob)
    {
        var startDate = delivery.LearnStartDate;
        var plannedEndDate = delivery.LearnPlanEndDate;
        var progType = delivery.ProgTypeSpecified ? delivery.ProgType : (int?)null;

        return new Course
        {
            Id = delivery.LearnAimRef,
            AimSequenceNumber = delivery.AimSeqNumber,
            Type = progType == 25 ? CourseType.Apprenticeship : CourseType.ShortCourse, // TODO: add FunctionalSkill mapping once ILR field/value is confirmed
            TrainingType = progType == 25 ? TrainingType.Standard : TrainingType.ShortCourse,
            StandardCode = delivery.StdCodeSpecified ? delivery.StdCode : null,
            StartDate = startDate,
            PlannedEndDate = plannedEndDate,
            EndDate = delivery.LearnActEndDateSpecified ? delivery.LearnActEndDate : plannedEndDate,
            Status = delivery.CompStatus switch
            {
                1 => LearnerCourseStatus.InLearning,
                2 => LearnerCourseStatus.Completed,
                3 => LearnerCourseStatus.Withdrawn,
                _ => LearnerCourseStatus.InLearning
            },
            AgeAtStartOfCourse = CalculateAge(dob, startDate)
        };
    }

    private static int CalculateAge(DateOnly dob, DateTime startDate)
    {
        var age = startDate.Year - dob.Year;
        if (startDate < dob.ToDateTime(TimeOnly.MinValue).AddYears(age))
            age--;
        return age;
    }
}