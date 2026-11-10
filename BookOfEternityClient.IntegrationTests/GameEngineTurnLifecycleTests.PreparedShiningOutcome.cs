using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("idle", true)]
    [InlineData("idle", false)]
    [InlineData("clear_helper", true)]
    [InlineData("clear_helper", false)]
    public async Task PreparedShiningCleanup_OriginalBoundaryPreservesPublicationDecision(string mode, bool unknown)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var cut = new CleanupPublicationCut();
        FileSystemManager? files = null;
        byte[]? beforeMutation = null;
        byte[]? knownJournal = null;
        byte[]? knownPublished = null;
        Dictionary<string, bool>? absenceAtCut = null;
        var knownCuts = 0;
        var known = new InvalidOperationException("known prepared-Shining cleanup publication failure");
        var targetRelative = ShiningAbodeState.StatePath;
        var absentPaths = new[] { "ready/turn_complete.json", "ready/turn_error.json", "input/turn_request.json",
            "game_state/control/incarnation_trigger.json", "game_state/control/pending_turn_snapshot.json" };
        bool IsRemoval(string path) => path == files!.ResolvePath(targetRelative) &&
            JsonNode.Parse(File.ReadAllText(path))!.AsObject()["preparedIncarnationPackage"] == null;
        void ValidateCut()
        {
            Assert.NotNull(beforeMutation);
            var prior = JsonNode.Parse(Encoding.UTF8.GetString(beforeMutation!).TrimStart('\uFEFF'))!.AsObject();
            var package = Assert.IsType<JsonObject>(prior["preparedIncarnationPackage"]);
            Assert.Null(ShiningAbodeState.ValidatePreparedIncarnationPackageForBootstrap(package));
            absenceAtCut = absentPaths.ToDictionary(x => x, x => !File.Exists(files!.ResolvePath(x)) && !Directory.Exists(files.ResolvePath(x)), StringComparer.Ordinal);
            Assert.All(absenceAtCut.Values, value => Assert.True(value));
        }
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (cut.Armed && cut.Cuts == 0 && knownCuts == 0 && path == targetRelative)
                    beforeMutation = File.ReadAllBytes(files!.ResolvePath(path));
                return Task.CompletedTask;
            },
            LocalPublicationObserver = (phase, index) =>
            {
                if (unknown) { cut.Hooks.LocalPublicationObserver!(phase, index); return; }
                if (!cut.Armed || knownCuts != 0 || phase != TrustedLocalPublicationPhase.MemberPublished) return;
                var bytes = File.ReadAllBytes(cut.JournalPath);
                using var journal = CleanupPublicationCut.Metadata(bytes);
                var member = journal.RootElement.GetProperty("Members")[index];
                var path = member.GetProperty("Path").GetString()!;
                if (!IsRemoval(path)) return;
                ValidateCut(); Assert.Equal(0, index); Assert.False(journal.RootElement.GetProperty("Committed").GetBoolean());
                knownCuts++; knownJournal = bytes; knownPublished = File.ReadAllBytes(path);
                throw known;
            },
            LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
            BeforeCanonicalMutationBoundaryAsync = cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
            AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
            SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync,
            BeforeCanonicalWriteLockOpenAsync = cut.Hooks.BeforeCanonicalWriteLockOpenAsync
        };
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
        cut.Attach(files);
        var packageRoot = CreatePreparedShiningOutcomePackage();
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["preparedIncarnationPackage"] = packageRoot.DeepClone();
        var soul = CreateLifecycleSoulState("Prepared Shining storage fixture", "Mortal World");
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(files, new AfterlifeOwnerResourceAcceptedState(
            Profiles: CreatePlayerSoulProfiles("Prepared Shining storage fixture", "Chaos Sea"),
            SpiritualConflict: AfterlifeSpiritualConflictState.CreateDefaultRoot(), SoulState: soul,
            ShiningAbode: shining, Guardians: CreateEmptyGuardiansState()));
        foreach (var path in absentPaths) files.DeleteFile(path);
        var logger = new PreparedShiningOutcomeLogger();
        var input = new ProgressionNoInput();
        var engine = CreateGameEngine(input, s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; }, fileSystem: files, logger: logger);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        Assert.Equal("Mortal World", GetPrivateField<StateManager>(engine, "_stateManager").CurrentState.CurrentRealm);
        var actualPackage = JsonNode.Parse(File.ReadAllText(files.ResolvePath(targetRelative)))!["preparedIncarnationPackage"]!.AsObject();
        Assert.True(JsonNode.DeepEquals(packageRoot, actualPackage));
        Assert.Null(ShiningAbodeState.ValidatePreparedIncarnationPackageForBootstrap(actualPackage));
        cut.Select = (path, member) => member.GetProperty("After").GetProperty("Exists").GetBoolean() && IsRemoval(path);
        cut.BeforeCut = ValidateCut;
        cut.Armed = true;
        // clear_helper is deliberately the original helper, not a fabricated successful bootstrap turn.
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine,
            mode == "idle" ? "NormalizeRuntimeUiArtifactsAsync" : "ClearPreparedShiningPackageAfterBootstrapAsync"));
        var after = CleanupPublicationCut.ReadOptional(files.ResolvePath(targetRelative));
        var retainedJournal = CleanupPublicationCut.ReadOptional(cut.JournalPath);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { mode, unknown, beforeMutation, knownCuts, knownJournal,
            knownPublished, absenceAtCut, after, retainedJournal, Failure = failure?.ToString(), input.Reads,
            Logged = logger.Errors.Select(x => x.ToString()).ToArray(), Cut = cut.Evidence() }));
        input.AssertCompleted();
        if (unknown) { cut.AssertReachedAndStopped(); Assert.Same(cut.OriginalUncertainty, failure); }
        else
        {
            Assert.Equal(1, knownCuts); Assert.NotNull(beforeMutation); Assert.NotEqual(beforeMutation, knownPublished);
            Assert.Equal(beforeMutation, after); Assert.Null(retainedJournal); Assert.Null(failure);
            Assert.Contains(logger.Errors, x => ReferenceEquals(x, known));
        }
    }

    private sealed class PreparedShiningOutcomeLogger : ILogger<GameEngine>
    {
        internal List<Exception> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        { if (error != null) Errors.Add(error); }
    }

    private static JsonObject CreatePreparedShiningOutcomePackage()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": {
            "experience": 380,
            "tier": 3
          },
          "lightSparks": 100,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_dawn",
              "originType": "player_founded",
              "hallId": "hall_dawn",
              "charter": {
                "factionName": "Хор Рассвета",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Поют утренний свет."
              },
              "leadership": {
                "headActorType": "player_soul",
                "headActorId": "player_soul",
                "leadershipState": "secure"
              },
              "baseStrength": 35,
              "factionStrength": 70,
              "investCountThisAscension": 0,
              "projects": [
                {
                  "projectId": "project_social",
                  "displayName": "Песнь согласия",
                  "summary": "Укрепляет связи.",
                  "toneTags": ["radiant"],
                  "targetFactionIds": [],
                  "projectArchetype": "accord",
                  "outputEffectFamily": "social",
                  "tier": 2,
                  "status": "completed",
                  "isSupported": true,
                  "strengthReward": 12
                },
                {
                  "projectId": "project_memory",
                  "displayName": "Хор памяти",
                  "summary": "Хранит отзвуки.",
                  "toneTags": ["memory"],
                  "targetFactionIds": [],
                  "projectArchetype": "remembrance",
                  "outputEffectFamily": "memory",
                  "tier": 2,
                  "status": "completed",
                  "isSupported": true,
                  "strengthReward": 12
                },
                {
                  "projectId": "project_passage",
                  "displayName": "Тропа возвращения",
                  "summary": "Зовёт спутников.",
                  "toneTags": ["passage"],
                  "targetFactionIds": [],
                  "projectArchetype": "passage",
                  "outputEffectFamily": "route",
                  "tier": 1,
                  "status": "completed",
                  "isSupported": true,
                  "strengthReward": 8
                }
              ]
            }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          }
        }
        """)!.AsObject();
        var residentRoot = JsonNode.Parse("""
        {
          "entries": [
            {
              "residentId": "resident_liora",
              "displayName": "Лиора",
              "ascensionState": "ascended",
              "shiningFactionId": "faction_dawn",
              "residentRole": "descent_support",
              "grantedRelicId": "relic_echo"
            }
          ]
        }
        """)!.AsObject();
        Assert.True(ShiningAbodeState.TryOpenGates(root, residentRoot, out var openError), openError);
        var first = root["gates"]!["availableBlessingCards"]![0]!["cardId"]!.GetValue<string>();
        Assert.True(ShiningAbodeState.TrySelectBlessingCard(root, first, out var selectError), selectError);
        Assert.True(ShiningAbodeState.TryPrepareIncarnationPackage(root, 155, out var packageError), packageError);
        var package = root["preparedIncarnationPackage"]!.AsObject();
        Assert.Null(ShiningAbodeState.ValidatePreparedIncarnationPackageForBootstrap(package));
        return package.DeepClone().AsObject();
    }
}
