# Saref Main Story E2E Audit Matrix

Source: GitHub issue #692 - `[Saref Main Story] E2E audit and progression walkthrough for the hidden main storyline`.

This matrix is the closure audit for `Крылья над Бездной`. It does not add new canon or new GM authority. It maps the existing hidden Saref runtime contract to player-visible safety, GM authoring surfaces, validation evidence, normalizer behavior, docs/examples, and the follow-up issue draft policy.

Canonical runtime authority:

- `game_state/meta/main_story_saref_state.json`
- `game_state/control/pending_saref_wings_infiltration.json`
- `sarefMainStoryState`
- `sarefMainStoryUpdate`
- `memoryScene`
- `memorySceneProof`

## Stage Matrix

| Stage / branch | Required canonical fields | Player-facing command behavior | GM-authored response surface | Pending/control files | Validation issue evidence | Normalizer / cleanup evidence | Docs and example evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `unknown` | `revealStage=unknown`, no revealed `sarefRevelations[]`, no `sarefAdvantages[]`, hidden `factionLinks.visibility` | `/сареф` says only "Ты пока не знаешь, что искать."; no name, Wings, final, raw stage, JSON, file path, API, or DTO leak | GM may keep only private planning or latent non-player-visible facts; no public Saref reveal | None | `saref_main_story_no_spoiler_stage_has_revealed_content` catches revealed content in no-spoiler stages | Default root from `CreateDefaultRoot()` is legacy-compatible and validates | `Afterlife_Contract_Matrix.md`, example 27, manifest `saref_reveal_stage`, `SarefMainStoryStateValidationTests` |
| `shadow` | `revealStage=shadow`, optional `latentTraces[]`, no canonical revealed Saref/Wings proof | `/сареф` still uses the unknown-text boundary unless player-visible fragments exist; `/воспоминание` can explain no active scene without revealing future branches | GM may write latent or recognized Guardian traces without naming Saref publicly | None | Same no-spoiler invariant plus quest order checks | Normalizer keeps default/root arrays stable | Guardian questline bibles, memory-boundary guide, example 27 |
| `name_revealed` | Completed Guardian quest 4 with `memorySceneProof`, at least one legal `sarefRevelations[]`; advantages require matching quest-4 proof | `/сареф` may show known fragments and available advantages; Wings route details stay hidden until enough fragments exist | `sarefMainStoryUpdate.mode=record_memory_scene` writes `memoryScene`, `guardianQuestline`, `sarefRevelation`, and `sarefAdvantage` | No Wings pending until route exists | `saref_main_story_quest_four_missing_memory_scene_proof`, `saref_main_story_revelation_without_questline_completion`, `saref_main_story_advantage_without_questline_completion`, `saref_main_story_physical_mortal_item_evidence` | `NormalizeAccumulatedStateAsync_SarefMemorySceneUpdateWrapper_ProjectsQuestClosureAgainstBackupBaseline` proves quest closure, `memorySceneProof`, and no `pendingMemoryLegacy` | `Saref_Memory_System_Boundaries.md`, Guardian questline bibles, example 27 |
| `wings_revealed` | All mandatory route fragments, or allowed risky/desperate substitutes; `factionLinks.visibility=revealed`, `wingsInfiltration.status=revealed`, actionable `wingsFactionId` | `/сареф найти_крылья` blocks outside ordinary active Shining Abode, blocks missing route, and does not create duplicate pending search | GM closes accepted search through `sarefMainStoryUpdate.mode=reveal_wings` with matching request id and turn evidence | `pending_saref_wings_infiltration.json` exists only until accepted closure/cleanup | `saref_wings_pending_missing_unlock_route`, `saref_wings_pending_wrong_realm`, `saref_wings_pending_invalid_shining_mode`, `saref_main_story_wings_revealed_missing_faction_id`, `saref_main_story_wings_faction_missing_shining_actor` | `ApplyUpdate` writes revealed closure; `HasMatchingWingsInfiltrationClosure` requires stage/faction visibility before cleanup | `Afterlife_Contract_Matrix.md`, pending inventory, example 27 |
| `infiltration_active` | Pending request has `requestId`, `createdAtTurn`, `createdAtUtc`, `routeSafety`, `entryMode`, fragments, advantages, disadvantages, and expected closure | `/сареф найти_крылья` shows an in-world "already waiting" panel and avoids duplicate request creation | GM must choose `reveal_wings`, `refuse_wings`, or `block_wings`; risky/desperate routes must apply listed disadvantages | `game_state/control/pending_saref_wings_infiltration.json` | `saref_wings_pending_safe_route_incomplete`, `saref_wings_pending_risky_route_incomplete`, `saref_wings_pending_desperate_route_incomplete`, `saref_wings_pending_missing_closure`, `saref_wings_pending_blocked_by_other_contract` | `EnsureWingsInfiltrationHealthyAsync` and accepted-turn validation require exact matching closure before cleanup | Pending matrix row, example 27, manifest surface description |
| `confrontation_available` | `revealStage=confrontation_available`, Wings route/faction evidence, usable `sarefAdvantageUses[]` for claimed advantages | `/сареф` may show route/faction/advantage progress but still uses in-world labels | GM resolves only through direct final, defeat, deal, or oath-related state; no off-screen victory | No unrelated pending contract may substitute for the final | `saref_main_story_final_confrontation_offscreen`, `saref_main_story_final_unknown_advantage_use`, `saref_main_story_final_hybrid_missing_components`, `saref_main_story_final_deep_victory_insufficient_guardians` | `ApplyUpdate_RecordFinalConfrontation_SetsCompletedStateAndEnding` projects final resolution | Final branch docs in matrix/example/manifest |
| `completed` | `finalConfrontation.status=resolved`, `directScene=true`, `resolvedAtTurn`, final route and outcome fields, matching `endings[]` | `/сареф` may show final result and reward bundle in player-facing terms | `sarefMainStoryUpdate.mode=record_final_confrontation` | No unresolved matching Wings pending closure remains | `saref_main_story_completed_without_final_confrontation`, `saref_main_story_ending_final_mismatch`, `saref_main_story_ending_victory_missing_protection`, `saref_main_story_final_wings_lifecycle_mismatch` | Final normalizer and validation tests cover combat/hybrid/deep victory and rewards | Example 27 covers final confrontation and reward bundles |
| `oathbound_to_saref` | Deal final has `routeType=deal`, `victoryTier=deal`, `sarefOutcome=allied`, `wingsFactionOutcome=joined`, `playerOathState.state=oathbound|strained`, `postStoryAgenda.state=oathbound_to_saref` | `/сареф` shows the post-story agenda as continuing play, not game over | `record_oathbound_agenda` updates assignments and domination scene; assignments link Shining campaigns | None unless a separate Shining campaign pending exists | `saref_main_story_ending_deal_missing_oath_cost`, `saref_main_story_oathbound_agenda_missing`, `saref_main_story_oathbound_assignment_campaign_missing`, `saref_main_story_oathbound_domination_scene_missing` | Deal final initializes post-story agenda; agenda normalizer merges assignments/domination scene | Example 27 and matrix rows for deal/post-story |

