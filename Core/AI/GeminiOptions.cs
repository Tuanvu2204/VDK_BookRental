using System.ComponentModel.DataAnnotations;

namespace VDK_BookRental.Core.AI;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = "gemini-2.5-flash";

    [Required]
    [Url]
    public string BaseUrl { get; set; } =
        "https://generativelanguage.googleapis.com/";

    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 45;

    [Range(128, 8192)]
    public int MaxOutputTokens { get; set; } = 800;

    [Range(1, 500)]
    public int MaxCatalogBooks { get; set; } = 200;

    [Range(5, 600)]
    public int CatalogCacheSeconds { get; set; } = 30;

    [Range(0, 20)]
    public int MaxHistoryMessages { get; set; } = 8;

    [RegularExpression(
        "^(minimal|low|medium|high)$",
        ErrorMessage = "ThinkingLevel không hợp lệ.")]
    public string ThinkingLevel { get; set; } = "low";
}