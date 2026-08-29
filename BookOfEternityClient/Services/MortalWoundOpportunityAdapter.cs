using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Services;

/// <summary>
/// Converts the GM-visible correlation for one Mortal wound occurrence into the
/// ordinary wound-response input. All mechanical authority is reconstructed from the
/// active detached pending-turn snapshot; this adapter never writes canonical state.
/// </summary>
internal static class MortalWoundOpportunityAdapter
{
    private const string SourcePath = "woundSourceEvent";
    private const int MaximumAcceptedEventOrdinal = 159;
    private const int MaximumReadableLength = 2_000;

    private static readonly IReadOnlySet<string> RootFields = Set(
        "schemaVersion",
        "adapterKind",
        "acceptedEventOrdinal",
        "opportunityRef",
        "owner",
        "domain",
        "profileKey",
        "source",
        "outcome",
        "safeContext",
        "worseningTarget");
    private static readonly IReadOnlySet<string> OwnerFields = Set(
        "realm", "ownerKind", "ownerId", "carrierPath");
    private static readonly IReadOnlySet<string> SourceFields = Set(
        "kind", "sourceId", "state");
    private static readonly IReadOnlySet<string> OutcomeFields = Set(
        "kind", "maximumSeverityRank", "readableCause");
    private static readonly IReadOnlySet<string> SafeContextFields = Set(
        "target", "cause", "allowedLocationKinds");
    private static readonly IReadOnlySet<string> WorseningTargetFields = Set(
        "woundId", "causeKind");
    private static readonly IReadOnlySet<string> AdapterKinds = Set(
        "formal", "qte", "combat", "trap", "check", "hazard", "narrative");
    private static readonly IReadOnlySet<string> LocationKinds = Set(
        "anatomical", "systemic", "mental", "spiritual_axis", "other");

    internal static WoundResponseInputCompositionResult ComposeAcceptedResponse(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease,
        JsonElement sourceEvent,
        GameResponse gameResponse)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(gameResponse);
        fs.EnsureCanonicalWriteLeaseActive(lease);

        var issues = new List<ValidationIssue>();
        if (!TryParseSourceCorrelation(sourceEvent, issues, out var correlation))
            return Failure(issues);

        var snapshotRead = PendingTurnSnapshotReader.ReadCurrent(
            fs,
            lease,
            new[]
            {
                MortalWoundOccurrenceState.StatePath,
                MortalWoundOpportunityReceiptState.StatePath
            });
        if (!snapshotRead.Success || snapshotRead.Snapshot is null)
            return Failure(snapshotRead.Issues);
        var snapshot = snapshotRead.Snapshot;

        if (!TryParseOccurrenceState(
                snapshot.ReadRequiredBytes(MortalWoundOccurrenceState.StatePath),
                issues,
                out var signedOccurrences) ||
            !TryParseReceiptState(
                snapshot.ReadRequiredBytes(MortalWoundOpportunityReceiptState.StatePath),
                issues,
                out var signedReceipts))
        {
            return Failure(issues);
        }

        issues.AddRange(
            MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(
                signedOccurrences!,
                signedReceipts!));
        if (issues.Count != 0)
            return Failure(issues);