## Defeat Outcomes

All defeat outcomes require `defeatOutcomes[]` entries with `outcomeId`, `outcomeType`, `sceneType`, `resolvedAtTurn`, `summary`, and `gmMotivation`. The GM writes them through `sarefMainStoryUpdate.mode=record_defeat_outcome` or equivalent canonical root state.

| Defeat outcome | Required evidence | Validation issue evidence | Player/GM notes |
| --- | --- | --- | --- |
| `forced_oath` | Matching `playerOathState` and `oathId` | `saref_main_story_defeat_forced_oath_missing_oath_state`, `saref_main_story_defeat_forced_oath_mismatch` | This moves play into oathbound state, not a silent death. |
| `exile_to_chaos_sea` | `exileAudit.destinationRealm=Chaos Sea` and reason | `saref_main_story_defeat_exile_missing_audit`, `saref_main_story_defeat_exile_wrong_destination` | Exile is a dramatic setback and must not use Mortal transfer authority. |
| `memory_suppression` | `memorySuppressionAudit` with scope/severity/summary | `saref_main_story_defeat_memory_missing_audit`, `saref_main_story_defeat_memory_missing_scope` | This is a Saref scene consequence, not free GM erasure. |
| `soul_dissipation` | `conflictId` and `soulDissipationProofId` | `saref_main_story_defeat_soul_dissipation_missing_proof` | Only proved player soul dissipation may be terminal. |
| `pyrrhic_escape` | `escapeCost` and mitigation tied to valid `sarefAdvantageUses[]` | `saref_main_story_defeat_pyrrhic_escape_missing_cost`, `saref_main_story_defeat_unknown_mitigation_advantage` | This is non-terminal and must carry a cost. |

