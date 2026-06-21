using System.ComponentModel.DataAnnotations;

namespace daily_tracker_api.Options;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string TextModel { get; set; } = "gpt-4o-mini";

    [Required]
    public string TranscriptionModel { get; set; } = "gpt-4o-mini-transcribe";
}
