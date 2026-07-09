# Mortal Training GM Skill Evolution Plan

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1424

**Status**: Superseded by `specs/1378-training-vitrines/`. Ordinary first
Mortal skill purchases from a fresh showcase are now local client-owned unlocks;
this plan remains historical background for threshold/effect-change requests.

## Architecture

Extend the existing `pending_training_showcase_requests.json` surface with a second Mortal request kind for skill evolution. Keep showcase refresh requests and skill evolution requests in one file because both are GM training work packets, but make the request kind and reason explicit.

`TrainingService.BuyTrainingAsync` will keep the same entry point. For Mortal offers it will:

1. Validate source, offer freshness, cap, relationship, money, and XP exactly as before.
2. Deduct money and current-level XP.
3. If the offer can be applied as simple active-skill progress without crossing the threshold, update only `skill_mastery.json`.
4. Current 1378 contract update: if the offer is an ordinary first-skill purchase from a fresh showcase, add the skill locally at mastery level 1. Only if the paid lesson crosses the threshold or needs new/changed mechanical effects, append a pending GM skill-evolution request, append a receipt, and leave effect-changing skill state to the GM response.
5. When the GM has written the updated skill/mastery state and the target level is present, clear the matching pending skill-evolution request so the player is not blocked by stale paid-training work.

## Data Contract

New request kind: `mortal_training_skill_evolution`.

The request carries:

- `sourceActorId`, `sourceActorName`, `sourceActorKind`, `realm`
- `sourceActorSnapshotHash`
- `offerId`, `targetId`, `targetName`, `targetKind`
- `currentValue`, `targetValue`, `sourceCap`
- `moneySpent`, `currentLevelExperiencePercent`, `currentLevelExperienceSpent`
- `reason`: `mastery_threshold_crossed`; legacy saves may still contain `unknown_skill_unlock` and should be cleaned up only when satisfied by player skill state.
- `skillStateBefore`: best-known current active/passive skill and mastery snapshot
- `gmInstruction`: concise Russian instruction for the GM

The request remains open until the player state contains the target skill and a matching mastery level at or above `targetValue`.

## Verification

- Focused unit tests for Mortal training purchase behavior.
- Focused web command test for readable pending-finalization output.
- Documentation coverage tests for GM-facing contract updates where applicable.
- Existing afterlife training tests must remain green.
