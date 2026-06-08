# Feature Specification: Afterlife combatConditions layer (#898)

**Source issue:** https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/898
**Status:** Active implementation slice for the shared afterlife spiritual-combat condition contract.

## User Stories

### Story 1: GM records temporary spiritual-combat conditions

As the GM resolving an afterlife spiritual conflict, I need a first-class `combatConditions[]` layer so temporary marks, wards, burdens, openings, and vows can be created, consumed, expired, and audited without inventing hidden ad-hoc modifiers.

**Acceptance:** Active/recent conflict state and relevant exchange snapshots can contain structured combat conditions with identity, source, target, kind, affected operations, mechanical axis, payoff, duration, counterplay, visibility, summary, and audit requirement.

### Story 2: Player sees visible tactical conditions

As a player, I need visible active conditions to explain what is easier, harder, costlier, risky, protected, or vulnerable in the current spiritual conflict, without exposing raw JSON or hidden Saref/Wings spoilers.

**Acceptance:** Console/player-facing afterlife combat/profile/log surfaces summarize visible conditions with name, source, target side/actor, affected operations, remaining duration/uses, counterplay, and short summary. Hidden or GM-only conditions remain hidden from ordinary player output.

### Story 3: Validator rejects malformed or illegal condition state

As a maintainer, I need validation to prevent malformed, stale, unaudited, or illegal condition entries from becoming canonical afterlife combat state.

**Acceptance:** Validation accepts absent `combatConditions` for backward compatibility, validates present active entries, rejects unsupported kinds/mechanical axes, missing required fields, stale active expired entries, roll-mode sources that reference missing conditions, illegal operation-to-payoff mappings, and player-visible spoiler text where existing Saref/Wings visibility rules can identify it.

### Story 4: GM-facing docs and examples teach the contract

As a future GM/agent, I need documentation and worked examples showing how to create, consume, expire, audit, and display combat conditions, including Guardian/Saref-linked effects without premature story disclosure.

**Acceptance:** GM prompt/docs/examples and documentation coverage tests are updated in the same change. Examples cover `mark`, `ward`, `burden`, `opening`, `vow`, consumption through `rollMode` or another legal axis, expiration/counterplay, and at least two Predvechnye Guardian special-art-adjacent examples that remain compatible with follow-up #897/#894.

## Functional Requirements

1. Add/document `combatConditions[]` for afterlife spiritual conflicts and relevant before/after exchange snapshots.
2. Support first-class condition kinds: `mark`, `ward`, `burden`, `opening`, `vow`.
3. Preserve the existing spiritual-combat matrix: conditions may map only to legal axes such as `rollMode.*.advantageSources/disadvantageSources`, `conflictPosition`, `controlState`, side strain, `tempoAdvantage`, `counterPayoff`, and `actionCostAudit`/ОД costs.
4. Conditions must not become indefinite passive `+X` stat stacking, unlimited stacking, duplicate `controlState`, Mortal HP/status vocabulary, or hidden state without source/target/duration/counterplay.
5. Conditions must have auditable lifecycle state: active, consumed, expired, or blocked/cleared if implementation chooses explicit status names.
6. When a condition affects a roll, dice/roll audit sources must reference the condition identity or stable display name and the exchange must explain the fictional trigger.
7. Backward compatibility: old profiles/conflicts without `combatConditions` remain valid.
8. Related issues #897, #894, and #896 remain separate follow-ups unless a small compatibility hook is necessary. This issue creates the shared vocabulary and runtime/docs/validation/UI foundation.

## Contract Scope

- **Runtime/canonical state:** afterlife spiritual conflict state and exchange snapshots.
- **Validation:** afterlife spiritual conflict/profile validation for active condition shape and condition-backed roll/action audit references.
- **Console/player UI:** afterlife combat/profile/log surfaces showing visible active conditions.
- **Browser UI:** only if the shared DTO/output already renders the affected blocks; do not invent React gameplay logic in this issue.
- **GM-facing docs/prompts/examples:** required because this is a GM-authored afterlife contract.
- **Afterlife contract docs/tests:** required by AGENTS.md and the afterlife contract guardrail.

## Out of Scope

- Adding `specialArts[].combatEffect` as the structured source field for arts (#897).
- Rewriting all Predvechnye Guardian dossier arts to the new model (#894).
- Final broad regression examples after #897/#894 stabilize (#896), except for the minimum examples required to prove #898 itself.
- A deterministic mini-engine that applies every art automatically; GM remains the authority, with validation/audit guardrails.
- Changing Mortal-world combat/status contracts.

## Verification Requirements

Minimum local gates for this issue:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- Focused validation/UI tests covering combat-condition valid/invalid state and visible/hidden player output.
- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal`
- `git diff --check origin/main...HEAD`
- Added-line static scan excluding `specs/**` for hardcoded secrets, shell execution, eval/exec, pickle, and SQL string-formatting patterns.

GitHub Actions are not required for this repository's normal local-gated workflow unless Stanislav explicitly asks.
