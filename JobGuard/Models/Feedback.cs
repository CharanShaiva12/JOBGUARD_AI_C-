namespace JobGuard.Models;

public class Feedback
{
    public int FeedbackId { get; set; }
    public int UserId { get; set; }
    public int AnalysisId { get; set; }
    public string FeedbackType { get; set; } = string.Empty; // 'Accurate', 'FalsePositive', 'FalseNegative', 'Other'
    public string? FeedbackText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
