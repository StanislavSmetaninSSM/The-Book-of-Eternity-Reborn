# Spiritual Wound Opportunity Math Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the independently testable, non-authoritative arithmetic core of #1536 T076/T084 without exposing an unfinished spiritual-wound capability.

**Architecture:** An internal, allocation-bounded pure calculator takes immutable value inputs and returns the approved pressure, exact caps, and calculated maximum. It never reads canonical files, accepts an exchange, emits an opportunity, spends OD, rolls dice, changes a wound, or decides for the GM. A subsequent accepted-conflict adapter must independently obtain these inputs from existing authority; this result is not that authority.

**Tech Stack:** C#/.NET 8, nullable enabled, xUnit 2.9.2, PowerShell 7 and the repository bounded test runner; no new dependency.

## Global Constraints

- Tracked source: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), `specs/1536-complete-wound-materialization/spec.md`, `plan.md`, and unchecked T076/T084 in `tasks.md`.
- Governance: `.specify/memory/constitution.md` and `AGENTS.md`; remain in `1536-complete-wound-materialization`, the existing approved worktree, with no migration or remote operation.
- Exact FR-031: Ordinary spiritual wound eligibility MUST occur only on an accepted harmful strain transition and MUST reuse the accepted exchange evidence without a second injury roll.
- Exact FR-033: Natural 1 and natural 20 in the conflict exchange MUST NOT independently raise spiritual wound severity.
- Exact FR-041: Spiritual Resilience MUST passively affect the maximum spiritual injury calculation and MUST NOT consume OD.
- Training has cap 0, controlled 2, hostile/annihilation 4; destination caps are clear 0, strained 1, fractured 2, overwhelmed 3, broken 4. Arts are tier 0-V; source severity cap is 0-IV.
- Use the raw harmful margin, including negative values; do not normalize critical display outcomes or add a critical-roll parameter to arithmetic. Calculated data is not accepted-event evidence.
- No HP, mandatory wound, automatic dissipation, ready-made wound catalog, full afterlife inventory, or post-battle fatigue is introduced.
- Only the active implementer owns C# verification; do not overlap another task's build or tests. `Fast` stays at five minutes. Never run an unbounded solution test.
- Preserve the existing required legacy-source RED; do not skip it, change its assertion, or treat an incomplete Fast as fully GREEN.
- Do not mark full T076/T084 complete: actual harmful-margin provenance, passive art registration, mode/escalation acceptance, per-side/re-trauma seals, and opportunity export remain their explicit outstanding obligations.

## Parent source-backed design decisions

The approved formula and exact caps are in
`specs/1536-complete-wound-materialization/contracts/afterlife-spiritual-wounds-and-healing.md`,
research R-009, data-model's accepted strain audit, and spec FR-029 through FR-033.
Existing strain vocabulary is `AfterlifeSpiritualConflictState.StrainStates`;
the current private validator `TryGetStrainRank` confirms the same ranks.
Neither existing conflict registry nor validator is changed by this isolated task.

For a transition from rank p to rank n, extra jumps are the steps beyond the first:
`Math.Max(0, n - p - 1)`. This is a parent implementation interpretation of
"extra strain jumps", not a new caller-authored input or a claim that an accepted
adapter already exists. A non-increasing transition, including broken -> broken,
has no ordinary opportunity even when the diagnostic pressure is high. It still
returns the same auditable formula and caps with maximum 0. Explicit older-wound
re-trauma authority remains outside this arithmetic contract.

Input strain/mode strings must already be exact canonical lowercase tokens. This
internal method does not alter public parser normalization. Unknown/null tokens
and tiers/caps outside their domains return null, never a permissive fallback.
Use checked long margin/pressure arithmetic. The current exchange stores a
player-relative int margin; a future harmed-side conversion may negate int.MinValue
and must be able to represent +2147483648 without wrapping. This calculator does
not perform or authenticate that conversion. An unrepresentable long pressure
returns null; an ordinary negative representable margin must not be clamped.
Combine the bounded tier/strain/jump adjustment before adding it to the long
margin, so cancellation between those small terms cannot create a spurious
intermediate overflow when the final pressure is representable.
All output fields are immutable, caller-recomputable diagnostics, not secrets or
authority tokens. No request ID, fingerprint, actor, wound ID, natural die, OD,
timestamp, filesystem, JSON mutation, or publish method belongs in this API.

