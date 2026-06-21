using daily_tracker_api.Contracts;
using daily_tracker_api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace daily_tracker_api.Controllers;

[ApiController]
[Route("api/v1/interpret")]
[EnableRateLimiting("interpretation")]
public sealed class InterpretationController(
    IExpenseInterpreter interpreter,
    ILogger<InterpretationController> logger) : ControllerBase
{
    private const long MaxAudioBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedAudioTypes = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg", "audio/mp4", "audio/x-m4a", "audio/m4a", "audio/wav",
        "audio/x-wav", "audio/webm", "audio/ogg", "audio/flac"
    };

    [HttpPost("text")]
    [ProducesResponseType<ExpenseProposal>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExpenseProposal>> InterpretText(
        InterpretTextRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [nameof(request.Text)] = ["Text cannot be empty."]
                }));
        }

        return await InterpretSafely(
            request.Text.Trim(),
            request.UtcOffsetMinutes,
            "text",
            cancellationToken);
    }

    [HttpPost("voice")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAudioBytes)]
    [ProducesResponseType<ExpenseProposal>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExpenseProposal>> InterpretVoice(
        [FromForm] InterpretVoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Audio.Length is <= 0 or > MaxAudioBytes)
        {
            return BadRequest(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [nameof(request.Audio)] = ["Audio must be between 1 byte and 10 MB."]
                }));
        }

        if (!AllowedAudioTypes.Contains(request.Audio.ContentType))
        {
            return BadRequest(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [nameof(request.Audio)] = ["Unsupported audio format."]
                }));
        }

        try
        {
            await using var stream = request.Audio.OpenReadStream();
            var transcription = await interpreter.TranscribeAsync(
                stream,
                Path.GetFileName(request.Audio.FileName),
                request.Audio.ContentType,
                cancellationToken);
            return await InterpretSafely(
                transcription,
                request.UtcOffsetMinutes,
                "voice",
                cancellationToken);
        }
        catch (OpenAiException exception)
        {
            logger.LogWarning(exception, "Voice interpretation provider failed.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Voice interpretation is temporarily unavailable.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException &&
            !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Voice interpretation request failed.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Voice interpretation is temporarily unavailable.");
        }
    }

    private async Task<ActionResult<ExpenseProposal>> InterpretSafely(
        string text,
        int utcOffsetMinutes,
        string inputType,
        CancellationToken cancellationToken)
    {
        try
        {
            var localNow = DateTimeOffset.UtcNow.ToOffset(
                TimeSpan.FromMinutes(utcOffsetMinutes));
            return Ok(await interpreter.InterpretTextAsync(
                text,
                localNow,
                inputType,
                cancellationToken));
        }
        catch (OpenAiException exception)
        {
            logger.LogWarning(exception, "Text interpretation provider failed.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Expense interpretation is temporarily unavailable.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException &&
            !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Text interpretation request failed.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Expense interpretation is temporarily unavailable.");
        }
    }
}
