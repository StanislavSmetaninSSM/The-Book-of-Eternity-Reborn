using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    // Consume the actual typed pair already published by each required native44
    // case. Strict result construction owns signed preparation and executed
    // boundaries; a second hand-written result builder would mirror that work.
    // Everything below is detached DATA: no second fixture, source read or write.
    private static void AssertShiningPatientSerializedHistory(WoundHistoryState history, string kind)
    {
        var serialized = WoundHistoryState.SerializeCanonical(history);
        var root = JsonNode.Parse(serialized)!.AsObject();
        var results = history.Transitions.Where(row => row.TransitionResult is SpiritualHealingPersistedResult).ToArray();
        var grantRow = Assert.Single(results, row => ((SpiritualHealingPersistedResult)row.TransitionResult!).IsNatural);
        var activeRow = Assert.Single(results, row => !((SpiritualHealingPersistedResult)row.TransitionResult!).IsNatural);
        var grant = Assert.IsType<SpiritualHealingPersistedResult>(grantRow.TransitionResult);
        var active = Assert.IsType<SpiritualHealingPersistedResult>(activeRow.TransitionResult);
        Assert.Equal(8, grant.SchemaVersion); Assert.Equal(8, active.SchemaVersion);
        Assert.Equal("aid", active.Mode); Assert.True(active.AidGrantAgrees(grant));
        Assert.Equal(kind, SpiritualTreatmentParticipation.RecordOwner(grant.ShiningPatientParticipationRecord!).OwnerKind);

        foreach (var result in new[] { grant, active })
        {
            var issues = new List<ValidationIssue>();
            var parsed = Assert.IsType<SpiritualHealingPersistedResult>(SpiritualHealingPersistedResult.Parse(
                JsonSerializer.SerializeToElement(result.ToCanonicalJson()), "shining.patient.standalone", issues));
            Assert.Empty(issues);
            Assert.Equal(result.ResultFingerprint, parsed.ResultFingerprint);
        }
        var roundTrip = WoundHistoryState.Parse(serialized, "shining.patient.history");
        Assert.True(roundTrip.IsValid, string.Join("; ", roundTrip.Issues.Select(issue => issue.Code + ":" + issue.Actual)));
        var restored = Assert.IsType<WoundHistoryState>(roundTrip.State);
        Assert.Equal(serialized, WoundHistoryState.SerializeCanonical(restored));
        foreach (var row in restored.Transitions)
        {
            var replay = restored.ResolveReplay(WoundHistoryState.CreateReplayProbe(row));
            Assert.Equal(WoundHistoryReplayDisposition.Exact, replay.Disposition);
            Assert.Same(row, replay.Transition); Assert.Empty(replay.Issues);
        }
        var conflict = restored.ResolveReplay(WoundHistoryState.CreateReplayProbe(activeRow) with
        { OutputFingerprint = DifferentFingerprint(activeRow.OutputFingerprint) });
        Assert.Equal(WoundHistoryReplayDisposition.Conflict, conflict.Disposition);
        Assert.Contains(conflict.Issues, issue => issue.Code == "wound_history_conflicting_replay");

        JsonObject Copy() => root.DeepClone().AsObject();
        static JsonObject Row(JsonObject candidate, string transitionId) => candidate["transitions"]!.AsArray()
            .OfType<JsonObject>().Single(row => row["transitionId"]!.GetValue<string>() == transitionId);
        static WoundHistoryParseResult Refused(JsonObject candidate, string control)
        {
            var parsed = WoundHistoryState.Parse(candidate.ToJsonString(), "shining.patient.history." + control);
            Assert.False(parsed.IsValid); Assert.Null(parsed.State);
            return parsed;
        }

        var missing = Copy();
        // Omission preserves every stage, ordinal and before/after chain. Explicit
        // null would instead invoke the strict recovery-result shape reader.
        Assert.True(Row(missing, grantRow.TransitionId).Remove("transitionResult"));
        Assert.Contains(Refused(missing, "missing_grant").Issues,
            issue => issue.Code == "spiritual_aid_grant_history_invalid");

        var duplicate = Copy();
        var extraGrant = Row(duplicate, grantRow.TransitionId).DeepClone().AsObject();
        extraGrant["ordinal"] = history.NextOrdinal;
        duplicate["transitions"]!.AsArray().Add(extraGrant);
        duplicate["nextOrdinal"] = checked(history.NextOrdinal + 1);
        // Generic duplicate protection runs before grant indexing. Do not claim
        // this malformed cohort reached the exact-one grant comparison.
        Assert.Contains(Refused(duplicate, "duplicate_grant").Issues,
            issue => issue.Code == "wound_history_duplicate_transition_id");

        var wrongFamily = Copy();
        // The unchanged, independently parsed natural result cannot replace an
        // active primary result: its family belongs to a recover row, not treat.
        Row(wrongFamily, activeRow.TransitionId)["transitionResult"] = grant.ToCanonicalJson();
        Assert.Contains(Refused(wrongFamily, "wrong_primary_family").Issues,
            issue => issue.Code == "spiritual_healing_history_invalid" && issue.Actual == "treat");

        var fingerprint = Copy();
        Row(fingerprint, activeRow.TransitionId)["transitionResult"]!["resultFingerprint"] = DifferentFingerprint(active.ResultFingerprint);
        Assert.Contains(Refused(fingerprint, "result_fingerprint").Issues,
            issue => issue.Code == "spiritual_healing_history_invalid" &&
                issue.Actual == "Spiritual result kind/schema/fingerprint differs.");

        Assert.Equal(serialized, WoundHistoryState.SerializeCanonical(history));
        Assert.Equal(serialized, WoundHistoryState.SerializeCanonical(restored));
        Assert.True(active.AidGrantAgrees(grant));
    }

    private static string DifferentFingerprint(string fingerprint) =>
        fingerprint[..^1] + (fingerprint[^1] == '0' ? "1" : "0");
}
