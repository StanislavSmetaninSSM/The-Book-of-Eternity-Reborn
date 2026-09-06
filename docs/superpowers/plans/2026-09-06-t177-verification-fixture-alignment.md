# T177 Verification Fixture Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair the two source-confirmed stale test inputs/harness assumptions
exposed by the selective-removal checkpoint, without changing gameplay or validators.

**Architecture:** Supply the existing required skill identity in the common valid
snapshot fixture; bound a named Markdown example at the next same-or-higher-level
heading while retaining nested lower-level sections. Existing failing owners are the
regressions. No production behavior, example payload or assertion is weakened.

**Tech Stack:** C#/.NET 8, xUnit, System.Text.RegularExpressions, PowerShell 7.

**Tracking:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T177, the approved same-branch
test-alignment checkpoint. This is not completion of T070/T177 or the full feature.

## Global Constraints

- Use `E:/Games/worktrees/boe-1536-wound-materialization` and the existing
  `1536-complete-wound-materialization` branch. No remote action or `.serena` access.
- Dispatch only after selective-removal task review/parent acceptance, with the
  resulting committed HEAD as exact BASE. One implementer and one bounded C# lane.
- Change only the two test-owner files below. No production schema/parser change,
  fixture bypass, history fabrication, weakened/deleted/skipped assertion or example
  payload change. Keep default lane timeouts, scheduling and project membership.
- The required unfinished legacy-source RED remains explicit. Any newly exposed
  unrelated failure requires diagnosis and parent scope review, not an improvised fix.
- Mortal/afterlife prompts, examples and manifests need no gameplay-contract update:
  both corrections are test-only and preserve the existing authored contracts.
  FullValidation is nevertheless required because its shared example reader changes.

### Task 1: Restore valid snapshot and named-example fixture boundaries

**Files:**

- Modify `BookOfEternityClient.Tests/MortalWoundRequirementAuthorityTests.cs`:
  shared valid snapshot's actor skill near2147, using the existing `SkillRef` constant.
- Modify `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`:
  only section-end discovery inside `ParseNamedJsonFences` near3939.

**Interfaces:**

- Preserve the private fixture APIs and the exact
  `ParseNamedJsonFences(string file, string contractId)` return shape/signature.
- `MortalWoundTreatmentAuthority.SkillFields` and `ParseSkills` already require
  `skillId`; production is unchanged. `SkillRef` is the fixture's exact skill identity.
- Existing `### contractId`/alternate `## contractId` lookup, JSON fence parsing,
  error assertions and returned object sequence remain unchanged. For a level3
  section, levels1–3 end it; for level2, levels1–2 end it. Deeper headings remain
  inside their owning section. Both LF and CRLF line starts remain supported.

- [x] **Step 1: Confirm the already-recorded RED against the exact task BASE.**

  Parent and removal implementer already observed and source-audited these failures:

  - `TestResults/test-lanes/20260906-205232-674-38928-bc049757df254d988b738319956748e1-fast`:
    actual5TRX4211 rows,4110pass/101fail; all failures are missing
    `treatmentSnapshot.actors[0|1].skills[0].skillId` before requirement resolution.
    Both test/production owner files are unchanged from removal BASE51c97c24.
  - `TestResults/test-lanes/20260906-205503-004-40236-b759f856cd2d4d7bb0ae319354773d70-fullvalidation`:
    actual300 rows,299pass/1fail; unchanged
    `AlternativeTreatmentRepairWorkedExamples_ExecuteRealProjectionAndStrictTransport`
    finds two JSON fences instead of one at line37. The original reader includes
    the following level2 scalar-course section when starting at a level3 corrected
    alternative example. Parent independently reproduced exactly two fences on
    both51c97c24 and the current document. The helper and failing test body are unchanged.

  Inspect actual summaries/TRX and the named source lines; do not rerun unchanged
  broad lanes merely to reproduce these known REDs. If task BASE changes either
  relevant input, rerun the corresponding smallest owner before editing instead.

- [x] **Step 2: Supply the required identity in the valid snapshot fixture.**

  Add only the new line shown below; leave capability matching and other fields intact:

  ```csharp
  ["skills"] = new JsonArray(new JsonObject
  {
      ["skillId"] = SkillRef,
      ["capabilityRef"] = SkillRef,
      ["displayName"] = DisplayName("skill_tier"),
      ["tier"] = 3,
      ["lifecycle"] = "active",
      ["active"] = true
  }),
  ```

  Run the complete affected pure owner:

  ```powershell
  pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundRequirementAuthorityTests"
  ```

  Expected: all selected rows pass, including valid typed projection, exact requirement
  resolution and existing malformed/unknown-member negative tests. If another failure
  emerges, inspect its actual cause before proposing any additional edit.

