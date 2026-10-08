using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundTreatmentPublicationBeforeImage
{
    private readonly byte[]? _bytes;

    internal MortalWoundTreatmentPublicationBeforeImage(
        string path,
        byte[]? bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
        _bytes = bytes?.ToArray();
    }

    internal string Path { get; }
    internal byte[]? Bytes => _bytes?.ToArray();
}

internal static class MortalWoundTreatmentDurableSurfaceQuarantine
{
    internal sealed record Result(bool CommandChanged, bool PendingChanged);

    private static readonly IReadOnlySet<string> CommandRowFields =
        new HashSet<string>(
            new[]
            {
                "kind", "transitionKind", "commandRef", "operationKey",
                "authority", "result", "finalSceneText"
            },
            StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> PendingRowFields =
        new HashSet<string>(
            new[]
            {
                "operationKey", "attemptId", "requestFingerprint", "request"
            },
            StringComparer.Ordinal);

    internal static Result RemoveExactRequestRows(
        JsonObject command,
        JsonObject? pending,
        string operationKey,
        string attemptId,
        string requestFingerprint)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!ResourceMaterializationContract.IsExactIdentifier(operationKey) ||
            !ResourceMaterializationContract.IsExactIdentifier(attemptId) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                requestFingerprint))
        {
            throw new ArgumentException(
                "Durable treatment quarantine requires one exact operation, attempt, and request fingerprint.");
        }

        var commandChanged = RemoveCommandRows(
            command,
            operationKey,
            attemptId,
            requestFingerprint);
        var pendingChanged = pending is not null && RemovePendingRows(
            pending,
            operationKey,
            attemptId,
            requestFingerprint);
        return new Result(commandChanged, pendingChanged);
    }

    internal static bool ContainsExactRequestRows(
        JsonObject command,
        JsonObject? pending,
        string operationKey,
        string attemptId,
        string requestFingerprint) =>
        ContainsCommandRow(
            command,
            operationKey,
            attemptId,
            requestFingerprint) ||
        pending is not null && ContainsPendingRow(
            pending,
            operationKey,
            attemptId,
            requestFingerprint);

    private static bool RemoveCommandRows(
        JsonObject command,
        string operationKey,
        string attemptId,
        string requestFingerprint)
    {
        if (command["commands"] is not JsonArray rows)
            return false;

        var changed = false;
        for (var index = rows.Count - 1; index >= 0; index--)
        {
            if (rows[index] is JsonObject row && IsExactCommandRow(
                    row,
                    operationKey,
                    attemptId,
                    requestFingerprint))
            {
                rows.RemoveAt(index);
                changed = true;
            }
        }
        return changed;
    }

    private static bool RemovePendingRows(
        JsonObject pending,
        string operationKey,
        string attemptId,
        string requestFingerprint)
    {
        if (pending["submittedTreatmentRequests"] is not JsonArray rows)
            return false;

        var changed = false;
        for (var index = rows.Count - 1; index >= 0; index--)
        {
            if (rows[index] is JsonObject row && IsExactPendingRow(
                    row,
                    operationKey,
                    attemptId,
                    requestFingerprint))
            {
                rows.RemoveAt(index);
                changed = true;
            }
        }
        if (changed && rows.Count == 0)
            pending.Remove("submittedTreatmentRequests");
        return changed;
    }

    private static bool ContainsCommandRow(
        JsonObject command,
        string operationKey,
        string attemptId,
        string requestFingerprint) =>
        command["commands"] is JsonArray rows &&
        rows.OfType<JsonObject>().Any(row => IsExactCommandRow(
            row,
            operationKey,
            attemptId,
            requestFingerprint));

    private static bool ContainsPendingRow(
        JsonObject pending,
        string operationKey,
        string attemptId,
        string requestFingerprint) =>
        pending["submittedTreatmentRequests"] is JsonArray rows &&
        rows.OfType<JsonObject>().Any(row => IsExactPendingRow(
            row,
            operationKey,
            attemptId,
            requestFingerprint));

    private static bool IsExactCommandRow(
        JsonObject row,
        string operationKey,
        string attemptId,
        string requestFingerprint) =>
        HasExactFields(row, CommandRowFields) &&
        StringEquals(row, "kind", "accepted_transition") &&
        StringEquals(row, "transitionKind", "treat") &&
        StringEquals(row, "operationKey", operationKey) &&
        row["authority"] is JsonObject authority &&
        authority.Count == 1 &&
        authority["request"] is JsonObject authorityRequest &&
        RequestCoordinatesAgree(
            authorityRequest,
            operationKey,
            attemptId,
            requestFingerprint) &&
        row["result"] is JsonObject result &&
        result["requestAuthority"] is JsonObject resultRequest &&
        RequestCoordinatesAgree(
            resultRequest,
            operationKey,
            attemptId,
            requestFingerprint);

    private static bool IsExactPendingRow(
        JsonObject row,
        string operationKey,
        string attemptId,
        string requestFingerprint) =>
        HasExactFields(row, PendingRowFields) &&
        StringEquals(row, "operationKey", operationKey) &&
        StringEquals(row, "attemptId", attemptId) &&
        StringEquals(row, "requestFingerprint", requestFingerprint) &&
        row["request"] is JsonObject request &&
        RequestCoordinatesAgree(
            request,
            operationKey,
            attemptId,
            requestFingerprint);

    private static bool RequestCoordinatesAgree(
        JsonObject request,
        string operationKey,
        string attemptId,
        string requestFingerprint) =>
        StringEquals(request, "requestFingerprint", requestFingerprint) &&
        request["coordinates"] is JsonObject coordinates &&
        StringEquals(coordinates, "operationKey", operationKey) &&
        StringEquals(coordinates, "attemptId", attemptId);

    private static bool HasExactFields(
        JsonObject source,
        IReadOnlySet<string> fields) =>
        source.Count == fields.Count &&
        source.All(pair => fields.Contains(pair.Key));

    private static bool StringEquals(
        JsonObject source,
        string property,
        string expected) =>
        source[property] is JsonValue value &&
        value.TryGetValue<string>(out var actual) &&
        string.Equals(actual, expected, StringComparison.Ordinal);
}

