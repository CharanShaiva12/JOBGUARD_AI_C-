using System.Text;
using Tesseract;
using UglyToad.PdfPig;

namespace JobGuard.Services;

public class OcrExtractionResult
{
    public bool Success { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public string ExtractionSource { get; set; } = string.Empty; // "PdfTextExtraction", "TesseractOCR", etc.
    public int PageCount { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IOcrService
{
    Task<OcrExtractionResult> ExtractTextAsync(Stream fileStream, string fileName, string contentType);
}

public class OcrService : IOcrService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<OcrService> _logger;

    public OcrService(IWebHostEnvironment env, ILogger<OcrService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<OcrExtractionResult> ExtractTextAsync(Stream fileStream, string fileName, string contentType)
    {
        var result = new OcrExtractionResult();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        try
        {
            if (extension == ".pdf")
            {
                return await ExtractFromPdfAsync(fileStream);
            }
            else if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp")
            {
                return await ExtractFromImageAsync(fileStream);
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = $"Unsupported file extension '{extension}'. Allowed: .pdf, .png, .jpg, .jpeg.";
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from file: {FileName}", fileName);
            result.Success = false;
            result.ErrorMessage = "Unable to process the uploaded file. Please check file formatting or paste the description text manually.";
            return result;
        }
    }

    private async Task<OcrExtractionResult> ExtractFromPdfAsync(Stream pdfStream)
    {
        var result = new OcrExtractionResult();

        // Copy stream into memory
        using var memoryStream = new MemoryStream();
        await pdfStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        try
        {
            using var document = PdfDocument.Open(memoryStream);
            var sb = new StringBuilder();
            int pages = document.NumberOfPages;
            result.PageCount = pages;

            // Enforce page limits for local processing safety (up to 10 pages)
            int maxPages = Math.Min(pages, 10);
            for (int i = 1; i <= maxPages; i++)
            {
                var page = document.GetPage(i);
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }

            var extracted = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(extracted) && extracted.Length > 20)
            {
                result.Success = true;
                result.ExtractedText = extracted;
                result.ExtractionSource = "Direct PDF Vector Text Extraction";
                return result;
            }

            // If selectable text is empty, this is a scanned PDF
            result.Success = false;
            result.ErrorMessage = "PDF contains scanned or flattened bitmap images without an embedded text layer. Please upload the image version (.png, .jpg) directly or paste the text.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF Pig parsing error.");
            result.Success = false;
            result.ErrorMessage = "Failed to parse PDF document structure.";
            return result;
        }
    }

    private async Task<OcrExtractionResult> ExtractFromImageAsync(Stream imageStream)
    {
        var result = new OcrExtractionResult();

        // Check possible tessdata directories
        var candidatePaths = new[]
        {
            Path.Combine(_env.ContentRootPath, "tessdata"),
            Path.Combine(AppContext.BaseDirectory, "tessdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "JobGuard", "tessdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "tessdata")
        };

        string? tessDataPath = candidatePaths.FirstOrDefault(p => Directory.Exists(p) && File.Exists(Path.Combine(p, "eng.traineddata")));

        if (tessDataPath == null)
        {
            result.Success = false;
            result.ErrorMessage = "Tesseract OCR engine language file (eng.traineddata) was not found in the application path. Please copy and paste the description text manually.";
            return result;
        }

        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream);
        byte[] imageBytes = memoryStream.ToArray();

        try
        {
            using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            using var img = Pix.LoadFromMemory(imageBytes);
            using var page = engine.Process(img);

            var text = page.GetText();
            var meanConfidence = page.GetMeanConfidence();

            if (string.IsNullOrWhiteSpace(text))
            {
                result.Success = false;
                result.ErrorMessage = "OCR scan completed but detected no legible text characters in this image. Please verify resolution and contrast, or paste the text directly.";
                return result;
            }

            result.Success = true;
            result.ExtractedText = text.Trim();
            result.ExtractionSource = $"Tesseract OCR (Engine confidence: {meanConfidence:P1})";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesseract native OCR extraction failed.");
            result.Success = false;
            result.ErrorMessage = $"OCR processing failed ({ex.Message}). Please copy and paste the job description text manually.";
            return result;
        }
    }
}
