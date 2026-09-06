# Spiritual Healing Outcome Math Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the non-authoritative numeric prerequisite of #1536 T094/T101: one tier gate and exact spiritual-healing result calculation, without presenting an unfinished healing workflow as available.

**Architecture:** One internal pure calculator accepts immutable value inputs and returns recomputable diagnostics, a bounded improvement and the separate terminal-heal flag. The later single accepted healing resolver obtains the actual wound, art, modifier and sealed-die authority and consumes this calculation; this class neither generates a roll nor authorizes a transition.

**Tech Stack:** C#/.NET 8, nullable enabled, xUnit 2.9.2, PowerShell 7 bounded runner; no new dependencies.

## Global Constraints

- Tracked source: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), `specs/1536-complete-wound-materialization/spec.md`, `plan.md`, `tasks.md`, open T094/T101; governance is `.specify/memory/constitution.md` and `AGENTS.md`.
- Stay in the existing `1536-complete-wound-materialization` branch/worktree. No migration, new branch, remote operation, unrelated `.serena/` edit or legacy-preparation decision.
- FR-042: Spiritual Healing tier 0 MUST support diagnosis only; tiers I-IV MUST treat matching-or-lower severity; tier V MUST treat every spiritual severity.
- FR-043: Active spiritual healing MUST use the approved total, difficulty, margin bands, complication modifiers, and sealed die evidence.
- FR-044: A healer below the wound's current severity MUST NOT reduce severity even on a natural 20.
- FR-045: A sufficient healer's natural 20 or margin at least +8 MUST reduce severity by two steps; margin 0 through +7 by one; margin -1 through -4 MUST add one recovery point; natural 1 or margin at most -5 MUST add no active improvement.
- Exact formula: total = d20 + 2H + validatedModifiers; difficulty = 10 + 2W + complications; margin = total - difficulty. H is 0-V, active W is I-IV, d20 is 1-20.
- Natural 1 overrides a numerically successful total. Tier insufficiency precedes every result band, including natural 20. A result crossing below I heals, but canonical healed severity remains I, not an invented severity 0.
- This value-only task must not add a runtime caller, accepted dice/actor authority, roll generation, art registration/progression, OD/resource spend, cycle identity, natural recovery, wound/effect/history mutation, command or GM-authored output.
- Keep complete T094/T101 unchecked: real sealed-die provenance, accepted modifiers and wounds, shared command/self/NPC/service resolver, transitions and retry/publish integration remain required.
- One source/C# owner only; finish each verification command before source edits. Use five-minute Focused/Fast bounds and no unbounded solution test. Preserve the required unfinished legacy-source Fast failure without skipping, fixing or waiving it.

## Source-backed design and scope

Approved contract: `contracts/afterlife-spiritual-wounds-and-healing.md` active
healing section; research R-011; data-model section 12; spec FR-042 through FR-045.
The data-model example H3/WII/d20=14/modifiers0/complication1 has total20,
difficulty15, margin5 and reduce_one. The current wound contract reads each
complication difficulty in 0..4; its nonnegative sum is a later authority owner's
job. This calculator accepts a nonnegative long aggregate, not caller evidence.
General validated modifiers are signed long; no clamping or invented upper bound.

There is no existing spiritual healing resolver/test file. Use a dedicated
`SpiritualHealingOutcomeMath` beside the accepted `SpiritualWoundOpportunityMath`,
not the authority-bearing future `SpiritualHealingResolver` name. Both remain
unused numerical helpers until their accepted adapters are implemented. The
current scalar-tier versus approved `{tier,experience}` art-schema discrepancy
does not affect value arithmetic and is not resolved by this plan.

Malformed numeric input or unrepresentable total/difficulty/margin returns null.
Valid insufficient tier returns an explicit result with `InsufficientTier`, no
roll arithmetic (nullable audit values), no points/steps and unchanged severity.
This is a diagnosis-capable distinction from malformed input, not an accepted
attempt, resource reservation or promise that the diagnosis UI exists.
Validate all input domains before the insufficiency branch; malformed dice or
negative complications cannot hide behind a low-tier result.

The bounded improvement count includes the final I-to-healed step. Thus strong
healing of III yields active I; strong healing of II yields healed I; either
positive band on I yields healed I. A separate `HealsWound` flag retains this
distinction without severity0 or a terminal mutation. One recovery point is only
the active partial result, not a safe-cycle tick; no time passes in this function.

## File map and documentation boundary

