using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Runs coherent detached mutations before any late hook, preserving all original exceptions and exact named refusal guards.</summary>
    private static int AssertGuardianCappedRetryProbes(GuardianAttributionJourney setup, GuardianCappedRetryAttemptData first, Action<string> output)
    { _ = ReadGuardianCappedRetryAttempt(setup, first.EnvelopeJson); return AssertGuardianCappedRetryProbes(first, output); }

    /// <summary>Exercises pure reader/graph guards on one pinned baseline with per-probe independent mutable copies, never live fixtures.</summary>
    private static int AssertGuardianCappedRetryProbes(GuardianCappedRetryAttemptData first, Action<string> output)
    {
        first = ValidateGuardianCappedReportedAttempt(first); var count = 0; var zero = "sha256:" + new string('0', 64);
        Probe("unknown top field", "envelope-schema", json => EditOuter(json, root => root["unexpectedCappedRetryField"] = true));
        Probe("missing explicit null", "envelope-schema", json => EditOuter(json, root => root["Plan"]!.AsObject().Remove("PendingPublicationAuthority")));
        Probe("authority entry disagrees with its typed record despite regenerated prepared operands", "authority-entry-producer-join", json => Reseal(EditOuter(json, root => root["Plan"]!["AuthorityFingerprintEntries"]![0]!["Value"] = zero), first));
        Probe("missing completed journal row", "journal-shape-and-prefix", json => EditOuter(json, root => root["Completed"]!["AllocationJournal"]!.AsArray().RemoveAt(4)));
        Probe("duplicate generated allocation with every copy resealed", "journal-uniqueness", json => Reseal(PatchSymbol(json, first, "A4", first.Symbols["A0"]), first));
        Probe("fifth allocation causal coordinate in every journal copy", "effect-allocation", json => Reseal(Patch(json, row => row.Property == "coordinate" && row.Path.EndsWith("/allocations/4/coordinate", StringComparison.Ordinal) || row.Path == "/Completed/AllocationJournal/4/coordinate" ? zero : null), first));
        Probe("cross-side spent/depleted event operation with source and packet descendants resealed", "resource-events-and-order", json =>
            Reseal(Patch(json, row => row.Value == first.Symbols["A0"] && row.Property == "OperationId" && (row.Path.Contains("/Events/", StringComparison.Ordinal) || row.Path.StartsWith("/Plan/ResourceEvents/", StringComparison.Ordinal)) ? first.Symbols["A2"] : null), first));
        Probe("cross-side current State chronology with candidate images and packet descendants resealed", "resource-state-and-history", json =>
            Reseal(Patch(json, row => row.Value == first.Symbols["A1"] && row.Property == "lastTransitionId" ? first.Symbols["A3"] : null), first));
        Probe("candidate State differs from genuine resource producer with all packet descendants resealed", "positive-packet-checkpoint-none", json =>
        {
            var imagePath = "/candidateAfterImages/";
            var changed = new GuardianCappedRetryDocument(json).Patch(row =>
            {
                if (!row.Path.Contains(imagePath, StringComparison.Ordinal) || row.Property != "contentBase64" || row.Embedded?.Root is not JsonObject payload || payload["entries"] is not JsonArray entries || entries.Count != 2 || entries[0]?["current"] is null) return null;
                var edited = payload.DeepClone(); edited["entries"]![0]!["current"] = 1;
                return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(edited.ToJsonString()));
            }, allowContainerReplacement: true);
            return Reseal(changed, first);
        });
        Probe("wrong current guardian target in full effect and lifecycle copies with payload descendants resealed", "guardian-carrier-history", json =>
            Reseal(Patch(json, row => row.Property == "targetId" && row.Value == "guardian_frame" && (row.Path.Contains("/ActiveEffects/", StringComparison.Ordinal) || row.Path.Contains("/CarrierAfterImages/", StringComparison.Ordinal) || row.Path.Contains("/EffectCarrierAfterImages/", StringComparison.Ordinal) || row.Path.Contains("/ResourceTriggerCarriers/AfterlifeProfiles/", StringComparison.Ordinal) || row.Path.Contains("/LifecycleExecutionLedger/passes/1/", StringComparison.Ordinal)) ? "guardian_other" : null), first));
        Probe("wrong new effect transition kind in every current identity copy with payload resealed", "effect-identity-current", json => Reseal(EditOuter(json, root =>
        {
            foreach (var identity in new[] { root["Plan"]!["EffectIdentityAfterImage"]!, root["Plan"]!["EffectPlan"]!["IdentityIndexAfterImage"]!, root["Completed"]!["Reduction"]!["EffectPlan"]!["IdentityIndexAfterImage"]! })
            { var transitions = identity["entries"]![0]!["transitions"]!.AsArray(); transitions[^1]!["kind"] = "expire"; }
        }), first));
        Probe("final lifecycle guardian roster moved to opposition with ledger digest resealed", "lifecycle-event-placement", json => EditOuter(json, root =>
        {
            foreach (var effect in new[] { root["Plan"]!["EffectPlan"]!, root["Completed"]!["Reduction"]!["EffectPlan"]! })
            {
                var events = effect["LifecycleExecutionLedger"]!["passes"]![1]!["events"]!;
                foreach (var field in new[] { "roster", "subjects", "rosterRef", "disposition" })
                { var a = events[0]![field]!.DeepClone(); var b = events[1]![field]!.DeepClone(); events[0]![field] = b; events[1]![field] = a; }
                effect["LifecycleExecutionLedgerFingerprint"] = BookOfEternityClient.Services.WoundAcceptedTurnFingerprintWriter.Compute(new[] { "book_of_eternity.effect.lifecycle_execution_ledger", "1", BookOfEternityClient.Services.WoundAcceptedTurnFingerprintWriter.CanonicalJson(effect["LifecycleExecutionLedger"]) });
            }
        }));
        Probe("unrelated player strain in full original root", "fixture-raw-invariants", json => EditOuter(json, root => root["First"]!["actualRoot"]!["activeConflict"]!["playerSideStrain"] = "strained"));
        Probe("exact embedded ordinary JSON whitespace", "fixture-raw-invariants", json => EditOuter(json, root => root["First"]!["Source"]!["ExchangeJson"] = root["First"]!["Source"]!["ExchangeJson"]!.GetValue<string>() + " "));
        Probe("closed resource claim with all dependent copies and digests resealed", "closed-resource-hash", json => Reseal(PatchSymbol(json, first, "resourceResult", zero), first, "resourceResult"));
        Probe("closed effect claim with all dependent copies and digests resealed", "closed-effect-hash", json => Reseal(PatchSymbol(json, first, "closedEffectPrefix", zero), first, "closedEffectPrefix"));
        Probe("source digest with every declared source copy changed", "source-hash", json => Reseal(PatchSymbol(json, first, "sourceFingerprint", zero), first, "sourceFingerprint"));
        Probe("full effect payload claim with terminal and lifecycle descendants resealed", "effect-payload-hash", json => Reseal(PatchSymbol(json, first, "finalEffectPayload", zero), first, "finalEffectPayload"));
        Probe("common prepared claim", "prepared-plan-hash", json => PatchSymbol(json, first, "preparedPlan", zero));
        Probe("new generated reference hidden in sceneId with event artifact copies resealed", "effect-event-input", json => Reseal(EditOuter(json, root =>
        {
            foreach (var effect in new[] { root["Plan"]!["EffectPlan"]!, root["Completed"]!["Reduction"]!["EffectPlan"]! })
            {
                effect["EventInput"]!["sceneId"] = first.Allocations[0]; var eventInputRef = GuardianCappedLifecycleArtifact("event_input", effect["EventInput"]);
                effect["LifecycleExecutionLedger"]!["eventInputRef"] = eventInputRef;
                foreach (var pass in effect["LifecycleExecutionLedger"]!["passes"]!.AsArray()) pass!["eventInputRef"] = eventInputRef;
            }
        }), first));
        Probe("current registered seal false", "terminal-current-observations", json => EditOuter(json, root => root["CurrentTerminalSeals"]!["validSeal"] = false));
        var duplicate = first.EnvelopeJson.Insert(first.EnvelopeJson.IndexOf('{') + 1, "\"Rank\":" + first.Fixture.Rank + ",");
        Reject("duplicate outer raw key", "envelope-json", duplicate);
        var forged = first with { EnvelopeJson = EditOuter(first.EnvelopeJson, root => root["First"]!["actualRoot"]!["activeConflict"]!["playerSideStrain"] = "strained") };
        var failure = Record.Exception(() => ValidateGuardianCappedReportedAttempt(forged));
        output(JsonSerializer.Serialize(new { phase = "guardian_capped_retry_detached_probe", name = "forged envelope with stale reported graph", expectedGuard = "fixture-raw-invariants", failure = failure?.ToString(), diagnostics = ReadGuardianCappedExceptionData(failure) }));
        Assert.Equal("fixture-raw-invariants", Assert.IsType<GuardianCappedRetryGuardFailure>(failure).Guard); count++;
        // Detached positive control exercises the exact comparer before a real late hook can be armed.
        var renamedValues = first.Allocations.Select((value, index) => value[..(value.Length - 32)] + new string((char)('a' + index), 32)).ToArray();
        Assert.Equal(first.Allocations.Length + renamedValues.Length, first.Allocations.Concat(renamedValues).Distinct(StringComparer.Ordinal).Count());
        var renamedJson = Patch(first.EnvelopeJson, row => first.Bindings.TryGetValue(row.Path, out var symbol) && symbol.Length == 2 && symbol[0] == 'A' ? renamedValues[symbol[1] - '0'] : null);
        renamedJson = Reseal(renamedJson, first); var renamed = ReadGuardianCappedRetryAttempt(first.Fixture, renamedJson);
        AssertGuardianCappedGraphEquivalent(first, renamed);
        output(JsonSerializer.Serialize(new { phase = "guardian_capped_retry_detached_positive_renaming", allocationCount = renamed.Allocations.Length, renamed.Symbols.Count, bindingCount = renamed.Bindings.Count, liveRetry = false }));
        var rawChanged = renamed with { EnvelopeJson = EditOuter(renamed.EnvelopeJson, root => root["First"]!["Source"]!["ExchangeJson"] = root["First"]!["Source"]!["ExchangeJson"]!.GetValue<string>() + " ") };
        var rawComparisonFailure = Record.Exception(() => AssertGuardianCappedGraphEquivalent(first, rawChanged));
        Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(rawComparisonFailure);
        output(JsonSerializer.Serialize(new { phase = "guardian_capped_retry_detached_raw_comparison_refusal", failure = rawComparisonFailure!.ToString(), liveRetry = false }));
        // Positive identity patch witness: no-op patch retains every lexical surface byte, including BOM and embedded escaping.
        Assert.Equal(first.EnvelopeJson, new GuardianCappedRetryDocument(first.EnvelopeJson).Patch(_ => null));
        Assert.Equal(23, count); return count;

        void Probe(string name, string expectedGuard, Func<string, string> change)
        {
            var started = Stopwatch.GetTimestamp(); string changed;
            try { changed = change(first.EnvelopeJson); }
            catch (Exception preparationFailure)
            {
                output(JsonSerializer.Serialize(new { phase = "guardian_capped_retry_detached_probe_preparation_failure", name, expectedGuard, failure = preparationFailure.ToString(), diagnostics = ReadGuardianCappedExceptionData(preparationFailure) })); throw;
            }
            Assert.NotEqual(first.EnvelopeJson, changed); Reject(name, expectedGuard, changed);
            output(JsonSerializer.Serialize(new { phase = "guardian_capped_retry_detached_probe_timing", name, elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds }));
        }
        void Reject(string name, string expectedGuard, string changed)
        {
            var rejected = Record.Exception(() => ReadGuardianCappedRetryAttempt(first.Fixture, changed));
            output(JsonSerializer.Serialize(new { phase = "guardian_capped_retry_detached_probe", name, expectedGuard, failure = rejected?.ToString(), diagnostics = ReadGuardianCappedExceptionData(rejected) }));
            var guarded = Assert.IsType<GuardianCappedRetryGuardFailure>(rejected); Assert.Equal(expectedGuard, guarded.Guard); Assert.NotNull(guarded.InnerException); count++;
        }
    }

    /// <summary>Reseals complete dependent copies at already checked graph locations, never by global substring replacement or an unrelated seal bypass.</summary>
    private static string Reseal(string json, GuardianCappedRetryAttemptData baseline, params string[] keepDefective)
    {
        var previous = json;
        // The finite dependency DAG bounds convergence; each round advances at least one derived layer or fails explicitly.
        for (var round = 0; round <= baseline.Symbols.Count; round++)
        {
            var document = new GuardianCappedRetryDocument(previous); var hashes = ReadGuardianCappedRetryHashes(document);
            var computed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var hash in hashes)
                if (baseline.Bindings.TryGetValue(hash.Path, out var symbol) && !keepDefective.Contains(symbol, StringComparer.Ordinal))
                {
                    if (computed.TryGetValue(symbol, out var prior)) Assert.Equal(prior, hash.Calculated);
                    else computed.Add(symbol, hash.Calculated);
                }
            var changed = document.Patch(row => baseline.Bindings.TryGetValue(row.Path, out var symbol) && computed.TryGetValue(symbol, out var value) ? value : null);
            changed = RefreshGuardianCappedRetryOperands(changed);
            if (changed == previous) return changed;
            previous = changed;
        }
        throw new InvalidOperationException("Coherent capped probe digest DAG did not converge.");
    }

    /// <summary>Regenerates captured ordered operand arrays from the same complete detached producers after a coherent semantic mutation.</summary>
    private static string RefreshGuardianCappedRetryOperands(string json)
    {
        var root = new GuardianCappedRetryDocument(json).Root.AsObject(); var effects = root["Plan"]!["EffectPlan"]!.AsObject();
        var effect = JsonSerializer.SerializeToNode(ReadGuardianCappedRetryEffectPayloadHashFields(effects));
        root["EffectPayloadHashOperands"] = effect; root["Completed"]!["Reduction"]!["EffectPayloadHashOperands"] = effect!.DeepClone();
        root["PreparedPlanHashOperands"] = JsonSerializer.SerializeToNode(ReadGuardianCappedRetryPreparedPlanHashFields(root, effects["AcceptedBoundaryFinalPlanFingerprint"]!.GetValue<string>()));
        return root.ToJsonString();
    }

    /// <summary>Changes only exact tokens at previously checked semantic graph locations.</summary>
    private static string PatchSymbol(string json, GuardianCappedRetryAttemptData baseline, string symbol, string value) => Patch(json, row => baseline.Bindings.TryGetValue(row.Path, out var bound) && bound == symbol ? value : null);
    /// <summary>Applies a structural lexical mutation through every JSON/base64 representation without normalizing any other bytes.</summary>
    private static string Patch(string json, Func<GuardianCappedRetryScalar, string?> change) => new GuardianCappedRetryDocument(json).Patch(change);
    /// <summary>Edits only the outer envelope tree while retaining all embedded raw string DATA untouched.</summary>
    private static string EditOuter(string json, Action<JsonObject> edit) { var root = new GuardianCappedRetryDocument(json).Root.AsObject(); edit(root); return root.ToJsonString(); }
    /// <summary>Projects all exception Data entries recursively; ToString separately preserves every inner exception and stack.</summary>
    private static object? ReadGuardianCappedExceptionData(Exception? failure) => failure is null ? null : new { entries = failure.Data.Cast<System.Collections.DictionaryEntry>().Select(row => new { key = row.Key?.ToString(), value = row.Value?.ToString() }).ToArray(), inner = ReadGuardianCappedExceptionData(failure.InnerException) };
}
