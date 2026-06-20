# Contract: Console Live Playtest Boundary

Source issue: #1157

## Player-Side Boundary

During active play, the player-side agent may use only Agent Console snapshots, events, structured actions, text input, and player-facing stdout/stderr captured as artifacts.

The player-side agent must not inspect `game_session` JSON, source code, schemas, prompts, or validation internals during active play. Those are allowed only for setup, teardown, or post-failure debugging.

## GM-Side Boundary

The GM bridge must launch a separate Codex CLI through normal bridge configuration:

```text
codex --dangerously-bypass-approvals-and-sandbox
```

The GM must not receive the playtest plan, hidden checklist, or instructions that it is being tested. Normal GM prompt context is acceptable because that is part of ordinary play.

## Covered Player Surfaces

The run should cover as many of these as the lifecycle allows:

- `/статус`
- `/инв`
- `/книги`
- `/эффекты`
- `/навыки`
- `/квесты`
- `/нпс` or visible NPC equivalent
- `/фракции` or visible faction equivalent
- `/карта` and location/navigation equivalents
- `/новости_мира`
- combat/QTE entry points
- death/end-life/afterlife reward surfaces

## Defect Classification

- **P0**: Crash, permanent hang, session corruption, or client cannot start.
- **P1**: Progress requires manual file edits, hidden schemas, developer knowledge, or bridge surgery.
- **P2**: Mechanically works but is confusing, misleading, incomplete, visually broken, or likely to make a normal player abandon the flow.
- **P3**: Low-risk polish issue.

## Fix Policy

Fix in this issue only when the defect is console/player-facing, narrow enough for focused regression coverage, does not require browser/frontend redesign, and does not silently change GM-authored contracts without docs/examples/tests or a follow-up issue.
