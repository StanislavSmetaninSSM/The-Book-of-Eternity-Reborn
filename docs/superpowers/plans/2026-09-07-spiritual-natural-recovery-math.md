# Spiritual Natural Recovery Math Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Implement #1536 T097/T105's pure natural-recovery arithmetic without claiming accepted cycles or entity recovery are implemented.

**Architecture:** An internal immutable calculator adds one numerical cycle to current progress, carries points through at most four severity steps, and returns exact diagnostics with a separate healed flag. The later single accepted recovery planner obtains canonical inputs and owns eligibility, cycle authority, transitions and publication.

**Tech Stack:** C#/.NET 8, xUnit 2.9.2, PowerShell 7 bounded runner; no new dependency.

## Global Constraints

- Tracked source: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), open T097/T105 in `specs/1536-complete-wound-materialization/tasks.md`; governance is `AGENTS.md` and `.specify/memory/constitution.md`.
- Use only the existing `1536-complete-wound-materialization` branch/worktree. No migration, new branch, remote operation, unrelated `.serena/` edit or unresolved legacy/art-schema decision.
- FR-050: Every safe afterlife cycle MUST add `1 + Spiritual Healing tier` natural recovery points to each eligible spiritual wound.
- FR-051: Natural recovery thresholds MUST be 2, 4, 6, and 8 points for leaving severity I, II, III, and IV respectively, with remaining points carried forward.
- FR-052: Worsening a spiritual wound MUST reset progress for its current step.
- FR-053: The natural-recovery and art-progression rules MUST apply to players, Guardians, residents, leaders, and other authorized afterlife entities.
- Natural recovery has no active-healing tier gate: tier 0 recovers every spiritual severity. Healing tier is 0-V; active severity rank is I-IV.
- Overflow crosses later steps; final healing leaves canonical severity I and progress 0, not severity 0. Excess after healing is diagnostic only, never transferred to another wound.
- This unused calculator must not add cycle identity, unsafe/conflict eligibility, art/profile authority, worsening mutation, OD/resources, scheduler/wound/effect/history publication or a GM/player-facing capability.
- Keep T097/T105 open for safe/unsafe and replay seals, worsening integration, player/entity eligibility and accepted transitions. Numerical steps are not proof that game time elapsed.
- One C#/source owner only. Five-minute Focused/Fast bounds, finish commands before source edits, no unbounded tests. Preserve the required unfinished legacy-source Fast failure without skip, waiver or unrelated repair.

## Source-backed design and boundaries

Approved sources: afterlife spiritual-wounds/healing contract's Natural recovery
section, research R-012, data-model section 13 and FR-050 through FR-053. The
approved IV examples require 20 tier-0 numerical cycles or four tier-V cycles.

`WoundRecovery` stores progress and threshold as long. Its current parser accepts
nonnegative long progress, not only progress below threshold. Do not invent a
below-threshold restriction or clamp carried points. The future owner proves the
canonical value; this function adds exactly `1+tier` with checked long arithmetic.
The threshold derives from rank, not a caller-selected number.

Current `WoundTransitionReducer.ValidateWorsen` already requires progress0.
Actual integration must preserve that reset, apply active treatment before ordinary
recovery once, and obtain cycle identity from accepted progression state. This
helper has no reset/safety/cycle boolean pretending to prove those facts.

The current generic recovery follow-up heal predicate permits original I/II only.
Carried progress can mathematically cross III/IV through healing. T105/T107 must
represent the approved ordered transitions and exact tick/terminal authority;
never discard/cap those points or reuse the Mortal-only staging exception.
The active partial one-point result followed by natural recovery at a threshold
also needs an actual combined-planner test. This task changes no reducer permission,
accepted phase ordering or public contract.

## File map and GM boundary

- Create `BookOfEternityClient/Services/SpiritualWoundRecoveryMath.cs`: immutable numeric input/result and bounded calculator.
- Create `BookOfEternityClient.Tests/SpiritualWoundNaturalRecoveryTests.cs`: 43 valid-value rows plus nine invalid/overflow rows, in-memory arithmetic only.
- Parent owns plan/task evidence and acceptance. No recovery planner, scheduler, profile, canonical wound or reducer change belongs here.
- No runtime caller, GM-authored state, pending/control, response, receipt or command is exposed. The approved spec already contains the formula, so Mortal/afterlife prompts, matrix, examples, manifest and source guards need no update for this unused prerequisite. No conditional FullValidation; future integration retains all GM synchronization tasks.

