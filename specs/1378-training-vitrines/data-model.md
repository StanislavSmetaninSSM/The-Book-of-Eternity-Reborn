# Data Model: Training Vitrines

## TrainingShowcase

Represents the current training offer surface for one teacher/mentor.

- `showcaseId`: stable id for the synced showcase.
- `realm`: `mortal_world`, `chaos_sea`, or `shining_abode`.
- `sourceActorId`: NPC/Guardian/resident/representative id.
- `sourceActorName`: localized display name.
- `sourceActorKind`: `mortal_npc`, `guardian`, `abode_resident`, `shining_representative`, or `system_self_training`.
- `title`: player-facing title.
- `summary`: short player-facing explanation.
- `syncedAtTurn`: turn number when showcase was materialized/refreshed.
- `syncedAtCycle`: optional afterlife scheduler/cycle key.
- `sourceActorSnapshotHash`: hash of the teaching-relevant actor profile.
- `relationshipSnapshot`: relationship/reputation/trust values used for pricing and locks.
- `playerProgressionSnapshot`: level, current-level XP progress, Enlightenment/progression caps, known skills/arts.
- `offerRevision`: monotonically changing revision.
- `status`: `fresh`, `stale`, `needs_refresh`, `blocked`.
- `offers`: list of `TrainingOffer`.

## TrainingOffer

Single learn/upgrade option.

- `offerId`: stable within showcase.
- `targetKind`: `mortal_skill`, `spiritual_art`, `spirit_focus`, or `special_spiritual_art`.
- `targetId`: skill/art/focus id.
- `targetName`: localized display name.
- `actionKind`: `learn`, `upgrade`, or `self_upgrade`.
- `currentValue`: player's current mastery/tier.
- `targetValue`: value after purchase.
- `sourceCap`: teacher/mentor maximum value.
- `playerProgressionCap`: cap from level, Enlightenment, or other progression.
- `quality`: optional rarity/discipline/category label.
- `description`: player-facing learning description.
- `requirements`: list of `TrainingRequirement`.
- `cost`: `TrainingCost`.
- `lockState`: `available`, `locked`, or `stale`.
- `lockReasons`: localized reasons.
- `gmNotes`: optional GM-only audit, never shown as primary player text.

## TrainingRequirement

Human-readable and machine-checkable requirement.

- `kind`: `relationship`, `quest`, `flag`, `faction`, `level`, `enlightenment`, `known_skill`, `known_art`, `resource`, or `story_source`.
- `label`: localized label.
- `requiredValue`: required threshold/value.
- `currentValue`: current threshold/value.
- `isMet`: boolean.
- `summary`: localized explanation.

## TrainingCost

Resources consumed by purchase.

- `money`: Mortal currency.
- `currentLevelXpPercent`: Mortal current-level XP progress percent.
- `inkFeathers`: afterlife currency.
- `lightSparks`: afterlife currency.
- `otherCurrencies`: explicit localized entries only when validated.
- `baseCost`: optional reference cost for afterlife arts.
- `multiplierPercent`: price multiplier after relationship/fallback rules.
- `summary`: localized compact cost text.

## TrainingPurchaseReceipt

Canonical proof of a successful client-owned purchase.

- `receiptId`: unique id.
- `showcaseId`: source showcase.
- `offerId`: purchased offer.
- `realm`: active realm.
- `sourceActorId`: teacher/mentor/self-training source.
- `targetKind`: purchased target kind.
- `targetId`: purchased target id.
- `beforeValue`: pre-purchase mastery/tier.
- `afterValue`: post-purchase mastery/tier.
- `deductions`: exact resource deductions.
- `snapshotHash`: source actor snapshot hash used by the purchase.
- `purchasedAtTurn`: turn number.
- `summary`: localized receipt summary.

## TrainingRefreshRequest

Pending/control request for GM refresh.

- `requestId`: unique id.
- `realm`: active realm.
- `sourceActorId`: requested teacher/mentor id or empty for nearby teachers.
- `reason`: `missing_showcase`, `stale_showcase`, `player_request`, `relationship_changed`, `progression_changed`.
- `playerIntent`: player-facing request text.
- `createdAtTurn`: turn number.
- `status`: `pending`, `closed`, `cancelled`.
- `expectedClosure`: required GM fields/receipt for showcase refresh.

## Relationships

- One teacher/mentor may have many showcases over time, but only one fresh showcase per actor and realm should be purchasable.
- One showcase has many offers.
- One purchase receipt consumes one offer from one fresh showcase.
- Refresh request creates or replaces showcase data; purchase never edits teacher/mentor source capability.
