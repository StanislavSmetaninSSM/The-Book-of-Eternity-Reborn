using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PendingTurnSnapshotAuthorityTests
{
    [Theory]
    [InlineData("exact-callback")]
    [InlineData("exact-default")]
    [InlineData("legacy-ignored")]
    public async Task RollbackHashCallback_UsesOnlyVerifiedExactMode(string mode)
    {
        var manifest = await CreateManifestWithSnapshotAndRollbackAsync(21);
        var authority = mode == "legacy-ignored" ? CreatePortableAuthorityJson(manifest) : CreateDetachedAuthorityJson(manifest);
        var byteReads = 0; var hashReads = 0;
        byte[]? Read(string path) { byteReads++; return ReadRelativeFileBytes(path); }
        string? Hash(string path) { hashReads++; return PendingTurnSnapshotAuthority.ComputeSha256(ReadRelativeFileBytes(path)!); }

        bool Validate(string json, Func<string, string?>? hash, Func<string, byte[]?>? bytes, out string failure) =>
            PendingTurnSnapshotAuthority.TryValidateManifestForDestructiveAuthority(manifest, json, ManifestJsonOpts,
                static m => m.ManifestPayloadHash, static (m, h) => m.ManifestPayloadHash = h,
                static m => m.SessionId, static m => m.RequestId, static m => m.TurnNumber,
                static m => m.Files, static m => m.SnapshotFileHashes, static m => m.ClientOwnedValidationHashes,
                static m => m.RollbackBaselineFiles, static m => m.SourceLabel, static m => m.RollbackBackups,
                bytes, out _, out failure, exactRollbackHash: hash);

        Assert.True(Validate(authority, mode == "exact-default" ? null : Hash, Read, out var failure));
        Assert.Equal("authorized", failure);
        Assert.Equal(mode == "exact-callback" ? manifest.RollbackBackups.Count : 0, hashReads);
        Assert.Equal(mode == "exact-callback" ? 0 : manifest.RollbackBackups.Count, byteReads);

        // Corrupt detached integrity must refuse before either content callback.
        byteReads = hashReads = 0;
        var corrupt = JsonNode.Parse(authority)!.AsObject(); corrupt["payloadSha256"] = new string('0', 64);
        Assert.False(Validate(corrupt.ToJsonString(), Hash, Read, out failure));
        Assert.Equal("invalid_detached_authority", failure);
        Assert.Equal(0, hashReads); Assert.Equal(0, byteReads);

        if (mode == "exact-callback")
        {
            foreach (var invalid in new string?[] { null, "", "not-a-digest", new string('G', 64) })
            {
                Assert.False(Validate(authority, _ => invalid, Read, out failure));
                Assert.Equal("rollback_backup_unreadable", failure);
            }
            Assert.False(Validate(authority, _ => new string('0', 64), Read, out failure));
            Assert.Equal("detached_authority_mismatch", failure);
            // The existing byte-reader prerequisite remains even with a hash callback.
            hashReads = 0;
            Assert.False(Validate(authority, Hash, null, out failure));
            Assert.Equal("rollback_backup_unreadable", failure); Assert.Equal(0, hashReads);
        }
    }
}
