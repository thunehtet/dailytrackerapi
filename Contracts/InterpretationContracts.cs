using System.ComponentModel.DataAnnotations;

namespace daily_tracker_api.Contracts;

public sealed record InterpretTextRequest(
    [property: Required, StringLength(1000, MinimumLength = 1)] string Text,
    [property: Range(-840, 840)] int UtcOffsetMinutes = 0);

public sealed class InterpretVoiceRequest
{
    [Required]
    public required IFormFile Audio { get; init; }

    [Range(-840, 840)]
    public int UtcOffsetMinutes { get; init; }
}

public sealed record ExpenseProposal(
    string Title,
    string Category,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt,
    double Confidence,
    string InputType,
    string SourceText,
    bool Confirmed = false);
