# T070 Alternative Repair Projection Implementation Detail

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. This is the required detail for Task 2 of the response/repair plan, not a separate independently dispatched task.

**Issue:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536).

**Goal:** Repair a rejected GM-authored treatment route/path without rewriting valid
siblings, exposing private authority, or pretending public packets can resume a live turn.

**Architecture:** Retain the four-kind closed public packet and its existing private
candidate. Independently validate the rejected and corrected alternative response with
the Task 1 parser. Use stable semantic paths and non-shifting masks for comparison;
the public schema reader checks transport, while existing live adapters remain closed.

**Tech Stack:** C#, System.Text.Json/JsonNode, xUnit, bounded PowerShell 7 test lanes.

## Global constraints

- The governing repair contract requires candidateKind on all four packet variants.
- No migration or tagless compatibility reader.
- No fake wound, severity, opportunity, evidence event, accepted command or fingerprint.
- Mortal alternative authoring is distinct from spiritual wound construction.
- Keep the existing receipt's five fields and the wave's 1–64 bound.
- No changes to test lanes, runtime limits, branch, remote state or .serena.
- The owning brief records the actual reviewed Task 1 commit. Public repair validity
  never substitutes for the still-required fresh authoring adapter.

## Source ownership and exact interfaces

Modify:

- BookOfEternityClient/Services/WoundRepairPacketBuilder.cs:
  packet discriminator, shared build dispatch, context routing and private snapshots.
- BookOfEternityClient/Services/WoundRepairPacketBuilder.PersistenceValidation.cs:
  eleven-field packet, variant-specific strict reader and shared privacy rules.
- BookOfEternityClient/Services/MortalWoundTreatmentPersistence.cs:
  ComposePendingRoot and ParsePending admission gates.
- BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:
  exact capture/retry refusal, candidate-kind resubmission route and runtime GM text.
- BookOfEternityClient/Services/MortalWoundTreatmentContract.GmDrafts.cs and
  MortalWoundTreatmentContract.MemberShapes.cs: phase raw shape diagnostics before
  semantic validation so skipped invalid raw array entries cannot renumber later
  semantic diagnostics. Valid complete-member behavior remains unchanged.

Create:

- BookOfEternityClient/Services/WoundRepairPacketBuilder.AlternativeTreatment.cs:
  safe projection, semantic path handling and generated issue descriptions.
- BookOfEternityClient/Services/WoundRepairPacket.AlternativeTreatment.cs:
  immutable edit rules and alternative corrected-draft comparison. Make the existing
  WoundRepairPacket partial; do not expand its construction implementation.
- BookOfEternityClient/Services/WoundRepairPacketBuilder.AlternativePersistence.cs:
  the closed alternative public transport reader, reusing existing shared strict helpers.
- BookOfEternityClient.Tests/WoundAlternativeTreatmentRepairTests.cs:
  pure build, privacy, immutable comparison and contextual parity.
- Cohesive partials WoundAlternativeTreatmentRepairTests.Privacy.cs,
  .Correction.cs and .Persistence.cs in the same test directory, if needed to keep
  the separate assertion groups readable. Shared fixture remains in the base partial.
- BookOfEternityClient.IntegrationTests/WoundMaterializationLifecycleTests.AlternativeRepair.cs:
  actual file/capture/rollback and public-only live admission tests.

Existing tests owned by this cutover:

- BookOfEternityClient.Tests/WoundRepairPacketBuilderTests.cs
- BookOfEternityClient.Tests/WoundRepairPacketPrivacyTests.cs
- BookOfEternityClient.Tests/ValidationRepairRequestTests.cs
- BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs
- BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs
- BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.PersistedRepairWave.cs
- BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.Persistence.cs
- BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.Wounds.cs
- BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs

Consume the actual Task 1 API:

~~~csharp
WoundAlternativeTreatmentResponseParseResult
    WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
        JsonElement entry, string path);
// Result: IsValid, ImmutableArray<ValidationIssue> Issues,
// ImmutableArray<WoundAlternativeTreatmentResponseDraft> Drafts.
// Draft: AuthoringRequestRef, Decision, GmTreatmentRouteDraft? Route,
// MortalWoundDiagnosisPathDefinition? DiagnosisPath.
~~~

Produce:

~~~csharp
// On the existing immutable packet:
internal string CandidateKind { get; }
internal string ResubmissionRoute => CandidateKind ==
    "author_alternative_treatment" ? "woundTreatmentAuthorings" : "woundDecisions";
internal bool MatchesCorrectedAlternativeTreatmentAuthoring(JsonObject corrected);
internal WoundAlternativeTreatmentCorrectionAnalysis AnalyzeAlternativeTreatmentCorrection(
    JsonObject corrected);

