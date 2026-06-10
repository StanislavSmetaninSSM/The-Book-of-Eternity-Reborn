# Daren QTE Training Showcase Contract

Source issue: [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

This contract defines the durable behavior for the client-owned Daren QTE showcase. It does not add a GM-authored campaign QTE response field and does not change afterlife pending/control contracts.

## Ownership and Storage

- Daren showcase route content is client-owned authored training content.
- Daren route attempts are local/transient and must not write ordinary campaign `game_state`, pending action files, chat-log turns, inventory, quests, XP, afterlife state, or QTE practice rewards.
- Permanent Daren reward state is a client/profile record outside `game_session/game_state`; default path for implementation is `client_profile/qte_showcase_rewards.json` under the configured base path unless an existing profile convention is found and documented in this feature.
- New Game writes a per-session idempotency marker inside the newly initialized session state so retry/repair/load does not apply the same Daren bonus twice to that session.

## Required Route Beats

The route must include these beats in order, with local choices allowed inside each beat:

1. `approach_manor`: Daren approaches the locked manor under patrol pressure.
2. `gadget_infiltration`: Daren uses medieval thief gadgets to enter or bypass defenses.
3. `stealth_crossing`: Daren crosses a noise-sensitive section.
4. `lock_pick`: at least one `LockPinSet` action opens a lock.
5. `rune_memory`: at least one `PatternMemory` action resolves a rune/memory interaction.
6. `physical_pressure`: at least one `MashInput` action forces or braces a mechanism.
7. `timed_rhythm`: at least one `RhythmPulse` or `TimingBar` action resolves timing pressure.
8. `route_decision`: at least one `PrecisionChoice` action makes a timed route choice.
9. `staff_theft`: Daren steals the magical staff or reaches a failure/partial theft state.
10. `pursuit`: pursuit starts after the theft beat.
11. `chase_chain`: multiple QTE actions decide escape quality.
12. `hideout_return`: the route ends with a valid ending only if Daren reaches or safely resolves the hideout return state.

## Required QTE Type Coverage

The Daren route must include at least one meaningful action for every implemented type:

- `TimingBar`
- `PromptChain`
- `BalanceMeter`
- `ChargeRelease`
- `BranchChoice`
- `MashInput`
- `PatternMemory`
- `RhythmPulse`
- `PrecisionChoice`
- `StealthNoise`
- `LockPinSet`

Each action must use existing QTE validation/resolution/scoring helpers. Browser UI may compute local mini-game grades through existing #918 components, but C# remains authoritative for accepting grades, route state, scoring, profile writes, and New Game reward grants.

## Ending Tiers and Rewards

Valid endings use these exact tier ids, Russian display names, minimum normalized score thresholds, and New Game Ink Feather bonuses:

| Tier id | Display name | Minimum normalized score | Ink Feather bonus | Summary |
| --- | --- | ---: | ---: | --- |
| `shadow_on_the_run` | `Тень в бегах` | 40 | +1 | Daren survives or escapes, but the theft is compromised, the staff is damaged/lost, or the hideout is exposed. |
| `broken_trail` | `Сорванный след` | 55 | +2 | Daren escapes with some objective value but leaves major evidence, loses optional loot, or suffers a serious complication. |
| `clean_heist` | `Чистая кража` | 75 | +4 | Daren steals the staff and reaches the hideout with manageable consequences. |
| `perfect_shadow` | `Идеальная тень` | 90 | +6 | Clean infiltration, successful theft, strong escape, minimal evidence, and best optional objective outcome. |

A route that ends before safe escape/hideout resolution or has normalized score below 40 is `no_reward_failure` and must not write or upgrade the permanent reward profile.

## Score Inputs

Implementation may choose exact point weights, but the final score model must be deterministic and must include at least:

- QTE grades across the route (`success`, `partial`, `fail`, timeout/cancel where supported).
- Stealth/noise quality and evidence left behind.
- Staff/loot condition.
- Pursuit/chase result.
- Hideout safety.
- Optional objective outcome.

The completion summary must show the ending tier plus player-facing reasons derived from these inputs.

## Permanent Profile Schema

The permanent profile record must normalize to one best Daren record. Suggested JSON shape:

```json
{
  "schemaVersion": 1,
  "darenShowcase": {
    "bestTierId": "clean_heist",
    "bestTierName": "Чистая кража",
    "inkFeatherBonus": 4,
    "bestScore": 82,
    "completedAtUtc": "2026-06-11T00:00:00Z",
    "source": "daren_qte_showcase"
  }
}
```

Rules:

- Unknown tier ids, negative bonuses, impossible scores, duplicate Daren records, or lower-tier overwrite attempts are invalid.
- First valid completion writes the tier.
- Better completion upgrades the tier, bonus, score, and timestamp.
- Same or worse completion does not downgrade, stack, or duplicate.
- `inkFeatherBonus` must be derived from `bestTierId`, not trusted blindly from a corrupt file.

## New Game Grant Contract

When a new game/session is initialized:

1. Read and normalize the permanent Daren reward profile.
2. If no valid Daren tier exists, grant nothing.
3. If a valid tier exists, add the tier's Ink Feather bonus to the newly initialized soul state exactly once.
4. Record an idempotency marker in the new session state. The marker must include source `daren_qte_showcase`, tier id, bonus amount, and the profile schema/version used.
5. Render player-facing copy that names the Daren ending and Ink Feather amount.

Forbidden grant surfaces:

- Save load.
- State repair/normalization for an existing session.
- Reincarnation or life start inside an existing session.
- Afterlife transitions.
- Ordinary campaign turns.
- QTE Practice Mode.
- Replaying Daren showcase without subsequently starting a new game.

## Documentation and Example Contract

Update GM/player-facing materials when implementing this feature:

- `CLI_API_Specification.md` must document the client-owned Daren showcase/profile reward boundary and New Game grant behavior.
- `Rules/Block_CLI_QTE.txt` must explain that normal campaign QTE offers remain GM-authored, while Daren showcase is client-owned training content.
- `Examples/E_CLI_QTE_Offer.txt` or a companion example must mention Daren showcase as non-GM-authored and show that ordinary QTE offers do not carry permanent profile rewards.
- Source/documentation guards must assert that the Daren reward cannot be granted by reincarnation, practice mode, save load, or ordinary campaign turns.

## Player-Facing Copy Boundary

Default UI strings must use Russian in-world/player-facing terms. Avoid raw terms such as `endpoint`, `DTO`, `JSON`, `manual grade`, `debug`, file paths, agent workflow labels, or implementation exception text.
