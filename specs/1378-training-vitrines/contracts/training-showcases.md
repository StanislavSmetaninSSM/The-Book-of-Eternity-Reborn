# Contract: Training Showcases

## Authority

- The GM authors or refreshes `TrainingShowcase` data through documented response fields.
- The client owns local purchase execution from a fresh showcase.
- The validator owns legality checks for offers and receipts.
- The player-facing UI must never rely on raw JSON as the primary explanation.

## Mortal World Flow

1. Player opens `/обучение`.
2. If a fresh teacher showcase exists in the current scene/location, the client renders offers.
3. If no fresh showcase exists, the client creates or reuses `TrainingRefreshRequest` and immediately dispatches the dedicated GM action for that request. This must not wait for the player to close the command screen, press a key, or type the next ordinary turn input.
4. GM closes the request by materializing teacher data and offers.
5. Player purchases an available offer.
6. Client deducts money and current-level XP progress, updates skill/mastery locally only when the lesson is pure mastery practice below a threshold, and writes `TrainingPurchaseReceipt`.
7. If a paid lesson unlocks a skill or crosses a mastery threshold, the client writes `mortal_training_skill_evolution` and immediately dispatches that dedicated GM action. This must not wait for the player to close the command screen, press a key, or type the next ordinary turn input.
8. Validator rejects the receipt if the showcase is stale, the offer is illegal, resources do not match, or the cap is exceeded.

## Afterlife Flow

1. Player opens `/обучение` or `/духовные_искусства` mentor-training section.
2. Client renders active mentor offers when fresh.
3. If a mentor showcase is missing or stale, the client creates or reuses `TrainingRefreshRequest` and immediately dispatches the dedicated GM action for that request without requiring the player to close the command screen or press a key.
4. Self-training fallback is always visible for legal already-known standard art/focus upgrades, with high multipliers.
5. New special-art unlocks are hidden or locked unless a mentor/story/Shining source exists.
6. Purchase writes receipt and deducts afterlife currencies.

## Required Validation Rejections

- Wrong realm showcase or receipt.
- Missing source actor.
- Source actor does not have teacher/mentor capability.
- Offer target missing from teacher/mentor profile.
- Offer source cap exceeds actor capability.
- Purchase target value exceeds source cap or player progression cap.
- Stale actor snapshot, relationship snapshot, player progression snapshot, or offer revision.
- Negative or zero-invalid cost.
- Resource deduction mismatch.
- Fallback unlock of a new special Spiritual Art.
- Receipt without matching fresh showcase and offer.

## Player-Facing Rendering Contract

- All labels are localized Russian.
- Internal ids are secondary and dim only where useful for debugging a support report.
- Costs and requirements are separated into readable sections.
- Nested data uses nested cards in browser and panels/sections in console.
- Lists use bullets/cards, not semicolon-packed strings.
- Large collections use selectors or collapsible sections.
