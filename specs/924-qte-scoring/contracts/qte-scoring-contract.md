# QTE Scoring Contract

Source issue: #924 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/924
Parent epic: #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911

## Authored shape

A QTE offer may define an optional `scoreModel` alongside existing QTE action/check/routing fields. The score model is generic and not tied to Daren, rewards, inventory, achievements, or practice mode.

```json
{
  "qteOfferId": "manor_infiltration_example",
  "scoreModel": {
    "metrics": [
      {
        "id": "stealth",
        "label": "Скрытность",
        "initial": 50,
        "min": 0,
        "max": 100,
        "visibility": "always"
      },
      {
        "id": "evidence",
        "label": "Улики",
        "initial": 0,
        "min": 0,
        "max": 100,
        "visibility": "final"
      },
      {
        "id": "alarm",
        "label": "Тревога",
        "initial": 0,
        "min": 0,
        "max": 100,
        "visibility": "always"
      }
    ],
    "rankOrder": ["best", "good", "partial", "bad"],
    "ranks": [
      {
        "id": "best",
        "label": "Безупречный исход",
        "summary": "Следы почти не остались, тревога не поднята.",
        "allOf": [
          { "metric": "stealth", "op": ">=", "value": 80 },
          { "metric": "evidence", "op": "<=", "value": 20 },
          { "metric": "alarm", "op": "<=", "value": 10 }
        ]
      },
      {
        "id": "good",
        "label": "Удачный исход",
        "summary": "Цель достигнута, последствия управляемы.",
        "allOf": [
          { "metric": "stealth", "op": ">=", "value": 55 },
          { "metric": "alarm", "op": "<=", "value": 45 }
        ]
      },
      {
        "id": "partial",
        "label": "Неровный исход",
        "summary": "Победа есть, но цена заметна.",
        "allOf": [
          { "metric": "stealth", "op": ">=", "value": 25 }
        ]
      },
      {
        "id": "bad",
        "label": "Провальный исход",
        "summary": "Сцена завершилась с тяжёлыми последствиями.",
        "fallback": true
      }
    ]
  },
  "actions": [
    {
      "actionId": "cross_patrol_yard",
      "label": "Пройти двор между патрулями",
      "check": { "type": "StealthNoise", "baseDifficulty": 3, "primaryCharacteristic": "dexterity", "config": {} },
      "scoreDeltas": {
        "success": [
          { "metric": "stealth", "delta": 15 },
          { "metric": "alarm", "delta": -5 }
        ],
        "partial": [
          { "metric": "stealth", "delta": -5 },
          { "metric": "evidence", "delta": 10 }
        ],
        "fail": [
          { "metric": "stealth", "delta": -20 },
          { "metric": "alarm", "delta": 30 },
          { "metric": "evidence", "delta": 25 }
        ]
      },
      "routing": {
        "success": { "nextChapterId": "inner_garden" },
        "partial": { "nextChapterId": "inner_garden_alerted" },
        "fail": { "nextChapterId": "pursuit_begins" }
      }
    }
  ]
}
```

## Required validation

Validation must reject:

- non-object `scoreModel` when present;
- missing/non-array/empty `scoreModel.metrics`;
- metric ids that are empty, contain unsupported characters, or are duplicated;
- metric labels that are missing, empty, or not player-facing strings;
- missing or nonnumeric `initial`, `min`, or `max` values;
- `min > max` or `initial` outside `[min, max]`;
- unsupported metric `visibility` values; supported values are `always`, `final`, and `hidden`;
- `rankOrder` entries that do not match defined rank ids, duplicate rank ids, or omit a non-fallback rank that the authored ranks define;
- missing/non-array/empty `ranks`;
- rank ids that are empty or duplicated;
- rank labels/summaries that are empty when present;
- rank definitions without either `fallback: true` or a non-empty `allOf` threshold list;
- multiple fallback ranks or no fallback rank;
- threshold rules referencing unknown metric ids;
- threshold operators outside `>=`, `>`, `<=`, `<`, `==`;
- threshold values outside the referenced metric bounds when the comparison cannot be satisfied;
- action `scoreDeltas` grade keys outside `success`, `partial`, `fail`;
- action `scoreDeltas` entries that reference unknown metrics or nonnumeric deltas;
- score deltas on unknown actions or score models that cannot be associated with QTE action resolution.

Validation messages should include `scoreModel`, the field path, and the offending id/value where possible.

## Runtime scoring rules

- Initial active score state is built from `scoreModel.metrics[].initial`.
- When an action resolves to `success`, `partial`, or `fail`, the runtime applies only the deltas for that action and grade.
- Metric values are clamped to `[min, max]` after every delta.
- Each applied delta appends an audit entry with action id, action label when available, grade, metric id, previous value, delta, clamped value, and a player/GM-facing reason if authored or derivable.
- Score application is deterministic and testable without wall-clock sleeps.
- Unscored actions inside a scored offer are allowed and append no score delta.
- A scored offer with no remaining actions computes final rank when the QTE reaches a terminal/completed state.
- Final rank evaluation checks ranks in `rankOrder` when provided; otherwise it checks authored rank order. The first rank whose `allOf` thresholds all match wins. If none match, the fallback rank wins.
- Final score summary includes final visible metrics, final rank label/id, rank summary, and audit details for tests/history.

## Visibility rules

- `always`: metric label/value may be shown during active play and in final summary.
- `final`: metric label may be hinted during active play, but the numeric/current value should only be shown in final summary.
- `hidden`: metric is not shown in default active player UI. Final display is allowed only when the rank/summary needs it and must use player-facing text, not raw ids.
- Advanced/debug mode may expose fuller score audit details, but default player UI must not show raw JSON, DTO, endpoint, file path, or debug labels.

## Browser boundary

- Browser DTOs may expose read-only score model/state/final summary needed for rendering.
- Browser/React may display score meters and final rank, but C# remains the score state mutation and rank/audit authority.
- Browser QTE mini-games from #918 still submit only action id plus grade/result through existing QTE action flow; the browser must not post arbitrary metric deltas.

## Documentation scope

Update these surfaces when implementing the contract:

- `CLI_API_Specification.md` for the QTE score model fields and response/history surfaces.
- `Rules/Block_CLI_QTE.txt` for GM authoring rules and visibility guidance.
- `Examples/E_CLI_QTE_Offer.txt` with a worked ordinary scored QTE example.
- Documentation/source guard tests proving the above files mention score model metrics, deltas, ranks, visibility, and generic/non-Daren scope.
