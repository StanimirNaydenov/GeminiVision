using Microsoft.AspNetCore.Mvc;

namespace GeminiVisionApp.Models
{
    public class ImageComparisonRequest
    {
        public byte[] Image1Bytes { get; set; } = Array.Empty<byte>();
        public byte[] Image2Bytes { get; set; } = Array.Empty<byte>();

        public string MimeType1 { get; set; } = "image/png";
        public string MimeType2 { get; set; } = "image/png";

        public string FileName1 { get; set; } = string.Empty;
        public string FileName2 { get; set; } = string.Empty;

        public string Prompt { get; set; } =
            "Сравни двете изображения и посочи основните прилики и разлики.";

        public string ModelName { get; set; } = "gemini-2.5-flash";
    }
}