## File map

- Create `BookOfEternityClient/Services/SpiritualWoundOpportunityMath.cs`: internal value input/result and exact pure calculation.
- Create `BookOfEternityClient.Tests/SpiritualWoundOpportunityTests.cs`: fast deterministic math and domain tests only; no production fixture, temporary root, engine, or integration loop.
- Update this plan's checkboxes and link the accepted subtask evidence under T076/T084 in `specs/1536-complete-wound-materialization/tasks.md` without checking those full tasks.
- No production change to the still-absent `SpiritualWoundOpportunityAdapter.cs`, profiles, conflict state, validators, pending/control files, source registries, reducer, or publisher.

## Documentation boundary

This is intentionally client-owned unused arithmetic, not a new runtime capability
or GM-authored contract. Existing approved specification already contains the exact
formula. Mortal and afterlife GM prompts, matrix, examples, manifest, daemon entry
points, and player commands therefore require no change in this subtask, and
`FullValidation` is not justified by an example/documentation boundary change here.
Record this rationale in the task report. The accepted adapter must update those
surfaces when it actually exposes spiritual-wound opportunities. This task does not
claim an actual healing art or finished spiritual wounds.

---

### Task 1: Exact immutable opportunity arithmetic and domain gates

**Files:** the two new files and evidence-only task/plan updates in the file map.

**Interfaces:**
- Consumes: immutable `SpiritualWoundCalculationInput` defined below; no accepted-state service.
- Produces: `internal static SpiritualWoundCalculation? SpiritualWoundOpportunityMath.Calculate(SpiritualWoundCalculationInput input)` and the exact immutable records below. Null means invalid input domain, not an ordinary valid no-wound result.
- A valid result with `MaximumSeverityRank == 0` is ordinary no-wound arithmetic, never an error or consumed opportunity. Valid maximum >=1 permits nothing by itself; the real adapter still authenticates eligibility and the GM still chooses.

- [x] **Step 1: Add the compilable API shell and behavioral formula tests.**

Create the production file with these definitions. The initial shell is deliberately
not a calculator; it provides a semantic RED rather than an unrelated compile error.
Do not add the implementation or domain gates until their tests have run.

```csharp
namespace BookOfEternityClient.Services;

internal readonly record struct SpiritualWoundCalculationInput(
    long HarmfulMargin,
    int AppliedArtTier,
    int TargetResilienceTier,
    string? PreviousStrain,
    string? NewStrain,
    string? DangerMode,
    int SourceSeverityCap);

// A recomputable value result, never evidence authorizing a wound transition.
internal sealed record SpiritualWoundCalculation(
    SpiritualWoundCalculationInput Input,
    int PreviousStrainRank,
    int NewStrainRank,
    int ExtraJumpSteps,
    long TraumaPressure,
    int FormulaSeverityRank,
    int DestinationSeverityCap,
    int DangerModeSeverityCap,
    bool HasIncreasingStrain,
    int MaximumSeverityRank);

internal static class SpiritualWoundOpportunityMath
{
    internal static SpiritualWoundCalculation? Calculate(SpiritualWoundCalculationInput input)
        => null;
}
```

Create the test file with complete value assertions, not method-existence assertions:

```csharp
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualWoundOpportunityTests
{
    private static SpiritualWoundCalculationInput Input(
        long margin = 0, int art = 0, int resilience = 0,
        string? before = "clear", string? after = "strained",
        string? mode = "hostile", int sourceCap = 4)
        => new(margin, art, resilience, before, after, mode, sourceCap);

    [Theory]
    [InlineData(7, 0)]
    [InlineData(8, 1)]
    [InlineData(12, 1)]
    [InlineData(13, 2)]
    [InlineData(17, 2)]
    [InlineData(18, 3)]
    [InlineData(22, 3)]
    [InlineData(23, 4)]
    public void PressureThresholds_UseEveryExactBoundary(int pressure, int expected)
    {
        var input = Input(margin: pressure - 6, before: "overwhelmed", after: "broken");
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(input));
        Assert.Equal(input, actual.Input);
        Assert.Equal((long)pressure, actual.TraumaPressure);
        Assert.Equal(expected, actual.FormulaSeverityRank);
        Assert.Equal(expected, actual.MaximumSeverityRank);
        Assert.Equal(3, actual.PreviousStrainRank);
        Assert.Equal(4, actual.NewStrainRank);
        Assert.Equal(0, actual.ExtraJumpSteps);
        Assert.True(actual.HasIncreasingStrain);
    }

    [Theory]
    [InlineData("clear", "clear", 0, 0, 0)]
    [InlineData("clear", "strained", 1, 0, 1)]
    [InlineData("strained", "fractured", 2, 0, 2)]
    [InlineData("fractured", "overwhelmed", 3, 0, 3)]
    [InlineData("overwhelmed", "broken", 4, 0, 4)]
    [InlineData("clear", "fractured", 2, 1, 2)]
    [InlineData("clear", "overwhelmed", 3, 2, 3)]
    [InlineData("clear", "broken", 4, 3, 4)]
    [InlineData("strained", "broken", 4, 2, 4)]
    [InlineData("broken", "broken", 4, 0, 0)]
    [InlineData("broken", "strained", 1, 0, 0)]
    public void ExactDestinationAndExtraJumps_RequireIncreasingStrain(
        string before, string after, int rank, int jumps, int maximum)
    {
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: 30, before: before, after: after)));
        Assert.Equal(rank, actual.NewStrainRank);
        Assert.Equal(rank, actual.DestinationSeverityCap);
        Assert.Equal(jumps, actual.ExtraJumpSteps);
        Assert.Equal(30L + 2 * (rank - 1) + 3 * jumps, actual.TraumaPressure);
        Assert.Equal(maximum, actual.MaximumSeverityRank);
        Assert.Equal(maximum > 0, actual.HasIncreasingStrain);
    }

    [Theory]
    [InlineData("training", 0)]
    [InlineData("controlled", 2)]
    [InlineData("hostile", 4)]
    [InlineData("annihilation", 4)]
    public void DangerModes_OnlyCapAndNeverForceWounds(string mode, int cap)
    {
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: 30, before: "overwhelmed", after: "broken", mode: mode)));
        Assert.Equal(cap, actual.DangerModeSeverityCap);
        Assert.Equal(cap, actual.MaximumSeverityRank);
        Assert.True(actual.HasIncreasingStrain);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SourceCap_IsNeverBypassed(int cap)
    {
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: 30, before: "overwhelmed", after: "broken", sourceCap: cap)));
        Assert.Equal(cap, actual.MaximumSeverityRank);
        Assert.Equal(cap, actual.Input.SourceSeverityCap);
    }

    [Theory]
    [InlineData(0, 0, 10)]
    [InlineData(1, 0, 12)]
    [InlineData(2, 0, 14)]
    [InlineData(3, 0, 16)]
    [InlineData(4, 0, 18)]
    [InlineData(0, 1, 8)]
    [InlineData(0, 2, 6)]
    [InlineData(0, 3, 4)]
    [InlineData(0, 4, 2)]
    [InlineData(5, 0, 20)]
    [InlineData(0, 5, 0)]
    [InlineData(5, 5, 10)]
    public void ArtAndPassiveResilience_UseExactSignedTierDifference(int art, int resilience, int pressure)
    {
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: 10, art: art, resilience: resilience)));
        Assert.Equal((long)pressure, actual.TraumaPressure);
        Assert.Equal(art, actual.Input.AppliedArtTier);
        Assert.Equal(resilience, actual.Input.TargetResilienceTier);
    }

    [Fact]
    public void RawNegativeMargin_IsNotNormalizedOrClamped()
    {
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: -7, art: 5, before: "clear", after: "broken")));
        Assert.Equal(-7L, actual.Input.HarmfulMargin);
        Assert.Equal(18L, actual.TraumaPressure);
        Assert.Equal(3, actual.MaximumSeverityRank);
    }

    [Fact]
    public void ExtremeIntMargins_DoNotWrapPressure()
    {
        var high = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: int.MaxValue, art: 5, after: "broken")));
        var low = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: int.MinValue, resilience: 5)));
        var opposite = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: -(long)int.MinValue, art: 5, after: "broken")));
        Assert.Equal((long)int.MaxValue + 25, high.TraumaPressure);
        Assert.Equal(4, high.MaximumSeverityRank);
        Assert.Equal((long)int.MinValue - 10, low.TraumaPressure);
        Assert.Equal(0, low.MaximumSeverityRank);
        Assert.Equal(2147483648L, opposite.Input.HarmfulMargin);
        Assert.Equal(2147483673L, opposite.TraumaPressure);
        Assert.Equal(4, opposite.MaximumSeverityRank);
        Assert.Equal(long.MaxValue, Assert.IsType<SpiritualWoundCalculation>(
            SpiritualWoundOpportunityMath.Calculate(Input(margin: long.MaxValue - 25,
                art: 5, after: "broken"))).TraumaPressure);
        Assert.Equal(long.MinValue, Assert.IsType<SpiritualWoundCalculation>(
            SpiritualWoundOpportunityMath.Calculate(Input(margin: long.MinValue + 10,
                resilience: 5))).TraumaPressure);
        Assert.Equal(long.MaxValue - 1, Assert.IsType<SpiritualWoundCalculation>(
            SpiritualWoundOpportunityMath.Calculate(Input(margin: long.MaxValue - 9,
                art: 5, after: "clear"))).TraumaPressure);
        Assert.Equal(long.MinValue + 4, Assert.IsType<SpiritualWoundCalculation>(
            SpiritualWoundOpportunityMath.Calculate(Input(margin: long.MinValue + 1,
                resilience: 1, after: "fractured"))).TraumaPressure);
    }
}
```

