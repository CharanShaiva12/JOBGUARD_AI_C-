using JobGuard.Services;
using Xunit;

namespace JobGuard.Tests;

public class AuthenticationTests
{
    private readonly AuthenticationService _authService = new(
        null!, // Not required for hashing algorithm verification
        new Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthenticationService>());

    [Fact]
    public void HashPassword_Generates_Valid_PBKDF2_Salted_Hash()
    {
        string rawPassword = "StrongSecurityPassword123!";
        string hash = _authService.HashPassword(rawPassword);

        Assert.NotNull(hash);
        Assert.StartsWith("v1:100000:", hash);

        // Different salts must produce distinct hash strings
        string secondHash = _authService.HashPassword(rawPassword);
        Assert.NotEqual(hash, secondHash);
    }

    [Fact]
    public void VerifyPassword_Validates_Correct_Password_Successfully()
    {
        string rawPassword = "MySecurePassword2026";
        string hash = _authService.HashPassword(rawPassword);

        bool isValid = _authService.VerifyPassword(rawPassword, hash);
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_Rejects_Incorrect_Password()
    {
        string rawPassword = "CorrectPassword123";
        string hash = _authService.HashPassword(rawPassword);

        bool isValid = _authService.VerifyPassword("WrongPassword456", hash);
        Assert.False(isValid);
    }
}
