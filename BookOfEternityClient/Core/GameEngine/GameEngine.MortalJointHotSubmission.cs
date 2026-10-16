using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // These are observations of the actual ordinary producers, never submitted
    // admission. No serialized value can construct any of these private owners.
    private static readonly string[] HotInputPaths =
    [
        "game_state/player/status_changes.json", "game_state/player/experience.json",
        "game_state/core/player_status.json", "game_state/meta/soul_state.json",
        "game_state/misc/characteristics.json", "game_state/player/stat_points.json"
    ];
    private static readonly string[] HotOutputPaths =
        ["output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json"];
    private static readonly string[] HotControlPaths =
    [
        PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
        "input/turn_request.json", "game_state/control/validation_repair_request.json", "ready/turn_complete.json"
    ];
    private long _hotStageOrdinal;
    private long _hotReadyOrdinal;
    private HotOrdinaryOriginal? _hotOrdinaryOriginal;
    private HotRepairReadyRead? _hotRepairReady;
    private enum HotRepairRoute { Pending, Ordinary, Worker, DiagnosticOnly }
    private HotRepairRoute _hotRepairRoute;

    private void ObserveHotRepairDispatch(ValidationRepairDispatchState dispatch)
    {
        _hotRepairRoute = dispatch.MetadataDiagnosticOnly ? HotRepairRoute.DiagnosticOnly :
            dispatch.WorkerApplyAccepted || dispatch.ReadySignalCreated ? HotRepairRoute.Worker : HotRepairRoute.Ordinary;
    }

    private sealed class HotOrdinaryOriginal(long ordinal, string generation,
        PendingPlayerActionService.Binding? browserBinding)
    {
        internal readonly long Ordinal = ordinal;
        internal readonly string Generation = generation;
        internal readonly PendingPlayerActionService.Binding? BrowserBinding = browserBinding;
        internal string State = "Beginning";
        internal bool ProducerEntered, CleanupConsumed;
        internal TurnRequest? ProducerRequest;
        internal RollbackSnapshot? ProducerBackup;
        internal byte[]? GenerationBytes, ManifestBytes, AuthorityBytes, RequestBytes;
        internal string? SessionId, RequestId, SnapshotToken;
        internal int Turn;
        internal FileSystemManager.MainAdmission? ConsoleOwner;
        internal PlayerTurnPreparation? Preparation;
    }

    private sealed class HotRepairReadyRead(HotOrdinaryOriginal? origin, long ordinal, byte[]? bytes,
        string? text, DateTime? lastWriteTimeUtc)
    {
        internal readonly HotOrdinaryOriginal? Origin = origin;
        internal readonly long Ordinal = ordinal;
        internal readonly byte[]? Bytes = bytes?.ToArray();
        internal readonly string? Text = text;
        internal readonly DateTime? LastWriteTimeUtc = lastWriteTimeUtc;
        internal string Status = "Unavailable", Reason = "MatchingConsumptionNotCompleted";
        internal bool ReadyObservedBeforeDelete, ReadyDeleteReturned, ReadyReleaseReturned, CurrentSourceRecheckSucceeded;
        internal HotSourceObservation? Source;
        internal bool Faulted;
        internal void Fail(string reason)
        {
            if (!Faulted) Reason = reason;
            Faulted = true; Status = "Unavailable"; Source = null;
        }
    }

    private sealed record HotSourceObservation(PendingTurnSnapshotReadAuthority Snapshot,
        PendingTurnSnapshotReader.OriginalReadEvidence Evidence, JsonObject OriginalOperations,
        Dictionary<string, byte[]?> Current, JsonArray OriginalInputs);

    private void EndHotOrdinaryOriginal(string reason)
    {
        if (_hotOrdinaryOriginal is { } origin) origin.State = "Ended";
        _hotRepairReady?.Fail(reason);
    }

    private void EnterHotOriginalProducer(HotOrdinaryOriginal owner, TurnRequest request)
    {
        if (!ReferenceEquals(owner, _hotOrdinaryOriginal) || owner.State != "Beginning" || owner.ProducerEntered)
            throw new InvalidOperationException("Ordinary original producer requires its one actual Stage invocation.");
        owner.ProducerEntered = true; owner.ProducerRequest = request;
        owner.SessionId = request.SessionId; owner.RequestId = request.RequestId; owner.Turn = request.TurnNumber;
    }

    private void ObserveHotOriginalIssued(HotOrdinaryOriginal owner, FileSystemManager.CanonicalWriteLease lease,
        PendingTurnSnapshotManifest manifest, string manifestJson, string authorityJson)
    {
        if (!ReferenceEquals(owner, _hotOrdinaryOriginal) || owner.State != "Beginning" || !owner.ProducerEntered ||
            manifest.SessionId != owner.SessionId || manifest.RequestId != owner.RequestId || manifest.TurnNumber != owner.Turn)
            throw new InvalidOperationException("The issued ordinary original does not belong to its Stage producer.");
        var generation = _fs.ReadLocalGenerationSnapshot(lease);
        if (generation.Binding.Id != owner.Generation || generation.Bytes is null)
            throw new SessionReplacedException("Ordinary staging generation changed.", owner.Generation, generation.Binding.Id);
        owner.GenerationBytes = generation.Bytes.ToArray();
        owner.ManifestBytes = FileSystemManager.EncodeUtf8WithPreamble(manifestJson);
        owner.AuthorityBytes = FileSystemManager.EncodeUtf8WithPreamble(authorityJson);
        owner.SnapshotToken = manifest.ManifestPayloadHash;
        // The canonical lease owns and disposes its child MainAdmission. Retain
        // the actual enclosing continuous operation, never that short child.
        owner.ConsoleOwner = owner.BrowserBinding is null ? lease.MainAdmission?.Parent : null;
        owner.State = "Issued";
    }

    private void CompleteHotOriginalStaging(PlayerTurnPreparation preparation)
    {
        var owner = preparation.HotOrigin ?? throw new InvalidOperationException("Actual Stage origin is missing.");
        if (!ReferenceEquals(owner, _hotOrdinaryOriginal) || owner.State != "Issued" ||
            preparation.Generation != owner.Generation || preparation.Request.SessionId != owner.SessionId ||
            preparation.Request.RequestId != owner.RequestId || preparation.Request.TurnNumber != owner.Turn ||
            preparation.SnapshotContext.Payload.ManifestPayloadHash != owner.SnapshotToken)
            throw new InvalidOperationException("Stage returned a different original or request.");
        owner.RequestBytes = FileSystemManager.EncodeUtf8WithPreamble(preparation.HotRequestJson!);
        owner.Preparation = preparation;
        // Console remains under its continuous owner. Browser must also return
        // through the real outer participating close before it becomes usable.
        owner.State = owner.BrowserBinding is null ? "StagedUnderLiveOwner" : "AwaitingStagingOwnerReturn";
    }

    private void AcknowledgeHotBrowserStaging(PlayerTurnPreparation preparation)
    {
        var owner = preparation.HotOrigin;
        if (owner is null || !ReferenceEquals(owner, _hotOrdinaryOriginal) ||
            !ReferenceEquals(owner.Preparation, preparation) || owner.State != "AwaitingStagingOwnerReturn")
            throw new InvalidOperationException("Browser Stage acknowledgement requires its actual returned preparation.");
        owner.State = "Staged";
    }

    private void RequireHotOrigin(FileSystemManager.CanonicalWriteLease lease, HotOrdinaryOriginal origin)
    {
        RequireOrdinaryJointRepairLease(lease);
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        var main = lease.MainAdmission ?? throw new InvalidDataException("HOT requires actual ordinary Main admission.");
        main.Validate(lease);
        var generation = _fs.ReadLocalGenerationSnapshot(lease);
        if (generation.Binding.Id != origin.Generation)
            throw new SessionReplacedException("HOT original belongs to a replaced session.", origin.Generation, generation.Binding.Id);
        if (!ReferenceEquals(origin, _hotOrdinaryOriginal) || origin.Preparation is null ||
            origin.State is not ("Staged" or "StagedUnderLiveOwner") || !HotBytesEqual(generation.Bytes, origin.GenerationBytes))
            throw new InvalidDataException("The actual live ordinary original is unavailable.");
        if (origin.BrowserBinding is { } browser)
        {
            if (!_fs.HasBrowserOriginalOperation) throw new InvalidDataException("Current browser original admission is missing.");
            _ = _fs.CaptureBrowserOriginalCondition(lease, browser.Generation);
        }
        else if (origin.ConsoleOwner is not { Closed: false } console || !main.SharesAccess(console))
            throw new InvalidDataException("The continuous console staging owner is no longer current.");
    }

    private async Task<HotRepairReadyRead> ReadHotRepairReadyForSessionAsync(string expectedGeneration)
    {
        if (_hotOrdinaryOriginal is not { Preparation: not null } origin ||
            origin.State is not ("Staged" or "StagedUnderLiveOwner"))
        {
            var text = await ReadValidationRepairFileForSessionAsync(ValidationRepairReadyPath, expectedGeneration);
            return new(null, ++_hotReadyOrdinal, null, text, null);
        }
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        ThrowIfRepairSessionReplaced(lease, expectedGeneration);
        var snapshot = await _fs.ReadFileSnapshotAsync(lease, ValidationRepairReadyPath);
        var read = new HotRepairReadyRead(origin, ++_hotReadyOrdinal, snapshot?.Content,
            snapshot is null ? null : FileSystemManager.DecodeFileBytes(snapshot.Content), snapshot?.LastWriteTimeUtc);
        _hotRepairReady?.Fail("SubsequentRepairObservation");
        _hotRepairReady = read;
        return read;
    }

    private async Task CaptureMatchingHotRepairReadyAsync(HotRepairReadyRead read, ValidationRepairReady ready,
        PendingTurnSnapshotResolution resolution, string generation, bool workerRoute)
    {
        if (read.Origin is null) { read.Fail("MissingLiveOrigin"); return; }
        if (workerRoute) { read.Fail("WorkerDispositionNotIntegrated"); return; }
        try
        {
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            ThrowIfRepairSessionReplaced(lease, generation);
            if (!ReferenceEquals(read, _hotRepairReady) || resolution.Context is not { } frame ||
                !IsMatchingRepairReady(ready, frame)) throw new InvalidDataException("HOT matching branch changed.");
            var source = await ReadHotSourceAsync(lease, read.Origin, frame);
            if (!HotBytesEqual(read.Bytes, await _fs.ReadFileBytesAsync(lease, ValidationRepairReadyPath)))
                throw new InvalidDataException("Ready changed after its actual read.");
            source.Evidence.RequireCurrent(_fs, lease, source.Snapshot);
            await RequireHotCurrentImagesAsync(lease, source.Current);
            read.Source = source;
            read.Status = "MatchingReadyObserved";
        }
        catch (Exception error)
        {
            read.Fail(IsOptionalHotFailure(error) ? "SourceChanged" : "OwnerFailure");
            if (!IsOptionalHotFailure(error)) throw;
        }
    }

    private async Task<HotSourceObservation> ReadHotSourceAsync(FileSystemManager.CanonicalWriteLease lease,
        HotOrdinaryOriginal origin, ValidatedPendingTurnSnapshotContext frame)
    {
        RequireHotOrigin(lease, origin);
        var observed = PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease, new PendingTurnSnapshotPathSelection(
            ["game_state/meta/soul_state.json"], HotInputPaths.Where(path => path != "game_state/meta/soul_state.json")));
        if (!observed.Success || observed.Snapshot is not { } original || observed.Evidence is not { } evidence)
            throw new InvalidDataException("HOT original source closure is unavailable.");
        if (original.SessionId != origin.SessionId || original.RequestId != origin.RequestId ||
            original.SnapshotToken != origin.SnapshotToken || original.TurnNumber != origin.Turn ||
            original.SessionId != frame.SessionId || original.RequestId != frame.RequestId ||
            original.SnapshotToken != frame.Payload.ManifestPayloadHash || original.TurnNumber != frame.TurnNumber)
            throw new InvalidDataException("HOT source is not the actual Stage original.");
        var current = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in HotInputPaths.Concat(HotOutputPaths).Concat(HotControlPaths))
            current.Add(path, await _fs.ReadFileBytesAsync(lease, path));
        if (!HotBytesEqual(current[PendingTurnSnapshotManifestPath], origin.ManifestBytes) ||
            !HotBytesEqual(current[PendingTurnSnapshotAuthority.AuthorityPath], origin.AuthorityBytes) ||
            !HotBytesEqual(current["input/turn_request.json"], origin.RequestBytes))
            throw new InvalidDataException("HOT original or final Stage request changed.");
        var rows = new JsonArray();
        foreach (var path in HotInputPaths)
        {
            var present = original.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal);
            var absent = original.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal);
            rows.Add(new JsonObject { ["path"] = path,
                ["sourceStatus"] = present ? "Present" : absent ? "Absent" : "UncoveredInSignedInventory",
                ["present"] = present ? true : absent ? false : null,
                ["base64"] = present ? Convert.ToBase64String(original.ReadRequiredBytes(path)) : null });
        }
        evidence.RequireCurrent(_fs, lease, original);
        await RequireHotCurrentImagesAsync(lease, current);
        RequireHotOrigin(lease, origin);
        return new(original, evidence, evidence.ExportDiagnosticData(), current, rows);
    }

    private async Task RequireHotCurrentImagesAsync(FileSystemManager.CanonicalWriteLease lease,
        IReadOnlyDictionary<string, byte[]?> images)
    {
        foreach (var (path, bytes) in images)
            if (!HotBytesEqual(bytes, await _fs.ReadFileBytesAsync(lease, path)))
                throw new InvalidDataException("HOT source changed during physical recheck: " + path);
    }

    private async Task RecheckHotReadySourceAsync(FileSystemManager.CanonicalWriteLease lease, HotRepairReadyRead read,
        bool requireReadyAbsent)
    {
        if (read.Faulted || read.Source is not { } previous || read.Origin?.Preparation is not { } preparation)
            throw new InvalidDataException("HOT has no closed matching source observation.");
        var fresh = await ReadHotSourceAsync(lease, read.Origin, preparation.SnapshotContext);
        if (!JsonNode.DeepEquals(fresh.OriginalOperations, previous.OriginalOperations))
            throw new InvalidDataException("HOT original operations changed between actual leases.");
        foreach (var (path, bytes) in previous.Current)
            if (!HotBytesEqual(bytes, fresh.Current[path])) throw new InvalidDataException("HOT submitted source changed: " + path);
        var ready = await _fs.ReadFileBytesAsync(lease, ValidationRepairReadyPath);
        if (requireReadyAbsent ? ready is not null : !HotBytesEqual(ready, read.Bytes))
            throw new InvalidDataException("HOT Ready no longer matches its actual consumption event.");
        fresh.Evidence.RequireCurrent(_fs, lease, fresh.Snapshot);
        await RequireHotCurrentImagesAsync(lease, fresh.Current);
        // Only this new lease's genuinely issued reader evidence is retained.
        read.Source = fresh;
    }

    private static bool HotBytesEqual(byte[]? a, byte[]? b) => a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b);
    private static bool IsOptionalHotFailure(Exception error)
    {
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        bool HasOwningFailure(Exception current)
        {
            if (!seen.Add(current)) return false;
            if (current is SessionReplacedException or CoordinatedStatePublicationUncertainException) return true;
            if (current is AggregateException aggregate && aggregate.InnerExceptions.Any(HasOwningFailure)) return true;
            if (current.InnerException is { } inner && HasOwningFailure(inner)) return true;
            return current.Data.Values.OfType<Exception>().Any(HasOwningFailure);
        }
        // Optional source failure cannot hide the owning failure just because a
        // close/continuation wrapper retained it as an inner or secondary error.
        return !HasOwningFailure(error) && error is not AggregateException &&
            error is IOException or InvalidDataException or InvalidOperationException or ArgumentException or JsonException;
    }

    private JsonObject HotBindingDiagnostic(HotOrdinaryOriginal? origin) => new()
    {
        ["generation"] = origin?.Generation, ["sessionId"] = origin?.SessionId, ["requestId"] = origin?.RequestId,
        ["snapshotToken"] = origin?.SnapshotToken, ["turn"] = origin?.Turn,
        ["actualStageCompleted"] = origin?.Preparation is not null, ["stageDisposition"] = origin?.State
    };

    private static JsonArray HotImageRows(IEnumerable<string> paths, IReadOnlyDictionary<string, byte[]?>? images)
    {
        var rows = new JsonArray();
        if (images is null) return rows;
        foreach (var path in paths)
        {
            var bytes = images[path];
            rows.Add(new JsonObject { ["path"] = path, ["present"] = bytes is not null,
                ["base64"] = bytes is null ? null : Convert.ToBase64String(bytes) });
        }
        return rows;
    }

    private JsonObject HotCustodyDiagnostic(HotRepairReadyRead? read) => new()
    {
        ["status"] = read?.Status ?? "Unavailable", ["reason"] = read?.Reason ?? (_hotRepairRoute == HotRepairRoute.Worker ? "WorkerDispositionNotIntegrated" :
            _hotRepairRoute == HotRepairRoute.DiagnosticOnly ? "DiagnosticOnlyDispatch" :
            _hotOrdinaryOriginal?.Preparation is not null ? "MatchingConsumptionNotCompleted" : "MissingLiveOrigin"),
        ["readyObservedBeforeDelete"] = read?.ReadyObservedBeforeDelete ?? false,
        ["readyDeleteReturned"] = read?.ReadyDeleteReturned ?? false,
        ["readyReleaseReturned"] = read?.ReadyReleaseReturned ?? false,
        ["currentSourceRecheckSucceeded"] = read?.CurrentSourceRecheckSucceeded ?? false,
        ["originalInputs"] = read?.Source?.OriginalInputs.DeepClone() ?? new JsonArray(),
        ["currentInputs"] = HotImageRows(HotInputPaths, read?.Source?.Current),
        ["outputs"] = HotImageRows(HotOutputPaths, read?.Source?.Current)
    };

    private JsonObject ExportHotOrdinarySubmissionDiagnostic() => new()
    {
        ["binding"] = HotBindingDiagnostic(_hotOrdinaryOriginal), ["custody"] = HotCustodyDiagnostic(_hotRepairReady),
        ["hasS"] = false, ["hasB"] = false, ["hasM3"] = false
    };
}
