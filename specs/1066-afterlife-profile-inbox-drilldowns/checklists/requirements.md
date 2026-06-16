# Requirements Quality Checklist: Afterlife Profile and Inbox Follow-Through Drill-Downs

Source feature: `specs/1066-afterlife-profile-inbox-drilldowns/spec.md`

## Completeness

- [X] Source GitHub issue #1066 is linked.
- [X] Origin audit #949 AFD-005 and `docs/audits/afterlife-drilldown-audit.md` are linked.
- [X] User stories cover profile/threat/chronicle selected details, inbox follow-through, and read-only contract preservation.
- [X] Requirements include overview preservation, selected-detail actions, missing/stale targets, default no-raw/no-debug copy, and no runtime contract churn.
- [X] Out-of-scope sibling issue #1067 is explicit.
- [X] Verification plan includes focused tests, broader afterlife/browser slice, builds, Spec Kit prerequisite, diff check, static scan, and conditional docs/frontend gates.

## Ambiguity check

- [X] “Follow-through” means read-only navigation/detail/context actions, not mutating acknowledgement or local-turn writes.
- [X] Unsupported targets have a defined safe behavior: player-facing unavailable output.
- [X] Advanced/debug output is allowed only through explicit existing advanced/debug pathways.
- [X] Runtime/GM contract changes are out of scope unless same-PR docs/tests are added.

## Scope check

- [X] The feature is focused on #1066 / AFD-005.
- [X] #1063, #1064, #1065, and #1072 are treated as already-handled context, not reopened scope.
- [X] #1067 spiritual conflict exchange/art drill-downs remain separate.
- [X] Browser React/Vite work is avoided unless proven necessary for presentation-only rendering.
