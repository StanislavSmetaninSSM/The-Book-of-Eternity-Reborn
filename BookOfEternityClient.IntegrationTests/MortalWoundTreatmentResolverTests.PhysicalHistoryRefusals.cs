using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    /// <summary>
    /// Mutates detached copies of one genuinely composed positive fixture. No
    /// original, request, publication, payment or heavyweight fixture is repeated.
    /// The caller first proves the unchanged positive player-History projection.
    /// </summary>
    private void AssertPlainTerminalHistoryRefusals(WoundPlayerProjectionInput baseline,
        WoundCarrierCatalog catalog, JsonObject historyRoot, JsonObject identityRoot, JsonObject effectIdentityRoot)
    {
        var historicalEffects = Assert.IsType<WoundHistoryEffectData>(baseline.HistoricalEffects);
        var primary = Assert.Single(baseline.History.Transitions, row => row.Kind == "treat");
        var child = Assert.Single(baseline.History.Transitions, row => row.Kind == "heal");
        var retained = Assert.IsType<MortalWoundTreatmentPersistedResult>(primary.TransitionResult);
        Assert.IsType<MortalWoundStabilizeOperation>(retained.Receipt.DeclaredResult[0]);
        Assert.Equal(3, retained.ToCanonicalJson()["schemaVersion"]!.GetValue<int>());
        var creationEvent = WoundEffectOperationEventRef.Create(primary.EventRef, 1, 1, "apply");
        var allocation = Assert.Single(historicalEffects.Identities.Entries,
            entry => entry.Transitions[0].EventRef == creationEvent);
        var historyBefore = historyRoot.ToJsonString();
        var identityBefore = identityRoot.ToJsonString();
        var effectIdentityBefore = effectIdentityRoot.ToJsonString();

        JsonObject HistoryRow(JsonObject root, string id) => Assert.Single(root["transitions"]!.AsArray().OfType<JsonObject>(),
            row => row["transitionId"]!.GetValue<string>() == id);
        JsonObject Allocation(JsonObject root) => Assert.Single(root["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["effectId"]!.GetValue<string>() == allocation.EffectId);
        void ChangePrimaryCoordinate(JsonObject root, string field, string value)
        {
            var row = HistoryRow(root, primary.TransitionId);
            row[field] = value;
            row["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(
                row["operationKey"]!.GetValue<string>(), row["eventRef"]!.GetValue<string>(), row["readableSummary"]!.GetValue<string>());
        }

        var cases = new PlainHistoryRefusalCase[]
        {
            new("primary_operation", PlainHistoryRefusalSurface.History,
                root => ChangePrimaryCoordinate(root, "operationKey", "operation_foreign_physical_history")),
            new("primary_event", PlainHistoryRefusalSurface.History,
                root => ChangePrimaryCoordinate(root, "eventRef", "turn_42:foreign_physical_history")),
            new("primary_attempt", PlainHistoryRefusalSurface.History,
                root => HistoryRow(root, primary.TransitionId)["attemptId"] = "attempt_foreign_physical_history"),
            new("child_global_ordinal", PlainHistoryRefusalSurface.History, root =>
            {
                HistoryRow(root, child.TransitionId)["ordinal"] = checked(child.Ordinal + 1);
                root["nextOrdinal"] = checked(baseline.History.NextOrdinal + 1);
            }),
            new("child_terminal_fingerprint", PlainHistoryRefusalSurface.History,
                root => HistoryRow(root, child.TransitionId)["afterFingerprint"] = "sha256:" + new string('0', 64)),
            new("identity_owner", PlainHistoryRefusalSurface.WoundIdentity, root =>
                Assert.Single(root["entries"]!.AsArray().OfType<JsonObject>(),
                    entry => entry["woundId"]!.GetValue<string>() == primary.WoundId)["ownerId"] = "player_foreign_history"),
            new("allocation_event", PlainHistoryRefusalSurface.EffectIdentity,
                root => Allocation(root)["transitions"]![0]!["eventRef"] = "turn_42:foreign_history_allocation",
                RequireHistoricalRefusal: true),
            new("allocation_ordinal", PlainHistoryRefusalSurface.EffectIdentity,
                root => Allocation(root)["transitions"]![0]!["eventRef"] =
                    WoundEffectOperationEventRef.Create(primary.EventRef, 1, 2, "apply")),
            new("allocation_source", PlainHistoryRefusalSurface.EffectIdentity, root =>
            {
                var entry = Allocation(root);
                entry["source"]!["sourceId"] = "wound_foreign_history";
                entry["stackCoordinate"]!["sourceId"] = "wound_foreign_history";
            }),
            new("missing_historical_context", PlainHistoryRefusalSurface.History, root =>
            {
                var row = HistoryRow(root, primary.TransitionId);
                // A deliberately incomplete negative, never a positive producer
                // fixture: retain the real strict raw receipt but remove context.
                row["transitionResult"] = row["transitionResult"]!["terminal"]!.DeepClone();
            }, RequireHistoricalRefusal: true),
            new("resealed_publication_minute", PlainHistoryRefusalSurface.History, root =>
            {
                var result = HistoryRow(root, primary.TransitionId)["transitionResult"]!.AsObject();
                var originalPreparation = result["outcomePreparationFingerprint"]!.GetValue<string>();
                result["publicationMinute"] = checked(result["publicationMinute"]!.GetValue<long>() + 1);
                // Re-seal only the negative outer DATA envelope. The genuine
                // retained preparation comparator and raw receipt remain unchanged.
                result["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(result);
                Assert.Equal(originalPreparation, result["outcomePreparationFingerprint"]!.GetValue<string>());
            }, RequireParseRefusal: true)
        };
        Assert.Equal(11, cases.Length);
        var total = Stopwatch.StartNew();
        foreach (var test in cases)
        {
            var timer = Stopwatch.StartNew();
            var candidate = baseline;
            var changed = (test.Surface switch
            {
                PlainHistoryRefusalSurface.History => historyRoot,
                PlainHistoryRefusalSurface.WoundIdentity => identityRoot,
                PlainHistoryRefusalSurface.EffectIdentity => effectIdentityRoot,
                _ => throw new InvalidOperationException("Unknown plain History refusal surface.")
            }).DeepClone().AsObject();
            test.Mutate(changed);
            IReadOnlyList<ValidationIssue> parseIssues;
            var parsed = false;
            switch (test.Surface)
            {
                case PlainHistoryRefusalSurface.History:
                    var readHistory = WoundHistoryState.Parse(changed.ToJsonString(), WoundHistoryState.HistoryPath);
                    parseIssues = readHistory.Issues;
                    if (readHistory.IsValid)
                    {
                        candidate = baseline with { History = readHistory.State! };
                        parsed = true;
                    }
                    break;
                case PlainHistoryRefusalSurface.WoundIdentity:
                    var readIdentity = WoundIdentityState.Parse(changed.ToJsonString(), WoundIdentityState.StatePath);
                    parseIssues = readIdentity.Issues;
                    if (readIdentity.IsValid)
                    {
                        candidate = baseline with { Identities = readIdentity.State! };
                        parsed = true;
                    }
                    break;
                case PlainHistoryRefusalSurface.EffectIdentity:
                    var readEffect = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(changed), EffectIdentityState.StatePath);
                    parseIssues = readEffect.Issues;
                    if (readEffect.State is not null && readEffect.Issues.Count == 0)
                    {
                        // Carriers are unchanged. Reuse their already parsed
                        // mechanics DATA but rerun the changed index agreement.
                        var agreement = new List<ValidationIssue>();
                        ValidationService.ValidateEffectCarrierIndexAgreement(historicalEffects.Catalog, readEffect.State, agreement);
                        candidate = baseline with
                        {
                            Effects = agreement.Count == 0 ? baseline.Effects : baseline.Effects with { IsAccepted = false, Issues = agreement },
                            HistoricalEffects = new(historicalEffects.Catalog, readEffect.State)
                        };
                        parsed = true;
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unknown plain History refusal surface.");
            }
            if (!parsed)
            {
                Assert.NotEmpty(parseIssues);
                if (test.RequireParseRefusal)
                    Assert.Contains(parseIssues, issue => issue.Code == "mortal_wound_treatment_history_result_invalid");
                Assert.False(test.RequireHistoricalRefusal, test.Name + " must reach the historical reader, not stop at parsing.");
                output.WriteLine($"PLAIN_HISTORY refusal={test.Name} boundary=strict_parse elapsed_ms={timer.ElapsedMilliseconds} issues={DescribeIssues(parseIssues)}");
                continue;
            }
            Assert.Empty(parseIssues);
            Assert.False(test.RequireParseRefusal, test.Name + " must fail strict retained-preparation comparison.");
            if (test.RequireHistoricalRefusal)
            {
                Assert.True(candidate.Effects.IsAccepted, test.Name);
                Assert.Empty(candidate.History.ValidateAgreement(candidate.Identities, catalog));
                Assert.True(candidate.History.TryResolveExactTransition(primary.TransitionId, out var changedPrimary), test.Name);
                var changedResult = Assert.IsType<MortalWoundTreatmentPersistedResult>(changedPrimary!.TransitionResult);
                var changedEffects = Assert.IsType<WoundHistoryEffectData>(candidate.HistoricalEffects);
                Assert.False(MortalWoundTreatmentCompletion.TryReadHistoricalHeal(changedResult, changedPrimary,
                    candidate.History, candidate.Identities, catalog, changedEffects.Catalog, changedEffects.Identities,
                    out var before, out var after), test.Name);
                Assert.Null(before); Assert.Null(after);
            }
            var projected = WoundPlayerProjection.Build(candidate, catalog);
            Assert.Empty(projected.Active);
            Assert.Empty(projected.History);
            output.WriteLine($"PLAIN_HISTORY refusal={test.Name} boundary={(projected.IsAvailable ? "reader_empty" : "reader_unavailable")} historical_helper={test.RequireHistoricalRefusal} elapsed_ms={timer.ElapsedMilliseconds}");
        }
        Assert.Equal(historyBefore, historyRoot.ToJsonString());
        Assert.Equal(identityBefore, identityRoot.ToJsonString());
        Assert.Equal(effectIdentityBefore, effectIdentityRoot.ToJsonString());
        output.WriteLine($"PLAIN_HISTORY refusal_cases={cases.Length} elapsed_ms={total.ElapsedMilliseconds}");
    }

    private enum PlainHistoryRefusalSurface { History, WoundIdentity, EffectIdentity }
    private sealed record PlainHistoryRefusalCase(string Name, PlainHistoryRefusalSurface Surface,
        Action<JsonObject> Mutate, bool RequireHistoricalRefusal = false, bool RequireParseRefusal = false);
}
