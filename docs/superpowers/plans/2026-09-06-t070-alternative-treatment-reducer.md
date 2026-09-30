# T070 Append-Only Alternative Treatment Transition Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Accept one evidence-backed new Mortal treatment route, with exactly one new
diagnosis path when hidden, without rewriting any existing wound definition or history.

**Architecture:** The sole `MortalWoundTreatmentPlanner` factory derives complete local
route/path/state/result seals from parsed before/after wounds and validated external
authority references. A narrow reducer verifies the sealed request and exact append-only
delta, then emits one carrier replacement and one immutable history result. This is
the next pure layer after the diagnosis factory, not a new GM response/publication path.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** GitHub [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T059/T070; approved source contracts
are Spec Kit `plan.md` T067 production-owned sealing boundary and `data-model.md` sections
5, 9 Alternative treatment authoring authority, and accepted transition commands.
Dependency: independent approval of `2026-09-06-t070-diagnosis-reducer.md` before execution.

## Global Constraints

- Stay in `E:/Games/worktrees/boe-1536-wound-materialization`, existing
  `1536-complete-wound-materialization` branch. Preserve `.serena/` and unrelated edits.
- No migration, compatibility parser, dual write, or old-save fallback.
- `opportunity_decision` owns create/worsen; `accepted_transition` owns diagnosis and
  alternative authoring. Do not replace or erase either accepted command family.
- Mortal wounds have arbitrary GM-authored setting-specific consequences and treatment,
  not a predefined catalog of injuries or cures. Added routes must use the existing
  complete registered route constructor, not a new permissive fragment schema.
- Alternative authoring appends exactly one complete route, and for a hidden route
  exactly one new reachable diagnosis path. No existing route/path, order, completed
  facts, mechanics, display, or prior history may be replaced, reordered, or removed.
- The full appended route/path, including readable display names, must be sealed.
  The existing mechanics-only `MortalWoundTreatmentRouteFingerprint` is insufficient.
- Factories derive local seals and typed results. Tests use those factories and may
  tamper with returned requests; they do not create concrete evidence or hash recipes.
- One accepted authoring is a nonterminal history event, not a treatment/diagnosis
  attempt. It creates no terminal-attempt, effect, healing, recovery, or legacy intent.
- `CanonicalStateNormalizer` remains the sole publisher. This pure factory/reducer
  exposes no incomplete player command or GM-authored response capability.
- Fast stays physically isolated, deterministic, and bounded to five minutes. No game-state file,
  lease, restart, publication, or rollback tests in this slice.
- Use the bounded PowerShell 7 test runner; never overlap C# lanes. No broad Fast,
  Integration, FullValidation, or PreMerge is needed for this pure checkpoint.
- No push, PR, merge, issue closure, new branch, or session cleanup.

### Task 1: Sealed append-only alternative route and durable result

**Files:**
- Create: `BookOfEternityClient/Services/MortalWoundTreatmentPlanner.Alternative.cs`
- Create: `BookOfEternityClient/Services/WoundTransitionReducer.Alternative.cs`
- Modify: `BookOfEternityClient/Services/WoundTransitionReducer.cs` (exact kind
  registration, dispatch/seal integration, and result intent only)
- Modify: `BookOfEternityClient/Services/WoundMaterializationContract.cs` (register
  `author_alternative_treatment` in the closed last-transition kind set only)
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentContract.cs` (narrow
  diagnosis-path exact/confusable uniqueness check in `ValidateDiagnosis` only;
  dependency confirmed by the new `old_path` / `OLD_PATH` RED fixture).
- Optional narrow shared-helper reuse/extraction in
  `BookOfEternityClient/Services/MortalWoundTreatmentPlanner.Diagnosis.cs`: reuse the
  complete local path serializer/hash from the approved diagnosis task; no diagnosis
  semantics may change. If extracting, name and report the one cohesive helper file.
- Create: `BookOfEternityClient.Tests/MortalWoundAlternativeTransitionTests.cs`
- Read existing RED: `BookOfEternityClient.Tests/MortalWoundDiagnosisTests.cs`
- Read existing production history codec and canonical wound/treatment serialization.

Do not edit commands, response/repair, fresh world authority, publication, other
transition kinds, or parent plans/tasks/ledger. Any concrete dependency beyond this
ownership must be reported before editing. The external producer and future signed
snapshot proof are not inferred from the pure factory's checksum.

**Exact frozen API:** one production method with this name/arity:

```csharp
internal static WoundTransitionRequest CreateAlternativeTreatmentTransition(
    string transitionId,
    string authoringRequestRef,
    string requestAuthorityFingerprint,
    string operationKey,
    string eventRef,
    int turn,
    WoundMaterializationEnvelope before,
    WoundMaterializationEnvelope proposedAfter,
    string addedRouteId,
    string? addedDiagnosisPathId,
    string evidenceAuthorityFingerprint,
    string requirementAuthorityFingerprint);
```

It returns kind `author_alternative_treatment`; safe `authoringRequestRef` binds the
command/evidence authority, while operationKey remains the history coordinate.
Factories receive IDs that select exact appended members, not trusted local seals.
The typed durable result is the existing `WoundAlternativeTreatmentTransitionResult`:

```json
{"kind":"author_alternative_treatment","authoringRequestRef":"safe_ref","addedRouteId":"exact_route","addedDiagnosisPathId":null,"routeFingerprint":"sha256:...","diagnosisPathFingerprint":null,"resultFingerprint":"sha256:..."}
```

The production history codec owns `resultFingerprint`. Route/path fingerprints cover
their complete canonical wire content, preserving ordered arrays and explicit nulls,
excluding parser source-location metadata. Local result/evidence values must be
immutable/detached. No mode-specific convenience aliases or guessed old schema.

- [x] **Step 1: Observe the existing 27-row RED contract**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundDiagnosisTests.Reduce_AuthorAlternative_|FullyQualifiedName~MortalWoundDiagnosisTests.History_AlternativeAuthoringAppendsOnceAndReplaysExactResult|FullyQualifiedName~MortalWoundDiagnosisTests.History_AlternativeReplayComparesEveryTypedResultCoordinate"
```

Expected: 27 rows reach the unimplemented alternative kind/factory boundary and fail
with a clean build. Record the actual owning missing boundary, not an assumed error.
Do not rerun the entire still-incomplete diagnosis command/response class.

- [x] **Step 2: Add focused constructor, binding, and append RED cases**

Use central `WoundContractTestData.CreateActiveWound` plus small local helpers and the
production parser/factories, without copying the large existing diagnosis test class.
Keep its current 27 assertions unchanged. Cover:

- Public, known-to-player, and hidden route additions with complete setting-specific
  procedure, course, and guaranteed shapes supported by the existing route parser.
  Reuse valid shared route builders where available; do not handwave required fields.
- Exactly one carrier replacement plus one nonterminal history event with null attempt/
  tick coordinates and the full typed result; zero effect/heal/recovery/legacy/attempt
  intents. Canonical prior history rows remain byte-identical after append.
- Visible route appends its ID once at the end of known routes and adds no diagnosis
  path. Hidden route changes no known-route entry and appends exactly one complete path
  that reveals the exact new route through the existing player-reachable discovery graph.
- Existing route/path prefixes and completed/known arrays are preserved in exact order.
  Existing display names, requirements, outcomes, and path text cannot change.
- Hidden path cannot be `gm_only`, irrelevant to the new route, dependent only on itself,
  or unseeded. The complete after wound still satisfies the existing structural
  least-fixed-point contract; do not introduce a second weakened reachability algorithm.
- Missing/wrong-kind/null evidence, missing or nonexact selected route/path identity,
  visible/hidden path pairing mismatch, extra appended route/path, and confusable IDs
  reject. Parser-invalid objects need not be accepted by the factory to create a fixture.
- Post-factory mutation of transition/turn/operation/event/request reference, any of the
  three external authority seals, public before/after seals, selected route/path ID,
  full route/path text (including display name), or typed result rejects. Include an
  after-image with recomputed public after seal; sealed evidence must still reject it.
- Mutable caller wound/list/JSON inputs and result canonical output are detached.
- Factory-created, structurally valid but semantically illegal after-images reach
  reducer rejection rather than exceptions, silent normalization, or partial intents.

Run the new class RED before production implementation. No test-only evidence/hash
constructor, reflection into private evidence, or forged successful history is allowed.

- [x] **Step 3: Derive complete factory authority**

Clone parsed before/after inputs. Resolve the exact selected new route and optional
path from the proposed-after. Derive wound ID, complete before/after fingerprints,
full route/path fingerprints, typed result, and a versioned domain-separated authority
seal covering kind, transition/turn, request reference/seal, operation/event, wound ID,
local IDs/seals, evidence/requirement seals, and complete result.

The existing full-path helper from the preceding diagnosis task should be reused when
appropriate, not copied verbatim. Do not change the treatment mechanics hash contract.
Every external fingerprint must have the existing exact lowercase SHA-256 format.
The factory does not grant fresh world authority merely because those strings are
well-shaped; real authoring request/requirements are revalidated in the later producer.

Keep semantically illegal but structurally valid proposed-after inputs representable
as rejected reducer requests. In particular, a changed old route or known-route reorder
must not throw during factory creation or be silently repaired. Factory errors must not
open a path to partially accepted state.

- [x] **Step 4: Validate exact append delta and emit result**

Register the closed kind in reducer and canonical wound last-transition registries;
history already registers it. Reuse existing request/identity/turn/ordinal preconditions.
Require an active physical `mortal_world` wound with stable owner/provenance.

Independently validate the full evidence before consuming appended members. The eight
existing post-seal mutation rows must retain
`wound_transition_alternative_evidence_invalid`, including expected before/after changes
that would otherwise be preempted by generic evidence-seal checks. Integrate this as a
narrow kind-specific branch; unrelated transition diagnostics remain unchanged.

For a request created from a structurally valid semantic violation, preserve
`wound_transition_alternative_append_invalid`. Compare exact before prefixes and counts
of routes/paths and known/completed order; restore only the permitted treatment append
and last-transition metadata for whole-wound canonical comparison. This rejects every
unrelated mechanics, care, severity, recovery, effects, display, or complication change.
Hidden routes require their one new player-reachable path, and visible routes require
no new path. Structural parsing remains the owner of closed member shapes, fact bounds,
confusable identity checks, and graph validity.

Emit one carrier replacement and one history intent carrying the immutable result,
null attempt/tick IDs, and Terminal=false. Alternative authoring creates a new option;
it does not execute that option or terminalize a treatment attempt. Do not manufacture
effect IDs or a source batch. A decline remains a later response concern with no
transition, not a fake successful alternative event.

- [x] **Step 5: Run coherent GREEN controls and independent review**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundAlternativeTransitionTests|FullyQualifiedName~MortalWoundDiagnosisTests.Reduce_AuthorAlternative_|FullyQualifiedName~MortalWoundDiagnosisTests.History_AlternativeAuthoringAppendsOnceAndReplaysExactResult|FullyQualifiedName~MortalWoundDiagnosisTests.History_AlternativeReplayComparesEveryTypedResultCoordinate|FullyQualifiedName~MortalWoundDiagnosisTransitionTests|FullyQualifiedName~WoundTransitionReducerTests|FullyQualifiedName~WoundHistoryStateTests|FullyQualifiedName~WoundTransitionResultTests|FullyQualifiedName~WoundMaterializationContractTests"
```

Expected: complete owning selection GREEN, clean build, no timeout/duplicate ID and
complete cleanup. If the measured Focused selection genuinely needs more time, use
documented Focused headroom with evidence; Fast's five-minute bound is not changed.
No broad diagnostic lane merely to repeat these deterministic tests.

Self-review exact files and factory ownership, then request independent spec/quality
review. Commit only owned code/tests with
`feat(wounds): seal alternative treatment authoring (#1536)`. Report exact RED/GREEN
artifacts, all diagnostics, and remaining boundaries; parent updates plans/tasks/ledger.

This internal constructor/reducer publishes no new canonical state or GM capability.
The narrow parser dependency does reject ambiguous diagnosis path IDs in already
authored Mortal wounds, so Task 2 below synchronizes that existing constructor rule
in the GM guide and worked example before this plan is considered complete.
Closed accepted commands, kind-specific `woundTreatmentAuthorings`
response/repair, fresh source authority, common publication/cache/replay/rollback and
worked GM examples remain mandatory next. Keep T070/T177/#1536 open and retain the
known unrelated course/replay publication failures until their owning fix is verified.

### Task 2: Explain and demonstrate unambiguous diagnosis path identity

**Task 1 checkpoint:** `2f5ceda1..b45c5d2e`, independent spec/quality Approved,
0 Critical/Important/Minor. Parent inspected the complete production diff, report,
and actual final 484/484 summary/log
(`20260906-095307-279-16128-3dc013d240e742d39138a7d77dde5048-focused`, 0:31.738),
clean build/cleanup, no timeout/duplicates/skips. All 27 frozen cases and 74 new rows
pass; existing history/result/reducer/materialization controls remain green. The
history-evidence review caveat is resolved by those artifacts and the reviewed history
foundation. This does not close the following GM synchronization or future authority/
command/publication responsibilities. T070/T177/#1536 remain open.

**Files:**
- Modify: `OtherGuides/Wound_Materialization_Contract.md`.
- Modify: `Examples/E_CLI_Wound_Materialization.txt` (only the existing complete
  `wound_mortal_roll_scope_skill_v1` worked example and its immediate explanatory text).
- Modify: `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs`.
- Parent synchronizes `specs/1536-complete-wound-materialization/data-model.md` and
  evidence/tasks; do not edit parent records or the manifest in this task.

**Interface:** The existing Mortal constructor accepts complete `diagnosisPaths[]`.
Within one wound, their `diagnosisPathId` values must be exact and case/Unicode-
confusable unique; selection stays exact, never fuzzy or by display name. This is
not a new diagnosis command or a successful diagnosis result. The matching route
in the worked example is already `ashglass_clean_and_bind`.

- [x] **Step 1: Add the documentation/example RED guard**

Add one deterministic source/documentation test in the existing Wounds partial that
checks the guide marker `wound_mortal_diagnosis_identity_v1`, its explicit exact and
case/Unicode-confusable uniqueness rule, and that the named complete existing example
contains both `inspect_ashglass_cuts` and `assess_ashglass_tendon` as distinct paths.
Use existing example extraction helpers, not a whole-file presence-only substitute
for checking the two paths belong to the exact example. Assert the complete path
objects, their unchanged route reference and failure policy. Execute:

```csharp
[Fact]
public void WoundDiagnosisIdentityDocumentation_UsesDistinctCompleteWorkedPaths()
{
    var guide = ExtractWoundDocumentationSection(
        ReadRepoFile("OtherGuides", "Wound_Materialization_Contract.md"),
        "wound_mortal_diagnosis_identity_v1");
    Assert.Contains("case/Unicode-confusable unique", guide, StringComparison.Ordinal);
    Assert.Contains("never fuzzy or display-name matching", guide, StringComparison.Ordinal);
    var section = ExtractWoundDocumentationSection(
        ReadRepoFile("Examples", "E_CLI_Wound_Materialization.txt"),
        "wound_mortal_roll_scope_skill_v1");
    var roots = Regex.Matches(section, @"```json\s*(?<json>.*?)```",
            RegexOptions.Singleline | RegexOptions.CultureInvariant)
        .Select(match => Assert.IsType<JsonObject>(JsonNode.Parse(match.Groups["json"].Value)))
        .ToArray();
    var response = Assert.Single(roots, root => root.ContainsKey("woundDecisions"));
    var decision = Assert.Single(response["woundDecisions"]!.AsArray());
    var paths = decision!["proposal"]!["treatment"]!["diagnosisPaths"]!.AsArray();
    Assert.Equal(2, paths.Count);
    var expected = new[]
    {
        (Id: "inspect_ashglass_cuts", Name: "Осмотреть края порезов"),
        (Id: "assess_ashglass_tendon", Name: "Проверить подвижность пальцев")
    };
    for (var index = 0; index < expected.Length; index++)
    {
        var value = new JsonObject
        {
            ["diagnosisPathId"] = expected[index].Id,
            ["displayName"] = expected[index].Name,
            ["visibility"] = "known_to_player",
            ["requiresKnownFacts"] = new JsonArray(),
            ["requirements"] = new JsonArray(),
            ["check"] = new JsonObject(),
            ["reveals"] = new JsonArray("route:ashglass_clean_and_bind"),
            ["failurePolicy"] = "no_reveal"
        };
        Assert.True(JsonNode.DeepEquals(value, paths[index]), value.ToJsonString());
    }
}
```

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests.WoundDiagnosisIdentityDocumentation"
```

Expected: new guard fails because marker and paths are absent; clean build.

- [x] **Step 2: Synchronize the guide and existing worked example**

Add the guide section `## wound_mortal_diagnosis_identity_v1` with this rule:

> Within one Mortal wound, `diagnosisPathId` values must be exact and case/Unicode-
> confusable unique. Choose a genuinely distinct ID for each diagnosis path; changing
> only capitalization or using visually confusable characters is invalid. Selection
> uses the exact ID, never fuzzy or display-name matching. This identifies possible
> examinations; it does not assert that an examination succeeded or grant treatment.

In `wound_mortal_roll_scope_skill_v1` only, replace its empty diagnosis array with:

```json
"diagnosisPaths": [
  {
    "diagnosisPathId": "inspect_ashglass_cuts",
    "displayName": "Осмотреть края порезов",
    "visibility": "known_to_player",
    "requiresKnownFacts": [],
    "requirements": [],
    "check": {},
    "reveals": ["route:ashglass_clean_and_bind"],
    "failurePolicy": "no_reveal"
  },
  {
    "diagnosisPathId": "assess_ashglass_tendon",
    "displayName": "Проверить подвижность пальцев",
    "visibility": "known_to_player",
    "requiresKnownFacts": [],
    "requirements": [],
    "check": {},
    "reveals": ["route:ashglass_clean_and_bind"],
    "failurePolicy": "no_reveal"
  }
]
```

Explain beside the example that `INSPECT_ASHGLASS_CUTS` cannot be a second path ID
next to `inspect_ashglass_cuts`; use the genuinely distinct second ID shown. Keep
the existing complete example ID, selected skill, consequence, route mechanics and
manifest registration unchanged. Both examination descriptions are setting-specific
examples, not a universal wound or diagnosis catalog.

- [x] **Step 3: Verify guide and complete worked constructor**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests.WoundDiagnosisIdentityDocumentation|FullyQualifiedName~PromptDocumentationCoverageTests.WoundRollScopeDocumentation|FullyQualifiedName~PromptDocumentationCoverageTests.WoundMaterializationContract"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests.MortalWoundRollScopeWorkedExamples_ComposeAndBindExactSkill|FullyQualifiedName~ExampleDocumentationValidationTests.CompleteEffectMaterializationManifest_CoversEveryRequiredWorkedFamily"
```

The complete example must still validate, not only its source strings. The exact
selection above covers the existing full worked constructors and manifest registration;
record the actual filter and row count.
No afterlife parser/schema, guide, matrix, manifest shape, prompt entrypoint or daemon
changes: their existing shared guide routing remains valid. No extra FullValidation
is required solely for this Mortal-only documentation clarification. Report clean
RED/GREEN evidence and commit only owned files:
`docs(wounds): clarify exact diagnosis path identities (#1536)`.

**Task 2 checkpoint:** `59e9d74a..a6ff984c`, independently Approved,
0 Critical/Important/Minor. Parent inspected the full three-file diff and actual
3/3 documentation (`20260906-100529-701-16084-ebab65abdfae43298564bbf91d577f8f-focused`)
and 3/3 complete example/manifest (`20260906-100547-999-28716-5ac981642eab490fbad70aa85e65aad6-focused`)
summaries/logs: clean build/cleanup, no timeout/duplicates/skips. The initial missing
marker RED and intermediate line-wrap RED are retained in the report; assertions were
not weakened. The guide, worked complete Mortal constructor and existing manifest
route are synchronized. No afterlife contract or daemon routing changed. This bounded
plan is complete; T070/T177/#1536 and fresh command/publication work remain open.
