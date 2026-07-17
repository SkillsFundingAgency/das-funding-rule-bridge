using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SFA.DAS.FundingRuleBridge.Jobs.Activities;
using SFA.DAS.FundingRuleBridge.Jobs.Infrastructure;
using SFA.DAS.FundingRuleBridge.Jobs.Messages;

namespace SFA.DAS.FundingRuleBridge.UnitTests.Activities;

[TestFixture]
public class DownloadAndParseIlrActivityTests
{
    private const string Container = "ilr2526-files";
    private const string Filename = "10034309/sample.xml";

    private Mock<IIlrBlobStorageClient> _blobServiceClient = null!;
    private Mock<BlobContainerClient> _containerClient = null!;
    private Mock<BlobClient> _blobClient = null!;

    [SetUp]
    public void SetUp()
    {
        _blobServiceClient = new Mock<IIlrBlobStorageClient>();
        _containerClient = new Mock<BlobContainerClient>();
        _blobClient = new Mock<BlobClient>();

        _blobServiceClient
            .Setup(s => s.GetBlobContainerClient(Container))
            .Returns(_containerClient.Object);

        _containerClient
            .Setup(c => c.GetBlobClient(Filename))
            .Returns(_blobClient.Object);
    }

    [Test]
    public async Task Run_ReturnsAllLearners()
    {
        using var stream = LoadTestXml("SFA.DAS.FundingRuleBridge.UnitTests.TestData.sample-ilr.xml");
        _blobClient
            .Setup(b => b.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), default))
            .ReturnsAsync(stream);

        var sut = new DownloadAndParseIlrActivity(_blobServiceClient.Object, NullLogger<DownloadAndParseIlrActivity>.Instance);

        var result = await sut.Run(new IlrFileReference { Container = Container, Filename = Filename }, Mock.Of<FunctionContext>());

        result.Should().HaveCount(9);
        result.Select(l => l.LearnRefNumber).Should().BeEquivalentTo([
            "9000004402", "9000009803", "9000567903", "9000568004",
            "9001019004", "9001019101", "9001019209", "9001019306", "DEVCONTRA1"
        ]);
    }

    [Test]
    public async Task Run_ParsesDateOfBirthCorrectly()
    {
        using var stream = LoadTestXml("SFA.DAS.FundingRuleBridge.UnitTests.TestData.sample-ilr.xml");
        _blobClient
            .Setup(b => b.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), default))
            .ReturnsAsync(stream);

        var sut = new DownloadAndParseIlrActivity(_blobServiceClient.Object, NullLogger<DownloadAndParseIlrActivity>.Instance);

        var result = await sut.Run(new IlrFileReference { Container = Container, Filename = Filename }, Mock.Of<FunctionContext>());

        result.First(l => l.LearnRefNumber == "9000004402").DateOfBirth
            .Should().Be(new DateOnly(2000, 1, 2));
    }

    [Test]
    public async Task Run_ParsesCoursesForEachLearner()
    {
        using var stream = LoadTestXml("SFA.DAS.FundingRuleBridge.UnitTests.TestData.sample-ilr.xml");
        _blobClient
            .Setup(b => b.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), default))
            .ReturnsAsync(stream);

        var sut = new DownloadAndParseIlrActivity(_blobServiceClient.Object, NullLogger<DownloadAndParseIlrActivity>.Instance);

        var result = await sut.Run(new IlrFileReference { Container = Container, Filename = Filename }, Mock.Of<FunctionContext>());

        var firstLearner = result.First(l => l.LearnRefNumber == "9000004402");
        firstLearner.Courses.Should().HaveCount(3);

        var firstCourse = firstLearner.Courses.First();
        firstCourse.Id.Should().Be("ZPROG001");
        firstCourse.StartDate.Should().Be(new DateTime(2025, 8, 9));
        firstCourse.PlannedEndDate.Should().Be(new DateTime(2025, 9, 27));
        firstCourse.EndDate.Should().Be(new DateTime(2025, 9, 27)); // still in learning — falls back to planned end date
        firstCourse.Status.Should().Be(LearnerCourseStatus.InLearning);
        firstCourse.AgeAtStartOfCourse.Should().Be(25);
        firstCourse.TrainingType.Should().Be(TrainingType.Standard);
        firstCourse.StandardCode.Should().Be(60);

        // remaining deliveries are short courses with no StdCode
        firstLearner.Courses.Skip(1).Should().AllSatisfy(c =>
        {
            c.TrainingType.Should().Be(TrainingType.ShortCourse);
            c.StandardCode.Should().BeNull();
        });
    }

    [Test]
    public async Task Run_MapsCompStatusToLearnerCourseStatus()
    {
        using var stream = LoadTestXml("SFA.DAS.FundingRuleBridge.UnitTests.TestData.sample-ilr.xml");
        _blobClient
            .Setup(b => b.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), default))
            .ReturnsAsync(stream);

        var sut = new DownloadAndParseIlrActivity(_blobServiceClient.Object, NullLogger<DownloadAndParseIlrActivity>.Instance);

        var result = await sut.Run(new IlrFileReference { Container = Container, Filename = Filename }, Mock.Of<FunctionContext>());

        // 9000009803 has CompStatus=3 (Withdrawn)
        result.First(l => l.LearnRefNumber == "9000009803")
            .Courses.First().Status.Should().Be(LearnerCourseStatus.Withdrawn);

        // 9000567903 has CompStatus=2 (Completed)
        result.First(l => l.LearnRefNumber == "9000567903")
            .Courses.First().Status.Should().Be(LearnerCourseStatus.Completed);
    }

    [Test]
    public async Task Run_UsesActualEndDate_WhenPresent()
    {
        using var stream = LoadTestXml("SFA.DAS.FundingRuleBridge.UnitTests.TestData.sample-ilr.xml");
        _blobClient
            .Setup(b => b.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), default))
            .ReturnsAsync(stream);

        var sut = new DownloadAndParseIlrActivity(_blobServiceClient.Object, NullLogger<DownloadAndParseIlrActivity>.Instance);

        var result = await sut.Run(new IlrFileReference { Container = Container, Filename = Filename }, Mock.Of<FunctionContext>());

        // 9000567903 first delivery has LearnActEndDate=2025-08-06, PlannedEndDate=2025-06-19
        result.First(l => l.LearnRefNumber == "9000567903")
            .Courses.First().EndDate.Should().Be(new DateTime(2025, 8, 6));
    }

    [Test]
    public async Task Run_FallsBackToPlannedEndDate_WhenNoActualEndDate()
    {
        using var stream = LoadTestXml("SFA.DAS.FundingRuleBridge.UnitTests.TestData.sample-ilr.xml");
        _blobClient
            .Setup(b => b.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), default))
            .ReturnsAsync(stream);

        var sut = new DownloadAndParseIlrActivity(_blobServiceClient.Object, NullLogger<DownloadAndParseIlrActivity>.Instance);

        var result = await sut.Run(new IlrFileReference { Container = Container, Filename = Filename }, Mock.Of<FunctionContext>());

        // 9000004402 first delivery has no LearnActEndDate, PlannedEndDate=2025-09-27
        result.First(l => l.LearnRefNumber == "9000004402")
            .Courses.First().EndDate.Should().Be(new DateTime(2025, 9, 27));
    }

    [Test]
    public async Task Run_ReturnsEmptyList_WhenNoLearnersInFile()
    {
        const string emptyIlr = """
            <?xml version="1.0" encoding="utf-8"?>
            <Message xmlns="ESFA/ILR/2025-26">
              <Header><CollectionDetails><Collection>ILR</Collection></CollectionDetails></Header>
            </Message>
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(emptyIlr));
        _blobClient
            .Setup(b => b.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), default))
            .ReturnsAsync(stream);

        var sut = new DownloadAndParseIlrActivity(_blobServiceClient.Object, NullLogger<DownloadAndParseIlrActivity>.Instance);

        var result = await sut.Run(new IlrFileReference { Container = Container, Filename = Filename }, Mock.Of<FunctionContext>());

        result.Should().BeEmpty();
    }

    private static Stream LoadTestXml(string resourceName)
    {
        var assembly = typeof(DownloadAndParseIlrActivityTests).Assembly;
        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
    }
}