internal sealed class MortalWoundTreatmentResourcePublicationTransaction
    : IAsyncDisposable
{
    private const string TokenMismatchCode =
        "mortal_wound_treatment_publication_transaction_token_mismatch";
    private const string TokenReplayedCode =
        "mortal_wound_treatment_publication_transaction_token_replayed";
    private const string TokenStaleCode =
        "mortal_wound_treatment_publication_transaction_stale";
    private const string ReservationChangedCode =
        "mortal_wound_treatment_publication_transaction_reservation_changed";
    private const string PublishedAgreementChangedCode =
        "mortal_wound_treatment_publication_published_agreement_changed";
    private const string TerminalReleaseFailedCode =
        "mortal_wound_treatment_publication_terminal_release_failed";

    private readonly FileSystemManager _fileSystem;
    private readonly AcceptedMechanicsPlan _plan;
    private readonly string _bindingFingerprint;
    private readonly MortalWoundTreatmentPublicationTakeReceipt _receipt;
    private readonly MortalWoundTreatmentPublicationBeforeImage[] _beforeImages;
    private readonly IReadOnlyDictionary<
        string,
        MortalWoundTreatmentPublicationBeforeImage> _beforeImagesByPath;
    private MortalWoundTreatmentPublicationBeforeImage[]? _publishedAgreement;
    private int _progressionAgreementAdvanced;
    private int _rearmed;
    private int _closed;
    private ExceptionDispatchInfo? _publicationUncertainty;

    private MortalWoundTreatmentResourcePublicationTransaction(
        FileSystemManager fileSystem,
        AcceptedMechanicsPlan plan,
        AcceptedMechanicsPlanBinding binding,
        MortalWoundTreatmentPublicationTakeReceipt receipt,
        IReadOnlyList<MortalWoundTreatmentPublicationBeforeImage> beforeImages)
    {
        _fileSystem = fileSystem;
        _plan = plan;
        _bindingFingerprint =
            AcceptedMechanicsPlanFingerprints.ComputeInput(binding);
        _receipt = receipt;
        _beforeImages = beforeImages
            .Select(static value =>
                new MortalWoundTreatmentPublicationBeforeImage(
                    value.Path,
                    value.Bytes))
            .ToArray();
        _beforeImagesByPath = _beforeImages.ToDictionary(
            static value => value.Path,
            StringComparer.Ordinal);
    }

    internal static MortalWoundTreatmentResourcePublicationTransaction Create(
        FileSystemManager fileSystem,
        AcceptedMechanicsPlan plan,
        AcceptedMechanicsPlanBinding binding,
        MortalWoundTreatmentPublicationTakeReceipt receipt,
        IReadOnlyList<MortalWoundTreatmentPublicationBeforeImage> beforeImages)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(beforeImages);

        var authority = plan.TreatmentResourcePublicationAuthority;
        if (authority is null ||
            !authority.RequiresCoordinatedSettlement ||
            !authority.HasValidSeal() ||
            !ReferenceEquals(fileSystem, receipt.FileSystem) ||
            !ReferenceEquals(plan, receipt.Plan) ||
            !ReferenceEquals(authority, receipt.PublicationAuthority) ||
            !string.Equals(
                AcceptedMechanicsPlanFingerprints.ComputeInput(binding),
                AcceptedMechanicsPlanFingerprints.ComputeInput(receipt.Binding),
                StringComparison.Ordinal) ||
            beforeImages.Count == 0 ||
            beforeImages.Select(static value => value.Path)
                .Distinct(StringComparer.Ordinal).Count() != beforeImages.Count)
        {
            throw new InvalidOperationException(
                "The held Mortal wound-treatment publication transaction requires one exact plan, binding, receipt, and rollback inventory.");
        }

        return new MortalWoundTreatmentResourcePublicationTransaction(
            fileSystem,
            plan,
            binding,
            receipt,
            beforeImages);
    }

    internal async Task<MortalWoundTreatmentPublicationProbeResult> ProbeAsync(
        FileSystemManager fileSystem)
    {
        ThrowIfPublicationUncertain();
        ArgumentNullException.ThrowIfNull(fileSystem);
        await using var writeLease =
            await fileSystem.AcquireCanonicalWriteLeaseAsync();
        return AcceptedTurnAuthorityRegistry
            .ProbeTakenMortalWoundTreatmentPublication(
                fileSystem,
                writeLease,
                _receipt);
    }

    internal async Task<IReadOnlyList<ValidationIssue>>
        FilterExactPublishedItemValidationIssuesAsync(
            FileSystemManager fileSystem,
            IReadOnlyList<ValidationIssue> issues)
    {
        ThrowIfPublicationUncertain();
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(issues);
        var publicationAuthority = _plan.TreatmentResourcePublicationAuthority;
        var itemAuthority = publicationAuthority?.ItemPublicationAuthority;
        if (issues.Count == 0 ||
            _receipt.IsTerminalReleaseOnly ||
            !ReferenceEquals(fileSystem, _fileSystem) ||
            Volatile.Read(ref _closed) != 0 ||
            itemAuthority is null ||
            !ReferenceEquals(_plan, _receipt.Plan) ||
            !ReferenceEquals(publicationAuthority, _receipt.PublicationAuthority) ||
            !publicationAuthority!.HasValidSeal() ||
            !itemAuthority.HasValidSeal() ||
            !string.Equals(
                _bindingFingerprint,
                AcceptedMechanicsPlanFingerprints.ComputeInput(_receipt.Binding),
                StringComparison.Ordinal))
        {
            return issues;
        }

        await using var writeLease =
            await fileSystem.AcquireCanonicalWriteLeaseAsync();
        if (Volatile.Read(ref _closed) != 0 ||
            ToOperationFailure(AcceptedTurnAuthorityRegistry
                .ProbeTakenMortalWoundTreatmentPublication(
                    fileSystem,
                    writeLease,
                    _receipt)) is not null ||
            !await PublishedAgreementStillExactAsync(fileSystem, writeLease))
        {
            return issues;
        }

        var agreement = Volatile.Read(ref _publishedAgreement);
        var npcAgreement = agreement?.SingleOrDefault(static image =>
            string.Equals(
                image.Path,
                NpcCoreChangesContract.NpcCorePath,
                StringComparison.Ordinal));
        if (npcAgreement?.Bytes is not { } npcBytes)
            return issues;

        JsonObject publishedNpcRoot;
        try
        {
            publishedNpcRoot = JsonNode.Parse(DecodeUtf8(npcBytes)) as JsonObject ??
                throw new InvalidDataException(
                    "The exact published NPC agreement is not a JSON object.");
        }
        catch (Exception exception) when (
            exception is JsonException or DecoderFallbackException or
            InvalidDataException)
        {
            return issues;
        }

        var matchingIndex = -1;
        var matchingCount = 0;
        for (var index = 0; index < issues.Count; index++)
        {
            if (!itemAuthority.ProvesExactPublishedNpcInventoryContinuityIssue(
                    issues[index],
                    publishedNpcRoot))
            {
                continue;
            }

            matchingIndex = index;
            matchingCount++;
        }

        if (matchingCount != 1)
            return issues;

        var filtered = new ValidationIssue[issues.Count - 1];
        var filteredIndex = 0;
        for (var index = 0; index < issues.Count; index++)
        {
            if (index == matchingIndex)
                continue;

            filtered[filteredIndex++] = issues[index];
        }

        return filtered;
    }

    internal async Task CapturePublishedAgreementAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ThrowIfPublicationUncertain();
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        if (_receipt.IsTerminalReleaseOnly ||
            !ReferenceEquals(fileSystem, _fileSystem) ||
            Volatile.Read(ref _closed) != 0 ||
            Volatile.Read(ref _publishedAgreement) is not null)
        {
            throw new InvalidOperationException(
                "The held treatment publication agreement may be captured exactly once by its open transaction.");
        }
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);

        var paths = WoundAcceptedTurnSnapshotContract
            .BuildRequiredPaths(_plan.TouchedPaths);
        var captured = new MortalWoundTreatmentPublicationBeforeImage[paths.Count];
        for (var index = 0; index < paths.Count; index++)
        {
            captured[index] = new MortalWoundTreatmentPublicationBeforeImage(
                paths[index],
                await fileSystem.ReadFileBytesAsync(writeLease, paths[index]));
        }

        if (Interlocked.CompareExchange(
                ref _publishedAgreement,
                captured,
                comparand: null) is not null)
        {
            throw new InvalidOperationException(
                "The held treatment publication agreement was already captured.");
        }
    }

    internal async Task<MortalWoundTreatmentPublicationOperationResult>
        AdvancePublishedAgreementWithProgressionAsync(
            FileSystemManager fileSystem,
            ProgressionScheduleService progressionSchedule,
            ProgressionControl control)
    {
        ThrowIfPublicationUncertain();
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(progressionSchedule);
        ArgumentNullException.ThrowIfNull(control);
        if (_receipt.IsTerminalReleaseOnly)
            return TerminalReleaseOnlyFailure("advance progression publication");
        FileSystemManager.CanonicalWriteLease? writeLease = null;
        MortalWoundTreatmentPublicationOperationResult? result = null;
        var ownsAdvance = false;
        var advanced = false;
        try
        {
            writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
            result = ToOperationFailure(AcceptedTurnAuthorityRegistry
                .ProbeTakenMortalWoundTreatmentPublication(
                    fileSystem,
                    writeLease,
                    _receipt));
            if (result is not null)
            {
                ObserveClosure(result);
                return result;
            }
            if (!await PublishedAgreementStillExactAsync(fileSystem, writeLease))
                return PublishedAgreementChangedFailure();
            if (Interlocked.CompareExchange(
                    ref _progressionAgreementAdvanced,
                    1,
                    0) != 0)
            {
                return PublishedAgreementChangedFailure();
            }
            ownsAdvance = true;

            var mutation = await progressionSchedule
                .PrepareAcceptedTurnOutcomeMutationAsync(writeLease, control);
            var agreement = Volatile.Read(ref _publishedAgreement);
            if (agreement is null ||
                !ProgressionMutationBeforeImagesAgree(agreement, mutation))
            {
                return PublishedAgreementChangedFailure();
            }
            if (!await progressionSchedule
                    .TryCommitAcceptedTurnOutcomeMutationAsync(
                        writeLease,
                        mutation))
            {
                return PublishedAgreementChangedFailure();
            }

            Volatile.Write(
                ref _publishedAgreement,
                AdvanceProgressionAgreement(agreement, mutation));
            advanced = true;
            result = new MortalWoundTreatmentPublicationOperationResult(
                true,
                Array.Empty<ValidationIssue>(),
                1,
                MortalWoundTreatmentPublicationTransactionOutcome
                    .PublishedAgreementAdvanced);
            return result;
        }
        finally
        {
            if (ownsAdvance && !advanced)
                Interlocked.Exchange(ref _progressionAgreementAdvanced, 0);
            if (writeLease is not null)
            {
                await DisposeLeaseAsync(
                    writeLease,
                    result is not null && ClosesReceipt(result));
            }
        }
    }

    private static bool ProgressionMutationBeforeImagesAgree(
        IReadOnlyList<MortalWoundTreatmentPublicationBeforeImage> agreement,
        ProgressionScheduleService.AcceptedTurnOutcomeMutation mutation) =>
        AgreementImageMatches(
            agreement,
            ProgressionScheduleService.SchedulePath,
            mutation.ScheduleBefore) &&
        AgreementImageMatches(
            agreement,
            ProgressionScheduleService.ReportPath,
            mutation.ReportBefore);

    private static bool AgreementImageMatches(
        IReadOnlyList<MortalWoundTreatmentPublicationBeforeImage> agreement,
        string path,
        CanonicalBeforeImage image)
    {
        var expected = agreement.SingleOrDefault(value => string.Equals(
            value.Path,
            path,
            StringComparison.Ordinal));
        if (expected == null)
            return false;
        var expectedBytes = expected.Bytes;
        var mutationBytes = image.Bytes;
        return (expectedBytes is null) == (mutationBytes is null) &&
               (expectedBytes is null ||
                expectedBytes.AsSpan().SequenceEqual(mutationBytes!));
    }

    private static MortalWoundTreatmentPublicationBeforeImage[]
        AdvanceProgressionAgreement(
            IReadOnlyList<MortalWoundTreatmentPublicationBeforeImage> agreement,
            ProgressionScheduleService.AcceptedTurnOutcomeMutation mutation) =>
        agreement.Select(value =>
            string.Equals(
                value.Path,
                ProgressionScheduleService.SchedulePath,
                StringComparison.Ordinal)
                ? new MortalWoundTreatmentPublicationBeforeImage(
                    value.Path,
                    mutation.ScheduleAfter.Bytes)
                : string.Equals(
                    value.Path,
                    ProgressionScheduleService.ReportPath,
                    StringComparison.Ordinal)
                    ? new MortalWoundTreatmentPublicationBeforeImage(
                        value.Path,
                        mutation.ReportAfter.Bytes)
                    : new MortalWoundTreatmentPublicationBeforeImage(
                        value.Path,
                        value.Bytes))
            .ToArray();

    internal async Task<MortalWoundTreatmentPublicationOperationResult>
        CompleteAsync(FileSystemManager fileSystem)
    {
        ThrowIfPublicationUncertain();
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (_receipt.IsTerminalReleaseOnly)
            return TerminalReleaseOnlyFailure("finalize publication");
        FileSystemManager.CanonicalWriteLease? writeLease = null;
        MortalWoundTreatmentPublicationOperationResult? result = null;
        try
        {
            writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
            result = ToOperationFailure(AcceptedTurnAuthorityRegistry
                .ProbeTakenMortalWoundTreatmentPublication(
                    fileSystem,
                    writeLease,
                    _receipt));
            if (result is not null)
            {
                ObserveClosure(result);
                return result;
            }
            if (!await PublishedAgreementStillExactAsync(fileSystem, writeLease))
            {
                return PublishedAgreementChangedFailure();
            }
            result = AcceptedTurnAuthorityRegistry
                .CompleteTakenMortalWoundTreatmentPublication(
                    fileSystem,
                    writeLease,
                    _receipt);
            ObserveClosure(result);
            return result;
        }
        finally
        {
            if (writeLease is not null)
            {
                await DisposeLeaseAsync(
                    writeLease,
                    result is not null && ClosesReceipt(result));
            }
        }
    }

    private async Task<bool> PublishedAgreementStillExactAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var agreement = Volatile.Read(ref _publishedAgreement);
        if (agreement is null || agreement.Length == 0)
            return false;

        foreach (var expected in agreement)
        {
            var current = await fileSystem.ReadFileBytesAsync(
                writeLease,
                expected.Path);
            var expectedBytes = expected.Bytes;
            if ((expectedBytes is null) != (current is null) ||
                expectedBytes is not null &&
                !expectedBytes.AsSpan().SequenceEqual(current!))
            {
                return false;
            }
        }
        return true;
    }

    internal async Task<MortalWoundTreatmentPublicationOperationResult>
        CompensateAsync(FileSystemManager fileSystem)
    {
        ThrowIfPublicationUncertain();
        ArgumentNullException.ThrowIfNull(fileSystem);
        FileSystemManager.CanonicalWriteLease? writeLease = null;
        MortalWoundTreatmentPublicationOperationResult? result = null;
        try
        {
            writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
            result = _receipt.IsTerminalReleaseOnly
                ? await ReleaseTerminalWithLeaseAsync(
                    fileSystem,
                    writeLease,
                    "validation_failed",
                    fromRearmed: false)
                : await RestoreAndSettleAsync(fileSystem, writeLease);
            ObserveClosure(result);
            return result;
        }
        finally
        {
            if (writeLease is not null)
            {
                await DisposeLeaseAsync(
                    writeLease,
                    result is not null && ClosesReceipt(result));
            }
        }
    }

    internal async Task<MortalWoundTreatmentPublicationOperationResult>
        ReleaseTerminalAsync(
            FileSystemManager fileSystem,
            string reason)
    {
        ThrowIfPublicationUncertain();
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        FileSystemManager.CanonicalWriteLease? writeLease = null;
        MortalWoundTreatmentPublicationOperationResult? result = null;
        try
        {
            writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
            var fromRearmed = Volatile.Read(ref _rearmed) != 0;
            result = await ReleaseTerminalWithLeaseAsync(
                fileSystem,
                writeLease,
                reason,
                fromRearmed);
            ObserveClosure(result);
            return result;
        }
        finally
        {
            if (writeLease is not null)
            {
                await DisposeLeaseAsync(
                    writeLease,
                    result is not null && ClosesReceipt(result));
            }
        }
    }

    internal async Task<MortalWoundTreatmentPublicationOperationResult>
        ReleaseTerminalUnderLeaseAsync(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            string reason)
    {
        ThrowIfPublicationUncertain();
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!ReferenceEquals(fileSystem, _fileSystem) ||
            Volatile.Read(ref _closed) != 0 ||
            Volatile.Read(ref _rearmed) != 0)
        {
            throw new InvalidOperationException(
                "Pre-publication terminal cleanup requires the exact open treatment transaction under its owning canonical lease.");
        }
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);

        var result = await ReleaseTerminalWithLeaseAsync(
            fileSystem,
            writeLease,
            reason,
            fromRearmed: false);
        ObserveClosure(result);
        return result;
    }

    internal async Task FinishHelperFailureAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ThrowIfPublicationUncertain();
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        var result = _receipt.IsTerminalReleaseOnly
            ? await ReleaseTerminalWithLeaseAsync(
                fileSystem,
                writeLease,
                "validation_failed",
                fromRearmed: false)
            : await RestoreAndSettleAsync(fileSystem, writeLease);
        ObserveClosure(result);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(
                "The held Mortal wound-treatment helper failure could not be settled: " +
                string.Join(", ", result.Issues.Select(static issue =>
                    issue.Code ?? issue.FilePath)));
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (Volatile.Read(ref _closed) != 0 || Volatile.Read(ref _publicationUncertainty) is not null)
            return;

        var result = await CompensateAsync(_fileSystem);
        if (!result.IsValid &&
            result.Outcome !=
            MortalWoundTreatmentPublicationTransactionOutcome.TokenReplayed)
        {
            throw new InvalidOperationException(
                "The open held-treatment publication transaction could not be safely disposed: " +
                string.Join(", ", result.Issues.Select(static issue =>
                    issue.Code ?? issue.FilePath)));
        }
    }

    private async Task<MortalWoundTreatmentPublicationOperationResult>
        RestoreAndSettleAsync(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ThrowIfPublicationUncertain();
        try
        {
            return await RestoreAndSettleCoreAsync(fileSystem, writeLease);
        }
        catch (CoordinatedStatePublicationUncertainException uncertain)
        {
            ObservePublicationUncertainty(uncertain);
            try
            {
                var failure = AcceptedTurnAuthorityRegistry.FailTakenMortalWoundTreatmentPublicationTerminal(
                    fileSystem, writeLease, _receipt);
                ObserveClosure(failure);
                uncertain.Data["TreatmentPublicationTerminalFence"] = failure;
            }
            catch (Exception fencingFailure)
            {
                // A secondary in-memory fencing diagnostic cannot replace the actual storage decision.
                uncertain.Data["TreatmentPublicationTerminalFenceFailure"] = fencingFailure;
            }
            throw;
        }
    }

    private async Task<MortalWoundTreatmentPublicationOperationResult>
        RestoreAndSettleCoreAsync(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        var liveHold = AcceptedTurnAuthorityRegistry
            .ProbeMortalWoundTreatmentCoordinatedPublicationClaims(
                fileSystem,
                writeLease,
                _receipt);
        var receiptProbe = AcceptedTurnAuthorityRegistry
            .ProbeTakenMortalWoundTreatmentPublication(
                fileSystem,
                writeLease,
                _receipt);
        var probeFailure = ToOperationFailure(receiptProbe);
        if (probeFailure is { Outcome:
                MortalWoundTreatmentPublicationTransactionOutcome.TokenMismatch or
                MortalWoundTreatmentPublicationTransactionOutcome.TokenReplayed })
        {
            return probeFailure;
        }

        try
        {
            await RestoreExactBeforeImagesAsync(
                fileSystem,
                writeLease,
                forceDurablePublication: true);
        }
        catch (Exception exception) when (exception is not CoordinatedStatePublicationUncertainException)
        {
            var failure = AcceptedTurnAuthorityRegistry
                .FailTakenMortalWoundTreatmentPublicationTerminal(
                    fileSystem,
                    writeLease,
                    _receipt);
            throw new AggregateException(
                "Treatment publication exact-byte restoration failed.",
                exception,
                new InvalidOperationException(
                    string.Join(
                        ", ",
                        failure.Issues.Select(static issue =>
                            issue.Code ?? issue.FilePath))));
        }

        var durableAuthorityAgrees =
            await DurableTreatmentAuthorityAgreesAsync(fileSystem, writeLease);
        if (durableAuthorityAgrees && LiveHoldAgrees(liveHold))
        {
            return AcceptedTurnAuthorityRegistry
                .RearmTakenMortalWoundTreatmentPublication(
                    fileSystem,
                    writeLease,
                    _receipt);
        }

        try
        {
            await WriteQuarantinedDurableSurfacesAsync(fileSystem, writeLease);
        }
        catch (Exception quarantineException) when (quarantineException is not CoordinatedStatePublicationUncertainException)
        {
            Exception? restorationFailure = null;
            try
            {
                await RestoreExactBeforeImagesAsync(
                    fileSystem,
                    writeLease,
                    forceDurablePublication: false);
            }
            catch (CoordinatedStatePublicationUncertainException uncertain)
            {
                uncertain.Data["TreatmentPublicationOriginalFailure"] = quarantineException;
                throw;
            }
            catch (Exception exception)
            {
                restorationFailure = exception;
            }
            _ = AcceptedTurnAuthorityRegistry
                .FailTakenMortalWoundTreatmentPublicationTerminal(
                    fileSystem,
                    writeLease,
                    _receipt);
            throw new AggregateException(
                restorationFailure is null
                    ? "Treatment publication quarantine failed."
                    : "Treatment publication quarantine failed and its durable restoration also failed.",
                restorationFailure is null
                    ? new[] { quarantineException }
                    : new[] { quarantineException, restorationFailure });
        }

        var closed = AcceptedTurnAuthorityRegistry
            .CloseTakenMortalWoundTreatmentPublicationAfterQuarantine(
                fileSystem,
                writeLease,
                _receipt);
        if (closed.IsValid)
            return closed;

        try
        {
            await RestoreExactBeforeImagesAsync(
                fileSystem,
                writeLease,
                forceDurablePublication: false);
        }
        catch (Exception restorationException) when (restorationException is not CoordinatedStatePublicationUncertainException)
        {
            throw new AggregateException(
                "Treatment publication quarantine settlement failed and its durable authority could not be restored.",
                restorationException,
                BuildTerminalReleaseFailureException(closed));
        }
        return closed;
    }

    private async Task<MortalWoundTreatmentPublicationOperationResult>
        ReleaseTerminalWithLeaseAsync(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            string reason,
            bool fromRearmed)
    {
        ThrowIfPublicationUncertain();
        try
        {
            return await ReleaseTerminalWithLeaseCoreAsync(fileSystem, writeLease, reason, fromRearmed);
        }
        catch (CoordinatedStatePublicationUncertainException uncertain)
        {
            ObservePublicationUncertainty(uncertain);
            try
            {
                var failure = fromRearmed
                    ? AcceptedTurnAuthorityRegistry.FailRearmedMortalWoundTreatmentPublicationTerminal(fileSystem, writeLease, _receipt)
                    : AcceptedTurnAuthorityRegistry.FailTakenMortalWoundTreatmentPublicationTerminal(fileSystem, writeLease, _receipt);
                ObserveClosure(failure);
                uncertain.Data["TreatmentPublicationTerminalFence"] = failure;
            }
            catch (Exception fencingFailure)
            {
                // A secondary in-memory fencing diagnostic cannot replace the actual storage decision.
                uncertain.Data["TreatmentPublicationTerminalFenceFailure"] = fencingFailure;
            }
            throw;
        }
    }

    private async Task<MortalWoundTreatmentPublicationOperationResult>
        ReleaseTerminalWithLeaseCoreAsync(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            string reason,
            bool fromRearmed)
    {
        var liveHold = AcceptedTurnAuthorityRegistry
            .ProbeMortalWoundTreatmentCoordinatedPublicationClaims(
                fileSystem,
                writeLease,
                _receipt);
        if (!LiveHoldAgrees(liveHold))
        {
            return await BlockTerminalClaimDriftAsync(
                fileSystem,
                writeLease,
                fromRearmed,
                reason);
        }
        if (!fromRearmed)
        {
            var receiptProbe = AcceptedTurnAuthorityRegistry
                .ProbeTakenMortalWoundTreatmentPublication(
                    fileSystem,
                    writeLease,
                    _receipt);
            var probeFailure = ToOperationFailure(receiptProbe);
            if (probeFailure is not null)
                return probeFailure;
        }

        try
        {
            await RestoreExactBeforeImagesAsync(
                fileSystem,
                writeLease,
                forceDurablePublication: false);
            await WriteQuarantinedDurableSurfacesAsync(fileSystem, writeLease);
        }
        catch (Exception cleanupException) when (cleanupException is not CoordinatedStatePublicationUncertainException)
        {
            Exception? restorationFailure = null;
            try
            {
                await RestoreExactBeforeImagesAsync(
                    fileSystem,
                    writeLease,
                    forceDurablePublication: false);
            }
            catch (CoordinatedStatePublicationUncertainException uncertain)
            {
                uncertain.Data["TreatmentPublicationOriginalFailure"] = cleanupException;
                throw;
            }
            catch (Exception exception)
            {
                restorationFailure = exception;
            }
            var failure = fromRearmed
                ? AcceptedTurnAuthorityRegistry
                    .FailRearmedMortalWoundTreatmentPublicationTerminal(
                        fileSystem,
                        writeLease,
                        _receipt)
                : AcceptedTurnAuthorityRegistry
                    .FailTakenMortalWoundTreatmentPublicationTerminal(
                        fileSystem,
                        writeLease,
                        _receipt);
            if (restorationFailure is null)
            {
                try
                {
                    if (await IsProvenSafeTerminalReleaseFailureAsync(
                            fileSystem,
                            writeLease,
                            failure))
                    {
                        if (cleanupException is SessionReplacedException or
                            OperationCanceledException)
                        {
                            throw;
                        }
                        return failure;
                    }
                }
                catch (Exception proofException) when (
                    proofException is not SessionReplacedException &&
                    proofException is not OperationCanceledException)
                {
                    restorationFailure = proofException;
                }
            }
            throw new AggregateException(
                restorationFailure is null
                    ? "Terminal treatment publication cleanup failed."
                    : "Terminal treatment publication cleanup failed and its durable restoration also failed.",
                restorationFailure is null
                    ? new[]
                    {
                        cleanupException,
                        BuildTerminalReleaseFailureException(failure)
                    }
                    : new[] { cleanupException, restorationFailure });
        }

        var released = fromRearmed
            ? AcceptedTurnAuthorityRegistry
                .ReleaseRearmedMortalWoundTreatmentPublicationTerminal(
                    fileSystem,
                    writeLease,
                    _receipt,
                    reason)
            : AcceptedTurnAuthorityRegistry
                .ReleaseTakenMortalWoundTreatmentPublicationTerminal(
                    fileSystem,
                    writeLease,
                    _receipt,
                    reason);
        if (released.IsValid)
            return released;

        await RestoreExactBeforeImagesAsync(
            fileSystem,
            writeLease,
            forceDurablePublication: false);
        if (await IsProvenSafeTerminalReleaseFailureAsync(
                fileSystem,
                writeLease,
                released))
        {
            return released;
        }
        throw new AggregateException(
            "Terminal treatment publication release failed without one exact restored held blocker.",
            BuildTerminalReleaseFailureException(released));
    }

    private async Task<MortalWoundTreatmentPublicationOperationResult>
        BlockTerminalClaimDriftAsync(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            bool fromRearmed,
            string reason)
    {
        await RestoreExactBeforeImagesAsync(
            fileSystem,
            writeLease,
            forceDurablePublication: false);
        var failure = fromRearmed
            ? AcceptedTurnAuthorityRegistry
                .ReleaseRearmedMortalWoundTreatmentPublicationTerminal(
                    fileSystem,
                    writeLease,
                    _receipt,
                    reason)
            : AcceptedTurnAuthorityRegistry
                .ReleaseTakenMortalWoundTreatmentPublicationTerminal(
                    fileSystem,
                    writeLease,
                    _receipt,
                    reason);
        var resourceHold = AcceptedTurnAuthorityRegistry
            .ProbeMortalWoundTreatmentResourcePublicationHold(
                fileSystem,
                writeLease,
                _receipt.AcceptedState,
                _receipt.Request,
                _receipt.Finalization);
        if (!IsExactProvenTerminalReleaseFailure(failure) ||
            !await ExactBeforeImagesStillAgreeAsync(fileSystem, writeLease) ||
            !LiveHoldAgrees(resourceHold) ||
            !AcceptedTurnAuthorityRegistry
                .HasExactMortalWoundTreatmentPublicationRestartBlocker(
                    fileSystem,
                    writeLease,
                    _receipt))
        {
            throw new AggregateException(
                "Terminal treatment claim drift could not be fenced behind one exact restored restart blocker.",
                BuildTerminalReleaseFailureException(failure));
        }
        return failure;
    }

    private async Task<bool> IsProvenSafeTerminalReleaseFailureAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentPublicationOperationResult result)
    {
        if (!IsExactProvenTerminalReleaseFailure(result) ||
            !await ExactBeforeImagesStillAgreeAsync(fileSystem, writeLease))
        {
            return false;
        }

        var hold = AcceptedTurnAuthorityRegistry
            .ProbeMortalWoundTreatmentCoordinatedPublicationClaims(
                fileSystem,
                writeLease,
                _receipt);
        return LiveHoldAgrees(hold) &&
               AcceptedTurnAuthorityRegistry
                   .HasExactMortalWoundTreatmentPublicationRestartBlocker(
                       fileSystem,
                       writeLease,
                       _receipt);
    }

    private async Task<bool> ExactBeforeImagesStillAgreeAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        foreach (var expected in _beforeImages)
        {
            var current = await fileSystem.ReadFileBytesAsync(
                writeLease,
                expected.Path);
            var expectedBytes = expected.Bytes;
            if ((expectedBytes is null) != (current is null) ||
                expectedBytes is not null &&
                !expectedBytes.AsSpan().SequenceEqual(current!))
            {
                return false;
            }
        }
        return true;
    }

    internal static bool IsExactProvenTerminalReleaseFailure(
        MortalWoundTreatmentPublicationOperationResult result) =>
        !result.IsValid &&
        result.ChangedCount == 0 &&
        result.Outcome ==
            MortalWoundTreatmentPublicationTransactionOutcome.ReleaseFailed &&
        result.Issues.Any(static issue => string.Equals(
            issue.Code,
            TerminalReleaseFailedCode,
            StringComparison.Ordinal));

    private static InvalidOperationException BuildTerminalReleaseFailureException(
        MortalWoundTreatmentPublicationOperationResult result) => new(
        "Treatment publication terminal release did not close safely: " +
        string.Join(
            ", ",
            result.Issues.Select(static issue => issue.Code ?? issue.FilePath)));

    private async Task RestoreExactBeforeImagesAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        bool forceDurablePublication)
    {
        var failures = new List<Exception>();
        for (var index = _beforeImages.Length - 1; index >= 0; index--)
        {
            var beforeImage = _beforeImages[index];
            try
            {
                var expected = beforeImage.Bytes;
                var current = await fileSystem.ReadFileBytesAsync(
                    writeLease,
                    beforeImage.Path);
                if (expected is null)
                {
                    if (current is not null)
                        fileSystem.DeleteFile(writeLease, beforeImage.Path);
                    continue;
                }

                if (!forceDurablePublication ||
                    !IsDurableTreatmentSurface(beforeImage.Path))
                {
                    if (current is not null &&
                        current.AsSpan().SequenceEqual(expected))
                    {
                        continue;
                    }
                }

                await fileSystem.WriteFileAtomicBytesAsync(
                    writeLease,
                    beforeImage.Path,
                    expected);
            }
            catch (Exception exception) when (exception is not CoordinatedStatePublicationUncertainException)
            {
                failures.Add(new InvalidOperationException(
                    $"Failed to restore exact treatment-publication before-image for '{beforeImage.Path}'.",
                    exception));
            }
        }

        if (failures.Count != 0)
        {
            throw new AggregateException(
                "One or more treatment-publication before-images could not be restored.",
                failures);
        }
    }

    private async Task<bool> DurableTreatmentAuthorityAgreesAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        if (!TryGetDurableBeforeImage(
                AcceptedMechanicsPlan.WoundCommandPath,
                out var expectedCommand) ||
            !_beforeImagesByPath.TryGetValue(
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                out var pendingBeforeImage))
        {
            return false;
        }

        var expectedPending = pendingBeforeImage.Bytes;

        var command = await fileSystem.ReadFileBytesAsync(
            writeLease,
            AcceptedMechanicsPlan.WoundCommandPath);
        var pending = await fileSystem.ReadFileBytesAsync(
            writeLease,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        if (command is null ||
            !command.AsSpan().SequenceEqual(expectedCommand) ||
            (expectedPending is null) != (pending is null) ||
            (expectedPending is not null &&
             !pending!.AsSpan().SequenceEqual(expectedPending)))
        {
            return false;
        }

        var commandText = DecodeUtf8(command);
        if (!ContainsDurableCoordinates(commandText))
        {
            return false;
        }

        var historyBytes = await fileSystem.ReadFileBytesAsync(
            writeLease,
            WoundHistoryState.HistoryPath);
        if (historyBytes is null)
            return false;

        try
        {
            using var commandDocument = JsonDocument.Parse(commandText);
            var history = WoundHistoryState.Parse(
                DecodeUtf8(historyBytes),
                WoundHistoryState.HistoryPath);
            MortalWoundTreatmentPersistedRequestCatalogResult catalog;
            if (pending is null)
            {
                catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(
                    commandDocument.RootElement,
                    pendingRoot: null,
                    history);
            }
            else
            {
                using var pendingDocument = JsonDocument.Parse(
                    DecodeUtf8(pending));
                catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(
                    commandDocument.RootElement,
                    pendingDocument.RootElement,
                    history);
            }
            return catalog.IsValid &&
                   catalog.HeldRequests.Count(RequestAgrees) == 1 &&
                   !catalog.FinalizedRequests.Any(RequestAgrees);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task WriteQuarantinedDurableSurfacesAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        if (!TryGetDurableBeforeImage(
                AcceptedMechanicsPlan.WoundCommandPath,
                out var commandBytes) ||
            !_beforeImagesByPath.TryGetValue(
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                out var pendingBeforeImage))
        {
            throw new InvalidDataException(
                "Treatment publication quarantine requires an exact command before-image and pending-surface inventory.");
        }

        var command = JsonNode.Parse(DecodeUtf8(commandBytes)) as JsonObject ??
            throw new InvalidDataException(
                "The durable treatment command root is not a JSON object.");
        var pendingBytes = pendingBeforeImage.Bytes;
        var pending = pendingBytes is null
            ? null
            : JsonNode.Parse(DecodeUtf8(pendingBytes)) as JsonObject ??
              throw new InvalidDataException(
                  "The durable pending-treatment root is not a JSON object.");
        var quarantine = MortalWoundTreatmentDurableSurfaceQuarantine
            .RemoveExactRequestRows(
                command,
                pending,
                _receipt.Request.Coordinates.OperationKey,
                _receipt.Request.Coordinates.AttemptId,
                _receipt.Request.RequestFingerprint);
        if ((!quarantine.CommandChanged && !quarantine.PendingChanged) ||
            MortalWoundTreatmentDurableSurfaceQuarantine.ContainsExactRequestRows(
                command,
                pending,
                _receipt.Request.Coordinates.OperationKey,
                _receipt.Request.Coordinates.AttemptId,
                _receipt.Request.RequestFingerprint))
        {
            throw new InvalidDataException(
                "The exact durable treatment request could not be quarantined from its durable roots.");
        }

        if (quarantine.CommandChanged)
        {
            await fileSystem.WriteFileAtomicAsync(
                writeLease,
                AcceptedMechanicsPlan.WoundCommandPath,
                command.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
        if (quarantine.PendingChanged && pending is not null)
        {
            await fileSystem.WriteFileAtomicAsync(
                writeLease,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                pending.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
        if (!await DurableTreatmentAuthorityAbsentAsync(fileSystem, writeLease))
        {
            throw new InvalidDataException(
                "The quarantined treatment request remained present after durable readback.");
        }
    }

    private async Task<bool> DurableTreatmentAuthorityAbsentAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var commandBytes = await fileSystem.ReadFileBytesAsync(
            writeLease,
            AcceptedMechanicsPlan.WoundCommandPath);
        if (commandBytes is null)
            return false;
        var pendingBytes = await fileSystem.ReadFileBytesAsync(
            writeLease,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        try
        {
            var command = JsonNode.Parse(DecodeUtf8(commandBytes)) as JsonObject;
            var pending = pendingBytes is null
                ? null
                : JsonNode.Parse(DecodeUtf8(pendingBytes)) as JsonObject;
            return command is not null &&
                   (pendingBytes is null || pending is not null) &&
                   !MortalWoundTreatmentDurableSurfaceQuarantine
                       .ContainsExactRequestRows(
                           command,
                           pending,
                           _receipt.Request.Coordinates.OperationKey,
                           _receipt.Request.Coordinates.AttemptId,
                           _receipt.Request.RequestFingerprint);
        }
        catch (Exception exception) when (
            exception is JsonException or DecoderFallbackException)
        {
            return false;
        }
    }

    private bool TryGetDurableBeforeImage(
        string path,
        out byte[] bytes)
    {
        if (_beforeImagesByPath.TryGetValue(path, out var beforeImage) &&
            beforeImage.Bytes is { } value)
        {
            bytes = value;
            return true;
        }
        bytes = Array.Empty<byte>();
        return false;
    }

    private bool ContainsDurableCoordinates(string json) =>
        json.Contains(
            _receipt.Request.RequestFingerprint,
            StringComparison.Ordinal) &&
        json.Contains(
            _receipt.Request.Coordinates.OperationKey,
            StringComparison.Ordinal);

    private bool RequestAgrees(MortalWoundTreatmentAttemptRequest candidate) =>
        string.Equals(
            candidate.Coordinates.OperationKey,
            _receipt.Request.Coordinates.OperationKey,
            StringComparison.Ordinal) &&
        string.Equals(
            candidate.Coordinates.AttemptId,
            _receipt.Request.Coordinates.AttemptId,
            StringComparison.Ordinal) &&
        string.Equals(
            candidate.RequestFingerprint,
            _receipt.Request.RequestFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            candidate.ResourceAuthority.AuthorityFingerprint,
            _receipt.Finalization.ResourceAuthorityFingerprint,
            StringComparison.Ordinal);

    private bool LiveHoldAgrees(
        MortalWoundTreatmentPublicationProbeResult probe) =>
        ReferenceEquals(_plan, _receipt.Plan) &&
        string.Equals(
            _bindingFingerprint,
            AcceptedMechanicsPlanFingerprints.ComputeInput(_receipt.Binding),
            StringComparison.Ordinal) &&
        probe.IsValid &&
        probe.Issues.Count == 0 &&
        probe.ChangedCount == 0 &&
        probe.State ==
            MortalWoundTreatmentResourceReservationState.ConfirmedHeld &&
        string.Equals(
            probe.OperationKey,
            _receipt.Request.Coordinates.OperationKey,
            StringComparison.Ordinal) &&
        string.Equals(
            probe.RequestFingerprint,
            _receipt.Request.RequestFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            probe.ResourceAuthorityFingerprint,
            _receipt.Finalization.ResourceAuthorityFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            probe.FinalizationFingerprint,
            _receipt.Finalization.FinalizationFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            probe.SessionGeneration,
            _receipt.SessionGeneration,
            StringComparison.Ordinal) &&
        probe.SessionGenerationRevision == _receipt.SessionGenerationRevision;

    private static MortalWoundTreatmentPublicationOperationResult?
        ToOperationFailure(MortalWoundTreatmentPublicationProbeResult probe)
    {
        if (probe.IsValid)
            return null;
        var code = probe.Issues.FirstOrDefault()?.Code ?? TokenStaleCode;
        var outcome = code switch
        {
            TokenMismatchCode =>
                MortalWoundTreatmentPublicationTransactionOutcome.TokenMismatch,
            TokenReplayedCode =>
                MortalWoundTreatmentPublicationTransactionOutcome.TokenReplayed,
            ReservationChangedCode =>
                MortalWoundTreatmentPublicationTransactionOutcome.ReservationChanged,
            _ => MortalWoundTreatmentPublicationTransactionOutcome.Stale
        };
        return new MortalWoundTreatmentPublicationOperationResult(
            false,
            probe.Issues,
            0,
            outcome);
    }

    private static MortalWoundTreatmentPublicationOperationResult
        ReservationChangedFailure() => new(
        false,
        new[]
        {
            new ValidationIssue(
                AcceptedMechanicsPlan.WoundCommandPath,
                IssueSeverity.Error,
                "The held treatment resource reservation changed before terminal cleanup.",
                ReservationChangedCode,
                actor: "accepted_turn",
                section: "wound_materialization",
                expected: "the exact unchanged confirmed treatment resource hold",
                actual: "live hold proof mismatch")
        },
        0,
        MortalWoundTreatmentPublicationTransactionOutcome.ReservationChanged);

    private static MortalWoundTreatmentPublicationOperationResult
        PublishedAgreementChangedFailure() => new(
        false,
        new[]
        {
            new ValidationIssue(
                AcceptedMechanicsPlan.WoundCommandPath,
                IssueSeverity.Error,
                "The published Mortal wound-treatment agreement changed before final resource commitment.",
                PublishedAgreementChangedCode,
                actor: "accepted_turn",
                section: "wound_materialization",
                expected: "the exact byte-for-byte canonical publication agreement captured under the publication lease",
                actual: "one or more published, retained, or deleted authority roots drifted before final commit")
        },
        0,
        MortalWoundTreatmentPublicationTransactionOutcome
            .PublishedAgreementChanged);

    private static MortalWoundTreatmentPublicationOperationResult
        TerminalReleaseOnlyFailure(string attemptedOperation) => new(
        false,
        new[]
        {
            new ValidationIssue(
                AcceptedMechanicsPlan.WoundCommandPath,
                IssueSeverity.Error,
                "The terminal-release-only Mortal wound-treatment receipt cannot be used for normal publication.",
                TokenStaleCode,
                actor: "accepted_turn",
                section: "wound_materialization",
                expected: "quarantine or terminal release",
                actual: attemptedOperation)
        },
        0,
        MortalWoundTreatmentPublicationTransactionOutcome.Stale);

    private static string DecodeUtf8(byte[] bytes)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var offset = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        return new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(bytes, offset, bytes.Length - offset);
    }

    private static bool IsDurableTreatmentSurface(string path) =>
        string.Equals(
            path,
            AcceptedMechanicsPlan.WoundCommandPath,
            StringComparison.Ordinal) ||
        string.Equals(
            path,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
            StringComparison.Ordinal);

    // Local stop only: an uncertainty observed after another scope's lease ended does
    // not consume the original receipt or release its held claims. No recovery is run.
    internal void ObservePublicationUncertainty(CoordinatedStatePublicationUncertainException uncertain)
    {
        ArgumentNullException.ThrowIfNull(uncertain);
        Interlocked.CompareExchange(ref _publicationUncertainty, ExceptionDispatchInfo.Capture(uncertain), null);
    }

    private void ThrowIfPublicationUncertain() => Volatile.Read(ref _publicationUncertainty)?.Throw();

    private static bool ClosesReceipt(
        MortalWoundTreatmentPublicationOperationResult result) =>
        result.Outcome is
            MortalWoundTreatmentPublicationTransactionOutcome.Finalized or
            MortalWoundTreatmentPublicationTransactionOutcome.Rearmed or
            MortalWoundTreatmentPublicationTransactionOutcome.HeldBlocked or
            MortalWoundTreatmentPublicationTransactionOutcome.CommandQuarantined or
            MortalWoundTreatmentPublicationTransactionOutcome.Released or
            MortalWoundTreatmentPublicationTransactionOutcome.ReleaseFailed or
            MortalWoundTreatmentPublicationTransactionOutcome.TokenReplayed;

    private void ObserveClosure(
        MortalWoundTreatmentPublicationOperationResult result)
    {
        if (result.IsValid &&
            result.Outcome ==
            MortalWoundTreatmentPublicationTransactionOutcome.Rearmed)
        {
            Interlocked.Exchange(ref _rearmed, 1);
        }
        if (ClosesReceipt(result))
            Interlocked.Exchange(ref _closed, 1);
    }

    private async Task DisposeLeaseAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        bool suppressFailure)
    {
        try
        {
            await writeLease.DisposeAsync();
        }
        catch (Exception closingFailure) when (suppressFailure || Volatile.Read(ref _publicationUncertainty) is not null)
        {
            if (Volatile.Read(ref _publicationUncertainty) is { } uncertainty)
                uncertainty.SourceException.Data["TreatmentPublicationLeaseCloseFailure"] = closingFailure;
            // The one-use registry state is already terminal or uncertainty is retained. A lock-handle close
            // failure must not turn an exact resource commit into a second spend.
        }
    }
}
