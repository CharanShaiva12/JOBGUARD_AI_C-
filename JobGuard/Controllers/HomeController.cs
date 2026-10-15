using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using JobGuard.Services;
using JobGuard.ViewModels;

namespace JobGuard.Controllers;

public class HomeController : Controller
{
    private readonly IOcrService _ocrService;
    private readonly IJobAnalysisService _analysisService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IOcrService ocrService, 
        IJobAnalysisService analysisService, 
        ILogger<HomeController> logger)
    {
        _ocrService = ocrService;
        _analysisService = analysisService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var model = new JobAnalysisInputViewModel();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Analyze(JobAnalysisInputViewModel model)
    {
        // 1. Check if authenticated
        if (User.Identity?.IsAuthenticated != true)
        {
            // Store pending inputs in TempData to restore upon login
            TempData["PendingCompanyName"] = model.CompanyName;
            TempData["PendingJobUrl"] = model.JobUrl;
            TempData["PendingJobDescription"] = model.JobDescription;
            TempData["AuthRequiredMessage"] = "Please sign in or register an account to execute the AI fraud detection analysis.";

            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Home") });
        }

        // 2. Validate inputs
        string? extractedText = null;

        if (model.UploadedFile != null && model.UploadedFile.Length > 0)
        {
            if (model.UploadedFile.Length > 10 * 1024 * 1024) // 10MB limit
            {
                ModelState.AddModelError("UploadedFile", "Uploaded file exceeds maximum limit of 10 MB.");
                return View("Index", model);
            }

            using var stream = model.UploadedFile.OpenReadStream();
            var ocrResult = await _ocrService.ExtractTextAsync(stream, model.UploadedFile.FileName, model.UploadedFile.ContentType);
            if (!ocrResult.Success)
            {
                ModelState.AddModelError("UploadedFile", ocrResult.ErrorMessage ?? "Could not extract text from document.");
                return View("Index", model);
            }

            extractedText = ocrResult.ExtractedText;
        }

        if (string.IsNullOrWhiteSpace(model.JobDescription) && string.IsNullOrWhiteSpace(extractedText))
        {
            ModelState.AddModelError("JobDescription", "Please enter a job description or upload an ad document.");
            return View("Index", model);
        }

        // 3. User Identifier
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

            return RedirectToAction("Result", "Analysis", new { id = result.AnalysisId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis failed for user {UserId}", userId);
            ModelState.AddModelError(string.Empty, "Analysis could not be completed. Please check database connectivity or ML model availability.");
            return View("Index", model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtractDocumentText(IFormFile? documentFile)
    {
        if (documentFile == null || documentFile.Length == 0)
        {
            return Json(new { success = false, message = "No file was selected for upload." });
        }

        if (documentFile.Length > 10 * 1024 * 1024)
        {
            return Json(new { success = false, message = "File exceeds the 10 MB size limit." });
        }

        try
        {
            using var stream = documentFile.OpenReadStream();
            var result = await _ocrService.ExtractTextAsync(stream, documentFile.FileName, documentFile.ContentType);

            if (result.Success)
            {
                return Json(new { success = true, text = result.ExtractedText, source = result.ExtractionSource });
            }

            return Json(new { success = false, message = result.ErrorMessage ?? "Could not extract text from document." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document extraction for file {FileName}", documentFile.FileName);
            return Json(new { success = false, message = $"Extraction encountered an error: {ex.Message}. Please paste the description text manually." });
        }
    }
}
