using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Writes complete component messages while deferring diagnostic failures until the actual outcome is known.
/// </summary>
/// <typeparam name="TCategory">
/// The component type whose logging interface is implemented.
/// </typeparam>
/// <param name="writer">
/// The caller-owned synchronized external JSONL writer, kept open through component and closing operations.
/// </param>
/// <param name="capture">
/// The existing deferred diagnostic ledger merged after the actual component or owner outcome.
/// </param>
/// <param name="boundary">
/// The record boundary identifying these complete component messages in the caller's diagnostic stream.
/// </param>
internal sealed class JointComponentDiagnosticLogger<TCategory>(
    TextWriter writer, JointCleanupProfileDiagnosticCapture capture, string boundary) : ILogger<TCategory>
{
    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        try
        {
            writer.WriteLine(JsonSerializer.Serialize(new { Boundary = boundary,
                Level = logLevel.ToString(), EventId = eventId.Id,
                Message = formatter(state, exception), Exception = exception?.ToString() }));
        }
        catch (Exception error) { capture.RecordDiagnosticFailure(error, "engine-log"); }
    }
}
