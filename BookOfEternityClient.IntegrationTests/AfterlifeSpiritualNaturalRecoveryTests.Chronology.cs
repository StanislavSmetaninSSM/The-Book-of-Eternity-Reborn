using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    [Fact]
    public async Task CurrentDataWitnessRequiresActualNewbornProvenanceAndEveryCycleAuditRow()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 0, 20, stageRequest: false);
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        await AddSecondWoundAsync(fixture);
        var (control, _) = await StageAsync(fixture);
        await PublishAsync(fixture);
        var history = await fixture.ReadHistoryAsync();
        var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions,
            t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
        var eventRef = result.Stages[0].EventRef;
        var unchanged = fixture.CaptureMutationRoots();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var frame = await fixture.ReadManifestAsync(lease);
        var actualProfiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
        var actualIdentities = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundIdentityState.StatePath))!)!.AsObject();
        var actualEffects = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, EffectIdentityState.StatePath))!)!.AsObject();

        async Task<SpiritualHealingPersistedResult?> Read(IReadOnlyDictionary<string, byte[]> data) =>
            await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(control, frame.SessionId, frame.RequestId,
                frame.TurnNumber, p => data.TryGetValue(p, out var bytes) ? Task.FromResult<byte[]?>(bytes)
                    : fixture.Files.ReadFileBytesAsync(lease, p));

        // Strict current DATA from a different accepted owner is probed here. This
        // does NOT claim natural+new-wound command composition is a supported publisher.
        foreach (var matchingEvent in new[] { true, false })
        {
            var birthEvent = matchingEvent ? eventRef : "event_different_accepted_creation";
            var profiles = actualProfiles.DeepClone().AsObject();
            var player = profiles["profiles"]![0]!.AsObject();
            string Rebind(string text) => text.Replace(original.WoundId, "wound_newborn_current", StringComparison.Ordinal)
                .Replace("effect_", "effect_newborn_", StringComparison.Ordinal)
                .Replace("definition_", "definition_newborn_", StringComparison.Ordinal);
            var newborn = JsonNode.Parse(Rebind(WoundMaterializationContract.SerializeCanonical(original)))!.AsObject();
            newborn["origin"]!["createdAtTurn"] = frame.TurnNumber;
            newborn["origin"]!["eventRef"] = birthEvent;
            newborn["origin"]!["opportunityId"] = "opportunity_newborn_current";
            newborn["lastTransition"] = new JsonObject { ["transitionId"] = "create_newborn_current", ["ordinal"] = 1,
                ["turn"] = frame.TurnNumber, ["kind"] = "create" };
            var parsedWound = WoundMaterializationContract.Parse(newborn.ToJsonString(), "newborn.current");
            Assert.True(parsedWound.IsValid, string.Join(";", parsedWound.Issues.Select(i => i.Actual)));
            var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(parsedWound.Wound!);
            player["activeWounds"]!.AsArray().Add(newborn);
            var identities = actualIdentities.DeepClone().AsObject();
            identities["entries"]!.AsArray().Add(WoundContractTestData.CreateIdentityEntry("wound_newborn_current", "chaos_sea",
                "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath, "spiritual",
                createdAtTurn: frame.TurnNumber, createdEventRef: birthEvent, semanticFingerprint: fingerprint));
            var row = WoundContractTestData.CreateTransition();
            row["transitionId"] = "create_newborn_current"; row["woundId"] = "wound_newborn_current";
            row["ordinal"] = history.NextOrdinal; row["woundTransitionOrdinal"] = 1; row["turn"] = frame.TurnNumber;
            row["eventRef"] = birthEvent; row["operationKey"] = "create_newborn_current";
            row["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint("wound_newborn_current");
            row["afterFingerprint"] = fingerprint; row["sourceFingerprint"] = "sha256:" + new string('b', 64);
            row["attemptId"] = null;
            row["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint("create_newborn_current", birthEvent,
                row["readableSummary"]!.GetValue<string>());
            var historyRoot = JsonNode.Parse(WoundHistoryState.SerializeCanonical(history))!.AsObject();
            historyRoot["transitions"]!.AsArray().Add(row); historyRoot["nextOrdinal"] = history.NextOrdinal + 1;

            var effect = JsonNode.Parse(Rebind(player["activeEffects"]![0]!.ToJsonString()))!.AsObject();
            effect["chronology"]!["createdAtTurn"] = frame.TurnNumber;
            effect["chronology"]!["createdEventRef"] = birthEvent;
            effect["chronology"]!["lastTransitionTurn"] = frame.TurnNumber;
            effect["chronology"]!["lastTransitionId"] = "effect_transition_newborn_create";
            player["activeEffects"]!.AsArray().Add(effect);
            var effectEntry = EffectMaterializationTestFixture.CreateIdentityIndex(effect)["entries"]![0]!.DeepClone().AsObject();
            effectEntry["owner"]!["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
            effectEntry["createdAtTurn"] = frame.TurnNumber;
            effectEntry["transitions"]![0]!["turn"] = frame.TurnNumber;
            effectEntry["transitions"]![0]!["eventRef"] = birthEvent;
            effectEntry["transitions"]![0]!["transitionId"] = "effect_transition_newborn_create";
            var effects = actualEffects.DeepClone().AsObject(); effects["entries"]!.AsArray().Add(effectEntry);
            var catalog = RequireCurrentAgreement(profiles, identities, historyRoot);
            var mechanics = EffectMechanicsSnapshot.Build(new(new(null, null, null, null, profiles, null), effects));
            Assert.True(mechanics.IsAccepted, string.Join(";", mechanics.Issues.Select(i => i.Actual)));
            Assert.Equal(2, catalog.Occurrences.Count);
            var data = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [WoundCarrierCatalog.AfterlifeProfilesPath] = System.Text.Encoding.UTF8.GetBytes(profiles.ToJsonString()),
                [WoundIdentityState.StatePath] = System.Text.Encoding.UTF8.GetBytes(identities.ToJsonString()),
                [WoundHistoryState.HistoryPath] = System.Text.Encoding.UTF8.GetBytes(historyRoot.ToJsonString()),
                [EffectIdentityState.StatePath] = System.Text.Encoding.UTF8.GetBytes(effects.ToJsonString())
            };
            if (matchingEvent) Assert.NotNull(await Read(data));
            else
            {
                var failure = await Assert.ThrowsAsync<InvalidDataException>(() => Read(data));
                Assert.Contains("Newborn exemption lacks this accepted turn's actual creation provenance", failure.Message);
            }
        }

        // A legal postterminal archive row can retain its healed semantic seal.
        // Give it another source FP so selecting only already-matching rows would
        // wrongly hide it before the independent event/cohort bijection check.
        var terminal = Assert.Single(history.Transitions, t => t.Terminal);
        const string archiveSummary = "Независимая архивная запись о зажившей ране.";
        var archive = terminal with
        {
            TransitionId = "archive_current_terminal", Ordinal = history.NextOrdinal,
            WoundTransitionOrdinal = terminal.WoundTransitionOrdinal + 1, Kind = "archive",
            OperationKey = "archive_current_terminal", BeforeFingerprint = terminal.AfterFingerprint,
            AfterFingerprint = terminal.AfterFingerprint, SourceFingerprint = "sha256:" + new string('c', 64),
            AttemptId = null, CycleKey = null, Terminal = false, TransitionResult = null,
            ReadableSummary = archiveSummary,
            OutputFingerprint = WoundHistoryState.ComputeOutputFingerprint("archive_current_terminal", eventRef, archiveSummary)
        };
        Assert.NotEqual(terminal.SourceFingerprint, archive.SourceFingerprint);
        var archived = WoundHistoryState.CreateValidated(history.NextOrdinal + 1, history.Transitions.Append(archive));
        Assert.True(archived.IsValid, string.Join(";", archived.Issues.Select(i => i.Actual)));
        var archiveIdentities = actualIdentities.DeepClone().AsObject();
        archiveIdentities["entries"]!.AsArray().OfType<JsonObject>().Single(i =>
            i["woundId"]!.GetValue<string>() == terminal.WoundId)["lastTransitionOrdinal"] = archive.WoundTransitionOrdinal;
        var archiveHistory = JsonNode.Parse(WoundHistoryState.SerializeCanonical(archived.State!))!.AsObject();
        RequireCurrentAgreement(actualProfiles, archiveIdentities, archiveHistory);
        var archiveFailure = await Assert.ThrowsAsync<InvalidDataException>(() => Read(new Dictionary<string, byte[]>
        {
            [WoundIdentityState.StatePath] = System.Text.Encoding.UTF8.GetBytes(archiveIdentities.ToJsonString()),
            [WoundHistoryState.HistoryPath] = System.Text.Encoding.UTF8.GetBytes(archiveHistory.ToJsonString())
        }));
        Assert.Contains("stages do not biject with the independently witnessed cycle rows", archiveFailure.Message);
        fixture.AssertMutationRootsUnchanged(unchanged);
    }

    private static WoundCarrierCatalog RequireCurrentAgreement(JsonObject profiles, JsonObject identities, JsonObject history)
    {
        var catalog = WoundCarrierCatalog.Build(new(null, null, null, null, profiles));
        Assert.Empty(catalog.Issues);
        var index = WoundIdentityState.Parse(identities.ToJsonString(), "current.identities");
        var ledger = WoundHistoryState.Parse(history.ToJsonString(), "current.history");
        Assert.True(index.IsValid, string.Join(";", index.Issues.Select(i => i.Actual)));
        Assert.True(ledger.IsValid, string.Join(";", ledger.Issues.Select(i => i.Actual)));
        Assert.Empty(ledger.State!.ValidateAgreement(index.State!, catalog));
        return catalog;
    }
}
