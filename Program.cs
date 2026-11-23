using GeminiVisionApp.Models;
using GeminiVisionApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Конфигуриране на настройките за Google Gemini API
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

// Регистриране на HttpClient и GeminiVisionService
builder.Services.AddHttpClient<IGeminiVisionService, GeminiVisionService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();

public partial class Program { }
