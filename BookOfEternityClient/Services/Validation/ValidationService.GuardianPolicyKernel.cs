using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;
public partial class ValidationService
{
    private GuardianPolicyContext? _guardianPolicyContextInProgress;

    private enum GuardianTrackedSnapshotFileStatus
    {
        MissingManifest,
        UnusableManifest,
        MissingSnapshotFile,
        InvalidSnapshotFile,
        Usable
    }

    private sealed record GuardianTrackedSnapshotFileResolution(
        ValidatedPendingTurnSnapshotStatus ManifestStatus,
        GuardianTrackedSnapshotFileStatus FileStatus,
        ValidationPendingTurnSnapshotManifest? Manifest,
        string? SnapshotJson);

    private enum GuardianBaselineFailureKind
    {
        None,
        MissingManifest,
        UnusableManifest,
        MissingSnapshotFile,
        InvalidSnapshotFile
    }

    private sealed class GuardianPolicyContext
    {
        public bool CurrentStateReadable { get; set; } = true;
        public bool HasCurrentRoot { get; set; }
        public JsonElement CurrentRoot { get; set; }
        public bool HasPreTurnRoot { get; set; }
        public JsonElement PreTurnRoot { get; set; }
        public bool HasCurrentAuthorityRoot { get; set; }
        public JsonElement CurrentAuthorityRoot { get; set; }
        public bool HasPreTurnAuthorityRoot { get; set; }
        public JsonElement PreTurnAuthorityRoot { get; set; }
        public bool HasCurrentGuardiansArray { get; set; }
        public bool HasCurrentUpdateGuardians { get; set; }
        public bool HasCurrentGuardianPowerEvents { get; set; }
        public bool HasCurrentActiveGuardian { get; set; }
        public JsonElement CurrentActiveGuardian { get; set; }
        public GuardianTrackedSnapshotFileResolution PreTurnGuardiansSnapshot { get; set; } =
            new(ValidatedPendingTurnSnapshotStatus.Missing, GuardianTrackedSnapshotFileStatus.MissingManifest, null, null);
        public Dictionary<string, JsonElement> CurrentGuardiansById { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, JsonElement> PreTurnGuardiansById { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, JsonElement> AuthorizedSameTurnCreateGuardiansById { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<JsonObject> AuthorizedSameTurnGuardianCommands { get; } = new();
        public List<JsonObject> AuthorizedCurrentGuardianPowerEvents { get; } = new();
        public GuardianPowerEventAuthorityStatus CurrentGuardianPowerEventAuthorityStatus { get; set; } = GuardianPowerEventAuthorityStatus.None;
        public string? CurrentGuardianPowerEventAuthorityFailureDescription { get; set; }
        public Dictionary<string, List<string>> ReasoningAliasLookup { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> BaselineGuardianIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AuthoritativeGuardianIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AuthorizedSameTurnCreateGuardianIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool HasUsableValidatedPreTurnGuardiansSnapshot =>
            PreTurnGuardiansSnapshot.FileStatus == GuardianTrackedSnapshotFileStatus.Usable;
    }

    private sealed class GuardianProjectTrackerPolicyContext
    {
        public bool CurrentStateReadable { get; set; } = true;
        public bool HasCurrentRoot { get; set; }
        public JsonElement CurrentRoot { get; set; }
        public GuardianCurrentStateFailureKind CurrentStateFailureKind { get; set; } = GuardianCurrentStateFailureKind.MissingCurrentState;
        public bool HasPreTurnRoot { get; set; }
        public JsonElement PreTurnRoot { get; set; }
        public bool HasProjectedAuthorityRoot { get; set; }
        public JsonElement ProjectedAuthorityRoot { get; set; }
        public bool HasCurrentAuthorityRoot { get; set; }
        public JsonElement CurrentAuthorityRoot { get; set; }
        public GuardianTrackedSnapshotFileResolution PreTurnTrackerSnapshot { get; set; } =
            new(ValidatedPendingTurnSnapshotStatus.Missing, GuardianTrackedSnapshotFileStatus.MissingManifest, null, null);

        public bool HasUsableValidatedPreTurnTrackerSnapshot =>
            PreTurnTrackerSnapshot.FileStatus == GuardianTrackedSnapshotFileStatus.Usable;
    }

    private enum GuardianCurrentStateFailureKind
    {
        None,
        MissingCurrentState,
        UnreadableCurrentState
    }

    private enum GuardianPowerEventAuthorityStatus
    {
        None,
        Resolved,
        MissingValidatedPreTurnJournalIdentity,
        InvalidValidatedPreTurnJournalIdentity,
        InvalidRawPowerEvents
    }

    internal sealed record GuardianPolicyContextDebugSnapshot(
        bool CurrentStateReadable,
        bool HasCurrentRoot,
        bool HasPreTurnRoot,
        string ManifestStatus,
        string PreTurnGuardiansSnapshotFileStatus,
        int CurrentGuardianCount,
        int PreTurnGuardianCount,
        int AuthorizedSameTurnCreateCount,
        bool HasCurrentActiveGuardian,
        string? CurrentActiveGuardianId,
        IReadOnlyCollection<string> BaselineGuardianIds,
        IReadOnlyCollection<string> AuthoritativeGuardianIds,
        IReadOnlyCollection<string> AuthorizedSameTurnCreateGuardianIds,
        string CurrentGuardianPowerEventAuthorityStatus,
        string? CurrentGuardianPowerEventAuthorityFailureDescription,
        bool HasPreTurnAuthorityRoot,
        bool HasCurrentAuthorityRoot,
        string? PreTurnAuthorityRootJson,
        string? CurrentAuthorityRootJson);

    internal sealed record GuardianProjectTrackerPolicyContextDebugSnapshot(
        bool CurrentStateReadable,
        string CurrentStateFailureKind,
        bool HasPreTurnRoot,
        string ManifestStatus,
        string PreTurnTrackerSnapshotFileStatus,
        bool HasProjectedAuthorityRoot,
        bool HasCurrentAuthorityRoot,
        IReadOnlyCollection<string> ProjectedActiveProjectKeys,
        IReadOnlyCollection<string> CurrentActiveProjectKeys,
        string? ProjectedAuthorityRootJson,
        string? CurrentAuthorityRootJson);

    private async Task<GuardianPolicyContext> ResolveGuardianPolicyContextAsync()
    {
        var currentJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var preTurnResolution = await ResolveValidatedGuardianTrackedSnapshotFileAsync("game_state/meta/guardians.json");
        return BuildGuardianPolicyContext(currentJson, preTurnResolution);
    }

    private GuardianPolicyContext ResolveGuardianPolicyContextSync()
    {
        var currentJson = ReadCurrentTrackedFileSync("game_state/meta/guardians.json");
        var preTurnResolution = ResolveValidatedGuardianTrackedSnapshotFileSync("game_state/meta/guardians.json");
        return BuildGuardianPolicyContext(currentJson, preTurnResolution);
    }

    private GuardianPolicyContext BuildGuardianPolicyContext(
        string? currentGuardianJson,
        GuardianTrackedSnapshotFileResolution preTurnResolution)
    {
        var context = new GuardianPolicyContext
        {
            PreTurnGuardiansSnapshot = preTurnResolution
        };
        var previousInProgress = _guardianPolicyContextInProgress;
        _guardianPolicyContextInProgress = context;
        try
        {
            if (preTurnResolution.FileStatus == GuardianTrackedSnapshotFileStatus.Usable &&
                !string.IsNullOrWhiteSpace(preTurnResolution.SnapshotJson))
            {
                try
                {
                    using var preTurnDoc = JsonDocument.Parse(preTurnResolution.SnapshotJson);
                    context.PreTurnRoot = preTurnDoc.RootElement.Clone();
                    context.HasPreTurnRoot = context.PreTurnRoot.ValueKind == JsonValueKind.Object;
                    foreach (var (guardianId, guardian) in ReadGuardianStateMap(context.PreTurnRoot))
                        context.PreTurnGuardiansById[guardianId] = guardian;
                }
                catch
                {
                    context.PreTurnGuardiansSnapshot = preTurnResolution with
                    {
                        FileStatus = GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile,
                        SnapshotJson = null
                    };
                    context.HasPreTurnRoot = false;
                }
            }

            if (string.IsNullOrWhiteSpace(currentGuardianJson))
                return context;

            try
            {
                using var currentDoc = JsonDocument.Parse(currentGuardianJson);
                if (currentDoc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    context.CurrentStateReadable = false;
                    return context;
                }

                context.CurrentRoot = currentDoc.RootElement.Clone();
                context.HasCurrentRoot = true;
                context.HasCurrentGuardiansArray =
                    context.CurrentRoot.TryGetProperty("guardians", out var currentGuardians) &&
                    currentGuardians.ValueKind == JsonValueKind.Array;
                context.HasCurrentUpdateGuardians =
                    context.CurrentRoot.TryGetProperty("UpdateGuardians", out var updates) &&
                    updates.ValueKind == JsonValueKind.Array;
                context.HasCurrentGuardianPowerEvents =
                    context.CurrentRoot.TryGetProperty("guardianPowerEvents", out var eventsNode) &&
                    eventsNode.ValueKind == JsonValueKind.Array;
                if (context.CurrentRoot.TryGetProperty("activeGuardian", out var activeGuardian) &&
                    activeGuardian.ValueKind == JsonValueKind.Object)
                {
                    context.HasCurrentActiveGuardian = true;
                    context.CurrentActiveGuardian = activeGuardian.Clone();
                }

                foreach (var (guardianId, guardian) in ReadGuardianStateMap(context.CurrentRoot))
                    context.CurrentGuardiansById[guardianId] = guardian;

                BuildGuardianIdentityAuthority(context);
                BuildGuardianAuthorityRoots(context);
                BuildAuthorizedGuardianPowerEventsForAuthority(context);
                if (context.AuthorizedCurrentGuardianPowerEvents.Count > 0 &&
                    context.CurrentGuardianPowerEventAuthorityStatus == GuardianPowerEventAuthorityStatus.Resolved)
                {
                    BuildGuardianAuthorityRoots(context);
                }
                BuildGuardianReasoningAuthority(context);
                return context;
            }
            catch
            {
                context.CurrentStateReadable = false;
                return context;
            }
        }
        finally
        {
            _guardianPolicyContextInProgress = previousInProgress;
        }
    }

    private void BuildGuardianIdentityAuthority(GuardianPolicyContext context)
    {
        if (context.HasUsableValidatedPreTurnGuardiansSnapshot && context.HasPreTurnRoot)
        {
            MergeGuardianAliasLookupFromStoredGuardians(
                context.PreTurnRoot,
                context.ReasoningAliasLookup,
                context.AuthoritativeGuardianIds);
        }

        foreach (var guardianId in context.AuthoritativeGuardianIds)
            context.BaselineGuardianIds.Add(guardianId);

        if (context.HasCurrentRoot)
        {
            var authorizationResult = AuthorizeGuardianCommandsForPolicy(
                context.CurrentRoot,
                "game_state/meta/guardians.json",
                guardianPolicyContext: context);
            foreach (var (guardianId, guardian) in authorizationResult.AuthorizedCreateGuardiansById)
            {
                RegisterGuardianAliasLookup(context.ReasoningAliasLookup, guardian);
                context.AuthoritativeGuardianIds.Add(guardianId);
                context.AuthorizedSameTurnCreateGuardianIds.Add(guardianId);
                context.AuthorizedSameTurnCreateGuardiansById[guardianId] = guardian;
            }

            context.AuthorizedSameTurnGuardianCommands.Clear();
            context.AuthorizedSameTurnGuardianCommands.AddRange(authorizationResult.AuthorizedCommands);
        }
    }

    private void BuildGuardianReasoningAuthority(GuardianPolicyContext context)
    {
        context.ReasoningAliasLookup.Clear();

        if (context.HasPreTurnAuthorityRoot)
        {
            MergeGuardianAliasLookupFromStoredGuardians(
                context.PreTurnAuthorityRoot,
                context.ReasoningAliasLookup,
                context.AuthoritativeGuardianIds);
        }
        else if (context.HasUsableValidatedPreTurnGuardiansSnapshot && context.HasPreTurnRoot)
        {
            MergeGuardianAliasLookupFromStoredGuardians(
                context.PreTurnRoot,
                context.ReasoningAliasLookup,
                context.AuthoritativeGuardianIds);
        }

        if (context.HasCurrentAuthorityRoot)
        {
            MergeGuardianAliasLookupFromStoredGuardians(
                context.CurrentAuthorityRoot,
                context.ReasoningAliasLookup,
                context.AuthoritativeGuardianIds);
        }
        else
        {
            foreach (var guardian in context.AuthorizedSameTurnCreateGuardiansById.Values)
                RegisterGuardianAliasLookup(context.ReasoningAliasLookup, guardian);
        }
    }

    private void BuildGuardianAuthorityRoots(GuardianPolicyContext context)
    {
        if (!HasUsableValidatedPreTurnGuardianBaseline(context))
            return;

        var preTurnRoot = TryParseJsonObject(context.PreTurnRoot);
        if (preTurnRoot == null)
            return;

        var currentTurn = ReadCurrentTurnNumberForProjectAuthority();
        var preTurnAuthorityRoot = CanonicalStateNormalizer.BuildGuardianAuthorityRootForValidation(
            preTurnRoot,
            currentRoot: null,
            authorizedCommands: null,
            authorizedCreateGuardiansById: null,
            authorizedPowerEvents: null,
            currentTurn);
        context.PreTurnAuthorityRoot = CloneJsonObjectToElement(preTurnAuthorityRoot);
        context.HasPreTurnAuthorityRoot = true;

        if (!context.CurrentStateReadable || !context.HasCurrentRoot)
            return;

        var currentRoot = TryParseJsonObject(context.CurrentRoot);
        if (currentRoot == null)
            return;

        var authorizedCreatesById = BuildAuthorizedGuardianCreateObjectsForAuthority(context);
        var currentAuthorityRoot = CanonicalStateNormalizer.BuildGuardianAuthorityRootForValidation(
            preTurnAuthorityRoot.DeepClone().AsObject(),
            currentRoot,
            context.AuthorizedSameTurnGuardianCommands,
            authorizedCreatesById,
            context.AuthorizedCurrentGuardianPowerEvents,
            currentTurn);
        context.CurrentAuthorityRoot = CloneJsonObjectToElement(currentAuthorityRoot);
        context.HasCurrentAuthorityRoot = true;
    }

    private void BuildAuthorizedGuardianPowerEventsForAuthority(GuardianPolicyContext context)
    {
        context.AuthorizedCurrentGuardianPowerEvents.Clear();
        context.CurrentGuardianPowerEventAuthorityStatus = GuardianPowerEventAuthorityStatus.None;
        context.CurrentGuardianPowerEventAuthorityFailureDescription = null;
        if (!context.HasCurrentRoot ||
            !context.HasCurrentAuthorityRoot ||
            !context.CurrentRoot.TryGetProperty("guardianPowerEvents", out var powerEvents) ||
            powerEvents.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        context.CurrentGuardianPowerEventAuthorityStatus = GuardianPowerEventAuthorityStatus.Resolved;
        var trackerContext = ResolveGuardianProjectTrackerPolicyContextSync();
        var knownPoliticalProjects = ReadKnownPoliticalGuardianPowerEventProjectsFromTrackerContext(trackerContext);
        var seenEventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenResonanceLifeScopeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var preTurnJournalIdentityResolution = ResolveValidatedPreTurnGuardianPowerJournalIdentityState();
        if (preTurnJournalIdentityResolution.Status != GuardianPowerJournalIdentityBaselineStatus.Resolved ||
            preTurnJournalIdentityResolution.IdentityState == null)
        {
            context.CurrentGuardianPowerEventAuthorityStatus =
                preTurnJournalIdentityResolution.Status == GuardianPowerJournalIdentityBaselineStatus.MissingValidatedSnapshotJournal
                    ? GuardianPowerEventAuthorityStatus.MissingValidatedPreTurnJournalIdentity
                    : GuardianPowerEventAuthorityStatus.InvalidValidatedPreTurnJournalIdentity;
            context.CurrentGuardianPowerEventAuthorityFailureDescription = preTurnJournalIdentityResolution.FailureDescription;
            return;
        }

        seenEventIds.UnionWith(preTurnJournalIdentityResolution.IdentityState.EventIds);
        seenResonanceLifeScopeKeys.UnionWith(preTurnJournalIdentityResolution.IdentityState.ResonanceLifeScopeKeys);

        foreach (var item in powerEvents.EnumerateArray())
        {
            if (!TryAuthorizeGuardianPowerEventForAuthority(item, context, knownPoliticalProjects, out var authorizedEvent))
            {
                context.CurrentGuardianPowerEventAuthorityStatus = GuardianPowerEventAuthorityStatus.InvalidRawPowerEvents;
                context.CurrentGuardianPowerEventAuthorityFailureDescription =
                    "current raw guardianPowerEvents cannot be authorized into strict guardian authority";
                context.AuthorizedCurrentGuardianPowerEvents.Clear();
                return;
            }

            var eventId = GuardianPowerEventState.GetEventId(authorizedEvent);
            if (!string.IsNullOrWhiteSpace(eventId) && !seenEventIds.Add(eventId))
            {
                context.CurrentGuardianPowerEventAuthorityStatus = GuardianPowerEventAuthorityStatus.InvalidRawPowerEvents;
                context.CurrentGuardianPowerEventAuthorityFailureDescription =
                    $"current raw guardianPowerEvents reuses append-only eventId '{eventId}'";
                context.AuthorizedCurrentGuardianPowerEvents.Clear();
                return;
            }

            if (string.Equals(GetFirstNonEmptyString(item, "reasonType"), "resonance", StringComparison.OrdinalIgnoreCase) &&
                TryBuildGuardianResonanceLifeScopeKey(item, out var resonanceLifeScopeKey) &&
                !seenResonanceLifeScopeKeys.Add(resonanceLifeScopeKey))
            {
                context.CurrentGuardianPowerEventAuthorityStatus = GuardianPowerEventAuthorityStatus.InvalidRawPowerEvents;
                context.CurrentGuardianPowerEventAuthorityFailureDescription =
                    $"current raw guardianPowerEvents duplicates resonance life scope '{resonanceLifeScopeKey}'";
                context.AuthorizedCurrentGuardianPowerEvents.Clear();
                return;
            }

            if (authorizedEvent != null)
                context.AuthorizedCurrentGuardianPowerEvents.Add(authorizedEvent);
        }
    }

    private bool TryAuthorizeGuardianPowerEventForAuthority(
        JsonElement item,
        GuardianPolicyContext context,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        out JsonObject authorizedEvent)
    {
        authorizedEvent = null!;
        if (item.ValueKind != JsonValueKind.Object)
            return false;

        var guardianId = GetFirstNonEmptyString(item, "guardianId");
        if (string.IsNullOrWhiteSpace(guardianId) || !context.AuthoritativeGuardianIds.Contains(guardianId))
            return false;

        var scratchIssues = new List<ValidationIssue>();
        ValidateIntegerField(item, "game_state/meta/guardians.json.guardianPowerEvents", scratchIssues, "delta");
        if (!item.TryGetProperty("delta", out var deltaNode) ||
            deltaNode.ValueKind != JsonValueKind.Number ||
            !deltaNode.TryGetInt32(out var delta) ||
            delta == 0)
        {
            return false;
        }

        var reasonType = GetFirstNonEmptyString(item, "reasonType");
        if (string.IsNullOrWhiteSpace(reasonType) ||
            !GuardianPowerEventState.IsValidReasonType(reasonType) ||
            string.Equals(reasonType, "guardian_quest", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(GetFirstNonEmptyString(item, "eventId")) ||
            string.IsNullOrWhiteSpace(GetFirstNonEmptyString(item, "sourceSurface")) ||
            string.IsNullOrWhiteSpace(GetFirstNonEmptyString(item, "sourceId")) ||
            string.IsNullOrWhiteSpace(GetFirstNonEmptyString(item, "title")) ||
            string.IsNullOrWhiteSpace(GetFirstNonEmptyString(item, "summary")))
        {
            return false;
        }

        var relatedGuardianId = GetFirstNonEmptyString(item, "relatedGuardianId");
        ValidateOptionalNullableStringField(item, "game_state/meta/guardians.json.guardianPowerEvents", scratchIssues, "relatedGuardianId");
        ValidateOptionalKnownRelatedGuardianId(
            "game_state/meta/guardians.json.guardianPowerEvents.relatedGuardianId",
            guardianId,
            relatedGuardianId,
            scratchIssues,
            new HashSet<string>(context.AuthoritativeGuardianIds, StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(relatedGuardianId) && !context.AuthoritativeGuardianIds.Contains(relatedGuardianId))
            return false;

        var visibility = GetFirstNonEmptyString(item, "visibility");
        if (!string.IsNullOrWhiteSpace(visibility) && !GuardianPowerEventState.IsValidVisibility(visibility))
            return false;

        if (item.TryGetProperty("appliedAt", out var appliedAtNode) &&
            appliedAtNode.ValueKind != JsonValueKind.Null)
        {
            var appliedAt = GetFirstNonEmptyString(item, "appliedAt");
            if (!string.IsNullOrWhiteSpace(appliedAt) && !DateTimeOffset.TryParse(appliedAt, out _))
                return false;
        }

        if (!item.TryGetProperty("audit", out var auditNode) || auditNode.ValueKind != JsonValueKind.Object)
            return false;

        var sourceSurface = GetFirstNonEmptyString(item, "sourceSurface");
        var sourceId = GetFirstNonEmptyString(item, "sourceId");
        ValidateGuardianPowerEventAudit(
            guardianId,
            relatedGuardianId,
            sourceSurface,
            sourceId,
            reasonType!,
            delta,
            auditNode,
            "game_state/meta/guardians.json.guardianPowerEvents",
            "game_state/meta/guardians.json.guardianPowerEvents.audit",
            scratchIssues,
            knownPoliticalProjects);
        ValidateCompletionSourcedRivalStrikeEventContract(
            item,
            "game_state/meta/guardians.json.guardianPowerEvents",
            guardianId,
            relatedGuardianId,
            sourceSurface,
            sourceId,
            reasonType!,
            auditNode,
            "game_state/meta/guardians.json.guardianPowerEvents.audit",
            scratchIssues,
            knownPoliticalProjects,
            journalSurface: false);
        ValidateUpdateSourcedRivalStrikeEventContract(
            item,
            "game_state/meta/guardians.json.guardianPowerEvents",
            sourceSurface,
            reasonType!,
            scratchIssues);
        if (scratchIssues.Any(issue => issue.Severity == IssueSeverity.Error))
            return false;

        var eventObject = TryParseJsonObject(item);
        if (eventObject == null)
            return false;

        authorizedEvent = eventObject;
        return true;
    }

    private static IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> ReadKnownPoliticalGuardianPowerEventProjectsFromTrackerContext(
        GuardianProjectTrackerPolicyContext trackerContext)
    {
        var result = new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (!trackerContext.HasCurrentAuthorityRoot)
            return result;

        var ambiguousKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MergeKnownPoliticalGuardianPowerEventProjectsForValidation(
            result,
            ambiguousKeys,
            trackerContext.CurrentAuthorityRoot.GetRawText());
        return result;
    }

    private static IReadOnlyDictionary<string, JsonObject> BuildAuthorizedGuardianCreateObjectsForAuthority(GuardianPolicyContext context)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var (guardianId, guardian) in context.AuthorizedSameTurnCreateGuardiansById)
        {
            if (guardian.ValueKind != JsonValueKind.Object)
                continue;

            var guardianObject = TryParseJsonObject(guardian);
            if (guardianObject != null)
                result[guardianId] = guardianObject;
        }

        return result;
    }

    private static bool HasUsableValidatedPreTurnGuardianBaseline(GuardianPolicyContext context)
        => context.HasUsableValidatedPreTurnGuardiansSnapshot && context.HasPreTurnRoot;

    private static GuardianBaselineFailureKind ResolveGuardianBaselineFailureKind(GuardianPolicyContext context)
    {
        if (HasUsableValidatedPreTurnGuardianBaseline(context))
            return GuardianBaselineFailureKind.None;

        return context.PreTurnGuardiansSnapshot.FileStatus switch
        {
            GuardianTrackedSnapshotFileStatus.MissingManifest => GuardianBaselineFailureKind.MissingManifest,
            GuardianTrackedSnapshotFileStatus.UnusableManifest => GuardianBaselineFailureKind.UnusableManifest,
            GuardianTrackedSnapshotFileStatus.MissingSnapshotFile => GuardianBaselineFailureKind.MissingSnapshotFile,
            GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile => GuardianBaselineFailureKind.InvalidSnapshotFile,
            _ => GuardianBaselineFailureKind.InvalidSnapshotFile
        };
    }

    private static bool TryGetGuardianBaselineFailureKind(
        GuardianPolicyContext context,
        out GuardianBaselineFailureKind failureKind)
    {
        failureKind = ResolveGuardianBaselineFailureKind(context);
        return failureKind != GuardianBaselineFailureKind.None;
    }

    private static string DescribeGuardianTrackedSnapshotFileStatus(GuardianTrackedSnapshotFileStatus status) => status switch
    {
        GuardianTrackedSnapshotFileStatus.MissingManifest => "validated pending turn snapshot manifest is missing",
        GuardianTrackedSnapshotFileStatus.UnusableManifest => "validated pending turn snapshot manifest is unreadable, modified, missing required snapshot data, or not current for the active request context",
        GuardianTrackedSnapshotFileStatus.MissingSnapshotFile => "validated pending turn guardians snapshot entry is missing from manifest.Files or manifest.SnapshotFileHashes",
        GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile => "validated pending turn guardians snapshot entry is unreadable, empty, or hash-invalid",
        GuardianTrackedSnapshotFileStatus.Usable => "validated pending turn guardians snapshot entry is usable",
        _ => status.ToString()
    };

    private static string DescribeGuardianTrackedSnapshotFileStatus(string relativePath, GuardianTrackedSnapshotFileStatus status) => status switch
    {
        GuardianTrackedSnapshotFileStatus.MissingManifest => "validated pending turn snapshot manifest is missing",
        GuardianTrackedSnapshotFileStatus.UnusableManifest => "validated pending turn snapshot manifest is unreadable, modified, missing required snapshot data, or not current for the active request context",
        GuardianTrackedSnapshotFileStatus.MissingSnapshotFile => $"validated pending turn snapshot entry for {relativePath} is missing from manifest.Files or manifest.SnapshotFileHashes",
        GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile => $"validated pending turn snapshot entry for {relativePath} is unreadable, empty, or hash-invalid",
        GuardianTrackedSnapshotFileStatus.Usable => $"validated pending turn snapshot entry for {relativePath} is usable",
        _ => status.ToString()
    };

    private async Task<GuardianTrackedSnapshotFileResolution> ResolveValidatedGuardianTrackedSnapshotFileAsync(string relativePath)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status == ValidatedPendingTurnSnapshotStatus.Missing)
            return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.MissingManifest, null, null);

        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
            return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.UnusableManifest, lookup.Manifest, null);

        if (lookup.Manifest.Files == null ||
            !lookup.Manifest.Files.TryGetValue(relativePath, out var snapshotPath) ||
            string.IsNullOrWhiteSpace(snapshotPath) ||
            lookup.Manifest.SnapshotFileHashes == null ||
            !lookup.Manifest.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
            string.IsNullOrWhiteSpace(expectedSnapshotHash))
        {
            return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.MissingSnapshotFile, lookup.Manifest, null);
        }

