namespace JobGuard.Models;

public class CookieConsent
{
    public int ConsentId { get; set; }
    public int? UserId { get; set; }
    public string ConsentIdentifier { get; set; } = string.Empty;
    public bool AcceptedEssential { get; set; } = true;
    public bool AcceptedAnalytics { get; set; } = false;
    public bool AcceptedPreferences { get; set; } = false;
    public string PolicyVersion { get; set; } = "1.0";
    public DateTime ConsentDate { get; set; } = DateTime.UtcNow;
}