- Create `BookOfEternityClient/Services/SpiritualHealingOutcomeMath.cs`: input, result, result-band enum and pure calculator only.
- Create `BookOfEternityClient.Tests/SpiritualHealingResolverTests.cs`: 44 valid-value rows and 14 invalid/overflow rows, entirely in memory.
- Parent updates this plan and links evidence under T094/T101 in the feature plan/tasks after actual artifacts and independent review.
- No existing production owner or GM contract changes. The already-approved specification contains the formula; Mortal/afterlife prompts, matrix, examples, manifest, daemon entrypoints and source guards need no change for this unused internal prerequisite. Record this rationale, keep the later full GM synchronization tasks open, and do not run conditional FullValidation for this math-only boundary.

### Task 1: Tier-gated immutable spiritual healing arithmetic

**Interfaces:** Consumes the immutable input defined below; produces
`internal static SpiritualHealingCalculation? SpiritualHealingOutcomeMath.Calculate(SpiritualHealingCalculationInput input)`.
Every field is caller-recomputable, with no authorization/seal semantics. Null is
invalid data; a non-null `InsufficientTier` result is valid blocked arithmetic.

- [ ] **Step 1: Add the API shell and all 44 valid-value tests.**

Create the service with this compilable shell; do not add arithmetic yet:

```csharp
namespace BookOfEternityClient.Services;

internal readonly record struct SpiritualHealingCalculationInput(
    int HealingTier, int WoundSeverityRank, int NaturalRoll,
    long ValidatedModifiers, long ComplicationModifier);

internal enum SpiritualHealingResultBand
{
    InsufficientTier,
    NoImprovement,
    RecoveryPoint,
    ReduceOne,
    ReduceTwo
}

// Recomputable values only: never an accepted roll or wound-transition authority.
internal sealed record SpiritualHealingCalculation(
    SpiritualHealingCalculationInput Input,
    bool HasSufficientTier,
    long? Total,
    long? Difficulty,
    long? Margin,
    SpiritualHealingResultBand ResultBand,
    int ImprovementSteps,
    int ResultingSeverityRank,
    bool HealsWound,
    int RecoveryPointsAdded);

internal static class SpiritualHealingOutcomeMath
{
    internal static SpiritualHealingCalculation? Calculate(SpiritualHealingCalculationInput input)
        => null;
}
```

Create the complete valid-value tests below. These exercise values only; no
fixture root, GameEngine, filesystem, mock or looped integration scenario belongs here.

