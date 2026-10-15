namespace JobGuard.Models;

public class JobAnalysis
{
    public int AnalysisId { get; set; }
    public int UserId { get; set; }
    public string? CompanyName { get; set; }
    public string? JobUrl { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public string? ExtractedText { get; set; }
    public string Prediction { get; set; } = "Legitimate"; // 'Legitimate' or 'Fraudulent'
    public double RiskScore { get; set; } // 0.0 to 100.0
    public string RiskCategory { get; set; } = "Lower Risk";
    public double Confidence { get; set; }
    public string? SuspiciousFlags { get; set; } // Comma or semicolon delimited
    public string ModelVersion { get; set; } = "1.0.0-LR-TFIDF";
    public string? AnalysisSummary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Optional navigation/display helper
    public string? UserEmail { get; set; }
}