internal enum WoundAlternativeTreatmentCorrectionStatus
{
    Rejected,
    NeedsAnotherRepair,
    Complete
}
internal sealed record WoundAlternativeTreatmentCorrectionAnalysis(
    WoundAlternativeTreatmentCorrectionStatus Status,
    ImmutableArray<ValidationIssue> Issues,
    JsonElement? CorrectedAuthor,
    WoundAlternativeTreatmentResponseDraft? CompleteDraft);

// On the existing builder partial:
internal static bool IsSupportedLiveRepairPacketKind(string? candidateKind) =>
    candidateKind is "construct_wound" or "repair_wound" or "narrate_acquisition";
~~~

The property is derived from an already validated immutable candidate kind, not from
the presence of a response array. The public persisted reader validates the full
closed four-value vocabulary. The live admission helper deliberately accepts only
the three opportunity-family values until real private alternative authority exists.

## Build decision and diagnostic authority

Keep exact root IDs, semantic fingerprint syntax, candidate order, confusable
uniqueness and the candidate count in the shared builder. Dispatch the alternative
branch before severity, construction decisions, woundRef and proposal checks.
The frozen constructor's OpportunityRef slot means only authoringRequestRef for this
variant; its I/IV placeholders are neither validated nor emitted as mechanics.

An alternative candidate requires the four exact response fields, exact safe ref equal
to its correlation slot, decision author or decline, and both offered decision tokens
without duplicates. OpportunityAuthorityFingerprint must be null for this kind.
An independently valid decline with no error issues produces no packet and does not
require fail-closed rollback. A malformed decline or fabricated error attached to a
valid decline fails closed; neither creates an author draft. The shared build loop
therefore skips a successful null packet, but still discards every sibling when any
candidate fails. It does not skip failed candidates.

For rejected author:

1. Require at least one real Error issue. Accept source section wound_materialization
   (the frozen fixture) or wound_treatment_authorings (the actual Task 1 parser).
   Context reconstruction must preserve the kind and support the latter section;
   do not silently filter out alternative errors before deciding rollback.
2. Normalize every supplied path from one identical canonical
   woundTreatmentAuthorings[n] prefix. Reject leading zeros, signs, whitespace,
   different candidate ordinals, backslashes, files, traversal, and paths outside
   route/diagnosisPath. A root ref/decision error cannot authorize changing identity.
3. Parse the complete original rejected author at that same prefix. Require each
   supplied (Code, FilePath) to occur in the independent diagnostics. Never trust
   supplied Expected, Actual, Message or RepairHint as protocol instructions.
   The draft is intentionally invalid; requiring a successful parse here is a bug.
4. Collect private string values recursively from the original safeContext and
   rejected response. Sanitize a detached copy of only route and diagnosisPath.
   Then independently parse the sanitized four-field author envelope again.
5. Consider all safe diagnostics in the current validation phase, not just the
   caller's subset. A private alias removed from a required authored field creates a
   missing-field correction; an unknown container such as nestedSecrets must not
   silently become an immutable invalid sibling.
   Never expose a private field name as an issue path. If a remaining diagnostic
   cannot be represented safely, fail the whole wave.
6. Generate the public Expected text from a closed code/path catalog below. Derive
   Actual from the independent diagnostic, then apply existing evidence bounds and
   private-value redaction. Collapse identical semantic path/code pairs in stable order.
7. After the diagnostic-scope rules below, mask the selected offending semantic paths
   in the sanitized payload. Object properties
   are removed; array elements become null holes, never RemoveAt. Preserve all
   other values, order, array lengths and sibling locations.
8. Freeze the original response, sanitized masked payload, projected issues and the
   private sensitive-value set inside the packet. Getters and constructors remain
   detached; no caller-owned JsonNode or mutable set is retained.

If the original is rejected but sanitization leaves no safe actionable diagnostic,
fail closed; do not manufacture an issue, emit an empty repair packet, or treat the
original as accepted. If additional diagnostics need a later phase, report them only
after a scope-valid correction as described below.

The public preservedProposal has exactly route and diagnosisPath when those roots
are non-offending; a wholly offending root can be absent. It is a masked payload,
not a complete author response and not an executable treatment route.

## Diagnostic coordinates, dependencies and staged correction

The source-backed design audit identified three real obstacles to using diagnostics
as edit permissions: compacted raw arrays, derivative pairing-root errors, and
mode-dependent validators which cannot run until the GM fixes a discriminator.
Do not dispatch the earlier single-pass algorithm. Use these explicit rules.

### Preserve source coordinates by validation phase

