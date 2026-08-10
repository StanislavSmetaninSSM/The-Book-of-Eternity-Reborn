# Research: Authoritative Saref Story Materialization

## Scope and evidence reviewed

Research covered the current Saref runtime (`SarefMainStoryState`, its validator, player projections, and example 27), built-in Guardian bootstrap and attraction, Guardian quest validation/normalization, Actor Materialization from #1500, the in-progress Faction Materialization contract from #1510, the planned location authority in #1514, all ten Guardian questline bibles, Saref's character bible, GM turn reminder assembly, console/browser command registration, and repository test-lane policy.

The game has not shipped and there are no production saves. Every decision below therefore targets one strict current schema. Repository fixtures and examples are converted; product code does not retain receipt-less, case-insensitive, alias, or migration branches.

## R1 — Separate immutable story knowledge from mutable materialized entities

**Decision**: Introduce a packaged-only `SarefStoryCatalogService`. The complete plot, ten Guardian definitions, forty fixed quest definitions, reward bindings, Saref template, and Wings template exist eagerly as immutable application content. Guardians, quest progress, Saref, and the Wings remain lazy canonical game-state entities.

**Why**: `StoryService.BuildStoryContext()` summarizes the mutable campaign log and cannot make an undiscovered authored plot known before an actor exists. `SystemGuardianLibraryService` knows individual built-in manifests/dossiers but not the complete cross-Guardian line. Making canonical actors eagerly would create mutable state before play needs it and would conflate GM knowledge with player-world existence.

**Alternatives rejected**:

- Load story only when a Guardian materializes: reproduces the failure the user identified.
- Put the full ten-line prose into every turn: wastes prompt budget and raises drift risk.
- Materialize every story actor/faction on New Game: creates unnecessary mutable entities and conflicts with lazy world materialization.

## R2 — Package layout and immutable override policy

**Decision**: Store the cross-story catalog and principal templates under `BookOfEternityClient/story_content/saref/`; store each Guardian's materialization and questline JSON beside its existing packaged built-in manifest/dossier:

```text
story_content/saref/catalog.json
story_content/saref/saref_actor_materialization.json
story_content/saref/wings_faction_materialization.json
system_guardians/built_in/<preset>/guardian_materialization.json
system_guardians/built_in/<preset>/saref_questline.json
```

The story loader reads only application-packaged paths. User preset overrides may continue for ordinary system-Guardian presentation, but cannot replace story authority, exact IDs, quest definitions, rewards, or templates.

**Why**: This reuses the existing shipping path for built-in Guardians while keeping one cross-story entry point. User override support is valuable for generic presets but is unsafe for a fixed hidden-story contract.

## R3 — Deterministic catalog digest

**Decision**: `catalog.json` declares an exact ordered `contentFiles[]` inventory and `catalogDigest`. Verification sorts normalized relative paths with `StringComparer.Ordinal`, canonicalizes every JSON object by recursively sorting property names, preserves array order, removes only `catalogDigest` from the catalog document to avoid a cycle, and hashes the following UTF-8 sequence for every file:

```text
normalized/relative/path\n
<canonical-json>\n
```

The stored value is `sha256:<64 lowercase hexadecimal characters>`.

**Why**: File bytes, indentation, line endings, and JSON property order must not change semantic identity. Paths are included so content cannot be swapped between templates without changing the digest. An explicit inventory also proves there are no unbound extra definitions.

**Alternatives rejected**:

- Hash raw files: produces platform/formatting-only changes.
- Hash only `catalog.json`: does not protect referenced templates.
- Generate a digest into mutable game state at runtime: turns installation content into game authority and permits accidental rebinding.

## R4 — Cached all-realm GM context

**Decision**: Validate and parse the complete catalog once, cache typed content and pre-rendered private fragments, and append a compact index to every Mortal World, Chaos Sea, and Shining Abode GM reminder through a dedicated `SarefStoryContextComposer`. Do not overload the mutable campaign `StoryService`.

The compact fragment contains all ten exact Guardian IDs, all forty quest IDs/titles/ordinals, q4 revelation and advantage roles, scope/lifecycle rules, and instructions for exact full-package selection. Its UTF-8 size is capped at 32 KiB by a build/runtime assertion; overflow is a content-integrity failure, never truncation.

Full Guardian/questline packages are attached when exact relevance is established by the current player action/request, story progress, latent trace, pending Guardian creation/attraction, active Guardian, Saref/Wings action, or referenced catalog ID. References are deduplicated. If several distinct lines are genuinely relevant, every one is included; no silent first-match or arbitrary cap is allowed.

