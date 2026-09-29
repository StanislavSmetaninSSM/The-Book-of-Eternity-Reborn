using System.Text.Json.Nodes;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundRecoveryTests
{
    /// <summary>
    /// Publishes only newly elapsed intervals through real effect generations and
    /// preserves exact terminal replay after a later full heal and cold reopen.
    /// </summary>
    [Fact]
    public void Publication_ConsumesEpochDeltasRematerializesAndHealsOnTheNextInterval()
    {
        var scenario = Scenario.MultiCadenceJump();
        var authored = scenario.Wound.DeepClone().AsObject();
        authored["recovery"]!["currentStepThreshold"] = 2;
        // This complete setting-authored route stabilizes without treating away a
        // severity tier; the later change must come from actual natural recovery.
        foreach (var outcome in authored["treatment"]!["routes"]![0]!["outcomes"]!.AsArray().OfType<JsonObject>())
            outcome["result"] = new JsonArray(outcome["result"]!.AsArray().OfType<JsonObject>()
                .Where(operation => operation["kind"]!.GetValue<string>() != "reduce_severity")
                .Select(operation => (JsonNode?)operation.DeepClone()).ToArray());
        scenario = scenario with { Wound = authored };
        using var fixture = Fixture.Create(scenario);
        var original = ReadActiveOrTerminalRecoveryWound(fixture.FileSystem, fixture.WoundId);
        var originalWound = WoundMaterializationContract.Parse(original.ToJsonString(), "originalRecovery").Wound!;
        Assert.Equal(2, originalWound.Severity.Rank);
        var originalRoot = Assert.Single(originalWound.Consequences.OwnedEffectSources.RootBindings).EffectId;
        var resolution = Assert.IsType<MortalWoundRecoveryResolution>(Required(InvokePlan(fixture), "Resolution"));
        Assert.Equal(3, resolution.ElapsedCadences);
        Assert.Equal(140, resolution.NextRecoveryAnchorMinute);
        ComposeAndPublishRecovery(fixture, resolution);
        var first = WoundMaterializationContract.Parse(
            ReadActiveOrTerminalRecoveryWound(fixture.FileSystem, fixture.WoundId).ToJsonString(), "firstRecovery").Wound!;
        Assert.Equal(1, first.Severity.Rank);
        Assert.Equal(1, first.Recovery.CurrentStepProgress);
        Assert.Equal(originalWound.Recovery.RecoveryAnchor, first.Recovery.RecoveryAnchor);
        var newRoot = Assert.Single(first.Consequences.OwnedEffectSources.RootBindings).EffectId;
        Assert.NotEqual(originalRoot, newRoot);
        using var firstIdentityDocument = JsonDocument.Parse(Assert.IsType<string>(
            fixture.FileSystem.ReadFileSync(EffectIdentityState.StatePath)));
        var firstIdentity = EffectIdentityState.Parse(firstIdentityDocument.RootElement,
            EffectIdentityState.StatePath);
        Assert.Empty(firstIdentity.Issues);
        Assert.True(firstIdentity.State!.TryGetEntry(originalRoot, out var retired));
        Assert.DoesNotContain(retired.State, new[] { "active", "suspended" });

        fixture.PrepareFreshContinuationTurn(45, "next_minute_without_new_interval", 136);
        var noInterval = Assert.IsType<MortalWoundRecoveryResolution>(Required(InvokePlan(fixture), "Resolution"));
        Assert.Equal(0, noInterval.ElapsedCadences);
        Assert.Equal(140, noInterval.NextRecoveryAnchorMinute);
        ComposeAndPublishRecovery(fixture, noInterval);
        var second = WoundMaterializationContract.Parse(
            ReadActiveOrTerminalRecoveryWound(fixture.FileSystem, fixture.WoundId).ToJsonString(), "secondRecovery").Wound!;
        Assert.Equal(first.Severity, second.Severity);
        Assert.Equal(first.Recovery.CurrentStepProgress, second.Recovery.CurrentStepProgress);
        Assert.Equal(newRoot, Assert.Single(second.Consequences.OwnedEffectSources.RootBindings).EffectId);

        fixture.PrepareFreshContinuationTurn(46, "next_actual_recovery_interval", 140);
        var due = Assert.IsType<MortalWoundRecoveryResolution>(Required(InvokePlan(fixture), "Resolution"));
        Assert.Equal(1, due.ElapsedCadences);
        var terminalReceipt = ComposeAndPublishRecovery(fixture, due);
        var finalWound = ReadActiveOrTerminalRecoveryWound(fixture.FileSystem, fixture.WoundId);
        Assert.Equal("healed", finalWound["lifecycle"]!.GetValue<string>());
        using var terminalIdentityDocument = JsonDocument.Parse(Assert.IsType<string>(
            fixture.FileSystem.ReadFileSync(EffectIdentityState.StatePath)));
        var terminalIdentity = EffectIdentityState.Parse(terminalIdentityDocument.RootElement,
            EffectIdentityState.StatePath);
        Assert.Empty(terminalIdentity.Issues);
        Assert.True(terminalIdentity.State!.TryGetEntry(newRoot, out var healedRoot));
        Assert.DoesNotContain(healedRoot.State, new[] { "active", "suspended" });
        fixture.PrepareFreshContinuationTurn(47, "healed_same_minute_replay", 140);
        fixture.RestartForReplay();
        var clockPath = fixture.FileSystem.ResolvePath(EffectAcceptedTurnInputComposer.WorldTimePath);
        var clockBeforeCorruption = File.ReadAllBytes(clockPath);
        fixture.CorruptLiveClock();
        var malformedClockTree = fixture.CaptureCanonicalTreeBytes();
        AssertExactReplay(InvokePlan(fixture), terminalReceipt);
        fixture.AssertCanonicalTreeBytesUnchanged(malformedClockTree);
        Assert.NotEqual(clockBeforeCorruption, File.ReadAllBytes(clockPath));
    }

    /// <summary>
    /// Verifies the exact active or terminal identity and its complete retained recovery snapshot.
    /// </summary>
    /// <param name="catalog">
    /// The actual selected carrier catalog.
    /// </param>
    /// <param name="occurrence">
    /// The active occurrence, or null after a genuine full heal.
    /// </param>
    /// <param name="entry">
    /// The selected actual identity.
    /// </param>
    /// <param name="history">
    /// The parsed actual history, including closed recovery results.
    /// </param>
    /// <param name="path">
    /// The assertion context for active-state agreement.
    /// </param>
    /// <returns>
    /// The complete active or terminal wound whose state was verified.
    /// </returns>
    private static WoundMaterializationEnvelope AssertSelectedWoundState(
        WoundCarrierCatalog catalog, WoundCarrierOccurrence? occurrence,
        WoundIdentityEntry entry, WoundHistoryState history, string path)
    {
        if (entry.Status == "active")
        {
            Assert.NotNull(occurrence);
            Assert.Empty(WoundIdentityState.ValidateActiveAgreement(entry, occurrence!.Wound, path));
            return occurrence.Wound;
        }
        Assert.Equal("healed", entry.Status);
        Assert.Null(occurrence);
        Assert.Equal(0, catalog.CountExactOccurrences(entry.WoundId));
        var terminal = Assert.Single(history.Transitions,
            row => row.WoundId == entry.WoundId && row.Terminal);
        Assert.Equal("heal", terminal.Kind);
        Assert.Equal(entry.TerminalTransitionId, terminal.TransitionId);
        var result = Assert.Single(history.Transitions
            .Select(row => row.TransitionResult).OfType<MortalWoundRecoveryPersistedResult>(),
            row => row.SourceWound.WoundId == entry.WoundId &&
                   row.Stages.Last().TransitionId == terminal.TransitionId);
        var stage = result.Stages.Last();
        Assert.True(stage.Terminal);
        Assert.Equal("heal", stage.Kind);
        var wound = stage.AfterWound;
        Assert.Equal("healed", wound.Lifecycle);
        Assert.Equal(entry.WoundId, wound.WoundId);
        Assert.Equal(entry.LastTransitionOrdinal, wound.LastTransition.Ordinal);
        Assert.Equal(entry.TerminalTransitionId, wound.LastTransition.TransitionId);
        Assert.Equal(entry.SemanticFingerprint, WoundIdentityState.ComputeSemanticFingerprint(wound));
        Assert.Equal(terminal.AfterFingerprint, entry.SemanticFingerprint);
        Assert.Equal(result.Resolution.TickKey, wound.Recovery.LastTickKey);
        Assert.Equal(entry.Realm, wound.Owner.Realm);
        Assert.Equal(entry.OwnerKind, wound.Owner.OwnerKind);
        Assert.Equal(entry.OwnerId, wound.Owner.OwnerId);
        Assert.Equal(entry.CarrierPath, wound.Owner.CarrierPath);
        return wound;
    }

    /// <summary>
    /// Reads the actual recovery state through active carriers or verified terminal evidence.
    /// </summary>
    /// <param name="fileSystem">
    /// The fixture's actual published canonical filesystem.
    /// </param>
    /// <param name="woundId">
    /// The exact selected wound identifier.
    /// </param>
    /// <returns>
    /// A detached full wound projection after asserting carrier, identity and history agreement.
    /// </returns>
    private static JsonObject ReadActiveOrTerminalRecoveryWound(
        FileSystemManager fileSystem, string woundId)
    {
        var player = JsonNode.Parse(Assert.IsType<string>(
            fileSystem.ReadFileSync(WoundCarrierCatalog.PlayerPath)))!.AsObject();
        var catalog = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(player, null, null, null, null));
        Assert.Empty(catalog.Issues);
        catalog.TryResolveOne(woundId, out var occurrence);
        var identity = WoundIdentityState.Parse(fileSystem.ReadFileSync(WoundIdentityState.StatePath),
            WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(fileSystem.ReadFileSync(WoundHistoryState.HistoryPath),
            WoundHistoryState.HistoryPath);
        Assert.True(identity.IsValid, Issues(identity.Issues));
        Assert.True(history.IsValid, Issues(history.Issues));
        Assert.True(identity.State!.TryGetEntry(woundId, out var entry));
        Assert.Empty(history.State!.ValidateAgreement(identity.State, catalog));
        var wound = AssertSelectedWoundState(catalog, occurrence, entry!, history.State, "publishedRecoveryState");
        return JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!.AsObject();
    }
}
