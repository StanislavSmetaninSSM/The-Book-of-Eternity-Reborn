# Research: Skill Reference Identity Authority

## Decision 1: Reuse `MortalLocationIdentityState.BuildConfusableKey`

- **Decision**: the authority normalizes names with the existing `BuildConfusableKey` (trim → FormKC → invariant uppercase → Cyrillic/Greek homoglyph skeleton).
- **Rationale**: the composition layer (`EffectAcceptedTurnInputComposer.ComposeSkillAcceptedRootCore`) already rejects confusable skill names using this exact function. Two different normalizers would create a seam where a name passes one layer and fails the other — the class of bug this feature exists to remove.
- **Alternatives considered**: a new skill-specific normalizer (rejected: divergent behavior); plain `ToUpperInvariant` (rejected: misses homoglyphs such as Cyrillic `е` in `Firеball`).

## Decision 2: Classification over boolean matching

- **Decision**: `MortalSkillReferenceAuthority.Classify` returns Exact / Confusable(canonical) / Unknown instead of a boolean `Contains`.
- **Rationale**: repair hints and bounded repair packets must name the exact canonical `skillName` to write; a boolean cannot supply it.
- **Alternatives considered**: keep boolean helpers and add a separate "find canonical" helper (rejected: two lookups, two sources of truth).

## Decision 3: Ambiguity fails closed without a target

- **Decision**: when several canonically distinct names collapse to one confusable key, a non-exact reference to that key is Confusable with no canonical suggestion.
- **Rationale**: choosing any of them would be silent retargeting. The canonical state itself is then inconsistent and is reported by duplicate/confusable detection elsewhere.
- **Alternatives considered**: pick the first canonical name (rejected: nondeterministic binding).

## Decision 4: Tighten only the three mastery sites

- **Decision**: mastery reference validation (`skillMasteryChanges[].skillName`), player mastery-initialization coverage, and NPC mastery-initialization coverage go through the authority; the shared parse helpers and other consumers keep `OrdinalIgnoreCase` behavior.
- **Rationale**: the other consumers (prose-delta name sets, repair routing, showcase snapshots) are detection surfaces where case-insensitive matching is the safer default; only reference-binding validation must fail closed.
- **Alternatives considered**: changing the parse helpers globally (rejected: unbounded blast radius across repair routing and prose-delta detection).

## Decision 5: NPC actor keys stay untouched

- **Decision**: `ParseNpcSkillChangesByActor`/`ParseNpcSkillMasteryByActor` actor dictionaries remain `OrdinalIgnoreCase`; only skill names inside an actor entry are classified.
- **Rationale**: NPC identity authority is governed by the NPC materialization contour (`npc_core.json` identity fields); tightening actor keys here would couple two contours and break existing cross-file flows.

## Decision 6: Dedicated repair packet kind

- **Decision**: new `mortal_skill_reference_identity_repair` packet instead of routing into `mortal_training_skill_evolution_repair`.
- **Rationale**: the training-evolution packet tells the GM to remove invalid mastery entries and re-express them as passive changes — destructive and wrong for a pure identity mismatch. The correct repair is a rename to the exact canonical name.
- **Alternatives considered**: extend the training packet (rejected: destructive guidance for a non-destructive fix).

## Decision 7: Additive identity layer instead of in-place rewrites

- **Decision**: the identity checks live in a new partial-class file (`ValidationService.SkillReferenceIdentity.cs`) dispatched from `ValidationPhases.cs`; the three legacy validators stay byte-identical and keep emitting the legacy unknown/missing codes.
- **Rationale**: (a) the legacy methods are case-insensitive *backstops* — for a confusable reference they simply stay silent, so the new layer is the single place that classifies identity, with zero double-reporting; (b) the diff touches the monolithic partial files (300–500 KB each) as little as possible — one dispatch call in an 8 KB dispatcher and a 5-line packet dispatch — which keeps review, merge, and transport simple; (c) the whole contour (three surfaces, one authority) is readable in one file.
- **Alternatives considered**: replacing the `OrdinalIgnoreCase` checks inside the legacy methods (rejected: spreads the authority across three monolithic files and multiplies the edited surface); making the legacy methods delegate to the authority (rejected for this part: same edit surface, no behavioral gain over the additive layer).
