# PrecisionChoice QTE Contract

Source issue: #915 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/915
Parent epic: #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911

## Check shape

A PrecisionChoice QTE action uses the existing QTE action/check envelope:

```json
{
  "actionId": "choose_safe_alley",
  "label": "Выбрать безопасный проулок",
  "check": {
    "type": "PrecisionChoice",
    "baseDifficulty": 3,
    "primaryCharacteristic": "perception",
    "config": {
      "timeoutMs": 6000,
      "timeoutGrade": "fail",
      "correctChoiceId": "alley_with_wind",
      "choices": [
        {
          "id": "alley_with_wind",
          "label": "Проулок, где тянет холодным ветром",
          "grade": "success",
          "hint": "Пыль у входа уходит внутрь, значит проход открыт."
        },
        {
          "id": "alley_with_lantern",
          "label": "Проулок под красным фонарём",
          "grade": "partial",
          "hint": "Свет дрожит слишком ровно, будто это приманка."
        },
        {
          "id": "alley_with_silence",
          "label": "Тихий проулок без следов",
          "grade": "fail"
        }
      ],
      "decoyHints": [
        {
          "choiceId": "alley_with_lantern",
          "hint": "Фонарь отвлекает преследователей, но ведёт в тесный двор."
        }
      ]
    }
  },
  "routing": {
    "success": { "nextChapterId": "chase_breaks" },
    "partial": { "nextChapterId": "chase_scraped_escape" },
    "fail": { "terminalOutcomeId": "caught_in_alley" }
  }
}
```

## Required fields

- `check.type`: exactly `PrecisionChoice`.
- `check.baseDifficulty`: existing QTE integer difficulty range `1..5`.
- `check.primaryCharacteristic`: existing canonical lowercase stat id.
- `check.config.choices`: array of `2..8` choice objects.
- `check.config.choices[].id`: stable non-empty id unique within the config.
- `check.config.choices[].label`: non-empty player-facing choice label.
- `check.config.choices[].grade`: one of `success`, `partial`, or `fail`.
- `check.config.correctChoiceId`: id of the single correct choice; that choice must have `grade: "success"`.
- `check.config.timeoutMs`: integer timeout from `1000` to `30000` ms.
- `check.config.timeoutGrade`: optional `fail` or `partial`; absent/null means `fail`.
- `check.config.decoyHints`: optional hints for non-success choices.

## Validation rules

Validation must reject:

- missing or non-object `check.config`
- missing, non-array, too-small, or too-large `choices`
- non-object choice entries
- missing, empty, or duplicate choice ids
- missing or empty choice labels
- missing or unsupported choice grade tokens
- no choice whose id equals `correctChoiceId`
- `correctChoiceId` that references a non-success choice
- multiple success choices unless the implementation deliberately requires exactly one success; if multiple success choices are allowed later, docs/tests must explain how `correctChoiceId` remains authoritative
- missing, non-integer, less-than-1000, or greater-than-30000 `timeoutMs`
- `timeoutGrade: "success"` or any unsupported timeout grade
- malformed `decoyHints`, empty hint text, or hint references to unknown/success choices

Validation issue messages should name `PrecisionChoice` and the exact malformed field.

## Local resolution

- The resolver presents stable numbered choices and a visible remaining-time cue.
- A selection before effective timeout resolves to the selected choice grade.
- A selection of `correctChoiceId` resolves `success` when made before timeout.
- A partial choice resolves `partial`; a fail choice resolves `fail`.
- Unknown choice ids, malformed config, no selection when timeout elapses, or cancel resolve safely.
- Timeout resolves to `timeoutGrade` if provided, otherwise `fail`; timeout cannot resolve `success`.
- Escape/cancel resolves `fail` safely.
- The resolver must have deterministic test hooks or pure helper functions that avoid real-time sleeps.

## Difficulty and characteristic

The implementation should use a monotonic adjustment rule equivalent to:

- Effective timeout is `timeoutMs - ((baseDifficulty - 3) * 300) + (statTier * 250)`, clamped to `1000..30000` and never below half the authored timeout.
- Effective hint clarity is monotonic: higher stat tier may reveal/strengthen decoy hints, while higher difficulty may hide/soften them.
- Higher `baseDifficulty` does not make PrecisionChoice easier for the same character/config.
- Higher relevant characteristic tier does not make PrecisionChoice harder for the same difficulty/config.

The adjustment must be covered by deterministic tests. Codex may refine the exact formula if tests/docs/spec remain monotonic and synchronized.

## Input and accessibility

- The console must show text choices, a clear timer/remaining-time line, and optional decoy hints when effective requirements reveal them.
- Existing QTE audio cues may play when available, but audio is only an enhancement.
- GM-authored config must not encode keyboard layout or ask the player to switch OS layout.
- Dynamic labels, descriptions, hints, and narrative text must be escaped before Spectre.Console markup rendering.

## Browser boundary

This issue does not implement full browser interactive PrecisionChoice. Browser surfaces may expose read-only action metadata if already required by existing QTE DTOs, but React must not duplicate gameplay resolution logic in this slice. Full browser parity remains #918.
