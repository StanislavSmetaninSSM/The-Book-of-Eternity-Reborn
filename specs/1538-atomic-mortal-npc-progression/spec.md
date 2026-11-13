# Atomic Mortal NPC progression

Source: [#1538](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1538).
Base: main `7c8bb04a43f40939a9c78b10245d78283bd5805d`.
Status: implementation design, 2026-11-13. The owner delegated routine design decisions; the coordinator authorized proceeding with the explicit-profile approach.

## Intent and scope

An existing Mortal NPC gains or loses XP through one bounded `NPCCoreChanges.progression` package. The complete resulting tuple and every explicitly governed consequence must be valid together before publication. Reuse the current validated pre-turn snapshot, exact actor identity, canonical composition, transaction, rollback and receipt mechanisms. Do not introduce another progression writer or infer mechanics from a name, class, genre, prose, or display tag.

Afterlife/Guardians/residents, spiritual combat, old-save migration and new companion systems are excluded. #1533 and #1535 are closed dependencies. Their skill/effect authority remains authoritative; this task coordinates consequences, not replacement implementations.

## Design choices

Chosen approach: extend the existing NPC command and actor-owned explicit canonical progression profile. A separate service that mutates NPC XP is rejected because it would duplicate publication and rollback. Prompt-only enforcement is rejected because it cannot prevent partial accepted transitions.

NPC XP is within the current level, per Block 19.8.3; never reuse the player's cumulative-threshold semantics. A profile explicitly selects the existing Block 5.10.A.1 formula or an explicit finite threshold table. No hidden formula fallback. Exact threshold uses `>=`, including recursive multi-level transitions. A cap has an explicit terminal representation and overflow policy. Negative XP is rejected unless the profile explicitly permits it; losses do not silently delevel an actor.

A minimal non-progressing NPC is allowed; no fabricated level-ups or mandatory capabilities. Existing progressionType remains the actor's narrative role, separate from the mathematical profile. Profile changes are explicit bounded transitions, not implicit reinterpretation of old XP.

## Requirements

- FR1 Validate one complete absolute tuple whenever level, experience, threshold, profile or progression type changes. Numeric values must be JSON integers in supported range; reject negative/overflow/fractional values and incoherent baselines.
- FR2 Resolve an explicit canonical profile and independently compute the result from authenticated original plus declared XP delta. Deterministically cover no gain, exact threshold, multiple levels, cap, allowed loss and rejected underflow/overflow.
- FR3 Bind the transition to exact current-world Mortal NPC and current source turn. Unknown/confusable/wrong-actor/cross-realm/stale targets and duplicate commands fail closed. Repeated accepted transitions cannot grant XP or companions again.
- FR4 A profile declares mechanically required consequences, including skill/mastery cadence and setting-owned characteristics. Current Block 19 skill cadence is represented explicitly rather than inferred from class. Required exact references resolve through existing skill/effect owners.
- FR5 Explicitly governed changes to combat, trade/teacher authority, rewards, goals/activity or journals must be present in the composed state. Where an existing consumer derives values from current level, validate that consumer instead of inventing redundant stored data. Stale trade/teacher offers must remain unusable under existing freshness rules; do not regenerate merchandise or award loot merely because level changed.
- FR6 Validate the complete dependency package before accepting, preserve original ownership/lease/source fences, and use the existing all-or-nothing transaction. Faults restore actor, companions, journals, outputs and receipts.
- FR7 Canonical NPC creation validates the new profile and tuple. Current supported fixtures/templates are updated; no historical-save fallback is introduced.
- FR8 Console/browser show accepted public progression mechanics, not private receipt, snapshot or transition identifiers.
- FR9 Rules, daemon/repair guidance and worked examples match the real implementation. Bounded indexed repair diagnostics name the actor and missing/correctable fields.

## Verification and success

Use cheap pure typed arithmetic/profile tests first, then genuine existing-NPC public validation/composition tests. Cover ordinary NPC, merchant, teacher, combatant, non-progressing NPC and explicit setting table. Include negative identity/source/dependency cases and real rollback injection through existing publication. Use only impact-selected categories and discovery validation; no full suite, blanket historical replay or hosted CI. Each block needs causal RED, GREEN, independent review, source/PDB matching when compiled, and a remote checkpoint. Completion requires all FR1-FR9 mapped to evidence; partial blocks never imply whole-issue completion.

## P1 concrete numeric contract

Actor-owned `progressionProfile` is closed and versioned:
`schemaVersion: 1`, `mode: standard | table | none`.
Progressing profiles may explicitly set `allowExperienceLoss` (default false),
`maxLevel`, and `capOverflow: discard`. A table has a nonempty array `thresholds`:
entry L-1 is the positive integer XP cost to leave level L; its terminal cap is
array length + 1, optionally shortened by maxLevel. Every cap requires explicit
discard policy. An uncapped standard profile cannot carry capOverflow. The
standard cost is exact floor(sqrt(10000 * L^5)), equivalent to the documented
formula without floating-point rounding authority. Unsupported overflow fails.

Progressing tuples use Int32 integers with level >= 1 and 0 <= experience < the
current cost. At cap, experience and threshold are both 0. Overflow is rejected
before excess is discarded. Allowed negative delta can reduce current-level XP
only, never silently delevel. `none` accepts no other options, preserves its
nonnegative level, and requires zero XP, threshold and delta. Empty/missing or
unknown profiles are not authority for progression.

The progression command supplies signed Int32 `experienceDelta` and all three
absolute result fields. A pure evaluator compares them to the computed result
without mutating either input. Profile installation/change, owner/realm/source
binding, declared consequence fields and receipts belong to P2; the numeric
profile alone is never publication permission. P1 does not implicitly migrate
old actors or require profiles for unrelated non-progression changes.
