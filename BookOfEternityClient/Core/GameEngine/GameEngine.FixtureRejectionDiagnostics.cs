using BookOfEternityClient.Services;

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
        // Causal RED stub: existing production behavior emits no rejection observation.
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
        // Causal RED stub: no serialization, filesystem access, or authority change.
    }
#endif
}