**Why**: `GameEngine.TurnLifecycle.BuildTurnSystemReminderAsync` already assembles independent reminder fragments. A separate fragment preserves authority boundaries and works before any story actor exists. Caching avoids reparsing more than twenty content files per turn.

## R5 — Current Saref progress needs a closed catalog-bound schema

**Decision**: Move `main_story_saref_state.json` to current schema version 2. Every New Game writes an empty story root with an exact `catalogBinding`. Catalog-bound identity comparisons use `StringComparer.Ordinal`.

`guardianQuestlines[].questStates[]` becomes the only mutable story-quest lifecycle authority. `latentTraces[]` is append-only discovery evidence; it no longer carries a competing mutable status. A recognition transition changes quest state but preserves its trace.

**Why**: The current schema accepts arbitrary Guardian and quest IDs, compares them case-insensitively, and can treat `latentTraces` and quest state as overlapping progress. Exact catalog membership and one mutable status are required to reject forged rewards and cross-links.

**Migration decision**: There is no schema-1/runtime fallback. New Game and all repository fixtures are rewritten to schema 2. Missing state, missing binding, old schema, or a digest mismatch fails closed.

## R6 — Typed strict story lifecycle

**Decision**: Add `sarefMainStoryUpdate.mode=advance_guardian_quest` with exact catalog binding, Guardian ID, quest ID, expected status, target status, realm, turn, evidence, and request authority where required.

The lifecycle is strict:

```text
absent -> latent -> recognized -> active -> ready_to_turn_in -> completed
```

No stage may be skipped. A direct encounter with an exact materialized Guardian may supply the evidence for `absent -> latent`, but recognition remains a later validated transition with the full package. q1–q3 use ready-to-turn-in and afterlife hand-in. Quest 4 is the only structural exception: it closes from `active` through the existing successful `record_memory_scene` composite, which writes the completion proof and registered reward rather than fabricating a separate ready-to-turn-in handoff.

**Why**: A single typed transition gives validation enough evidence to enforce ordering, player acceptance, realm boundaries, and exact rewards. Treating a direct Guardian meeting as valid latent discovery evidence preserves natural play without adding an unapproved skip edge.

## R7 — Guardian quest offer and acceptance are incomplete today

**Finding**: Current Guardian panels render `availableQuests` and `activeQuests`, and the validator says an available quest must be accepted before completion, but there is no console/browser command or normalizer command that performs `available -> active`. New ordinary offers also rely on direct canonical quest-array authoring rather than a narrow command.

**Decision**: #1521 closes both harness gaps:

1. `UpdateGuardians.offerQuest` creates only complete `storyScope=non_story` offers. It validates identity, grounding, origin, source tokens when applicable, cap, and difficulty before changing one Guardian.
2. `/guardian_quest_accept` and `/принять_квест_хранителя` create `game_state/control/pending_guardian_quest_acceptances.json` from an exact current available quest. Console and browser expose equivalent selection and result behavior.
3. An accepted non-story request authorizes `UpdateGuardians.acceptQuest`, which moves the exact immutable offer snapshot to active.
4. An accepted catalog story request authorizes `advance_guardian_quest recognized -> active`; the story projector moves the derived view. `UpdateGuardians.acceptQuest`, `guardianQuestProgressUpdates`, and `UpdateGuardians.completeQuest` reject story quests.
5. A rejected/stale request records a bounded resolution and changes no quest authority.
6. The new file is registered as client-owned in `AfterlifeContractRegistry`, the pending-control inventory, validated snapshot/client-owned filters, Soul Gates blockers, accepted-resolution cleanup, daemon prompt routing, and their source guards.

**Why**: Free-form prose cannot reliably prove which offer the player accepted, and direct full-array edits are broader than necessary. A client-owned request makes the bad transition unrepresentable, supplies exact pre-turn authority, and gives console/browser parity.

**Alternatives rejected**:

- Parse the player's natural-language action: ambiguous across languages and names.
- Mutate canonical arrays immediately in the client: bypasses the accepted-turn snapshot and narrative/repair lifecycle.
- Treat all Guardian quests as Saref story transitions: would let ordinary generated content counterfeit the fixed line.

## R8 — One quest classification and one title shape

**Decision**: Every current-schema Guardian quest in `availableQuests`, `activeQuests`, `completedQuests`, and Guardian actor-profile projections must carry exactly one `storyScope`: `saref_main_story` or `non_story`. Canonical display text uses `title`; the UI is updated accordingly, and current fixtures are converted instead of preserving `name`-only compatibility.

Catalog membership plus `storyScope` determines authority. Prose, title, or ID shape never does. Fixed catalog quests always use `questOrigin=saref_main_story_catalog`. GM-authored quests use `non_story`, a supported ordinary origin, complete objective/success authority and reward outline, and cannot use any catalog identity.

