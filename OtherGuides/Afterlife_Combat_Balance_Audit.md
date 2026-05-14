# Afterlife Combat Balance Audit

Task: #443. Related implementation tasks: #426, #441, #442.

This document fixes the intended balance envelope for afterlife spiritual conflict. It is not a new GM contract surface; the canonical runtime contract remains in `OtherGuides/Afterlife_Contract_Matrix.md`. The purpose here is to keep the dice formula from drifting into either pure randomness or stat-lock.

## Formula Under Audit

For every contested afterlife exchange or forced-incarnation resolution:

```text
playerTotal = player d20 + sum(diceAudit.modifierBreakdown.player[].value)
oppositionTotal = opposition d20 + sum(diceAudit.modifierBreakdown.opposition[].value)
margin = playerTotal - oppositionTotal
```

Outcome bands:

```text
margin >= 8       -> decisive_player_success
margin 3..7       -> player_success
margin -2..2      -> mixed_or_no_effect
margin -7..-3     -> opposition_success
margin <= -8      -> decisive_opposition_success
```

Balance assumptions:

- A one-to-two tier advantage should matter on average dice, but should not guarantee victory against a strong opposing roll.
- A four-or-more tier/rank advantage should be decisive on average dice, but an extreme opposing roll can still produce a dramatic reversal.
- A weak player helped by a strong champion should be represented through `sideModel=champion_duel` or the ally's support modifier, not through mass combat.
- A returned Shining soul keeps retained Radiance as real combat authority in Chaos Sea conflicts, even after Enlightenment is reset.
- Source of Light / Light Incarnate is a major capstone bonus, but it is still applied as an explicit modifier inside the same dice envelope, not as automatic victory.

## Deterministic Matrix

The following rows are encoded in `AfterlifeSpiritualConflictBalanceTests`. Fixed dice values come from `turn_request.preGeneratedDices1d20`, so the GM cannot invent a convenient roll.

| Case | Dice | Modifiers | Margin | Expected band | Balance reading |
| --- | --- | --- | --- | --- | --- |
| Equal sides, average roll | player 11 vs opposition 9 | 0 vs 0 | +2 | `mixed_or_no_effect` | Equal sides do not auto-win from a small roll edge. |
| Weak player vs average Guardian | player 14 vs opposition 9 | 0 vs +4 | +1 | `mixed_or_no_effect` | Good player roll can avoid collapse but does not beat the Guardian outright. |
| Upgraded Chaos Sea player vs average Guardian | player 14 vs opposition 9 | +3 vs +2 | +6 | `player_success` | A one-to-two tier investment matters without becoming decisive. |
| Same upgrade, bad roll | player 5 vs opposition 18 | +3 vs +2 | -12 | `decisive_opposition_success` | Dice can still create a dramatic loss. |
| Returned Shining soul with retained Radiance | player 12 vs opposition 8 | +4 vs +3 | +5 | `player_success` | Retained Radiance remains relevant after return to Chaos Sea. |
| Player aided by strong champion | player 13 vs opposition 6 | +2 player/support plus +4 champion authority vs +4 Guardian | +9 | `decisive_player_success` | Champion support solves the weak-player-plus-strong-ally case without mass combat. |
| Strong Guardian vs novice | player 9 vs opposition 11 | 0 vs +6 | -8 | `decisive_opposition_success` | A novice should not trade evenly with a high-authority Guardian on average rolls. |
| Four-tier advantage, average roll | player 14 vs opposition 9 | +6 vs +1 | +10 | `decisive_player_success` | Large progression advantage is decisive on normal dice. |
| Four-tier advantage, extreme bad roll | player 5 vs opposition 18 | +6 vs +1 | -8 | `decisive_opposition_success` | Even a large advantage does not erase rare dramatic reversals. |
| Source of Light lead, average roll | player 11 vs opposition 9 | +8 vs +6 | +4 | `player_success` | Light Incarnate strongly shifts an even lead duel without forcing decisive success. |
| Source of Light lead, extreme bad roll | player 5 vs opposition 18 | +8 vs +6 | -11 | `decisive_opposition_success` | Light Incarnate does not erase extreme dice reversals against strong opposition. |
| Source of Light support role | player 13 vs opposition 6 | +4 vs +6 | +5 | `player_success` | Support-role Light Incarnate is useful, but smaller than the lead-contestant bonus. |

