using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GeminiVisionApp.Models;
using Microsoft.Extensions.Options;

namespace GeminiVisionApp.Services;

/// <summary>
/// Имплементация на услугата за мултимодален анализ на изображения чрез Google Gemini REST API.
/// </summary>
public class GeminiVisionService : IGeminiVisionService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiVisionService> _logger;

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/webp",
        "image/gif"
    };

    public GeminiVisionService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiVisionService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }


    public async Task<ImageAnalysisResult> AnalyzeImageAsync(
        ImageAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var stopwatch = Stopwatch.StartNew();

        // Валидация на изображението
        if (request.ImageBytes == null || request.ImageBytes.Length == 0)
        {
            return new ImageAnalysisResult
            {
                Success = false,
                ErrorMessage = "Липсват данни за изображението (празен файл)."
            };
        }

        // Валидация на MIME типа
        var mimeType = string.IsNullOrWhiteSpace(request.MimeType)
            ? "image/png"
            : request.MimeType.Trim();

        if (mimeType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            mimeType = "image/jpeg";
        }

        if (!SupportedMimeTypes.Contains(mimeType))
        {
            return new ImageAnalysisResult
            {
                Success = false,
                ErrorMessage = $"Неподдържан формат на изображението: {mimeType}. Поддържани са PNG, JPEG, WEBP и GIF."
            };
        }

        // Валидация на API ключа
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new ImageAnalysisResult
            {
                Success = false,
                ErrorMessage = "Не е конфигуриран Gemini API ключ."
            };
        }

        // Избор на модел
        var model = string.IsNullOrWhiteSpace(request.ModelName)
            ? _options.DefaultModel
            : request.ModelName.Trim();

        // Prompt за разпознаване на обекти

        var prompt = string.IsNullOrWhiteSpace(request.Prompt)
            ? @"Анализирай предоставеното изображение.

                Върни резултата единствено като валиден JSON обект.
                Задължително използвай точно следните имена на полетата
                на английски език:

                {
                  ""description"": ""Описание на изображението на български език"",
                  ""main_objects"": [
                    {
                      ""name"": ""Име на обекта"",
                      ""details"": ""Подробно описание на обекта"",
                      ""colors"": [""цвят 1"", ""цвят 2""]
                    }
                  ],
                  ""colors"": [""основен цвят 1"", ""основен цвят 2""],
                  ""inscriptions"": [
                    {
                      ""text"": ""Разпознат надпис"",
                      ""location"": ""Местоположение на надписа""
                    }
                  ],
                  ""context"": ""Контекст на изображението""
                }

                Правила:
                1. Всички стойности трябва да бъдат на български език.
                2. Имената на JSON полетата трябва да останат точно както са зададени.
                3. main_objects задължително трябва да бъде масив.
                4. Не променяй структурата на JSON обекта.
                5. Не добавяй Markdown, коментари или текст извън JSON.
                6. Ако няма разпознати обекти, върни празен масив [] за main_objects."
            : request.Prompt.Trim();


        var base64Image = Convert.ToBase64String(request.ImageBytes);

        // Формиране на JSON payload за Gemini generateContent
        var payload = new
        {
            contents = new[]
            {
            new
            {
                parts = new object[]
                {
                    new { text = prompt },
                    new
                    {
                        inline_data = new
                        {
                            mime_type = mimeType,
                            data = base64Image
                        }
                    }
                }
            }
        },

            // Конфигурация за структуриран JSON отговор
            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        var jsonPayload = JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

        var baseUrl = _options.BaseUrl.TrimEnd('/');

        var endpointUrl =
            $"{baseUrl}/{model}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";

        try
        {
            using var content = new StringContent(
                jsonPayload,
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(
                endpointUrl,
                content,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            stopwatch.Stop();

            // Проверка за грешка от Gemini API
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Gemini API върна грешка {StatusCode}: {Body}",
                    response.StatusCode,
                    responseBody);

                return new ImageAnalysisResult
                {
                    Success = false,
                    ModelUsed = model,
                    PromptUsed = prompt,
                    MimeType = mimeType,
                    ImageSizeBytes = request.ImageBytes.Length,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    ErrorMessage =
                        $"Грешка от Gemini API ({(int)response.StatusCode}): {ExtractApiError(responseBody)}"
                };
            }




            var extractedText = ExtractTextFromResponse(responseBody);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return new ImageAnalysisResult
                {
                    Success = false,
                    ModelUsed = model,
                    PromptUsed = prompt,
                    MimeType = mimeType,
                    ImageSizeBytes = request.ImageBytes.Length,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = "Gemini не върна текстово съдържание."
                };
            }

            try
            {
                using var jsonDocument = JsonDocument.Parse(extractedText);

                if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException(
                        "Полученият резултат не е JSON обект.");
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Невалиден JSON от Gemini: {Text}",
                    extractedText);

                return new ImageAnalysisResult
                {
                    Success = false,
                    ModelUsed = model,
                    PromptUsed = prompt,
                    MimeType = mimeType,
                    ImageSizeBytes = request.ImageBytes.Length,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = "Полученият отговор не е валиден JSON обект."
                };
            }


            // Успешен резултат
            return new ImageAnalysisResult
            {
                Success = true,
                Description = extractedText,
                ModelUsed = model,
                PromptUsed = prompt,
                MimeType = mimeType,
                ImageSizeBytes = request.ImageBytes.Length,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger.LogInformation(
                "Анализът на изображението беше отменен.");

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Изключение при комуникация с Gemini API.");

            return new ImageAnalysisResult
            {
                Success = false,
                ModelUsed = model,
                PromptUsed = prompt,
                MimeType = mimeType,
                ImageSizeBytes = request.ImageBytes.Length,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ErrorMessage = $"Грешка при свързване с Gemini API: {ex.Message}"
            };
        }
    }

        public async Task<ImageAnalysisResult> CompareImagesAsync(
    ImageComparisonRequest request,
    CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.Image1Bytes.Length == 0 || request.Image2Bytes.Length == 0)
        {
            return new ImageAnalysisResult
            {
                Success = false,
                ErrorMessage = "Трябва да бъдат предоставени две изображения."
            };
        }

        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new ImageAnalysisResult
            {
                Success = false,
                ErrorMessage = "Не е конфигуриран Gemini API ключ."
            };
        }

        var model = string.IsNullOrWhiteSpace(request.ModelName)
            ? _options.DefaultModel
            : request.ModelName.Trim();

        var prompt = string.IsNullOrWhiteSpace(request.Prompt)
            ? "Сравни двете изображения и опиши подробно приликите и разликите между тях."
            : request.Prompt.Trim();

        var payload = new
        {
            contents = new[]
            {
            new
            {
                parts = new object[]
                {
                    new { text = prompt },

                    new
                    {
                        inline_data = new
                        {
                            mime_type = request.MimeType1,
                            data = Convert.ToBase64String(request.Image1Bytes)
                        }
                    },

                    new
                    {
                        inline_data = new
                        {
                            mime_type = request.MimeType2,
                            data = Convert.ToBase64String(request.Image2Bytes)
                        }
                    }
                }
            }
        }
        };

        var jsonPayload = JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

        var baseUrl = _options.BaseUrl.TrimEnd('/');

        var endpointUrl =
            $"{baseUrl}/{model}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";

        try
        {
            using var content = new StringContent(
                jsonPayload,
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(
                endpointUrl,
                content,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return new ImageAnalysisResult
                {
                    Success = false,
                    ModelUsed = model,
                    PromptUsed = prompt,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    ErrorMessage =
                        $"Грешка от Gemini API ({(int)response.StatusCode}): {ExtractApiError(responseBody)}"
                };
            }

            var extractedText = ExtractTextFromResponse(responseBody);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return new ImageAnalysisResult
                {
                    Success = false,
                    ModelUsed = model,
                    PromptUsed = prompt,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = "Gemini не върна резултат от сравнението."
                };
            }

            return new ImageAnalysisResult
            {
                Success = true,
                Description = extractedText,
                ModelUsed = model,
                PromptUsed = prompt,
                MimeType = "image comparison",
                ImageSizeBytes = request.Image1Bytes.Length + request.Image2Bytes.Length,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Грешка при сравняване на две изображения.");

            return new ImageAnalysisResult
            {
                Success = false,
                ModelUsed = model,
                PromptUsed = prompt,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ErrorMessage = $"Грешка при сравняване на изображенията: {ex.Message}"
            };
        }
    }
    



    public static string ExtractTextFromResponse(string jsonResponse)
    {
        if (string.IsNullOrWhiteSpace(jsonResponse))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];

                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.ValueKind == JsonValueKind.Array)
                {
                    var textParts = new List<string>();

                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var textProp) &&
                            textProp.ValueKind == JsonValueKind.String)
                        {
                            var text = textProp.GetString();

                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                textParts.Add(text);
                            }
                        }
                    }

                    return string.Join("\n", textParts).Trim();
                }
            }
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }


    private static string ExtractApiError(string jsonResponse)
    {
        if (string.IsNullOrWhiteSpace(jsonResponse))
            return "Празен отговор от сървъра.";

        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msg))
            {
                return msg.GetString() ?? jsonResponse;
            }
        }
        catch
        {
            // Игнориране на грешка при парсване на грешката
        }

        return jsonResponse;
    }
}