### Task 1: Exact bounded natural-recovery arithmetic

**Interface:** `internal static SpiritualRecoveryCalculation? SpiritualWoundRecoveryMath.Calculate(SpiritualRecoveryCalculationInput input)`.
Null means invalid input/overflow. All result fields are recomputable values, not
accepted source authority. An unchanged severity still gains progress.

- [ ] **Step 1: Add the compilable API shell and 43 valid-value tests.**

Create the service:
```csharp
namespace BookOfEternityClient.Services;

internal readonly record struct SpiritualRecoveryCalculationInput(
    int HealingTier, int SeverityRank, long CurrentStepProgress);

// Numeric diagnostics only: not an accepted cycle, tick seal or wound authority.
internal sealed record SpiritualRecoveryCalculation(
    SpiritualRecoveryCalculationInput Input,
    int PointsAdded,
    long AvailablePoints,
    int StepsCompleted,
    int ResultingSeverityRank,
    long RemainingProgress,
    int ResultingThreshold,
    bool HealsWound,
    long UnusedPoints);

internal static class SpiritualWoundRecoveryMath
{
    internal static SpiritualRecoveryCalculation? Calculate(SpiritualRecoveryCalculationInput input)
        => null;
}
```

Create the test class. Numerical repetition never launches a game, changes world
state, authenticates an actor or fabricates a tick.
```csharp
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualWoundNaturalRecoveryTests
{
    private static SpiritualRecoveryCalculationInput Input(int tier = 0, int rank = 4, long progress = 0)
        => new(tier, rank, progress);

    private static SpiritualRecoveryCalculation Calculate(SpiritualRecoveryCalculationInput input)
    {
        var actual = Assert.IsType<SpiritualRecoveryCalculation>(SpiritualWoundRecoveryMath.Calculate(input));
        Assert.Equal(input, actual.Input);
        Assert.Equal(1 + input.HealingTier, actual.PointsAdded);
        Assert.Equal(checked(input.CurrentStepProgress + actual.PointsAdded), actual.AvailablePoints);
        Assert.InRange(actual.StepsCompleted, 0, input.SeverityRank);
        Assert.InRange(actual.ResultingSeverityRank, 1, 4);
        Assert.InRange(actual.RemainingProgress, 0L, actual.ResultingThreshold - 1L);
        var spent = Enumerable.Range(0, actual.StepsCompleted)
            .Sum(step => 2L * (input.SeverityRank - step));
        Assert.Equal(actual.AvailablePoints, spent + actual.RemainingProgress + actual.UnusedPoints);
        return actual;
    }

    private static void AssertResult(SpiritualRecoveryCalculation actual,
        int steps, int rank, long progress, bool heals, long unused)
    {
        Assert.Equal(steps, actual.StepsCompleted);
        Assert.Equal(rank, actual.ResultingSeverityRank);
        Assert.Equal(progress, actual.RemainingProgress);
        Assert.Equal(2 * rank, actual.ResultingThreshold);
        Assert.Equal(heals, actual.HealsWound);
        Assert.Equal(unused, actual.UnusedPoints);
    }

    [Theory]
    [InlineData(0, 1, 1, 0, 1, 1, false, 0)]
    [InlineData(1, 1, 2, 1, 1, 0, true, 0)]
    [InlineData(2, 1, 3, 1, 1, 0, true, 1)]
    [InlineData(3, 1, 4, 1, 1, 0, true, 2)]
    [InlineData(4, 1, 5, 1, 1, 0, true, 3)]
    [InlineData(5, 1, 6, 1, 1, 0, true, 4)]
    [InlineData(0, 2, 1, 0, 2, 1, false, 0)]
    [InlineData(1, 2, 2, 0, 2, 2, false, 0)]
    [InlineData(2, 2, 3, 0, 2, 3, false, 0)]
    [InlineData(3, 2, 4, 1, 1, 0, false, 0)]
    [InlineData(4, 2, 5, 1, 1, 1, false, 0)]
    [InlineData(5, 2, 6, 2, 1, 0, true, 0)]
    [InlineData(0, 3, 1, 0, 3, 1, false, 0)]
    [InlineData(1, 3, 2, 0, 3, 2, false, 0)]
    [InlineData(2, 3, 3, 0, 3, 3, false, 0)]
    [InlineData(3, 3, 4, 0, 3, 4, false, 0)]
    [InlineData(4, 3, 5, 0, 3, 5, false, 0)]
    [InlineData(5, 3, 6, 1, 2, 0, false, 0)]
    [InlineData(0, 4, 1, 0, 4, 1, false, 0)]
    [InlineData(1, 4, 2, 0, 4, 2, false, 0)]
    [InlineData(2, 4, 3, 0, 4, 3, false, 0)]
    [InlineData(3, 4, 4, 0, 4, 4, false, 0)]
    [InlineData(4, 4, 5, 0, 4, 5, false, 0)]
    [InlineData(5, 4, 6, 0, 4, 6, false, 0)]
    public void EveryTierAddsOnePlusTier_WithoutAnActiveHealingTierGate(
        int tier, int rank, int added, int steps, int nextRank, long progress, bool heals, long unused)
    {
        var actual = Calculate(Input(tier, rank));
        Assert.Equal(added, actual.PointsAdded);
        AssertResult(actual, steps, nextRank, progress, heals, unused);
    }

    [Theory]
    [InlineData(1, 0, 0, 1, 1, false)]
    [InlineData(1, 1, 1, 1, 0, true)]
    [InlineData(2, 2, 0, 2, 3, false)]
    [InlineData(2, 3, 1, 1, 0, false)]
    [InlineData(3, 4, 0, 3, 5, false)]
    [InlineData(3, 5, 1, 2, 0, false)]
    [InlineData(4, 6, 0, 4, 7, false)]
    [InlineData(4, 7, 1, 3, 0, false)]
    public void ExactThresholdBoundary_UsesTheCurrentSeverity(
        int rank, long progress, int steps, int nextRank, long nextProgress, bool heals)
    {
        AssertResult(Calculate(Input(rank: rank, progress: progress)),
            steps, nextRank, nextProgress, heals, 0);
    }

    [Theory]
    [InlineData(4, 7, 5, 1, 3, 5, false, 0)]
    [InlineData(3, 5, 5, 2, 1, 1, false, 0)]
    [InlineData(2, 3, 5, 2, 1, 0, true, 3)]
    [InlineData(3, 6, 5, 3, 1, 0, true, 0)]
    [InlineData(4, 14, 5, 4, 1, 0, true, 0)]
    [InlineData(4, 20, 0, 4, 1, 0, true, 1)]
    [InlineData(3, 8, 0, 1, 2, 3, false, 0)]
    [InlineData(1, 2, 0, 1, 1, 0, true, 1)]
    public void CarriedProgress_CrossesEveryFollowingStepWithoutLoss(
        int rank, long progress, int tier, int steps, int nextRank, long nextProgress, bool heals, long unused)
    {
        AssertResult(Calculate(Input(tier, rank, progress)),
            steps, nextRank, nextProgress, heals, unused);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(5, 4, 4)]
    public void RepeatedNumericCycles_FromFourHealInTheApprovedCount(
        int tier, int expectedCycles, long unused)
    {
        var rank = 4;
        long progress = 0;
        var totalSteps = 0;
        SpiritualRecoveryCalculation? last = null;
        for (var cycle = 1; cycle <= expectedCycles; cycle++)
        {
            last = Calculate(Input(tier, rank, progress));
            Assert.Equal(cycle == expectedCycles, last.HealsWound);
            totalSteps += last.StepsCompleted;
            rank = last.ResultingSeverityRank;
            progress = last.RemainingProgress;
        }
        Assert.NotNull(last);
        Assert.Equal(4, totalSteps);
        Assert.Equal(1, rank);
        Assert.Equal(0L, progress);
        Assert.Equal(2, last.ResultingThreshold);
        Assert.Equal(unused, last.UnusedPoints);
    }

    [Fact]
    public void LargestRepresentableAggregate_CompletesInFourBoundedSteps()
    {
        var actual = Calculate(Input(tier: 5, progress: long.MaxValue - 6));
        Assert.Equal(long.MaxValue, actual.AvailablePoints);
        AssertResult(actual, 4, 1, 0, true, long.MaxValue - 20);
    }
}
```

