# Mortal Training GM Skill Evolution Tasks

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1424

- [x] Add failing tests proving Mortal `/training buy` creates a pending GM request instead of locally raising a skill level when the offer crosses the mastery threshold.
- [x] Add failing tests proving an unknown Mortal skill unlock creates a pending GM request and leaves skill files unchanged.
- [x] Add failing tests proving a GM-satisfied skill-evolution request is cleared after the updated skill/mastery state appears.
- [x] Implement the pending request payload and helper methods in the training service layer.
- [x] Update Mortal training purchase logic to deduct resources, append receipts, and branch between local progress and GM-owned evolution.
- [x] Clear stale paid skill-evolution requests when the GM-authored skill/mastery state satisfies the request.
- [x] Update console/browser player-facing training result text so pending GM finalization is clear and not technical.
- [x] Update GM-facing Mortal skill/training prompt templates and examples.
- [x] Update validation/documentation tests for the new request kind and contract.
- [x] Run focused and documentation-sensitive verification.
