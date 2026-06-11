# Feature Specification: Daren Narrative Spine and Scene Map

**Feature Branch**: `work/956-daren-narrative-spine`
**Created**: 2026-06-11
**Status**: Draft for autonomous implementation
**Source Issues**: [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), base scenario [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Source Issues & Scope

- **Source GitHub issue(s)**: #956 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956>.
- **Parent**: #955 — Daren adventure should become an interactive-book style QTE route using existing QTE mechanics.
- **Related base implementation**: #919 — client-owned Daren QTE training/showcase route and reward/profile flow.
- **Issue type**: player-facing QTE UX/content planning, console/browser shared QTE contract, durable authoring handoff.
- **Spec Kit justification**: #956 changes the authored scenario backbone that future prose, dialogue, branch, ending, and quality-gate tasks will implement. It affects player-facing QTE route structure and console/browser parity expectations, so the scene map must be durable.
- **In scope**: a complete scene-map authority for Daren's existing route beats, pacing expectations, dramatic purpose, player goal, QTE mechanic, branch points, consequence carry-forward, and implementation/test hooks that future child issues can follow without inventing the story again.
- **Out of scope**: adding a new one-off dialogue engine, changing Daren reward thresholds, changing New Game reward grants, adding new QTE check types, rewriting browser mini-games, and implementing all prose/dialogue/endings from #957-#961 in this slice.

## User Scenarios & Testing

### Scenario 1 - Future implementer can follow the scene map (Priority: P1)

A future implementation agent opens the Daren narrative spine and can see the full 20-30 minute heist arc from preparation through epilogue, mapped to every current route beat and QTE mechanic.

**Independent Test**: Add or update a source/fixture guard that reads the scene-map authority and asserts every current Daren beat id from `QteSceneService.GetDarenShowcaseRoute()` has a named scene entry with dramatic purpose, player goal, QTE mechanic, branch point notes, and consequence hooks.

**Acceptance Scenarios**:
1. **Given** the route has the beat `approach_manor`, **When** the scene map is validated, **Then** it names the preparation/approach role rather than a bare mechanic.
2. **Given** the route includes all existing QTE nodes, **When** the scene map is validated, **Then** every node has a unique narrative role and no duplicate or missing beat id remains.
3. **Given** a future prose task starts, **When** it reads the map, **Then** it can identify where NPCs, dialogue, complications, chase pressure, and epilogue material should be authored.

---

### Scenario 2 - The route reads as a paced interactive-book heist (Priority: P1)

The planned route has a coherent arc: preparation, approach, infiltration, reconnaissance, lock/security challenge, staff complications, theft, alarm/escalation, chase, return to hideout, and epilogue.

**Independent Test**: Add a guard that verifies the scene map declares the expected arc phase for each beat and that the phases appear in route order without skipping the major issue-required stages.

**Acceptance Scenarios**:
1. **Given** a player starts Daren showcase, **When** the planned beats are read in order, **Then** the arc progresses from preparation/approach into infiltration, magical security, theft, alarm, chase, hideout, and epilogue.
2. **Given** the route uses a mechanical QTE check, **When** the scene-map entry describes it, **Then** the mechanic is framed by stakes and player intent instead of debug/tutorial wording.
3. **Given** the expected playtime is 20-30 minutes, **When** the scene map is reviewed, **Then** it marks approximate scene weight or pacing notes that keep the route short enough for console and rich enough for interactive-book presentation.

---

### Scenario 3 - Branch and consequence hooks are ready for follow-up tasks (Priority: P1)

The scene map explains where success, partial success, failure, dialogue choice, NPC reaction, suspicion, evidence, pursuit, loot, and hideout-safety consequences should appear later.

**Independent Test**: Add guard coverage that rejects scene-map entries without at least one consequence hook and rejects maps that do not carry earlier consequences into later beats.

**Acceptance Scenarios**:
1. **Given** an earlier stealth mistake, **When** later scenes are authored from the map, **Then** the map identifies at least one later beat where suspicion or pursuit pressure can be referenced.
2. **Given** an NPC/dialogue follow-up task, **When** it reads the map, **Then** it can find the intended contact/informant, estate staff/guard, magical-security authority or house representative, and pursuit figure insertion points.
3. **Given** future ending work, **When** it reads the map, **Then** it can trace which consequences should influence poor, mixed, good, and excellent endings.

---

### Scenario 4 - Console and browser remain consumers of the same QTE contract (Priority: P1)

The scene map documents shared authored data and test expectations without creating separate console-only or browser-only story routes.

**Independent Test**: Existing Daren route tests plus new scene-map guards prove the authoritative beat ids and QTE types are shared. Browser and console tests remain consumers of `QteSceneService.GetDarenShowcaseRoute()` and related DTOs.

**Acceptance Scenarios**:
1. **Given** console Daren showcase renders a chapter, **When** future prose work lands, **Then** it uses the same chapter/beat data that browser receives.
2. **Given** browser Daren showcase renders a chapter, **When** future prose work lands, **Then** no browser-only story copy is invented to cover missing C# route data.
3. **Given** this task only defines the spine, **When** implementation is complete, **Then** it does not add a separate one-off scenario runtime.

## Edge Cases

- Existing Daren route beat ids change in future work: the scene-map guard fails and forces the map to be updated in the same change.
- A QTE mechanic is reused for two beats: the scene map still requires distinct narrative roles and player goals for each beat.
- A future branch task wants a consequence not in the map: it must update the map/spec instead of silently drifting.
- Scene-map content is documentation-like but player-facing in authority: tests should fail on missing structure, not on literary style preferences.
- Follow-up tasks #957-#961 should be able to add prose/dialogue/branches/endings without changing reward thresholds or campaign-mutation boundaries from #919.

## Functional Requirements

- **FR-001**: The repository MUST contain a Daren narrative spine/scene-map artifact linked to #956 and #955.
- **FR-002**: The scene map MUST cover every existing Daren route beat id in route order.
- **FR-003**: Each beat entry MUST define phase, dramatic purpose, player goal, QTE mechanic, intended scene framing, branch/consequence hooks, and later carry-forward notes.
- **FR-004**: The scene map MUST cover the issue-required arc: preparation, approach, infiltration, reconnaissance, lock/security challenge, staff/NPC complications, theft, alarm/escalation, chase, return to hideout, and epilogue.
- **FR-005**: The planned flow MUST preserve the 20-30 minute target and avoid walls of text unsuitable for console.
- **FR-006**: The scene map MUST identify where future NPC/dialogue tasks can introduce at least a contact/informant, estate staff or guard, magical-security authority or house representative, and pursuit figure.
- **FR-007**: The scene map MUST identify where success/partial/failure outcomes and earlier choices can visibly affect later prose or ending material.
- **FR-008**: Tests or source guards MUST fail if the scene map omits a current beat, omits a required structural field, or drifts away from the shared Daren QTE route.
- **FR-009**: Implementation MUST keep console and browser clients as consumers of the same shared C# QTE route/contract.
- **FR-010**: Implementation MUST NOT introduce a separate dialogue/scenario runtime, new QTE check type, or reward/profile contract change for this slice.

## Non-Functional Requirements

- **NFR-001**: Scene-map copy should be concise, actionable, and suitable for future implementation agents.
- **NFR-002**: Validation should check structure/presence and known ids, not subjective literary quality.
- **NFR-003**: The new artifact should live in a stable repo path that is not scratch or local-only state.
- **NFR-004**: Existing Daren reward/profile/New Game tests should continue to pass unchanged unless a future tracked task intentionally changes those contracts.

## Success Criteria

- The scene map is committed as durable product/planning documentation or fixture data.
- Every existing Daren QTE node has a named narrative role and story-stage placement.
- Tests/guards tie the scene map to `QteSceneService.GetDarenShowcaseRoute()` so future route drift is caught.
- The final PR documents that this is the planning/spine slice for #956 and that #957-#961 remain implementation/content follow-ups under #955.
