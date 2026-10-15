using System.Text.RegularExpressions;
using JobGuard.ML;

namespace JobGuard.Services;

public class RiskAssessment
{
    public string Prediction { get; set; } = "Legitimate"; // "Legitimate" or "Fraudulent"
    public double RiskScore { get; set; } // 0.0 - 100.0
    public string RiskCategory { get; set; } = "Lower Risk";
    public double ModelFraudProbability { get; set; }
    public List<string> SuspiciousIndicators { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public interface IRiskScoringService
{
    RiskAssessment CalculateRisk(JobPredictionOutput mlOutput, string jobDescription, UrlAnalysisResult urlResult);
}

public class RiskScoringService : IRiskScoringService
{
    // Thresholds:
    // 0 - 29: Lower Risk
    // 30 - 59: Moderate Risk
    // 60 - 79: High Risk
    // 80 - 100: Very High Risk

    public RiskAssessment CalculateRisk(JobPredictionOutput mlOutput, string jobDescription, UrlAnalysisResult urlResult)
    {
        var assessment = new RiskAssessment();

        // 1. Base Score directly derived from ML model fraud probability (0.0 to 100.0)
        double baseScore = Math.Clamp(mlOutput.Probability * 100.0, 0.0, 100.0);
        assessment.ModelFraudProbability = Math.Round(mlOutput.Probability, 4);

        // 2. Heuristic text pattern indicators (explicitly separated and transparent)
        var textFlags = DetectTextIndicators(jobDescription);
        foreach (var flag in textFlags)
        {
            assessment.SuspiciousIndicators.Add(flag);
        }

        // 3. Incorporate URL warnings if any
        if (urlResult.HasUrl && urlResult.Warnings.Any())
        {
            foreach (var w in urlResult.Warnings)
            {
                assessment.SuspiciousIndicators.Add($"[URL Security] {w}");
            }
        }

        // 4. Calculate adjusted risk score
        // We preserve the primary contribution from ML (85% weight) + transparent indicators adjustment (up to 15%)
        double indicatorAdjustment = Math.Min(15.0, (textFlags.Count * 4.0) + (urlResult.AdditionalRiskScoreAdjustment * 0.3));
        
        // If the ML model already predicted fraudulent with high confidence, maintain high score
        double finalScore = (baseScore * 0.85) + (indicatorAdjustment * 1.0);
        if (mlOutput.PredictedLabel && finalScore < 60.0)
        {
            finalScore = Math.Max(finalScore, 65.0);
        }
        else if (!mlOutput.PredictedLabel && finalScore > 75.0 && textFlags.Count == 0)
        {
            // Keep legitimate model prediction grounded if no strong red flags
            finalScore = Math.Min(finalScore, 49.0);
        }

        finalScore = Math.Clamp(Math.Round(finalScore, 1), 0.0, 100.0);
        assessment.RiskScore = finalScore;

        // Categorization based on documented thresholds
        if (finalScore < 30.0)
        {
            assessment.RiskCategory = "Lower Risk";
            assessment.Prediction = "Legitimate";
            assessment.Recommendation = "The listing displays typical legitimate recruiting patterns. Continue standard professional verification (official company domain, recognized job portal) before sharing personal details.";
        }
        else if (finalScore < 60.0)
        {
            assessment.RiskCategory = "Moderate Risk";
            assessment.Prediction = mlOutput.PredictedLabel ? "Fraudulent" : "Legitimate";
            assessment.Recommendation = "Some non-standard phrasing or unusual signals were detected. Verify the employer independently through their official LinkedIn page and corporate website prior to applying.";
        }
        else if (finalScore < 80.0)
        {
            assessment.RiskCategory = "High Risk";
            assessment.Prediction = "Fraudulent";
            assessment.Recommendation = "High risk signals detected. Do not send identity documents, bank account details, or advance payments. Verify directly with the verified employer headquarters.";
        }
        else
        {
            assessment.RiskCategory = "Very High Risk";
            assessment.Prediction = "Fraudulent";
            assessment.Recommendation = "Strong indicators of employment fraud detected matching known scam patterns. We recommend avoiding this posting and not engaging with the recruiter.";
        }

        // Summary statement
        assessment.Summary = $"Evaluation completed using JobGuard ML.NET TF-IDF Logistic Regression model. Estimated fraud risk score is {finalScore}/100 ({assessment.RiskCategory}). Model prediction: {assessment.Prediction}. Identified {assessment.SuspiciousIndicators.Count} specific indicator(s).";

        return assessment;
    }

    private static List<string> DetectTextIndicators(string text)
    {
        var indicators = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return indicators;

        var lower = text.ToLowerInvariant();

        if (Regex.IsMatch(lower, @"(wire\s*transfer|western\s*union|moneygram|cash\s*app|crypto|bitcoin|usdt)"))
        {
            indicators.Add("Mentions irreversible money transfer, cryptocurrency, or non-standard payment channels.");
        }

        if (Regex.IsMatch(lower, @"(no\s*experience\s*(needed|necessary|required).*(\$\s*[0-9]{3,}|earn\s*\$\s*[0-9]{3,}|per\s*day|daily))"))
        {
            indicators.Add("Unrealistically high guaranteed daily earnings with zero required experience.");
        }

        if (Regex.IsMatch(lower, @"(contact\s*(me|us)\s*on\s*(telegram|whatsapp|signal)|@gmail\.com|@yahoo\.com|@hotmail\.com)"))
        {
            indicators.Add("Instructs direct off-platform communication via private messaging app or free personal email domain.");
        }

        if (Regex.IsMatch(lower, @"(equipment\s*(fee|deposit|check)|starter\s*kit\s*fee|background\s*check\s*fee|registration\s*fee)"))
        {
            indicators.Add("Requires candidate to pay upfront fees, buy equipment, or deposit a company check.");
        }

        if (Regex.IsMatch(lower, @"(package\s*forwarding|re-shipping|mystery\s*shopper|reshipping\s*agent)"))
        {
            indicators.Add("Job description matches known reshipping / package mule fraud schemes.");
        }

        return indicators;
    }
}
