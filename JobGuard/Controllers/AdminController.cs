using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobGuard.Data;
using JobGuard.ViewModels;

namespace JobGuard.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IAnalysisRepository _analysisRepository;
    private readonly IUserRepository _userRepository;

    public AdminController(IAnalysisRepository analysisRepository, IUserRepository userRepository)
    {
        _analysisRepository = analysisRepository;
        _userRepository = userRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var (totalAnalyses, totalUsers, legCount, fraudCount, avgRisk) = await _analysisRepository.GetSystemStatsAsync();
        var recentAnalyses = await _analysisRepository.GetRecentAnalysesAsync(limit: 20);
        var recentUsers = await _userRepository.GetAllUsersAsync(max: 20);

        var viewModel = new AdminDashboardViewModel
        {
            TotalAnalyses = totalAnalyses,
            TotalUsers = totalUsers,
            LegitimateCount = legCount,
            FraudulentCount = fraudCount,
            AvgRiskScore = Math.Round(avgRisk, 1),
            RecentAnalyses = recentAnalyses,
            RecentUsers = recentUsers
        };

        return View(viewModel);
    }
}