- [ ] **Step 2: Observe 43 semantic RED rows before arithmetic.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~SpiritualWoundNaturalRecoveryTests"
```

Expected 43 executed, all fail Assert.IsType because the shell returns null.
Compilation/discovery/fixture errors are not this RED. Record actual rows/messages
and finish the command before source edits.

- [ ] **Step 3: Implement arithmetic and confirm 43 GREEN.**

Replace the shell method:
```csharp
    internal static SpiritualRecoveryCalculation? Calculate(SpiritualRecoveryCalculationInput input)
    {
        var added = 1 + input.HealingTier;
        var available = input.CurrentStepProgress + added;
        var remaining = available;
        var rank = input.SeverityRank;
        var steps = 0;
        while (rank > 0 && remaining >= 2L * rank)
        {
            remaining -= 2L * rank;
            steps++;
            if (rank == 1)
                return new(input, added, available, steps, 1, 0, 2, true, remaining);
            rank--;
        }
        return new(input, added, available, steps, rank, remaining, 2 * rank, false, 0);
    }
```

Run Step 2's exact command: expected 43/43 PASS. Input/overflow gates deliberately
follow their separate RED. The rank-positive loop condition also prevents the
interim rank0 invalid case from looping forever.

- [ ] **Step 4: Add nine invalid-domain/overflow tests and observe RED.**

Insert without changing earlier assertions:
```csharp
    [Theory]
    [InlineData(-1, 4, 0)]
    [InlineData(6, 4, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 5, 0)]
    [InlineData(0, 4, -1)]
    [InlineData(0, 4, long.MinValue)]
    public void InvalidDomain_RejectsWithoutClamping(int tier, int rank, long progress)
    {
        Assert.Null(SpiritualWoundRecoveryMath.Calculate(Input(tier, rank, progress)));
    }

    [Theory]
    [InlineData(0, long.MaxValue)]
    [InlineData(5, long.MaxValue - 5)]
    [InlineData(5, long.MaxValue)]
    public void InvalidArithmetic_RejectsOverflowRatherThanLosingProgress(int tier, long progress)
    {
        Assert.Null(SpiritualWoundRecoveryMath.Calculate(Input(tier: tier, progress: progress)));
    }
