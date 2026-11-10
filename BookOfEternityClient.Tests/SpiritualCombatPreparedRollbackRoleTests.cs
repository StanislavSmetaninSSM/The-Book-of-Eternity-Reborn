using BookOfEternityClient.Services;
using Xunit;
using Role = BookOfEternityClient.Services.ValidationService.SpiritualOriginalTurnCapture.PreparedCombatCommandRollback;

namespace BookOfEternityClient.Tests;

/// <summary>Pure role DATA is distinct from canonical coverage and never grants a completed owner.</summary>
public sealed class SpiritualCombatPreparedRollbackRoleTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("baseline")]
    [InlineData("backup")]
    [InlineData("backup_hash")]
    [InlineData("missing_file")]
    [InlineData("missing_hash")]
    [InlineData("legacy_hash")]
    [InlineData("unsafe_path")]
    [InlineData("aliased_path")]
    [InlineData("null_baseline")]
    public void PreparedRoleRequiresExactSignedInputMetadataWithoutCanonicalRole(string mutation)
    {
        const string path = AcceptedMechanicsPlan.WoundCommandPath;
        var payload = new PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload
        {
            SnapshotHashMode = PendingTurnSnapshotAuthority.ExactSnapshotHashMode,
            Files = new(StringComparer.OrdinalIgnoreCase) { [path] = "game_state/control/pending_turn_snapshot/prepared-command.json" },
            SnapshotFileHashes = new(StringComparer.OrdinalIgnoreCase) { [path] = new string('a', 64) }
        };
        Assert.True(Role.HasExactPreparedRole(payload));
        Assert.False(PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(payload,
            p => p.Files, p => p.SnapshotFileHashes, p => p.RollbackBaselineFiles, new[] { path }, out var missing));
        Assert.Equal(path, missing);
        switch (mutation)
        {
            case "baseline": payload.RollbackBaselineFiles.Add(path); break;
            case "backup": payload.RollbackBackups[path] = "prepared.rollback"; break;
            case "backup_hash": payload.RollbackBackupHashes[path] = new string('b', 64); break;
            case "missing_file": payload.Files.Clear(); break;
            case "missing_hash": payload.SnapshotFileHashes.Clear(); break;
            case "legacy_hash": payload.SnapshotHashMode = null; break;
            case "unsafe_path": payload.Files[path] = "../foreign.json"; break;
            case "aliased_path": payload.Files.Clear(); payload.Files[path.ToUpperInvariant()] = "game_state/control/pending_turn_snapshot/prepared-command.json"; break;
            case "null_baseline": payload.RollbackBaselineFiles = null!; break;
            default: Assert.Equal("valid", mutation); break;
        }
        Assert.Equal(mutation == "valid", Role.HasExactPreparedRole(payload));
    }
}