Both detached member parsers must finish raw shape validation before starting their
semantic validator. Their raw readers currently discard invalid requirement/outcome/
fact rows. Calling semantic validation on that partial list renumbers later errors.
Change the two existing guards, without inventing placeholders or rewriting array data:

~~~csharp
// ReadValidatedRouteShape, after ReadTreatmentRoute:
if (route is not null && issues.Count == 0)
{
    ValidateRoute(route, path, new ValidationContext("mortal_world", null, dialect), issues);
    if (issues.Count == 0)
        return build(route);
}

// ParseDiagnosisPathShape, after ReadDiagnosisPath:
if (diagnosis is not null && issues.Count == 0)
{
    ValidateDiagnosisPath(diagnosis, path, routeIds: null, complicationIds: null, issues);
    if (issues.Count == 0)
        return new(true, ImmutableArray<ValidationIssue>.Empty, BuildDiagnosisPath(diagnosis));
}
~~~

Raw diagnostics already use original array indices. With zero raw errors no entries
were skipped, so semantic diagnostics also use original indices. This deliberately
reports a phase, not an exhaustive list of errors in still-malformed input.
For reveals=[null,"bogus","route:valid"], phase one repairs index 0; phase two then
reports index 1, never compacted index 0. Preserve index 2 across both corrections.
Exercise equivalent requirement/outcome cases and sanitizer-created null holes.

### Narrow edit scopes before masking

An error appearing in the real parser does not automatically authorize replacing all
its descendants. Normalize/project diagnostics, then keep the most specific semantic
paths: if an issue path has a strictly deeper issue path, defer the ancestor issue
until a later phase instead of granting its broad mask. Compare parsed path segments,
not a textual prefix which confuses indices 1 and 10. Stable order remains source order.
Duplicate path/code entries collapse; multiple codes at one exact path do not widen it.

This rule applies even when a parent collection error might be independent. First
repair the precisely identified child; if the collective error remains, the next
phase diagnoses the collection on otherwise well-shaped input. A genuine collective
array error with no child diagnostic permits correction of that array as a unit.
Its outside siblings remain exact; no promise is made that every descendant of an
explicitly collectively invalid array was independently valid. Tests must distinguish
this from two leaf faults, which never permit replacing/reordering their third sibling.

Treat the existing response pairing diagnostic specially. Independently examine the
actual route/path local parse results before turning its root into a permission:

- A hidden path with an invalid member has only its narrow member corrections now.
  The root pairing error caused by the null typed parse result grants no root mask.
- A hidden missing/non-object path has a genuinely absent/invalid whole member and
  can receive a whole diagnosisPath correction.
- A visible non-null path is forbidden as a whole and must become null.
- An otherwise valid hidden diagnosis path with visibility=gm_only needs only
  diagnosisPath.visibility changed, preserving ID, requirements and all facts.
- An otherwise valid path lacking the required new route fact needs that fact
  appended to diagnosisPath.reveals, not the whole path replaced. Use a distinct
  generated code wound_alternative_required_route_reveal_missing and expected text
  "append exactly the new route fact and preserve every existing fact in order".
  Internally retain an append-only edit rule: the corrected array is the original
  array followed by exactly the required "route:" + originalRoute.RouteId fact.
  Keep this array in preservedProposal; normalize only the approved appended value
  out of the detached comparison copy. Never mask away the original facts.

The original caller diagnostic is still matched by its actual code/path before this
client-owned narrowing. The packet's generated narrower issue describes the edit
rule, not fabricated independent world authority. Persisted validation recognizes
the narrow visibility and append-fact descriptions/templates; it cannot reconstruct
the private original or independently execute an append from public bytes.

### Reveal latent errors without granting new authority

Fixing an invalid mode can expose a previously unreachable difficulty or pairing
error. Do not guess a mode, grant every mode-dependent field a mask, or emit a packet
which can never progress. AnalyzeAlternativeTreatmentCorrection first verifies the
exact four-field identity, privacy and current edit-scope relationship. It then calls
the complete parser on the GM's supplied correction:

- Complete: zero issues; return its one typed non-executable draft.
- NeedsAnotherRepair: every currently selected edit root's exact path is resolved,
  at least one allowed value changed, and the parser now reports new/narrower issues.
  Return only the detached corrected JsonElement and immutable local diagnostics.
- Rejected: unrelated sibling changed, privacy/identity failed, no permitted progress,
  or a currently selected exact path remains invalid. No partial typed draft.

