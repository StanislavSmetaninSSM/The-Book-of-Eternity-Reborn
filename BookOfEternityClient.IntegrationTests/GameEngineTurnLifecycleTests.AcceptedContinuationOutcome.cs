using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const string ContinuationSoul = "game_state/meta/soul_state.json";
    private const string ContinuationStats = "game_state/misc/characteristics.json";
    private const string ContinuationDiagnostic = "game_state/control/validation_diagnostic_failure_report.json";

    [Theory]
    [InlineData("application")]
    [InlineData("audit")]
    [InlineData("finalize")]
    public async Task MemoryLegacyUnknown_OriginalConsumerRetainsPriorEffect(string mode)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new AcceptedContinuationProbe(_rootPath);
        var files = probe.Files;
        var soul = CreateLifecycleSoulState("Memory continuation", mode == "finalize" ? "Mortal World" : "Chaos Sea");
        var legacy = new JsonObject
        {
            ["legacyId"] = "bounded_memory_strength", ["legacyType"] = "startingCharacteristicBonus",
            ["characteristic"] = Characteristics.Strength, ["bonus"] = 2,
            ["applicationState"] = "pending", ["grantSource"] = "memoryLegacyGrant",
            ["grantSnapshot"] = new JsonObject { ["legacyId"] = "bounded_memory_strength",
                ["legacyType"] = "startingCharacteristicBonus", ["characteristic"] = Characteristics.Strength, ["bonus"] = 2 }
        };
        soul["pendingMemoryLegacy"] = legacy;
        await SeedMortalLifeTransitionAuthorityAsync(soul);
        var keys = mode == "finalize" ? Array.Empty<ConsoleKey>() : mode == "application"
            ? new[] { ConsoleKey.Enter } : new[] { ConsoleKey.Enter }.Concat(Enumerable.Repeat(ConsoleKey.RightArrow, 8)).Append(ConsoleKey.Enter).ToArray();
        var input = new AcceptedContinuationInput(keys);
        var engine = CreateGameEngine(input, s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; }, fileSystem: files);
        object? accepted = null;
        if (mode != "finalize")
        {
            const string trigger = "{\"worldDescription\":\"A quiet port\",\"characterDescription\":\"A clerk\",\"circumstances\":\"An ordinary arrival\",\"source\":\"test\"}";
            _fs.DeleteFile("game_state/control/life_transitions.json");
            _fs.DeleteFile("game_state/control/ascension.json");
            await _fs.WriteFileAtomicAsync("game_state/control/incarnation_trigger.json", trigger);
            await WriteCurrentSoulStateToPendingSnapshotAsync();
            await WritePendingTurnSnapshotManifestAsync("memory-session", "memory-request", 3, ContinuationSoul);
            await WriteJsonAsync("input/turn_request.json", new { sessionId = "memory-session", requestId = "memory-request", turnNumber = 3, playerAction = "memory continuation" });
            await WriteJsonAsync("ready/turn_complete.json", new { sessionId = "memory-session", requestId = "memory-request", turnNumber = 3,
                status = "success", timestamp = "2026-10-09T00:00:00Z", filesModified = new[] { "game_state/control/incarnation_trigger.json" } });
            await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
            Assert.Equal("Chaos Sea", GetPrivateField<StateManager>(engine, "_stateManager").CurrentState.CurrentRealm);
            var manifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
            accepted = await InvokePrivateTaskResultAsync(engine, "LoadValidatedPendingTurnSnapshotContextAsync", manifest, true);
            Assert.NotNull(accepted);
            Assert.True(IncarnationTriggerContract.TryParse(trigger, out var payload));
            Assert.True(await InvokePrivateAsync<bool>(engine, "HasAcceptedTurnAuthorityForIncarnationTriggerAsync", payload, false, accepted));
        }
        else
        {
            await GetPrivateField<CharacteristicsService>(engine, "_charService").InitializeForNewIncarnation();
            Assert.False(string.IsNullOrWhiteSpace(await InvokePrivateAsync<string>(engine, "ApplyPendingMemoryLegacyForIncarnationAsync")));
            Assert.Equal("applied-awaiting-turn-accept", ReadContinuationObject(files, ContinuationSoul)["pendingMemoryLegacy"]!["applicationState"]!.GetValue<string>());
        }
        var expectedStrength = mode == "audit" ? 11 : 3;
        var preparedEffect = mode == "finalize" ? File.ReadAllBytes(files.ResolvePath(ContinuationStats)) : null;
        Dictionary<string, byte[]?>? retainedAtCut = null;
        probe.Cut.Select = (path, member) =>
        {
            if (path != files.ResolvePath(ContinuationSoul) || !member.GetProperty("After").GetProperty("Exists").GetBoolean()) return false;
            var current = ReadContinuationObject(files, ContinuationSoul)["pendingMemoryLegacy"];
            return mode switch
            {
                "application" => current?["applicationState"]?.GetValue<string>() == "applied-awaiting-turn-accept",
                "audit" => current?["applicationAudit"]?["expectedCharacteristicValue"]?.GetValue<int>() == 11,
                _ => current == null
            };
        };
        probe.Cut.BeforeCut = () =>
        {
            Assert.Equal(expectedStrength, ReadContinuationObject(files, ContinuationStats)[Characteristics.Strength]!.GetValue<int>());
            var actual = File.ReadAllBytes(files.ResolvePath(ContinuationStats));
            if (mode == "finalize") Assert.Equal(preparedEffect, actual);
            else Assert.Equal(probe.CommittedImages[files.ResolvePath(ContinuationStats)], actual);
            if (mode == "audit")
            {
                Assert.Equal(0, ReadContinuationObject(files, "game_state/player/stat_points.json")["unspentStatPoints"]!.GetValue<int>());
                Assert.Equal(probe.CommittedImages[files.ResolvePath("game_state/player/stat_points.json")], File.ReadAllBytes(files.ResolvePath("game_state/player/stat_points.json")));
            }
            retainedAtCut = probe.Capture([ContinuationStats, "game_state/player/stat_points.json", "game_state/player/computed_characteristics.json"]);
            input.AssertCompleted();
        };
        probe.Cut.Armed = true;
        var failure = await Record.ExceptionAsync(() => mode == "finalize"
            ? InvokePrivateTaskAsync(engine, "FinalizePendingMemoryLegacyConsumptionAsync")
            : InvokePrivateTaskAsync(engine, "CheckGmIncarnationTrigger", accepted));
        var after = probe.Capture([ContinuationStats, "game_state/player/stat_points.json", "game_state/player/computed_characteristics.json"]);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { mode, Failure = failure?.ToString(), input.Reads, input.UnexpectedReads,
            probe.RequestAttempts, preparedEffect, retainedAtCut, after, probe.CommittedImages, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure);
        Assert.Equal(0, probe.RequestAttempts); input.AssertCompleted();
        Assert.NotNull(retainedAtCut); foreach (var path in retainedAtCut!.Keys) Assert.Equal(retainedAtCut[path], after[path]);
    }

    [Fact]
    public async Task ShiningEffectUnknown_OriginalEngineWrapperPreservesExpiredDecision()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new AcceptedContinuationProbe(_rootPath);
        var files = probe.Files;
        await SeedMortalLifeTransitionAuthorityAsync(CreateLifecycleSoulState("Shining continuation", "Mortal World"));
        var card = new JsonObject
        {
            ["cardId"] = "bounded_route", ["dedupeKey"] = "route:bounded_route", ["sourceType"] = ShiningAbodeState.CardSourceTypeProject,
            ["sourceFactionId"] = "faction_dawn", ["displayName"] = "bounded route", ["displaySummary"] = "route",
            ["sourceActorId"] = "guardian_dawn", ["effectFamily"] = "route", ["rarity"] = ShiningAbodeState.RarityCommon,
            ["effectPayload"] = new JsonObject { ["type"] = "seed_early_routes", ["routeOptions"] = 1, ["latestTurn"] = 8 }
        };
        var prepared = new JsonObject { ["preparedAtTurn"] = 0, ["selectedCardIds"] = new JsonArray("bounded_route"), ["selectedCards"] = new JsonArray(card) };
        var materialized = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(files, prepared, 2);
        Assert.True(materialized.Success, materialized.ErrorMessage);
        var effect = Assert.Single(ReadContinuationObject(files, ContinuationSoul)[ShiningBlessingEffectState.SoulStateProperty]!["pendingRouteEffects"]!.AsArray());
        Assert.Equal(ShiningBlessingEffectState.RouteStatusPendingEarlyRouteSeed, effect!["status"]!.GetValue<string>());
        var effectId = effect["effectId"]!.GetValue<string>();
        var input = new AcceptedContinuationInput([]);
        var engine = CreateGameEngine(input, s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; }, fileSystem: files);
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession("shining-continuation", 9);
        probe.Cut.Select = (path, member) => path == files.ResolvePath(ContinuationSoul) && member.GetProperty("After").GetProperty("Exists").GetBoolean();
        probe.Cut.BeforeCut = () =>
        {
            var expired = Assert.Single(ReadContinuationObject(files, ContinuationSoul)[ShiningBlessingEffectState.SoulStateProperty]!["pendingRouteEffects"]!.AsArray());
            Assert.Equal(effectId, expired!["effectId"]!.GetValue<string>());
            Assert.Equal(ShiningBlessingEffectState.GenericStatusExpired, expired["status"]!.GetValue<string>());
            Assert.Equal(9, expired["expiredAtTurn"]!.GetValue<int>());
        };
        probe.Cut.Armed = true;
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "ApplyPendingShiningBlessingRuntimeEffectsAsync", new object?[] { null }));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { effectId, Failure = failure?.ToString(), input.Reads, probe.RequestAttempts, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure); input.AssertCompleted(); Assert.Equal(0, probe.RequestAttempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedRollbackUnknown_OriginalCallerStopsBeforeFurtherBookkeeping(bool diagnostic)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new AcceptedContinuationProbe(_rootPath);
        var files = probe.Files;
        var logger = new AcceptedContinuationLogger();
        var input = new AcceptedContinuationInput([]);
        var engine = CreateGameEngine(input, s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; }, fileSystem: files, logger: logger);
        await GetPrivateField<StateManager>(engine, "_stateManager").BootstrapLocalStorageAsync();
        const string tracked = "game_state/world/weather.json";
        await files.WriteFileAtomicAsync(tracked, "{\"weather\":\"baseline\"}");
        var original = File.ReadAllBytes(files.ResolvePath(tracked));
        var snapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "continuation_rollback");
        var backups = Assert.IsAssignableFrom<Dictionary<string, string>>(snapshot.GetType().GetProperty("BackupFiles")!.GetValue(snapshot));
        Assert.True(Assert.IsType<bool>(typeof(GameEngine).GetMethod("HasRollbackCapability", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, [snapshot])));
        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", CreateSnapshotByteContractRequest("continuation_rollback"), snapshot, "continuation rollback");
        await files.WriteFileAtomicAsync(tracked, "{\"weather\":\"changed\"}");
        await files.WriteFileAtomicAsync("input/turn_request.json", "{\"requestId\":\"retained\"}");
        await files.WriteFileAtomicAsync("ready/turn_complete.json", "{\"requestId\":\"retained\"}");
        var missingBackup = files.ResolvePath(backups[tracked]);
        if (diagnostic) files.DeleteFile(backups[tracked]); // Disclosed fixture removal of one genuine, mapped backup.
        var evidencePaths = backups.Values.Concat(new[] { "input/turn_request.json", "ready/turn_complete.json", "game_state/control/pending_turn_snapshot.json", PendingTurnSnapshotAuthority.AuthorityPath }).ToArray();
        var before = probe.Capture(evidencePaths);
        probe.Cut.Select = (path, member) => path == files.ResolvePath(diagnostic ? ContinuationDiagnostic : tracked) && member.GetProperty("After").GetProperty("Exists").GetBoolean();
        probe.Cut.BeforeCut = () =>
        {
            if (!diagnostic) { Assert.Equal(original, File.ReadAllBytes(files.ResolvePath(tracked))); return; }
            var known = Assert.IsType<InvalidDataException>(logger.RollbackFailure);
            var aggregate = Assert.IsType<AggregateException>(known.InnerException);
            var invalid = Assert.IsType<InvalidDataException>(Assert.Single(aggregate.InnerExceptions));
            var missing = Assert.IsType<FileNotFoundException>(invalid.InnerException);
            Assert.Equal(backups[tracked], missing.FileName);
            var report = ReadContinuationObject(files, ContinuationDiagnostic);
            Assert.Equal(known.GetType().FullName, report["exceptionType"]!.GetValue<string>());
            Assert.Equal(known.Message, report["message"]!.GetValue<string>());
            Assert.Equal(known.ToString(), report["details"]!.GetValue<string>());
        };
        probe.Cut.Armed = true;
        object? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await InvokePrivateTaskResultAsync(engine, "RollbackRejectedAcceptedTurnAsync", snapshot, string.Empty));
        var after = probe.Capture(evidencePaths);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { diagnostic, Failure = failure?.ToString(), result, original, missingBackup,
            KnownFailure = logger.RollbackFailure?.ToString(), KnownCauseRetained = logger.RollbackFailure != null && probe.Cut.RetainsDiagnostic(logger.RollbackFailure), before, after, input.Reads, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure); input.AssertCompleted();
        foreach (var path in before.Keys) Assert.Equal(before[path], after[path]);
        if (diagnostic) Assert.True(probe.Cut.RetainsDiagnostic(Assert.IsType<InvalidDataException>(logger.RollbackFailure)));
    }

    [Fact]
    public async Task BookkeepingDeleteUnknown_OriginalDiagnosticCallerStopsBeforeNextControls()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new AcceptedContinuationProbe(_rootPath);
        var files = probe.Files;
        var input = new AcceptedContinuationInput([]);
        var engine = CreateGameEngine(input, s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; }, fileSystem: files);
        await GetPrivateField<StateManager>(engine, "_stateManager").BootstrapLocalStorageAsync();
        string generation;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync()) generation = files.GetOrCreateSessionGeneration(lease);
        string[] sentinels = ["game_state/control/validation_repair_ready.json", "game_state/control/validation_repair_request.json", "game_state/control/gm_validation_repair_artifact_stall_report.json"];
        foreach (var path in sentinels) await files.WriteFileAtomicAsync(path, JsonSerializer.Serialize(new { retained = path }));
        await files.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, "{\"report\":\"retained until actual delete\"}");
        var before = probe.Capture(sentinels);
        byte[]? diagnosticAtCut = null;
        probe.Cut.Select = (path, member) => path == files.ResolvePath(ProgressionScheduleService.ReportPath) && !member.GetProperty("After").GetProperty("Exists").GetBoolean();
        probe.Cut.BeforeCut = () =>
        {
            diagnosticAtCut = File.ReadAllBytes(files.ResolvePath(ContinuationDiagnostic));
            Assert.Equal(probe.CommittedImages[files.ResolvePath(ContinuationDiagnostic)], diagnosticAtCut);
        };
        var issue = new ValidationIssue("game_state/world/world_events.json", IssueSeverity.Error, "Bounded diagnostic-only failure", code: "accepted_turn_invalid_snapshot_baseline");
        probe.Cut.Armed = true;
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "FailClosedDiagnosticOnlyValidationRepairAsync", "continuation fixture", new List<ValidationIssue> { issue }, 1, null, generation));
        var after = probe.Capture(sentinels);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { Failure = failure?.ToString(), generation, before, after, diagnosticAtCut,
            RetainedDiagnostic = CleanupPublicationCut.ReadOptional(files.ResolvePath(ContinuationDiagnostic)), input.Reads, probe.CommittedImages, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure); input.AssertCompleted();
        foreach (var path in before.Keys) Assert.Equal(before[path], after[path]);
        Assert.Equal(diagnosticAtCut, File.ReadAllBytes(files.ResolvePath(ContinuationDiagnostic)));
    }

    private static JsonObject ReadContinuationObject(FileSystemManager files, string relative) => JsonNode.Parse(File.ReadAllText(files.ResolvePath(relative)))!.AsObject();

    private sealed class AcceptedContinuationProbe : IDisposable
    {
        internal CleanupPublicationCut Cut { get; } = new();
        internal FileSystemManager Files { get; }
        internal Dictionary<string, byte[]?> CommittedImages { get; } = new(StringComparer.Ordinal);
        internal int RequestAttempts { get; private set; }
        internal AcceptedContinuationProbe(string root)
        {
            var hooks = new FileSystemManagerHooks
            {
                LocalPublicationObserver = Cut.Hooks.LocalPublicationObserver,
                LocalPublicationRecoveryObserver = Cut.Hooks.LocalPublicationRecoveryObserver,
                BeforeCanonicalMutationBoundaryAsync = Cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
                AfterCanonicalReadInitialValidationAsync = Cut.Hooks.AfterCanonicalReadInitialValidationAsync,
                SessionOperationClosingAsync = Cut.Hooks.SessionOperationClosingAsync,
                BeforeCanonicalWriteLockOpenAsync = Cut.Hooks.BeforeCanonicalWriteLockOpenAsync,
                BeforeCanonicalMutationAsync = path =>
                {
                    if (Cut.Armed && path.Replace('\\', '/').EndsWith("input/turn_request.json", StringComparison.Ordinal))
                    { RequestAttempts++; throw new InvalidOperationException("fixture refuses later GM request"); }
                    return Task.CompletedTask;
                }
            };
            Files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
            Cut.Attach(Files);
            Cut.ObserveBeforeCut = (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.Committed) return;
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Cut.JournalPath));
                foreach (var member in journal.RootElement.GetProperty("Members").EnumerateArray())
                {
                    var path = member.GetProperty("Path").GetString()!;
                    CommittedImages[path] = CleanupPublicationCut.ReadOptional(path);
                }
            };
        }
        internal Dictionary<string, byte[]?> Capture(IEnumerable<string> paths) => paths.Distinct(StringComparer.Ordinal).ToDictionary(x => x, x => CleanupPublicationCut.ReadOptional(Files.ResolvePath(x)), StringComparer.Ordinal);
        public void Dispose() => Cut.Dispose();
    }

    private sealed class AcceptedContinuationInput(ConsoleKey[] keys) : IConsoleInputSource
    {
        internal int Reads { get; private set; }
        internal int UnexpectedReads { get; private set; }
        public bool IsScripted => true;
        public bool KeyAvailable => Reads < keys.Length;
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            if (Reads < keys.Length) return Key(keys[Reads++]);
            UnexpectedReads++; throw new InvalidOperationException("continuation fixture refuses extra input");
        }
        public string? ReadLine() { UnexpectedReads++; throw new InvalidOperationException("continuation fixture refuses line input"); }
        public void AssertCompleted() { Assert.Equal(keys.Length, Reads); Assert.Equal(0, UnexpectedReads); }
    }

    private sealed class AcceptedContinuationLogger : ILogger<GameEngine>
    {
        internal Exception? RollbackFailure { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, error).StartsWith("Rejected accepted-turn rollback failed", StringComparison.Ordinal)) RollbackFailure = error;
        }
    }
}
