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

public class DownloadAndParseIlrActivity(IIlrBlobStorageClient blobServiceClient, ILogger<DownloadAndParseIlrActivity> logger)
{
    private static readonly XmlSerializer Serializer = new(typeof(Message), "ESFA/ILR/2025-26");

    [Function(nameof(DownloadAndParseIlrActivity))]
    public async Task<List<LearnerSummary>> Run([ActivityTrigger] IlrFileReference fileRef, FunctionContext context)
    {
        logger.LogInformation("Downloading ILR file '{Filename}' from container '{Container}'.", fileRef.Filename, fileRef.Container);
        var containerClient = blobServiceClient.GetBlobContainerClient(fileRef.Container);
        
        return await FetchLearnersAsync(containerClient, fileRef, context.CancellationToken);
    }

    private async Task<List<LearnerSummary>> FetchLearnersAsync(BlobContainerClient containerClient, IlrFileReference fileRef, CancellationToken cancellationToken = default)
    {
        var blobClient = containerClient.GetBlobClient(fileRef.Filename);
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            logger.LogError("ILR file not found in container: {Container}/{Filename}", fileRef.Container, fileRef.Filename);
            throw new FileNotFoundException($"ILR file not found in container: {fileRef.Container}/{fileRef.Filename}");
        }

        await using var stream = await blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken);
        var message = (Message)Serializer.Deserialize(stream)!;

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

        logger.LogInformation("Parsed {Count} learners from '{Filename}'.", learners.Count, fileRef.Filename);
        return learners;
    }

    private static bool IsValidShortCourse(MessageLearnerLearningDelivery learningDelivery)
    {
        return learningDelivery is { FundModel: FundingModel.NonFunded, ProgType: ProgrammeType.GrowthAndSkillsOfferApprenticeshipUnits };
    }

    private static bool IsValidApprenticeship(MessageLearnerLearningDelivery learningDelivery)
    {
        return learningDelivery is { FundModel: FundingModel.Apprenticeships, ProgType: ProgrammeType.ApprenticeshipStandard };
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