```csharp
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualHealingResolverTests
{
    private static SpiritualHealingCalculationInput Input(
        int tier = 3, int rank = 3, int roll = 10,
        long modifiers = 0, long complications = 0)
        => new(tier, rank, roll, modifiers, complications);

    private static SpiritualHealingCalculation Calculate(SpiritualHealingCalculationInput input)
        => Assert.IsType<SpiritualHealingCalculation>(SpiritualHealingOutcomeMath.Calculate(input));

    [Theory]
    [InlineData(-6, "NoImprovement", 0, 0, 3)]
    [InlineData(-5, "NoImprovement", 0, 0, 3)]
    [InlineData(-4, "RecoveryPoint", 0, 1, 3)]
    [InlineData(-1, "RecoveryPoint", 0, 1, 3)]
    [InlineData(0, "ReduceOne", 1, 0, 2)]
    [InlineData(7, "ReduceOne", 1, 0, 2)]
    [InlineData(8, "ReduceTwo", 2, 0, 1)]
    [InlineData(9, "ReduceTwo", 2, 0, 1)]
    public void ExactMarginBands_PreserveEveryBoundary(
        int margin, string band, int steps, int points, int resultingRank)
    {
        var input = Input(modifiers: margin);
        var actual = Calculate(input);
        Assert.Equal(input, actual.Input);
        Assert.True(actual.HasSufficientTier);
        Assert.Equal((long?)(16 + margin), actual.Total);
        Assert.Equal((long?)16, actual.Difficulty);
        Assert.Equal((long?)margin, actual.Margin);
        Assert.Equal(band, actual.ResultBand.ToString());
        Assert.Equal(steps, actual.ImprovementSteps);
        Assert.Equal(points, actual.RecoveryPointsAdded);
        Assert.Equal(resultingRank, actual.ResultingSeverityRank);
        Assert.False(actual.HealsWound);
    }

    [Theory]
    [InlineData(3, 2, 14, 0, 1, 20, 15, 5)]
    [InlineData(5, 4, 10, -3, 2, 17, 20, -3)]
    [InlineData(2, 1, 5, 4, 0, 13, 12, 1)]
    [InlineData(5, 1, 10, 0, 0, 20, 12, 8)]
    [InlineData(1, 1, 10, -8, 0, 4, 12, -8)]
    public void ExactAudit_UsesTierSignedModifiersAndComplicationAggregate(
        int tier, int rank, int roll, int modifiers, int complications,
        int total, int difficulty, int margin)
    {
        var input = Input(tier, rank, roll, modifiers, complications);
        var actual = Calculate(input);
        Assert.Equal(input, actual.Input);
        Assert.True(actual.HasSufficientTier);
        Assert.Equal((long?)total, actual.Total);
        Assert.Equal((long?)difficulty, actual.Difficulty);
        Assert.Equal((long?)margin, actual.Margin);
    }

    [Theory]
    [InlineData(5, 4, 1, 100, 0, 93, "NoImprovement", 0, 4, false)]
    [InlineData(1, 1, 1, 100, 0, 91, "NoImprovement", 0, 1, false)]
    [InlineData(4, 4, 20, -100, 5, -95, "ReduceTwo", 2, 2, false)]
    [InlineData(1, 1, 20, -100, 0, -90, "ReduceTwo", 1, 1, true)]
    public void NaturalResults_OverrideOppositeNumericBandOnlyAfterTierGate(
        int tier, int rank, int roll, int modifiers, int complications,
        int margin, string band, int steps, int resultingRank, bool heals)
    {
        var actual = Calculate(Input(tier, rank, roll, modifiers, complications));
        Assert.Equal((long?)margin, actual.Margin);
        Assert.Equal(band, actual.ResultBand.ToString());
        Assert.Equal(steps, actual.ImprovementSteps);
        Assert.Equal(resultingRank, actual.ResultingSeverityRank);
        Assert.Equal(heals, actual.HealsWound);
        Assert.Equal(0, actual.RecoveryPointsAdded);
    }

    [Theory]
    [InlineData(1, 0, "ReduceOne", 1, 1, true)]
    [InlineData(2, 0, "ReduceOne", 1, 1, false)]
    [InlineData(3, 0, "ReduceOne", 1, 2, false)]
    [InlineData(4, 0, "ReduceOne", 1, 3, false)]
    [InlineData(1, 8, "ReduceTwo", 1, 1, true)]
    [InlineData(2, 8, "ReduceTwo", 2, 1, true)]
    [InlineData(3, 8, "ReduceTwo", 2, 1, false)]
    [InlineData(4, 8, "ReduceTwo", 2, 2, false)]
    public void BoundedImprovement_DistinguishesActiveOneFromHealedOne(
        int rank, int margin, string band, int steps, int resultingRank, bool heals)
    {
        var actual = Calculate(Input(tier: 5, rank: rank,
            modifiers: 10 + 2 * rank - 20 + margin));
        Assert.Equal((long?)margin, actual.Margin);
        Assert.Equal(band, actual.ResultBand.ToString());
        Assert.Equal(steps, actual.ImprovementSteps);
        Assert.Equal(resultingRank, actual.ResultingSeverityRank);
        Assert.InRange(actual.ResultingSeverityRank, 1, 4);
        Assert.Equal(heals, actual.HealsWound);
        Assert.Equal(0, actual.RecoveryPointsAdded);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(0, 3)]
    [InlineData(0, 4)]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    [InlineData(1, 4)]
    [InlineData(2, 3)]
    [InlineData(2, 4)]
    [InlineData(3, 4)]
    public void InsufficientTier_IsExplicitAndNeverBypassedByNaturalTwenty(int tier, int rank)
    {
        var input = Input(tier, rank, roll: 20, modifiers: 100);
        var actual = Calculate(input);
        Assert.Equal(input, actual.Input);
        Assert.False(actual.HasSufficientTier);
        Assert.Equal(SpiritualHealingResultBand.InsufficientTier, actual.ResultBand);
        Assert.Null(actual.Total);
        Assert.Null(actual.Difficulty);
        Assert.Null(actual.Margin);
        Assert.Equal(0, actual.ImprovementSteps);
        Assert.Equal(0, actual.RecoveryPointsAdded);
        Assert.Equal(rank, actual.ResultingSeverityRank);
        Assert.False(actual.HealsWound);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(5, 1)]
    [InlineData(5, 2)]
    [InlineData(5, 3)]
    [InlineData(5, 4)]
    public void MatchingTierAndTierFive_AreSufficient(int tier, int rank)
    {
        var actual = Calculate(Input(tier, rank, roll: 20));
        Assert.True(actual.HasSufficientTier);
        Assert.Equal(SpiritualHealingResultBand.ReduceTwo, actual.ResultBand);
        Assert.Equal(Math.Min(2, rank), actual.ImprovementSteps);
        Assert.Equal(Math.Max(1, rank - 2), actual.ResultingSeverityRank);
        Assert.Equal(rank <= 2, actual.HealsWound);
    }

    [Fact]
    public void LongAuditBoundaries_RemainExactWithoutClamping()
    {
        var low = Calculate(Input(tier: 1, rank: 1, roll: 2, modifiers: long.MinValue + 8));
        Assert.Equal((long?)(long.MinValue + 12), low.Total);
        Assert.Equal((long?)12, low.Difficulty);
        Assert.Equal((long?)long.MinValue, low.Margin);
        Assert.Equal(SpiritualHealingResultBand.NoImprovement, low.ResultBand);
        var high = Calculate(Input(tier: 5, rank: 4, roll: 19, modifiers: long.MaxValue - 29));
        Assert.Equal((long?)long.MaxValue, high.Total);
        Assert.Equal((long?)(long.MaxValue - 18), high.Margin);
        Assert.Equal(SpiritualHealingResultBand.ReduceTwo, high.ResultBand);
        var difficulty = Calculate(Input(tier: 5, rank: 4, roll: 19,
            complications: long.MaxValue - 18));
        Assert.Equal((long?)long.MaxValue, difficulty.Difficulty);
        Assert.Equal((long?)(29 - long.MaxValue), difficulty.Margin);
        Assert.Equal(SpiritualHealingResultBand.NoImprovement, difficulty.ResultBand);
    }
}
```

