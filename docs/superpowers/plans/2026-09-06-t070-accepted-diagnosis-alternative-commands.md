# T070 Closed Diagnosis and Alternative Treatment Commands

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve, parse and recompose the approved closed diagnosis/alternative
accepted commands from real local factory authority, with deterministic tamper detection
and explicit rejection at production consumers that do not yet have a fresh-world adapter.

**Architecture:** Immutable closed draft variants retain the complete approved wire.
The existing complete route/path readers and writers supply standalone members, while
one shared full-member hash recipe is reused by factories and wire recomposition.
Wire integrity is distinct from private full-request seals and from fresh world authority.
Recognition and unsupported-consumer gates ship together, so no new family becomes
an empty successful opportunity/treatment batch.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** GitHub [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T059/T070; approved `data-model.md`
diagnosis/alternative authority and accepted-command sections, and `plan.md` T067
production-owned sealing boundary. Execute after independent approval of both tasks in
`2026-09-06-t070-treatment-member-shapes.md` and the diagnosis-cardinality alignment.
This is not the following GM response/repair schema or fresh publication task.

## Global Constraints

- Stay in `E:/Games/worktrees/boe-1536-wound-materialization` on the existing
  `1536-complete-wound-materialization` branch. Preserve `.serena/` and unrelated edits.
- No migration, compatibility parser, dual write, old-save fallback, hidden memory
  registry, fake wound, sentinel rank, guessed omitted event or invented authority.
- Mortal wounds and cures remain setting-specific GM constructions, not a predefined
  injury/cure catalog. Reuse the complete existing route/path constructor for every mode
  and registered nested operation, including complication effects and heal legacies.
- `opportunity_decision`, `accepted_transition/treat`, and the new accepted draft
  family remain distinct. Preserve every parsed row and its order within its family;
  do not discard, reorder or silently consume mixed families.
- Structural parse success is not seal verification. Recomposition unconditionally
  recomputes all available local seals, even for shape-valid placeholder fingerprints.
- A public recomputable checksum is not proof of fresh gameplay authority. The later
  canonical/signed-source adapter and `CanonicalStateNormalizer` remain mandatory.
- The closed wire is fixed. Do not add omitted wound, event, transition, history-only,
  owner, provider, private check or secret fields to make hashing convenient.
- A diagnosis success carries the nonempty complete ordered declared facts, possibly
  already known; a failure carries none. Alternative decline creates no accepted command.
- Full route/path hashes include display text, ordered arrays, explicit nulls and every
  mechanical field, excluding parser source-location metadata.
- Fast remains physically pure and bounded to five minutes. File/lease/distribution/
  repair/cold-replay tests belong in Integration; do not introduce cross-project test-file
  links or alter lanes/timeouts/runner configuration for this task.
- Use the bounded PowerShell 7 runner with exclusive C# lane ownership. No broad Fast,
  FullValidation, RegressionIntegration or PreMerge for this internal checkpoint.
- No push, PR, merge, issue closure, new branch, remote mutation or session cleanup.

### Task 1: Closed codec, complete projection seals and fail-closed consumers

**Files:**
- Create: `BookOfEternityClient/Services/MortalWoundTreatmentMemberFingerprint.cs`
  (only the existing complete member hash recipes).
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentPlanner.Diagnosis.cs`
  and `.Alternative.cs` (delegate only the selected member hash recipe).
- Create cohesive accepted-command partials, preferably
  `WoundResponseInputComposer.AcceptedTransitions.cs` and
  `WoundResponseInputComposer.AcceptedTransitionParsing.cs`, plus immutable models in
  `BookOfEternityClient/Services/WoundAcceptedTransitionCommand.cs`.
- Modify: `BookOfEternityClient/Services/WoundResponseInputComposer.CommandParsing.cs`
  (retention, exact dispatch, scene/coordinate/batch checks and recomposition routing).
- Modify only the new-family ingress gates in:
  `BookOfEternityClient/IO/StateDistributor.cs`,
  `BookOfEternityClient/Services/Validation/ValidationService.WoundMaterialization.cs`,
  `BookOfEternityClient/Services/MortalWoundTreatmentPersistence.cs`, and
  `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`.
- Create pure tests: `BookOfEternityClient.Tests/WoundAcceptedTransitionCommandTests.cs`.
- Create owning Integration partial:
  `BookOfEternityClient.IntegrationTests/WoundMaterializationLifecycleTests.AcceptedTransitions.cs`.
- Extend `BookOfEternityClient.Tests/WoundMaterializationSourceGuardTests.cs` only
  for otherwise inaccessible repair/cold-replay ingress gates, preserving all old guards.
- Create shared pure factory input builders for both test assemblies in
  `BookOfEternityClient.TestSupport/WoundAcceptedTransitionCommandTestData.cs` using
  its already-linked `WoundContractTestData`; no new test-project links or runner edits.
- Read unchanged frozen tests: `BookOfEternityClient.Tests/MortalWoundDiagnosisTests.cs`.

Do not edit full wound/member validators, reducers, accepted-world publication,
GameResponse/repair response schema, GM prompts/examples or parent-owned task records.
Report concrete dependencies or an unanticipated cohesive file split before expanding
ownership; do not leave unused parallel helpers or grow the existing persistence class
with the new codec. Its changes here are explicit routing gates only.

**Exact frozen composer:** one internal production method with this name and arity:

```csharp
internal static JsonObject ComposeAcceptedTransitionCommandRoot(
    WoundAcceptedTurnBinding binding,
    WoundTransitionRequest request,
    string finalSceneText);
```

Keep `ComposeMortalWoundTreatmentCommandRoot(binding, resolution, string)` separate.
The new composer supports only `diagnose` and `author_alternative_treatment`. Reject
invalid/tampered local requests with the existing argument/invalid-operation style;
call the production reducer to establish complete local legality before projecting.
Do not deserialize or mint concrete diagnosis/alternative evidence outside its factories.

**Closed root:** exactly `schemaVersion=1`, `sessionId`, `requestId`, `snapshotToken`,
`commands`; at most the existing `MaximumAcceptedCommandCount=32` rows.

**Closed row:** exactly `kind=accepted_transition`, `commandRef`, `transitionKind`,
`operationKey`, `authority`, `result`, `finalSceneText`.

| Variant | Exact authority fields | Exact result fields |
| --- | --- | --- |
| `diagnose` | `commandRef`, `operationKey`, `attemptId`, `woundId`, `diagnosisPathId`, `expectedBeforeFingerprint`, `pathFingerprint`, `requirementAuthorityFingerprint`, `checkResultFingerprint`, `authorityFingerprint` | `result`, `revealedFacts`, `resultFingerprint` |
| `author_alternative_treatment` | `authoringRequestRef`, `requestAuthorityFingerprint`, `operationKey`, `woundId`, `eventRef`, `addedRouteId`, `addedDiagnosisPathId`, `expectedBeforeFingerprint`, `expectedAfterFingerprint`, `routeFingerprint`, `diagnosisPathFingerprint`, `evidenceAuthorityFingerprint`, `requirementAuthorityFingerprint`, `authorityFingerprint` | `decision=author`, `route`, `diagnosisPath`, `resultFingerprint` |

Every listed field is required, including explicitly nullable alternative path fields.
Fingerprints use exact lowercase `sha256:` plus 64 lowercase hexadecimal digits.
`commandRef` and `authoringRequestRef` are safe opaque exact identifiers: no whitespace,
slash, backslash or colon; Unicode identifiers remain permitted. Do not apply that narrower
reference rule to eventRef or all game identifiers globally.
Alternative route visibility is only `public`, `known_to_player` or `hidden`, never
`gm_only`, matching the existing reducer. For hidden routes exactly one complete
non-GM-only diagnosis path must reveal the new
route; for visible routes the path/pair is null. Repeated ref/operation/route/path IDs
must match exactly. Other path facts may refer to wound members absent from this fragment;
full-wound reachability is intentionally not guessed here. Preserve the existing raw
`finalSceneText` string-or-null contract in parsing/recomposition, and preserve exact text
rather than trimming/normalizing it. The frozen composer signature remains one three-arg
method; do not add a reflection-breaking overload for null handling.

- [ ] **Step 1: Observe frozen RED and add focused boundary tests**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundDiagnosisTests.CommandParsing_|FullyQualifiedName~MortalWoundDiagnosisTests.CommandRecomposition_"
```

These are the 20 required rows, including real factory composition/recomposition; do
not weaken their exact field paths/codes, classify them as obsolete, or rewrite their
placeholder hashes. Parse-only fixtures intentionally contain unrelated well-shaped
fingerprints. They may parse structurally, but must fail recomposition if their available
seals do not agree. No special-casing hash values or test names.

Add direct tests around the real composer and parser/recomposer: both diagnosis outcomes;
visible/hidden authoring; one exact three-arg API; wrong evidence/kind; invalid local delta;
changed full request/images/seals; wrong session/request/snapshot/realm/turn/event/digest;
multiple legitimate accepted events; detached input/output; complete round trips; and
tampering with every available authority field, facts, scene and full member payload.
Cover structural required/unknown/wrong-type/null/recursive-duplicate fields, lowercase
hash syntax, fact order/bounds/token grammar/confusable duplicates, forbidden decline,
route/path ID agreement, hidden/visible pairing, route modes/nested draft rejection,
batch count, mixed-family rejection, retained order and conflicting scene/coordinates.
Use existing production hash writers only for genuine authority, never an independent
test-authored recipe that could pass while production hashing disagrees.

Start the new pure class with this executable round-trip regression (the shared pure
builder below supplies complete production factory inputs, not fabricated evidence):

```csharp
[Theory]
[InlineData("diagnose")]
[InlineData("diagnose_failure")]
[InlineData("author_alternative_treatment")]
[InlineData("author_alternative_treatment_hidden")]
public void Codec_RoundTripsActualFactoryAuthorityAndRejectsChangedScene(string variant)
{
    var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
    var root = WoundResponseInputComposer.ComposeAcceptedTransitionCommandRoot(
        binding, request, "The examination is complete.");
    var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(root));
    Assert.True(parsed.Success, string.Join("; ", parsed.Issues.Select(issue => issue.Code)));
    Assert.Single(parsed.AcceptedTransitionCommands);
    Assert.Empty(parsed.Commands);
    Assert.Empty(parsed.TreatmentCommands);
    var exact = WoundResponseInputComposer.RecomposeCommandRoot(
        binding, parsed, Array.Empty<WoundOpportunityDecisionReceipt>());
    Assert.True(exact.Success, string.Join("; ", exact.Issues.Select(issue => issue.Code)));
    Assert.True(JsonNode.DeepEquals(root, exact.CommandRoot));
    root["commands"]![0]!["finalSceneText"] = "A changed scene.";
    var changed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(root));
    Assert.True(changed.Success);
    var rejected = WoundResponseInputComposer.RecomposeCommandRoot(
        binding, changed, Array.Empty<WoundOpportunityDecisionReceipt>());
    Assert.False(rejected.Success);
    Assert.Contains(rejected.Issues, issue => issue.Code == "wound_command_recomposition_mismatch");
}
```

Complete pure builder in the declared TestSupport file (no xUnit dependency or file IO):

```csharp
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static class WoundAcceptedTransitionCommandTestData
{
    internal static (WoundAcceptedTurnBinding Binding, WoundTransitionRequest Request) Create(
        string variant, string suffix = "one")
    {
        var diagnosis = variant is "diagnose" or "diagnose_failure";
        var hidden = variant == "author_alternative_treatment_hidden";
        if (!diagnosis && variant is not ("author_alternative_treatment" or
            "author_alternative_treatment_hidden"))
            throw new ArgumentOutOfRangeException(nameof(variant));
        var kind = diagnosis ? "diagnose" : "author_alternative_treatment";
        var beforeRoot = WoundContractTestData.CreateActiveWound();
        var pathId = "diagnosis_" + suffix;
        if (diagnosis)
            beforeRoot["treatment"]!["diagnosisPaths"] = new JsonArray(Path(pathId, "clean_and_suture"));
        var afterRoot = beforeRoot.DeepClone().AsObject();
        var routeId = "alternative_" + suffix;
        if (!diagnosis)
        {
            var route = beforeRoot["treatment"]!["routes"]![0]!.DeepClone().AsObject();
            route["routeId"] = routeId;
            route["displayName"] = "New setting-specific treatment " + suffix;
            route["visibility"] = hidden ? "hidden" : "known_to_player";
            afterRoot["treatment"]!["routes"]!.AsArray().Add(route);
            if (hidden)
                afterRoot["treatment"]!["diagnosisPaths"]!.AsArray().Add(Path(pathId, routeId));
            else
                afterRoot["treatment"]!["knownRouteIds"]!.AsArray().Add(routeId);
        }
        var transitionId = "transition_command_" + suffix;
        afterRoot["lastTransition"] = new JsonObject
        {
            ["transitionId"] = transitionId,
            ["ordinal"] = beforeRoot["lastTransition"]!["ordinal"]!.GetValue<int>() + 1,
            ["turn"] = 43,
            ["kind"] = kind
        };
        var before = Parse(beforeRoot);
        var after = Parse(afterRoot);
        const string eventRef = "turn_43:accepted_command";
        var events = new[] { new WoundAcceptedEventAuthority(
            eventRef, "treatment_check", "event_authority_command", Seal('a')) };
        var binding = new WoundAcceptedTurnBinding("session_command", "request_command",
            "snapshot_command", "mortal_world", 43, events,
            WoundAcceptedEventSetFingerprint.Compute(events));
        var request = diagnosis
            ? MortalWoundTreatmentPlanner.CreateDiagnosisTransition(transitionId,
                "command_" + suffix, "operation_" + suffix, "attempt_" + suffix,
                eventRef, 43, before, after, pathId,
                variant == "diagnose_failure" ? "failure" : "success", Seal('6'), Seal('7'))
            : MortalWoundTreatmentPlanner.CreateAlternativeTreatmentTransition(transitionId,
                "authoring_" + suffix, Seal('1'), "operation_" + suffix,
                eventRef, 43, before, after, routeId, hidden ? pathId : null, Seal('6'), Seal('7'));
        return (binding, request);
    }

    private static JsonObject Path(string id, string routeId) => new()
    {
        ["diagnosisPathId"] = id,
        ["displayName"] = "Examine the wound",
        ["visibility"] = "known_to_player",
        ["requiresKnownFacts"] = new JsonArray("route:clean_and_suture"),
        ["requirements"] = new JsonArray(),
        ["check"] = new JsonObject(),
        ["reveals"] = new JsonArray("route:" + routeId),
        ["failurePolicy"] = "no_reveal"
    };

    private static WoundMaterializationEnvelope Parse(JsonObject root)
    {
        var parsed = WoundMaterializationContract.Parse(root.ToJsonString(), "command_test.wound");
        return parsed.IsValid && parsed.Wound is not null ? parsed.Wound :
            throw new InvalidOperationException(string.Join("; ", parsed.Issues.Select(issue => issue.Code)));
    }

    private static string Seal(char digit) => "sha256:" + new string(digit, 64);
}
```

Add Integration RED for real validation/distribution ingress before implementing its
new-family gates; both variants must be retained by parse/recompose yet rejected as
unsupported for fresh publication, with no command/accepted plan/state writes caused
by the attempted ingestion. Reuse existing lease-aware lifecycle helpers and compare
the relevant before/after bytes. Do not populate arbitrary placeholder roots and call
that production accepted-command evidence.

- [ ] **Step 2: Share complete member hashes and compose from legal factory requests**

Extract only the existing domain/version/canonical-member recipe. Factory methods still
select one member from complete canonical wound JSON, retaining their null-on-missing
selection semantics. Use the same helper with standalone canonical writers at recomposition:

- Route domain: `book_of_eternity.mortal_wound.alternative_treatment_route`, version `1`.
- Path domain: `book_of_eternity.mortal_wound.diagnosis_path`, version `1`.
- Hash inputs remain domain, version, then complete canonical member JSON or null.

The complete shared helper is:

```csharp
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class MortalWoundTreatmentMemberFingerprint
{
    internal static string ComputeRoute(JsonNode? route) => Compute(
        "book_of_eternity.mortal_wound.alternative_treatment_route", route);

    internal static string ComputeDiagnosisPath(JsonNode? path) => Compute(
        "book_of_eternity.mortal_wound.diagnosis_path", path);

    private static string Compute(string domain, JsonNode? member) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            domain, "1", member is null ? null : WoundAcceptedTurnFingerprintWriter.CanonicalJson(member)
        });
}
```

Replace only the final hash-expression in the two current factory member selectors with
`MortalWoundTreatmentMemberFingerprint.ComputeRoute(route)` and
`MortalWoundTreatmentMemberFingerprint.ComputeDiagnosisPath(path)`, respectively.

Do not substitute the existing mechanics-only treatment-route fingerprint. Pin equality
with real factory evidence, including display/array/null mutation cases and omitted
source paths. The canonical member writers supplied by the preceding task emit one
complete object without fabricating an enclosing wound.

Composition validates the actual accepted binding: exact root identities, Mortal realm,
positive turn matching the request, complete accepted event shapes and recomputed digest,
and exactly one accepted event for the request's exact eventRef. Reuse the existing
binding validation primitives where appropriate, then apply these variant-specific checks.
Do not require that the accepted set has only one event, infer an omitted diagnosis event,
or treat external requirement/check/evidence fingerprint strings as fresh world proof.

After successful local reduction, project precisely the selected evidence and typed
history result. A new deterministic wire-projection seal must be shared by compose and
recompose, separate from the unreconstructible private full-request seal. Use one named
production domain/version recipe binding all represented authority fields except itself,
the verified typed result fingerprint, outer kind/ref/transition/operation/final scene,
and available binding session/request/snapshot/realm/turn/accepted-events fingerprint.
Use this complete shared wire-seal function in the new composer partial; both compose
and recompose call it after validating the binding/result. Do not copy this recipe into
the tests or a second production class:

```csharp
private static string ComputeAcceptedTransitionWireFingerprint(
    WoundAcceptedTurnBinding binding, string commandRef, string transitionKind,
    string operationKey, JsonObject authority, string resultFingerprint, string? finalSceneText)
{
    var unsealedAuthority = authority.DeepClone().AsObject();
    unsealedAuthority.Remove("authorityFingerprint");
    return WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
    {
        "book_of_eternity.wound.accepted_transition_wire", "1",
        binding.SessionId, binding.RequestId, binding.SnapshotToken, binding.Realm,
        binding.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
        binding.AcceptedEventsFingerprint, "accepted_transition", commandRef,
        transitionKind, operationKey, finalSceneText, resultFingerprint,
        WoundAcceptedTurnFingerprintWriter.CanonicalJson(unsealedAuthority)
    });
}
```

- [ ] **Step 3: Parse immutable complete drafts and recompute every available seal**

Use a closed immutable draft family for the two variants, distinct from opportunities
and treatments. Retain root coordinates, complete typed authority/result and exact scene.
Name the retained collection `WoundResponseCommandParsingResult.AcceptedTransitionCommands`
so composition and all ingress gates refer to the same explicit family.

Use these exact immutable draft interfaces; admit only the two concrete variants in
typed serialization/recomposition and reject any other runtime subtype. Root session/request/snapshot remain in the
parsing result's detached root and are matched explicitly before recomposition:

```csharp
namespace BookOfEternityClient.Services;

internal abstract record WoundAcceptedTransitionCommandDraft(
    string CommandRef, string OperationKey, string? FinalSceneText)
{
    internal abstract string TransitionKind { get; }
}

internal sealed record WoundDiagnosisCommandAuthority(
    string CommandRef, string OperationKey, string AttemptId, string WoundId,
    string DiagnosisPathId, string ExpectedBeforeFingerprint, string PathFingerprint,
    string RequirementAuthorityFingerprint, string CheckResultFingerprint,
    string AuthorityFingerprint);

internal sealed record WoundAlternativeTreatmentCommandAuthority(
    string AuthoringRequestRef, string RequestAuthorityFingerprint, string OperationKey,
    string WoundId, string EventRef, string AddedRouteId, string? AddedDiagnosisPathId,
    string ExpectedBeforeFingerprint, string ExpectedAfterFingerprint,
    string RouteFingerprint, string? DiagnosisPathFingerprint,
    string EvidenceAuthorityFingerprint, string RequirementAuthorityFingerprint,
    string AuthorityFingerprint);

internal sealed record WoundDiagnosisCommandDraft(
    string CommandRef, string OperationKey, string? FinalSceneText,
    WoundDiagnosisCommandAuthority Authority, WoundDiagnosisTransitionResult Result)
    : WoundAcceptedTransitionCommandDraft(CommandRef, OperationKey, FinalSceneText)
{
    internal override string TransitionKind => "diagnose";
}

internal sealed record WoundAlternativeTreatmentCommandDraft(
    string CommandRef, string OperationKey, string? FinalSceneText,
    WoundAlternativeTreatmentCommandAuthority Authority,
    MortalWoundTreatmentRouteDefinition Route, MortalWoundDiagnosisPathDefinition? DiagnosisPath,
    WoundAlternativeTreatmentTransitionResult Result)
    : WoundAcceptedTransitionCommandDraft(CommandRef, OperationKey, FinalSceneText)
{
    internal override string TransitionKind => "author_alternative_treatment";
}
```

The existing typed member/result models contain immutable arrays, copied JSON elements
and detached canonical projections. Keep them typed; do not replace them with exposed
mutable JsonObjects alongside another authoritative copy. Result objects constructed
by structural parsing carry unverified fingerprints until recomposition validates them.

Add the following storage/overload to the existing parsing-result class, preserving
the old overloads. No externally mutable list is retained:

```csharp
private readonly WoundAcceptedTransitionCommandDraft[] _acceptedTransitionCommands =
    Array.Empty<WoundAcceptedTransitionCommandDraft>();

internal WoundResponseCommandParsingResult(
    JsonObject? commandRoot, IReadOnlyList<WoundResponseCommandDraft> commands,
    IReadOnlyList<MortalWoundTreatmentCommandDraft> treatmentCommands,
    IReadOnlyList<WoundAcceptedTransitionCommandDraft> acceptedTransitionCommands,
    IReadOnlyList<ValidationIssue> issues)
    : this(commandRoot, commands, treatmentCommands, issues)
{
    _acceptedTransitionCommands = acceptedTransitionCommands.ToArray();
}

internal IReadOnlyList<WoundAcceptedTransitionCommandDraft> AcceptedTransitionCommands =>
    Array.AsReadOnly(_acceptedTransitionCommands);
```

Parsed-root accessors remain detached; disposal/mutation of original input or an exported
root cannot alter stored authority. Reuse existing same-partial strict field readers and
diagnostic construction; reject recursive duplicate properties on original JSON before
any JsonNode conversion. Use the reviewed standalone route/path parsers and writers;
no opaque/permissive fragment schema, fake wound or bypass of nested validation.

Extend `WoundResponseCommandParsingResult` with the retained new family while preserving
old constructors/callers. Dispatch it explicitly before the fallback opportunity parser.
Count it in scene consistency and the total bounded batch; require exact/confusable
unique commandRef and operationKey in the new family, plus diagnosis attempt uniqueness.
Do not invent event uniqueness across distinct commands or lose interleaved diagnosis/
alternative order inside this new family. Preserve rejection of mixed opportunity/treat/
new-family batches at recomposition; do not reorder separate arrays into a successful mix.

Recomposition first matches root/binding, validates binding shape/digest and Mortal realm,
then serializes typed drafts. Reconstruct the closed history result internally:
diagnosis kind/path from authority plus outcome/facts; alternative kind/request/IDs plus
independently recomputed member hashes. Call the existing
`WoundHistoryState.ComputeTransitionResultFingerprint`; do not add history-only fields
to the wire result. Check carried result and complete member fingerprints, then compute
and compare the wire authority seal. Finally require deep equality with the strictly
parsed root. No clone-only success, trusted carried hash, secret cache or guessed omitted
diagnosis event/transition/after-image. Mismatch uses `wound_command_recomposition_mismatch`
unless strict parsing already produced a more precise existing command diagnostic.

- [ ] **Step 4: Fail closed at every currently unsupported production consumer**

Implement recognition and these gates in the same code commit:

- `StateDistributor.ResolveAcceptedWoundCommand`:
  reject the new parsed family before the treatment/opportunity distribution branches,
  using an explicit `wound_command_transition_adapter_unavailable` diagnostic in the
  thrown invalid-data error. No mutation or successful empty distribution.
- `ValidationService.ParseAcceptedTurnWoundCommands`: after strict parse succeeds,
  report `wound_command_transition_adapter_unavailable` and return null for the new
  family before rebuilding an opportunity/treatment accepted authority or plan.
- `MortalWoundTreatmentPersistedRequestCatalog.Parse` command ingestion:
  do not silently drop a structurally valid new or mixed family while extracting
  treatment requests. Reject it explicitly; opportunity-only remains its existing
  intentional treatment-catalog behavior. History diagnosis rows are not treatment
  requests and retain the existing history filtering semantics.
- `WoundRepairPacketBuilder.ComposePendingRoot(binding, packets, parsedCommand)`:
  reject new-family capture explicitly before creating a pending root, until the
  following kind-specific capture adapter exists. Do not accept a recomposed root but
  serialize only an empty `submittedTreatmentRequests` collection.
- `GameEngine` cold treatment replay: reject mixed/new parsed families before its
  quarantine/finalization path, retaining the existing not-a-treatment return shape.
- `ReadStrictWoundRepairCommandAsync`: do not pass a new-family root to the current
  opportunity-only capture/retry consumers. Return the existing no-capture shape;
  retain exact-resubmission rejection. Preserve all existing source-guard anchors.

These are temporary internal unsupported-adapter diagnostics, not removal of the planned
capability. The following fresh-authority/capture tasks must replace these gates with
complete kind-specific handling before T070/#1536 is complete. Do not publish a command
merely because its checksums are internally consistent.

The distribution and pending-root gates use the same direct pattern with their local
parsed value (the latter uses `InvalidOperationException`):

```csharp
if (parsed.AcceptedTransitionCommands.Count != 0)
    throw new InvalidDataException(
        "wound_command_transition_adapter_unavailable: diagnosis/alternative commands require their fresh accepted-world adapter.");
```

The validation parse gate has its existing issue/result protocol:

```csharp
if (parsed.AcceptedTransitionCommands.Count != 0)
{
    issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
        "wound_command_transition_adapter_unavailable",
        "one independently validated fresh diagnosis/alternative transition adapter",
        "the command is structurally valid but its fresh-world adapter is unavailable"));
    return null;
}
```

The catalog's branch belongs immediately after strict-parse failure, before existing
treatment/opportunity branching; it preserves all earlier invalid-history precedence:

```csharp
else if (parsed.AcceptedTransitionCommands.Count != 0)
{
    issues.Add(PersistenceIssue("wound_command_transition_adapter_unavailable",
        "one supported homogeneous persisted wound-command family",
        "diagnosis/alternative capture requires its own accepted-world adapter"));
}
```

Add `parsedCommand.AcceptedTransitionCommands.Count != 0` to the existing cold-treatment
replay rejection disjunction. In the strict repair reader, preserve the existing tuple
protocol with:

```csharp
return parsed.Success && parsed.AcceptedTransitionCommands.Count == 0
    ? (root, parsed) : (null, null);
```

- [ ] **Step 5: Run owning controls, self-review and commit**

Pure control (all new rows plus local reducers and source guards; existing opportunity
and treatment command execution is covered by the Integration selection below):

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundAcceptedTransitionCommandTests|FullyQualifiedName~MortalWoundDiagnosisTests.CommandParsing_|FullyQualifiedName~MortalWoundDiagnosisTests.CommandRecomposition_|FullyQualifiedName~MortalWoundDiagnosisTransitionTests|FullyQualifiedName~MortalWoundAlternativeTransitionTests|FullyQualifiedName~WoundMaterializationSourceGuardTests"
```

Name new Integration methods `AcceptedTransitionCommand_...` in the owning lifecycle
partial, and run this bounded combined control sequentially after the pure lane:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~WoundMaterializationLifecycleTests.AcceptedTransitionCommand_|FullyQualifiedName~WoundMaterializationLifecycleTests.AcceptedCommand_ForeignAcceptedEventFailsExactPreparationAuthority|FullyQualifiedName~WoundMaterializationLifecycleTests.AcceptedDecline_UnresolvedOwnerTargetFailsBeforeCommandConsumption|FullyQualifiedName~WoundMaterializationLifecycleTests.AcceptedCommand_DuplicateNestedOpportunityFieldFailsStrictParsing|FullyQualifiedName~WoundMaterializationLifecycleTests.AcceptedCommand_ChangedSealedCoordinateFailsRecomposition|FullyQualifiedName~MortalWoundTreatmentResolverTests.TreatmentCommandWriteIngress_|FullyQualifiedName~MortalWoundTreatmentResolverTests.PersistedCatalog_RejectsMixedOpportunityAndTreatmentCommandFamilies|FullyQualifiedName~MortalWoundTreatmentResolverTests.RepairPending_DoesNotCopySubmittedTreatmentWithoutANonEmptyRepairWave"
```

This covers the actual pre-existing command-authority partial, treatment ingress,
mixed-family catalog and optional pending capture. Do not substitute the whole large
lifecycle/resolver class or use the unsupported course publication baseline as coverage;
those separate remaining T070 failures are not waived by this task.

No changed-file completion or task closure from the agent report alone. Report every
RED/intermediate/GREEN command, artifact, counts, wall/exit/timeout/duplicates/cleanup,
clean build and skips. Commit only owned code/tests after self-review and GREEN:
`feat(wounds): seal diagnosis and alternative accepted commands (#1536)`.
The parent inspects actual artifacts/diff and requests independent spec/quality review.

## GM synchronization and unfinished authority boundary

This task implements the already specified client-owned command wire, exposes no GM
response capability, and explicitly refuses unsupported fresh publication. No Mortal/
afterlife prompt, worked example, manifest, matrix or daemon change is required for
this internal codec checkpoint. The following `woundTreatmentAuthorings` response,
kind-specific repair/capture and fresh publication tasks must synchronize their GM
docs/examples/guards in their own changes. A checksum-valid draft still requires current
canonical/signed wound/path/request/check/requirement/evidence authority, exact history
replay/conflict checks, and sole atomic publication by `CanonicalStateNormalizer`.
T070/T177/#1536 remain open; neither 20 green command rows nor this plan completes them.
