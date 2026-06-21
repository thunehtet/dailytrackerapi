namespace daily_tracker_api.Services;

public sealed class OpenAiException(string message, Exception? innerException = null)
    : Exception(message, innerException);
