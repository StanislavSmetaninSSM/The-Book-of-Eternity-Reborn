# Contract: Safe GM Probes

## Purpose

Expose RLM-like programmatic context access through bounded harness-owned probes, not arbitrary repo-root shell or implementation source browsing.

## Probe categories

- Current realm and mode summary.
- Active pending/control contracts.
- Active actors, guardians, NPCs, factions, locations, inventory, or afterlife profiles relevant to the turn.
- Validation issue explanation and repair packet summary.
- Allowed output template and target file summary.
- Rollback baseline/status summary.
- Enabled worker role/task type summary.

## Probe rules

- Probes are read-only unless explicitly documented as routing through an existing validated gate.
- Probe output must include source authority and limitations.
- Probe output must be compact enough for live-turn use.
- If a probe needs implementation knowledge, that is a missing-harness-surface finding.

## GM prompt rule

The GM should prefer safe probes, compact templates, repair packets, and session-local context pack files before considering implementation source. Ordinary play and repair prompts must not present implementation source as the normal source of truth.
