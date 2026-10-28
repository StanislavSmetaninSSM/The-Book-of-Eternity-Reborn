using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualTreatmentParticipationTests
{
    [Theory]
    [InlineData("profiles")]
    [InlineData("afterlifeEntityProfiles")]
    [InlineData("afterlifeEntityProfileUpdates")]
    public void EveryProfileRepresentationUsesTheSameProposalAndPreservesTheOwnedRecord(string property)
    {
        var previous = Previous();
        // Existing command identity supports actorRef. Realm comes from the
        // unchanged preexisting profile for this domain check, not an alias.
        var response = new JsonObject
        {
            ["actorType"] = "guardian", ["actorRef"] = "guardian_aid",
            [SpiritualTreatmentParticipation.ChangeProperty] = Proposal()
        };
        var current = new JsonObject { [property] = new JsonArray(response) };
        var change = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(current, previous));
        Assert.Equal("guardian_aid", change.TargetId);
        var projected = AfterlifeEntityProfileState.ProjectCanonicalRoot(current, previous);
        var profile = Assert.Single(projected["profiles"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(previous["profiles"]![0]![SpiritualTreatmentParticipation.RecordProperty],
            profile![SpiritualTreatmentParticipation.RecordProperty]));
        Assert.True(JsonNode.DeepEquals(change.Proposal, profile[SpiritualTreatmentParticipation.ChangeProperty]));
        // Three identical mirrors are one intent; projection must not lose it.
        foreach (var mirror in new[] { "profiles", "afterlifeEntityProfiles", "afterlifeEntityProfileUpdates" })
            current[mirror] = new JsonArray(response.DeepClone());
        Assert.Single(SpiritualTreatmentParticipation.ReadChanges(current, previous));
        Assert.NotNull(AfterlifeEntityProfileState.ProjectCanonicalRoot(current, previous)["profiles"]![0]![SpiritualTreatmentParticipation.RecordProperty]);
    }

    [Fact]
    public void RawStampForgeryConflictingMirrorsAndIncapableGrantNeverBecomeParticipation()
    {
        var previous = Previous();
        var row = previous["profiles"]![0]!.DeepClone().AsObject();
        var current = new JsonObject { ["profiles"] = new JsonArray(row) };
        Assert.Empty(SpiritualTreatmentParticipation.ReadChanges(current, previous));
        row[SpiritualTreatmentParticipation.RecordProperty]!["readableEvidence"] = "Изменённый текст с прежним принятым tuple.";
        Assert.Contains("cannot mint or change", Assert.Throws<InvalidDataException>(() =>
            SpiritualTreatmentParticipation.ReadChanges(current, previous)).Message);
        row[SpiritualTreatmentParticipation.RecordProperty] = null;
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ReadChanges(current, previous));
        row.Remove(SpiritualTreatmentParticipation.RecordProperty);
        row[SpiritualTreatmentParticipation.ChangeProperty] = Proposal();
        var mirror = row.DeepClone().AsObject();
        mirror[SpiritualTreatmentParticipation.ChangeProperty]!["operation"] = "revoke";
        current["afterlifeEntityProfileUpdates"] = new JsonArray(mirror);
        Assert.Contains("conflicting", Assert.Throws<InvalidDataException>(() =>
            SpiritualTreatmentParticipation.ReadChanges(current, previous)).Message);
        current.Remove("afterlifeEntityProfileUpdates");
        row[SpiritualTreatmentParticipation.ChangeProperty]!["capable"] = false;
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ReadChanges(current, previous));
        row[SpiritualTreatmentParticipation.ChangeProperty]!["operation"] = "revoke";
        Assert.Single(SpiritualTreatmentParticipation.ReadChanges(current, previous));
    }

    private static JsonObject Proposal() => new()
    {
        ["schemaVersion"] = 1, ["operation"] = "grant", ["healerId"] = "player_soul", ["woundId"] = "wound_aid",
        ["conscious"] = true, ["capable"] = true, ["readableEvidence"] = "Хранитель явно согласен на помощь этой души."
    };

    private static JsonObject Previous()
    {
        // Closed canonical DATA for projection/forgery controls; this helper does
        // not establish actual accepted permission. The publisher test does.
        var binding = new JsonObject { ["sessionId"] = "session_aid", ["requestId"] = "request_aid",
            ["snapshotToken"] = "snapshot_aid", ["realm"] = "chaos_sea", ["turn"] = 42 };
        var outcome = SpiritualTreatmentParticipation.CreateOutcome(new("guardian_aid", Proposal()), "abode_aid", binding, 1, "transition_aid");
        return new() { ["schemaVersion"] = 1, ["profiles"] = new JsonArray(new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian_aid", ["realm"] = "Chaos Sea",
            [SpiritualTreatmentParticipation.RecordProperty] = outcome["record"]!.DeepClone()
        }) };
    }
}
