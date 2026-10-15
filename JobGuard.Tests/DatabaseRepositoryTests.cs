using System.Data;
using Microsoft.Data.SqlClient;
using JobGuard.Data;
using JobGuard.Models;
using Xunit;

namespace JobGuard.Tests;

public class DatabaseRepositoryTests
{
    private readonly string _connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=JobGuardDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    private SqlConnectionFactory CreateFactory()
    {
        var mockConfig = new Moq.Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        mockConfig.Setup(c => c.GetSection("ConnectionStrings")["DefaultConnection"]).Returns(_connectionString);
        var mockSection = new Moq.Mock<Microsoft.Extensions.Configuration.IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns(_connectionString);
        mockConfig.Setup(c => c.GetSection("ConnectionStrings:DefaultConnection")).Returns(mockSection.Object);
        // Also setup standard extension method indexer fallback
        mockConfig.Setup(c => c[Microsoft.Extensions.Configuration.ConfigurationPath.Combine("ConnectionStrings", "DefaultConnection")]).Returns(_connectionString);
        mockConfig.Setup(c => c["ConnectionStrings:DefaultConnection"]).Returns(_connectionString);

        return new SqlConnectionFactory(mockConfig.Object);
    }

    [Fact]
    public async Task UserRepository_Can_Register_And_Retrieve_User()
    {
        var factory = CreateFactory();
        var repo = new UserRepository(factory);

        string testEmail = $"testuser_{Guid.NewGuid():N}@jobguard.local";
        var user = new User
        {
            FullName = "Integration Test User",
            Email = testEmail,
            PasswordHash = "v1:100000:salt:hash",
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        int userId = await repo.RegisterUserAsync(user);
        Assert.True(userId > 0);

        var retrieved = await repo.GetUserByEmailAsync(testEmail);
        Assert.NotNull(retrieved);
        Assert.Equal(testEmail, retrieved.Email);
        Assert.Equal("Integration Test User", retrieved.FullName);
    }

    [Fact]
    public async Task AnalysisRepository_Can_Create_And_Retrieve_JobAnalysis()
    {
        var factory = CreateFactory();
        var userRepo = new UserRepository(factory);
        var analysisRepo = new AnalysisRepository(factory);

        string testEmail = $"analysistest_{Guid.NewGuid():N}@jobguard.local";
        var user = new User
        {
            FullName = "Analysis Tester",
            Email = testEmail,
            PasswordHash = "v1:100000:salt:hash",
            Role = "User",
            IsActive = true
        };
        int userId = await userRepo.RegisterUserAsync(user);

        var analysis = new JobAnalysis
        {
            UserId = userId,
            CompanyName = "Test Fraud Corp",
            JobUrl = "https://example.com/posting/99",
            JobDescription = "Testing database insertion and SqlDataReader retrieval.",
            Prediction = "Fraudulent",
            RiskScore = 84.5,
            RiskCategory = "Very High Risk",
            Confidence = 0.94,
            SuspiciousFlags = "Wire transfer payment | No interview required",
            ModelVersion = "1.0.0-LR-TFIDF",
            AnalysisSummary = "Flagged during automated integration testing."
        };

        int analysisId = await analysisRepo.CreateJobAnalysisAsync(analysis);
        Assert.True(analysisId > 0);

        var retrieved = await analysisRepo.GetAnalysisByIdAsync(analysisId);
        Assert.NotNull(retrieved);
        Assert.Equal("Test Fraud Corp", retrieved.CompanyName);
        Assert.Equal(84.5, retrieved.RiskScore);
        Assert.Equal("Fraudulent", retrieved.Prediction);
        Assert.Equal(userId, retrieved.UserId);

        var (total, leg, fraud) = await analysisRepo.GetUserStatsAsync(userId);
        Assert.Equal(1, total);
        Assert.Equal(1, fraud);
        Assert.Equal(0, leg);
    }
}