Compare semantic paths, not codes, for the remaining-current-path check. Require the
corrected raw payload to differ from the sanitized pre-mask baseline, not from a
masked object whose deliberately absent fields would create false progress. Reject
new root/ref/decision errors, unsafe diagnostics, unsupported code/path descriptions
or any diagnostic phase that cannot make a safe next packet. The append-fact rule is
validated independently even if another malformed member suppresses pairing checks.
Return null CorrectedAuthor and CompleteDraft on Rejected; return a cloned JsonElement
and null CompleteDraft on NeedsAnotherRepair. Complete returns both the detached
element and exactly one typed author draft. Never retain a caller's mutable JsonObject.

MatchesCorrectedAlternativeTreatmentAuthoring is true only for Complete. A
NeedsAnotherRepair result does not become an accepted command, consume resources,
publish state, mint an operation key, create a receipt, or count as a completed repair.
It does not manufacture another packet/fingerprint either. The later real private
adapter must refresh the rejected candidate and its semantic fingerprint from this
checked local progress under the existing bounded retry policy before calling Build
again. Reusing the old packet/fingerprint for a changed rejected draft is forbidden.

Required local sequences: invalid mode plus invalid difficulty; invalid mode plus
hidden null path; redacted first fact/requirement/outcome plus a later independent
fault. Prove progress to a new precise diagnostic, no accepted result at that stage,
and complete eventual local repair without changing an unaffected sibling. No live
adapter is introduced as part of these pure projection tests.

## Required response shape

The alternative shape has exactly woundTreatmentAuthorings and response. Its array
contains exactly one four-field entry:

~~~json
{
  "woundTreatmentAuthorings": [
    {
      "authoringRequestRef": "the-original-safe-ref",
      "decision": "author",
      "route": {
        "base": "preservedProposal.route",
        "correctOnly": ["route.mode"]
      },
      "diagnosisPath": {
        "base": "preservedProposal.diagnosisPath",
        "correctOnly": []
      }
    }
  ],
  "response": "complete accepted scene"
}
~~~

These base/correctOnly objects are harness instructions, never a submitted GM route.
They are the alternative equivalent of the existing construction proposal recipe.
Keep the ordered distinct semantic route paths in route.correctOnly and diagnosis
paths in diagnosisPath.correctOnly. A preserved non-offending null diagnosisPath is
literal null in the template. If diagnosisPath itself is offending, use its recipe
even when its base was null/absent; the issue describes whether the corrected value
must be null or a complete revealing path. This avoids inventing a diagnosis path
merely to construct the repair template.

No alternative template may contain woundDecisions, woundRef, severity, an opportunity
binding or an acquisitionNarration requirement. The final accepted scene is still a
full response, but adding a cure does not narrate receiving a new wound.

## Closed local issue catalog

Use one helper for construction of alternative issue descriptions and verification
of persisted alternative issue descriptions. The input path must already be a valid
route/diagnosisPath semantic path; unsupported codes fail closed. Common nested effect
definition errors are rebased by MortalWoundTreatmentContract to invalid_field;
source-owned effect parameters additionally retain their three exact parameter codes.

~~~csharp
private static string? AlternativeExpected(string path, string code) =>
    (path, code) switch
    {
        ("route.mode", "wound_materialization_invalid_field") =>
            "procedure, course, or guaranteed",
        ("diagnosisPath", "wound_response_invalid_field") =>
            "one complete non-GM-only path revealing the new hidden route; null for a visible route",
        ("diagnosisPath.visibility", "wound_response_invalid_field") =>
            "public, known_to_player, or hidden; preserve the complete diagnosis path",
        ("diagnosisPath.reveals", "wound_alternative_required_route_reveal_missing") =>
            "append exactly the new route fact and preserve every existing fact in order",
        ("route.visibility", "wound_treatment_route_visibility_invalid") =>
            "public, known_to_player, or hidden",
        (_, "wound_materialization_unknown_field") =>
            "omit only this unknown GM-authored field and preserve every valid sibling",
        (_, "wound_materialization_missing_field") =>
            "supply this required complete current-version route/path field",
        (_, "wound_materialization_invalid_identifier") =>
            "one non-empty trimmed NFKC exact identifier without control or separator characters",
        (_, "wound_materialization_duplicate_identifier") =>
            "one exact and Unicode-confusable-unique identifier in this collection",
        (_, "wound_materialization_duplicate_coordinate") =>
            "one unique coordinate in this closed local route/path payload",
        (_, "wound_materialization_limit_exceeded") =>
            "a collection within the documented closed current-version route/path limits",
        (_, "wound_treatment_diagnosis_fact_unknown") =>
            "route:<routeId> or complication:<complicationId> in the documented fact grammar",
        (_, "wound_treatment_diagnosis_path_unreachable") =>
            "at least one known-fact prerequisite for a hidden diagnosis path",
        (_, "effect_source_parameter_forbidden") =>
            "omit this parameter not declared by its local source definition",
        (_, "effect_source_parameter_required") =>
            "supply the required parameter declared by its local source definition",
        (_, "effect_source_parameter_out_of_bounds") =>
            "a value inside the exact local source definition parameter bounds",
        (_, "wound_materialization_invalid_field" or
            "wound_materialization_consequence_slot_invalid" or
            "wound_materialization_effect_binding_invalid" or
            "wound_materialization_owned_source_graph_invalid") =>
            "replace only this invalid value with a complete legal current-version route/path value",
        _ => null
    };
