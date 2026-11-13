using System.Text;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PendingTurnSnapshotPresenceIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveProducer_NpcSkillsObservesOriginalPresenceWithoutChangingRollbackMembership(bool originallyPresent)
    {
        const string skillsPath = "game_state/npcs/npc_skills.json";
        await using var context = await CreateSeededContextAsync();
        var originalBytes = Encoding.UTF8.GetBytes("{\"NPCActiveSkillChanges\":[],\"NPCPassiveSkillChanges\":[]}\n");
        Assert.False(context.FileSystem.FileExists(skillsPath));
        if (originallyPresent) await context.WriteExactBytesAsync(skillsPath, originalBytes);

        // Actual production capture/signing, not a manufactured manifest or re-signed absence.
        await PrepareLiveAsync(context);
        var manifest = await ReadManifestAsync(context);
        var observations = manifest["originalPathPresenceV1"]!.AsObject();
        Assert.True(observations.ContainsKey(skillsPath), "The live producer must observe original NPC skills presence.");
        Assert.Equal(originallyPresent, observations[skillsPath]!.GetValue<bool>());
        Assert.Equal(originallyPresent, manifest["files"]!.AsObject().ContainsKey(skillsPath));
        Assert.Equal(originallyPresent, manifest["snapshotFileHashes"]!.AsObject().ContainsKey(skillsPath));
        Assert.Equal(originallyPresent, manifest["rollbackBaselineFiles"]!.AsArray()
            .Any(path => path!.GetValue<string>() == skillsPath));
        Assert.Empty(manifest["rollbackBackups"]!.AsObject());

        // A newly emitted current command cannot rewrite the authenticated original observation.
        await context.WriteExactJsonAsync(skillsPath,
            "{\"NPCActiveSkillChanges\":[{\"NPCId\":\"npc_later\",\"skillChanges\":[]}]}");
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var read = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(RequiredPaths, [skillsPath]));
        Assert.True(read.Success, string.Join(Environment.NewLine, read.Issues));
        Assert.NotNull(read.Snapshot);
        if (originallyPresent)
        {
            Assert.Equal(originalBytes, read.Snapshot!.ReadRequiredBytes(skillsPath));
            Assert.DoesNotContain(skillsPath, read.Snapshot.AbsentLogicalPaths);
        }
        else
        {
            Assert.Contains(skillsPath, read.Snapshot!.AbsentLogicalPaths);
            Assert.DoesNotContain(skillsPath, read.Snapshot.CoveredLogicalPaths);
            Assert.Throws<KeyNotFoundException>(() => read.Snapshot.ReadRequiredBytes(skillsPath));
        }
    }
    [Theory]
    [InlineData("mortal_world", true)]
    [InlineData("Chaos Sea", false)]
    [InlineData("Shining Abode", false)]
    public async Task LiveProducer_NpcSkillsObservationIsMortalOnly(string realm, bool observesSkills)
    {
        await using var context = await CreateSeededContextAsync();
        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "presence-realm", RequestId = "presence-realm-request", TurnNumber = 42,
            CurrentRealm = realm, PlayerAction = "Observe realm-specific original sources.", PreGeneratedDices1d20 = [7]
        });
        var manifest = await ReadManifestAsync(context);
        var paths = manifest["originalPathPresenceV1"]!.AsObject().Select(pair => pair.Key).Order(StringComparer.Ordinal);
        Assert.Equal((observesSkills ? PendingTurnSnapshotPathPresenceV1.MortalNpcSkillLogicalPaths :
            PendingTurnSnapshotPathPresenceV1.LogicalPaths).Order(StringComparer.Ordinal), paths);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var read = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(RequiredPaths,
                [PendingTurnSnapshotPathPresenceV1.MortalNpcSkillsPath]));
        Assert.Equal(observesSkills, read.Success);
        if (!observesSkills)
            Assert.Contains(read.Issues, issue => issue.Code == "pending_turn_snapshot_reader_absence_unproven");
    }

    [Fact]
    public async Task PriorBasePresence_RemainsReadableWithoutProvingNpcSkillAbsence()
    {
        await using var context = await CreateSeededContextAsync();
        await PrepareLiveAsync(context);
        var manifest = await ReadManifestAsync(context);
        Assert.True(manifest["originalPathPresenceV1"]!.AsObject().Remove(PendingTurnSnapshotPathPresenceV1.MortalNpcSkillsPath));
        // Deliberate reader-contract reconstruction of the prior exact closed form, never gameplay evidence.
        await ResignAsync(context, manifest);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.True(PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease, RequiredPaths).Success);
        var selected = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(RequiredPaths,
                [PendingTurnSnapshotPathPresenceV1.MortalNpcSkillsPath]));
        Assert.False(selected.Success);
        Assert.Contains(selected.Issues, issue => issue.Code == "pending_turn_snapshot_reader_absence_unproven");
    }

}
