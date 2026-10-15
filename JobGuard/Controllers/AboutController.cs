using Microsoft.AspNetCore.Mvc;

namespace JobGuard.Controllers;

public class AboutController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