~~~

Do not accept arbitrary caller descriptions in the persisted reader. This catalog
describes the schema restriction without copying untrusted authored values into
instructions. Full details remain in the wound GM guide; corrected response parsing
enforces the actual mode/operation/requirement/effect/legacy rules and their bounds.
Do not register duplicate-property/root identity errors as leaf repairs: the input
JsonObject is already a trusted detached candidate, and ambiguous original JSON must
have been refused before candidate creation.

Validate diagnostic-specific paths as well as the root grammar:

- wound_treatment_diagnosis_fact_unknown: diagnosisPath.requiresKnownFacts or
  diagnosisPath.reveals, optionally followed by one canonical array index.
- wound_treatment_diagnosis_path_unreachable: diagnosisPath.requiresKnownFacts.
- wound_response_invalid_field: exactly diagnosisPath, or the builder-narrowed
  diagnosisPath.visibility rule; no arbitrary route field.
- wound_alternative_required_route_reveal_missing: exactly diagnosisPath.reveals.
- wound_treatment_route_visibility_invalid: exactly route.visibility.
- Source-parameter codes: beneath a legal route outcome/interruption result's
  legacies[i].effectDraft.applications[j].parameters, optionally one parameter key.
  Use the actual paths generated in ValidateMechanicalLegacyDraft; a free-standing
  route.effectDraft is not a legal source. Match parsed segments/canonical indices.

Generic materialization errors must use strict parsed route/diagnosisPath paths and
the same private-key/path restrictions. They are independently matched to the actual
parser before projection, not assigned to arbitrary supplied paths. The public reader
checks the closed description and path vocabulary, not the absent original diagnostic.

## Privacy and stable masking

Extend the existing exact case-insensitive sensitive key set, preserving all its old
members, with:

~~~text
operationKey
expectedBeforeFingerprint
expectedAfterFingerprint
requestAuthorityFingerprint
routeFingerprint
diagnosisPathFingerprint
evidenceAuthorityFingerprint
requirementAuthorityFingerprint
checkResultFingerprint
resultFingerprint
~~~

Existing keys already include woundId, effectId, ownerId, providerId, resourceId,
carrierPath, routeSeal, resourceSeal, sourceSeal, providerSeal, authorityFingerprint,
privateNpcData and gmPrivateNotes. Do not strip every *Id or *Ref: routeId,
diagnosisPathId, complicationRef and allowed authored requirement references are
part of the approved GM dialect.

The alternative sanitizer additionally omits string values containing collected
private values or existing unsafe authority evidence markers, including aliases in
ordinary display text, nested objects, arrays and issue Actual. Retain array slots
as null when redacting array values. Project readable safe context only after
checking it against the same value policy; do not invent an event/target if its
text is unsafe. The packet needs a privately copied sensitive-value set so a later
correction cannot reintroduce a secret that appeared only in the original safeContext.
Check projected semantic path strings against this value policy too: an unknown
property whose name aliases a private string must not leak through issues.path.
The persisted public reader enforces known forbidden keys/markers and closed schema;
it cannot recover original private strings already omitted from the packet. Only the
private packet's copied set proves absence of those original aliases during correction.

Stable masking uses the existing strict path grammar, not a second parser. The core
mutation is:

~~~csharp
private static void MaskAlternativePath(JsonObject payload, string path)
{
    if (!TryParsePath(path, out var segments))
        throw new InvalidOperationException("An alternative mask requires a validated semantic path.");
    JsonNode? current = payload;
    for (var index = 0; index < segments.Count - 1; index++)
    {
        current = (current, segments[index]) switch
        {
            (JsonObject obj, string field) => obj[field],
            (JsonArray array, int ordinal) when ordinal < array.Count => array[ordinal],
            _ => null
        };
        if (current is null)
            return; // A missing required field already has no preserved value.
    }
    switch (current, segments[^1])
    {
        case (JsonObject obj, string field):
            obj.Remove(field);
            break;
        case (JsonArray array, int ordinal) when ordinal < array.Count:
            array[ordinal] = null;
            break;
    }
}
~~~