If a future change alters the formula, thresholds, or modifier scale, update this table and the matching tests in the same change.

## Reward Economy Envelope

Victorious afterlife spiritual conflicts may grant currency only through the strict `rewardAudit` contract. The reward is intentionally small: it should make a real victory feel useful, but it must not compete with the primary progression loops or become an afterlife farming exploit.

Reward formula under audit:

```text
finalAmount = baseAmount * challengeTier * outcomeMultiplierPercent * riskMultiplierPercent / 10000
finalAmount is capped by realm
```

Realm constants:

| Realm | Currency | Base | Cap | Balance intent |
| --- | --- | ---: | ---: | --- |
| `Chaos Sea` | `ink_feathers` | 10 | 120 | Meaningful but below major Shining costs and not enough to replace normal Feather sources. |
| `Shining Abode` | `light_sparks` | 1 | 8 | Very scarce because Light Sparks are stronger and feed Shining systems. |

Multipliers:

| Source | Values |
| --- | --- |
| Outcome | `player_success = 100`, `decisive_player_success = 150` |
| Starting risk | `opposition_dominant = 150`, `opposition_advantaged = 125`, `contested = 100`, `player_advantaged = 75`, `player_dominant = 50` |
| Challenge tier | Derived from opposing lead strength, `sideModel`, and starting position; clamped to `1..5`. |

Reward balance examples encoded in tests:

| Case | Realm | Inputs | Expected reward | Balance reading |
| --- | --- | --- | ---: | --- |
| Ordinary contested victory | Chaos Sea | strength 3, `direct_duel`, `contested`, `player_success` | 30 Ink Feathers | A normal real win is worth noticing. |
| Low-risk weak conflict | Chaos Sea | strength 1, `champion_duel`, `player_dominant`, `player_success` | 5 Ink Feathers | Weak/no-risk wins are low-value and should not become farming. |
| Low-risk weak conflict | Shining Abode | strength 1, `champion_duel`, `player_dominant`, `player_success` | 0 Light Sparks | Light Sparks are scarce enough that trivial Shining fights can pay nothing. |
| High-risk decisive victory | Shining Abode | strength 12, `direct_duel`, `opposition_dominant`, `decisive_player_success` | 8 Light Sparks | Even the hardest Shining victory hits the cap. |

No reward is allowed for `repair_cancel`, `no_effect`, voluntary retreat/surrender, pure negotiation/no-contest closure, duplicate reward for the same `conflictId`, wrong-realm currency, or a reward delta that does not match the accepted state.

## Spiritual Arts Balance

Spiritual Arts are operation lanes, not prose labels. They matter because each upgraded lane can justify explicit dice modifiers and state deltas, but each lane has a limited tactical job:

| Operation | Mechanical role | What it may change | What it must not do |
| --- | --- | --- | --- |
| `pressure` | Offensive push | Increase `oppositionSideStrain`, improve position on success | Directly reduce player strain |
| `guard` | Defensive stabilization | Reduce/prevent `playerSideStrain`, hold position | Directly strain the opponent as its main effect |
| `counter` | Reactive punish | Requires `incomingAction`; can block/counter that action | Fire without an incoming action |
| `maneuver` | Positional play | Move `conflictPosition` | Directly change either side's strain |
| `binding` / `force_binding` | Control after leverage | Add binding/control proof when leverage exists | Create control from neutral/no-leverage state |
| `break_binding` | Escape coercive control | Remove binding/coercive handoff state | Act as generic attack |
| `incarnation_resistance` | Resist forced incarnation | Contest `force_incarnation` / `guardian_forced` | Apply to ordinary non-coercive pressure |
| `champion_coordination` | Player helps an allied lead | Improve champion-side result in `champion_duel` | Turn the conflict into mass combat |

The balance goal is that a player chooses an operation because its state effect fits the situation, then upgrades that lane to improve the associated modifier. The GM must not treat all arts as interchangeable "do something spiritual" buttons.
