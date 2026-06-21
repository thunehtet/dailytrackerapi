using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using daily_tracker_api.Contracts;
using daily_tracker_api.Options;
using Microsoft.Extensions.Options;

namespace daily_tracker_api.Services;

public sealed class OpenAiExpenseInterpreter(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options) : IExpenseInterpreter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly OpenAiOptions _options = options.Value;

    public async Task<ExpenseProposal> InterpretTextAsync(
        string text,
        DateTimeOffset now,
        string inputType,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _options.TextModel,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = $"""
                        Extract one personal expense from the user's statement.
                        Current local date and time: {now:O}.
                        Use only these categories: Food, Transport, Shopping, Bills,
                        Entertainment, Health, Other. Never invent an amount. If the
                        currency is omitted use USD. Resolve relative dates using the
                        supplied local date and time. Return only the requested schema.
                        """
                },
                new { role = "user", content = text }
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "expense_proposal",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string" },
                            category = new
                            {
                                type = "string",
                                @enum = new[]
                                {
                                    "Food", "Transport", "Shopping", "Bills",
                                    "Entertainment", "Health", "Other"
                                }
                            },
                            amount = new { type = "number", minimum = 0 },
                            currency = new { type = "string", minLength = 3, maxLength = 3 },
                            occurredAt = new { type = "string" },
                            confidence = new { type = "number", minimum = 0, maximum = 1 }
                        },
                        required = new[]
                        {
                            "title", "category", "amount", "currency",
                            "occurredAt", "confidence"
                        },
                        additionalProperties = false
                    }
                }
            }
        };

        using var response = await httpClient.PostAsJsonAsync(
            "chat/completions",
            request,
            JsonOptions,
            cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var requestId = GetRequestId(response);
        if (!response.IsSuccessStatusCode)
        {
            throw new OpenAiException(
                $"OpenAI returned status {(int)response.StatusCode}. RequestId: {requestId}.");
        }

        try
        {
            using var envelope = JsonDocument.Parse(responseBody);
            var content = envelope.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new OpenAiException(
                    $"OpenAI returned an empty interpretation. RequestId: {requestId}.");
            }

            var parsed = JsonSerializer.Deserialize<ParsedExpense>(content, JsonOptions)
                ?? throw new OpenAiException(
                    $"OpenAI returned an invalid interpretation. RequestId: {requestId}.");
            var occurredAt = DateTimeOffset.TryParse(
                parsed.OccurredAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedDate)
                ? parsedDate
                : now;

            return new ExpenseProposal(
                parsed.Title.Trim(),
                parsed.Category,
                parsed.Amount,
                parsed.Currency.ToUpperInvariant(),
                occurredAt,
                Math.Clamp(parsed.Confidence, 0, 1),
                inputType,
                text,
                Confirmed: false);
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new OpenAiException(
                $"OpenAI returned malformed interpretation JSON. RequestId: {requestId}.",
                exception);
        }
    }

    public async Task<string> TranscribeAsync(
        Stream audio,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(_options.TranscriptionModel), "model");
        var audioContent = new StreamContent(audio);
        audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(audioContent, "file", fileName);

        using var response = await httpClient.PostAsync(
            "audio/transcriptions",
            form,
            cancellationToken);
        var requestId = GetRequestId(response);
        if (!response.IsSuccessStatusCode)
        {
            throw new OpenAiException(
                $"OpenAI transcription returned status {(int)response.StatusCode}. RequestId: {requestId}.");
        }

        try
        {
            using var body = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            var transcription = body.RootElement.GetProperty("text").GetString();
            return string.IsNullOrWhiteSpace(transcription)
                ? throw new OpenAiException("OpenAI returned an empty transcription.")
                : transcription.Trim();
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new OpenAiException(
                $"OpenAI returned malformed transcription JSON. RequestId: {requestId}.",
                exception);
        }
    }

    private static string GetRequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-request-id", out var values)
            ? values.FirstOrDefault() ?? "unavailable"
            : "unavailable";

    private sealed record ParsedExpense(
        string Title,
        string Category,
        decimal Amount,
        string Currency,
        string OccurredAt,
        double Confidence);
}
