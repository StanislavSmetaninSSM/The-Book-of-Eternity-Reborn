using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;

// Each invocation is a fresh process beneath the unchanged independent guardian.
internal static class OwnedTerminalScenarioDriver
{
    internal static async Task<int> RunAsync(string mode, string package, string output)
    {
        var result = new Dictionary<string, object?>();
        IOwnedTerminalSession? session = null;
        try
        {
            session = await OwnedTerminalSessionFactory.StartNeutralAsync(package, output, CancellationToken.None);
            var text = new StringBuilder();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var decoder = Encoding.UTF8.GetDecoder();
            async Task Until(string marker)
            {
                var buffer = new byte[4096];
                var chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
                while (!text.ToString().Contains(marker, StringComparison.Ordinal))
                {
                    var n = await session.OutputReader.ReadAsync(buffer, deadline.Token);
                    if (n == 0) throw new EndOfStreamException("Fixture output ended before required observation.");
                    var count = decoder.GetChars(buffer, 0, n, chars, 0, false); text.Append(chars, 0, count);
                }
            }
            async Task Write(string value) => await session.InputWriter.WriteAsync(Encoding.UTF8.GetBytes(value), deadline.Token);
            await Until("TTY_READY owned=1");
            await Write("one Ж😀\n"); await Until("RESULT1:one Ж😀");
            await Write("two\n"); await Until("RESULT2:two"); result["TwoInputs"] = true;
            await session.ResizeAsync(new(93, 31), deadline.Token); await Until("RESIZE 31x93"); result["Resize"] = true;
            await Write("canonical\n"); await Until("CANONICAL_READY");
            await Write("\u0004"); await Until("CANONICAL_EOF");
            result["EofStillAlive"] = !session.RootExited.IsCompleted;
            var stop = await session.StopAndObserveAsync(deadline.Token); result["StopState"] = stop.State.ToString();
            while (await session.OutputReader.ReadAsync(new byte[1024], deadline.Token) != 0) { }
            await session.DisposeAsync(); session = null;
            result["Transcript"] = text.ToString();
            return 0;
        }
        catch (Exception ex)
        {
            result["Failure"] = ex.GetType().Name + ": " + ex.Message;
            if (session != null) try { result["Cleanup"] = await session.StopAndObserveAsync(CancellationToken.None); } catch { }
            return 1;
        }
        finally { await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(result)); }
    }
}
