using JobGuard.Services;
using Xunit;

namespace JobGuard.Tests;

public class OcrServiceTests
{
    private readonly OcrService _ocrService;

    public OcrServiceTests()
    {
        var mockEnv = new Moq.Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);
        _ocrService = new OcrService(mockEnv.Object, new Microsoft.Extensions.Logging.Abstractions.NullLogger<OcrService>());
    }

    [Fact]
    public async Task ExtractText_Rejects_Unsupported_File_Types()
    {
        using var stream = new MemoryStream(new byte[] { 0x01, 0x02, 0x03 });
        var result = await _ocrService.ExtractTextAsync(stream, "resume.exe", "application/octet-stream");

        Assert.False(result.Success);
        Assert.Contains("Unsupported file extension", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractText_Handles_Malformed_Empty_Pdf_Safely()
    {
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // incomplete "%PDF"
        var result = await _ocrService.ExtractTextAsync(stream, "job_ad.pdf", "application/pdf");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
}
