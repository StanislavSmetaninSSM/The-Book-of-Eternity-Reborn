# Daren Endings and Reward Presentation Contract

## Contract Purpose

This contract defines the #960 endings/reward-presentation slice for Daren's standalone QTE heist. It consumes the #956 scene map, #957 prose, #958 dialogue/cast, and #959 branch consequences, then expands the final outcome into authored epilogues and in-world reward explanation while preserving #919 reward mechanics.

## Source and Authority

- Source issue: #960 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/960>
- Parent: #955 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955>
- Narrative spine: #956 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956>
- Shared route prose: #957 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957>
- Dialogue/cast prerequisite: #958 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958>
- Branch-consequence prerequisite: #959 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/959>
- Base Daren showcase: #919 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919>
- Shared route authority: `QteSceneService.GetDarenShowcaseRoute()` in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Reward authority: `DarenQteRewardProfileService` in `BookOfEternityClient/Services/DarenQteRewardProfileService.cs`.
- Browser DTO authority: `DarenShowcaseEndingDto` in `BookOfEternityClient/WebUi/QteWebInteractionService.cs`.
- Scene map authority: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`.

## Ending Presentation Contract

Every Daren completion outcome must expose player-facing ending data from shared C# authority:

| Outcome | Reward behavior | Required player-visible ending shape |
| --- | --- | --- |
| `no_reward_failure` | no profile write, no New Game bonus | Failure epilogue explains unsafe or below-threshold outcome and why no permanent achievement is recorded. |
| `shadow_on_the_run` | profile may store best tier, +1 Ink Feather for future new games | Low-tier epilogue describes a narrow survival/dirty escape and a modest permanent lesson. |
| `broken_trail` | profile may store best tier, +2 Ink Feathers for future new games | Mixed-tier epilogue describes a successful escape with visible traces, witnesses, or pursuit pressure. |
| `clean_heist` | profile may store best tier, +4 Ink Feathers for future new games | Good-tier epilogue describes controlled loot, manageable evidence, and a stronger achievement. |
| `perfect_shadow` | profile may store best tier, +6 Ink Feathers for future new games | Excellent-tier epilogue describes a clean legendary theft with minimal traces and strongest achievement. |

Required shared fields may be implemented as new record properties or equivalent structured data, but they must be available to both console completion rendering and browser DTO serialization.

## Reward Mechanics Invariants

#960 may change player-facing reward wording but must preserve these mechanics:

- `ProfileRelativePath` remains `client_profile/qte_showcase_rewards.json`.
- `GrantMarkerProperty` remains `darenQteShowcase`.
- Tier ids, display names, minimum scores, and bonuses remain compatible with #919:
  - `shadow_on_the_run`: minimum 40, +1 Ink Feather.
  - `broken_trail`: minimum 55, +2 Ink Feathers.
  - `clean_heist`: minimum 75, +4 Ink Feathers.
  - `perfect_shadow`: minimum 90, +6 Ink Feathers.
- Unsafe route failure or score below 40 remains no-reward and must not write or upgrade the permanent profile.
- Profile writes keep best-tier-only semantics; worse or equal completions do not downgrade or stack rewards.
- New Game grant applies the best saved tier exactly once per newly created session through `clientRewardGrants.darenQteShowcase`.

## Shared Console/Browser Contract

- Console completion must be able to show ending display name, epilogue, normalized score, reward explanation, and score summary from shared C# data.
- Browser Daren state must expose the same ending display name, epilogue/reward copy, normalized score, bonus, and grant flag through `DarenShowcaseEndingDto` or an equivalent DTO.
- Browser/frontend code must not maintain a separate ending-tier text table or reward mapping. React may render fields supplied by C# if needed.
- #960 should not add a new endpoint or state file; existing Daren browser state is the expected transport.

## Narrative Spine Contract

`BookOfEternityClient/Content/DarenQteNarrativeSpine.json` should retain #956/#957/#958/#959 invariants and record #960 source/ending/reward handoff truth where the existing schema supports it:

- #960 appears in source/future links or handoff notes for ending/reward presentation.
- Existing route beat order, cast slots, consequence hooks, and #961 future quality-gate handoff remain intact.
- The spine remains an authoring/handoff artifact, not a runtime ending engine.

## Test Contract

Good #960 regression tests should fail when:

- any Daren outcome lacks epilogue copy;
- epilogue copy is identical or generic across tiers;
- reward-granting endings only say `+N Чернильных Перьев` without in-world achievement explanation;
- no-reward failure omits why the permanent reward was not recorded;
- browser DTO state lacks the shared ending epilogue/reward fields after completion;
- console completion summary omits the epilogue or reward explanation;
- reward threshold/profile/New Game semantics drift from #919;
- implementation adds a new reward profile path, ending-state runtime, frontend-only ending table, or QTE check type.

Good tests should not attempt to judge prose style beyond objective distinctness, tier-specific consequence language, reward explanation, and shared-data availability.

## GM-Contract Boundary

#960 is client-owned Daren showcase presentation. It should not change GM-authored QTE contract fields, validation rules, examples, prompts, or campaign pending/control state. If implementation changes a GM-authored QTE contract or validation rule, that is a scope expansion and must update `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and relevant documentation/source-guard tests in the same PR.

## Follow-up Boundaries

- #961 owns broad content-quality gates across all interactive-book presentation content.
- #955 remains the umbrella and should close only after #960 and #961 are verified/closed or explicitly scoped out.
- Future browser visual polish may improve presentation of the shared ending fields, but #960 should not require a React redesign when shared C# data already carries the player-facing ending text.
