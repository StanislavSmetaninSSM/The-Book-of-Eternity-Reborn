# Mortal Training GM Skill Evolution Spec

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1424

## Goal

Mortal World training vitrines must let the client charge resources and advance practice safely, but must not let the client author or silently mutate the mechanical level of a player skill when that level-up can change effects.

## Problem

`/training buy` currently applies a Mortal offer locally by setting player skill mastery to `targetValue`, resetting progress, and marking `masteryLeveledUp=true`. This bypasses the GM even though Mortal active skills contain authored `combatEffect` data and passive skills contain authored `structuredBonuses`.

## Requirements

- Teacher showcases remain GM-authored and source-hash checked.
- Mortal training purchases remain local for resource payment: money and current-level XP are deducted only after the offer passes validation.
- The client may locally add mastery progress for an already-known active skill when the purchase does not cross the next mastery threshold.
- When training would unlock an unknown skill or cross a mastery threshold, the client must create a pending GM request and must not locally mutate the full skill object or final mastery level.
- The pending request must include the teacher, offer, current/target values, paid costs, source cap, source snapshot hash, and enough skill context for the GM to author the updated skill.
- Teacher `sourceCap` remains authoritative: no request may target a value above the teacher's capability.
- The GM resolves a pending Mortal training evolution by following `details.targetKind`: active targets use complete updated `activeSkillChanges` plus matching `skillMasteryChanges`, while passive targets use complete updated `passiveSkillChanges` only.
- Afterlife standard training remains client-owned for this issue.

## Non-Goals

- Do not redesign afterlife spiritual art training in this issue.
- Do not add a new UI flow beyond making the existing training result explain that GM finalization is pending.
- Do not rebalance prices.