- [x] **Step 3: Correct section-end discovery in the existing example reader.**

  Replace only the current exact-same-heading `IndexOf`/fallback block with:

  ```csharp
  var headingLevel = heading.StartsWith("## ", StringComparison.Ordinal) ? 2 : 3;
  var nextHeading = new Regex(
      $@"^#{{1,{headingLevel}}}[ \t]+\S",
      RegexOptions.Multiline | RegexOptions.CultureInvariant)
      .Match(source, headingIndex + heading.Length);
  var sectionEnd = nextHeading.Success ? nextHeading.Index : source.Length;
  ```

  The existing corrected-alternative fixture already proves the level3→level2
  regression; retain its `Assert.Single` and complete real pipeline checks unchanged.
  No source-string parser extraction or separate Markdown subsystem is needed.

  ```powershell
  pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests.AlternativeTreatmentRepairWorkedExamples_ExecuteRealProjectionAndStrictTransport"
  ```

  Expected: one real worked-example owner passes, with exactly the intended JSON
  fence and all existing projection/strict transport assertions still executed.

- [x] **Step 4: Run one meaningful Fast and the required FullValidation control.**

  ```powershell
  pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
  pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
  git diff --check
  ```

  Expected: the two diagnosed failures disappear. Report actual full/partial status,
  owner membership, clean builds, timeout/cleanup and artifacts; never call a fail-fast
  partial lane complete. Preserve the known legacy-source failure if reached. No
  PreMerge, extra Fast rerun or unrelated diagnostic lane is warranted here.

- [x] **Step 5: Inspect the scoped diff, commit and request independent review.**

  ```powershell
  git diff -- BookOfEternityClient.Tests/MortalWoundRequirementAuthorityTests.cs BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs
  git add -- BookOfEternityClient.Tests/MortalWoundRequirementAuthorityTests.cs BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs
  git commit -m "test(wounds): align verification fixtures with current contracts (#1536)"
  ```

  Leave parent-owned plans/Spec Kit bookkeeping unstaged. Return exact BASE/HEAD and
  a detailed report with all actual artifacts. A fresh reviewer checks both correctness
  and scope; parent inspects evidence and accepts before addition Task1 starts.

**Task1 acceptance (2026-09-06):** `b4f177c9..c7301340`, independently Spec
compliant / Task quality Approved, no findings. Parent inspected both changed files,
the complete review and all four actual artifact sets. Focused example1/1 and full
FullValidation1856/1856 (11 TRX,9:18.5424317) pass. Focused requirement owner175/235
and incomplete Fast4168/4211 remain RED: every60/43 failure is the same unchanged
`ReadResolvedRequirement` exact-member expectation omitting production `SkillId`.
Parent confirmed the full production owner is unchanged since51c97c24 and audited
all failed rows, clean builds, cleanup, timeouts and summaries. No broader success
is claimed. Parent-approved Task2 below completes this narrowly related test adapter;
T070/T177/#1536 remain open and the legacy choice remains unanswered.

### Task 2: Align the exact resolved-skill test projection

**Files:** Modify only `BookOfEternityClient.Tests/MortalWoundRequirementAuthorityTests.cs`.

**Interfaces:** Preserve exact reflection member equality and existing resolver
assertions. The test-only `ResolvedRequirementView` receives the existing nullable
`SkillId` after `AuthorityRef`, matching production. This is not a schema change.
No production, guide/example/manifest, Integration or lane inventory/default edit.
No FullValidation rerun: Task1's shared example-reader evidence remains applicable.
Use the parent acceptance commit as exact BASE; one implementer/C# lane.

- [x] **Step 1: Confirm the existing RED and exact production ownership.**

  Reuse actual Focused235/175/60 from
  `20260906-212239-382-30672-72db5e6c90374644b36906001d38ed8b-focused` and
  Fast4211/4168/43 from
  `20260906-212534-653-34180-17468f66dc0540ae81fea581afaeab34-fast` after confirming
  no change to this cause at the task BASE. All failures are collection-shape
  equality at `ReadResolvedRequirement:1697`, not gameplay failures. Production
  `MortalWoundResolvedRequirement:3040` includes nullable SkillId; `ResolveSkill`
  passes the exact snapshot skill identity and fingerprints it (`:854,:868`).
  No duplicate broad RED run is needed.

