#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Non-live witness only: no Process.Start, fixture worker, game reference or diagnostic API.
internal static class D01Baseline
{
    private sealed class GatedReader : TextReader
    {
        internal const string Text = "{\"Boundary\":\"iobs_current_resume_diagnostic\",\"Stage\":\"finalization-return\",\"Detail\":\"MortalJointCustodyCaptureCompletedBeforePreparation\"}\r\n";
        private int _offset;
        internal readonly TaskCompletionSource<bool> FirstRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> End = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (_offset < Text.Length)
            {
                var count = Math.Min(buffer.Length, Text.Length - _offset);
                Text.AsMemory(_offset, count).CopyTo(buffer);
                _offset += count;
                FirstRead.TrySetResult(true);
                return count;
            }
            await End.Task.WaitAsync(cancellationToken);
            return 0;
        }
        public override Task<int> ReadAsync(char[] buffer, int index, int count)
            => ReadAsync(buffer.AsMemory(index, count)).AsTask();
        public override int Read(char[] buffer, int index, int count)
            => ReadAsync(buffer.AsMemory(index, count)).AsTask().GetAwaiter().GetResult();
    }

    private static async Task<int> Main()
    {
        using var reader = new GatedReader();
        // Exact old parent API; the available first chunk is not a completed result.
        var raw = reader.ReadToEndAsync();
        bool pendingBeforeEof = false, rawEqual = false, joined = false;
        string? failure = null;
        try
        {
            await reader.FirstRead.Task.WaitAsync(TimeSpan.FromSeconds(3));
            pendingBeforeEof = !raw.IsCompleted;
        }
        catch (Exception error) { failure = error.GetType().Name + ": " + error.Message; }
        finally
        {
            reader.End.TrySetResult(true);
            try { rawEqual = await raw.WaitAsync(TimeSpan.FromSeconds(3)) == GatedReader.Text; joined = true; }
            catch (Exception error) { failure ??= error.GetType().Name + ": " + error.Message; }
        }
        var expectedGap = pendingBeforeEof && rawEqual && joined && failure is null;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Kind = "D01_SOURCE_BOUND_NONLIVE_EOF_WITNESS", ExpectedMissingLiveDelivery = expectedGap,
            FirstChunkRead = reader.FirstRead.Task.IsCompletedSuccessfully, PendingBeforeEof = pendingBeforeEof,
            RawDecodedTextExact = rawEqual, EofReleasedAndJoined = joined, Failure = failure,
            FixtureSubprocessTargets = 0, GameProcesses = 0, DiagnosticRequests = 0,
            Assertion = "Existing ReadToEndAsync has no consumable event result before EOF"
        }));
        return expectedGap ? 7 : 2; // Known expected bounded feature gap, never product RED.
    }
}