- [x] **Step 2: Run the owning formula tests and confirm semantic RED.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~SpiritualWoundOpportunityTests"
```

Expected: compilation succeeds; all 42 formula rows fail `Assert.IsType` because the
shell returns null. Record the actual result artifacts and counts. A compile or
discovery failure is not the intended RED and must be repaired before proceeding.

- [x] **Step 3: Implement exact arithmetic without the not-yet-tested domain gates.**

Replace the shell class body with the following. Keep the records unchanged.
This intermediate implementation is not a candidate for integration: Step 4
must first expose permissive invalid domains and Step 5 must close them.

```csharp
    internal static SpiritualWoundCalculation? Calculate(SpiritualWoundCalculationInput input)
    {
        var previous = StrainRank(input.PreviousStrain);
        var next = StrainRank(input.NewStrain);
        var modeCap = ModeCap(input.DangerMode);
        var extra = Math.Max(0, next - previous - 1);
        var adjustment = 2L * (input.AppliedArtTier - (long)input.TargetResilienceTier)
            + 2L * (next - 1)
            + 3L * extra;
        var pressure = input.HarmfulMargin + adjustment;
        var formula = pressure switch
        {
            < 8 => 0,
            < 13 => 1,
            < 18 => 2,
            < 23 => 3,
            _ => 4
        };
        var increasing = next > previous;
        var maximum = increasing
            ? Math.Min(Math.Min(formula, next), Math.Min(modeCap, input.SourceSeverityCap))
            : 0;
        return new(input, previous, next, extra, pressure, formula, next, modeCap, increasing, maximum);
    }

    private static int StrainRank(string? strain) => strain switch
    {
        "clear" => 0,
        "strained" => 1,
        "fractured" => 2,
        "overwhelmed" => 3,
        "broken" => 4,
        _ => -1
    };

    private static int ModeCap(string? mode) => mode switch
    {
        "training" => 0,
        "controlled" => 2,
        "hostile" or "annihilation" => 4,
        _ => -1
    };
