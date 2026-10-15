using System.Text.RegularExpressions;
using JobGuard.ML;
using JobGuard.Services;
using Xunit;

namespace JobGuard.Tests;

public class SecurityAndRiskTests
{
    private readonly RiskScoringService _riskService = new();
    private readonly UrlAnalysisService _urlService = new(new Microsoft.Extensions.Logging.Abstractions.NullLogger<UrlAnalysisService>());

    [Fact]
    public void UrlAnalysis_Identifies_Localhost_And_Private_IPs_For_SSRF_Protection()
    {
        var localResult = _urlService.AnalyzeUrl("http://localhost:8080/admin");
        Assert.True(localResult.IsInternalOrPrivate);
        Assert.Contains(localResult.Warnings, w => w.Contains("loopback, or private internal network"));

        var privateIpResult = _urlService.AnalyzeUrl("http://192.168.1.100/apply");
        Assert.True(privateIpResult.IsInternalOrPrivate);

        var loopbackIpResult = _urlService.AnalyzeUrl("http://127.0.0.1/job");
        Assert.True(loopbackIpResult.IsInternalOrPrivate);
    }

    [Fact]
    public void UrlAnalysis_Detects_Suspicious_Raw_IP_And_Unencrypted_Http()
    {
        var result = _urlService.AnalyzeUrl("http://185.220.101.5/job-application");
        Assert.False(result.UsesHttps);
        Assert.True(result.IsIpAddress);
        Assert.Contains(result.Warnings, w => w.Contains("Unencrypted HTTP"));
        Assert.Contains(result.Warnings, w => w.Contains("raw numerical IP address"));
    }

    [Fact]
    public void UrlAnalysis_Accepts_Legitimate_Https_Domain()
    {
        var result = _urlService.AnalyzeUrl("https://careers.google.com/jobs/results");
        Assert.True(result.UsesHttps);
        Assert.False(result.IsInternalOrPrivate);
        Assert.False(result.IsIpAddress);
        Assert.Empty(result.Warnings);
        Assert.Equal(0, result.AdditionalRiskScoreAdjustment);
    }

    [Fact]
    public void RiskScoring_Calculates_LowerRisk_For_Legitimate_Job()
    {
        var mlOutput = new JobPredictionOutput
        {
            PredictedLabel = false,
            Probability = 0.05f,
            Score = -3.2f
        };

        var urlResult = new UrlAnalysisResult { UsesHttps = true };
        string description = "Senior Software Engineer. Requirements: 5 years C# experience, ASP.NET Core knowledge. Excellent team communication.";

        var assessment = _riskService.CalculateRisk(mlOutput, description, urlResult);

        Assert.Equal("Legitimate", assessment.Prediction);
        Assert.Equal("Lower Risk", assessment.RiskCategory);
        Assert.True(assessment.RiskScore < 30.0);
    }

    [Fact]
    public void RiskScoring_Flags_UpfrontPayment_And_Crypto_Indicators()
    {
        var mlOutput = new JobPredictionOutput
        {
            PredictedLabel = true,
            Probability = 0.95f,
            Score = 4.1f
        };

        var urlResult = new UrlAnalysisResult();
        string suspiciousDesc = "Urgent data entry assistant needed. No experience needed earn $800 daily. Candidate must pay equipment starter kit fee via wire transfer or western union or crypto bitcoin.";

        var assessment = _riskService.CalculateRisk(mlOutput, suspiciousDesc, urlResult);

        Assert.Equal("Fraudulent", assessment.Prediction);
        Assert.Equal("Very High Risk", assessment.RiskCategory);
        Assert.True(assessment.RiskScore >= 80.0);
        Assert.Contains(assessment.SuspiciousIndicators, i => i.Contains("money transfer"));
        Assert.Contains(assessment.SuspiciousIndicators, i => i.Contains("upfront fees"));
    }
}
