using JobGuard.ML;
using JobGuard.Services;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;

namespace JobGuard.Tests;

public class MachineLearningTests
{
    [Fact]
    public void MachineLearningService_Loads_Trained_Model_And_Performs_Inference()
    {
        // Setup mock environment pointing to the root of JobGuard project
        var mockEnv = new Mock<IWebHostEnvironment>();
        string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "JobGuard"));
        mockEnv.Setup(e => e.ContentRootPath).Returns(projectRoot);

        var mlService = new MachineLearningService(
            mockEnv.Object,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<MachineLearningService>());

        Assert.True(mlService.IsModelLoaded);
        Assert.Equal("1.0.0-LR-TFIDF", mlService.ModelVersion);

        // Test Legitimate Job Text
        var legResult = mlService.Predict("Software developer needed for ASP.NET Core web platform. Must have C#, SQL, and unit testing experience. Full benefits, medical, dental, 401k match.");
        Assert.NotNull(legResult);
        Assert.InRange(legResult.Probability, 0.0f, 1.0f);

        // Test Obvious Scam Text
        var fraudResult = mlService.Predict("URGENT: Work from home envelope packing. Earn $1,000 weekly guaranteed. No skills needed. Send $50 registration fee via Western Union or CashApp immediately.");
        Assert.NotNull(fraudResult);
        Assert.True(fraudResult.Probability > 0.5f);
        Assert.True(fraudResult.PredictedLabel);
    }
}
