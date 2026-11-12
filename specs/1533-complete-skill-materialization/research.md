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

## Review correction decisions (supersede assumptions above)

- The legacy OrdinalIgnoreCase checks are silent for case variants but NOT Unicode homoglyphs. The identity layer removes only the corresponding legacy unknown/missing diagnostic after proving a confusable match; other issue paths and invalid fields stay intact. This prevents the training-evolution packet from receiving the same typo.
- Existing parsers retain their default comparer and actor dictionary behavior. Optional skill comparers allow identity validation to preserve every exact name without duplicating JSON parsers. Pre/post new-skill detection remains case-insensitive.
- Repair coordinates use actual mastery array indices; expected/actual are literal names only when uniquely resolved. Distinct diagnostics sharing a legacy path are retained. Ambiguous expected text is descriptive, never a replacement string, and the packet requires stopping rather than guessing.
- Source guards read the deterministic logical partial family. Roslyn mutation fixtures edit the method's actual syntax tree. Test assertions and discovery of unsupported publication paths are preserved.
- The GM worked example documents correction packets rather than a complete executable turn. It is covered by the identity documentation test; it is intentionally not registered as a full-state fixture in example_validation_manifest.json.

## Remaining-slice audit (2026-11-11, local continuation)

- T105 confirmed: `CharacteristicsService.CollectPassiveSkillBonuses` called its sole legacy text parser when structuredBonuses was absent/null/empty/malformed. Valid passive profiles with display-only `playerStatBonus="+5 strength"` granted +5; the explicit structured +2 control remained +2. Part 6 removes only this fallback and resolves contradictory Block_12/14 guidance. Existing #1535 source-loss/replacement and scoped-effect authority are retained, not duplicated. Static bonus object schema and NPC core-versus-delta effect ownership are separate unclosed investigations.
- T103 confirmed: `ValidatePlayerSkillMastery` and identity validation union pre-turn names and look for `removeActiveSkills` on the mastery root. Exact removal and same-ID rename therefore retain stale mastery authority while a valid snapshot exists, both before and after production skill composition. Closing snapshot/request metadata alone rejects the same canonical bytes. Existing removal selector matching is already exact/confusable-safe and should not be rewritten.
- T103 confirmed independently: real `SaveLoadService` saves and reloads orphaned mastery; subsequent PlayerStateFiles validation rejects it. Save admission currently validates resources; archive admission validates soul/resources, not this closure.
- Smallest T103 seam: side-effect-free validation of parsed current active/mastery roots at the existing leased post-normalization canonical validation boundary, then reuse before save/archive publication. Do not remove pre-turn union indiscriminately or recompose normalized roots: removal selectors are consumed and recomposition can resurrect an old skill. Raw mastery-only updates must preserve retained skills. No new persisted stage flag/cache is needed. This is a next slice, not implemented by Part 6.
- T106: launchers and TaskGuide already state exact canonical spelling, and E_CLI_Skill_Reference_Identity_Repair already demonstrates safe indexed repairs. The compact generated MORTAL_SKILL_PROGRESSION_TEMPLATE in game_master_daemon.ps1 omits exact case/Unicode and safe identity-repair details. A bounded generated-template/authoring-route guard is warranted; no new runtime identity policy is needed. T106 remains open.

Part 7 implemented the confirmed ordinary accepted-refresh and save/archive admission defects. Independent review identified a separate **unverified pre-existing QTE hypothesis**: `QteSceneService.ApplyTerminalOutcomeValidatedStateChangesAsync` normalizes response-fragment skill changes, then calls snapshot-aware general validation around line 2077 without this ordinary refresh gate. It warrants a causal typed reproduction before any additional change; this checkpoint does not claim QTE closure. Joint publication is not a demonstrated bypass: its mechanical-admission guards preserve skills/mastery against the signed original. Do not expand the current slice or change raw-delta semantics on this hypothesis alone.

## Original #1533 criteria after the bounded QTE check (2026-11-11)

Source: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533, read live during this audit. The previously unverified QTE hypothesis is **confirmed and resolved by Part 9**: real terminal removal/replacement accepted stale mastery before the fix; the same permanent cases now reject and restore exact prior state. The shared current-only check also cannot be waived for an already invalid mastery reference. No additional runtime defect was established by this bounded audit.

- Starter/player and first-NPC completeness: shared profile validators and exact initialization gates/tests exist (Parts 1/3/4/5). This audit did not establish every complete starter/first-NPC route end to end.
- Training, reward, quest, item, Fate Card, combat and level-up routes: central composition/profile contracts exist, including changed NPC/Fate Card profiles. A complete route-by-route acceptance matrix was not proven here. This is a verification boundary, not evidence of a defect or a new subsystem requirement.
- Prose-only learning/mastery: T107 has implemented per-skill delta coupling with unchanged-entry, unreadable-source, exact identity and passive-progression controls; see its existing ledger.
- Unknown/duplicate/confusable/cross-owner/stale references: the recorded scoped gates cover these through exact identity, current-state duplicates, NPC owner/kind checks, ordinary/QTE player canonical closure and save/load admission. Broad unchecked historical headings do not independently demonstrate another remaining bug.
- Removal/replacement closure: player mastery is now verified at ordinary and QTE acceptance and save/load. Existing #1535 effect source-loss/replacement coverage is retained. Exhaustive combat/quest/UI-reference closure was not established by this audit; no new defect in those surfaces was reproduced.
- Partial legacy saves: the original issue's migration request is superseded by the accepted current pre-release spec and repository guardrail. Legacy state is unsupported; do not add migration merely to satisfy the older wording.
- Console/browser parity and authority privacy: no new concrete defect established, but no whole-criterion parity/privacy proof from this bounded work.
- Focused regression evidence: prior Parts 7–8 have 220/220 selected cases; Part 9 separately has 16/16 targeted QTE/ordinary-closure/restoration cases. These are scoped runs at their recorded source revisions, not a full-suite or whole-issue qualification.
- GM rules/examples/manifest guards: scoped synchronization is implemented, including actual generated daemon rendering under Linux PowerShell 7. Windows PowerShell 5.1 remains unrun; unrelated launchers and existing examples were not needlessly rewritten.

Objective conclusion: the specific QTE acceptance defect is fixed locally with no new system. Current evidence supports the documented slices, but does not justify closing all original #1533 criteria. Unproven broad criteria are not automatically an implementation backlog; further work needs concrete evidence and a bounded question. Publication/recovery remains separately blocked and was not retried.
