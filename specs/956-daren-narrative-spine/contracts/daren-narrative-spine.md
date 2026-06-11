# Daren Narrative Spine Contract

## Contract Purpose

This contract defines the durable shape for the Daren narrative spine created for #956. The artifact is a planning/authoring authority that future #957-#961 work consumes. It is not a new runtime scenario engine.

## Artifact Path

Preferred path:

```text
BookOfEternityClient/Content/DarenQteNarrativeSpine.json
```

The path may change only if the implementation records the new path in tests and this contract before merge.

## JSON Shape

```json
{
  "schemaVersion": 1,
  "routeId": "daren_qte_showcase",
  "sourceIssues": [956, 955, 919],
  "targetPlaytimeMinutes": { "min": 20, "max": 30 },
  "arcStages": ["preparation", "approach", "infiltration", "reconnaissance", "security", "complication", "theft", "alarm", "chase", "hideout", "epilogue"],
  "castSlots": [
    { "slotId": "contact_informant", "role": "contact/informant", "plannedBeatIds": ["approach_manor"] }
  ],
  "beats": [
    {
      "beatId": "approach_manor",
      "phase": "preparation/approach",
      "title": "Подступ к поместью",
      "dramaticPurpose": "Introduce the target, danger, and Daren's first decision.",
      "playerGoal": "Choose a stealthy approach before patrols notice him.",
      "qteType": "BranchChoice",
      "sceneFraming": "Short player-facing prose framing the place, stakes, and action.",
      "branchPoints": ["Strong choice finds the dark gap; partial choice creates light suspicion; poor choice costs time."],
      "consequenceHooks": ["stealth", "evidence", "pursuit"],
      "carryForward": ["Suspicion can echo in later guard/pursuit scenes."],
      "futureIssueLinks": [957, 958, 959, 960, 961],
      "pacingMinutes": 2
    }
  ],
  "handoffNotes": [
    "Future prose work should use these beat entries instead of inventing a separate route."
  ]
}
```

## Required Invariants

- `routeId` must equal `daren_qte_showcase`.
- `sourceIssues` must include #956, #955, and #919.
- `targetPlaytimeMinutes.min` must be at least 20 and `max` must be at most 30.
- Every beat id from `QteSceneService.GetDarenShowcaseRoute().Beats` must appear exactly once in `beats[]` and in the same order.
- Every `beats[].qteType` must match the only action check type in the matching route chapter.
- Every beat must have non-empty `phase`, `title`, `dramaticPurpose`, `playerGoal`, `sceneFraming`, `branchPoints`, `consequenceHooks`, `carryForward`, and positive `pacingMinutes`.
- The map must represent the issue-required arc: preparation, approach, infiltration, reconnaissance, lock/security challenge, staff/NPC complications, theft, alarm/escalation, chase, return to hideout, and epilogue.
- The map must include future insertion points for a contact/informant, estate staff or guard, magical-security authority or house representative, and pursuit figure.
- The map must not require a new dialogue runtime, new QTE check type, separate browser-only route, separate console-only route, or reward/profile contract change.

## Console/Browser Contract

- Console and browser continue to consume the same C# QTE route and DTOs.
- The scene map may inform future authored `Narrative`, `SuccessText`, `PartialText`, `FailText`, ending, and UI copy, but this slice does not create separate browser/console content forks.
- Any future player-facing scene prose added from this map must remain free of raw endpoint, DTO, API, manual-grade, or debug/Spec Kit wording in default UI.

## Test Contract

Tests should validate structure and drift. They should not score literary style. A good test failure names the missing beat/field/stage so future agents can update the map in the same change as route edits.
