using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningPatientMemoryProgramTests
{
    [Theory]
    [InlineData("resident")][InlineData("radiant_actor")]
    public void MemoryProgramsComposeDisjointAppendsWithAutoProgressionDisabled(string kind)
    {
        var (input, owner, locality) = ShiningPatientTreatmentAdmissionTests.Inputs(kind); AddMemorySupport(input, "support_memory");
        foreach (var profile in input.Profiles["profiles"]!.AsArray().OfType<JsonObject>())
            profile["progressionStrategy"]!["autoProgressionEnabled"] = false;
        input.Residents["thoughtJournal"] = new JsonArray(MemoryRow("support_memory", "old_memory", 42));
        var ordinary = new JsonArray(MemoryRow("support_memory", "ordinary_memory"));
        var protectedRows = kind == "resident" ? new JsonArray(MemoryRow(owner.OwnerId, "patient_memory")) : null;
        var radiant = kind == "radiant_actor" ? RadiantMemory() : null;
        var frozenProfiles = input.Profiles.ToJsonString(); var frozenResidents = input.Residents.ToJsonString(); var frozenShining = input.Shining.ToJsonString();
        var program = SpiritualTreatmentParticipation.PrepareShiningPatientMemory(owner, locality, input.Profiles,
            input.Residents, input.Shining, 43, ordinary, protectedRows, radiant);
        ordinary[0]!["summary"] = "Подмена caller-owned DATA.";
        if (radiant is not null) radiant["summary"] = "Подмена caller-owned DATA.";
        var projected = program.ProjectResidents(input.Residents);
        GuardianAbodeResidentState.ApplyThoughtJournalUpdates(projected, projected[GuardianAbodeResidentState.UpdateThoughtJournalProperty]!.AsArray());
        projected.Remove(GuardianAbodeResidentState.UpdateThoughtJournalProperty);
        var journal = projected["thoughtJournal"]!.AsArray();
        Assert.Equal(kind == "resident" ? 3 : 2, journal.Count);
        Assert.Equal("old_memory", journal[0]!["entryId"]!.GetValue<string>());
        Assert.Equal("ordinary_memory", journal[1]!["entryId"]!.GetValue<string>());
        Assert.DoesNotContain("Подмена", journal.ToJsonString());
        program.RequireResidentAfterImage(input.Residents, projected);
        Assert.True(JsonNode.DeepEquals(projected, program.ProjectResidents(projected)));
        projected["thoughtJournal"]![0]!["summary"] = "Rewritten old entry";
        Assert.Throws<InvalidDataException>(() => program.ProjectResidents(projected));
        var profiles = input.Profiles.DeepClone().AsObject(); program.ApplyProfiles(profiles);
        var once = profiles.DeepClone(); program.ApplyProfiles(profiles); Assert.True(JsonNode.DeepEquals(once, profiles));
        if (kind == "radiant_actor")
        {
            var profile = profiles["profiles"]![0]!;
            Assert.Equal("radiant_memory", Assert.Single(profile["ledger"]!.AsArray())!["entryId"]!.GetValue<string>());
            Assert.Equal("Я принимаю помощь.", profile["gmThoughtsSummary"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(input.Profiles["profiles"]![0]!["currencies"], profile["currencies"]));
            profile["ledger"]![0]!["summary"] = "Different content under the same ID";
            Assert.Throws<InvalidDataException>(() => program.ApplyProfiles(profiles));
        }
        Assert.Equal(frozenProfiles, input.Profiles.ToJsonString()); Assert.Equal(frozenResidents, input.Residents.ToJsonString()); Assert.Equal(frozenShining, input.Shining.ToJsonString());
    }

    [Theory]
    [InlineData("selected")][InlineData("absent")][InlineData("nonlocal")][InlineData("stale")]
    [InlineData("old_id")][InlineData("cross_set_id")][InlineData("extra")][InlineData("wrong_kind")]
    public void MemoryProgramsRefuseWrongOwnerLocationTurnAndGlobalIdCollisions(string change)
    {
        var (input, owner, locality) = ShiningPatientTreatmentAdmissionTests.Inputs("resident"); AddMemorySupport(input, "support_memory");
        var ordinary = new JsonArray(MemoryRow("support_memory", "ordinary_memory"));
        var protectedRows = new JsonArray(MemoryRow(owner.OwnerId, "patient_memory"));
        _ = SpiritualTreatmentParticipation.PrepareShiningPatientMemory(owner, locality, input.Profiles, input.Residents, input.Shining, 43, ordinary, protectedRows, null);
        switch (change)
        {
            case "selected": ordinary[0]!["residentId"] = owner.OwnerId; break;
            case "absent": ordinary[0]!["residentId"] = "absent_resident"; break;
            case "nonlocal": input.Profiles["profiles"]![1]!["locationId"] = "hall_foreign"; break;
            case "stale": ordinary[0]!["turn"] = 42; break;
            case "old_id": input.Residents["thoughtJournal"] = new JsonArray(MemoryRow("support_memory", "ordinary_memory", 42)); break;
            case "cross_set_id": protectedRows[0]!["entryId"] = "ordinary_memory"; break;
            case "extra": ordinary[0]!["foreignField"] = true; break;
        }
        var frozen = input.Profiles.ToJsonString() + input.Residents.ToJsonString() + input.Shining.ToJsonString();
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.PrepareShiningPatientMemory(owner,
            locality, input.Profiles, input.Residents, input.Shining, 43, ordinary, protectedRows, change == "wrong_kind" ? RadiantMemory() : null));
        Assert.Equal(frozen, input.Profiles.ToJsonString() + input.Residents.ToJsonString() + input.Shining.ToJsonString());
    }

    [Fact]
    public void OrdinaryResidentSharingRadiantIdRemainsAnIndependentTypedActor()
    {
        var (input, owner, locality) = ShiningPatientTreatmentAdmissionTests.Inputs("radiant_actor"); AddMemorySupport(input, owner.OwnerId);
        var program = SpiritualTreatmentParticipation.PrepareShiningPatientMemory(owner, locality, input.Profiles, input.Residents,
            input.Shining, 43, new JsonArray(MemoryRow(owner.OwnerId, "resident_same_id_memory")), null, RadiantMemory());
        var projected = program.ProjectResidents(input.Residents);
        Assert.Single(projected[GuardianAbodeResidentState.UpdateThoughtJournalProperty]!.AsArray());
        var profiles = input.Profiles.DeepClone().AsObject(); program.ApplyProfiles(profiles);
        Assert.Empty(profiles["profiles"]![1]!["ledger"]!.AsArray()); Assert.Single(profiles["profiles"]![0]!["ledger"]!.AsArray());
    }

    [Theory]
    [InlineData("null")][InlineData("array")][InlineData("extra")][InlineData("stale")]
    [InlineData("blank")][InlineData("bad_id")][InlineData("wrong_text_type")]
    public void RadiantMemoryClosedParserRefusesMalformedPresence(string shape)
    {
        JsonNode? value = shape == "null" ? null : shape == "array" ? new JsonArray() : RadiantMemory();
        if (shape == "extra") value!["actorId"] = "foreign_actor";
        if (shape == "stale") value!["turnNumber"] = 42;
        if (shape == "blank") value!["gmThoughtsSummary"] = " ";
        if (shape == "bad_id") value!["entryId"] = "bad id";
        if (shape == "wrong_text_type") value!["summary"] = 7;
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ReadShiningPatientMemoryCommand(value, 43));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void RadiantPatientRequiresExplicitMemoryEvenWhenOrdinaryOrAutomaticMemoryExists(bool ordinaryPresent)
    {
        var (input, owner, locality) = ShiningPatientTreatmentAdmissionTests.Inputs("radiant_actor"); AddMemorySupport(input, "support_memory");
        input.Profiles["profiles"]![0]!["progressionLedger"] = new JsonArray(new JsonObject
            { ["entryId"] = "client_auto_income", ["source"] = "client_auto_strategy", ["summary"] = "Automatic income" });
        var ordinary = ordinaryPresent ? new JsonArray(MemoryRow("support_memory", "ordinary_memory")) : null;
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.PrepareShiningPatientMemory(owner,
            locality, input.Profiles, input.Residents, input.Shining, 43, ordinary, null, null));
    }

    [Fact]
    public void ResponseAndRefreshPreserveExplicitRadiantNullAndKeepOrdinaryRoutingConditional()
    {
        var absent = JsonSerializer.Deserialize<GameResponse>("{}")!;
        var malformed = JsonSerializer.Deserialize<GameResponse>("{\"spiritualShiningPatientMemory\":null}")!;
        Assert.Equal(JsonValueKind.Undefined, absent.SpiritualShiningPatientMemory.ValueKind);
        Assert.Equal(JsonValueKind.Null, malformed.SpiritualShiningPatientMemory.ValueKind);
        var merged = GameResponseRefreshMerger.Merge(malformed, new GameResponse());
        var json = JsonSerializer.SerializeToNode(merged, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!.AsObject();
        Assert.True(json.ContainsKey(SpiritualTreatmentParticipation.ShiningPatientMemoryResponseProperty)); Assert.Null(json[SpiritualTreatmentParticipation.ShiningPatientMemoryResponseProperty]);
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ReadShiningPatientMemoryCommand(json[SpiritualTreatmentParticipation.ShiningPatientMemoryResponseProperty], 43));
        Assert.DoesNotContain(FileMapping.OutputOnlyResponseFields,
            field => StringComparer.OrdinalIgnoreCase.Equals(field, GuardianAbodeResidentState.UpdateThoughtJournalProperty));
        Assert.Contains(FileMapping.OutputOnlyResponseFields,
            field => StringComparer.OrdinalIgnoreCase.Equals(field, SpiritualTreatmentParticipation.ShiningPatientMemoryResponseProperty));
    }

    [Theory]
    [InlineData("Chaos Sea", false)][InlineData("Mortal World", false)][InlineData("unknown", false)][InlineData(null, false)][InlineData("missing_turn", false)]
    [InlineData("Chaos Sea", true)][InlineData("Mortal World", true)][InlineData("unknown", true)][InlineData(null, true)][InlineData("missing_turn", true)]
    public async Task PrivateResidentTransportCannotFallBackOutsideItsActualShiningRealm(string? realm, bool directRadiantPresence)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-patient-memory-transport-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance); files.EnsureDirectoryStructure();
            if (realm != "missing_turn") await files.WriteFileAtomicAsync(LiveTurnPreparationService.TurnRequestPath, JsonSerializer.Serialize(new TurnRequest
                { SessionId = "memory_transport", RequestId = "memory_turn", TurnNumber = 43, ProgressionControl = new() { CurrentRealm = realm! } }));
            await files.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, "{\"entries\":[],\"thoughtJournal\":[]}");
            await files.WriteFileAtomicAsync("output/interface_updates.json", "{\"dialogueOptions\":[]}");
            var before = await files.ReadFileBytesAsync(GuardianAbodeResidentState.StatePath);
            var output = await files.ReadFileBytesAsync("output/interface_updates.json");
            var response = new GameResponse { ResidentThoughtJournalUpdates = [JsonSerializer.SerializeToElement(MemoryRow("support_memory", "private_memory"))], ResidentThoughtsFromPatientInterface = !directRadiantPresence };
            if (directRadiantPresence) response.SpiritualShiningPatientMemory = JsonSerializer.SerializeToElement<object?>(null);
            await Assert.ThrowsAsync<InvalidDataException>(() => new StateDistributor(files, NullLogger<StateDistributor>.Instance).DistributeAsync(response));
            Assert.Equal(before, await files.ReadFileBytesAsync(GuardianAbodeResidentState.StatePath));
            Assert.Equal(output, await files.ReadFileBytesAsync("output/interface_updates.json"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    internal static void AddMemorySupport(ShiningFactionServiceInputs input, string id)
    {
        var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("resident", id, "Shining Abode", 42);
        profile["locationId"] = ShiningFactionHealingServiceTestData.HallId; profile["locationName"] = "Зал исцеления";
        profile["lifeState"] = "alive"; profile["visibility"] = "public";
        input.Profiles["profiles"]!.AsArray().Add(profile);
        var entries = input.Residents["entries"]!.AsArray();
        if (!entries.OfType<JsonObject>().Any(r => r["residentId"]?.GetValue<string>() == id))
        { var row = entries[0]!.DeepClone().AsObject(); row["residentId"] = id; entries.Add(row); }
    }
    private static JsonObject RadiantMemory() => new() { ["entryId"] = "radiant_memory", ["summary"] = "Я принимаю помощь.", ["turnNumber"] = 43, ["gmThoughtsSummary"] = "Я принимаю помощь." };
    private static JsonObject MemoryRow(string id, string entry, int turn = 43) => new()
    { ["residentId"] = id, ["entryId"] = entry, ["turn"] = turn, ["timestamp"] = "2026-10-30T12:00:00Z", ["title"] = "Отдельное решение", ["summary"] = "Я сохраняю самостоятельное решение." };
}
