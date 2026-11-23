using GeminiVisionApp.Models;

namespace GeminiVisionApp.Services;

/// <summary>
/// Интерфейс за услуга, осъществяваща мултимодална комуникация с Google Gemini AI.
/// </summary>
public interface IGeminiVisionService
{
    Task<ImageAnalysisResult> AnalyzeImageAsync(
         ImageAnalysisRequest request,
         CancellationToken cancellationToken = default);

    Task<ImageAnalysisResult> CompareImagesAsync(
        ImageComparisonRequest request,
        CancellationToken cancellationToken = default);
}