- [ ] **Step 2: Observe the 44-row semantic RED before arithmetic.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~SpiritualHealingResolverTests"
```

Expected 44 executed, all fail the null-shell `Assert.IsType`; zero compile errors.
Keep exact artifact and actual messages. A compile/discovery/fixture failure is not
this RED. Complete the run before editing production.

- [ ] **Step 3: Implement the tier gate and calculation, then observe 44 GREEN.**

Replace the shell method with the complete method below. The numeric-domain and
checked-overflow gates deliberately follow their separate RED in Steps 4/5.

```csharp
    internal static SpiritualHealingCalculation? Calculate(SpiritualHealingCalculationInput input)
    {
        if (input.HealingTier < input.WoundSeverityRank)
        {
            return new(input, false, null, null, null,
                SpiritualHealingResultBand.InsufficientTier, 0, input.WoundSeverityRank, false, 0);
        }
        var total = input.ValidatedModifiers + input.NaturalRoll + 2L * input.HealingTier;
        var difficulty = input.ComplicationModifier + 10L + 2L * input.WoundSeverityRank;
        var margin = total - difficulty;
        var band = input.NaturalRoll == 1 ? SpiritualHealingResultBand.NoImprovement
            : input.NaturalRoll == 20 || margin >= 8 ? SpiritualHealingResultBand.ReduceTwo
            : margin >= 0 ? SpiritualHealingResultBand.ReduceOne
            : margin >= -4 ? SpiritualHealingResultBand.RecoveryPoint
            : SpiritualHealingResultBand.NoImprovement;
        var requestedSteps = band switch
        {
            SpiritualHealingResultBand.ReduceTwo => 2,
            SpiritualHealingResultBand.ReduceOne => 1,
            _ => 0
        };
        var steps = Math.Min(requestedSteps, input.WoundSeverityRank);
        return new(input, true, total, difficulty, margin, band, steps,
            Math.Max(1, input.WoundSeverityRank - steps),
            steps >= input.WoundSeverityRank,
            band == SpiritualHealingResultBand.RecoveryPoint ? 1 : 0);
    }
```

Run the exact Step 2 command. Expected 44/44 PASS; no compilation warnings/errors.

- [ ] **Step 4: Add 14 invalid-domain/overflow tests and observe their RED.**

Insert these methods in the same test class, without modifying the earlier tests:

```csharp
    [Theory]
    [InlineData(-1, 3, 10, 0)]
    [InlineData(6, 3, 10, 0)]
    [InlineData(3, 0, 10, 0)]
    [InlineData(5, 5, 10, 0)]
    [InlineData(3, 3, 0, 0)]
    [InlineData(3, 3, 21, 0)]
    [InlineData(3, 3, 10, -1)]
    [InlineData(3, 3, 10, long.MinValue)]
    [InlineData(0, 1, 21, 0)]
    [InlineData(0, 1, 20, -1)]
    public void InvalidDomain_RejectsBeforeInsufficientTier(
        int tier, int rank, int roll, long complications)
    {
        Assert.Null(SpiritualHealingOutcomeMath.Calculate(Input(tier, rank, roll,
            complications: complications)));
    }

    [Theory]
    [InlineData(2, long.MaxValue, 0)]
    [InlineData(2, 0, long.MaxValue)]
    [InlineData(2, long.MinValue, 0)]
    [InlineData(20, long.MaxValue, 0)]
    public void InvalidArithmetic_RejectsOverflowInEveryAuditFieldEvenOnNaturalTwenty(
        int roll, long modifiers, long complications)
    {
        Assert.Null(SpiritualHealingOutcomeMath.Calculate(
            Input(tier: 4, rank: 4, roll: roll, modifiers: modifiers, complications: complications)));
    }
