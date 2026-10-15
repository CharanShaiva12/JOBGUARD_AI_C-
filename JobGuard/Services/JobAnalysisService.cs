using JobGuard.Data;
using JobGuard.Models;

namespace JobGuard.Services;

public class FullAnalysisResult
{
    public int AnalysisId { get; set; }
    public string Prediction { get; set; } = "Legitimate";
    public double RiskScore { get; set; }
    public string RiskCategory { get; set; } = "Lower Risk";
    public double Confidence { get; set; }
    public List<string> SuspiciousFlags { get; set; } = new();
    public string ModelVersion { get; set; } = "1.0.0-LR-TFIDF";
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public UrlAnalysisResult UrlAnalysis { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public interface IJobAnalysisService
{
    Task<FullAnalysisResult> AnalyzeAndSaveAsync(int userId, string? companyName, string? jobUrl, string jobDescription, string? extractedText = null);
}

public class JobAnalysisService : IJobAnalysisService
{
    private readonly IMachineLearningService _mlService;
    private readonly IRiskScoringService _riskScoringService;
    private readonly IUrlAnalysisService _urlAnalysisService;
    private readonly IAnalysisRepository _analysisRepository;
    private readonly ILogger<JobAnalysisService> _logger;

    public JobAnalysisService(
        IMachineLearningService mlService,
        IRiskScoringService riskScoringService,
        IUrlAnalysisService urlAnalysisService,
        IAnalysisRepository analysisRepository,
        ILogger<JobAnalysisService> logger)
    {
        _mlService = mlService;
        _riskScoringService = riskScoringService;
        _urlAnalysisService = urlAnalysisService;
        _analysisRepository = analysisRepository;
        _logger = logger;
    }

    public async Task<FullAnalysisResult> AnalyzeAndSaveAsync(
        int userId, 
        string? companyName, 
        string? jobUrl, 
        string jobDescription, 
        string? extractedText = null)
    {
        if (string.IsNullOrWhiteSpace(jobDescription) && string.IsNullOrWhiteSpace(extractedText))
        {
            throw new ArgumentException("A job description or extracted document text is required.");
        }

        string fullTextToEvaluate = string.IsNullOrWhiteSpace(jobDescription) 
            ? (extractedText ?? "") 
            : $"{companyName ?? ""} {jobDescription}".Trim();

        // 1. URL Analysis
        var urlResult = _urlAnalysisService.AnalyzeUrl(jobUrl);

        // 2. Machine Learning TF-IDF Logistic Regression Prediction
        var mlPrediction = _mlService.Predict(fullTextToEvaluate);

        // 3. Risk Calculation
        var risk = _riskScoringService.CalculateRisk(mlPrediction, fullTextToEvaluate, urlResult);

        // 4. Persistence to SQL Server via ADO.NET
        var entity = new JobAnalysis
        {
            UserId = userId,
            CompanyName = companyName?.Trim(),
            JobUrl = jobUrl?.Trim(),
            JobDescription = jobDescription.Trim(),
            ExtractedText = string.IsNullOrWhiteSpace(extractedText) ? null : extractedText.Trim(),
            Prediction = risk.Prediction,
            RiskScore = risk.RiskScore,
            RiskCategory = risk.RiskCategory,
            Confidence = risk.ModelFraudProbability,
            SuspiciousFlags = risk.SuspiciousIndicators.Any() ? string.Join(" | ", risk.SuspiciousIndicators) : null,
            ModelVersion = _mlService.ModelVersion,
            AnalysisSummary = risk.Summary,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            entity.AnalysisId = await _analysisRepository.CreateJobAnalysisAsync(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist job analysis record to SQL Server for user {UserId}", userId);
            // Re-throw or let user know DB failed
            throw new InvalidOperationException("Failed to save analysis record to the database.", ex);
        }

        return new FullAnalysisResult
        {
            AnalysisId = entity.AnalysisId,
            Prediction = entity.Prediction,
            RiskScore = entity.RiskScore,
            RiskCategory = entity.RiskCategory,
            Confidence = entity.Confidence,
            SuspiciousFlags = risk.SuspiciousIndicators,
            ModelVersion = entity.ModelVersion,
            Summary = entity.AnalysisSummary ?? "",
            Recommendation = risk.Recommendation,
            UrlAnalysis = urlResult,
            CreatedAt = entity.CreatedAt
        };
    }
}
