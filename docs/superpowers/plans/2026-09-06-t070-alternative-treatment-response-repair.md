# T070 Alternative Treatment Response and Repair Boundaries

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retain and strictly validate the GM's setting-specific alternative treatment
response, then construct a kind-specific safe correction packet without confusing it
with wound creation or claiming that a public packet proves fresh world authority.

**Architecture:** Complete immutable non-executable GM route drafts reuse the complete
reader/validator with an explicit removal-selector dialect, while canonical routes stay
distinct. Shared payload writers preserve every registered constructor. A repair-packet variant
retains only semantic route/path content and has a strict persisted public format.
Transient rejected-response authority and live retry/publication remain separate,
mandatory subsequent T070 work. Existing opportunity repair keeps its semantic payload;
Task 2 adds the approved mandatory candidate-kind discriminator to every packet variant.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** GitHub [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T059/T070; `data-model.md`
Alternative treatment authoring authority and sections 15–16. Execute after independent
approval of `2026-09-06-t070-accepted-diagnosis-alternative-commands.md`.
Read-only source audit is retained in repository Git metadata as
`sdd/t070-alternative-response-repair-audit.md`; it is evidence, not a replacement for
these requirements or the Spec Kit artifacts.

## Global Constraints

- Stay on `1536-complete-wound-materialization` in
  `E:/Games/worktrees/boe-1536-wound-materialization`. Preserve `.serena/` and unrelated work.
- No new branch, migration, compatibility parser, dual write, old-save fallback,
  remote mutation, push/PR/merge, issue closure, or session cleanup.
- Mortal injuries, consequences and cures remain GM-authored setting-specific constructs.
  No fixed catalog of injuries/medicines, discarded registered operation, opaque route
  fragment, fabricated wound/owner/severity, or guessed omitted authority.
- Distinguish a valid local shape, a safe public repair projection, a privately bound
  rejected draft, and fresh accepted-world authority. None substitutes for another.
- A valid decline has explicit null route/path and creates no accepted command,
  history row or repair work. A correction cannot silently change author into decline.
- Reject a nonempty raw authoring response before distribution until the actual
  fresh-authority adapter is implemented. Existing valid opportunity/treat input is
  not authority for an unrelated raw authoring field.
- `CanonicalStateNormalizer` remains the sole publisher. Preserve the preceding codec's
  explicit new-family gates; this plan does not unblock live alternative publication.
- Use original JSON for duplicate detection. Do not erase duplicates through JsonNode
  normalization before the structural parser sees them. Invalid batches return no drafts.
- Preserve complete canonical members, source-path omission, ordered arrays and explicit
  nulls. Keep authoring request refs opaque and exact; do not globally narrow all game IDs.
- Pure tests stay in Fast. Runtime filesystem, lease, distribution, rollback and restart
  tests stay in Integration. No runner/lane/time-limit changes or new project-file links.
- One implementation agent and one bounded PowerShell 7 C# lane at a time. Use the
  smallest Focused RED, then the exact owning control; no duplicate whole-suite runs.
- GM-facing field/repair changes require synchronized guidance, a worked example,
  manifest coverage and source/production guards in the same task checkpoint.
- Keep the exact three remaining `MortalWoundDiagnosisTests` response/repair rows
  unchanged. The legacy-source and course-publication REDs remain separate required T070.

### Task 1: Closed GM response drafts, canonical roundtrip and raw-ingress rejection

**Required implementation detail:** Read
`docs/superpowers/plans/2026-09-06-t070-gm-treatment-draft-model.md` completely before
editing. It contains this same task's complete typed GM route/operation model, shared
selector-aware validator, writer extraction and dialect regression. This is not an
extra independently dispatched prerequisite. Direct canonical route parsing behind
the GM response is forbidden: it accepts the wrong `complicationId` field and rejects
the approved unresolved `complicationRef` field. The shared scalar/graph rules stay intact.

**Files:**
- Modify `BookOfEternityClient/Models/GameResponse.cs` (one response property).
- Modify `BookOfEternityClient/Configuration/FileMapping.cs` (client-consumed set only).
- Create `BookOfEternityClient/Services/WoundResponseInputComposer.AlternativeTreatmentResponses.cs`.
- Create `BookOfEternityClient/Services/WoundAlternativeTreatmentResponse.cs` (immutable models).
- Create `BookOfEternityClient/Services/GmTreatmentRouteDraft.cs` and the cohesive
  `MortalWoundTreatmentContract.GmDrafts.cs`, `.GmDraftWriting.cs`, `.RouteWriting.cs`
  partials under that Services directory; modify `.MemberShapes.cs`, the private selector
  context/removal branch in `MortalWoundTreatmentContract.cs`, and shared writer-only
  portions of `MortalWoundTreatmentModel.cs` exactly as specified in the required detail.
- Create `BookOfEternityClient.Tests/GmTreatmentRouteDraftTests.cs` for the complete GM
  member dialect, all-mode/operation/requirement roundtrips and strict negative cases.
- Modify `BookOfEternityClient/IO/StateDistributor.cs` (raw-authorings pre-write gate only).
- If needed, extract the preceding codec's small route/path pairing predicate once into
  a shared same-partial helper. Report the exact helper/file before changing it; do not
  change the accepted wire, hashing recipe, factory/reducer seals or full member semantics.
  The intended extraction is `HasAlternativeTreatmentMemberPair(routeId, visibility, diagnosisPath)`
  in the new response partial, reused by the existing inline pairing check in
  `WoundResponseInputComposer.AcceptedTransitionParsing.cs`. Only that predicate call
  may change there; preserve the codec's existing diagnostic and source path.
- Create `BookOfEternityClient.Tests/WoundAlternativeTreatmentResponseTests.cs`.
- Create `BookOfEternityClient.IntegrationTests/WoundMaterializationLifecycleTests.AlternativeResponses.cs`.
- Update `OtherGuides/Wound_Materialization_Contract.md`, `Rules/Block_2.txt`,
  `Examples/E_CLI_Wound_Materialization.txt`, `Examples/example_validation_manifest.json`.
- Update `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs` and
  `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.Wounds.cs`
  only for this response contract and its worked examples.
- Update only the explicit expected-family dictionary in
  `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
  `CompleteEffectMaterializationManifest_CoversEveryRequiredWorkedFamily` to register
  the added wound response example; retain every existing family and exact count check.

Do not edit repair-packet production, transient request capture, accepted publication,
parent task records or unrelated tests. The two frozen repair rows remain Task 2.

**Exact storage and immutable draft interfaces:**

```csharp
// GameResponse: storage is not authority or strict validation.
[JsonPropertyName("woundTreatmentAuthorings")]
public JsonElement[]? WoundTreatmentAuthorings { get; set; }

// Services/WoundAlternativeTreatmentResponse.cs
internal sealed record WoundAlternativeTreatmentResponseDraft(
    string AuthoringRequestRef,
    string Decision,
    GmTreatmentRouteDraft? Route,
    MortalWoundDiagnosisPathDefinition? DiagnosisPath);

internal sealed record WoundAlternativeTreatmentResponseParseResult(
    bool IsValid,
    ImmutableArray<ValidationIssue> Issues,
    ImmutableArray<WoundAlternativeTreatmentResponseDraft> Drafts);

// WoundResponseInputComposer: the singular entry point is also the next repair
// builder's independent local-diagnostic source. A valid entry yields exactly one draft.
internal static WoundAlternativeTreatmentResponseParseResult ParseAlternativeTreatmentAuthoring(
    JsonElement entry, string path);
internal static WoundAlternativeTreatmentResponseParseResult ParseAlternativeTreatmentAuthorings(
    JsonElement entries, string path);
internal static void WriteAlternativeTreatmentAuthoringCanonical(
    Utf8JsonWriter writer, WoundAlternativeTreatmentResponseDraft draft);
```

The complete four-field wire remains exactly:
`authoringRequestRef`, `decision`, `route`, `diagnosisPath`.
All fields are required, including explicit nulls. The plural parser accepts only an
array of 0–32 entries, reusing the existing response `MaximumDecisions` bound. The
singular parser accepts one object. Missing top-level response input is handled by its
caller; a non-array value passed to the plural parser is invalid, not a permissive empty batch.

`authoringRequestRef` follows the existing safe opaque reference rule: exact nonempty
identifier, no whitespace, slash, backslash or colon, Unicode allowed. Refs in a batch
are exact and Unicode-confusable unique. Reuse the existing identifier/confusable helper;
do not introduce a second normalization dialect. Preserve the supplied original path.

For `author`, use the complete GM route-draft parser from the required detail and the
existing diagnosis-path shape parser. A visible route
(`public` or `known_to_player`) requires null path. A hidden route requires one complete
non-GM-only path that reveals its exact new route ID; other facts may refer to real wound
members unavailable at this local boundary. Do not infer full-wound discovery or append
legality. `gm_only` routes are invalid. For `decline`, both payloads are exactly null.
Reject an unknown decision, missing null, additional authority/result fields, recursive
duplicates, wrong types and any member error. No successful partial array or fake hashes.

- [ ] **Step 1: Observe the frozen response RED and add direct pure parser tests**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundDiagnosisTests.GameResponse_RoundTripsAlternativeTreatmentAuthoringWithoutInternalAuthority|FullyQualifiedName~WoundAlternativeTreatmentResponseTests|FullyQualifiedName~GmTreatmentRouteDraftTests"
```

Use complete production factory inputs from the preceding shared
`WoundAcceptedTransitionCommandTestData`, then extract the added canonical member from
the actual proposed-after wound. Do not copy internal evidence into the GM response.
The following complete starter regression proves the negative-result contract without
inventing authority:

```csharp
[Fact]
public void Decline_RoundTripsFourExplicitFieldsWithoutAnAcceptedResult()
{
    using var document = JsonDocument.Parse("""
        {"authoringRequestRef":"request_decline","decision":"decline",
         "route":null,"diagnosisPath":null}
        """);
    var parsed = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
        document.RootElement, "woundTreatmentAuthorings[0]");
    Assert.True(parsed.IsValid, string.Join("; ", parsed.Issues.Select(issue => issue.Code)));
    var draft = Assert.Single(parsed.Drafts);
    Assert.Equal("decline", draft.Decision);
    Assert.Null(draft.Route);
    Assert.Null(draft.DiagnosisPath);
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
        WoundResponseInputComposer.WriteAlternativeTreatmentAuthoringCanonical(writer, draft);
    using var roundTrip = JsonDocument.Parse(stream.ToArray());
    Assert.Equal(4, roundTrip.RootElement.EnumerateObject().Count());
    Assert.Equal(JsonValueKind.Null, roundTrip.RootElement.GetProperty("route").ValueKind);
    Assert.Equal(JsonValueKind.Null, roundTrip.RootElement.GetProperty("diagnosisPath").ValueKind);
    Assert.False(roundTrip.RootElement.TryGetProperty("authorityFingerprint", out _));
    Assert.False(roundTrip.RootElement.TryGetProperty("resultFingerprint", out _));
}
```

Add direct visible/hidden author roundtrip and detachment, all three route modes and
effectful nested operations, 0/32/33 rows, all four required fields, wrong type/decision,
null versus missing, exact/Unicode-confusable refs, valid punctuation in unrelated IDs,
duplicate original nested properties, foreign authority fields, hidden-path revelation
and visibility pairing, and all-or-nothing batch errors. Assert exact code/path for new
negative cases, not just invalid state. Keep complete member/writer regression controls.
GM removal always uses a declared opaque `complicationRef`; reject caller `complicationId`
and preserve nested new-complication refs without rewriting them. Test fixtures that
start from canonical route data must explicitly author the removal leaf in the GM
dialect, not treat canonical IDs as offered selectors. The immutable shared test
builder proves local factory inputs only, not fresh request authority.

- [ ] **Step 2: Share strict readers and expose complete immutable local drafts**

Use same-partial `TryReadObject`, strict string/field readers and recursive
`ValidateNoDuplicateProperties`; do not copy those implementations. Preserve original
member error paths/codes from the shared parsers. Construct a draft only with zero
issues. Match existing guarded exception-to-issue behavior, never catch-and-success.
Plural parsing uses the singular pipeline plus bounded batch/ref checks and returns
an empty immutable draft array on any failure.

The existing generic response readers produce opportunity-specific messages/hints.
Reframe their issues at this new boundary without changing their exact code/path,
expected/actual or severity; an alternative parser must not instruct the GM to fix an
unrelated wound creation opportunity. Do not alter the existing readers globally.

Complete new partial implementation (the shared readers, `Set`, `ReadString`, `Add`
and `ExactIdentifierConfusableKey.Build` already exist):

```csharp
using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class WoundResponseInputComposer
{
    private static readonly IReadOnlySet<string> AlternativeResponseFields = Set(
        "authoringRequestRef", "decision", "route", "diagnosisPath");

    internal static WoundAlternativeTreatmentResponseParseResult ParseAlternativeTreatmentAuthoring(
        JsonElement entry, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var issues = new List<ValidationIssue>();
        try
        {
            var objectValid = TryReadObject(entry, path, AlternativeResponseFields,
                AlternativeResponseFields, issues, out var fields);
            ValidateNoDuplicateProperties(entry, path, issues);
            if (!objectValid || issues.Count != 0)
                return AlternativeResponseResult(issues);

            var reference = ReadString(fields, "authoringRequestRef", path, issues);
            if (reference is not null &&
                (!ResourceMaterializationContract.IsExactIdentifier(reference) ||
                 reference.Any(character => char.IsWhiteSpace(character) ||
                     character is '/' or '\\' or ':')))
                Add(issues, path + ".authoringRequestRef", "wound_response_invalid_field",
                    "safe opaque exact reference", reference);
            var decision = ReadString(fields, "decision", path, issues);
            GmTreatmentRouteDraft? route = null;
            MortalWoundDiagnosisPathDefinition? diagnosisPath = null;
            if (decision == "author")
            {
                var routeResult = MortalWoundTreatmentContract.ParseGmRouteDraftShape(
                    fields["route"], path + ".route");
                issues.AddRange(routeResult.Issues);
                route = routeResult.Route;
                var pathElement = fields["diagnosisPath"];
                if (pathElement.ValueKind == JsonValueKind.Object)
                {
                    var pathResult = MortalWoundTreatmentContract.ParseDiagnosisPathShape(
                        pathElement, path + ".diagnosisPath");
                    issues.AddRange(pathResult.Issues);
                    diagnosisPath = pathResult.DiagnosisPath;
                }
                else if (pathElement.ValueKind != JsonValueKind.Null)
                    Add(issues, path + ".diagnosisPath", "wound_response_invalid_field",
                        "complete diagnosis path or explicit null", pathElement.GetRawText());
                if (route is not null && !HasAlternativeTreatmentMemberPair(route.RouteId, route.Visibility, diagnosisPath))
                    Add(issues, path + ".diagnosisPath", "wound_response_invalid_field",
                        "one non-GM-only revealing path only for a hidden route",
                        pathElement.GetRawText());
            }
            else if (decision == "decline")
            {
                foreach (var name in new[] { "route", "diagnosisPath" })
                    if (fields[name].ValueKind != JsonValueKind.Null)
                        Add(issues, path + "." + name, "wound_response_invalid_field",
                            "explicit null for decline", fields[name].GetRawText());
            }
            else
                Add(issues, path + ".decision", "wound_response_invalid_field",
                    "author or decline", fields["decision"].GetRawText());

            return AlternativeResponseResult(issues, issues.Count == 0
                ? new[] { new WoundAlternativeTreatmentResponseDraft(
                    reference!, decision!, route, diagnosisPath) }
                : Array.Empty<WoundAlternativeTreatmentResponseDraft>());
        }
        catch (Exception exception) when (exception is JsonException or
            InvalidOperationException or FormatException or OverflowException)
        {
            Add(issues, path, "wound_response_invalid_field",
                "one complete alternative treatment response", exception.GetType().Name);
            return AlternativeResponseResult(issues);
        }
    }

    internal static WoundAlternativeTreatmentResponseParseResult ParseAlternativeTreatmentAuthorings(
        JsonElement entries, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var issues = new List<ValidationIssue>();
        var drafts = new List<WoundAlternativeTreatmentResponseDraft>();
        if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > MaximumDecisions)
        {
            Add(issues, path, "wound_response_invalid_field", $"array of 0–{MaximumDecisions} authorings",
                entries.ValueKind == JsonValueKind.Array ? entries.GetArrayLength().ToString(
                    System.Globalization.CultureInfo.InvariantCulture) : entries.ValueKind.ToString());
            return AlternativeResponseResult(issues);
        }
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var itemPath = $"{path}[{index++}]";
            var parsed = ParseAlternativeTreatmentAuthoring(entry, itemPath);
            issues.AddRange(parsed.Issues);
            if (!parsed.IsValid) continue;
            var draft = parsed.Drafts.Single();
            if (!exact.Add(draft.AuthoringRequestRef) ||
                !confusable.Add(ExactIdentifierConfusableKey.Build(draft.AuthoringRequestRef)))
                Add(issues, itemPath + ".authoringRequestRef", "wound_response_duplicate_reference",
                    "exact and confusable-unique authoring request reference", draft.AuthoringRequestRef);
            drafts.Add(draft);
        }
        return AlternativeResponseResult(issues, drafts);
    }

    private static bool HasAlternativeTreatmentMemberPair(
        string routeId, string visibility, MortalWoundDiagnosisPathDefinition? path) =>
        visibility == "hidden"
            ? path is not null && path.Visibility != "gm_only" &&
              path.Reveals.Any(fact => fact.CanonicalValue == "route:" + routeId)
            : path is null;

    private static WoundAlternativeTreatmentResponseParseResult AlternativeResponseResult(
        IReadOnlyCollection<ValidationIssue> issues,
        IEnumerable<WoundAlternativeTreatmentResponseDraft>? drafts = null)
    {
        var resultIssues = issues.Select(issue => new ValidationIssue(
            issue.FilePath, issue.Severity,
            "The alternative treatment response violates the complete local authoring contract.",
            code: issue.Code, actor: issue.Actor, section: "wound_treatment_authorings",
            expected: issue.Expected, actual: issue.Actual,
            repairHint: "Keep the offered authoring request reference and correct only the indicated route/path fields.",
            category: issue.Category)).ToImmutableArray();
        return new(resultIssues.IsEmpty, resultIssues, resultIssues.IsEmpty
            ? (drafts ?? Array.Empty<WoundAlternativeTreatmentResponseDraft>()).ToImmutableArray()
            : ImmutableArray<WoundAlternativeTreatmentResponseDraft>.Empty);
    }

    internal static void WriteAlternativeTreatmentAuthoringCanonical(
        Utf8JsonWriter writer, WoundAlternativeTreatmentResponseDraft draft)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(draft);
        using var stream = new MemoryStream();
        using (var buffer = new Utf8JsonWriter(stream))
        {
            buffer.WriteStartObject();
            buffer.WriteString("authoringRequestRef", draft.AuthoringRequestRef);
            buffer.WriteString("decision", draft.Decision);
            buffer.WritePropertyName("route");
            if (draft.Route is null) buffer.WriteNullValue();
            else MortalWoundTreatmentContract.WriteGmRouteDraft(buffer, draft.Route);
            buffer.WritePropertyName("diagnosisPath");
            if (draft.DiagnosisPath is null) buffer.WriteNullValue();
            else MortalWoundTreatmentContract.WriteDiagnosisPathCanonical(buffer, draft.DiagnosisPath);
            buffer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(stream.ToArray());
        var verified = ParseAlternativeTreatmentAuthoring(document.RootElement, "woundTreatmentAuthorings[0]");
        if (!verified.IsValid)
            throw new InvalidOperationException("Cannot write an invalid alternative response: " +
                string.Join("; ", verified.Issues.Select(issue => issue.Code)));
        document.RootElement.WriteTo(writer);
    }
}
```

The preceding codec's pairing branch becomes this call; its existing body/diagnostic
stays unchanged:

```csharp
if (!HasAlternativeTreatmentMemberPair(route.RouteId, route.Visibility, diagnosisPath))
    InvalidAcceptedField(issues, resultPath + ".diagnosisPath",
        "one non-GM-only revealing path only for a hidden route", Raw(result, "diagnosisPath"));
```

Canonical response writing emits one four-field object, delegates the route to its
distinct complete GM writer and the path to its existing complete writer, and explicitly
writes both nullable payloads. It does not convert the GM route to canonical gameplay
state. Validate
the typed decision/payload pairing before writing; a manually constructed invalid draft
must throw rather than serialize a new permissive wire. No accepted command, transition,
event binding, resource reservation, result fingerprint or world-state mutation here.

- [ ] **Step 3: Declare client consumption and prove fail-closed raw distribution**

Add `woundTreatmentAuthorings` beside `woundDecisions` in
`FileMapping.ClientConsumedResponseFields`, never `FieldToFile`. At the very start of
`StateDistributor.ResolveAcceptedWoundCommand`, before the null accepted-input branch
and before any write preparation, add:

```csharp
if (response.WoundTreatmentAuthorings is { Length: > 0 })
    throw new InvalidDataException(
        "wound_authorings_require_accepted_adapter: raw alternative treatment responses require their fresh accepted-world adapter.");
```

This remains active even alongside a valid unrelated opportunity/treat input. A nonempty
decline is still a request resolution requiring the missing adapter, not permission to
drop the GM field. Null/empty input preserves existing no-input behavior. The existing
command-family gate remains unchanged. The subsequent fresh adapter must replace both
gates before T070 is complete; this step does not claim a playable authoring workflow.

Name new Integration tests `AlternativeResponse_...`. Reuse actual lease-aware lifecycle
helpers. Test author/decline raw input alone and beside a genuine accepted opportunity
input; seed unrelated canonical and output files and prove byte-identical rejection,
no command/backup remnants and no silently written scene. Include null/empty controls.
Observe real pre-gate behavior RED after the DTO/parser seam is available.

- [ ] **Step 4: Synchronize the GM response contract and worked examples**

Use marker `wound_mortal_alternative_response_v1` in the guide and wound example file.
Explain the exact four fields, setting-authored complete members, hidden/visible path
rule, explicit-null decline, no GM authority fingerprints or client transition IDs,
GM `remove_complication.complicationRef` versus canonical `complicationId`, and that only an offered
client-bound request may eventually be resolved. Explicitly identify this checkpoint
as local parsing with live distribution still unavailable; do not instruct the GM to
invent a request or send a field the current owning workflow has not offered.

Add complete visible author, hidden author and decline JSON responses under distinct
submarkers. Each example includes a complete route/path as appropriate, not `{}` or
ellipses. A documented setting-specific alternative is an example, never a catalog.
At least one authored route removes a complication using an offered opaque selector.
Its worked production guard must reject the corresponding canonical-field substitution.
Preserve the approved diagnosis-fact grammar locally; this task does not invent a new
fact selector or disclose hidden canonical complication identities through request packets.
Add a source guard and production example tests that deserialize GameResponse, invoke
the new strict parser, canonically roundtrip every entry and reject copied authority.
Register the worked examples in `example_validation_manifest.json` with exact production
validation route and honest coverageLimit (no fresh request/publication proof).
Use one `effectMaterializationCoverage` entry for the complete
`wound_mortal_alternative_response_v1` family with all three submarkers in requiredText,
and extend the owning exact expected-family dictionary by that same one entry. Add no
syntax/shape exemptions. Append examples without changing unrelated existing snippets.
Update `Rules/Block_2.txt` field guidance without implying live capability is available.

This task is strictly Mortal and preserves every construction/afterlife packet shape;
record that afterlife matrix/examples/daemon entrypoint need no change. The daemon
already requires the wound guide; do not create a new prompt-only workaround.

- [ ] **Step 5: Verify the owning boundary and commit**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundAlternativeTreatmentResponseTests|FullyQualifiedName~GmTreatmentRouteDraftTests|FullyQualifiedName~MortalWoundDiagnosisTests.GameResponse_RoundTripsAlternativeTreatmentAuthoringWithoutInternalAuthority|FullyQualifiedName~WoundAcceptedTransitionCommandTests|FullyQualifiedName~MortalWoundTreatmentMemberShapeTests|FullyQualifiedName~PromptDocumentationCoverageTests.WoundAlternativeTreatmentResponseDocumentation_|FullyQualifiedName~MortalWoundTreatmentContractTests.Parse_CanonicalRemoveComplicationAcceptsOnlyPermanentComplicationId|FullyQualifiedName~MortalWoundTreatmentContractTests.ProposalComposition_RewritesSameProposalComplicationRefToCanonicalId|FullyQualifiedName~MortalWoundTreatmentContractTests.ProposalComposition_WorseningPreservesSignedCanonicalComplicationSelector|FullyQualifiedName~MortalWoundTreatmentContractTests.ProposalComposition_RejectsUnicodeDashConfusableComplicationRefs|FullyQualifiedName~MortalWoundTreatmentContractTests.ProposalComposition_RejectsCallerAuthoredCanonicalComplicationId"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~WoundMaterializationLifecycleTests.AlternativeResponse_|FullyQualifiedName~WoundMaterializationLifecycleTests.AcceptedTransitionCommand_|FullyQualifiedName~ExampleDocumentationValidationTests.AlternativeTreatmentResponseWorkedExamples_|FullyQualifiedName~ExampleDocumentationValidationTests.CompleteEffectMaterializationManifest_CoversEveryRequiredWorkedFamily"
```

Run the final commands sequentially. Record every RED/intermediate/GREEN artifact,
counts, wall/exit/timeout/duplicates/cleanup/build/skips and GM/afterlife rationale.
Commit only owned files after exact controls pass:
`feat(wounds): validate alternative GM treatment responses (#1536)`.
Parent inspects actual evidence/diff and obtains independent spec/quality review before
Task 2. Do not mark any parent task complete from the implementer report alone.

### Task 2: Kind-specific safe repair packets and strict persisted public transport

**Status:** Bounded design recorded; controller must finish this task's executable
brief after Task 1, incorporating its actual parser API and diagnostic evidence. Do
not dispatch this task from the provisional checklist below.

**Governing packet discriminator:** The approved
`contracts/wound-repair-retry-and-rollback.md` requires `candidateKind` on the public
packet for all four kinds, not only alternatives. This stronger explicit contract
governs the narrower data-model section 16 example. Do not weaken that approved rule
to preserve the current implementation's omitted discriminator. Task 2 performs one
current-format cutover, updating every affected producer, strict consumer, example and
guard together; missing tags fail, and no tagless compatibility reader is introduced.
The earlier read-only repair audit missed this stronger contract; its alternative-only
tag recommendation is superseded by this source-backed correction.

**Planned ownership:** `WoundRepairPacketBuilder.cs`, a cohesive alternative partial,
`.PersistenceValidation.cs`; kind-specific public resubmission-route projection and
unsupported alternative capture/retry checks in `GameEngine.ValidationAndRepair.cs`;
pure packet/privacy/harness tests; owning persisted-wave and rollback Integration
partials; wound/repair GM guidance, worked correction example, manifest and guards.
Do not widen the opportunity command-ordinal obligation into a loose unsealed authoring
JSON bag. Current public pending packets cannot reconstruct transient authoring authority.

- [ ] Add the required `candidateKind` field to every closed public packet variant:
  `construct_wound`, `repair_wound`, `author_alternative_treatment`, or
  `narrate_acquisition`. Preserve the exact kind internally from the build request,
  never infer it from untrusted response shape. Existing construction semantic payloads
  remain unchanged; every closed field set gains the discriminator. Missing, unknown
  and kind/shape-mismatched tags fail. Update all exact field-set assertions and callers
  in the same task, with no optional legacy wire or compatibility loading.
- [ ] Branch before construction-only allowed-decision, severity, woundRef/proposal
  requirements. The frozen constructor's shared correlation slot supplies only the safe
  authoring ref for this variant; its I/IV placeholders confer no wound/rank authority
  and must not be validated, invented or emitted as alternative treatment mechanics.
- [ ] Build a repair candidate only for a rejected `author` route/path draft. A valid
  decline has no repair work; malformed decline is fail-closed, never synthesized into
  an author route. Preserve exact original authoring ref and decision during correction.
- [ ] Normalize canonical-index `woundTreatmentAuthorings[n].route.*` and
  `.diagnosisPath.*` to semantic member paths. Derive legal expected values from the
  actual local member/response validator, not arbitrary supplied issue.Expected text.
  Match supplied local code/path to independent diagnostics where feasible; a rejected
  draft need not be a successful complete member. Do not accept arbitrary path/code pairs.
- [ ] Preserve only non-offending semantic route/path content, recursively remove
  private authority keys/values, and emit exactly one four-field
  `requiredResponseShape.woundTreatmentAuthorings` entry. No woundDecisions, woundRef,
  severity or acquisition-narration obligation. Bound readable evidence and retain
  permitted authored route/path IDs and safe request refs (no generic *Id scrubbing).
- [ ] A correction replaces invalid required values and omits unknown forbidden fields;
  it does not inherit the construction matcher's blanket invalid-field-means-omission
  rule. Independently parse complete corrected members and preserve every non-offending
  sibling and ordered array position. Test multiple offending indices without deleting
  or shifting unrelated array elements. Local matching still proves no world authority.
- [ ] Update strict persisted packet/issue/template/public-payload validation as one
  variant-aware contract. Preserve recursive duplicate checks, exact root identities,
  positional receipts, confusable uniqueness and the 1–64 wave bound. The same privacy
  policy applies to builder and persisted input. Public format validity is not restart
  reconstruction of hidden before/evidence/requirement authority.
- [ ] Retain typed treat-only submittedTreatmentRequests semantics and preceding codec
  gates. Explicitly reject unsupported alternative live capture/retry as a whole wave,
  including mixed siblings. Do not create an accepted command from the rejected raw
  response merely to reuse current command-ordinal capture.
- [ ] Correct the independent public resubmission obligation route currently hardcoded
  to woundDecisions. Alternative projection uses woundTreatmentAuthorings; ordinary
  construction remains unchanged. Ensure renderable-only packets cannot become an
  actionable live wave before the transient-authority adapter exists.
- [ ] Keep the two frozen repair tests unchanged; add kind/path/code mismatches, privacy,
  corrected required field versus omitted forbidden field, null/path pairing, unrelated
  sibling preservation, contextual-build parity, exact persisted transport and mixed-wave
  rejection. Update GM repair guidance and one executable hidden-route correction example.
- [ ] The shared construction discriminator also affects afterlife repair transport.
  Audit/update the afterlife matrix, CLI examples, manifest, source/production guards
  and daemon guidance where applicable. Preserve all spiritual mechanics and semantic
  construction shapes; synchronize only the changed required packet field. Include the
  repository-required Focused AfterlifeDocumentationCoverageTests and one FullValidation
  control for this documentation-sensitive boundary, without duplicate broad controls.
- [ ] Obtain owning Focused evidence and independent review. Then the three original
  response/repair REDs are implemented, but transient capture/fresh publication,
  closed heal-legacy sources and course publication remain required T070 work.

## Completion Boundary

No part of this plan removes a planned mechanic or authorizes a merge. The next actual
fresh adapter must bind the GM response to the offered private request, recheck current
wound/evidence/requirements, enforce append-only history and exact retry/replay, and use
sole atomic canonical publication. Only then may the unsupported-adapter gates be
replaced. T070/T177/#1536 stay open throughout these internal boundary checkpoints.
