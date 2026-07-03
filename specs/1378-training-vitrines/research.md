# Research: Training Vitrines

## Decision 1: Treat training as a showcase plus client-owned purchase

**Decision**: The GM materializes or refreshes the teacher/mentor showcase. The client completes purchases locally only when the showcase is fresh and the offer is legal.

**Rationale**: This mirrors shops: the interesting narrative work belongs to the GM, while repeated purchase clicks should not require a full GM turn. It also makes training feel like a game system rather than hidden prompt behavior.

**Alternatives considered**:
- Pure roleplay training only: rejected because the user wants a visible game-like learning surface.
- Fully client-generated offers from NPC stats: rejected for v1 because GM narrative requirements, relationship gates, and special-art sources need authored context.

## Decision 2: Preserve free roleplay training

**Decision**: Roleplay training remains legal. A GM may grant skills/arts through scenes using explicit learning receipts, but the vitrine provides a convenient deterministic path.

**Rationale**: The game is freeform; the vitrine should not make creative scenes illegal.

## Decision 3: Mortal training spends money plus current-level XP progress

**Decision**: Mortal offers require money and an XP-progress percent of the current level budget. Spending cannot delevel the character.

**Rationale**: Money alone turns training into a shop. XP progress makes learning compete with level progression while keeping cost scale stable across levels.

**Implementation note**: If current-level progress is not explicit in existing state, add a safe canonical field or compute it from existing XP thresholds. If neither exists reliably, block XP-spending purchases and surface the missing authority as validation/harness feedback.

## Decision 4: Teacher cap is hard

**Decision**: An NPC teacher cannot raise a player above the NPC's own skill mastery/cap, and cannot teach skills absent from the teacher profile.

**Rationale**: This makes teachers meaningful and prevents cheap arbitrary progression.

## Decision 5: Afterlife mentor training discounts standard Spiritual Arts

**Decision**: Mentor training uses base cost at neutral relation, 80% at good relation, and 60% at excellent relation or completed personal trust.

**Rationale**: The player should prefer finding teachers/mentors. Strong relationships become mechanically valuable without removing fallback.

## Decision 6: Self-training fallback remains but is intentionally expensive

**Decision**:
- Standard Spiritual Art self-upgrade: 400% base cost.
- Soul Focus/base AP capacity self-upgrade: 300% base cost.
- Already-known special art self-upgrade: 500% base cost.
- New special art unlock: never through fallback.

**Rationale**: Fallback prevents hard blocking when no mentor is available, but mentor/story sources should be the desired path. Special arts need identity/source, so new unlocks require mentor/story/reward/Shining source.

## Decision 7: Staleness metadata is mandatory

**Decision**: Every showcase carries realm, source actor id, actor snapshot hash, relationship/reputation snapshot, player progression snapshot, synced turn/cycle, and offer revision.

**Rationale**: The client owns purchase execution after sync. Old offers must not survive actor, relationship, progression, or economy changes.

## Decision 8: Browser rendering must use the approved data-card prototype

**Decision**: Training browser UI must use selector-driven entity choice, nested cards, localized labels, collapsible large sections, readable lists, and image support where applicable.

**Rationale**: The user explicitly rejected table/serialized-data rendering across the browser client.
