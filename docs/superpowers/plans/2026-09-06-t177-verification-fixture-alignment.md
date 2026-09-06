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

- [ ] **Step 1: Confirm the already-recorded RED against the exact task BASE.**

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

- [ ] **Step 2: Supply the required identity in the valid snapshot fixture.**

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

- [ ] **Step 3: Correct section-end discovery in the existing example reader.**

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

- [ ] **Step 4: Run one meaningful Fast and the required FullValidation control.**

  ```powershell
  pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
  pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
  git diff --check
  ```

  Expected: the two diagnosed failures disappear. Report actual full/partial status,
  owner membership, clean builds, timeout/cleanup and artifacts; never call a fail-fast
  partial lane complete. Preserve the known legacy-source failure if reached. No
  PreMerge, extra Fast rerun or unrelated diagnostic lane is warranted here.

- [ ] **Step 5: Inspect the scoped diff, commit and request independent review.**

  ```powershell
  git diff -- BookOfEternityClient.Tests/MortalWoundRequirementAuthorityTests.cs BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs
  git add -- BookOfEternityClient.Tests/MortalWoundRequirementAuthorityTests.cs BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs
  git commit -m "test(wounds): align verification fixtures with current contracts (#1536)"
  ```

  Leave parent-owned plans/Spec Kit bookkeeping unstaged. Return exact BASE/HEAD and
  a detailed report with all actual artifacts. A fresh reviewer checks both correctness
  and scope; parent inspects evidence and accepts before addition Task1 starts.
