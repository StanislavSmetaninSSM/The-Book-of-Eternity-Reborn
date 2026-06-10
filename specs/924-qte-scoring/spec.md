# Feature Specification: QTE Score Metrics and Ending Ranks

**Feature Branch**: `work/924-qte-scoring`
**Created**: 2026-06-10
**Status**: Draft for autonomous implementation
**Source Issues**: [#924 QTE scoring metrics and ending ranks](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/924), parent [#911 QTE v2](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911), consumer [#919 Daren training mode](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919), related [#918 Browser QTE parity](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/918) and [#925 QTE Practice Mode](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/925)

## User Scenarios & Testing

### Scenario 1 - GM authors a scored long QTE scene (Priority: P1)

An ordinary GM-authored QTE offer can define a score model with named metrics, bounds, visibility, grade-based deltas, and rank thresholds. The client validates the model before play, applies deltas as QTE actions resolve, and shows a final metric/rank summary before or with the terminal outcome.

**Independent Test**: Add validation and runtime tests with a normal scored QTE offer that is not Daren-specific. Resolve a deterministic sequence of `success`, `partial`, and `fail` action results and assert final metric values, final rank, final summary text, and history/audit records.

**Acceptance Scenarios**:
1. **Given** a QTE offer with a score model and two actions, **When** the actions resolve with deterministic grades, **Then** configured metric deltas are applied locally and clamped to metric bounds.
2. **Given** final metric values that match a rank threshold, **When** the scene reaches a terminal outcome, **Then** the client records and displays the deterministic final rank and score summary.
3. **Given** a short QTE offer without a score model, **When** the scene resolves, **Then** existing v1/v2 QTE behavior remains unchanged.

---

### Scenario 2 - Malformed score models are rejected before play (Priority: P1)

Validation catches invalid metric ids, duplicate metrics, invalid bounds, unknown metric references, invalid visibility values, malformed grade deltas, and impossible rank thresholds. Error messages name the score-model field and preserve existing QTE validation behavior.

**Independent Test**: Extend validation tests with one valid score model and focused invalid cases for duplicate ids, `min > max`, initial values outside bounds, unknown metric references in deltas/thresholds, invalid grade keys, and rank thresholds that cannot be evaluated deterministically.

**Acceptance Scenarios**:
1. **Given** a score model with duplicate metric ids, **When** validation runs, **Then** validation rejects the offer and names the duplicate metric.
2. **Given** a score delta referencing an unknown metric id, **When** validation runs, **Then** validation rejects the exact action/grade delta field.
3. **Given** rank thresholds with unknown metrics or empty rank ids, **When** validation runs, **Then** validation rejects the score model before the QTE is playable.

---

### Scenario 3 - Console and browser show player-facing scoring information (Priority: P2)

Console and browser clients expose equivalent score information without making React a gameplay authority. During a scored QTE, player-facing surfaces can show visible metrics according to visibility rules. At completion, both clients can show the final score summary/rank. Browser state projection includes read-only score state and audit data; C# remains the mutation and history authority.

**Independent Test**: Add console/browser API tests that assert scored QTE state includes visible metric labels/values, hides hidden metrics until final if configured, and includes final rank/summary after completion. Add frontend tests for the Browser QTE panel/result surface to render score metrics without raw DTO/API/debug wording.

**Acceptance Scenarios**:
1. **Given** a scored QTE with visible metrics, **When** the player views the active scene, **Then** console/browser show player-facing metric labels and current values.
2. **Given** a hidden-until-final metric, **When** the scene is active, **Then** default player UI does not reveal the hidden current value, but final summary can show it if configured for final visibility.
3. **Given** a scored QTE completes, **When** browser/console show the result, **Then** the final rank and metric summary are visible without raw endpoint, DTO, JSON, file path, or manual-grade debug language.

---

### Scenario 4 - Daren and practice modes can consume the standard model later (Priority: P3)

The score model is generic and reusable. It does not add Daren-only fields, permanent rewards, achievements, Ink Feathers, inventory resources, or practice-mode persistence. #919 and #925 can later reference the standard model without changing this core contract.

**Independent Test**: Source guards/docs assert examples use generic metric ids (`stealth`, `evidence`, `alarm`, etc.) and do not add Daren-only reward fields to the core QTE score model.

## Requirements

### Functional Requirements

- **FR-001**: A QTE offer MAY define an optional `scoreModel`. Offers without `scoreModel` MUST keep existing QTE behavior.
- **FR-002**: `scoreModel.metrics[]` MUST support stable metric ids, player-facing labels, initial values, min/max bounds, and visibility rules.
- **FR-003**: Score deltas MUST be tied to action resolution grades `success`, `partial`, and `fail` and MUST support adding/subtracting values from one or more metrics.
- **FR-004**: Score application MUST be local and deterministic inside the C# QTE runtime; React/browser code MUST NOT mutate score state directly.
- **FR-005**: Metric values MUST clamp to their configured min/max bounds after every delta.
- **FR-006**: Ending rank selection MUST be deterministic and based on configured rank thresholds evaluated from final metric values.
- **FR-007**: QTE history/audit records MUST include initial metrics, applied deltas with action/grade, final metrics, final rank, and enough explanation for tests/GM debugging.
- **FR-008**: Console player-facing UI MUST show active/final score information according to visibility rules.
- **FR-009**: Browser QTE state and result surfaces MUST render the same scoring model/read-only score state after #918 browser QTE parity, while keeping C# as write authority.
- **FR-010**: Validation MUST accept well-formed score models and reject malformed metrics, invalid bounds, duplicate metric ids, impossible/unresolvable thresholds, unknown metric references, invalid visibility values, and invalid grade-delta definitions.
- **FR-011**: GM-facing QTE docs/examples/source guards MUST explain how ordinary GM-authored scored QTE scenes work and include at least one worked example.
- **FR-012**: The core score model MUST NOT include Daren-only gadget/resource/reward fields, practice-mode rewards, achievements, Ink Feather grants, or inventory mutation.

### Key Entities

- **QTE Score Model**: Optional authored contract on a QTE offer that defines metrics, grade deltas, visibility, and ending ranks.
- **Score Metric**: Named bounded counter such as `stealth`, `speed`, `evidence`, `lootIntegrity`, `alarm`, or `staffCondition`.
- **Score Delta**: Grade-specific metric adjustment applied when a QTE action resolves.
- **Score Rank**: Deterministic final classification such as `bad`, `partial`, `good`, or `best` selected by threshold rules.
- **Score Audit Record**: History entry explaining which action/grade changed which metric and how final rank was derived.

## Out of Scope

- Daren mini-adventure content, permanent achievements, New Game Ink Feather rewards, profile unlocks, or reward balancing (#919).
- Standalone QTE Practice Mode menus, attempt loops, or reward isolation (#925).
- Adding a general RPG resource/gadget/inventory system to QTE.
- Replacing existing QTE v1/v2 action types or changing RU/EN key-layout handling from #920.
- Afterlife pending/control contracts; this feature changes QTE authoring/runtime docs, not Chaos Sea/Shining Abode pending files.

## Success Criteria

- Focused validation/runtime/docs tests pass with non-zero counts and cover valid/invalid scored QTE models.
- Browser/frontend verification passes when browser score surfaces are touched.
- Existing unscored QTE v1/v2 tests remain green.
- `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, source guards, and Spec Kit artifacts stay synchronized.
- Issue #924 can be closed without implementing #919 Daren rewards or #925 Practice Mode.