```

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~SpiritualWoundNaturalRecoveryTests.Invalid"
```

Expected nine/nine fail Assert.Null against non-null interim results, including
wrapped negative available progress; not compilation, exception or timeout.
Complete this command before editing production.

- [ ] **Step 5: Add exact domain/checked-sum gates and confirm 52 GREEN.**

Insert at the start of Calculate:
```csharp
        if (input.HealingTier is < 0 or > 5
            || input.SeverityRank is < 1 or > 4
            || input.CurrentStepProgress < 0)
        {
            return null;
        }
```

Keep `var added = 1 + input.HealingTier;` and replace the available declaration:
```csharp
        long available;
        try
        {
            available = checked(input.CurrentStepProgress + added);
        }
        catch (OverflowException)
        {
            return null;
        }
```

Run Step 2's exact command: expected 52/52 PASS. Valid rank bounds the loop to four
iterations independently of progress magnitude. Other arithmetic uses bounded
rank/points or subtracts from sufficient nonnegative remaining progress.

- [ ] **Step 6: Run one bounded Fast, record evidence and independently review.**

```powershell
.\scripts\test-csharp.ps1 -Lane Fast
git diff --check
```

Inspect actual summaries, all TRX outcomes/counters, messages, build warnings/
errors, cleanup, timeout and duplicates. Report Fast discovery separately from
completed TRX Total. If the required legacy-source failure stops Fast, retain
exact name/counts and arithmetic uncompleted difference, not full GREEN or an
identity-set claim. No extra Fast/FV/PreMerge or unrelated legacy repair.

Commit only the two named code/test files. Parent inspects exact recorded
BASE..candidate, actual artifacts and a fresh independent Spec Compliance plus
Quality review before acceptance. Report the no-GM-update rationale; T097/T105
remain open for the actual cycle/entity/replay/worsening/transition integration.

## Parent self-review

- The 43 valid rows are 24 tier/rank + eight threshold + eight carry + two cycle
  counts + one long-endpoint fact. Invalid rows are six domain + three overflow,
  final52. Tests cover all tiers/severities, boundaries, carried multi-step progress,
  canonical healedI, unused overflow, conservation and approved 20/four counts.
  Parent parsed all51 literal InlineData rows from the written plan and checked
  them independently with a BigInt fixed-threshold table: zero mismatches; the
  separate long-endpoint fact also agrees. This is plan review, not C# evidence.
- Nonnegative long progress is preserved, not narrowed to normalized progress.
  Real integration must resolve the named phase/tick/follow-up boundary; this
  value helper supplies no new reducer permission or active-healing tier gate.
- FR-052 reset and FR-053 all-entity authority remain tracked integration tasks.
  No safety/cycle boolean substitutes for accepted progression evidence.
- Types/helpers are explicit; internal result types only occur in private test
  signatures. Common checks assert input/point conservation and bounds against
  independently derived expectations; no assertion compares a value to itself.
