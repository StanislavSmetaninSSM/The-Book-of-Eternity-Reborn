using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    /// <summary>
    /// Extends one genuinely composed positive prefix with explicitly synthetic
    /// archive audit DATA. This tests the supported History contract, not an
    /// actually accepted/published archive outcome or a new gameplay fixture.
    /// Existing carriers, effects and the independently observed visible entry
    /// are reused without another owner, filesystem, Engine or publication.
    /// </summary>
    /// <param name="baseline">The unchanged genuine positive projection inputs.</param>
    /// <param name="catalog">Their already validated current wound catalog.</param>
    /// <param name="expectedEntry">The visible entry already observed by the caller.</param>
    /// <param name="terminalBefore">The actual composed wound immediately before terminal heal.</param>
    private void AssertPlainTerminalHistoryAuditSuffix(WoundPlayerProjectionInput baseline,
        WoundCarrierCatalog catalog, WoundPlayerHistoryEntry expectedEntry, WoundMaterializationEnvelope terminalBefore)
    {
        var originalHistory = WoundHistoryState.SerializeCanonical(baseline.History);
        var originalIdentity = WoundIdentityState.SerializeCanonical(baseline.Identities);
        try
        {
            var primary = Assert.Single(baseline.History.Transitions, row => row.Kind == "treat");
            var heal = Assert.Single(baseline.History.ResolveWoundTransitions(primary.WoundId), row => row.Terminal);
            Assert.Equal("heal", heal.Kind);
            Assert.Equal("I", expectedEntry.Severity);
            Assert.Equal(1, terminalBefore.Severity.Rank);
            Assert.Equal(heal.BeforeFingerprint, WoundIdentityState.ComputeSemanticFingerprint(terminalBefore));
            Assert.True(baseline.Identities.TryGetEntry(heal.WoundId, out var oldIdentity));
            Assert.Equal("healed", oldIdentity.Status);
            Assert.Equal(heal.TransitionId, oldIdentity.TerminalTransitionId);
            Assert.Equal(heal.WoundTransitionOrdinal, oldIdentity.LastTransitionOrdinal);
            Assert.Equal(heal.AfterFingerprint, oldIdentity.SemanticFingerprint);

            const string transitionId = "synthetic_physical_history_archive_transition";
            const string operationKey = "synthetic_physical_history_archive_operation";
            const string eventRef = "synthetic_physical_history_archive_contract_data";
            const string summary = "Синтетическая архивная запись для проверки исторических данных.";
            Assert.DoesNotContain(baseline.History.Transitions, row => row.TransitionId == transitionId || row.OperationKey == operationKey);
            var intent = new WoundTransitionHistoryIntent(transitionId, heal.WoundId, "archive", operationKey,
                eventRef, checked(baseline.History.Transitions[^1].Turn + 1), heal.AfterFingerprint, heal.AfterFingerprint,
                AttemptId: null, TickKey: null, Terminal: false);
            // The suffix is deliberately synthetic DATA. Reusing the terminal
            // fingerprint as its input digest grants no provenance or authority.
            var appended = baseline.History.AppendTransition(intent, heal.AfterFingerprint,
                WoundHistoryState.ComputeOutputFingerprint(operationKey, eventRef, summary), summary);
            Assert.True(appended.IsValid, DescribeIssues(appended.Issues));
            var suffixJson = WoundHistoryState.SerializeCanonical(appended.State!);
            var parsedHistory = WoundHistoryState.Parse(suffixJson, WoundHistoryState.HistoryPath);
            Assert.True(parsedHistory.IsValid, DescribeIssues(parsedHistory.Issues));
            var history = parsedHistory.State!;
            var audit = history.Transitions[^1];
            Assert.Equal(transitionId, audit.TransitionId);
            Assert.Equal("archive", audit.Kind);
            Assert.False(audit.Terminal);
            Assert.Null(audit.TransitionResult);
            Assert.Equal(baseline.History.NextOrdinal, audit.Ordinal);
            Assert.Equal(checked(baseline.History.NextOrdinal + 1), history.NextOrdinal);
            Assert.Equal(checked(heal.WoundTransitionOrdinal + 1), audit.WoundTransitionOrdinal);
            Assert.Equal(heal.AfterFingerprint, audit.BeforeFingerprint);
            Assert.Equal(heal.AfterFingerprint, audit.AfterFingerprint);
            var originalRows = JsonNode.Parse(originalHistory)!["transitions"]!.AsArray();
            var appendedRows = JsonNode.Parse(suffixJson)!["transitions"]!.AsArray();
            Assert.Equal(originalRows.Count + 1, appendedRows.Count);
            for (var index = 0; index < originalRows.Count; index++)
                Assert.True(JsonNode.DeepEquals(originalRows[index], appendedRows[index]));

            var identityJson = JsonNode.Parse(originalIdentity)!.AsObject();
            var entryJson = Assert.Single(identityJson["entries"]!.AsArray().OfType<JsonObject>(),
                row => row["woundId"]!.GetValue<string>() == heal.WoundId);
            entryJson["lastTransitionOrdinal"] = audit.WoundTransitionOrdinal;
            var parsedIdentity = WoundIdentityState.Parse(identityJson.ToJsonString(), WoundIdentityState.StatePath);
            Assert.True(parsedIdentity.IsValid, DescribeIssues(parsedIdentity.Issues));
            var identities = parsedIdentity.State!;
            Assert.True(identities.TryGetEntry(heal.WoundId, out var latestIdentity));
            Assert.Equal(oldIdentity with { LastTransitionOrdinal = audit.WoundTransitionOrdinal }, latestIdentity);
            Assert.Equal(heal.TransitionId, latestIdentity.TerminalTransitionId);
            Assert.Equal(heal.AfterFingerprint, latestIdentity.SemanticFingerprint);
            Assert.Empty(history.ValidateAgreement(identities, catalog));

            void RefuseSuffix(string name, Action<JsonObject> mutate, string expectedCode)
            {
                var changed = JsonNode.Parse(suffixJson)!.AsObject();
                mutate(changed["transitions"]!.AsArray()[^1]!.AsObject());
                var refused = WoundHistoryState.Parse(changed.ToJsonString(), WoundHistoryState.HistoryPath);
                Assert.False(refused.IsValid, name);
                Assert.Contains(refused.Issues, issue => issue.Code == expectedCode);
                output.WriteLine("PLAIN_HISTORY synthetic_archive_contract_data refusal=" + name + " boundary=strict_parse");
            }
            var changedFingerprint = "sha256:" + new string('0', 64);
            Assert.NotEqual(heal.AfterFingerprint, changedFingerprint);
            RefuseSuffix("changed_terminal_fingerprint", row => row["afterFingerprint"] = changedFingerprint,
                "wound_history_terminal_state_changed");
            RefuseSuffix("forbidden_post_terminal_kind", row => row["kind"] = "recover",
                "wound_history_transition_after_terminal");
            output.WriteLine("PLAIN_HISTORY synthetic_archive_contract_data: preserved genuine prefix; strict History/identity parse, " +
                "latest ordinal/terminal identity/carrier agreement and two refusal controls passed; suffix is not published outcome evidence.");

            var candidate = baseline with { History = history, Identities = identities };
            var projected = WoundPlayerProjection.Build(candidate, catalog);
            Assert.True(projected.IsAvailable);
            Assert.Empty(projected.Active);
            Assert.True(projected.History.Count == 1,
                "PHYSICAL_HISTORY_ARCHIVE_CAUSAL: supported nonterminal archive DATA hid an otherwise proven healed History entry.");
            Assert.Equal(expectedEntry, Assert.Single(projected.History));
            Assert.True(history.TryResolveExactTransition(primary.TransitionId, out var exactPrimary));
            var result = Assert.IsType<MortalWoundTreatmentPersistedResult>(exactPrimary!.TransitionResult);
            var effects = Assert.IsType<WoundHistoryEffectData>(candidate.HistoricalEffects);
            Assert.True(MortalWoundTreatmentCompletion.TryReadHistoricalHeal(result, exactPrimary, history, identities,
                catalog, effects.Catalog, effects.Identities, out var before, out var after));
            Assert.NotNull(before); Assert.NotNull(after);
            Assert.Equal(1, before.Severity.Rank);
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(terminalBefore),
                WoundMaterializationContract.SerializeCanonical(before));
            Assert.Equal(heal.TransitionId, after.LastTransition.TransitionId);
            Assert.Equal(heal.WoundTransitionOrdinal, after.LastTransition.Ordinal);
            Assert.Equal(heal.AfterFingerprint, WoundIdentityState.ComputeSemanticFingerprint(after));
        }
        finally
        {
            Assert.Equal(originalHistory, WoundHistoryState.SerializeCanonical(baseline.History));
            Assert.Equal(originalIdentity, WoundIdentityState.SerializeCanonical(baseline.Identities));
        }
    }
}