        var matches = signedOccurrences!.Occurrences
            .Where(value => string.Equals(
                value.OpportunityRef,
                correlation!.OpportunityRef,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            Add(
                issues,
                SourcePath + ".opportunityRef",
                "mortal_wound_opportunity_adapter_occurrence_unresolved",
                "one exact signed pending Mortal-wound occurrence",
                $"matches={matches.Length}");
            return Failure(issues);
        }

        var occurrence = matches[0];
        ValidateCorrelation(correlation!, occurrence, issues);
        if (issues.Count != 0)
            return Failure(issues);

        if (!TryReadCurrentPartition(
                fs,
                snapshot,
                signedOccurrences,
                signedReceipts!,
                issues,
                out var currentReceipts))
        {
            return Failure(issues);
        }

        var producerBatch = signedOccurrences.Occurrences
            .Where(value => string.Equals(
                value.ProducerOperationKey,
                occurrence.ProducerOperationKey,
                StringComparison.Ordinal))
            .ToArray();
        var rebound = WoundAcceptedEventAuthorityComposer.RebindMortalOccurrence(
            occurrence,
            producerBatch,
            snapshot.SessionId,
            snapshot.RequestId,
            snapshot.SnapshotToken,
            snapshot.TurnNumber);
        if (!rebound.Success)
            return Failure(rebound.Issues);

        var binding = new WoundAcceptedTurnBinding(
            snapshot.SessionId,
            snapshot.RequestId,
            snapshot.SnapshotToken,
            snapshot.Realm,
            snapshot.TurnNumber,
            rebound.Events,
            rebound.EventsFingerprint);
        var selectedEvent = rebound.Events[occurrence.AcceptedEventOrdinal];

        WoundOpportunityWorseningTargetEvidence? worseningEvidence = null;
        if (occurrence.WorseningTarget is not null &&
            !TryResolveSignedWorseningTarget(
                fs,
                lease,
                occurrence,
                issues,
                out worseningEvidence))
        {
            return Failure(issues);
        }

        var opportunityResult = WoundOpportunityAuthority.Compose(
            new WoundOpportunityBuildRequest(
                binding,
                occurrence.OccurrenceId,
                occurrence.OpportunityRef,
                selectedEvent.EventRef,
                occurrence.Owner with { },
                occurrence.Domain,
                occurrence.ProfileKey,
                occurrence.Source.Kind,
                occurrence.Source.SourceId,
                occurrence.Source.State,
                new WoundOpportunityEventEvidence(
                    occurrence.AdapterKind,
                    selectedEvent.Kind,
                    selectedEvent.AuthorityId,
                    occurrence.Outcome.Kind,
                    occurrence.Outcome.MaximumSeverityRank,
                    occurrence.Outcome.ReadableCause),
                occurrence.HardMaximumSeverityRank,
                ProjectGuarantee(occurrence.GuaranteedTrigger),
                WoundOpportunityAuthority.CloneSafeContext(
                    occurrence.SafeContext),
                worseningEvidence));
        if (!opportunityResult.Success || opportunityResult.Opportunity is null)
            return Failure(opportunityResult.Issues);

        var priorReceipts = currentReceipts!.Receipts
            .Select(value => new WoundOpportunityDecisionReceipt(
                value.OpportunityId,
                value.DecisionFingerprint,
                value.OperationKey))
            .ToArray();
        return WoundResponseInputComposer.Compose(
            binding,
            new[] { opportunityResult.Opportunity },
            gameResponse.WoundDecisions,
            PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(
                gameResponse.Response),
            priorReceipts);
    }

    private static bool TryParseSourceCorrelation(
        JsonElement root,
        List<ValidationIssue> issues,
        out SourceCorrelation? correlation)
    {
        correlation = null;
        if (root.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                SourcePath,
                "mortal_wound_opportunity_adapter_invalid_root",
                "one closed version-1 source correlation object",
                root.ValueKind.ToString());
            return false;
        }

        ResourceMaterializationContract.FindDuplicateProperties(
            root,
            SourcePath,
            issues,
            "mortal_wound_opportunity_adapter_duplicate_property");
        ResourceMaterializationContract.ValidateClosedObject(
            root,
            SourcePath,
            RootFields,
            issues,
            "mortal_wound_opportunity_adapter_unknown_field");

        var schemaVersion = ReadInteger(
            root,
            "schemaVersion",
            SourcePath,
            issues);
        var adapterKind = ReadText(root, "adapterKind", SourcePath, issues);
        var acceptedEventOrdinal = ReadInteger(
            root,
            "acceptedEventOrdinal",
            SourcePath,
            issues);
        var opportunityRef = ReadText(
            root,
            "opportunityRef",
            SourcePath,
            issues);
        var owner = ParseOwner(root, issues);
        var domain = ReadText(root, "domain", SourcePath, issues);
        var profileKey = ReadText(root, "profileKey", SourcePath, issues);
        var source = ParseSource(root, issues);
        var outcome = ParseOutcome(root, issues);
        var safeContext = ParseSafeContext(root, issues);
        var worseningTarget = ParseWorseningTarget(root, issues);

