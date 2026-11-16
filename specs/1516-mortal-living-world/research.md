# Research decisions

Source #1516; see plan.md for exact owner map.

- Chosen: explicit structured assessment plus canonical evidence checked by existing transaction owners. Prompt-only enforcement cannot prevent frozen state; independent simulation would invent a new gameplay system and violate scope.
- Chosen: existing cycle configuration determines meaningful elapsed time, including newly crossed intervals. Pending-cycle counters alone are insufficient.
- Chosen: separate private report/history. Current news renderer exposes arbitrary event fields; putting hidden causes there would leak information.
- Chosen: existing faction/NPC/project reducers, followed by exact evidence check. Arbitrary JSON patching would bypass authority and lifecycle contracts.
- Chosen: current supported fixtures/bootstrap only, no old-save promotion migration. Later direct user instructions and pre-release governance supersede historical legacy wording.
- Still implementation work, not a product question: exact pre-seal joint/QTE receipt integration and current-root identity resolution. These remain explicit unchecked tasks rather than assumed coverage.
