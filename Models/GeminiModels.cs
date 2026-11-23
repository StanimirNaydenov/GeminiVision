namespace GeminiVisionApp.Models;

/// <summary>
/// Модел за входяща заявка за мултимодален анализ на изображение.
/// </summary>
public class ImageAnalysisRequest
{
    public string Prompt { get; set; } = "Опиши подробно какво се вижда на това изображение на български език.";
    public string ModelName { get; set; } = "gemini-2.5-flash";
    public string ModelName1 { get; set; } = "gemini-3.1-pro-preview";
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
    public string MimeType { get; set; } = "image/png";
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Модел за резултата от мултимодалния анализ чрез Google Gemini API.
/// </summary>
public class ImageAnalysisResult
{
    public bool Success { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public string PromptUsed { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public int ImageSizeBytes { get; set; }
    public string MimeType { get; set; } = string.Empty;
}

/// <summary>
/// Конфигурационни настройки за Google AI Studio (Gemini API).
/// </summary>
public class GeminiOptions
{
    public const string SectionName = "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = "gemini-2.5-flash";
    public string ProModel { get; set; } = "gemini-3.1-pro-preview";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";
}