Expose this helper internally within the builder only if the packet partial needs
it for comparison; do not copy its implementation into the packet. The path has
already passed the route/diagnosisPath whitelist and canonical ordinal checks.
Do not use the existing construction RemovePath implementation for alternative
arrays because it removes elements and shifts unrelated siblings.

## Corrected author comparison

MatchesCorrectedAlternativeTreatmentAuthoring must return false for every other
candidate kind. Conversely MatchesOpportunity and MatchesCorrectedDecision explicitly
refuse the alternative kind. Do not silently route alternative through construction.

AnalyzeAlternativeTreatmentCorrection owns the comparison in this order:

1. Require exactly the four response fields, immutable original authoringRequestRef
   and decision=author. A repair cannot switch to decline, change evidence binding
   or supply a woundDecisions entry.
2. Reject any sensitive key/value reintroduced into the complete corrected response.
3. Copy only route and diagnosisPath into a new detached JsonObject. Apply the
   packet's immutable edit rules: verify/normalize an append-only reveal, then mask
   only selected replacement/removal paths using MaskAlternativePath.
4. Require JsonNode.DeepEquals with the immutable preservedProposal. This preserves
   all non-offending siblings and array positions.
5. Parse the complete corrected author and classify Complete/NeedsAnotherRepair/Rejected
   with the phase rules above. Complete requires GM complicationRef, every nested
   mechanic and hidden/visible diagnosis pairing. A still-present unknown field or
   still-missing required value keeps the current exact path invalid and is Rejected;
   unlike construction, invalid_field is NOT an unconditional omission instruction.

The boolean API is only this wrapper:

~~~csharp
internal bool MatchesCorrectedAlternativeTreatmentAuthoring(JsonObject corrected) =>
    AnalyzeAlternativeTreatmentCorrection(corrected).Status ==
        WoundAlternativeTreatmentCorrectionStatus.Complete;
~~~

Use one immutable private edit-rule model containing semantic Path and an enum
ReplaceOrRemove/AppendRequiredRouteFact. The append rule also carries the required
fact and a cloned original JsonArray; replacement rules carry no appended fact.
Store a detached sanitized unmasked baseline in the packet as well as its existing
masked preservedProposal, so no-progress checks and original append-prefix comparison
do not depend on public getter mutations. These are local validation data, never
serialized authority. Avoid a generic patch executor or JSON mutation language.

This method proves only a local correction relationship. It neither allocates an
operation key nor executes an alternative treatment or reconstructs private authority.

## Strict persisted transport and live admission

The public packet has exactly eleven fields with candidateKind immediately after
kind. Keep the receipt unchanged. Read the tag before branching issue path, preserved
payload and requiredResponseShape validation. Construction/narration retain the exact
existing path/description/template rules. Alternative requires a closed subset of
route/diagnosisPath in the preserved payload. An absent root requires its exact
whole-root issue and recipe; a non-offending null remains present as null.
Require the recipe/null rules above, identical ordered distinct correctOnly
paths for each member, exact safe authoring ref and decision author, and the exact
response string complete accepted scene. Reject cross-kind shapes, unknown fields,
wrong-case tags, null/non-string tags, missing tags and recursive duplicate properties.

For the append-fact diagnostic the original reveals array remains in preservedProposal
and its path remains in diagnosisPath.correctOnly. Require a string-array base and
that distinct generated code/description; never require that array to be absent as
for replacement masks. For every absent root require a whole-root issue; for each
recipe require the exact ordered distinct paths projected for that member. Null path
templates cannot hide diagnosisPath issues. Validate every issue's Actual and semantic
path with the public privacy markers, not just the preserved payload. Retain exact
closed safeContext and all existing root/receipt/duplicate checks. The public reader
must not try to reconstruct an executable full draft from a deliberately masked base.

Public transport validation does not register a live request. Add explicit checks:

~~~csharp
// ComposePendingRoot, after packet array validation and before recomposition:
if (packetArray.Any(packet => !IsSupportedLiveRepairPacketKind(packet.CandidateKind)))
    throw new InvalidOperationException(
        "wound_repair_alternative_adapter_unavailable: private alternative repair authority is required.");

// CaptureWoundRepairRetryObligationsAsync, before reading command JSON:
if (packets.Any(packet => !WoundRepairPacketBuilder
        .IsSupportedLiveRepairPacketKind(packet.CandidateKind)))
    return Array.Empty<WoundRepairRetryObligation>();

// HasExactWoundRepairResubmissionAsync, before reading command JSON:
if (obligations.Any(obligation => !WoundRepairPacketBuilder
        .IsSupportedLiveRepairPacketKind(obligation.Packet.CandidateKind)))
    return false;
~~~

