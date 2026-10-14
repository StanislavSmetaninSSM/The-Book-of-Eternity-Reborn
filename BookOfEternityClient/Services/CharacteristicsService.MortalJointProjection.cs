using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class CharacteristicsService
{
    internal const string JointDerivedProjectionPath = "game_state/player/computed_characteristics.json";
    internal enum JointDerivedProjectionDisposition { WrittenAndVerified, ExistingVerified, Refused, PublicationUncertain }
    internal sealed record JointDerivedProjectionResult(JointDerivedProjectionDisposition Disposition, string? Failure);

    // This instance is private to one named owner invocation. It adds no ambient
    // state, lease acquisition or public calculator mode. Ordinary instances
    // keep the original unbound readers and warning/fallback behavior.
    private JointProjectionRead? _jointProjectionRead;
    private sealed class JointProjectionRead(FileSystemManager.CanonicalWriteLease lease)
    {
        internal FileSystemManager.CanonicalWriteLease Lease { get; } = lease;
        internal Dictionary<string, byte[]?> Images { get; } = new(StringComparer.Ordinal);
    }

    internal Task<JointDerivedProjectionResult> WriteMortalJointProjectionAsync(
        FileSystemManager.CanonicalWriteLease lease,
        MortalQteDecisionPublication.JointProgressionPhaseCapability applied)
        => ValidateMortalJointProjectionAsync(lease, applied, write: true);

    // Replay checks semantics and preserves the existing timestamp and bytes.
    // This result alone grants no story, turn or mechanical publication authority.
    internal Task<JointDerivedProjectionResult> VerifyMortalJointProjectionAsync(
        FileSystemManager.CanonicalWriteLease lease,
        MortalQteDecisionPublication.JointProgressionPhaseCapability applied)
        => ValidateMortalJointProjectionAsync(lease, applied, write: false);

    private async Task<JointDerivedProjectionResult> ValidateMortalJointProjectionAsync(
        FileSystemManager.CanonicalWriteLease lease,
        MortalQteDecisionPublication.JointProgressionPhaseCapability applied, bool write)
    {
        var publicationAttempted = false;
        try
        {
            _fs.EnsureCanonicalWriteLeaseActive(lease);
            await applied.ValidateCurrentAsync(_fs, lease);
            if (applied.Phase != "applied") throw new InvalidDataException("joint_projection_progression_not_applied");
            var calculator = new CharacteristicsService(_fs, _stateManager, _logger)
            { _jointProjectionRead = new JointProjectionRead(lease) };
            // ComputeAsync remains the actual calculation, including real relic,
            // equipment, passive-skill and complete accepted effect projections.
            var computed = await calculator.ComputeAsync();
            var expected = CreateMortalJointProjectionBytes(computed, applied.PreparedAtUtcForOwner);
            await calculator.ValidateJointProjectionReadsAsync();
            await applied.ValidateCurrentAsync(_fs, lease);
            if (write)
            {
                publicationAttempted = true;
                await _fs.WriteFileAtomicAsync(lease, JointDerivedProjectionPath,
                    CanonicalJsonUtf8.DecodeOneOptionalBom(expected));
                var actual = await _fs.ReadFileBytesAsync(lease, JointDerivedProjectionPath);
                if (actual is null || !expected.AsSpan().SequenceEqual(actual))
                    throw new InvalidDataException("joint_projection_written_bytes_mismatch");
            }
            else
            {
                var actual = await _fs.ReadFileBytesAsync(lease, JointDerivedProjectionPath) ??
                    throw new InvalidDataException("joint_projection_existing_missing");
                var current = ReadJointProjectionObject(actual);
                var recomputed = ReadJointProjectionObject(expected);
                if (current["_lastComputed"] is not JsonValue stamp || !stamp.TryGetValue<string>(out var timestamp) ||
                    !DateTimeOffset.TryParseExact(timestamp, "o", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out _))
                    throw new InvalidDataException("joint_projection_existing_timestamp");
                current.Remove("_lastComputed"); recomputed.Remove("_lastComputed");
                if (!JsonNode.DeepEquals(current, recomputed))
                    throw new InvalidDataException("joint_projection_existing_semantics_mismatch");
            }
            await calculator.ValidateJointProjectionReadsAsync();
            await applied.ValidateCurrentAsync(_fs, lease);
            return new(write ? JointDerivedProjectionDisposition.WrittenAndVerified :
                JointDerivedProjectionDisposition.ExistingVerified, null);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or FormatException)
        {
            // No rollback/rearm, claim mutation or success-by-warning. If a write
            // may have happened, retain its evidence and block the next owner.
            return new(publicationAttempted ? JointDerivedProjectionDisposition.PublicationUncertain :
                JointDerivedProjectionDisposition.Refused, error.Message);
        }
    }

    private static JsonObject ReadJointProjectionObject(byte[] bytes) =>
        StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "joint derived characteristics") ??
        throw new InvalidDataException("joint_projection_null_root");

    private async Task<string?> ReadCharacteristicsSourceAsync(string path)
    {
        if (_jointProjectionRead is not { } scope) return await _fs.ReadFileAsync(path);
        var bytes = await _fs.ReadFileBytesAsync(scope.Lease, path);
        if (scope.Images.TryGetValue(path, out var prior) && !SameJointProjectionBytes(prior, bytes))
            throw new InvalidDataException("joint_projection_source_changed: " + path);
        scope.Images[path] = bytes?.ToArray();
        if (bytes is null) return null;
        var text = CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
        if (StrictJsonAuthority.Deserialize<JsonObject>(text, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed,
            "joint characteristics source " + path) is null) throw new InvalidDataException("joint_projection_source_null: " + path);
        return text;
    }

    private async Task<bool> ReadCharacteristicsLifeEndAuthorityAsync(string? lifeTransitionsJson, JsonObject soulRoot)
    {
        if (_jointProjectionRead is not { } scope)
            return await CanonicalStateNormalizer.HasLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(_fs, lifeTransitionsJson, soulRoot);
        var read = PendingTurnSnapshotReader.ReadCurrent(_fs, scope.Lease,
            new PendingTurnSnapshotPathSelection(["game_state/meta/soul_state.json"], []));
        if (!read.Success || read.Snapshot is not { } original)
            throw new InvalidDataException("joint_projection_original_soul_coverage");
        var before = ReadJointProjectionObject(original.ReadRequiredBytes("game_state/meta/soul_state.json"));
        return CanonicalStateNormalizer.HasLifecycleAuthorizedTriggerLifeEnd(lifeTransitionsJson, before, soulRoot);
    }

    private async Task<EffectMechanicsSnapshot> ReadCharacteristicsEffectsAsync()
    {
        if (_jointProjectionRead is not { } scope) return await EffectMechanicsSnapshot.LoadAsync(_fs);
        string[] paths = ["game_state/player/skills_active.json", "game_state/player/skills_passive.json", "game_state/npcs/npc_core.json",
            EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath, EffectCarrierCatalog.EnemiesPath,
            EffectCarrierCatalog.AlliesPath, EffectCarrierCatalog.AfterlifeProfilesPath,
            EffectCarrierCatalog.SpiritualConflictPath, EffectIdentityState.StatePath];
        foreach (var path in paths) await ReadCharacteristicsSourceAsync(path);
        var snapshot = await EffectMechanicsSnapshot.LoadAsync(_fs, scope.Lease);
        if (!snapshot.IsAccepted) throw new InvalidDataException("joint_projection_effect_snapshot_rejected: " +
            string.Join(";", snapshot.Issues.Select(issue => issue.Code + ":" + issue.FilePath)));
        await ValidateJointProjectionReadsAsync();
        return snapshot;
    }

    private async Task ValidateJointProjectionReadsAsync()
    {
        var scope = _jointProjectionRead ?? throw new InvalidOperationException("The joint projection reader is absent.");
        foreach (var row in scope.Images)
            if (!SameJointProjectionBytes(row.Value, await _fs.ReadFileBytesAsync(scope.Lease, row.Key)))
                throw new InvalidDataException("joint_projection_source_changed: " + row.Key);
    }
    private static bool SameJointProjectionBytes(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);

    private static byte[] CreateMortalJointProjectionBytes(ComputedCharacteristics result, string preparedAtUtc)
    {
        var standard = new Dictionary<string, int>();
        var permanentlyModified = new Dictionary<string, int>();
        var modified = new Dictionary<string, int>();
        var playerVisibleModified = new Dictionary<string, int>();
        var breakdown = new Dictionary<string, object>();

        foreach (var (name, stat) in result.Stats)
        {
            standard[name] = stat.BaseValue;
            permanentlyModified[name] = stat.PermanentlyModified;
            modified[name] = stat.Modified;
            playerVisibleModified[name] = stat.PlayerVisibleModified;

            if (stat.PermanentBonus != 0 || stat.TemporaryBonus != 0)
            {
                var sources = new List<string>();
                foreach (var s in stat.PermanentSources)
                    sources.Add($"[Пост.] {s.Origin}: {s.Description} ({(s.Value > 0 ? "+" : "")}{s.Value})");
                foreach (var s in stat.TemporarySources)
                    sources.Add($"[Врем.] {s.Origin}: {s.Description} ({(s.Value > 0 ? "+" : "")}{s.Value})");

                breakdown[name] = new
                {
                    @base = stat.BaseValue,
                    permanentBonus = stat.PermanentBonus,
                    temporaryBonus = stat.PlayerVisibleTemporaryBonus,
                    permanentlyModified = stat.PermanentlyModified,
                    modified = stat.PlayerVisibleModified,
                    sources
                };
            }
        }

        var output = new
        {
            _note = "Автоматически вычислено клиентом. Используйте эти значения для проверок действий (Block 12).",
            playerLevel = result.PlayerLevel,
            unspentStatPoints = result.UnspentStatPoints,
            characteristics = standard,
            permanentlyModifiedCharacteristics = permanentlyModified,
            modifiedCharacteristics = modified,
            playerVisibleModifiedCharacteristics = playerVisibleModified,
            breakdown,
            _lastComputed = preparedAtUtc
        };

        return FileSystemManager.EncodeUtf8WithPreamble(JsonSerializer.Serialize(output, JsonOpts));
    }
}
