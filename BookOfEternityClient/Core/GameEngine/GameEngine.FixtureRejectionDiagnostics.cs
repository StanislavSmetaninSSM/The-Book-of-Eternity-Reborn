using BookOfEternityClient.Services;
using System.Text.Json;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
#if DEBUG
    /// <summary>Observes the existing protected-materialization refusal as fixture DATA; it grants no publication or rollback authority.</summary>
    /// <param name="errors">Already-computed validation issues, without a new validation pass.</param>
    /// <param name="mortalItem">Existing item refusal decision.</param>
    /// <param name="mortalLocation">Existing location refusal decision.</param>
    /// <param name="effect">Existing effect refusal decision.</param>
    /// <param name="resource">Existing resource refusal decision.</param>
    /// <param name="wound">Existing wound refusal decision.</param>
    /// <param name="source">Existing caller description.</param>
    /// <param name="attempt">Existing repair attempt.</param>
    /// <param name="rollbackAvailable">Capability observation, not proof of completed rollback.</param>
    /// <param name="nonce">Opt-in fixture nonce; only exact lowercase GUID N syntax is eligible.</param>
    /// <param name="sink">Captured diagnostic writer; its failures must not change the original refusal.</param>
    internal static void EmitProtectedMaterializationFixtureRejection(
        IReadOnlyList<ValidationIssue> errors, bool mortalItem, bool mortalLocation,
        bool effect, bool resource, bool wound, string source, int attempt,
        bool rollbackAvailable, string? nonce, TextWriter sink)
    {
        WriteFixtureRejection(nonce, sink, () => new
        {
            kind = "boe-game-loop-fixture-rejection", stage = "protected-materialization",
            nonce, pid = Environment.ProcessId, source, attempt, rollbackAvailable,
            flags = new { mortalItem, mortalLocation, effect, resource, wound },
            errors = errors.Select(issue => new
            {
                code = issue.Code, filePath = issue.FilePath, message = issue.Message,
                severity = issue.Severity.ToString(), category = issue.Category.ToString(),
                actor = issue.Actor, section = issue.Section, expected = issue.Expected,
                actual = issue.Actual, repairHint = issue.RepairHint
            }).ToArray()
        });
    }

    /// <summary>Observes the original caught canonical-refresh failure without replacing it or claiming completed rollback.</summary>
    /// <param name="failure">The original exception, including potentially hostile diagnostic getters.</param>
    /// <param name="source">Existing caller description.</param>
    /// <param name="rollbackAvailable">Capability observation only.</param>
    /// <param name="nonce">Opt-in fixture nonce; absent or malformed values must emit nothing.</param>
    /// <param name="sink">Captured writer; diagnostic failures must remain isolated.</param>
    internal static void EmitCanonicalRefreshFixtureRejection(Exception failure,
        string source, bool rollbackAvailable, string? nonce, TextWriter sink)
    {
        WriteFixtureRejection(nonce, sink, () => new
        {
            kind = "boe-game-loop-fixture-rejection", stage = "canonical-refresh-failed",
            nonce, pid = Environment.ProcessId, source, rollbackAvailable,
            exceptionType = failure.GetType().FullName, exception = failure.ToString()
        });
    }
    // Construct diagnostic DATA inside the protected boundary too: exception getters,
    // issue enumeration, serialization and the sink must not replace a refusal.
    private static void WriteFixtureRejection(string? nonce, TextWriter sink, Func<object> capture)
    {
        try
        {
            if (!Guid.TryParseExact(nonce, "N", out var parsed) || parsed.ToString("N") != nonce)
                return;
            sink.WriteLine("\n" + JsonSerializer.Serialize(capture()));
        }
        catch { /* Diagnostics never alter the original validation/publication outcome. */ }
    }
#endif
}