- [x] **Step 2: Update the strict test adapter and assert the coordinate.**

  Make these exact edits, retaining every existing member and assertion:

  1. In `ReadResolvedRequirement`'s sorted expected array, insert `"SkillId"`
     between `"RequirementIndex"` and `"TargetId"`.
  2. In the constructor call immediately after reading `AuthorityRef`, insert
     `ReadNullableStringProperty(value, "SkillId"),`.
  3. In `ResolvedRequirementView` immediately after `string AuthorityRef,`, insert
     `string? SkillId,`.
  4. In `AssertResolvedCoordinates` after the existing kind/authority checks, add
     `Assert.Equal(kind == "skill_tier" ? SkillRef : null, actual.SkillId);`.
     Existing all-kind success theories now assert exact skill identity and null
     for every other kind, rather than merely permitting another reflected member.

  Add this focused regression beside the existing independent-requirement test;
  it also proves capability matching never substitutes for the canonical skill ID:

  ```csharp
  [Fact]
  public void Resolve_SkillIdentityIsDistinctFromCapabilityAndFingerprintBound()
  {
      var route = CreateRoute(CreateRequirement("skill_tier"));
      var snapshot = CreateSnapshot();
      var original = Resolve(route, CreateContext(), snapshot);
      Assert.True(original.Success, DescribeIssues(original.Issues));
      var originalRow = Assert.Single(original.ResolvedRequirements);
      Assert.Equal(SkillRef, originalRow.SkillId);

      const string changedSkillId = "skill_field_medicine_canonical_002";
      First(Provider(snapshot), "skills")["skillId"] = changedSkillId;
      var changed = Resolve(route, CreateContext(), snapshot);
      Assert.True(changed.Success, DescribeIssues(changed.Issues));
      var changedRow = Assert.Single(changed.ResolvedRequirements);
      Assert.Equal(SkillRef, changedRow.AuthorityRef);
      Assert.Equal(changedSkillId, changedRow.SkillId);
      Assert.NotEqual(originalRow.AuthorityFingerprint, changedRow.AuthorityFingerprint);
      Assert.NotEqual(original.AuthorityFingerprint, changed.AuthorityFingerprint);
  }
  ```

- [x] **Step 3: Verify the owner, then one meaningful Fast checkpoint.**

  ```powershell
  pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundRequirementAuthorityTests"
  pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
  git diff --check
  ```

  Expected: the complete selected requirement owner is green, including the new
  exact identity regression and preserved negatives. Fast may reach the separately
  unfinished legacy-source RED; retain it and report actual fail-fast membership.
  If any other failure emerges, inspect the actual cause and send parent the
  evidence before expanding implementation; do not weaken assertions or guess.

- [x] **Step 4: Commit this one file and obtain independent review.**

  ```powershell
  git add -- BookOfEternityClient.Tests/MortalWoundRequirementAuthorityTests.cs
  git commit -m "test(wounds): assert exact resolved skill identity (#1536)"
  ```

  Return exact BASE/HEAD, complete actual summary/TRX/build/timeout/cleanup evidence,
  and test-only no-update rationale. Do not stage parent plans or Spec Kit progress.
  Parent reviews diff/artifacts and obtains fresh independent Spec+Quality review
  before accepting or dispatching addition Task1. No remote actions.

**Task2 acceptance (2026-09-06):** `580f682e..9f111e7b`, independently Spec
compliant / Task quality Approved, no findings. Parent inspected the complete
one-file diff, unchanged production owner, review and both actual artifact sets.
Focused236/236 (1:14.6932982) and both Fast requirement shards118+118 pass including
the new exact identity row; builds0/0, cleanup complete, no timeout/duplicate IDs.
Fast5627/5628 (2:20.2634328,7 TRX) is still incomplete, failing only unchanged
`AgentConsoleLiveInputSourceTests.EnqueueLine_WhenReadKeyIsPending_RejectsWithoutPoisoningQueue`
at596: its Task.Run registration wait exceeds1second. Parent ran the single named
diagnostic, artifact `20260906-220022-060-39060-042e692049064345a78fd0a31c50ed7f-focused`:
1/1 green, test41ms, lane1:07.9744426, clean build/cleanup, no timeout/duplicates.
This suggests load-sensitive scheduling, not a proven fix or a full Fast pass.
Keep that residual for T177/final lane triage; do not derail the wound graph task
with a speculative console refactor. No console code/test change or FV rerun made.
The required legacy source remains unfinished/unreached and T070/T177/#1536 open.
Both bounded fixture tasks are accepted; proceed to addition Task1 after committing
the parent plan/Spec Kit bookkeeping and recording its exact BASE.