        var snapshotJson = await _fs.ReadFileAsync(snapshotPath);
        if (string.IsNullOrWhiteSpace(snapshotJson))
            return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile, lookup.Manifest, null);

        var actualSnapshotHash = ComputeSha256(snapshotJson);
        if (!string.Equals(actualSnapshotHash, expectedSnapshotHash, StringComparison.OrdinalIgnoreCase))
            return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile, lookup.Manifest, null);

        return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.Usable, lookup.Manifest, snapshotJson);
    }

    private GuardianTrackedSnapshotFileResolution ResolveValidatedGuardianTrackedSnapshotFileSync(string relativePath)
    {
        var lookup = LoadValidatedPendingTurnSnapshotLookupSync();
        if (lookup.Status == ValidatedPendingTurnSnapshotStatus.Missing)
            return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.MissingManifest, null, null);

        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
            return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.UnusableManifest, lookup.Manifest, null);

        if (lookup.Manifest.Files == null ||
            !lookup.Manifest.Files.TryGetValue(relativePath, out var snapshotPath) ||
            string.IsNullOrWhiteSpace(snapshotPath) ||
            lookup.Manifest.SnapshotFileHashes == null ||
            !lookup.Manifest.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
            string.IsNullOrWhiteSpace(expectedSnapshotHash))
        {
            return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.MissingSnapshotFile, lookup.Manifest, null);
        }

        var resolvedSnapshotPath = _fs.ResolvePath(snapshotPath);
        if (!File.Exists(resolvedSnapshotPath))
            return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile, lookup.Manifest, null);

        try
        {
            var snapshotJson = File.ReadAllText(resolvedSnapshotPath);
            if (string.IsNullOrWhiteSpace(snapshotJson))
                return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile, lookup.Manifest, null);

            var actualSnapshotHash = ComputeSha256(snapshotJson);
            if (!string.Equals(actualSnapshotHash, expectedSnapshotHash, StringComparison.OrdinalIgnoreCase))
                return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile, lookup.Manifest, null);

            return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.Usable, lookup.Manifest, snapshotJson);
        }
        catch
        {
            return new GuardianTrackedSnapshotFileResolution(lookup.Status, GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile, lookup.Manifest, null);
        }
    }

    private async Task<GuardianProjectTrackerPolicyContext> ResolveGuardianProjectTrackerPolicyContextAsync()
    {
        var currentJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var preTurnResolution = await ResolveValidatedGuardianTrackedSnapshotFileAsync(GuardianProjectState.TrackerPath);
        if (_guardianPolicyContextInProgress != null)
        {
            BuildGuardianAuthorityRoots(_guardianPolicyContextInProgress);
            return BuildGuardianProjectTrackerPolicyContext(currentJson, preTurnResolution, _guardianPolicyContextInProgress);
        }

        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        return BuildGuardianProjectTrackerPolicyContext(currentJson, preTurnResolution, guardianPolicyContext);
    }

    private GuardianProjectTrackerPolicyContext ResolveGuardianProjectTrackerPolicyContextSync()
    {
        var currentJson = ReadCurrentTrackedFileSync(GuardianProjectState.TrackerPath);
        var preTurnResolution = ResolveValidatedGuardianTrackedSnapshotFileSync(GuardianProjectState.TrackerPath);
        if (_guardianPolicyContextInProgress != null)
        {
            BuildGuardianAuthorityRoots(_guardianPolicyContextInProgress);
            return BuildGuardianProjectTrackerPolicyContext(currentJson, preTurnResolution, _guardianPolicyContextInProgress);
        }

        var guardianPolicyContext = ResolveGuardianPolicyContextSync();
        return BuildGuardianProjectTrackerPolicyContext(currentJson, preTurnResolution, guardianPolicyContext);
    }

    private GuardianProjectTrackerPolicyContext BuildGuardianProjectTrackerPolicyContext(
        string? currentTrackerJson,
        GuardianTrackedSnapshotFileResolution preTurnResolution,
        GuardianPolicyContext guardianPolicyContext)
    {
        var context = new GuardianProjectTrackerPolicyContext
        {
            PreTurnTrackerSnapshot = preTurnResolution
        };

        if (preTurnResolution.FileStatus == GuardianTrackedSnapshotFileStatus.Usable &&
            !string.IsNullOrWhiteSpace(preTurnResolution.SnapshotJson))
        {
            try
            {
                using var preTurnDoc = JsonDocument.Parse(preTurnResolution.SnapshotJson);
                context.PreTurnRoot = preTurnDoc.RootElement.Clone();
                context.HasPreTurnRoot = context.PreTurnRoot.ValueKind == JsonValueKind.Object;
            }
            catch
            {
                context.PreTurnTrackerSnapshot = preTurnResolution with
                {
                    FileStatus = GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile,
                    SnapshotJson = null
                };
                context.HasPreTurnRoot = false;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentTrackerJson))
        {
            try
            {
                using var currentDoc = JsonDocument.Parse(currentTrackerJson);
                if (currentDoc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    context.CurrentStateReadable = false;
                    context.CurrentStateFailureKind = GuardianCurrentStateFailureKind.UnreadableCurrentState;
                }
                else
                {
                    context.CurrentRoot = currentDoc.RootElement.Clone();
                    context.HasCurrentRoot = true;
                    context.CurrentStateFailureKind = GuardianCurrentStateFailureKind.None;
                }
            }
            catch
            {
                context.CurrentStateReadable = false;
                context.CurrentStateFailureKind = GuardianCurrentStateFailureKind.UnreadableCurrentState;
            }
        }

        if (!HasUsableValidatedPreTurnGuardianProjectTrackerBaseline(context))
            return context;

        var preTurnTrackerRoot = TryParseJsonObject(context.PreTurnRoot);
        var currentTrackerRoot = context.HasCurrentRoot ? TryParseJsonObject(context.CurrentRoot) : null;
        var preTurnGuardiansRoot = guardianPolicyContext.HasPreTurnAuthorityRoot
            ? TryParseJsonObject(guardianPolicyContext.PreTurnAuthorityRoot)
            : null;
        var currentGuardiansRoot = guardianPolicyContext.HasCurrentAuthorityRoot
            ? TryParseJsonObject(guardianPolicyContext.CurrentAuthorityRoot)
            : null;
        var currentTurn = ReadCurrentTurnNumberForProjectAuthority();
        var (currentIncarnation, currentRealm) = ReadCurrentSoulStateForProjectAuthority();

        if (preTurnTrackerRoot != null &&
            preTurnGuardiansRoot != null &&
            currentGuardiansRoot != null)
        {
            var projectedAuthorityRoot = CanonicalStateNormalizer.BuildGuardianProjectAuthorityRootForValidation(
                preTurnTrackerRoot,
                currentTrackerRoot,
                preTurnGuardiansRoot,
                currentGuardiansRoot,
                currentTurn,
                currentIncarnation,
                currentRealm);
            context.ProjectedAuthorityRoot = CloneJsonObjectToElement(projectedAuthorityRoot);
            context.HasProjectedAuthorityRoot = true;

            if (context.CurrentStateFailureKind == GuardianCurrentStateFailureKind.None &&
                currentTrackerRoot != null)
            {
                context.CurrentAuthorityRoot = CloneJsonObjectToElement(projectedAuthorityRoot.DeepClone().AsObject());
                context.HasCurrentAuthorityRoot = true;
            }
        }

        return context;
    }

    private static bool HasUsableValidatedPreTurnGuardianProjectTrackerBaseline(GuardianProjectTrackerPolicyContext context)
        => context.HasUsableValidatedPreTurnTrackerSnapshot && context.HasPreTurnRoot;

    private static GuardianBaselineFailureKind ResolveGuardianProjectTrackerBaselineFailureKind(GuardianProjectTrackerPolicyContext context)
    {
        if (HasUsableValidatedPreTurnGuardianProjectTrackerBaseline(context))
            return GuardianBaselineFailureKind.None;

        return context.PreTurnTrackerSnapshot.FileStatus switch
        {
            GuardianTrackedSnapshotFileStatus.MissingManifest => GuardianBaselineFailureKind.MissingManifest,
            GuardianTrackedSnapshotFileStatus.UnusableManifest => GuardianBaselineFailureKind.UnusableManifest,
            GuardianTrackedSnapshotFileStatus.MissingSnapshotFile => GuardianBaselineFailureKind.MissingSnapshotFile,
            GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile => GuardianBaselineFailureKind.InvalidSnapshotFile,
            _ => GuardianBaselineFailureKind.InvalidSnapshotFile
        };
    }

    private static bool TryGetGuardianProjectTrackerBaselineFailureKind(
        GuardianProjectTrackerPolicyContext context,
        out GuardianBaselineFailureKind failureKind)
    {
        failureKind = ResolveGuardianProjectTrackerBaselineFailureKind(context);
        return failureKind != GuardianBaselineFailureKind.None;
    }

    private bool TryResolveGuardianProjectTrackerValidationRootSync(
        out JsonElement trackerRoot,
        out GuardianProjectTrackerPolicyContext trackerContext)
    {
        trackerContext = ResolveGuardianProjectTrackerPolicyContextSync();
        trackerRoot = default;
        if (!HasUsableValidatedPreTurnGuardianProjectTrackerBaseline(trackerContext))
            return false;

        if (trackerContext.HasCurrentAuthorityRoot)
        {
            trackerRoot = trackerContext.CurrentAuthorityRoot;
            return true;
        }

        return false;
    }

    private static bool TryResolveGuardianProjectProjectedAuthorityRootSync(
        GuardianProjectTrackerPolicyContext trackerContext,
        out JsonElement trackerRoot)
    {
        trackerRoot = default;
        if (trackerContext.HasProjectedAuthorityRoot)
        {
            trackerRoot = trackerContext.ProjectedAuthorityRoot;
            return true;
        }

        return false;
    }

    private static JsonObject? TryParseJsonObject(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        return JsonNode.Parse(root.GetRawText()) as JsonObject;
    }

    private static JsonObject? TryParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadJsonNodeString(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static JsonElement CloneJsonObjectToElement(JsonObject root)
    {
        using var doc = JsonDocument.Parse(root.ToJsonString());
        return doc.RootElement.Clone();
    }

    private int ReadCurrentTurnNumberForProjectAuthority()
    {
        var repairContext = LoadPendingTurnRequestValidationContextSync(_fs.ResolvePath("game_state/control/validation_repair_request.json"));
        if (repairContext?.TurnNumber > 0)
            return repairContext.TurnNumber;

        var turnContext = LoadPendingTurnRequestValidationContextSync(_fs.ResolvePath("input/turn_request.json"));
        return turnContext?.TurnNumber ?? 0;
    }

    private (int CurrentIncarnation, string? CurrentRealm) ReadCurrentSoulStateForProjectAuthority()
    {
        var soulJson = ReadCurrentTrackedFileSync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return (0, null);

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return (0, null);

            var currentIncarnation = doc.RootElement.TryGetProperty("currentIncarnation", out var incarnationNode) &&
                                     incarnationNode.ValueKind == JsonValueKind.Number &&
                                     incarnationNode.TryGetInt32(out var parsedIncarnation)
                ? parsedIncarnation
                : 0;
            var currentRealm = GetFirstNonEmptyString(doc.RootElement, "currentRealm");
            return (currentIncarnation, currentRealm);
        }
        catch
        {
            return (0, null);
        }
    }

    internal async Task<GuardianPolicyContextDebugSnapshot> DebugResolveGuardianPolicyContextAsync()
    {
        var context = await ResolveGuardianPolicyContextAsync();
        return BuildGuardianPolicyContextDebugSnapshot(context);
    }

    internal async Task<GuardianProjectTrackerPolicyContextDebugSnapshot> DebugResolveGuardianProjectTrackerPolicyContextAsync()
    {
        var context = await ResolveGuardianProjectTrackerPolicyContextAsync();
        return new GuardianProjectTrackerPolicyContextDebugSnapshot(
            context.CurrentStateReadable,
            context.CurrentStateFailureKind.ToString(),
            context.HasPreTurnRoot,
            context.PreTurnTrackerSnapshot.ManifestStatus.ToString(),
            context.PreTurnTrackerSnapshot.FileStatus.ToString(),
            context.HasProjectedAuthorityRoot,
            context.HasCurrentAuthorityRoot,
            ReadGuardianProjectKeysFromAuthorityRoot(context.HasProjectedAuthorityRoot ? context.ProjectedAuthorityRoot : default),
            ReadGuardianProjectKeysFromAuthorityRoot(context.HasCurrentAuthorityRoot ? context.CurrentAuthorityRoot : default),
            context.HasProjectedAuthorityRoot ? context.ProjectedAuthorityRoot.GetRawText() : null,
            context.HasCurrentAuthorityRoot ? context.CurrentAuthorityRoot.GetRawText() : null);
    }

    private static IReadOnlyCollection<string> ReadGuardianProjectKeysFromAuthorityRoot(JsonElement trackerRoot)
    {
        if (trackerRoot.ValueKind != JsonValueKind.Object ||
            !trackerRoot.TryGetProperty("activeProjects", out var activeProjects) ||
            activeProjects.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return activeProjects.EnumerateArray()
            .Select(entry => GuardianProjectState.BuildKey(
                GetFirstNonEmptyString(entry, "guardianId"),
                entry.TryGetProperty("project", out var project) ? GetFirstNonEmptyString(project, "projectId") : null))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private static GuardianPolicyContextDebugSnapshot BuildGuardianPolicyContextDebugSnapshot(GuardianPolicyContext context)
    {
        var currentActiveGuardianId = context.HasCurrentActiveGuardian
            ? GetFirstNonEmptyString(context.CurrentActiveGuardian, "guardianId", "id")
            : null;
        return new GuardianPolicyContextDebugSnapshot(
            context.CurrentStateReadable,
            context.HasCurrentRoot,
            context.HasPreTurnRoot,
            context.PreTurnGuardiansSnapshot.ManifestStatus.ToString(),
            context.PreTurnGuardiansSnapshot.FileStatus.ToString(),
            context.CurrentGuardiansById.Count,
            context.PreTurnGuardiansById.Count,
            context.AuthorizedSameTurnCreateGuardiansById.Count,
            context.HasCurrentActiveGuardian,
            currentActiveGuardianId,
            context.BaselineGuardianIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
            context.AuthoritativeGuardianIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
            context.AuthorizedSameTurnCreateGuardianIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
            context.CurrentGuardianPowerEventAuthorityStatus.ToString(),
            context.CurrentGuardianPowerEventAuthorityFailureDescription,
            context.HasPreTurnAuthorityRoot,
            context.HasCurrentAuthorityRoot,
            context.HasPreTurnAuthorityRoot ? context.PreTurnAuthorityRoot.GetRawText() : null,
            context.HasCurrentAuthorityRoot ? context.CurrentAuthorityRoot.GetRawText() : null);
    }

    private static bool TryGetGuardianFromAuthorityRoot(JsonElement authorityRoot, string guardianId, out JsonElement guardian)
    {
        guardian = default;
        if (authorityRoot.ValueKind != JsonValueKind.Object ||
            !authorityRoot.TryGetProperty("guardians", out var guardians) ||
            guardians.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in guardians.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            if (string.Equals(GetFirstNonEmptyString(item, "guardianId", "id"), guardianId, StringComparison.OrdinalIgnoreCase))
            {
                guardian = item.Clone();
                return true;
            }
        }

        return false;
    }

    private static bool TryGetCurrentGuardian(GuardianPolicyContext context, string guardianId, out JsonElement guardian)
    {
        guardian = default;
        return context.HasCurrentAuthorityRoot &&
               TryGetGuardianFromAuthorityRoot(context.CurrentAuthorityRoot, guardianId, out guardian);
    }

    private static bool TryGetPreTurnGuardian(GuardianPolicyContext context, string guardianId, out JsonElement guardian)
    {
        guardian = default;
        return context.HasPreTurnAuthorityRoot &&
               TryGetGuardianFromAuthorityRoot(context.PreTurnAuthorityRoot, guardianId, out guardian);
    }

    private static bool TryGetCurrentMaterializedGuardian(GuardianPolicyContext context, string guardianId, out JsonElement guardian) =>
        context.CurrentGuardiansById.TryGetValue(guardianId, out guardian);

    private static bool TryGetCurrentAuthorityActiveGuardian(GuardianPolicyContext context, out JsonElement guardian)
    {
        guardian = default;
        if (context.HasCurrentAuthorityRoot &&
            context.CurrentAuthorityRoot.ValueKind == JsonValueKind.Object &&
            context.CurrentAuthorityRoot.TryGetProperty("activeGuardian", out var activeGuardian) &&
            activeGuardian.ValueKind == JsonValueKind.Object)
        {
            guardian = activeGuardian.Clone();
            return true;
        }

        return false;
    }

    private static string? TryReadGuardianAbodeIdFromPolicyContext(GuardianPolicyContext context, string guardianId, bool preTurn = false)
    {
        if (preTurn)
            return TryGetPreTurnGuardian(context, guardianId, out var preTurnGuardian)
                ? TryReadGuardianAbodeId(preTurnGuardian)
                : null;

        return TryGetCurrentGuardian(context, guardianId, out var currentGuardian)
            ? TryReadGuardianAbodeId(currentGuardian)
            : null;
    }

    private static int? TryReadGuardianCurrentMaterializedReputationFromPolicyContext(GuardianPolicyContext context, string guardianId)
        => TryGetCurrentMaterializedGuardian(context, guardianId, out var currentGuardian)
            ? TryReadGuardianCurrentReputation(currentGuardian)
            : null;

    private static int? TryReadGuardianCurrentReputationFromPolicyContext(GuardianPolicyContext context, string guardianId, bool preTurn = false)
    {
        if (preTurn)
            return TryGetPreTurnGuardian(context, guardianId, out var preTurnGuardian)
                ? TryReadGuardianCurrentReputation(preTurnGuardian)
                : null;

        return TryGetCurrentGuardian(context, guardianId, out var currentGuardian)
            ? TryReadGuardianCurrentReputation(currentGuardian)
            : null;
    }

    private static int? TryReadGuardianCurrentMaterializedAbodePowerFromPolicyContext(GuardianPolicyContext context, string guardianId)
        => TryGetCurrentMaterializedGuardian(context, guardianId, out var currentGuardian)
            ? AbodePowerRules.GetCurrentPower(currentGuardian)
            : null;

    private static int? TryReadGuardianCurrentAbodePowerFromPolicyContext(GuardianPolicyContext context, string guardianId, bool preTurn = false)
    {
        JsonElement guardian;
        if (preTurn)
        {
            if (!context.HasPreTurnAuthorityRoot ||
                !TryGetGuardianFromAuthorityRoot(context.PreTurnAuthorityRoot, guardianId, out guardian))
                return null;
        }
        else if (!TryGetCurrentGuardian(context, guardianId, out guardian))
        {
            return null;
        }

        return AbodePowerRules.GetCurrentPower(guardian);
    }

    private static Dictionary<string, string> BuildGuardianAbodeMap(GuardianPolicyContext context, bool preTurn = false)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var source = preTurn
            ? (context.HasPreTurnAuthorityRoot ? context.PreTurnAuthorityRoot : default)
            : (context.HasCurrentAuthorityRoot ? context.CurrentAuthorityRoot : default);

        if (source.ValueKind != JsonValueKind.Object ||
            !source.TryGetProperty("guardians", out var guardians) ||
            guardians.ValueKind != JsonValueKind.Array)
        {
            return map;
        }

        foreach (var guardian in guardians.EnumerateArray())
        {
            var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
            if (string.IsNullOrWhiteSpace(guardianId))
                continue;

            map[guardianId!] = TryReadGuardianAbodeId(guardian) ?? string.Empty;
        }

        return map;
    }
}