Story and non-story offers share the existing Guardian available-quest cap and difficulty ceiling. A recognized story quest that cannot currently be offered remains recognized in story authority and is projected when eligible. New offers are rejected when over cap/ceiling. Already active and historical quests are never invalidated solely because Abode power later falls. Player-facing console and browser views render `non_story` as the Russian in-world marker `Несюжетный квест`; the raw enum remains internal.

**Why**: A closed classification prevents post-q4 generated quests from changing Saref progress. Standardizing the display field closes an existing `title`/`name` mismatch between fixtures and UI.

## R9 — Atomic Guardian materialization and derived story projections

**Decision**: Intercept exact story-backed built-in selection, attraction, and story appearance in `SystemGuardianLibraryService`. Stage and validate one bundle containing:

- complete `guardians.json` entry and active-Guardian mirror when applicable;
- matching common `actorType=guardian` afterlife profile with existing Actor Materialization envelope;
- one deterministic append-only initial entry in `guardian_thought_journal.json`;
- exact catalog/template and #1514 location bindings;
- any recognized/active/completed story projections derivable from the one story state.

Publish all changed roots only after every proposed root validates; on failure retain every pre-turn value. Retrying the same materialization is idempotent and cannot duplicate memory or rewrite a receipt.

The story projector is the only writer of fixed quest copies in Guardian quest management and profile `personalQuests`. GM output writes the typed story transition, not three coordinated copies.

**Why**: The current built-in bootstrap produces a useful but generic Guardian/profile shell. Fixed story characters need authored identity and agency, and multi-root partial writes would leave an impossible actor.

## R10 — Saref and Wings build on generic materialization contracts

**Decision**: Add exact common profile type `saref` and actor ID `saref_001`; keep `saref_agent` for supporters. Saref uses the #1500 receipt and profile schema, with actor-owned memory in meaningful `gmThoughtsSummary`.

Materialize `shine_faction_wings_of_angels_001` only through #1510's Shining `story` route, with exact `saref_main_story` authority, complete receipt, lifecycle/charter/leadership, strategic memory plus chronicle, and a #1514 hall reference. Replace old values only in identity-bearing `factionId`/`wingsFactionId` fields; preserve the valid `factionRole=wings_of_angels` value and player command aliases.

Both templates are available privately to the GM from the first turn but become mutable canonical actors only on an authorized story materialization transition. The first persistent Saref action or Wings structural influence must materialize the entity no later than that accepted turn; prose alone never creates identity.

**Why**: Reusing generic actor/faction/location authority avoids parallel schemas while preserving exact story-specific IDs and visibility.

## R11 — Visibility, repair, and local observability

**Decision**: Apply reveal filtering from canonical story state at the projection boundary. GM-private catalog, template contents, receipts, private truth, and hidden entity identity never enter ordinary console/browser DTOs. Console and browser share the same projection service and Russian labels.

Catalog corruption is an installation/content error, not a GM repair target. Current-schema binding mismatch or old fixtures fail closed and require a reset/fixture correction. Runtime repair packets may target only exact canonical state roots and JSON pointers for a valid catalog-bound transition. Multi-root proposals are staged and semantically compared against protected pre-turn roots before publication.

Log locally only: catalog ID/version/digest, successful one-time load, compact-index byte count, selected exact package IDs/count, and exact file/JSON-pointer errors. Do not log full private packages, player input, or remote telemetry.

## R12 — Testing and delivery boundaries

**Decision**: Deliver three independently reviewed children in strict order #1520 → #1521 → #1522. Each child starts with failing focused tests, runs one meaningful Fast checkpoint, runs FullValidation when afterlife docs/examples change, and runs PreMerge immediately before its merge. Frontend verify is conditional on actual React/TypeScript changes.

Catalog tests cover exact inventory, digest, 32 KiB index, all-realm inclusion, relevance selection, and no-player leakage. Guardian tests cover all ten bundles, atomic failure, idempotence, quest scope, typed offer/acceptance, a parameterized valid/invalid lifecycle matrix for all forty fixed quests, q4 memory, and a post-q4 non-story lifecycle for each of the ten Guardians. Saref/Wings tests cover exact actor/faction/location links, hidden/revealed projections, receipt immutability, and rollback. Because the feature changes New Game, accepted-turn, pending/control, and Guardian validation boundaries, #1520 and #1522 run `LifecycleIntegration`, while #1521 runs both `LifecycleIntegration` and the related `DeepValidation` Guardian matrix in addition to its ordinary Focused/Fast/PreMerge controls.

**Why**: The issue phases have real dependency boundaries and can be validated independently. One giant branch would make contract review and rollback needlessly difficult.