```

Run the exact Step 2 command again; expected all existing formula rows PASS.

- [x] **Step 4: Add complete invalid-domain tests and confirm their separate semantic RED.**

Insert these methods inside the existing test class, with no changes to earlier
assertions or the production method. Every invalid input currently produces a
non-null result, so each gate obtains actual behavioral RED before implementation.

```csharp
    [Theory]
    [InlineData(-1, 0, 4)]
    [InlineData(6, 0, 4)]
    [InlineData(0, -1, 4)]
    [InlineData(0, 6, 4)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 5)]
    [InlineData(int.MaxValue, int.MinValue, int.MaxValue)]
    public void InvalidNumericDomain_RejectsWithoutClamping(int art, int resilience, int cap)
    {
        Assert.Null(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: 30, art: art, resilience: resilience, after: "broken", sourceCap: cap)));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData("unknown", false)]
    [InlineData("unknown", true)]
    [InlineData("BROKEN", false)]
    [InlineData("BROKEN", true)]
    [InlineData(" broken ", false)]
    [InlineData(" broken ", true)]
    public void InvalidStrainToken_RejectsBothCoordinates(string? strain, bool destination)
    {
        var input = destination ? Input(after: strain) : Input(before: strain);
        Assert.Null(SpiritualWoundOpportunityMath.Calculate(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("HOSTILE")]
    [InlineData(" hostile ")]
    public void InvalidDangerToken_RejectsWithoutHostileFallback(string? mode)
    {
        Assert.Null(SpiritualWoundOpportunityMath.Calculate(Input(mode: mode)));
    }

    [Theory]
    [InlineData(long.MaxValue, 5, 0, "broken")]
    [InlineData(long.MinValue, 0, 5, "strained")]
    public void InvalidArithmetic_RejectsUnrepresentablePressureWithoutWrapping(
        long margin, int art, int resilience, string after)
    {
        Assert.Null(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: margin, art: art, resilience: resilience, after: after)));
    }
```

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~SpiritualWoundOpportunityTests.Invalid"
```

Expected: exactly the 24 newly added invalid-domain/overflow rows fail because result is not
null, not because of an exception. Origin and destination are independent theory
rows, so a failing origin assertion cannot mask an untested destination gate.

- [x] **Step 5: Add the bounded domain and arithmetic gates, then run the whole owning selection.**

Insert this code immediately after the three `previous`, `next`, `modeCap` local
initializations and before `extra` or any pressure calculation:

```csharp
        if (previous < 0 || next < 0 || modeCap < 0
            || input.AppliedArtTier is < 0 or > 5
            || input.TargetResilienceTier is < 0 or > 5
            || input.SourceSeverityCap is < 0 or > 4)
        {
            return null;
        }
```

Replace the unchecked `var pressure = ...` assignment with this exact block;
do not catch unrelated exceptions or saturate the diagnostic pressure:

```csharp
        long pressure;
        try
        {
            pressure = checked(input.HarmfulMargin + adjustment);
        }
        catch (OverflowException)
        {
            return null;
        }
```

Run Step 2's owning filter. Expected: all 66 formula and domain rows PASS, build with
zero warnings/errors, cleanup complete and no skips or duplicate test artifacts.
Read each actual TRX row/counter and the final summary, not only the exit status.

- [x] **Step 6: Record bounded verification, source boundary, and independent review.**

Run one meaningful Fast checkpoint, no extra FullValidation/PreMerge/Deep lane:

```powershell
.\scripts\test-csharp.ps1 -Lane Fast
git diff --check
```

The known required legacy-source RED may stop Fast before all discovered tests
finish; report its exact name, counters, uncompleted discovery and artifacts. Any
different failure requires diagnosis, not reclassification as expected legacy RED.
Inspect both complete source/test diffs and ensure the new pure file has no runtime
callers or GM/public/persistence surface. No actual accepted exchange, natural-roll
provenance, zero-OD profile integration, or full spiritual wound flow is proven by
this arithmetic-only control. Preserve those full T076/T084 obligations unchecked.