```

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~SpiritualHealingResolverTests.Invalid"
```

Expected 14/14 fail Assert.Null because the interim implementation returns a
non-null diagnostic or wrapped arithmetic, not because of compilation or a thrown
exception. Do not add the gates until this command has completed.

- [ ] **Step 5: Add complete domain/checked-arithmetic gates and observe 58 GREEN.**

Insert before the existing tier-insufficiency branch:

```csharp
        if (input.HealingTier is < 0 or > 5
            || input.WoundSeverityRank is < 1 or > 4
            || input.NaturalRoll is < 1 or > 20
            || input.ComplicationModifier < 0)
        {
            return null;
        }
```

Replace only the three interim total/difficulty/margin declarations with:

```csharp
        long total;
        long difficulty;
        long margin;
        try
        {
            total = checked(input.ValidatedModifiers + (input.NaturalRoll + 2L * input.HealingTier));
            difficulty = checked(input.ComplicationModifier + (10L + 2L * input.WoundSeverityRank));
            margin = checked(total - difficulty);
        }
        catch (OverflowException)
        {
            return null;
        }
```

Run the exact Step 2 command: expected 58/58 PASS, no skipped tests or build
warnings/errors. Every audit number must be representable, even if a critical
result would otherwise choose a positive band. No broad exception catch or clamp.

- [ ] **Step 6: Run one bounded Fast, record evidence and independently review.**

```powershell
.\scripts\test-csharp.ps1 -Lane Fast
git diff --check
```

Read actual TRX counters/outcomes, build summaries, timeout/cleanup and duplicate
artifacts. Fast may stop at the existing required legacy-source failure; report
its exact name and counts. Discovery rows are not the completed-TRX Total. Count
discovery and completed rows separately and identify any arithmetic uncompleted
difference without asserting an exact identity-set difference. Any new failure
requires diagnosis. Do not change the legacy test, run an extra Fast or claim
the whole feature is GREEN.

Commit exactly the two code/test files. Parent owns plan/task evidence and has a
fresh independent reviewer inspect the whole recorded BASE..candidate diff for
Spec Compliance and Code Quality. Report source boundaries and no-GM-update
rationale above. T094/T101/#1536 stay open: this calculator has no real accepted
healing attempt, source proof, scheduler, effect mutation or player command.

## Parent self-review

- Every FR-042/044/045 numeric branch has direct tests: all insufficient pairs,
  matching tiers and tierV, all four bands' edges, signed general modifiers,
  nonnegative complication aggregate, opposite critical/numeric results and I-IV
  terminal bounds. The accepted data-model example's exact audit is covered.
- FR-043 sealed-die/modifier/target authority and all FR-046+ costs, progression,
  cycles, healing services and publication are explicitly outside this numeric
  prerequisite and stay in their existing top-level tasks. No competing resolver
  or roll generator is introduced. A returned record cannot authorize gameplay.
- The 44 valid rows comprise 8 bands, 5 formula audits, 4 natural-result cases,
  8 terminal bounds, 10 insufficient pairs, 8 sufficient tiers and 1 long-bound fact.
  The 14 invalid rows comprise 10 domain/precedence cases and 4 overflow cases.
  Parent independently parsed all 57 literal InlineData rows from this plan and
  cross-checked them with BigInt arithmetic: zero mismatches; the three additional
  long-endpoint assertions also agree. This is plan self-review, not C# evidence.
- Internal enum values appear only inside method bodies or expected string names;
  public xUnit theory signatures do not expose an internal enum. Long nullable
  audit assertions explicitly use nullable-long expected values.
- All paths/interfaces are explicit; no integration fixture or new dependency is
  needed. This plan is prepared for one later exclusive implementer; it does not
  authorize overlapping the currently active Mortal staging build/source owner.
