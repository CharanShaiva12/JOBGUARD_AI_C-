using System.Security.Cryptography;
using System.Text;
using JobGuard.Data;
using JobGuard.Models;

namespace JobGuard.Services;

public interface IAuthenticationService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    Task<(bool Success, string? Error, User? User)> RegisterUserAsync(string fullName, string email, string password, string role = "User");
    Task<(bool Success, string? Error, User? User)> AuthenticateAsync(string email, string password);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(IUserRepository userRepository, ILogger<AuthenticationService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public string HashPassword(string password)
    {
        // PBKDF2 with SHA-256 and 100,000 iterations
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: 100_000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);

        // Format: v1:{iterations}:{salt_base64}:{hash_base64}
        return $"v1:100000:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) return false;

        var parts = passwordHash.Split(':');
        if (parts.Length != 4 || parts[0] != "v1") return false;

        if (!int.TryParse(parts[1], out int iterations)) return false;
        byte[] salt = Convert.FromBase64String(parts[2]);
        byte[] expectedHash = Convert.FromBase64String(parts[3]);

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public async Task<(bool Success, string? Error, User? User)> RegisterUserAsync(string fullName, string email, string password, string role = "User")
    {
        var existing = await _userRepository.GetUserByEmailAsync(email);
        if (existing != null)
        {
            return (false, "An account with this email address already exists.", null);
        }

        var user = new User
        {
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = HashPassword(password),
            Role = string.IsNullOrWhiteSpace(role) ? "User" : role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            user.UserId = await _userRepository.RegisterUserAsync(user);
            return (true, null, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while registering new user {Email}", email);
            return (false, "An error occurred while creating your account. Please try again.", null);
        }
    }

    public async Task<(bool Success, string? Error, User? User)> AuthenticateAsync(string email, string password)
    {
        var user = await _userRepository.GetUserByEmailAsync(email);
        if (user == null)
        {
            return (false, "Invalid email address or password.", null);
        }

        if (!user.IsActive)
        {
            return (false, "This account is inactive. Please contact administrator.", null);
        }

        if (!VerifyPassword(password, user.PasswordHash))
        {
            return (false, "Invalid email address or password.", null);
        }

        return (true, null, user);
    }
}