        if (schemaVersion != 1)
            Invalid(issues, SourcePath + ".schemaVersion");
        if (adapterKind is null || !AdapterKinds.Contains(adapterKind))
            Invalid(issues, SourcePath + ".adapterKind");
        if (acceptedEventOrdinal is null or < 0 or > MaximumAcceptedEventOrdinal)
            Invalid(issues, SourcePath + ".acceptedEventOrdinal");
        if (!PublicRefIsValid(opportunityRef))
            Invalid(issues, SourcePath + ".opportunityRef");
        if (owner is not null && !MortalOwnerIsValid(owner))
            Invalid(issues, SourcePath + ".owner");
        if (!string.Equals(domain, "physical", StringComparison.Ordinal))
            Invalid(issues, SourcePath + ".domain");
        if (!Exact(profileKey))
            Invalid(issues, SourcePath + ".profileKey");
        if (source is not null &&
            (!Exact(source.Kind) || !Exact(source.SourceId) || !Exact(source.State)))
        {
            Invalid(issues, SourcePath + ".source");
        }
        if (outcome is not null &&
            (!string.Equals(outcome.Kind, "harmful", StringComparison.Ordinal) ||
             outcome.MaximumSeverityRank is < 1 or > 4 ||
             !Readable(outcome.ReadableCause)))
        {
            Invalid(issues, SourcePath + ".outcome");
        }
        if (safeContext is not null && !SafeContextIsValid(safeContext))
            Invalid(issues, SourcePath + ".safeContext");
        if (worseningTarget is not null &&
            (!Exact(worseningTarget.WoundId) ||
             worseningTarget.CauseKind is not ("deterioration" or "retrauma")))
        {
            Invalid(issues, SourcePath + ".worseningTarget");
        }

        if (issues.Count != 0 || adapterKind is null ||
            acceptedEventOrdinal is null || opportunityRef is null ||
            owner is null || domain is null || profileKey is null ||
            source is null || outcome is null || safeContext is null)
        {
            return false;
        }

