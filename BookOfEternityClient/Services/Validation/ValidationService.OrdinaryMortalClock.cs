using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private const string OrdinaryClockPath = EffectAcceptedTurnInputComposer.WorldTimePath;
    private static readonly JsonSerializerOptions OrdinaryClockJson = new() { PropertyNameCaseInsensitive = true };

    // A working-prefix publication for a plain ordinary turn. The existing full
    // turn/normalizer rollback remains the owner after successful raw admission.
    private async Task<OrdinaryMortalClockPublication?> PrepareOrdinaryMortalClockAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidationPendingTurnSnapshotManifest expected,
        List<ValidationIssue> issues)
    {
        OrdinaryMortalClockPublication? publication = null;
        try
        {
            _fs.EnsureCanonicalWriteLeaseActive(lease);
            _fs.VerifyCurrentSessionOperation(lease);
            const string soulPath = "game_state/meta/soul_state.json";
            var soulBytes = await _fs.ReadFileBytesAsync(lease, soulPath);
            if (soulBytes is null) return null;
            var soul = ReadOrdinaryClockObject(soulBytes, "ordinary clock soul");
            if (!RealmSemantics.IsMortalRealm(soul["currentRealm"]?.GetValue<string>())) return null;
            var before = await _fs.ReadFileBytesAsync(lease, OrdinaryClockPath);
            if (before is null) return null;
            var command = ReadOrdinaryClockObject(before, OrdinaryClockPath);
            if (command["timeChange"] is null) return null;

            var paths = new[] { LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                PendingTurnSnapshotAuthority.AuthorityPath, LiveTurnPreparationService.TurnRequestPath,
                "game_state/meta/soul_state.json", ProgressionScheduleService.SchedulePath,
                ProgressionScheduleService.ReportPath, AcceptedMechanicsPlan.WoundCommandPath,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath };
            var guards = new List<(string Path, CanonicalBeforeImage Image)>();
            foreach (var path in paths)
            {
                var bytes = path == soulPath ? soulBytes : await _fs.ReadFileBytesAsync(lease, path);
                guards.Add((path, new(bytes is not null, bytes)));
            }
            byte[] Required(string path) => guards.Single(row => row.Path == path).Image.Bytes
                ?? throw new InvalidDataException("Ordinary clock source is absent: " + path);
            var request = ReadOrdinaryClockObject(Required(LiveTurnPreparationService.TurnRequestPath), "ordinary clock request");
            if (request["mortalDiagnosis"] is not null || request["mortalTreatment"] is not null ||
                request["spiritualHealing"] is not null ||
                guards.Any(row => row.Image.Existed && (row.Path is AcceptedMechanicsPlan.WoundCommandPath or
                    WoundAcceptedTurnSnapshotContract.PendingResolutionPath))) return null;

            IReadOnlyCollection<string> selection = [OrdinaryClockPath];
            // Deliberately bypass the validator's prevalidated lookup override.
            var observed = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, selection);
            if (!observed.Success || observed.Snapshot is null)
                throw new InvalidDataException("Ordinary clock requires fresh physical original authority: " + JsonSerializer.Serialize(observed.Issues));
            var original = observed.Snapshot;
            if (original.Realm != "mortal_world") return null;
            if (original.SessionId != expected.SessionId || original.RequestId != expected.RequestId ||
                original.TurnNumber != expected.TurnNumber || original.SnapshotToken != expected.ManifestPayloadHash ||
                request["sessionId"]?.GetValue<string>() != original.SessionId ||
                request["requestId"]?.GetValue<string>() != original.RequestId ||
                request["turnNumber"]?.GetValue<int>() != original.TurnNumber)
                throw new InvalidDataException("Ordinary clock original/current request identity changed.");
            var control = original.ReadOriginalControl() ?? throw new InvalidDataException("Ordinary clock has no signed original control.");
            if (!RealmSemantics.IsMortalRealm(control["currentRealm"]?.GetValue<string>()) ||
                !JsonNode.DeepEquals(control, request["progressionControl"]))
                throw new InvalidDataException("Ordinary clock control differs from its signed original.");
            var originalBytes = original.ReadRequiredBytes(OrdinaryClockPath);
            var baseline = ReadOrdinaryClockObject(originalBytes, "ordinary clock original");
            ValidateOrdinaryClockShape(command, issues);
            ValidateOrdinaryClockShape(baseline, issues);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error)) return null;
            if (baseline["timeChange"] is not null || baseline["setWorldTime"] is not null ||
                baseline["currentTimeInMinutes"] is not JsonValue time || !time.TryGetValue<int>(out var minute) || minute < 0 ||
                control["currentWorldTimeInMinutes"] is not JsonValue controlTime ||
                !controlTime.TryGetValue<int>(out var originalMinute) || originalMinute != minute)
                throw new InvalidDataException("Ordinary clock requires one canonical numeric original matching its control.");
            if (command["timeChange"] is not JsonValue delta || !delta.TryGetValue<int>(out var elapsed) || elapsed < 0 ||
                command["setWorldTime"] is not null)
                throw new InvalidDataException("Ordinary clock requires a nonnegative integer delta without an absolute command.");
            foreach (var field in new[] { "year", "monthName", "dayOfMonth", "timeOfDay", "currentTimeInMinutes" })
                if (command.ContainsKey(field) && !JsonNode.DeepEquals(command[field], baseline[field]))
                    throw new InvalidDataException("Ordinary clock command contains a competing absolute field: " + field);
            var future = checked(minute + elapsed);
            var after = baseline.DeepClone().AsObject();
            after.Remove("timeChange"); after.Remove("setWorldTime"); after["currentTimeInMinutes"] = future;

            var manifest = StrictJsonAuthority.Deserialize<GameEngine.PendingTurnSnapshotManifest>(
                FileSystemManager.DecodeFileBytes(Required(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)),
                OrdinaryClockJson, "ordinary clock physical manifest")
                ?? throw new InvalidDataException("Ordinary clock manifest is absent.");
            // The detached authority authenticates the complete original inventory
            // and rollback baseline. WoundCommandPath is not an optional-presence
            // protocol member; never manufacture that separate absence authority.
            if (manifest.Files.ContainsKey(AcceptedMechanicsPlan.WoundCommandPath) ||
                manifest.SnapshotFileHashes.ContainsKey(AcceptedMechanicsPlan.WoundCommandPath) ||
                manifest.RollbackBaselineFiles.Contains(AcceptedMechanicsPlan.WoundCommandPath, StringComparer.OrdinalIgnoreCase))
                return null;
            if (!PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest,
                    value => value.Files, value => value.SnapshotFileHashes, value => value.RollbackBaselineFiles,
                    [OrdinaryClockPath, AcceptedMechanicsPlan.WoundCommandPath], out _))
                throw new InvalidDataException("Ordinary clock original lacks exact rollback coverage.");
            guards.Add((manifest.Files[OrdinaryClockPath], new(true, originalBytes)));
            publication = new(this, lease, original, selection, guards, before, after.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            if (!await publication.PublishAsync())
            {
                InvalidateOrdinaryClockAuthority(lease);
                issues.Add(OrdinaryClockIssue("mortal_ordinary_clock_changed", "Clock or original sources changed before publication."));
                return null;
            }
            return publication;
        }
        catch (CoordinatedStatePublicationUncertainException)
        {
            publication?.PreserveUncertain();
            if (publication is not null) await publication.CloseAsync(null);
            throw;
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            if (publication is not null) await publication.CloseAsync(error);
            issues.Add(OrdinaryClockIssue("mortal_ordinary_clock_invalid", error.Message));
            return null;
        }
        catch (Exception error)
        {
            if (publication is not null) await publication.CloseAsync(error);
            throw;
        }
    }

    private static JsonObject ReadOrdinaryClockObject(byte[] bytes, string name) =>
        StrictJsonAuthority.Deserialize<JsonObject>(FileSystemManager.DecodeFileBytes(bytes), OrdinaryClockJson, name)
        ?? throw new InvalidDataException(name + " must be an object.");

    private void ValidateOrdinaryClockShape(JsonObject root, List<ValidationIssue> issues)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "timeChange", "setWorldTime", "year", "monthName", "dayOfMonth", "timeOfDay", "currentTimeInMinutes" };
        foreach (var field in root)
            if (!field.Key.StartsWith("_", StringComparison.Ordinal) && !allowed.Contains(field.Key))
                issues.Add(OrdinaryClockIssue("flexible_state_unknown_top_level_key", "Unsupported world-time field: " + field.Key));
        using var parsed = JsonDocument.Parse(root.ToJsonString());
        ValidateWorldQuestCombatFactionContract(parsed.RootElement, OrdinaryClockPath, issues);
    }

    private static ValidationIssue OrdinaryClockIssue(string code, string reason) => new(OrdinaryClockPath,
        IssueSeverity.Error, reason, code, section: "WorldTime", actor: "Client",
        expected: "one fresh ordinary Mortal original, integer delta and exact canonical clock publication",
        repairHint: "Preserve the original. Repair only the current ordinary time command; a changed original requires client recovery.");

    private void InvalidateOrdinaryClockAuthority(FileSystemManager.CanonicalWriteLease lease)
    {
        AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, lease);
        EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, lease);
    }

    private sealed class OrdinaryMortalClockPublication
    {
        private readonly ValidationService _validator;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly PendingTurnSnapshotReadAuthority _original;
        private readonly IReadOnlyCollection<string> _selection;
        private readonly IReadOnlyList<(string Path, CanonicalBeforeImage Image)> _guards;
        private readonly CanonicalBeforeImage _before, _after;
        private readonly string _afterJson;
        private bool _published, _retained, _uncertain;

        internal OrdinaryMortalClockPublication(ValidationService validator, FileSystemManager.CanonicalWriteLease lease,
            PendingTurnSnapshotReadAuthority original, IReadOnlyCollection<string> selection,
            IReadOnlyList<(string Path, CanonicalBeforeImage Image)> guards, byte[] before, string afterJson)
        {
            _validator = validator; _lease = lease; _original = original; _selection = selection; _guards = guards;
            _before = new(true, before); _afterJson = afterJson;
            _after = new(true, FileSystemManager.EncodeUtf8WithPreamble(afterJson));
        }

        internal async Task<bool> PublishAsync()
        {
            var fs = _validator._fs;
            fs.VerifyCurrentSessionOperation(_lease);
            if (!await SourcesCurrentAsync()) return false;
            _validator.InvalidateOrdinaryClockAuthority(_lease);
            var writes = _guards.Select(row => CoordinatedStateWriteHelper.CreateExactGuardWrite(row.Path, row.Image))
                .Append(new CoordinatedStateWriteHelper.PlannedWrite(OrdinaryClockPath, null, _afterJson,
                    RequireCurrentBaseline: true, ExactPrevious: _before)).ToArray();
            if (!await CoordinatedStateWriteHelper.TryCommitAsync(fs, _lease, writes)) return false;
            _published = true;
            if (!await SourcesCurrentAsync() || !Same(await fs.ReadFileBytesAsync(_lease, OrdinaryClockPath), _after.Bytes))
                throw new InvalidDataException("Ordinary clock source changed after its known publication.");
            return true;
        }

        internal async Task RetainAsync()
        {
            if (!await SourcesCurrentAsync() || !Same(await _validator._fs.ReadFileBytesAsync(_lease, OrdinaryClockPath), _after.Bytes))
                throw new InvalidDataException("Ordinary clock source changed before successful intake retained it.");
            _retained = true;
        }

        internal void PreserveUncertain() => _uncertain = true;

        internal async Task CloseAsync(Exception? primary)
        {
            if (!_published || _retained) return;
            try { _validator.InvalidateOrdinaryClockAuthority(_lease); }
            catch (Exception error)
            {
                if (primary is null)
                    throw new CoordinatedStatePublicationUncertainException(error);
                primary.Data["OrdinaryClock.AuthorityInvalidationFailure"] = error;
            }
            if (_uncertain || primary is CoordinatedStatePublicationUncertainException) return;
            try
            {
                var fs = _validator._fs;
                var current = await fs.ReadFileBytesAsync(_lease, OrdinaryClockPath);
                if (Same(current, _before.Bytes)) return;
                if (!Same(current, _after.Bytes))
                    throw new InvalidDataException("A foreign clock replaced the owned after-image; compensation cannot overwrite it.");
                var outcome = await fs.PublishLocalFilesAsync(_lease,
                    [new CanonicalLocalFileChange(OrdinaryClockPath, _after.Bytes, _before.Bytes)]);
                fs.RequireCommittedLocalPublication(outcome);
                if (!Same(await fs.ReadFileBytesAsync(_lease, OrdinaryClockPath), _before.Bytes))
                    throw new InvalidDataException("Exact ordinary clock compensation is unproven.");
                _published = false;
            }
            catch (Exception error)
            {
                _uncertain = true;
                throw new CoordinatedStatePublicationUncertainException(primary is null ? error : new AggregateException(primary, error));
            }
        }

        private async Task<bool> SourcesCurrentAsync()
        {
            var fs = _validator._fs;
            fs.EnsureCanonicalWriteLeaseActive(_lease); fs.VerifyCurrentSessionOperation(_lease);
            foreach (var guard in _guards)
                if (!Same(await fs.ReadFileBytesAsync(_lease, guard.Path), guard.Image.Bytes)) return false;
            var current = PendingTurnSnapshotReader.ReadCurrent(fs, _lease, _selection);
            return current.Success && current.Snapshot is { } source && source.SessionId == _original.SessionId &&
                source.RequestId == _original.RequestId && source.SnapshotToken == _original.SnapshotToken &&
                source.TurnNumber == _original.TurnNumber && source.Realm == _original.Realm &&
                Same(source.ReadRequiredBytes(OrdinaryClockPath), _original.ReadRequiredBytes(OrdinaryClockPath)) &&
                JsonNode.DeepEquals(source.ReadOriginalControl(), _original.ReadOriginalControl());
        }

        private static bool Same(byte[]? left, byte[]? right) =>
            left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
    }
}
