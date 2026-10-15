using System.ComponentModel.DataAnnotations;

namespace JobGuard.ViewModels;

public class JobAnalysisInputViewModel
{
    [Display(Name = "Company Name (Optional)")]
    [StringLength(200, ErrorMessage = "Company name cannot exceed 200 characters.")]
    public string? CompanyName { get; set; }

    [Display(Name = "Job Posting URL (Optional)")]
    [Url(ErrorMessage = "Please enter a valid URL format.")]
    [StringLength(1000, ErrorMessage = "URL cannot exceed 1000 characters.")]
    public string? JobUrl { get; set; }

    [Display(Name = "Job Description / Offer Text")]
    [MaxLength(20000, ErrorMessage = "Job description cannot exceed 20,000 characters.")]
    public string? JobDescription { get; set; }

    [Display(Name = "Upload Job Ad PDF or Image (Optional)")]
    public IFormFile? UploadedFile { get; set; }
}

public class AnalysisResultViewModel
{
    public int AnalysisId { get; set; }
    public string? CompanyName { get; set; }
    public string? JobUrl { get; set; }
    public string JobDescription { get; set; } = string.Empty;
    public string? ExtractedText { get; set; }
    public string Prediction { get; set; } = "Legitimate";
    public double RiskScore { get; set; }
    public string RiskCategory { get; set; } = "Lower Risk";
    public double Confidence { get; set; }
    public List<string> SuspiciousFlags { get; set; } = new();
    public string ModelVersion { get; set; } = "1.0.0-LR-TFIDF";
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public bool UsesHttps { get; set; }
    public string? Hostname { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DashboardViewModel
{
    public int TotalAnalyzed { get; set; }
    public int LowerRiskCount { get; set; }
    public int FraudulentRiskCount { get; set; }
    public List<JobGuard.Models.JobAnalysis> RecentAnalyses { get; set; } = new();
}

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalAnalyses { get; set; }
    public int LegitimateCount { get; set; }
    public int FraudulentCount { get; set; }
    public double AvgRiskScore { get; set; }
    public List<JobGuard.Models.JobAnalysis> RecentAnalyses { get; set; } = new();
    public List<JobGuard.Models.User> RecentUsers { get; set; } = new();
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms and conditions.")]
    public bool AcceptTerms { get; set; }

    public string? ReturnUrl { get; set; }
}

public class CookiePreferencesViewModel
{
    public bool Essential { get; set; } = true;
    public bool Analytics { get; set; }
    public bool Preferences { get; set; }
}
