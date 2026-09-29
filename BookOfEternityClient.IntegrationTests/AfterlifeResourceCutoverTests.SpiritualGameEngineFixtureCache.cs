using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private static readonly Lazy<Task<PreparedFixtureTree>> SpiritualGameEngineTemplate =
        new(PrepareSpiritualGameEngineTemplateAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly HashSet<string> SpiritualGameEngineUnusedBaselinePaths = new(StringComparer.Ordinal)
    {
        "game_state/afterlife/afterlife_active_threats.json",
        "game_state/afterlife/afterlife_chronicles.json",
        "game_state/afterlife/afterlife_global_flags.json",
        "game_state/afterlife/afterlife_story_outline.json",
        "game_state/afterlife/entity_profiles/guardian_mirror.json",
        "game_state/afterlife/entity_profiles/shining_senator_mirel.json",
        "game_state/chaos_sea/guardian_politics.json",
        "game_state/shining_abode/faction_chronicles.json",
        "lore/current_world/cultures.json",
        "lore/current_world/geography.json",
        "lore/current_world/history.json",
        "lore/current_world/threats.json",
        "lore/current_world/world_directives_draft_example.md",
        "lore/current_world/world_setting.json"
    };

    /// <summary>
    /// Copies the repository baseline into an owned Chaos Sea fixture while omitting unused legacy state and Mortal realm lore.
    /// </summary>
    /// <param name="gameSessionPath">
    /// Fresh mutable game-session destination; repository source files are read without modification.
    /// </param>
    private static void CopySpiritualGameEngineChaosSeaBaseline(string gameSessionPath)
    {
        foreach (var source in Directory.EnumerateFiles(TestRepoPaths.BaseSessionRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(TestRepoPaths.BaseSessionRoot, source);
            if (SpiritualGameEngineUnusedBaselinePaths.Contains(relativePath.Replace(Path.DirectorySeparatorChar, '/')))
                continue;
            var destination = Path.Combine(gameSessionPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
        }
    }

    /// <summary>
    /// Captures only the unsigned game-session tree after refusing any existing original snapshot or private spiritual transport.
    /// </summary>
    /// <param name="fileSystem">
    /// Hookless temporary filesystem containing the complete prepared baseline; its runtime tree is not captured.
    /// </param>
    /// <returns>
    /// Detached prepared bytes with no request, signed snapshot, decision checkpoint, command or repair artifacts.
    /// </returns>
    private static PreparedFixtureTree CaptureUnsignedSpiritualGameEngineTemplate(FileSystemManager fileSystem)
    {
        foreach (var path in new[]
        {
            "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
            PendingTurnSnapshotAuthority.AuthorityPath, SpiritualWoundCaptureCheckpointState.StatePath,
            SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath,
            "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json"
        })
            Assert.False(fileSystem.FileExists(path), path);
        Assert.False(Directory.Exists(fileSystem.ResolvePath("game_state/control/pending_turn_snapshot")));
        return PreparedFixtureTree.Capture(fileSystem.GameSessionPath);
    }
}
