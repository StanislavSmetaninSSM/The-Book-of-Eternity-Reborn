using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

/// <summary>Actual completed publication replay and authored weaker-graph repair.</summary>
public sealed class AfterlifeWoundHealingReplayGraphTests
{
    [Fact]
    public async Task PublishedAttemptReplaysThroughFreshServicesWithoutChangingAnyMember()
    {
        await using var fixture = await HealingFixture.CreateAsync(rank: 1, tier: 1, die: 20);
        var published = await fixture.PublishAsync();
        var before = fixture.CaptureMutationRoots(published.TouchedPaths.Concat(published.ConsumedPaths)
            .Concat(WoundAcceptedTurnSnapshotContract.OutputPaths));
        LiveTurnPendingSnapshotManifest manifest;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            manifest = await fixture.ReadManifestAsync(lease);
        var reopened = new FileSystemManager(fixture.Files.BasePath, NullLogger<FileSystemManager>.Instance);
        await using (var lease = await reopened.AcquireCanonicalWriteLeaseAsync())
        {
            var issues = await new ValidationService(reopened, NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
            Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Error);
            Assert.Null(await new CanonicalStateNormalizer(reopened, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
        }
        var scheduler = new ProgressionScheduleService(reopened, NullLogger<ProgressionScheduleService>.Instance);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            Assert.DoesNotContain(await scheduler.ValidateAcceptedTurnOutcomeAsync(manifest.ProgressionControl!),
                i => i.Severity == IssueSeverity.Error);
            await scheduler.ApplyAcceptedTurnOutcomeAsync(manifest.ProgressionControl!);
        }
        fixture.AssertMutationRootsUnchanged(before);
        await fixture.AssertOneCycleAndRetainedAttemptAsync();
        Assert.Single((await fixture.ReadHistoryAsync()).Transitions.Where(t => t.Terminal));
    }

    [Fact]
    public async Task MissingWeakerGraphsRetainAttemptThenAuthoredGraphsApplyBeforeNaturalRecovery()
    {
        await using var fixture = await HealingFixture.CreateAsync(rank: 3, tier: 3, die: 12);
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var originalRoots = original.Consequences.OwnedEffectSources.RootBindings.Select(b => b.EffectId).ToArray();
        JsonObject request;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.WriteReportAsync(lease);
            request = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
            var before = fixture.CaptureMutationRoots();
            var issues = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
            Assert.Contains(issues, i => i.Code == "spiritual_self_treatment_admission_invalid" &&
                i.Actual?.Contains("GM-authored spiritual destination graphs are required", StringComparison.Ordinal) == true);
            fixture.AssertMutationRootsUnchanged(before);
            var graphs = new JsonArray();
            foreach (var rank in new[] { 2, 1 })
            {
                var definitions = new JsonArray();
                // These are explicit authored destination choices, independent of ID/slot order.
                var authoredProfiles = rank == 2
                    ? new[] { "spiritual_roll_hindrance", "spiritual_action_cost_burden" }
                    : new[] { "spiritual_roll_hindrance" };
                foreach (var profile in authoredProfiles)
                {
                    var rootId = original.Consequences.Entries.First(e => e.ProfileKey == profile).EffectId;
                    var binding = original.Consequences.OwnedEffectSources.RootBindings.Single(b => b.EffectId == rootId);
                    var definition = JsonNode.Parse(original.Consequences.OwnedEffectSources.Definitions.Single(d =>
                        d.GetProperty("definitionKey").GetString() == binding.DefinitionKey).GetRawText())!.AsObject();
                    definition["links"] = new JsonArray();
                    if (profile == "spiritual_action_cost_burden") definition["components"]![0]!["payload"]!["magnitude"] = 1;
                    definitions.Add(new JsonObject
                    {
                        ["definitionRef"] = binding.DefinitionKey,
                        ["definition"] = definition,
                        ["root"] = new JsonObject
                        {
                            ["ownership"] = new JsonObject { ["kind"] = "base_wound", ["complicationRef"] = null },
                            ["slots"] = new JsonArray(new JsonObject { ["profileKey"] = profile,
                                ["readableSummary"] = "Ослабленное последствие, описанное Мастером." })
                        }
                    });
                }
                graphs.Add(new JsonObject
                {
                    ["stageRef"] = SpiritualWoundRecoveryPlanner.Coordinate("spiritual_healing_graph",
                        request["requestFingerprint"]!.GetValue<string>(), original.WoundId, rank),
                    ["consequenceDefinitions"] = definitions
                });
            }
            await fixture.Files.WriteFileAtomicAsync(lease, "output/interface_updates.json",
                new JsonObject { ["spiritualHealingGraphs"] = graphs }.ToJsonString());
        }
        await fixture.PublishAsync();
        var after = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(1, after.Severity.Rank);
        Assert.Equal(0, after.Recovery.CurrentStepProgress);
        Assert.Equal("spiritual_roll_hindrance", Assert.Single(after.Consequences.Entries).ProfileKey);
        Assert.Equal("Ослабленное последствие, описанное Мастером.", Assert.Single(after.Consequences.Entries).ReadableSummary);
        Assert.DoesNotContain(after.Consequences.OwnedEffectSources.RootBindings, b => originalRoots.Contains(b.EffectId));
        var history = await fixture.ReadHistoryAsync();
        Assert.Equal(new[] { "treat", "recover" }, history.Transitions.Where(t => t.Turn == 43).Select(t => t.Kind));
        Assert.DoesNotContain(history.Transitions, t => t.Terminal);
        var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions,
            t => t.Turn == 43 && t.AttemptId is not null).TransitionResult);
        var stages = result.Stages;
        Assert.Equal(new[] { 2, 1 }, stages.Select(s => s.After.Severity.Rank));
        var intermediate = stages[0].After;
        Assert.Equal(new[] { "spiritual_action_cost_burden", "spiritual_roll_hindrance" },
            intermediate.Consequences.Entries.Select(e => e.ProfileKey).Order(StringComparer.Ordinal));
        var intermediateCost = intermediate.Consequences.OwnedEffectSources.Definitions
            .SelectMany(d => d.GetProperty("components").EnumerateArray())
            .Single(c => c.GetProperty("profile").GetString() == "spiritual_action_cost_burden");
        Assert.Equal(1m, intermediateCost.GetProperty("payload").GetProperty("magnitude").GetDecimal());
        using var identityDocument = JsonDocument.Parse((await fixture.Files.ReadFileAsync(EffectIdentityState.StatePath))!);
        var identity = EffectIdentityState.Parse(identityDocument.RootElement, EffectIdentityState.StatePath);
        Assert.Empty(identity.Issues);
        foreach (var id in originalRoots.Concat(intermediate.Consequences.OwnedEffectSources.RootBindings.Select(b => b.EffectId)).Distinct())
        {
            Assert.True(identity.State!.TryGetEntry(id, out var retired));
            Assert.Equal("expired", retired.State);
        }
        foreach (var binding in after.Consequences.OwnedEffectSources.RootBindings)
        {
            Assert.True(identity.State!.TryGetEntry(binding.EffectId, out var active));
            Assert.Equal("active", active.State);
        }
        await fixture.AssertOneCycleAndRetainedAttemptAsync();
        fixture.AssertResourcesUnchanged();
    }
}