In MortalWoundTreatmentPersistence.ParsePending, after strict public-wave validation
and before its early !hasSubmitted return, reject any alternative packet with
mortal_wound_treatment_persisted_pending_invalid and expected private alternative
repair authority. This applies even to a public-valid alternative wave with no
submitted treatment, and to a mixed wave. No partial submittedTreatmentRequests are
admitted. Keep the earlier accepted-transition-command gates intact.

The independent public resubmission obligation uses packet.ResubmissionRoute. Keep
BuildValidationRepairRequestInstructions's existing three-argument signature; its
text instructs the GM to select the correct recipe by candidateKind and explains
both actual nested recipe paths. Count/capture mismatch continues to reject the whole wave and restore its
snapshot before any actionable public repair request is dispatched.

Contextual rollback detection must recognize an alternative Error/context before
filtering by the repairable-code list. An alternative error whose sole code concerns
an unsupported root/ref/decision still requires fail-closed handling, not an empty
successful repair wave. Keep this detection scoped to the alternative section or an
explicit alternative WoundRepairContext; unrelated validation errors are not wound
repair candidates. Test the unsupported-only case without a supported sibling that
would accidentally trigger the old Any(IsRepairableIssue) check.

## Test vectors and execution sequence

The two frozen MortalWoundDiagnosisTests.Repair_AlternativeCandidate* methods stay
unchanged. First run those methods alone and record their genuine current RED.
Add a pure fixture from WoundContractTestData.CreateActiveWound(): copy its first
treatment route, set routeId=alternative_hidden_route and visibility=hidden, and
use a complete known_to_player diagnosis path revealing route:alternative_hidden_route.
Set only mode to unsupported_mode for the basic repair. Source issues come from the
real parser; the frozen hand-authored issue remains a separate compatibility regression.

The basic test must assert this whole sequence, not just packet presence:

~~~csharp
[Fact]
public void CorrectedAuthor_ReplacesRequiredModeAndPreservesSiblings()
{
    var (request, original) = CreateInvalidModeCandidate();
    var packet = Assert.Single(WoundRepairPacketBuilder.Build(request));
    Assert.Equal("author_alternative_treatment", packet.CandidateKind);
    Assert.Equal("woundTreatmentAuthorings", packet.ResubmissionRoute);
    Assert.Null(packet.ToJsonObject()["preservedProposal"]!["route"]!["mode"]);

    var corrected = original.DeepClone().AsObject();
    corrected["route"]!["mode"] = "procedure";
    Assert.True(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
    corrected["route"]!["displayName"] = "unrelated rewrite";
    Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
}
~~~

The helper uses actual parser diagnostics; its fingerprint is only the existing
public correlation-format fixture, never canonical accepted-world evidence:

~~~csharp
private static (WoundRepairBuildRequest Request, JsonObject Original)
    CreateInvalidModeCandidate()
{
    var route = WoundContractTestData.CreateActiveWound()["treatment"]!["routes"]![0]!
        .DeepClone().AsObject();
    route["routeId"] = "alternative_hidden_route";
    route["visibility"] = "hidden";
    route["mode"] = "unsupported_mode";
    var original = new JsonObject
    {
        ["authoringRequestRef"] = "authoring_request_public_001",
        ["decision"] = "author",
        ["route"] = route,
        ["diagnosisPath"] = new JsonObject
        {
            ["diagnosisPathId"] = "diagnosis_alternative_hidden",
            ["displayName"] = "Узнать другой способ лечения",
            ["visibility"] = "known_to_player",
            ["requiresKnownFacts"] = new JsonArray("route:clean_and_suture"),
            ["requirements"] = new JsonArray(),
            ["check"] = new JsonObject(),
            ["reveals"] = new JsonArray("route:alternative_hidden_route"),
            ["failurePolicy"] = "no_reveal"
        }
    };
    var parsed = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
        JsonSerializer.SerializeToElement(original), "woundTreatmentAuthorings[0]");
    Assert.False(parsed.IsValid);
    Assert.Contains(parsed.Issues, issue =>
        issue.FilePath == "woundTreatmentAuthorings[0].route.mode" &&
        issue.Code == "wound_materialization_invalid_field");
    var candidate = new WoundRepairCandidateInput(
        "author_alternative_treatment",
        "candidate_alternative_treatment_001",
        "sha256:" + new string('b', 64),
        "authoring_request_public_001",
        new JsonObject
        {
            ["event"] = "В найденных записях описан другой способ лечения",
            ["target"] = "игрок",
            ["realm"] = "Смертный мир"
        },
        new[] { "decline", "author" },
        "I",
        "IV",
        original,
        parsed.Issues);
    return (new WoundRepairBuildRequest(
        "session_wound_diagnosis",
        "request_wound_diagnosis",
        "snapshot_wound_diagnosis",
        new[] { candidate }), original);
}
~~~

