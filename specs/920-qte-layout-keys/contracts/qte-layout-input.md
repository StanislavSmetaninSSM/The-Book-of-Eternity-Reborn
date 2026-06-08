# QTE Layout-Independent Input Contract

Source issue: #920 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/920

## Intent

QTE input compares the player's intended physical reaction key, not ordinary text typed under the current OS layout. This contract is scoped to QTE mini-game/key-prompt handling only.

## Canonical QTE key tokens

The initial supported token set for #920 is:

| Token | Physical/browser code | Latin display | Russian fallback display | Fallback characters |
| --- | --- | --- | --- | --- |
| `q` | `KeyQ` | `Q` | `Й` | `q`, `Q`, `й`, `Й` |
| `w` | `KeyW` | `W` | `Ц` | `w`, `W`, `ц`, `Ц` |
| `e` | `KeyE` | `E` | `У` | `e`, `E`, `у`, `У` |
| `a` | `KeyA` | `A` | `Ф` | `a`, `A`, `ф`, `Ф` |
| `s` | `KeyS` | `S` | `Ы` | `s`, `S`, `ы`, `Ы` |
| `d` | `KeyD` | `D` | `В` | `d`, `D`, `в`, `В` |
| `space` | `Space` | `Space` | none | space/control-space variants |

## Matching rules

1. Browser QTE matching MUST prefer `KeyboardEvent.code` when it maps to a supported physical key.
2. If physical code is unavailable/unsupported, QTE matching MAY use the produced character and normalize the fallback characters listed above.
3. Console/fallback QTE matching MUST normalize the listed Latin and Cyrillic fallback characters.
4. Unknown characters/codes MUST remain unmatched instead of collapsing to a supported key.
5. Normalization MUST be case-insensitive for the listed fallback characters.
6. This contract MUST NOT be applied to ordinary command/composer/chat/save-name text input.

## Prompt/display rules

1. Player-facing prompts SHOULD display both the Latin physical key and Russian fallback label, for example `Q / Й`.
2. `Space` SHOULD display as a clear control key and does not need a Russian fallback label.
3. Prompt copy SHOULD state that QTE input supports physical RU/EN layout pairs where implementation can handle it.
4. Prompt copy MUST NOT tell players to switch OS layout when the implementation supports physical/fallback matching.
5. If a platform cannot provide layout-independent input for a QTE, the QTE screen MUST warn the player before the timer starts.

## GM authoring rule

GM-authored QTE configuration does not encode the player's keyboard layout. GM-facing docs and examples should describe gameplay check types, intended prompt keys, difficulty, and consequences; client-side QTE handling owns the physical-key/RU-EN normalization.

## Verification hooks

Tests should instantiate the normalization helpers directly and feed deterministic strings/event-like objects. Tests must not depend on the actual OS keyboard layout active on the test machine.
