# Contract: Experience Memory

## Purpose

Convert prior trajectory records into compact, relevant lessons that can be included in a future session-local context pack.

## Selection rules

- Match by realm, mode, issue kind, task type, and contract/template version.
- Prefer successful accepted repairs over failed attempts.
- Prefer newer records when multiple lessons conflict.
- Exclude or mark stale lessons tied to obsolete contract/template versions.
- Enforce a maximum lesson count and maximum serialized size.

## Minimum lesson shape

```json
{
  "lessonId": "gmlesson_...",
  "sourceRecordIds": ["gmtraj_..."],
  "match": {
    "realm": "ChaosSea",
    "mode": "validation_repair",
    "issueKinds": ["actor_reasoning_subpoint_repair"],
    "taskTypes": []
  },
  "versions": {
    "contract": "afterlife-v...",
    "template": "turn-output-v1"
  },
  "badPattern": "GM omitted required actor subpoints.",
  "acceptedFix": "Use separate Situation, Thoughts, and Actions bullets for each canonical actor.",
  "preferredHarnessSurface": "ACTOR_REASONING_TEMPLATE.md",
  "confidence": "high",
  "lastSeenAt": "2026-06-26T00:00:00Z"
}
```

## Context-pack rendering

Lessons rendered for the GM must be short, action-oriented, and explicitly subordinate to validators and current templates.
