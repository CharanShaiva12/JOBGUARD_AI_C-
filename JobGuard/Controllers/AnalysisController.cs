using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobGuard.Data;
using JobGuard.Models;
using JobGuard.Services;
using JobGuard.ViewModels;

namespace JobGuard.Controllers;

[Authorize]
public class AnalysisController : Controller
{
    private readonly IJobAnalysisService _analysisService;
    private readonly IAnalysisRepository _analysisRepository;
    private readonly IOcrService _ocrService;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        IJobAnalysisService analysisService,
        IAnalysisRepository analysisRepository,
        IOcrService ocrService,
        ILogger<AnalysisController> logger)
    {
        _analysisService = analysisService;
        _analysisRepository = analysisRepository;
        _ocrService = ocrService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new JobAnalysisInputViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(JobAnalysisInputViewModel model)
    {
        string? extractedText = null;

        if (model.UploadedFile != null && model.UploadedFile.Length > 0)
        {
            if (model.UploadedFile.Length > 10 * 1024 * 1024)
            {
                ModelState.AddModelError("UploadedFile", "Uploaded file exceeds maximum limit of 10 MB.");
                return View(model);
            }

            using var stream = model.UploadedFile.OpenReadStream();
            var ocrResult = await _ocrService.ExtractTextAsync(stream, model.UploadedFile.FileName, model.UploadedFile.ContentType);
            if (!ocrResult.Success)
            {
                ModelState.AddModelError("UploadedFile", ocrResult.ErrorMessage ?? "Could not extract text from document.");
                return View(model);
            }

            extractedText = ocrResult.ExtractedText;
        }

        if (string.IsNullOrWhiteSpace(model.JobDescription) && string.IsNullOrWhiteSpace(extractedText))
        {
            ModelState.AddModelError("JobDescription", "Please provide job description text or an advertisement document.");
            return View(model);
        }

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId))
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            var result = await _analysisService.AnalyzeAndSaveAsync(
                userId,
                model.CompanyName,
                model.JobUrl,
                model.JobDescription ?? extractedText ?? "",
                extractedText);

            return RedirectToAction("Result", new { id = result.AnalysisId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis error occurred.");
            ModelState.AddModelError(string.Empty, "Analysis failed. Please check system components.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Result(int id)
    {
        var record = await _analysisRepository.GetAnalysisByIdAsync(id);
        if (record == null)
        {
            return NotFound("Analysis record not found.");
        }

        // Strict Server-Side Record Ownership Authorization
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        if (!int.TryParse(userIdStr, out int currentUserId) || (record.UserId != currentUserId && userRole != "Admin"))
        {
            _logger.LogWarning("Access denied: User {CurrentUserId} attempted to view Analysis {AnalysisId} belonging to User {OwnerId}",
                currentUserId, id, record.UserId);
            return Forbid();
        }

        var viewModel = new AnalysisResultViewModel
        {
            AnalysisId = record.AnalysisId,
            CompanyName = record.CompanyName,
            JobUrl = record.JobUrl,
            JobDescription = record.JobDescription,
            ExtractedText = record.ExtractedText,
            Prediction = record.Prediction,
            RiskScore = record.RiskScore,
            RiskCategory = record.RiskCategory,
            Confidence = record.Confidence,
            SuspiciousFlags = record.SuspiciousFlags?.Split(" | ").ToList() ?? new List<string>(),
            ModelVersion = record.ModelVersion,
            Summary = record.AnalysisSummary ?? "",
            UsesHttps = record.JobUrl?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ?? false,
            Hostname = Uri.TryCreate(record.JobUrl, UriKind.Absolute, out var uri) ? uri.Host : null,
            CreatedAt = record.CreatedAt
        };

        if (viewModel.RiskScore < 30.0)
        {
            viewModel.Recommendation = "Standard professional verification recommended before proceeding.";
        }
        else if (viewModel.RiskScore < 60.0)
        {
            viewModel.Recommendation = "Exercise caution. Confirm employer identity via verified public channels.";
        }
        else
        {
            viewModel.Recommendation = "High risk indicators found. Do not transfer funds or share sensitive credentials.";
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Feedback(int analysisId, string feedbackType, string? feedbackText)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

        var record = await _analysisRepository.GetAnalysisByIdAsync(analysisId);
        if (record == null || record.UserId != userId) return Forbid();

        var feedback = new Feedback
        {
            UserId = userId,
            AnalysisId = analysisId,
            FeedbackType = feedbackType,
            FeedbackText = feedbackText,
            CreatedAt = DateTime.UtcNow
        };

        await _analysisRepository.SaveFeedbackAsync(feedback);
        TempData["FeedbackSuccess"] = "Thank you. Your feedback has been recorded.";
        return RedirectToAction("Result", new { id = analysisId });
    }
}
