using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobGuard.Data;
using JobGuard.ViewModels;

namespace JobGuard.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IAnalysisRepository _analysisRepository;

    public DashboardController(IAnalysisRepository analysisRepository)
    {
        _analysisRepository = analysisRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var (total, legitimate, fraudulent) = await _analysisRepository.GetUserStatsAsync(userId);
        var recent = await _analysisRepository.GetAnalysesByUserIdAsync(userId, limit: 20);

        var viewModel = new DashboardViewModel
        {
            TotalAnalyzed = total,
            LowerRiskCount = legitimate,
            FraudulentRiskCount = fraudulent,
            RecentAnalyses = recent
        };

        return View(viewModel);
    }
}