## Oath-Break Routes

Oath break after a deal is recorded through `sarefMainStoryUpdate.mode=record_oath_break` into `postStoryAgenda.oathBreakArc`. `state=broken` requires proof, route evidence, valid `advantageUseIds[]`, root `playerOathState.state=broken|oath_reversed`, and serious consequences.

Routes:

- `seret`
- `lucian`
- `ilarion`
- `veyra`
- `deep_story_evidence`

Required consequence vocabulary:

- `renegade_from_wings`
- `oath_reversed`
- `beloved_traitor`
- `second_confrontation_unlocked`

Validation issue evidence:

- `saref_main_story_oath_break_missing_arc`
- `saref_main_story_oath_break_missing_proof`
- `saref_main_story_oath_break_missing_consequence`
- `saref_main_story_oath_break_unknown_advantage_use`
- `saref_main_story_oath_break_romance_missing_tragedy`
- `saref_main_story_oathbound_left_without_oath_break`

## Command And Memory Boundary Audit

Player-facing command behavior:

- `/сареф` must stay non-spoiler in `unknown` and `shadow`, using "Ты пока не знаешь, что искать."
- `/сареф` may show fragments, advantages, uses, endings, post-story agenda, and oath-break arc only after the corresponding canonical fields exist.
- `/сареф найти_крылья` can create `game_state/control/pending_saref_wings_infiltration.json` only in ordinary active `Shining Abode`, with no overlapping pending/control blocker and enough route fragments.
- `/сареф найти_крылья` must not create a duplicate request while one already waits for GM resolution.
- `/воспоминание` shows the playable `Воспоминание` layer, not `Memory Gates`, not `pendingMemoryLegacy`, and not physical mortal-item transfer.

GM-authored response surface:

- Quest 4: `sarefMainStoryUpdate.mode=record_memory_scene`
- Wings closure: `sarefMainStoryUpdate.mode=reveal_wings|refuse_wings|block_wings`
- Final: `sarefMainStoryUpdate.mode=record_final_confrontation`
- Deal agenda: `sarefMainStoryUpdate.mode=record_oathbound_agenda`
- Defeat: `sarefMainStoryUpdate.mode=record_defeat_outcome`
- Oath break: `sarefMainStoryUpdate.mode=record_oath_break`

Normalizer / cleanup evidence:

- `NormalizeAccumulatedStateAsync_SarefMainStoryUpdateWrapper_ProjectsAgainstBackupBaseline`
- `NormalizeAccumulatedStateAsync_SarefMemorySceneUpdateWrapper_ProjectsQuestClosureAgainstBackupBaseline`
- `NormalizeAccumulatedStateAsync_SarefDefeatOutcomeUpdateWrapper_ProjectsAgainstBackupBaseline`
- `ApplyUpdate_RecordMemoryScene_MergesSceneQuestRevelationAndAdvantage`
- `ApplyUpdate_RecordFinalConfrontation_SetsCompletedStateAndEnding`
- `ApplyUpdate_RecordFinalConfrontation_WithDealInitializesOathboundPostStoryAgenda`
- `ApplyUpdate_RecordOathBreak_MergesArcAndUpdatesOathState`
- `ValidateGameStateAsync_SarefWingsAcceptedTurnWithRevealClosure_PassesPendingClosureValidation`

Docs and example evidence:

- `OtherGuides/Afterlife_Contract_Matrix.md`
- `OtherGuides/Saref_Memory_System_Boundaries.md`
- `OtherGuides/Saref_Character_Bible.md`
- `OtherGuides/Saref_Guardian_Questlines/*.md`
- `Examples/E_CLI_Afterlife_Turns.txt` example 27
- `Examples/example_validation_manifest.json`
- `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
- `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs`

## Follow-Up Issue Draft Policy

No broad missing mechanic was identified by this bounded audit unit that needs a new GitHub issue before #692 can close. If a later reviewer finds a gap outside this PR's scope - true keyboard E2E harness, separate browser UX parity beyond existing write-service parity, or new Saref canon/mechanics - write a follow-up issue draft instead of expanding #692 silently.

The required run-local follow-up issue draft path for this closure unit is:

`E:/Games/codex-runs/20260608-190500-boe-692-saref-e2e-audit/follow-up-issue-drafts.md`
