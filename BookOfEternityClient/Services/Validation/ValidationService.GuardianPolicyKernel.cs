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
        InvalidSnapshotFile,
        InvalidAuthority
    }

    private sealed class GuardianPolicyContext
    {
        public bool CurrentStateReadable { get; set; } = true;
        public bool HasCurrentRoot { get; set; }
        public JsonElement CurrentRoot { get; set; }
        public bool HasPreTurnRoot { get; set; }
        public JsonElement PreTurnRoot { get; set; }
        public bool HasProofLocalCommandAuthorizationBaselineRoot { get; set; }
        public JsonElement ProofLocalCommandAuthorizationBaselineRoot { get; set; }
        public bool HasCurrentAuthorityRoot { get; set; }
        public JsonElement CurrentAuthorityRoot { get; set; }
        public bool HasStrictCurrentAuthorityRoot { get; set; }
        public JsonElement StrictCurrentAuthorityRoot { get; set; }
        public bool HasPreTurnAuthorityRoot { get; set; }
        public JsonElement PreTurnAuthorityRoot { get; set; }
        public bool HasCompatibilityPreTurnAuthorityRoot { get; set; }
        public JsonElement CompatibilityPreTurnAuthorityRoot { get; set; }
        public bool HasStrictPreTurnAuthorityRoot { get; set; }
        public bool HasGenericSharedStrictPreTurnAuthorityRoot { get; set; }
        public JsonElement GenericSharedStrictPreTurnAuthorityRoot { get; set; }
        public bool IsBuildingGuardianAuthorityRoots { get; set; }
        public bool IsBuildingStrictPreTurnAuthority { get; set; }
        public StrictPreTurnGuardianAuthorityStatus StrictPreTurnGuardianAuthorityStatus { get; set; } = StrictPreTurnGuardianAuthorityStatus.None;
        public string? StrictPreTurnGuardianAuthorityFailureDescription { get; set; }
        public GenericSharedStrictPreTurnGuardianAuthorityStatus GenericSharedStrictPreTurnGuardianAuthorityStatus { get; set; } = GenericSharedStrictPreTurnGuardianAuthorityStatus.None;
        public string? GenericSharedStrictPreTurnGuardianAuthorityFailureDescription { get; set; }
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
        public string? CurrentStateFailureDescription { get; set; }
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
        UnreadableCurrentState,
        SemanticallyInvalidCurrentState
    }

    private enum GuardianPowerEventAuthorityStatus
    {
        None,
        Resolved,
        MissingValidatedPreTurnJournalIdentity,
        InvalidValidatedPreTurnJournalIdentity,
        InvalidRawPowerEvents
    }

    private enum StrictPreTurnGuardianAuthorityStatus
    {
        None,
        Resolved,
        MissingValidatedSnapshotGuardians,
        InvalidValidatedSnapshotGuardians,
        MissingValidatedSnapshotTracker,
        InvalidValidatedSnapshotTracker,
        MissingValidatedSnapshotJournal,
        InvalidValidatedSnapshotJournal
    }

    private enum GenericSharedStrictPreTurnGuardianAuthorityStatus
    {
        None,
        Resolved,
        MissingValidatedSnapshotGuardians,
        InvalidValidatedSnapshotGuardians,
        MissingValidatedSnapshotTracker,
        InvalidValidatedSnapshotTracker,
        MissingValidatedSnapshotJournal,
        InvalidValidatedSnapshotJournal
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
        bool HasGenericSharedStrictPreTurnAuthorityRoot,
        string StrictPreTurnGuardianAuthorityStatus,
        string? StrictPreTurnGuardianAuthorityFailureDescription,
        string GenericSharedStrictPreTurnGuardianAuthorityStatus,
        string? GenericSharedStrictPreTurnGuardianAuthorityFailureDescription,
        string? PreTurnAuthorityRootJson,
        string? GenericSharedStrictPreTurnAuthorityRootJson,
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
                    HasNonEmptyGuardianPowerEventArray(eventsNode);
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
        if (context.IsBuildingGuardianAuthorityRoots)
            return;

        context.IsBuildingGuardianAuthorityRoots = true;
        try
        {
        if (!HasUsableValidatedPreTurnGuardianBaseline(context))
            return;

        var preTurnRoot = TryParseJsonObject(context.PreTurnRoot);
        if (preTurnRoot == null)
            return;

        var currentTurn = ReadCurrentTurnNumberForProjectAuthority();
        var rawPreTurnAuthorityRoot = CanonicalStateNormalizer.BuildGuardianAuthorityRootForValidation(
            preTurnRoot,
            currentRoot: null,
            authorizedCommands: null,
            authorizedCreateGuardiansById: null,
            authorizedPowerEvents: null,
            currentTurn);
        var compatibilityPreTurnGuardiansById = BuildGuardiansByIdFromAuthorityRoot(CloneJsonObjectToElement(rawPreTurnAuthorityRoot));
        var compatibilityPreTurnAuthorityRoot = rawPreTurnAuthorityRoot;

        if (!HasResolvedStrictPreTurnGuardianAuthority(context) &&
            TryMaterializeValidatedPreTurnTrackerGuardianEffects(
                context,
                CloneJsonObjectToElement(compatibilityPreTurnAuthorityRoot),
                compatibilityPreTurnGuardiansById,
                out var trackerMaterializedPreTurnAuthorityRoot,
                out var trackerMaterializedPreTurnGuardiansById))
        {
            compatibilityPreTurnAuthorityRoot = trackerMaterializedPreTurnAuthorityRoot;
            compatibilityPreTurnGuardiansById = trackerMaterializedPreTurnGuardiansById;
        }

        context.CompatibilityPreTurnAuthorityRoot = CloneJsonObjectToElement(compatibilityPreTurnAuthorityRoot);
        context.HasCompatibilityPreTurnAuthorityRoot = true;
        context.HasGenericSharedStrictPreTurnAuthorityRoot = false;
        context.GenericSharedStrictPreTurnAuthorityRoot = default;
        context.GenericSharedStrictPreTurnGuardianAuthorityStatus = GenericSharedStrictPreTurnGuardianAuthorityStatus.None;
        context.GenericSharedStrictPreTurnGuardianAuthorityFailureDescription = null;

        if (TryBuildGenericSharedStrictValidatedPreTurnGuardianAuthorityRoot(
                context,
                out var genericSharedStrictPreTurnAuthorityRoot,
                out var genericSharedStrictStatus,
                out var genericSharedStrictFailureDescription))
        {
            context.HasGenericSharedStrictPreTurnAuthorityRoot = true;
            context.GenericSharedStrictPreTurnAuthorityRoot = CloneJsonObjectToElement(genericSharedStrictPreTurnAuthorityRoot);
            context.GenericSharedStrictPreTurnGuardianAuthorityStatus = GenericSharedStrictPreTurnGuardianAuthorityStatus.Resolved;
            context.GenericSharedStrictPreTurnGuardianAuthorityFailureDescription = null;
        }
        else
        {
            context.HasGenericSharedStrictPreTurnAuthorityRoot = false;
            context.GenericSharedStrictPreTurnAuthorityRoot = default;
            context.GenericSharedStrictPreTurnGuardianAuthorityStatus = genericSharedStrictStatus;
            context.GenericSharedStrictPreTurnGuardianAuthorityFailureDescription = genericSharedStrictFailureDescription;
        }

        if (!context.IsBuildingStrictPreTurnAuthority)
        {
            context.IsBuildingStrictPreTurnAuthority = true;
            try
            {
                if (TryBuildStrictValidatedPreTurnGuardianAuthorityRoot(
                        context,
                        out var strictPreTurnAuthorityRoot,
                        out var strictPreTurnGuardiansById,
                        out var strictStatus,
                        out var strictFailureDescription))
                {
                    context.HasStrictPreTurnAuthorityRoot = true;
                    context.StrictPreTurnGuardianAuthorityStatus = StrictPreTurnGuardianAuthorityStatus.Resolved;
                    context.StrictPreTurnGuardianAuthorityFailureDescription = null;
                    context.PreTurnAuthorityRoot = CloneJsonObjectToElement(strictPreTurnAuthorityRoot);
                    context.HasPreTurnAuthorityRoot = true;
                    context.PreTurnGuardiansById.Clear();
                    foreach (var (guardianId, guardian) in strictPreTurnGuardiansById)
                    {
                        context.PreTurnGuardiansById[guardianId] = guardian.Clone();
                        context.AuthoritativeGuardianIds.Add(guardianId);
                        context.BaselineGuardianIds.Add(guardianId);
                    }
                }
                else
                {
                    context.HasStrictPreTurnAuthorityRoot = false;
                    context.HasPreTurnAuthorityRoot = false;
                    context.PreTurnAuthorityRoot = default;
                    context.PreTurnGuardiansById.Clear();
                    context.StrictPreTurnGuardianAuthorityStatus = strictStatus;
                    context.StrictPreTurnGuardianAuthorityFailureDescription = strictFailureDescription;
                }
            }
            finally
            {
                context.IsBuildingStrictPreTurnAuthority = false;
            }
        }

        context.HasCurrentAuthorityRoot = false;
        context.CurrentAuthorityRoot = default;
        context.HasStrictCurrentAuthorityRoot = false;
        context.StrictCurrentAuthorityRoot = default;
        if (!context.CurrentStateReadable || !context.HasCurrentRoot)
            return;

        var currentRoot = TryParseJsonObject(context.CurrentRoot);
        if (currentRoot == null)
            return;

        var authorizedCreatesById = BuildAuthorizedGuardianCreateObjectsForAuthority(context);
        if (HasResolvedGenericSharedStrictPreTurnGuardianAuthority(context))
        {
            var genericSharedCurrentBaselineRoot = TryParseJsonObject(context.GenericSharedStrictPreTurnAuthorityRoot);
            if (genericSharedCurrentBaselineRoot != null)
            {
                var currentAuthorityRoot = CanonicalStateNormalizer.BuildGuardianAuthorityRootForValidation(
                    genericSharedCurrentBaselineRoot.DeepClone().AsObject(),
                    currentRoot,
                    context.AuthorizedSameTurnGuardianCommands,
                    authorizedCreatesById,
                    context.AuthorizedCurrentGuardianPowerEvents,
                    currentTurn);
                context.CurrentAuthorityRoot = CloneJsonObjectToElement(currentAuthorityRoot);
                context.HasCurrentAuthorityRoot = true;
            }
        }

        if (!HasResolvedStrictPreTurnGuardianAuthority(context))
            return;

        var strictCurrentBaselineRoot = TryParseJsonObject(context.PreTurnAuthorityRoot);
        if (strictCurrentBaselineRoot == null)
            return;

        var strictCurrentAuthorityRoot = CanonicalStateNormalizer.BuildGuardianAuthorityRootForValidation(
            strictCurrentBaselineRoot.DeepClone().AsObject(),
            currentRoot,
            context.AuthorizedSameTurnGuardianCommands,
            authorizedCreatesById,
            context.AuthorizedCurrentGuardianPowerEvents,
            currentTurn);
        context.StrictCurrentAuthorityRoot = CloneJsonObjectToElement(strictCurrentAuthorityRoot);
        context.HasStrictCurrentAuthorityRoot = true;
        }
        finally
        {
            context.IsBuildingGuardianAuthorityRoots = false;
        }
    }

    private bool TryBuildGenericSharedStrictValidatedPreTurnGuardianAuthorityRoot(
        GuardianPolicyContext context,
        out JsonObject authorityRoot,
        out GenericSharedStrictPreTurnGuardianAuthorityStatus failureStatus,
        out string failureDescription)
    {
        authorityRoot = new JsonObject();
        failureStatus = GenericSharedStrictPreTurnGuardianAuthorityStatus.None;
        failureDescription = string.Empty;

        if (!context.HasUsableValidatedPreTurnGuardiansSnapshot ||
            string.IsNullOrWhiteSpace(context.PreTurnGuardiansSnapshot.SnapshotJson))
        {
            failureStatus = context.PreTurnGuardiansSnapshot.FileStatus switch
            {
                GuardianTrackedSnapshotFileStatus.MissingManifest => GenericSharedStrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotGuardians,
                GuardianTrackedSnapshotFileStatus.MissingSnapshotFile => GenericSharedStrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotGuardians,
                GuardianTrackedSnapshotFileStatus.UnusableManifest => GenericSharedStrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians,
                GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile => GenericSharedStrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians,
                _ => GenericSharedStrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians
            };
            failureDescription = DescribeGuardianTrackedSnapshotFileStatus("game_state/meta/guardians.json", context.PreTurnGuardiansSnapshot.FileStatus);
            return false;
        }

        List<JsonObject>? authorizedCommands = null;
        IReadOnlyDictionary<string, JsonObject>? authorizedCreateObjects = null;
        JsonElement guardianRoot;
        if (TryReadCanonicalGuardianSnapshotStateForProof(
                context.PreTurnGuardiansSnapshot.SnapshotJson,
                "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json",
                out guardianRoot,
                out _,
                out var commandAuthorizationResult,
                out _))
        {
            authorizedCommands = commandAuthorizationResult.AuthorizedCommands;
            authorizedCreateObjects = BuildGuardianCreateObjectsForSnapshotProof(commandAuthorizationResult);
        }
        else if (!TryReadGenericSharedStrictGuardianSnapshotState(
                     context.PreTurnGuardiansSnapshot.SnapshotJson,
                     "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json",
                     out guardianRoot,
                     out failureDescription))
        {
            failureStatus = GenericSharedStrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians;
            return false;
        }

        var guardianRootObject = TryParseJsonObject(guardianRoot);
        if (guardianRootObject == null)
        {
            failureStatus = GenericSharedStrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians;
            failureDescription = "generic shared strict pre-turn guardian authority root unreadable after validated snapshot canonicalization";
            return false;
        }

        var currentTurn = ReadCurrentTurnNumberForProjectAuthority();
        authorityRoot = CanonicalStateNormalizer.BuildGuardianAuthorityRootForValidation(
            guardianRootObject.DeepClone().AsObject(),
            guardianRootObject.DeepClone().AsObject(),
            authorizedCommands,
            authorizedCreateObjects,
            authorizedPowerEvents: null,
            currentTurn);

        failureStatus = GenericSharedStrictPreTurnGuardianAuthorityStatus.Resolved;
        failureDescription = string.Empty;
        return true;
    }

    private static bool TryReadGenericSharedStrictGuardianSnapshotState(
        string snapshotJson,
        string snapshotContext,
        out JsonElement guardianRoot,
        out string failureDescription)
    {
        guardianRoot = default;
        failureDescription = "validated shared pre-turn guardian snapshot is unreadable";
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            failureDescription = $"{snapshotContext} is empty";
            return false;
        }

        try
        {
            var rootNode = JsonNode.Parse(snapshotJson) as JsonObject;
            if (rootNode == null)
            {
                failureDescription = $"{snapshotContext} must be a JSON object";
                return false;
            }

            if (!rootNode.TryGetPropertyValue("guardians", out var guardiansNode) || guardiansNode is not JsonArray guardiansArray)
            {
                failureDescription = $"{snapshotContext}.guardians must be an array for shared strict guardian baseline";
                return false;
            }

            for (var index = 0; index < guardiansArray.Count; index++)
            {
                if (guardiansArray[index] is not JsonObject guardianObject)
                {
                    failureDescription = $"{snapshotContext}.guardians[{index}] must be an object for shared strict guardian baseline";
                    return false;
                }

                if (!TryValidateGenericSharedStrictGuardianSnapshotObject(
                        guardianObject,
                        $"{snapshotContext}.guardians[{index}]",
                        out failureDescription))
                {
                    return false;
                }
            }

            guardianRoot = CloneJsonObjectToElement(rootNode.DeepClone().AsObject());
            failureDescription = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            failureDescription = $"{snapshotContext} is unreadable: {ex.Message}";
            return false;
        }
    }

    private static bool TryValidateGenericSharedStrictGuardianSnapshotObject(
        JsonObject guardianObject,
        string guardianContext,
        out string failureDescription)
    {
        failureDescription = string.Empty;
        if (!TryReadRequiredGenericSharedStrictString(guardianObject, "guardianId", out _))
        {
            failureDescription = $"{guardianContext}.guardianId missing or invalid for shared strict guardian baseline";
            return false;
        }

        if (!TryReadRequiredGenericSharedStrictString(guardianObject, "canonicalName", out _))
        {
            failureDescription = $"{guardianContext}.canonicalName missing or invalid for shared strict guardian baseline";
            return false;
        }

        if (!guardianObject.TryGetPropertyValue("nameVariants", out var nameVariantsNode) || nameVariantsNode is not JsonObject nameVariants)
        {
            failureDescription = $"{guardianContext}.nameVariants missing or invalid for shared strict guardian baseline";
            return false;
        }

        if (!TryReadRequiredGenericSharedStrictString(nameVariants, "default", out _))
        {
            failureDescription = $"{guardianContext}.nameVariants.default missing or invalid for shared strict guardian baseline";
            return false;
        }

        if (!guardianObject.TryGetPropertyValue("manifestation", out var manifestationNode) || manifestationNode is not JsonObject manifestation)
        {
            failureDescription = $"{guardianContext}.manifestation missing or invalid for shared strict guardian baseline";
            return false;
        }

        foreach (var propertyName in new[] { "currentDisplayName", "formFlexibility", "currentPresentationStyle", "currentPronouns", "appearanceDescription" })
        {
            if (!TryReadRequiredGenericSharedStrictString(manifestation, propertyName, out _))
            {
                failureDescription = $"{guardianContext}.manifestation.{propertyName} missing or invalid for shared strict guardian baseline";
                return false;
            }
        }

        if (!guardianObject.TryGetPropertyValue("manifestationHistory", out var manifestationHistoryNode) || manifestationHistoryNode is not JsonArray)
        {
            failureDescription = $"{guardianContext}.manifestationHistory missing or invalid for shared strict guardian baseline";
            return false;
        }

        if (!guardianObject.TryGetPropertyValue("relationshipData", out var relationshipDataNode) || relationshipDataNode is not JsonObject relationshipData)
        {
            failureDescription = $"{guardianContext}.relationshipData missing or invalid for shared strict guardian baseline";
            return false;
        }

        if (!TryReadRequiredGenericSharedStrictNumber(relationshipData, "currentReputation"))
        {
            failureDescription = $"{guardianContext}.relationshipData.currentReputation missing or invalid for shared strict guardian baseline";
            return false;
        }

        if (!guardianObject.TryGetPropertyValue("abodePower", out var abodePowerNode) || abodePowerNode is not JsonObject abodePower)
        {
            failureDescription = $"{guardianContext}.abodePower missing or invalid for shared strict guardian baseline";
            return false;
        }

        if (!TryReadRequiredGenericSharedStrictNumber(abodePower, "currentPower") ||
            !TryReadRequiredGenericSharedStrictString(abodePower, "tier", out _) ||
            !TryReadRequiredGenericSharedStrictString(abodePower, "lastUpdatedAt", out _))
        {
            failureDescription = $"{guardianContext}.abodePower missing required shared strict fields";
            return false;
        }

        if (!abodePower.TryGetPropertyValue("history", out var abodePowerHistoryNode) || abodePowerHistoryNode is not JsonArray)
        {
            failureDescription = $"{guardianContext}.abodePower.history missing or invalid for shared strict guardian baseline";
            return false;
        }

        if (!guardianObject.TryGetPropertyValue("guardianRelationships", out var guardianRelationshipsNode) || guardianRelationshipsNode is not JsonArray guardianRelationships)
        {
            failureDescription = $"{guardianContext}.guardianRelationships missing or invalid for shared strict guardian baseline";
            return false;
        }

        for (var index = 0; index < guardianRelationships.Count; index++)
        {
            if (guardianRelationships[index] is not JsonObject relationshipObject)
            {
                failureDescription = $"{guardianContext}.guardianRelationships[{index}] must be an object for shared strict guardian baseline";
                return false;
            }

            if (!TryReadRequiredGenericSharedStrictString(relationshipObject, "targetGuardianId", out _) ||
                !TryReadRequiredGenericSharedStrictNumber(relationshipObject, "attitudeScore"))
            {
                failureDescription = $"{guardianContext}.guardianRelationships[{index}] missing required shared strict fields";
                return false;
            }
        }

        if (!guardianObject.TryGetPropertyValue("gachaSystem", out var gachaSystemNode) || gachaSystemNode is not JsonObject gachaSystem)
        {
            failureDescription = $"{guardianContext}.gachaSystem missing or invalid for shared strict guardian baseline";
            return false;
        }

        if (!TryReadRequiredGenericSharedStrictNumber(gachaSystem, "chargesPerReturn") ||
            !TryReadRequiredGenericSharedStrictNumber(gachaSystem, "chargesUsedThisReturn"))
        {
            failureDescription = $"{guardianContext}.gachaSystem missing required shared strict fields";
            return false;
        }

        if (!gachaSystem.TryGetPropertyValue("gachaHistory", out var gachaHistoryNode) || gachaHistoryNode is not JsonArray)
        {
            failureDescription = $"{guardianContext}.gachaSystem.gachaHistory missing or invalid for shared strict guardian baseline";
            return false;
        }

        return true;
    }

    private static bool TryReadRequiredGenericSharedStrictString(JsonObject obj, string propertyName, out string value)
    {
        value = string.Empty;
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue jsonValue)
            return false;

        var candidate = jsonValue.TryGetValue<string>(out var parsedValue) ? parsedValue : null;
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        value = candidate;
        return true;
    }

    private static bool TryReadRequiredGenericSharedStrictNumber(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue jsonValue)
            return false;

        return jsonValue.TryGetValue<int>(out _) ||
               jsonValue.TryGetValue<long>(out _) ||
               jsonValue.TryGetValue<double>(out _) ||
               jsonValue.TryGetValue<decimal>(out _);
    }

    private bool TryBuildStrictValidatedPreTurnGuardianAuthorityRoot(
        GuardianPolicyContext context,
        out JsonObject authorityRoot,
        out Dictionary<string, JsonElement> guardiansById,
        out StrictPreTurnGuardianAuthorityStatus failureStatus,
        out string failureDescription)
    {
        authorityRoot = new JsonObject();
        guardiansById = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        failureStatus = StrictPreTurnGuardianAuthorityStatus.None;
        failureDescription = string.Empty;
        if (!context.HasUsableValidatedPreTurnGuardiansSnapshot ||
            string.IsNullOrWhiteSpace(context.PreTurnGuardiansSnapshot.SnapshotJson))
        {
            failureStatus = context.PreTurnGuardiansSnapshot.FileStatus switch
            {
                GuardianTrackedSnapshotFileStatus.MissingManifest => StrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotGuardians,
                GuardianTrackedSnapshotFileStatus.MissingSnapshotFile => StrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotGuardians,
                GuardianTrackedSnapshotFileStatus.UnusableManifest => StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians,
                GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile => StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians,
                _ => StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians
            };
            failureDescription = DescribeGuardianTrackedSnapshotFileStatus("game_state/meta/guardians.json", context.PreTurnGuardiansSnapshot.FileStatus);
            return false;
        }

        var manifest = context.PreTurnGuardiansSnapshot.Manifest;
        var trackerJson = manifest?.Files != null &&
                          manifest.Files.ContainsKey(GuardianProjectState.TrackerPath)
            ? ReadValidatedPendingTurnSnapshotFileSync(manifest, GuardianProjectState.TrackerPath)
            : null;
        var journalJson = manifest?.Files != null &&
                          manifest.Files.ContainsKey(GuardianPowerEventState.JournalPath)
            ? ReadValidatedPendingTurnSnapshotFileSync(manifest, GuardianPowerEventState.JournalPath)
            : null;
        var soulStateJson = manifest?.Files != null &&
                            manifest.Files.ContainsKey("game_state/meta/soul_state.json")
            ? ReadValidatedPendingTurnSnapshotFileSync(manifest, "game_state/meta/soul_state.json")
            : null;
        var hasTrackerSnapshotEntry = manifest?.Files != null &&
                                      manifest.Files.ContainsKey(GuardianProjectState.TrackerPath);
        var hasJournalSnapshotEntry = manifest?.Files != null &&
                                      manifest.Files.ContainsKey(GuardianPowerEventState.JournalPath);
        if (!TryReadCanonicalGuardianSnapshotForProof(
                context.PreTurnGuardiansSnapshot.SnapshotJson,
                "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json",
                trackerJson,
                hasTrackerSnapshotEntry,
                $"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}",
                journalJson,
                $"game_state/control/pending_turn_snapshot/{GuardianPowerEventState.JournalPath}",
                soulStateJson,
                proofScope: null,
                authorityProofScope: CreateGuardianPowerEventAuthorityScopeForAllGuardians(),
                out var authorityElement,
                out guardiansById,
                out var failureKind,
                out var snapshotFailureDescription))
        {
            failureStatus = failureKind switch
            {
                GuardianSnapshotProofFailureKind.Journal when !hasJournalSnapshotEntry || string.IsNullOrWhiteSpace(journalJson)
                    => StrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotJournal,
                GuardianSnapshotProofFailureKind.Journal
                    => StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotJournal,
                GuardianSnapshotProofFailureKind.Tracker when !hasTrackerSnapshotEntry || string.IsNullOrWhiteSpace(trackerJson)
                    => StrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotTracker,
                GuardianSnapshotProofFailureKind.Tracker
                    => StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotTracker,
                _ => StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians
            };
            failureDescription = snapshotFailureDescription;
            guardiansById.Clear();
            return false;
        }

        var parsedAuthorityRoot = TryParseJsonObject(authorityElement);
        if (parsedAuthorityRoot == null)
        {
            guardiansById.Clear();
            failureStatus = StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians;
            failureDescription = "strict validated pre-turn guardian authority root unreadable after canonical snapshot proof";
            return false;
        }

        authorityRoot = parsedAuthorityRoot;
        failureStatus = StrictPreTurnGuardianAuthorityStatus.Resolved;
        return true;
    }

    private static bool SnapshotJsonHasNonEmptyGuardianPowerEvents(string? guardiansJson)
    {
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(guardiansJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object &&
                   doc.RootElement.TryGetProperty("guardianPowerEvents", out var powerEvents) &&
                   HasNonEmptyGuardianPowerEventArray(powerEvents);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool TryMaterializeValidatedPreTurnTrackerGuardianEffects(
        GuardianPolicyContext context,
        JsonElement guardianAuthorityRoot,
        IReadOnlyDictionary<string, JsonElement> guardiansById,
        out JsonObject materializedAuthorityRoot,
        out Dictionary<string, JsonElement> materializedGuardiansById)
    {
        materializedAuthorityRoot = new JsonObject();
        materializedGuardiansById = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var manifest = context.PreTurnGuardiansSnapshot.Manifest;
        if (manifest?.Files == null ||
            !manifest.Files.ContainsKey(GuardianProjectState.TrackerPath))
        {
            return false;
        }

        var trackerJson = ReadValidatedPendingTurnSnapshotFileSync(manifest, GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(trackerJson))
            return false;

        var soulStateJson = manifest.Files.ContainsKey("game_state/meta/soul_state.json")
            ? ReadValidatedPendingTurnSnapshotFileSync(manifest, "game_state/meta/soul_state.json")
            : null;

        if (!TryReadCanonicalGuardianProjectTrackerSnapshotForProof(
                trackerJson,
                $"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}",
                soulStateJson,
                guardianAuthorityRoot,
                guardiansById,
                out _,
                out var guardianAuthorityAfterTracker,
                out var guardiansByIdAfterTracker,
                out _,
                out _))
        {
            return false;
        }

        var parsedMaterializedRoot = TryParseJsonObject(guardianAuthorityAfterTracker);
        if (parsedMaterializedRoot == null)
            return false;

        materializedAuthorityRoot = parsedMaterializedRoot;
        materializedGuardiansById = guardiansByIdAfterTracker;
        return true;
    }

    private void BuildAuthorizedGuardianPowerEventsForAuthority(GuardianPolicyContext context)
    {
        context.AuthorizedCurrentGuardianPowerEvents.Clear();
        context.CurrentGuardianPowerEventAuthorityStatus = GuardianPowerEventAuthorityStatus.None;
        context.CurrentGuardianPowerEventAuthorityFailureDescription = null;
        if (!context.HasCurrentRoot ||
            !context.HasCurrentAuthorityRoot ||
            !context.CurrentRoot.TryGetProperty("guardianPowerEvents", out var powerEvents) ||
            powerEvents.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (powerEvents.ValueKind != JsonValueKind.Array)
        {
            context.CurrentGuardianPowerEventAuthorityStatus = GuardianPowerEventAuthorityStatus.InvalidRawPowerEvents;
            context.CurrentGuardianPowerEventAuthorityFailureDescription =
                "current guardianPowerEvents must be an array when the property is present";
            return;
        }

        if (!HasNonEmptyGuardianPowerEventArray(powerEvents))
        {
            return;
        }

        context.CurrentGuardianPowerEventAuthorityStatus = GuardianPowerEventAuthorityStatus.Resolved;
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects =
            new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (GuardianPowerEventArrayRequiresProjectTrackerAuthority(powerEvents) &&
            !TryReadKnownPoliticalGuardianPowerEventProjectsFromStrictTrackerAuthority(
                context,
                out knownPoliticalProjects,
                out var trackerAuthorityFailureDescription))
        {
            context.CurrentGuardianPowerEventAuthorityStatus = GuardianPowerEventAuthorityStatus.InvalidRawPowerEvents;
            context.CurrentGuardianPowerEventAuthorityFailureDescription = trackerAuthorityFailureDescription;
            return;
        }
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

    private bool TryReadKnownPoliticalGuardianPowerEventProjectsFromStrictTrackerAuthority(
        GuardianPolicyContext guardianPolicyContext,
        out IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        out string failureDescription)
    {
        knownPoliticalProjects = new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        failureDescription = string.Empty;
        if (!TryResolveStrictGuardianProjectTrackerAuthorityRoot(
                guardianPolicyContext,
                out var strictTrackerRoot,
                out failureDescription))
        return false;

        var result = new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        var ambiguousKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MergeKnownPoliticalGuardianPowerEventProjectsForValidation(
            result,
            ambiguousKeys,
            strictTrackerRoot.GetRawText());
        knownPoliticalProjects = result;
        return true;
    }

    private bool TryReadKnownPoliticalGuardianPowerEventProjectsFromStrictTrackerAuthority(
        out IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        out string failureDescription)
    {
        var guardianPolicyContext = _guardianPolicyContextInProgress ?? ResolveGuardianPolicyContextSync();
        return TryReadKnownPoliticalGuardianPowerEventProjectsFromStrictTrackerAuthority(
            guardianPolicyContext,
            out knownPoliticalProjects,
            out failureDescription);
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

    private static bool HasResolvedStrictPreTurnGuardianAuthority(GuardianPolicyContext context)
        => context.HasPreTurnAuthorityRoot &&
           context.HasStrictPreTurnAuthorityRoot &&
           context.StrictPreTurnGuardianAuthorityStatus == StrictPreTurnGuardianAuthorityStatus.Resolved;

    private static bool HasResolvedGenericSharedStrictPreTurnGuardianAuthority(GuardianPolicyContext context)
        => context.HasGenericSharedStrictPreTurnAuthorityRoot &&
           context.GenericSharedStrictPreTurnGuardianAuthorityStatus == GenericSharedStrictPreTurnGuardianAuthorityStatus.Resolved;

    private static bool TryGetGenericSharedStrictPreTurnGuardianAuthorityRoot(
        GuardianPolicyContext context,
        out JsonElement authorityRoot)
    {
        if (HasResolvedGenericSharedStrictPreTurnGuardianAuthority(context))
        {
            authorityRoot = context.GenericSharedStrictPreTurnAuthorityRoot;
            return true;
        }

        authorityRoot = default;
        return false;
    }

    private static bool TryGetSharedGuardianPreTurnBaselineRootForValidation(
        GuardianPolicyContext context,
        out JsonElement authorityRoot)
        => TryGetGenericSharedStrictPreTurnGuardianAuthorityRoot(context, out authorityRoot);

    private static bool TryGetGuardianPreTurnBaselineRootForCommandAuthorization(
        GuardianPolicyContext context,
        out JsonElement authorityRoot)
    {
        if (TryGetGenericSharedStrictPreTurnGuardianAuthorityRoot(context, out authorityRoot))
            return true;

        if (HasResolvedStrictPreTurnGuardianAuthority(context))
        {
            authorityRoot = context.PreTurnAuthorityRoot;
            return true;
        }

        if (context.HasProofLocalCommandAuthorizationBaselineRoot)
        {
            authorityRoot = context.ProofLocalCommandAuthorizationBaselineRoot;
            return true;
        }

        authorityRoot = default;
        return false;
    }

    private static GuardianBaselineFailureKind ResolveGuardianBaselineFailureKind(GuardianPolicyContext context)
    {
        if (!HasUsableValidatedPreTurnGuardianBaseline(context))
        {
            return context.PreTurnGuardiansSnapshot.FileStatus switch
            {
                GuardianTrackedSnapshotFileStatus.MissingManifest => GuardianBaselineFailureKind.MissingManifest,
                GuardianTrackedSnapshotFileStatus.UnusableManifest => GuardianBaselineFailureKind.UnusableManifest,
                GuardianTrackedSnapshotFileStatus.MissingSnapshotFile => GuardianBaselineFailureKind.MissingSnapshotFile,
                GuardianTrackedSnapshotFileStatus.InvalidSnapshotFile => GuardianBaselineFailureKind.InvalidSnapshotFile,
                _ => GuardianBaselineFailureKind.InvalidSnapshotFile
            };
        }

        if (!HasResolvedGenericSharedStrictPreTurnGuardianAuthority(context))
            return GuardianBaselineFailureKind.InvalidAuthority;

        return GuardianBaselineFailureKind.None;
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

    private static string DescribeStrictPreTurnGuardianAuthorityStatus(StrictPreTurnGuardianAuthorityStatus status) => status switch
    {
        StrictPreTurnGuardianAuthorityStatus.None => "validated pre-turn guardian authority has not been resolved",
        StrictPreTurnGuardianAuthorityStatus.Resolved => "validated pre-turn guardian authority is resolved",
        StrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotGuardians => "validated pre-turn guardian snapshot is missing",
        StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians => "validated pre-turn guardian snapshot is semantically invalid",
        StrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotTracker => "validated pre-turn tracker snapshot is missing",
        StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotTracker => "validated pre-turn tracker snapshot is semantically invalid",
        StrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotJournal => "validated pre-turn guardian power journal snapshot is missing",
        StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotJournal => "validated pre-turn guardian power journal snapshot is semantically invalid",
        _ => status.ToString()
    };

    private static string DescribeGenericSharedStrictPreTurnGuardianAuthorityStatus(GenericSharedStrictPreTurnGuardianAuthorityStatus status) => status switch
    {
        GenericSharedStrictPreTurnGuardianAuthorityStatus.None => "validated shared pre-turn guardian authority has not been resolved",
        GenericSharedStrictPreTurnGuardianAuthorityStatus.Resolved => "validated shared pre-turn guardian authority is resolved",
        GenericSharedStrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotGuardians => "validated shared pre-turn guardian snapshot is missing",
        GenericSharedStrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotGuardians => "validated shared pre-turn guardian snapshot is semantically invalid",
        GenericSharedStrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotTracker => "validated shared pre-turn tracker snapshot is missing",
        GenericSharedStrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotTracker => "validated shared pre-turn tracker snapshot is semantically invalid",
        GenericSharedStrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotJournal => "validated shared pre-turn guardian power journal snapshot is missing",
        GenericSharedStrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotJournal => "validated shared pre-turn guardian power journal snapshot is semantically invalid",
        _ => status.ToString()
    };

    private static string DescribeGuardianPreTurnBaselineFailure(GuardianPolicyContext context)
    {
        var baselineFailureKind = ResolveGuardianBaselineFailureKind(context);
        if (baselineFailureKind == GuardianBaselineFailureKind.InvalidAuthority &&
            !HasResolvedGenericSharedStrictPreTurnGuardianAuthority(context))
        {
            if (!string.IsNullOrWhiteSpace(context.GenericSharedStrictPreTurnGuardianAuthorityFailureDescription))
                return context.GenericSharedStrictPreTurnGuardianAuthorityFailureDescription!;

            return DescribeGenericSharedStrictPreTurnGuardianAuthorityStatus(context.GenericSharedStrictPreTurnGuardianAuthorityStatus);
        }

        if (HasResolvedStrictPreTurnGuardianAuthority(context))
            return DescribeStrictPreTurnGuardianAuthorityStatus(StrictPreTurnGuardianAuthorityStatus.Resolved);

        if (!string.IsNullOrWhiteSpace(context.StrictPreTurnGuardianAuthorityFailureDescription))
            return context.StrictPreTurnGuardianAuthorityFailureDescription!;

        if (baselineFailureKind == GuardianBaselineFailureKind.InvalidAuthority)
            return DescribeStrictPreTurnGuardianAuthorityStatus(context.StrictPreTurnGuardianAuthorityStatus);

        return DescribeGuardianTrackedSnapshotFileStatus(context.PreTurnGuardiansSnapshot.FileStatus);
    }

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
                    context.CurrentStateFailureDescription = null;
                }
            }
            catch
            {
                context.CurrentStateReadable = false;
                context.CurrentStateFailureKind = GuardianCurrentStateFailureKind.UnreadableCurrentState;
                context.CurrentStateFailureDescription = null;
            }
        }

        if (!HasUsableValidatedPreTurnGuardianProjectTrackerBaseline(context))
            return context;

        var preTurnTrackerRoot = TryParseJsonObject(context.PreTurnRoot);
        var currentTrackerRoot = context.HasCurrentRoot ? TryParseJsonObject(context.CurrentRoot) : null;
        JsonObject? preTurnTrackerAuthorityRoot = null;
        if (preTurnTrackerRoot != null &&
            TryGetGenericSharedStrictPreTurnGuardianAuthorityRoot(guardianPolicyContext, out var genericSharedPreTurnGuardiansRoot) &&
            TryBuildGuardianProjectTrackerPreTurnAuthorityRoot(
                preTurnTrackerRoot,
                genericSharedPreTurnGuardiansRoot,
                out var builtPreTurnTrackerAuthorityRoot,
                out _))
        {
            preTurnTrackerAuthorityRoot = builtPreTurnTrackerAuthorityRoot;
        }

        var preTurnGuardiansRoot = TryGetGenericSharedStrictPreTurnGuardianAuthorityRoot(guardianPolicyContext, out var genericSharedStrictPreTurnGuardiansRoot)
            ? TryParseJsonObject(genericSharedStrictPreTurnGuardiansRoot)
            : null;
        var currentGuardiansRoot = guardianPolicyContext.HasCurrentAuthorityRoot
            ? TryParseJsonObject(guardianPolicyContext.CurrentAuthorityRoot)
            : null;
        var currentTurn = ReadCurrentTurnNumberForProjectAuthority();

        if (preTurnTrackerAuthorityRoot != null &&
            preTurnGuardiansRoot != null &&
            currentGuardiansRoot != null)
        {
            if (!TryResolveCurrentSoulStateForProjectAuthority(
                    currentTrackerRoot,
                    preTurnTrackerAuthorityRoot,
                    context.PreTurnTrackerSnapshot.Manifest,
                    currentTurn,
                    out var currentIncarnation,
                    out var currentRealm,
                    out var soulStateFailureDescription))
            {
                context.CurrentStateFailureKind = GuardianCurrentStateFailureKind.SemanticallyInvalidCurrentState;
                context.CurrentStateFailureDescription = soulStateFailureDescription;
                return context;
            }

            if (context.CurrentStateFailureKind == GuardianCurrentStateFailureKind.None &&
                currentTrackerRoot != null &&
                !TryValidateGuardianProjectCurrentTrackerAuthorityInput(
                    context.CurrentRoot,
                    preTurnTrackerAuthorityRoot,
                    genericSharedStrictPreTurnGuardiansRoot,
                    guardianPolicyContext.CurrentAuthorityRoot,
                    out var currentTrackerSemanticFailureDescription))
            {
                context.CurrentStateFailureKind = GuardianCurrentStateFailureKind.SemanticallyInvalidCurrentState;
                context.CurrentStateFailureDescription = currentTrackerSemanticFailureDescription;
            }

            if (context.CurrentStateFailureKind == GuardianCurrentStateFailureKind.SemanticallyInvalidCurrentState)
                return context;

            var projectedAuthorityRoot = CanonicalStateNormalizer.BuildGuardianProjectAuthorityRootForValidation(
                preTurnTrackerAuthorityRoot,
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
        => TryResolveGuardianProjectTrackerValidationRootSync(
            out trackerRoot,
            out trackerContext,
            out _);

    private bool TryResolveGuardianProjectTrackerValidationRootSync(
        out JsonElement trackerRoot,
        out GuardianProjectTrackerPolicyContext trackerContext,
        out string failureDescription)
    {
        trackerContext = ResolveGuardianProjectTrackerPolicyContextSync();
        var guardianPolicyContext = _guardianPolicyContextInProgress ?? ResolveGuardianPolicyContextSync();
        trackerRoot = default;
        failureDescription = "current guardian project tracker authority unavailable";

        if (!HasResolvedGenericSharedStrictPreTurnGuardianAuthority(guardianPolicyContext))
        {
            failureDescription = DescribeGuardianPreTurnBaselineFailure(guardianPolicyContext);
            return false;
        }

        if (!guardianPolicyContext.HasCurrentAuthorityRoot)
        {
            failureDescription = DescribeCurrentGuardianAuthorityFailure(guardianPolicyContext);
            return false;
        }

        if (!TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRoot(
                guardianPolicyContext,
                trackerContext,
                out var preTurnTrackerAuthorityRoot,
                out failureDescription))
        {
            return false;
        }

        if (!trackerContext.HasCurrentRoot || trackerContext.CurrentStateFailureKind != GuardianCurrentStateFailureKind.None)
        {
            failureDescription = DescribeGuardianProjectTrackerAuthorityFailure(trackerContext);
            return false;
        }

        var preTurnTrackerRoot = TryParseJsonObject(preTurnTrackerAuthorityRoot);
        var currentTrackerRoot = TryParseJsonObject(trackerContext.CurrentRoot);
        var preTurnGuardiansRoot = TryParseJsonObject(guardianPolicyContext.GenericSharedStrictPreTurnAuthorityRoot);
        var currentGuardiansRoot = TryParseJsonObject(guardianPolicyContext.CurrentAuthorityRoot);
        if (preTurnTrackerRoot == null ||
            currentTrackerRoot == null ||
            preTurnGuardiansRoot == null ||
            currentGuardiansRoot == null)
        {
            failureDescription = "shared strict guardian-backed tracker authority root unreadable";
            return false;
        }

        var currentTurn = ReadCurrentTurnNumberForProjectAuthority();
        if (!TryResolveCurrentSoulStateForProjectAuthority(
                currentTrackerRoot,
                preTurnTrackerRoot,
                trackerContext.PreTurnTrackerSnapshot.Manifest,
                currentTurn,
                out var currentIncarnation,
                out var currentRealm,
                out failureDescription))
        {
            return false;
        }

        var projectedAuthorityRoot = CanonicalStateNormalizer.BuildGuardianProjectAuthorityRootForValidation(
            preTurnTrackerRoot,
            currentTrackerRoot,
            preTurnGuardiansRoot,
            currentGuardiansRoot,
            currentTurn,
            currentIncarnation,
            currentRealm);
        trackerRoot = CloneJsonObjectToElement(projectedAuthorityRoot);
        return true;
    }

    private bool TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRootSync(
        out JsonElement trackerRoot,
        out GuardianProjectTrackerPolicyContext trackerContext)
        => TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRootSync(
            out trackerRoot,
            out trackerContext,
            out _);

    private bool TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRootSync(
        out JsonElement trackerRoot,
        out GuardianProjectTrackerPolicyContext trackerContext,
        out string failureDescription)
    {
        trackerContext = ResolveGuardianProjectTrackerPolicyContextSync();
        var guardianPolicyContext = _guardianPolicyContextInProgress ?? ResolveGuardianPolicyContextSync();
        return TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRoot(
            guardianPolicyContext,
            trackerContext,
            out trackerRoot,
            out failureDescription);
    }

    private bool TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRoot(
        GuardianPolicyContext guardianPolicyContext,
        GuardianProjectTrackerPolicyContext trackerContext,
        out JsonElement trackerRoot,
        out string failureDescription)
    {
        trackerRoot = default;
        failureDescription = "validated shared pre-turn guardian project tracker authority unavailable";

        if (!TryGetGenericSharedStrictPreTurnGuardianAuthorityRoot(guardianPolicyContext, out var preTurnGuardiansRoot))
        {
            failureDescription = DescribeGuardianPreTurnBaselineFailure(guardianPolicyContext);
            return false;
        }

        if (!HasUsableValidatedPreTurnGuardianProjectTrackerBaseline(trackerContext))
        {
            failureDescription = DescribeGuardianTrackedSnapshotFileStatus(trackerContext.PreTurnTrackerSnapshot.FileStatus);
            return false;
        }

        var parsedTrackerRoot = TryParseJsonObject(trackerContext.PreTurnRoot);
        if (parsedTrackerRoot == null)
        {
            failureDescription = "validated pre-turn guardian project tracker baseline unreadable";
            return false;
        }

        if (!TryBuildGuardianProjectTrackerPreTurnAuthorityRoot(
                parsedTrackerRoot,
                preTurnGuardiansRoot,
                out var sharedAuthorityRoot,
                out failureDescription))
        {
            return false;
        }

        trackerRoot = CloneJsonObjectToElement(sharedAuthorityRoot);
        return true;
    }

    private bool TryBuildGuardianProjectTrackerPreTurnAuthorityRoot(
        JsonObject parsedTrackerRoot,
        JsonElement preTurnGuardiansRoot,
        out JsonObject authorityRoot,
        out string failureDescription)
    {
        authorityRoot = new JsonObject();
        failureDescription = "validated shared pre-turn guardian project tracker authority unavailable";

        var knownGuardianIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!MergeGuardianIdentityValidationStateFromStoredGuardians(
                preTurnGuardiansRoot,
                knownGuardianIds,
                new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase)))
        {
            if (!preTurnGuardiansRoot.TryGetProperty("guardians", out var guardians) ||
                guardians.ValueKind != JsonValueKind.Array)
            {
                failureDescription = "validated shared pre-turn guardian authority unreadable";
                return false;
            }
        }

        var parsedTrackerElement = CloneJsonObjectToElement(parsedTrackerRoot);
        var issues = new List<ValidationIssue>();
        if (parsedTrackerElement.TryGetProperty("activeProjects", out var activeProjects))
        {
            ValidateGuardianProjectIdentityAuthorityArray(
                activeProjects,
                "validated_pre_turn_tracker.activeProjects",
                issues,
                completed: false,
                knownGuardianIds);
        }

        if (parsedTrackerElement.TryGetProperty("completedProjects", out var completedProjects))
        {
            ValidateGuardianProjectIdentityAuthorityArray(
                completedProjects,
                "validated_pre_turn_tracker.completedProjects",
                issues,
                completed: true,
                knownGuardianIds);
        }

        if (parsedTrackerElement.TryGetProperty("temporaryProjectModifiers", out var temporaryProjectModifiers))
        {
            ValidateGuardianProjectModifierAuthorityArray(
                temporaryProjectModifiers,
                "validated_pre_turn_tracker.temporaryProjectModifiers",
                issues,
                knownGuardianIds);
        }

        ValidateGuardianProjectIdentityCollisions(parsedTrackerElement, "validated_pre_turn_tracker", issues);

        var firstError = issues.FirstOrDefault(issue => issue.Severity == IssueSeverity.Error);
        if (firstError != null)
        {
            failureDescription =
                $"validated pre-turn guardian project tracker baseline is semantically invalid: {firstError.Message}";
            return false;
        }

        authorityRoot = BuildGuardianProjectTrackerPreTurnAuthorityRoot(parsedTrackerRoot);
        return true;
    }

    private bool TryValidateGuardianProjectCurrentTrackerAuthorityInput(
        JsonElement currentTrackerRoot,
        JsonObject preTurnTrackerAuthorityRoot,
        JsonElement preTurnGuardiansRoot,
        JsonElement currentGuardiansRoot,
        out string failureDescription)
    {
        failureDescription = $"current {GuardianProjectState.TrackerPath} is semantically invalid";

        var issues = new List<ValidationIssue>();
        var knownGuardianIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var relationshipScores = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        MergeGuardianIdentityValidationStateFromStoredGuardians(preTurnGuardiansRoot, knownGuardianIds, relationshipScores);
        MergeGuardianIdentityValidationStateFromStoredGuardians(currentGuardiansRoot, knownGuardianIds, relationshipScores);

        var preTurnTrackerJson = preTurnTrackerAuthorityRoot.ToJsonString();
        var knownProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownCompletedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownProjectDetails = new Dictionary<string, GuardianProjectValidationSnapshot>(StringComparer.OrdinalIgnoreCase);
        var knownActiveProjectIdsByGuardian = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MergeKnownGuardianProjectKeysForValidation(knownProjects, preTurnTrackerJson);
        MergeKnownCompletedGuardianProjectKeysForValidation(knownCompletedProjects, preTurnTrackerJson);
        MergeKnownGuardianProjectsForValidation(knownProjectDetails, preTurnTrackerJson);
        MergeKnownActiveGuardianProjectIdsByGuardian(knownActiveProjectIdsByGuardian, preTurnTrackerJson);

        var startedThisTurn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var startedProjectDetails = new Dictionary<string, GuardianProjectValidationSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (currentTrackerRoot.TryGetProperty("startGuardianProjects", out var startCommands))
        {
            ValidateGuardianProjectStartCommands(
                startCommands,
                "current_tracker_authority.startGuardianProjects",
                issues,
                knownProjects,
                knownCompletedProjects,
                knownActiveProjectIdsByGuardian,
                startedThisTurn,
                startedProjectDetails,
                relationshipScores,
                knownGuardianIds);
        }

        if (currentTrackerRoot.TryGetProperty("guardianProjectUpdates", out var updateCommands))
        {
            ValidateGuardianProjectUpdateCommands(
                updateCommands,
                "current_tracker_authority.guardianProjectUpdates",
                issues,
                knownProjects,
                startedThisTurn,
                knownGuardianIds);
        }

        if (currentTrackerRoot.TryGetProperty("completeGuardianProjects", out var completionCommands))
        {
            ValidateGuardianProjectCompletionCommands(
                completionCommands,
                "current_tracker_authority.completeGuardianProjects",
                issues,
                knownProjects,
                knownProjectDetails,
                startedProjectDetails,
                startedThisTurn,
                relationshipScores,
                knownGuardianIds);
        }

        if (currentTrackerRoot.TryGetProperty("temporaryProjectModifiers", out var temporaryProjectModifiers))
        {
            ValidateGuardianProjectModifierAuthorityArray(
                temporaryProjectModifiers,
                "current_tracker_authority.temporaryProjectModifiers",
                issues,
                knownGuardianIds);
        }

        var firstError = issues.FirstOrDefault(issue => issue.Severity == IssueSeverity.Error);
        if (firstError == null)
            return true;

        failureDescription = $"current {GuardianProjectState.TrackerPath} is semantically invalid: {firstError.Message}";
        return false;
    }

    private void ValidateGuardianProjectModifierAuthorityArray(
        JsonElement value,
        string context,
        List<ValidationIssue> issues,
        HashSet<string> knownGuardianIds)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var seenModifierKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var guardianId = RequireString(item, itemContext, issues, "guardianId");
            var modifierId = RequireString(item, itemContext, issues, "modifierId");
            if (!string.IsNullOrWhiteSpace(guardianId) && !knownGuardianIds.Contains(guardianId))
                AddUnknownGuardianProjectIssue($"{itemContext}.guardianId", guardianId, issues);

            var modifierType = RequireString(item, itemContext, issues, "modifierType");
            if (!string.IsNullOrWhiteSpace(modifierType) &&
                !string.Equals(modifierType, "next_internal_project_starting_pressure", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.modifierType",
                    IssueSeverity.Error,
                    "temporaryProjectModifiers.modifierType использует неподдерживаемый тип",
                    code: "guardian_project_invalid_modifier_type",
                    section: "GuardianProjects",
                    expected: "next_internal_project_starting_pressure",
                    actual: modifierType,
                    repairHint: "В текущем этапе используй только next_internal_project_starting_pressure."));
            }

            ValidateIntegerField(item, itemContext, issues, "value");
            ValidateNonNegativeIntegerField(item, itemContext, issues, "remainingApplications", "GuardianProjects");

            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(modifierId))
                continue;

            var key = $"{guardianId}::{modifierId}";
            if (seenModifierKeys.Add(key))
                continue;

            issues.Add(new ValidationIssue(
                $"{itemContext}.modifierId",
                IssueSeverity.Error,
                "temporaryProjectModifiers не может содержать один и тот же guardianId + modifierId больше одного раза",
                code: "guardian_project_duplicate_modifier_key",
                section: "GuardianProjects",
                expected: "historically unique guardianId + modifierId key",
                actual: key,
                repairHint: "Оставь для temporaryProjectModifiers только одну canonical запись на каждую пару guardianId + modifierId."));
        }
    }

    private void ValidateGuardianProjectIdentityAuthorityArray(
        JsonElement value,
        string context,
        List<ValidationIssue> issues,
        bool completed,
        HashSet<string> knownGuardianIds)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var activeGuardians = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var entry in value.EnumerateArray())
        {
            var entryContext = $"{context}[{index++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            var guardianId = RequireString(entry, entryContext, issues, "guardianId");
            if (!string.IsNullOrWhiteSpace(guardianId) && !knownGuardianIds.Contains(guardianId))
                AddUnknownGuardianProjectIssue($"{entryContext}.guardianId", guardianId, issues);
            if (!completed && !string.IsNullOrWhiteSpace(guardianId) && !activeGuardians.Add(guardianId))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.guardianId",
                    IssueSeverity.Error,
                    "У одного Хранителя не может быть больше одного активного guardian project в v1",
                    code: "guardian_project_duplicate_active_guardian",
                    section: "GuardianProjects",
                    expected: "at most one active project per guardian",
                    actual: guardianId,
                    repairHint: "Оставляй у одного guardianId не более одной записи в activeProjects[]."));
            }

            if (!entry.TryGetProperty("project", out var project) ||
                !RequireObject(project, $"{entryContext}.project", issues))
            {
                continue;
            }

            RequireString(project, $"{entryContext}.project", issues, "projectId");
        }
    }

    private static JsonObject BuildGuardianProjectTrackerPreTurnAuthorityRoot(JsonObject trackerRoot)
    {
        var authorityRoot = new JsonObject
        {
            ["activeProjects"] = trackerRoot["activeProjects"] is JsonArray activeProjects
                ? activeProjects.DeepClone()
                : new JsonArray(),
            ["completedProjects"] = trackerRoot["completedProjects"] is JsonArray completedProjects
                ? completedProjects.DeepClone()
                : new JsonArray(),
            ["temporaryProjectModifiers"] = trackerRoot["temporaryProjectModifiers"] is JsonArray temporaryProjectModifiers
                ? temporaryProjectModifiers.DeepClone()
                : new JsonArray()
        };

        return authorityRoot;
    }

    private bool TryResolveStrictGuardianProjectTrackerAuthorityRootForProof(
        out JsonElement trackerRoot,
        out string failureDescription)
        => TryResolveStrictGuardianProjectTrackerAuthorityRootForValidation(
            out trackerRoot,
            out failureDescription);

    private bool TryResolveStrictGuardianProjectTrackerAuthorityRootForValidation(
        out JsonElement trackerRoot,
        out string failureDescription)
    {
        var guardianPolicyContext = _guardianPolicyContextInProgress ?? ResolveGuardianPolicyContextSync();
        var trackerContext = ResolveGuardianProjectTrackerPolicyContextSync();
        return TryResolveStrictGuardianProjectTrackerAuthorityRoot(
            guardianPolicyContext,
            trackerContext,
            out trackerRoot,
            out failureDescription);
    }

    private bool TryResolveStrictGuardianProjectTrackerAuthorityRoot(
        GuardianPolicyContext guardianPolicyContext,
        out JsonElement trackerRoot,
        out string failureDescription)
    {
        var trackerContext = ResolveGuardianProjectTrackerPolicyContextSync();
        return TryResolveStrictGuardianProjectTrackerAuthorityRoot(
            guardianPolicyContext,
            trackerContext,
            out trackerRoot,
            out failureDescription);
    }

    private bool TryResolveStrictGuardianProjectTrackerAuthorityRoot(
        GuardianPolicyContext guardianPolicyContext,
        GuardianProjectTrackerPolicyContext trackerContext,
        out JsonElement trackerRoot,
        out string failureDescription)
    {
        trackerRoot = default;
        failureDescription = "current guardian project tracker authority unavailable";

        if (!HasResolvedStrictPreTurnGuardianAuthority(guardianPolicyContext))
        {
            failureDescription = DescribeGuardianPreTurnBaselineFailure(guardianPolicyContext);
            return false;
        }

        if (!guardianPolicyContext.HasStrictCurrentAuthorityRoot)
        {
            failureDescription = "current strict guardian authority unavailable";
            return false;
        }

        if (!TryResolveStrictGuardianProjectTrackerPreTurnAuthorityRoot(
                guardianPolicyContext,
                trackerContext,
                out var preTurnTrackerAuthorityRoot,
                out failureDescription))
        {
            return false;
        }

        if (!trackerContext.HasCurrentRoot || trackerContext.CurrentStateFailureKind != GuardianCurrentStateFailureKind.None)
        {
            failureDescription = DescribeGuardianProjectTrackerAuthorityFailure(trackerContext);
            return false;
        }

        var preTurnTrackerRoot = TryParseJsonObject(preTurnTrackerAuthorityRoot);
        var currentTrackerRoot = TryParseJsonObject(trackerContext.CurrentRoot);
        var preTurnGuardiansRoot = TryParseJsonObject(guardianPolicyContext.PreTurnAuthorityRoot);
        var currentGuardiansRoot = TryParseJsonObject(guardianPolicyContext.StrictCurrentAuthorityRoot);
        if (preTurnTrackerRoot == null ||
            currentTrackerRoot == null ||
            preTurnGuardiansRoot == null ||
            currentGuardiansRoot == null)
        {
            failureDescription = "strict guardian-backed tracker authority root unreadable";
            return false;
        }

        var currentTurn = ReadCurrentTurnNumberForProjectAuthority();
        if (!TryResolveCurrentSoulStateForProjectAuthority(
                currentTrackerRoot,
                preTurnTrackerRoot,
                trackerContext.PreTurnTrackerSnapshot.Manifest,
                currentTurn,
                out var currentIncarnation,
                out var currentRealm,
                out failureDescription))
        {
            return false;
        }

        var projectedAuthorityRoot = CanonicalStateNormalizer.BuildGuardianProjectAuthorityRootForValidation(
            preTurnTrackerRoot,
            currentTrackerRoot,
            preTurnGuardiansRoot,
            currentGuardiansRoot,
            currentTurn,
            currentIncarnation,
            currentRealm);
        trackerRoot = CloneJsonObjectToElement(projectedAuthorityRoot);
        return true;
    }

    private bool TryResolveStrictGuardianProjectTrackerPreTurnAuthorityRoot(
        GuardianPolicyContext guardianPolicyContext,
        GuardianProjectTrackerPolicyContext trackerContext,
        out JsonElement trackerRoot,
        out string failureDescription)
    {
        trackerRoot = default;
        failureDescription = "validated strict pre-turn guardian project tracker authority unavailable";

        if (!HasResolvedStrictPreTurnGuardianAuthority(guardianPolicyContext))
        {
            failureDescription = DescribeGuardianPreTurnBaselineFailure(guardianPolicyContext);
            return false;
        }

        if (!HasUsableValidatedPreTurnGuardianProjectTrackerBaseline(trackerContext))
        {
            failureDescription = DescribeGuardianTrackedSnapshotFileStatus(trackerContext.PreTurnTrackerSnapshot.FileStatus);
            return false;
        }

        var parsedTrackerRoot = TryParseJsonObject(trackerContext.PreTurnRoot);
        if (parsedTrackerRoot == null)
        {
            failureDescription = "validated pre-turn guardian project tracker baseline unreadable";
            return false;
        }

        if (!TryBuildGuardianProjectTrackerPreTurnAuthorityRoot(
                parsedTrackerRoot,
                guardianPolicyContext.PreTurnAuthorityRoot,
                out var strictAuthorityRoot,
                out failureDescription))
        {
            return false;
        }

        trackerRoot = CloneJsonObjectToElement(strictAuthorityRoot);
        return true;
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

    private string? ReadValidatedPendingTurnSnapshotSoulStateJsonSync(ValidationPendingTurnSnapshotManifest? manifest)
    {
        return manifest?.Files != null &&
               manifest.Files.ContainsKey("game_state/meta/soul_state.json")
            ? ReadValidatedPendingTurnSnapshotFileSync(manifest, "game_state/meta/soul_state.json")
            : null;
    }

    private bool TryResolveCurrentSoulStateForProjectAuthority(
        JsonObject? currentTrackerRoot,
        JsonObject? preTurnTrackerRoot,
        ValidationPendingTurnSnapshotManifest? manifest,
        int currentTurn,
        out int currentIncarnation,
        out string? currentRealm,
        out string failureDescription)
    {
        var currentSoulJson = ReadCurrentTrackedFileSync("game_state/meta/soul_state.json");
        var preTurnSoulJson = ReadValidatedPendingTurnSnapshotSoulStateJsonSync(manifest);
        var currentLifeTransitionsJson = ReadCurrentTrackedFileSync("game_state/control/life_transitions.json");
        var soulContextRequirements =
            CanonicalStateNormalizer.ResolveRequiredCurrentGuardianProjectSoulContext(currentTrackerRoot, preTurnTrackerRoot);
        return CanonicalStateNormalizer.TryResolveGuardianProjectAuthoritySoulContext(
            currentSoulJson,
            preTurnSoulJson,
            currentLifeTransitionsJson,
            currentTurn,
            soulContextRequirements,
            out currentIncarnation,
            out currentRealm,
            out failureDescription);
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
            context.HasGenericSharedStrictPreTurnAuthorityRoot,
            context.StrictPreTurnGuardianAuthorityStatus.ToString(),
            context.StrictPreTurnGuardianAuthorityFailureDescription,
            context.GenericSharedStrictPreTurnGuardianAuthorityStatus.ToString(),
            context.GenericSharedStrictPreTurnGuardianAuthorityFailureDescription,
            context.HasPreTurnAuthorityRoot ? context.PreTurnAuthorityRoot.GetRawText() : null,
            HasResolvedGenericSharedStrictPreTurnGuardianAuthority(context)
                ? context.GenericSharedStrictPreTurnAuthorityRoot.GetRawText()
                : null,
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

    private static bool TryGetStrictCurrentGuardian(GuardianPolicyContext context, string guardianId, out JsonElement guardian)
    {
        guardian = default;
        return context.HasStrictCurrentAuthorityRoot &&
               TryGetGuardianFromAuthorityRoot(context.StrictCurrentAuthorityRoot, guardianId, out guardian);
    }

    private static bool TryEnsureCurrentGuardianAuthorityForPowerEventSensitiveOutcome(
        GuardianPolicyContext context,
        out string failureDescription)
    {
        failureDescription = "current guardian authority unavailable";

        if (!HasResolvedStrictPreTurnGuardianAuthority(context))
        {
            failureDescription = DescribeGuardianPreTurnBaselineFailure(context);
            return false;
        }

        if (context.CurrentGuardianPowerEventAuthorityStatus != GuardianPowerEventAuthorityStatus.None &&
            context.CurrentGuardianPowerEventAuthorityStatus != GuardianPowerEventAuthorityStatus.Resolved)
        {
            failureDescription = string.IsNullOrWhiteSpace(context.CurrentGuardianPowerEventAuthorityFailureDescription)
                ? $"current guardian power-event authority unavailable: {context.CurrentGuardianPowerEventAuthorityStatus}"
                : context.CurrentGuardianPowerEventAuthorityFailureDescription!;
            return false;
        }

        if (!context.HasStrictCurrentAuthorityRoot ||
            context.StrictCurrentAuthorityRoot.ValueKind != JsonValueKind.Object ||
            !context.StrictCurrentAuthorityRoot.TryGetProperty("guardians", out var guardians) ||
            guardians.ValueKind != JsonValueKind.Array)
        {
            failureDescription = context.HasStrictCurrentAuthorityRoot
                ? "current guardian authority unreadable or missing canonical guardians[]"
                : "current guardian authority unavailable";
            return false;
        }

        return true;
    }

    private static string DescribeCurrentGuardianAuthorityFailure(GuardianPolicyContext context)
    {
        if (!context.CurrentStateReadable || !context.HasCurrentRoot)
            return "current guardians.json unreadable or missing";

        if (!HasResolvedGenericSharedStrictPreTurnGuardianAuthority(context))
            return DescribeGuardianPreTurnBaselineFailure(context);

        if (!context.HasCurrentAuthorityRoot)
            return "current guardian authority unavailable";

        return "current guardian authority unreadable or missing canonical guardians[]";
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

        return TryGetStrictCurrentGuardian(context, guardianId, out var currentGuardian)
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
        else if (!TryGetStrictCurrentGuardian(context, guardianId, out guardian))
        {
            return null;
        }

        return AbodePowerRules.GetCurrentPower(guardian);
    }

    private static Dictionary<string, string> BuildGuardianAbodeMap(GuardianPolicyContext context, bool preTurn = false)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var source = preTurn
            ? (HasResolvedGenericSharedStrictPreTurnGuardianAuthority(context) ? context.GenericSharedStrictPreTurnAuthorityRoot : default)
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
