using GeminiVisionApp.Models;
using GeminiVisionApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace GeminiVisionApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VisionController : ControllerBase
{
    private readonly IGeminiVisionService _visionService;
    private readonly ILogger<VisionController> _logger;

    public VisionController(IGeminiVisionService visionService, ILogger<VisionController> logger)
    {
        _visionService = visionService ?? throw new ArgumentNullException(nameof(visionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Приема качена снимка (multipart/form-data) и текстов промпт и връща описание от Gemini AI.
    /// </summary>
    /// <summary>
    /// Приема две изображения и промпт за откриване на прилики и разлики.
    /// </summary>
    /// 
    /// <summary>
    /// Приема качена снимка (multipart/form-data) и текстов промпт и връща описание от Gemini AI.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<ImageAnalysisResult>> Analyze(
        [FromForm] IFormFile? image,
        [FromForm] string? prompt,
        [FromForm] string? model,
        CancellationToken cancellationToken = default)
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest(new ImageAnalysisResult
            {
                Success = false,
                ErrorMessage = "Моля, изберете и качете валидно изображение."
            });
        }

        using var memoryStream = new MemoryStream();
        await image.CopyToAsync(memoryStream, cancellationToken);

        var request = new ImageAnalysisRequest
        {
            ImageBytes = memoryStream.ToArray(),
            MimeType = string.IsNullOrWhiteSpace(image.ContentType) ? "image/png" : image.ContentType,
            FileName = image.FileName,
            Prompt = string.IsNullOrWhiteSpace(prompt)
                ? "Опиши подробно какво се вижда на това изображение на български език. Посочи основните обекти, цветове, текст и контекст."
                : prompt,
            ModelName = string.IsNullOrWhiteSpace(model) ? "gemini-2.5-flash" : model
        };

        _logger.LogInformation("Анализиране на изображение '{FileName}' ({Size} байта) с модел {Model}",
            request.FileName, request.ImageBytes.Length, request.ModelName);

        var result = await _visionService.AnalyzeImageAsync(request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("compare")]
    public async Task<ActionResult<ImageAnalysisResult>> Compare(
        [FromForm] IFormFile? image1,
        [FromForm] IFormFile? image2,
        [FromForm] string? prompt,
        [FromForm] string? model,
        CancellationToken cancellationToken = default)
    {
        if (image1 == null || image1.Length == 0 ||
            image2 == null || image2.Length == 0)
        {
            return BadRequest(new
            {
                Success = false,
                ErrorMessage = "Моля, изберете две валидни изображения."
            });
        }

        var allowedTypes = new[]
        {
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/gif"
    };

        if (!allowedTypes.Contains(image1.ContentType.ToLowerInvariant()) ||
            !allowedTypes.Contains(image2.ContentType.ToLowerInvariant()))
        {
            return BadRequest(new
            {
                Success = false,
                ErrorMessage = "Поддържат се PNG, JPEG, WEBP и GIF изображения."
            });
        }

        using var memoryStream1 = new MemoryStream();
        using var memoryStream2 = new MemoryStream();

        await image1.CopyToAsync(memoryStream1, cancellationToken);
        await image2.CopyToAsync(memoryStream2, cancellationToken);

        var request = new ImageComparisonRequest
        {
            Image1Bytes = memoryStream1.ToArray(),
            Image2Bytes = memoryStream2.ToArray(),

            MimeType1 = image1.ContentType,
            MimeType2 = image2.ContentType,

            FileName1 = image1.FileName,
            FileName2 = image2.FileName,

            Prompt = string.IsNullOrWhiteSpace(prompt)
                ? "Сравни двете изображения подробно. Посочи основните прилики, разлики, обекти, цветове, текст и промени. Отговори на български език."
                : prompt,

            ModelName = string.IsNullOrWhiteSpace(model)
                ? "gemini-2.5-flash"
                : model
        };

        _logger.LogInformation(
            "Сравняване на изображения '{Image1}' и '{Image2}' с модел {Model}",
            request.FileName1,
            request.FileName2,
            request.ModelName);

        var result = await _visionService.CompareImagesAsync(
            request, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Връща статус на услугата за проверка на работоспособността.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "Gemini Multimodal Vision API",
            Timestamp = DateTime.UtcNow
        });
    }
}