        correlation = new SourceCorrelation(
            adapterKind,
            acceptedEventOrdinal.Value,
            opportunityRef,
            owner,
            domain,
            profileKey,
            source,
            outcome,
            safeContext,
            worseningTarget);
        return true;
    }

    private static WoundOwnerCoordinate? ParseOwner(
        JsonElement root,
        List<ValidationIssue> issues)
    {
        if (!TryReadObject(root, "owner", SourcePath, OwnerFields, issues, out var value))
            return null;
        var path = SourcePath + ".owner";
        var realm = ReadText(value, "realm", path, issues);
        var ownerKind = ReadText(value, "ownerKind", path, issues);
        var ownerId = ReadText(value, "ownerId", path, issues);
        var carrierPath = ReadText(value, "carrierPath", path, issues);
        return new WoundOwnerCoordinate(
            realm ?? string.Empty,
            ownerKind ?? string.Empty,
            ownerId ?? string.Empty,
            carrierPath ?? string.Empty);
    }

    private static MortalWoundOccurrenceSource? ParseSource(
        JsonElement root,
        List<ValidationIssue> issues)
    {
        if (!TryReadObject(root, "source", SourcePath, SourceFields, issues, out var value))
            return null;
        var path = SourcePath + ".source";
        return new MortalWoundOccurrenceSource(
            ReadText(value, "kind", path, issues) ?? string.Empty,
            ReadText(value, "sourceId", path, issues) ?? string.Empty,
            ReadText(value, "state", path, issues) ?? string.Empty);
    }

    private static MortalWoundOccurrenceOutcome? ParseOutcome(
        JsonElement root,
        List<ValidationIssue> issues)
    {
        if (!TryReadObject(root, "outcome", SourcePath, OutcomeFields, issues, out var value))
            return null;
        var path = SourcePath + ".outcome";
        var maximum = ReadInteger(value, "maximumSeverityRank", path, issues);
        return new MortalWoundOccurrenceOutcome(
            ReadText(value, "kind", path, issues) ?? string.Empty,
            maximum ?? 0,
            ReadText(value, "readableCause", path, issues) ?? string.Empty);
    }

    private static WoundOpportunitySafeContext? ParseSafeContext(
        JsonElement root,
        List<ValidationIssue> issues)
    {
        if (!TryReadObject(
                root,
                "safeContext",
                SourcePath,
                SafeContextFields,
                issues,
                out var value))
        {
            return null;
        }

        var path = SourcePath + ".safeContext";
        var target = ReadText(value, "target", path, issues) ?? string.Empty;
        var cause = ReadText(value, "cause", path, issues) ?? string.Empty;
        var locations = new List<string>();
        if (!value.TryGetProperty("allowedLocationKinds", out var array))
        {
            Missing(issues, path + ".allowedLocationKinds");
        }
        else if (array.ValueKind != JsonValueKind.Array ||
                 array.GetArrayLength() is < 1 or > 5)
        {
            Invalid(issues, path + ".allowedLocationKinds");
        }
        else
        {
            var index = 0;
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String ||
                    item.GetString() is not { } location)
                {
                    Invalid(issues, $"{path}.allowedLocationKinds[{index}]");
                }
                else
                {
                    locations.Add(location);
                }
                index++;
            }
        }
        return new WoundOpportunitySafeContext(target, cause, locations);
    }

    private static MortalWoundOccurrenceWorseningTarget? ParseWorseningTarget(
        JsonElement root,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("worseningTarget", out var value))
            return null;
        const string path = SourcePath + ".worseningTarget";
        if (value.ValueKind != JsonValueKind.Object)
        {
            Invalid(issues, path);
            return null;
        }
        ResourceMaterializationContract.ValidateClosedObject(
            value,
            path,
            WorseningTargetFields,
            issues,
            "mortal_wound_opportunity_adapter_unknown_field");
        return new MortalWoundOccurrenceWorseningTarget(
            ReadText(value, "woundId", path, issues) ?? string.Empty,
            ReadText(value, "causeKind", path, issues) ?? string.Empty);
    }

    private static bool TryReadObject(
        JsonElement parent,
        string propertyName,
        string parentPath,
        IReadOnlySet<string> fields,
        List<ValidationIssue> issues,
        out JsonElement value)
    {
        value = default;
        var path = parentPath + "." + propertyName;
        if (!parent.TryGetProperty(propertyName, out value))
        {
            Missing(issues, path);
            return false;
        }
        if (value.ValueKind != JsonValueKind.Object)
        {
            Invalid(issues, path);
            return false;
        }
        ResourceMaterializationContract.ValidateClosedObject(
            value,
            path,
            fields,
            issues,
            "mortal_wound_opportunity_adapter_unknown_field");
        return true;
    }

    private static void ValidateCorrelation(
        SourceCorrelation correlation,
        MortalWoundOccurrence occurrence,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(correlation.AdapterKind, occurrence.AdapterKind, StringComparison.Ordinal) ||
            correlation.AcceptedEventOrdinal != occurrence.AcceptedEventOrdinal ||
            !string.Equals(correlation.OpportunityRef, occurrence.OpportunityRef, StringComparison.Ordinal) ||
            correlation.Owner != occurrence.Owner ||
            !string.Equals(correlation.Domain, occurrence.Domain, StringComparison.Ordinal) ||
            !string.Equals(correlation.ProfileKey, occurrence.ProfileKey, StringComparison.Ordinal) ||
            correlation.Source != occurrence.Source ||
            correlation.Outcome != occurrence.Outcome ||
            !correlation.SafeContext.Equals(occurrence.SafeContext) ||
            !WorseningTargetsEqual(
                correlation.WorseningTarget,
                occurrence.WorseningTarget))
        {
            Add(
                issues,
                SourcePath,
                "mortal_wound_opportunity_adapter_correlation_mismatch",
                "an exact public projection of the signed occurrence",
                "one or more correlation fields differ");
        }
    }

    private static bool TryReadCurrentPartition(
        FileSystemManager fs,
        PendingTurnSnapshotReadAuthority snapshot,
        MortalWoundOccurrenceState signedOccurrences,
        MortalWoundOpportunityReceiptState signedReceipts,
        List<ValidationIssue> issues,
        out MortalWoundOpportunityReceiptState? currentReceipts)
    {
        currentReceipts = null;
        var currentOccurrenceParse = MortalWoundOccurrenceState.Parse(
            fs.ReadFileSync(MortalWoundOccurrenceState.StatePath),
            MortalWoundOccurrenceState.StatePath);
        var currentReceiptParse = MortalWoundOpportunityReceiptState.Parse(
            fs.ReadFileSync(MortalWoundOpportunityReceiptState.StatePath),
            MortalWoundOpportunityReceiptState.StatePath);
        if (!currentOccurrenceParse.IsValid || currentOccurrenceParse.State is null)
            issues.AddRange(currentOccurrenceParse.Issues);
        if (!currentReceiptParse.IsValid || currentReceiptParse.State is null)
            issues.AddRange(currentReceiptParse.Issues);
        if (issues.Count != 0)
            return false;

        var currentOccurrences = currentOccurrenceParse.State!;
        currentReceipts = currentReceiptParse.State!;
        issues.AddRange(
            MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(
                currentOccurrences,
                currentReceipts));
        if (issues.Count != 0)
            return false;

        if (currentReceipts.Receipts.Count < signedReceipts.Receipts.Count ||
            !signedReceipts.Receipts.SequenceEqual(
                currentReceipts.Receipts.Take(signedReceipts.Receipts.Count)))
        {
            CurrentStateConflict(issues, "the signed receipt prefix was replaced");
            return false;
        }

        var appended = currentReceipts.Receipts
            .Skip(signedReceipts.Receipts.Count)
            .ToArray();
        if (appended.Length > signedOccurrences.Occurrences.Count ||
            appended.Any(receipt =>
                receipt.SessionId != snapshot.SessionId ||
                receipt.RequestId != snapshot.RequestId ||
                receipt.SnapshotToken != snapshot.SnapshotToken ||
                receipt.Turn != snapshot.TurnNumber ||
                signedOccurrences.Occurrences.Count(occurrence =>
                    ReceiptRetainsOccurrence(receipt, occurrence)) != 1))
        {
            CurrentStateConflict(
                issues,
                "an appended receipt is not retained from this signed decision snapshot");
            return false;
        }

        var consumedIds = appended
            .Select(value => value.OpportunityId)
            .ToHashSet(StringComparer.Ordinal);
        var expectedRemaining = signedOccurrences.Occurrences
            .Where(value => !consumedIds.Contains(value.OccurrenceId))
            .ToArray();
        if (currentOccurrences.Occurrences.Count != expectedRemaining.Length ||
            !currentOccurrences.Occurrences.Select(value =>
                    (value.OccurrenceId, value.OccurrenceFingerprint))
                .SequenceEqual(expectedRemaining.Select(value =>
                    (value.OccurrenceId, value.OccurrenceFingerprint))))
        {
            CurrentStateConflict(
                issues,
                "the live pending partition is not the signed rows minus appended receipts");
            return false;
        }

        return true;
    }

    private static bool ReceiptRetainsOccurrence(
        MortalWoundOpportunityReceipt receipt,
        MortalWoundOccurrence occurrence)
    {
        if (occurrence.AcceptedEventOrdinal < 0 ||
            occurrence.AcceptedEventOrdinal >= occurrence.AcceptedEvents.Count)
        {
            return false;
        }
        var selected = occurrence.AcceptedEvents[occurrence.AcceptedEventOrdinal];
        return receipt.OpportunityId == occurrence.OccurrenceId &&
               receipt.EventRef == selected.EventRef &&
               receipt.EventSemanticFingerprint == selected.SemanticFingerprint &&
               receipt.SourceSessionId == occurrence.SourceSessionId &&
               receipt.SourceRequestId == occurrence.SourceRequestId &&
               receipt.SourceSnapshotToken == occurrence.SourceSnapshotToken &&
               receipt.SourceTurn == occurrence.SourceTurn &&
               receipt.ProducerOperationKey == occurrence.ProducerOperationKey &&
               receipt.ProducerCandidateOrdinal == occurrence.ProducerCandidateOrdinal &&
               receipt.ProducerCandidateCount == occurrence.ProducerCandidateCount &&
               receipt.SourceResultFingerprint == occurrence.SourceResultFingerprint &&
               receipt.CandidateFingerprint == occurrence.CandidateFingerprint &&
               receipt.OccurrenceFingerprint == occurrence.OccurrenceFingerprint;
    }

    private static bool TryResolveSignedWorseningTarget(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease,
        MortalWoundOccurrence occurrence,
        List<ValidationIssue> issues,
        out WoundOpportunityWorseningTargetEvidence? evidence)
    {
        evidence = null;
        var target = occurrence.WorseningTarget!;
        var read = PendingTurnSnapshotReader.ReadCurrent(
            fs,
            lease,
            new[]
            {
                MortalWoundOccurrenceState.StatePath,
                MortalWoundOpportunityReceiptState.StatePath,
                occurrence.Owner.CarrierPath
            });
        if (!read.Success || read.Snapshot is null)
        {
            issues.AddRange(read.Issues);
            return false;
        }

        if (!TryParseSignedCarrierRoot(
                read.Snapshot.ReadRequiredBytes(occurrence.Owner.CarrierPath),
                occurrence.Owner.CarrierPath,
                issues,
                out var carrierRoot))
        {
            return false;
        }

        try
        {
            var carriers = WoundCarrierCollectionAuthority.WithRoot(
                new WoundCarrierCatalogInput(null, null, null, null, null),
                occurrence.Owner.CarrierPath,
                carrierRoot!);
            var catalog = WoundCarrierCatalog.Build(carriers);
            issues.AddRange(catalog.Issues);
            if (issues.Count != 0 ||
                catalog.CountExactOccurrences(target.WoundId) != 1 ||
                !catalog.TryResolveOne(target.WoundId, out var resolved) ||
                !string.Equals(
                    resolved.FilePath,
                    occurrence.Owner.CarrierPath,
                    StringComparison.Ordinal) ||
                resolved.Coordinate.Realm != occurrence.Owner.Realm ||
                resolved.Coordinate.OwnerKind != occurrence.Owner.OwnerKind ||
                resolved.Coordinate.OwnerId != occurrence.Owner.OwnerId ||
                resolved.Coordinate.CarrierPath != occurrence.Owner.CarrierPath)
            {
                Add(
                    issues,
                    SourcePath + ".worseningTarget",
                    "mortal_wound_opportunity_adapter_worsening_target_unresolved",
                    "one exact active wound in the signed owner carrier before-image",
                    target.WoundId);
                return false;
            }

            evidence = new WoundOpportunityWorseningTargetEvidence(
                resolved.Wound,
                target.CauseKind);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                JsonException or NullReferenceException)
        {
            Add(
                issues,
                SourcePath + ".worseningTarget",
                "mortal_wound_opportunity_adapter_worsening_target_unresolved",
                "one exact active wound in the signed owner carrier before-image",
                exception.GetType().Name);
            return false;
        }
    }

    private static bool TryParseOccurrenceState(
        byte[] bytes,
        List<ValidationIssue> issues,
        out MortalWoundOccurrenceState? state)
    {
        state = null;
        if (!TryDecode(bytes, MortalWoundOccurrenceState.StatePath, issues, out var json))
            return false;
        var parsed = MortalWoundOccurrenceState.Parse(
            json,
            MortalWoundOccurrenceState.StatePath);
        if (!parsed.IsValid || parsed.State is null)
        {
            issues.AddRange(parsed.Issues);
            return false;
        }
        state = parsed.State;
        return true;
    }

    private static bool TryParseReceiptState(
        byte[] bytes,
        List<ValidationIssue> issues,
        out MortalWoundOpportunityReceiptState? state)
    {
        state = null;
        if (!TryDecode(
                bytes,
                MortalWoundOpportunityReceiptState.StatePath,
                issues,
                out var json))
        {
            return false;
        }
        var parsed = MortalWoundOpportunityReceiptState.Parse(
            json,
            MortalWoundOpportunityReceiptState.StatePath);
        if (!parsed.IsValid || parsed.State is null)
        {
            issues.AddRange(parsed.Issues);
            return false;
        }
        state = parsed.State;
        return true;
    }

    private static bool TryParseSignedCarrierRoot(
        byte[] bytes,
        string path,
        List<ValidationIssue> issues,
        out JsonObject? root)
    {
        root = null;
        if (!TryDecode(bytes, path, issues, out var json))
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                Add(
                    issues,
                    path,
                    "mortal_wound_opportunity_adapter_carrier_invalid",
                    "one signed canonical wound carrier object",
                    document.RootElement.ValueKind.ToString());
                return false;
            }
            ResourceMaterializationContract.FindDuplicateProperties(
                document.RootElement,
                path,
                issues,
                "mortal_wound_opportunity_adapter_carrier_duplicate_property");
            if (issues.Count != 0)
                return false;
            root = JsonNode.Parse(document.RootElement.GetRawText()) as JsonObject;
            if (root is null)
            {
                Add(
                    issues,
                    path,
                    "mortal_wound_opportunity_adapter_carrier_invalid",
                    "one signed canonical wound carrier object",
                    "unmaterializable root");
                return false;
            }
            return true;
        }
        catch (JsonException)
        {
            Add(
                issues,
                path,
                "mortal_wound_opportunity_adapter_carrier_invalid",
                "one signed canonical wound carrier object",
                "malformed JSON");
            return false;
        }
    }

    private static bool TryDecode(
        byte[] bytes,
        string path,
        List<ValidationIssue> issues,
        out string json)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true);
            json = reader.ReadToEnd();
            return true;
        }
        catch (DecoderFallbackException)
        {
            json = string.Empty;
            Add(
                issues,
                path,
                "mortal_wound_opportunity_adapter_snapshot_json_invalid",
                "strict UTF-8 JSON",
                "invalid UTF-8");
            return false;
        }
    }

    private static WoundGuaranteedTriggerEvidence? ProjectGuarantee(
        WoundGuaranteedTriggerAuthority? value) => value is null
        ? null
        : new WoundGuaranteedTriggerEvidence(
            value.TriggerId,
            value.SourceKind,
            value.SourceId,
            value.SourceState,
            value.Realm,
            value.Domain,
            value.Owner with { },
            value.RequiredSeverityRank,
            value.MaterializedAtTurn,
            value.SourceContractFingerprint);

    private static string? ReadText(
        JsonElement parent,
        string propertyName,
        string parentPath,
        List<ValidationIssue> issues)
    {
        var path = parentPath + "." + propertyName;
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            Missing(issues, path);
            return null;
        }
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text)
        {
            Invalid(issues, path);
            return null;
        }
        return text;
    }

    private static int? ReadInteger(
        JsonElement parent,
        string propertyName,
        string parentPath,
        List<ValidationIssue> issues)
    {
        var path = parentPath + "." + propertyName;
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            Missing(issues, path);
            return null;
        }
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
        {
            Invalid(issues, path);
            return null;
        }
        return result;
    }

    private static bool MortalOwnerIsValid(WoundOwnerCoordinate owner) => owner switch
    {
        {
            Realm: "mortal_world",
            OwnerKind: "player",
            OwnerId: "player_current",
            CarrierPath: WoundCarrierCatalog.PlayerPath
        } => true,
        {
            Realm: "mortal_world",
            OwnerKind: "npc",
            CarrierPath: WoundCarrierCatalog.NpcPath
        } => Exact(owner.OwnerId),
        {
            Realm: "mortal_world",
            OwnerKind: "combatant" or "combatant_member",
            CarrierPath: WoundCarrierCatalog.EnemiesPath or WoundCarrierCatalog.AlliesPath
        } => Exact(owner.OwnerId),
        _ => false
    };

    private static bool SafeContextIsValid(WoundOpportunitySafeContext value) =>
        Readable(value.Target) &&
        Readable(value.Cause) &&
        value.AllowedLocationKinds.Count is >= 1 and <= 5 &&
        value.AllowedLocationKinds.All(LocationKinds.Contains) &&
        value.AllowedLocationKinds.Distinct(StringComparer.Ordinal).Count() ==
        value.AllowedLocationKinds.Count;

    private static bool WorseningTargetsEqual(
        MortalWoundOccurrenceWorseningTarget? left,
        MortalWoundOccurrenceWorseningTarget? right) =>
        left is null
            ? right is null
            : right is not null &&
              left.WoundId == right.WoundId &&
              left.CauseKind == right.CauseKind;

    private static bool Exact(string? value) =>
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static bool PublicRefIsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 256 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static bool Readable(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumReadableLength &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        !value.Any(char.IsControl);

    private static WoundResponseInputCompositionResult Failure(
        IEnumerable<ValidationIssue> issues)
    {
        var values = issues.ToArray();
        if (values.Length == 0)
        {
            values = new[]
            {
                new ValidationIssue(
                    SourcePath,
                    IssueSeverity.Error,
                    "Mortal wound opportunity composition failed closed.",
                    "mortal_wound_opportunity_adapter_failed")
            };
        }
        return new WoundResponseInputCompositionResult(
            null,
            Array.Empty<WoundOpportunityAuthority>(),
            Array.Empty<WoundAcceptedTransitionDraft>(),
            Array.Empty<WoundPlayerNotification>(),
            Array.Empty<WoundOpportunityDecisionReceipt>(),
            values);
    }

    private static void Missing(List<ValidationIssue> issues, string path) =>
        Add(
            issues,
            path,
            "mortal_wound_opportunity_adapter_missing_field",
            "one required public correlation field",
            "missing");

    private static void Invalid(List<ValidationIssue> issues, string path) =>
        Add(
            issues,
            path,
            "mortal_wound_opportunity_adapter_invalid_field",
            "one valid public correlation value",
            "malformed or out of range");

    private static void CurrentStateConflict(
        List<ValidationIssue> issues,
        string actual) =>
        Add(
            issues,
            MortalWoundOpportunityReceiptState.StatePath,
            "mortal_wound_opportunity_adapter_current_state_conflict",
            "the signed append-only receipt prefix and its exact pending partition",
            actual);

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            $"Mortal wound opportunity source is invalid: {actual}.",
            code,
            expected: expected,
            actual: actual,
            repairHint:
            "Repeat only the public source correlation from the current signed wound occurrence; never supply authority, state after-images, or inferred targets."));

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private sealed record SourceCorrelation(
        string AdapterKind,
        int AcceptedEventOrdinal,
        string OpportunityRef,
        WoundOwnerCoordinate Owner,
        string Domain,
        string ProfileKey,
        MortalWoundOccurrenceSource Source,
        MortalWoundOccurrenceOutcome Outcome,
        WoundOpportunitySafeContext SafeContext,
        MortalWoundOccurrenceWorseningTarget? WorseningTarget);
}
