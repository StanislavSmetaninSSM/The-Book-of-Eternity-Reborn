using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    // Diagnostic entry only: the original theory/57-target coverage is unchanged.
    [Fact]
    public Task GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss()
        => RunJointInterfaceActualProcessLossAsync(1, diagnostic: true);

    private sealed class JointInterfaceResumeDiagnostic(bool enabled, int cut, string root, string nonce)
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly int _pid = Environment.ProcessId;
        private readonly ulong _birthTick = enabled ? ReadJointInterfaceLinuxBirthTick(Environment.ProcessId) : 0;
        internal bool Enabled => enabled;
        internal string? OutputFailure { get; private set; }

        internal void Observe(string stage, string? detail = null)
        {
            if (!enabled) return;
            try
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    Boundary = "iobs_current_resume_diagnostic", Stage = stage,
                    Utc = DateTimeOffset.UtcNow, ElapsedMilliseconds = _clock.ElapsedMilliseconds,
                    Pid = _pid, BirthTick = _birthTick, Cut = cut, Root = root, Nonce = nonce,
                    Detail = detail is { Length: > 2000 } ? detail[..2000] : detail
                }));
                Console.Out.Flush();
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException)
            {
                // Observer I/O must not replace the operation's primary exception.
                // The original result carries this scalar failure and diagnostic1
                // additionally requires it to be absent before a positive outcome.
                OutputFailure ??= error.GetType().Name + ": " + error.Message;
            }
        }
    }

    private sealed class JointInterfaceResumeDiagnosticLogger(
        SpiritualLifecycleTestLogger retained, JointInterfaceResumeDiagnostic diagnostic) : ILogger<GameEngine>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => retained.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => retained.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter(state, exception);
            retained.Log(logLevel, eventId, state, exception, (_, _) => message);
            diagnostic.Observe("engine-log", logLevel + ": " + message);
            if (exception is not null)
                diagnostic.Observe("engine-log-exception", exception.GetType().FullName + ": " + exception.Message);
        }
    }
}