Required separate tests:

- Eleven-field ordered serialization for all four kinds; keep construction semantics.
- Exact direct/contextual build parity for alternative errors and clone safety.
- Supplied wrong code/path/index/section, arbitrary Expected/Actual, root ref/decision
  and mismatch with the independent diagnostic all fail closed or ignore untrusted
  descriptions as specified. Never turn malformed decline into author.
- Valid decline/no issues returns no packet and no rollback requirement.
- Missing required field replaced; unknown field omitted; canonical complicationId
  cannot substitute for GM complicationRef after correction.
- Two offending nested array elements remain in their original positions; changing
  a third sibling, deleting/reordering an unrelated element or dropping a required
  field fails. Include one whole-array validation issue as a distinct case.
- Invalid mode plus difficulty, invalid mode plus hidden null path, and raw/sanitized
  malformed first entries followed by another bad requirement/outcome/fact use staged
  correction. Each intermediate result has no complete draft; preserve a third valid
  sibling, and obtain an eventual complete local draft after exact next-phase repairs.
- A missing diagnosis displayName cannot authorize changed ID, prerequisites,
  requirements or facts. gm_only path visibility changes only that field. Missing
  required route reveal appends exactly one fact and preserves the complete old prefix;
  replacing/reordering/removing an old fact or adding another fact fails.
- Hidden null/wrong path corrected; visible non-null path corrected to null;
  route/path pair mismatch still fails the complete parser.
- Every frozen private key/value plus value aliases in display text, safeContext,
  nested arrays and Actual is excluded. Corrected input cannot reintroduce them.
- The independent sanitizer's additional missing/unknown diagnostics are represented;
  the frozen nestedSecrets container does not make an otherwise valid correction
  impossible while silently retaining an invalid sibling.
- Strict persisted tag and member-template mutations, omitted/mismatched correctOnly,
  code/description/path mismatch, null holes, recursive duplicates, positional
  receipts, root identities and confusable candidate collisions.
- Actual ComposePendingRoot, ParsePending/cold catalog, capture and exact retry reject
  alternative-only and mixed waves. Assert no accepted-command fabrication, no
  actionable repair dispatch, and unchanged canonical/output bytes.
- Retain a real supported construction capture/retry positive control in the same
  owning Integration selection; a test suite that rejects every candidate is not green.

Use the existing PersistedRepairWaveValidator fixtures for transport controls.
WoundMaterializationRollbackTests.cs is a partial of WoundMaterializationLifecycleTests;
reflection helpers and file/lease checks belong in Integration, not Fast. Reuse its
actual snapshot/capture setup rather than introducing a looser test-only path.

## GM-facing synchronization and documentation verification

Update these documents in the same Task 2:

- OtherGuides/Wound_Materialization_Contract.md and Rules/Block_12.txt:
  four-kind tag, recipe paths, kind-specific narration and correction restrictions.
- Examples/E_Block_12.txt: add candidateKind=construct_wound to its sole
  clientrepairpacketexcerpt and keep the complete existing construction example.
- Examples/E_CLI_Wound_Materialization.txt: one complete hidden-route rejected author,
  builder-produced safe packet and legal corrected author, explicitly local-only.
- TaskGuides/CLI_Step_Main.txt, Examples/E_CLI_Step_Main.txt,
  CLI_API_Specification.md and CLI_Agent_Daemon_Specification.md: scoped wound
  discriminator/routing guidance, without modifying other repair families.
- BookOfEternityClient/game_master_daemon.ps1: keep opportunity creation scoped to
  woundDecisions; direct wound repair to its candidateKind-specific response shape.
- OtherGuides/Afterlife_Contract_Matrix.md and Examples/E_CLI_Afterlife_Turns.txt:
  the mandatory tag also applies to spiritual construction packets in both realms,
  but never enables Mortal alternative mechanics in afterlife.
- Examples/example_validation_manifest.json: add the executable local repair family,
  its complete source markers, production builder/matcher/persisted-validator routes
  and an honest no-live-authority coverage limit. Update the exact family dictionary
  and count in ExampleDocumentationValidationTests.cs.

Preserve effect/resource/Guardian repair contracts and their source guards. Add a
narrow afterlife wound-tag guard; do not spread candidateKind to unrelated packet kinds.

The owning plan supplies final exact Focused filters after Task 1 acceptance.
Required sequence is focused RED, implementation, owning pure/Integration GREEN,
AfterlifeDocumentationCoverageTests Focused, and one FullValidation control for this
documentation-sensitive shared transport change. Do not duplicate broad controls,
run lanes concurrently, or mark T070/T177/#1536 complete at this boundary.
