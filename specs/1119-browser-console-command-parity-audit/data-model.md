# Data Model: Browser Console Command Parity Audit

## Coverage Entry

- **Command ID**: Stable identifier from browser command coverage.
- **Aliases**: Player-entered aliases for the command.
- **Realm/Group**: Mortal World, Chaos Sea, Shining Abode, local turn, write flow, blocked, or advanced-only family.
- **Browser Status**: Current browser migration classification.
- **Handler Kind**: How the browser command service handles the command.
- **Audit Status**: Adequate, tracked follow-up, advanced-only, blocked, or equivalent coverage metadata.
- **Follow-up Issue**: GitHub issue owning any implementation gap.

## Audit Row

- **Command ID**
- **Aliases**
- **Realm**
- **Browser surface**
- **Console sections**
- **Browser sections**
- **Missing browser details**
- **Raw JSON dependency**
- **Drill-down status**
- **Priority**
- **Follow-up issue**
- **Notes**

## Priority

- **P0**: Blocker; browser command is unusable or dangerously misleading.
- **P1**: Major player-facing information loss.
- **P2**: Notable quality or navigation gap.
- **P3**: Adequate, intentionally advanced-only, intentionally blocked, or minor follow-up.