Parent records the source commit and artifact links under T076/T084 plus this
plan's link in the feature plan, including the no-GM-update rationale above.
Commit only the exact two source/test files and parent-owned evidence documents;
do not stage unrelated changes. Use a fresh independent Spec then Code Quality
review against the exact task BASE/candidate diff. No remote push/merge/issue close.

## Parent self-review before dispatch

- Formula term signs, all four threshold boundaries, zero through maximum tier,
  negative raw margin, widened int-minimum sign reversal, exact and overflowing
  long bounds (with small-term cancellation), destination/mode/source caps, jump
  arithmetic, non-increasing/broken target and invalid input domains are included.
- This is a bounded sub-plan for T076/T084 math only, not a replacement for US3
  T077-T092, US4 healing, or Mortal treatment/legacy work. There is no completeness
  claim for raw accepted harmful-margin source, natural critical preservation,
  accepted mode escalation, passive OD behavior, one-per-side, re-trauma, export,
  GM choice, or dissipation without their actual adapters and lifecycle tests.
- Exact signatures and record fields are defined here and used consistently in
  all test/implementation snippets. No production caller, authority seal, mutable
  JSON, migration, or unspecified follow-on API is introduced.
- Before dispatch, verify current implementer has returned and its accepted source
  baseline is fixed; extract this task with these global constraints into a fresh
  brief. The plan is not permission for parallel C# execution or concurrent source
  changes in another active implementation task.

## Parent acceptance — 2026-09-07

Bounded Task 1 is complete at `1969a695d0ae15faaef4240e3259503f1a000a05`
against recorded BASE `8186cf6ad6aebd1b128049b8663490ffaaf95b30`: exactly two new
files, 287 lines. Parent inspected the full source/test diff and actual result
artifacts; independent task review returned Spec Compliant / Quality Approved,
zero Critical/Important/Minor findings. Its outside-diff item is resolved by
retaining the actual accepted-adapter/profile/OD/provenance/export requirements
under unchecked T076/T084. Production search confirms no runtime caller.

All artifacts are under `TestResults/test-lanes/`:

- `20260907-073159-177-30160-7fe9e0083e604aaa82bb43913499d65c-focused`:
  42/42 intended null-shell assertion failures, 1:10.699 at five minutes.
- `20260907-073330-616-49900-a205247754a24e129fc3da3162f50edc-focused`:
  42/42 arithmetic PASS, 1:05.886.
- `20260907-073456-894-49240-aac8c4bc0e174900a58fae87fedb7b67-focused`:
  24/24 intended invalid-domain/non-null assertion failures, 32.886 seconds.
- `20260907-073545-781-47512-b0b32850da5844b1a070d049fcb27c58-focused`:
  66/66 final PASS, 1:01.650.
- `20260907-073653-328-37776-3452b89033674cbb9a4116bb8f714fc5-fast`:
  one Fast, 2:17.472 at five minutes; 6,207 PASS / one required unchanged
  `WoundLegacySource_SurvivesWithoutActiveWoundButIsNeverPubliclyMaterializable`
  failure. Eleven completed TRX files contain 6,208 rows; discovery has 7,380,
  leaving arithmetic difference 1,172 uncompleted, not an exact identity-set
  assertion. All 66 new math rows passed here too. Fast is incomplete, not GREEN.

Every run has zero build warnings/errors, no timeout, successful owned-process
cleanup and no duplicate artifacts. Parent verified counters, row outcomes,
semantic failure messages and logs. `git diff --check` passed. The report's initial
confusion between completed-TRX Total and all discovery was corrected before
acceptance; no source or verification rerun was needed for that report correction.
The documented no-GM-update rationale still holds: this unused value calculator
does not expose spiritual wounds or healing. No FullValidation, PreMerge, remote
operation, legacy-choice change or top-level task closure occurred.
