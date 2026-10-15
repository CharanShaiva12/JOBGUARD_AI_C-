using System.Text.RegularExpressions;

namespace JobGuard.Services;

public class UrlAnalysisResult
{
    public bool HasUrl { get; set; }
    public string? RawUrl { get; set; }
    public string? Hostname { get; set; }
    public bool UsesHttps { get; set; }
    public bool IsIpAddress { get; set; }
    public bool IsInternalOrPrivate { get; set; }
    public bool HasSuspiciousSubdomains { get; set; }
    public bool HasSuspiciousKeywords { get; set; }
    public List<string> Warnings { get; set; } = new();
    public int AdditionalRiskScoreAdjustment { get; set; }
}

public interface IUrlAnalysisService
{
    UrlAnalysisResult AnalyzeUrl(string? url);
}

public class UrlAnalysisService : IUrlAnalysisService
{
    private readonly ILogger<UrlAnalysisService> _logger;

    public UrlAnalysisService(ILogger<UrlAnalysisService> logger)
    {
        _logger = logger;
    }

    public UrlAnalysisResult AnalyzeUrl(string? url)
    {
        var result = new UrlAnalysisResult();
        if (string.IsNullOrWhiteSpace(url))
        {
            return result;
        }

        result.HasUrl = true;
        result.RawUrl = url.Trim();

        // Normalize URL if protocol is omitted
        string normalized = result.RawUrl;
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "https://" + normalized;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri))
        {
            result.Warnings.Add("Malformed or unparseable URL format.");
            result.AdditionalRiskScoreAdjustment += 15;
            return result;
        }

        // Scheme security checks
        if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            result.UsesHttps = true;
        }
        else if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            result.UsesHttps = false;
            result.Warnings.Add("Unencrypted HTTP protocol used instead of HTTPS.");
            result.AdditionalRiskScoreAdjustment += 10;
        }
        else
        {
            result.Warnings.Add($"Non-standard URL scheme '{uri.Scheme}' detected.");
            result.AdditionalRiskScoreAdjustment += 20;
        }

        result.Hostname = uri.Host;

        // SSRF and private network checks
        if (IsPrivateOrLoopbackHost(uri.Host))
        {
            result.IsInternalOrPrivate = true;
            result.Warnings.Add("URL points to a local, loopback, or private internal network address.");
            result.AdditionalRiskScoreAdjustment += 35;
        }

        // IP address host check
        if (System.Net.IPAddress.TryParse(uri.Host, out _))
        {
            result.IsIpAddress = true;
            result.Warnings.Add("Host uses raw numerical IP address rather than a domain name.");
            result.AdditionalRiskScoreAdjustment += 25;
        }

        // Unusual subdomain structures (e.g., career.apple.fake-domain.com or many levels)
        var hostParts = uri.Host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (hostParts.Length > 4)
        {
            result.HasSuspiciousSubdomains = true;
            result.Warnings.Add("Excessive subdomain depth detected (potential domain spoofing pattern).");
            result.AdditionalRiskScoreAdjustment += 15;
        }

        // Common deceptive keywords in host path or domain
        var suspiciousKeywords = new[] { "login-verify", "wire-transfer", "payment-check", "free-cash", "crypto-payout", "westernunion", "telegram-hire" };
        foreach (var kw in suspiciousKeywords)
        {
            if (uri.Host.Contains(kw, StringComparison.OrdinalIgnoreCase) || uri.AbsolutePath.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                result.HasSuspiciousKeywords = true;
                result.Warnings.Add($"Suspicious token '{kw}' found in URL address.");
                result.AdditionalRiskScoreAdjustment += 20;
                break;
            }
        }

        return result;
    }

    private static bool IsPrivateOrLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;

        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            if (System.Net.IPAddress.IsLoopback(ip)) return true;

            var bytes = ip.GetAddressBytes();
            if (bytes.Length == 4)
            {
                // 10.0.0.0/8
                if (bytes[0] == 10) return true;
                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                // 169.254.0.0/16 link-local
                if (bytes[0] == 169 && bytes[1] == 254) return true;
            }
        }

        return false;
    }
}
