using daily_tracker_api.Contracts;

namespace daily_tracker_api.Services;

public interface IExpenseInterpreter
{
    Task<ExpenseProposal> InterpretTextAsync(
        string text,
        DateTimeOffset now,
        string inputType,
        CancellationToken cancellationToken);

    Task<string> TranscribeAsync(
        Stream audio,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);
}
