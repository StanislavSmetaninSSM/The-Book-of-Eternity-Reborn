# Afterlife Combat Balance Audit

Task: #426.

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

If a future change alters the formula, thresholds, or modifier scale, update this table and the matching tests in the same change.
