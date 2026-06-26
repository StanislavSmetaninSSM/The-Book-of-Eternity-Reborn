# Research: RLM-Inspired GM Harness

## Decision: Use RLM as an architecture pattern, not as a direct dependency

**Rationale**: The useful RLM ideas are context-as-object, recursive subcalls, trajectory logging, and trainable/evaluable rollouts. Directly embedding the upstream Python REPL would add unsafe arbitrary execution and another runtime dependency. The project already has a safer local harness: daemon helpers, context packs, validators, repair packets, rollback, and worker proposal gates.

**Alternatives considered**:
- Add upstream `rlms` package directly. Rejected for v1 because local REPL execution is not a safe gameplay boundary.
- Prompt-only RLM imitation. Rejected because the project methodology requires harness engineering before prompt-only fixes.
- Build only worker delegation. Rejected because without trajectory and reward records there is no way to know whether delegation improves live play.

## Decision: Start with trajectory/reward ledger

**Rationale**: Experience memory, retrieval, and live-test scoring need normalized input. A ledger creates a compact audit surface over existing logs without removing detailed diagnostics.

**Alternatives considered**:
- Continue manual notes only. Rejected because repeated failures cannot be automatically retrieved.
- Store full prompts and outputs in one giant log. Rejected because it risks secrets, huge context bloat, and poor retrieval quality.

## Decision: Store experience lessons as derived compact artifacts

**Rationale**: The GM needs a few relevant hints, not raw historical logs. Lessons can reference issue kind, realm, accepted fix, and preferred harness tool. They should be versioned by contract/template identity to avoid stale advice.

**Alternatives considered**:
- Always include all prior failures. Rejected for prompt size and stale-context risk.
- Never include prior failures. Rejected because it wastes live-test learning.

## Decision: Safe probes replace arbitrary REPL/file access

**Rationale**: RLM's REPL is valuable because it gives the model programmatic context access. In this game, that access must be harness-owned: generated summaries, specific repair packets, allowed target files, and read-only context probes.

**Alternatives considered**:
- Give the GM a repo-root shell. Rejected because prior live tests already showed harmful implementation-code spelunking.
- Give no programmatic context tools. Rejected because the GM then reads giant docs and makes preventable contract mistakes.

## Decision: Worker delegation remains proposal-only

**Rationale**: Recursive subcalls map well to hidden worker bridges, but game-state authority must stay with validators, apply gates, and the main GM/harness. Workers can propose narrative, analysis, repair, and content changes; they do not own canonical writes.

**Alternatives considered**:
- Let workers write canonical game state directly. Rejected because it bypasses validation and rollback authority.
- Disable workers until all specialist roles are complete. Rejected because the shared delegation path can be tested with a small subset first.
