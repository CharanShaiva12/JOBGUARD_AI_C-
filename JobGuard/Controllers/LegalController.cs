using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using JobGuard.Data;
using JobGuard.Models;
using JobGuard.ViewModels;

namespace JobGuard.Controllers;

public class LegalController : Controller
{
    private readonly ICookieConsentRepository _consentRepo;

    public LegalController(ICookieConsentRepository consentRepo)
    {
        _consentRepo = consentRepo;
    }

    [HttpGet]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Terms()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Cookies()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCookieConsent([FromBody] CookiePreferencesViewModel model)
    {
        string consentToken = Request.Cookies["JobGuard_ConsentToken"] ?? Guid.NewGuid().ToString("N");

        int? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int uid)) userId = uid;
        }

        var consent = new CookieConsent
        {
            UserId = userId,
            ConsentIdentifier = consentToken,
            AcceptedEssential = true,
            AcceptedAnalytics = model.Analytics,
            AcceptedPreferences = model.Preferences,
            PolicyVersion = "1.0",
            ConsentDate = DateTime.UtcNow
        };

        await _consentRepo.SaveConsentAsync(consent);

        // Store cookie on client with root Path = "/" so all routes see it
        var options = new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = false, // Accessible to JS for banner logic
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps
        };

        Response.Cookies.Append("JobGuard_ConsentToken", consentToken, options);
        Response.Cookies.Append("JobGuard_ConsentStatus", $"{model.Analytics}_{model.Preferences}", options);

        return Json(new { success = true, analytics = model.Analytics, preferences = model.Preferences });
    }
}
