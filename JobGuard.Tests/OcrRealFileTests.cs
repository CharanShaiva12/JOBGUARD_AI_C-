using JobGuard.Services;
using Microsoft.AspNetCore.Hosting;
using Moq;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace JobGuard.Tests;

public class OcrRealFileTests
{
    private readonly OcrService _ocrService;

    public OcrRealFileTests()
    {
        var mockEnv = new Mock<IWebHostEnvironment>();
        string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "JobGuard"));
        mockEnv.Setup(e => e.ContentRootPath).Returns(projectRoot);

        _ocrService = new OcrService(mockEnv.Object, new Microsoft.Extensions.Logging.Abstractions.NullLogger<OcrService>());
    }

    [Fact]
    public async Task ExtractText_From_Real_Vector_Pdf_Succeeds_And_Preserves_Text()
    {
        // Generate a valid PDF stream in memory using PdfPig writer
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        
        string testAdContent = "Senior Cybersecurity Analyst position. Experience with SIEM, penetration testing, and incident response required. Contact HR at careers@verifiedcorp.com.";
        page.AddText(testAdContent, 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), font);

        byte[] pdfBytes = builder.Build();
        using var stream = new MemoryStream(pdfBytes);

        var result = await _ocrService.ExtractTextAsync(stream, "SampleJobAd.pdf", "application/pdf");

        Assert.True(result.Success);
        Assert.Contains("Senior Cybersecurity Analyst", result.ExtractedText);
        Assert.Contains("Direct PDF Vector Text Extraction", result.ExtractionSource);
    }

    [Fact]
    public async Task ExtractText_With_Image_Initializes_Engine_Gracefully()
    {
        // 1x1 blank PNG
        byte[] blankPng = new byte[] {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
            0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82
        };

        using var stream = new MemoryStream(blankPng);
        var result = await _ocrService.ExtractTextAsync(stream, "empty.png", "image/png");

        // Blank image contains no text - verify it returns proper error without throwing unhandled exception
        Assert.False(result.Success);
        Assert.Contains("no legible text", result.ErrorMessage);
    }
}
