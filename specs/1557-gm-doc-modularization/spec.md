# Feature Specification: GM Documentation Modularization

> **Current IMPLEMENTATION continuation, 2026-10-17.** User explicitly selected this approved #1557 scope as fallback. [Current plan](plan.md) uses the published continuation branch on accepted a5/A1/A2 code and current GM source, selectively imports existing2601 work/history and continues coherent A. Historical frozen/release wording below does not force another design-only stop. No Block_5 before independent A acceptance, gameplay/provider/Windows/CI/security/merge/main/issue-close expansion or unchanged proof replay.

**Revision**: 1, 2026-10-13
**Status**: USER APPROVED revision1 (2026-10-13 16:17 UTC); A preparation released after ordinary13 freeze; implementation/evidence acceptance remains pending.
**Feature Branch**: `1557-gm-doc-modularization-cloud-20261013`; isolated worktree, frozen ordinary13 base `e8bbbf3948637bc4dbd1153be68673e0653b0f42`.
**Input**: Split oversized GM rules/examples into thematic files, with clear situation-specific reading instructions; preserve all requirements and examples without forcing the GM to read all modules for every turn.

## Source Issues & Scope

- **Source GitHub issue**: [#1557](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1557), OPEN; exactly one authorized issue created/verified 2026-10-13 16:22 UTC.
- **Issue type**: Documentation organization and bounded documentation-delivery/validation integration.
- **Spec Kit justification**: Multiple files and sessions; GM-facing rules/examples, launcher/daemon reading instructions, session documentation delivery, references, manifests and guards must change coherently.
- **Contract scope**: GM documentation/navigation and its existing delivery/verification consumers. Existing game mechanics, schemas, command authority, canonical state, receipts and publication semantics are preserved.
- **Save compatibility**: No game-save or canonical-schema change. Historical evidence and accepted receipts remain untouched.
- **Related handoff, not source tracker**: #1536 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536 records the approved design, but is not broadened into implementation ownership of all GM documentation.
- **Durable approved input**: `specs/1536-complete-wound-materialization/recovery/gm-documentation-modularization-approved-design-20261013.md`, coordinator-verified carrier `9abaf73a3352b3c7ad08f99c5bb1256ce2fbe668`. It preserves the 19,051-byte inventory payload with SHA-256 `41822128e8df1c691367456562df630e629832b53b02921a7f5c53e9dd34172f`.
- **Out of scope**: New gameplay/diagnosis policy; initial wound recommendation alignment; FR063 implementation/evidence; runtime packet redesign, RLM/context framework, caching or latency optimization; model/provider calls; Windows runs; full-suite verification; main/merge/PR operations; issue mutations beyond one separately authorized tracker creation; unrelated NPC/faction/item document reorganization.

## Clarifications

### Session 2026-10-13 — Structure and order approved

- Proposal: Sentinel_13f1026bc96881918c9a56578f512fca, 14:09 UTC.
- User acceptance: Sentinel_956b26d890e8819197aee2df392a8657, 14:10 UTC, «да».
- Approved: ten paired afterlife rule/example modules, compact common mandatory core, conditional situation router, active-topic dependency union, exact repair routing, complete session delivery, reference/manifest/guard updates, automatic preservation proof; Block_5 follows the accepted afterlife block.
- Structure/order and this exact revision1 were approved. Exact-spec/one-issue approval: Sentinel_17b7eb9dc8b08191affb9fe4a5c76723, 2026-10-13 16:17 UTC; historical approval record is preserved in recovery.

## User Scenarios & Testing

### US1 — Find complete afterlife guidance for the current task (P1)

A GM starts from a short common core and a situation router, then reads the complete rules and worked examples needed for the current realm, lifecycle, pending contracts or repair. Unrelated topic modules are not mandatory reading.

**Independent test**: For the scenarios below, follow the advertised entrypoint and verify that every required rule/example is reachable and applicable, while no instruction requires reading all topic bodies.

1. **Given** an ordinary afterlife turn without special pending work, **when** the GM follows the router, **then** common obligations and applicable realm/scheduler guidance are available without opening unrelated wound, faction, gacha or story examples.
2. **Given** several active pending contracts and due scheduler work, **when** reading is selected, **then** the union of their requirements and dependencies is required; no contract is silently omitted.
3. **Given** an issued spiritual wound continuation or validation repair, **when** the GM follows its current packet references, **then** the exact relevant rule/example and authority restrictions are available; reading a broader module grants no extra write authority.
4. **Given** an old matrix/example link, **when** followed, **then** the retained navigation entrypoint identifies the exact destination and stable example/contract identifier.

### US2 — Preserve and deliver every rule and example (P1)

A GM using the session-local documentation receives all advertised module dependencies, and a maintainer can prove that the split preserved the original normative material and verification coverage.

**Independent test**: Compare the frozen source inventory with destination content, references, scenario manifest and delivered session copies using the finite affected validation scope.

1. **Given** the frozen original documents, **when** relocated content is compared, **then** every normative block and example has one mapped destination with unchanged requirements, surrounding applicability, expected-valid/invalid classification and fragment order.
2. **Given** a copied session context pack, **when** its router/module links are followed, **then** every advertised file/anchor and dependency resolves inside that delivery; source availability alone does not count as delivery.
3. **Given** an example with multiple ordered JSON fragments or companion files, **when** its existing validation scenario is resolved, **then** the same logical inputs, ordering, expected writes/absences, coverage kind and limitations remain covered after path remapping.
4. **Given** a missing module, lost fragment, broken link, uncovered moved file or duplicate authority copy, **when** preservation/delivery guards run, **then** the defect is reported rather than hidden by an empty selection or weakened check.

### US3 — Apply the accepted organization to Block_5 (P2)

After US1/US2 are accepted, a GM can select the relevant Block_5 topic and its examples without opening the whole general-overview block.

**Independent test**: Map all original top-level Block_5 rules and corresponding examples to the seven proposed topic pairs below, preserving exact rule IDs, inherited scope and examples.

1. **Given** a units/IDs, combat, item/resource, progression, narrative/social, wound or chronology task, **when** the GM follows the Block_5 router, **then** the complete applicable rule and example group is accessible.
2. **Given** the original XML/CDATA material, **when** split, **then** rule identities, good/bad example classification, complete nested groups and applicability instructions remain intact.
3. **Given** unfinished afterlife delivery/preservation acceptance, **when** work is scheduled, **then** the Block_5 move waits rather than mixing two incomplete migrations.

## Document Destinations Proposed by This Specification

The afterlife themes and ordering are already approved. Exact surface names and the seven-way Block_5 partition are shown here for specification approval. The following are documentation surface names, not new gameplay interfaces. Each pair contains complete rules separately from worked examples; the detailed block/range move map belongs in planning before any content is moved.

| Topic | Rule file | Example file |
|---|---|---|
| Lifecycle | `OtherGuides/Afterlife_Lifecycle.md` | `Examples/E_CLI_Afterlife_Lifecycle.txt` |
| Scheduler | `OtherGuides/Afterlife_Scheduler.md` | `Examples/E_CLI_Afterlife_Scheduler.txt` |
| Guardian contracts | `OtherGuides/Afterlife_Guardian_Contracts.md` | `Examples/E_CLI_Afterlife_Guardian_Contracts.txt` |
| Abodes/residents | `OtherGuides/Afterlife_Abodes_Residents.md` | `Examples/E_CLI_Afterlife_Abodes_Residents.txt` |
| Shining actions | `OtherGuides/Afterlife_Shining_Actions.md` | `Examples/E_CLI_Afterlife_Shining_Actions.txt` |
| Spiritual conflict | `OtherGuides/Afterlife_Spiritual_Conflict.md` | `Examples/E_CLI_Afterlife_Spiritual_Conflict.txt` |
| Effects/resources | `OtherGuides/Afterlife_Effects_Resources.md` | `Examples/E_CLI_Afterlife_Effects_Resources.txt` |
| Actor profiles | `OtherGuides/Afterlife_Actor_Profiles.md` | `Examples/E_CLI_Afterlife_Actor_Profiles.txt` |
| Story/memory | `OtherGuides/Afterlife_Story_Memory.md` | `Examples/E_CLI_Afterlife_Story_Memory.txt` |
| Wound continuation | `OtherGuides/Afterlife_Wound_Continuation.md` | `Examples/E_CLI_Afterlife_Wound_Continuation.txt` |

- Common mandatory obligations: `OtherGuides/GM_Common_Core.md`.
- Main situation router: existing `CLI_Rules_Index.md`; no additional competing global router.
- Afterlife navigation entrypoints: existing `OtherGuides/Afterlife_Contract_Matrix.md` and `Examples/E_CLI_Afterlife_Turns.txt` become short routing/coverage maps, not second full authoritative copies.
- Block_5 navigation entrypoints: existing `Rules/Block_5.txt` and `Examples/E_Block_5.txt` after that phase is accepted.

| Block_5 topic | Rule file | Example file | Preserved original rule IDs |
|---|---|---|---|
| Units/IDs | `Rules/Block_5_Units_Ids.txt` | `Examples/E_Block_5_Units_Ids.txt` | 5.0, 5.8 |
| Characteristics/progression | `Rules/Block_5_Characteristics_Progression.txt` | `Examples/E_Block_5_Characteristics_Progression.txt` | 5.1, 5.7, 5.10, 5.24 |
| Combat foundations | `Rules/Block_5_Combat_Foundations.txt` | `Examples/E_Block_5_Combat_Foundations.txt` | 5.2–5.6, 5.16, 5.19 |
| Items/resources | `Rules/Block_5_Items_Resources.txt` | `Examples/E_Block_5_Items_Resources.txt` | 5.9, 5.11, 5.13–5.15, 5.23 |
| Narrative/social | `Rules/Block_5_Narrative_Social.txt` | `Examples/E_Block_5_Narrative_Social.txt` | 5.12, 5.17–5.18 |
| Wounds | `Rules/Block_5_Wounds.txt` | `Examples/E_Block_5_Wounds.txt` | 5.20 |
| World/chronology | `Rules/Block_5_World_Chronology.txt` | `Examples/E_Block_5_World_Chronology.txt` | 5.21–5.22, 5.25 |

## Functional Requirements

- **FR-001**: Preserve all original normative requirements and worked-example payloads. Shorter navigation MUST NOT replace complete rules with summaries or alter a game capability, cost, prerequisite, permission, receipt or acceptance rule.
- **FR-002**: Keep rules and examples in separate thematic files with explicit `Read when`, prerequisites/dependencies, authoring scope, prohibited writes and exact example references. Preserve whole dependent examples and inherited context.
- **FR-003**: Provide one compact common core of exact existing cross-topic obligations and one situation router. Preserve current request, realm/lifecycle, client-owned authority and terminal/repair distinctions. Do not impose ordinary-turn reading on special receipt-only tasks where existing contracts forbid it.
- **FR-004**: Ordinary-turn guidance MUST require only applicable topics and their dependency union. Multiple active contracts remain cumulative. No entrypoint may simultaneously require reading every module for all afterlife turns.
- **FR-005**: Preserve existing bootstrap restrictions. Availability of all modules in a session MUST NOT imply mandatory reading of all module bodies during bootstrap or every turn.
- **FR-006**: Every advertised documentation path/anchor and repair/worker reference MUST resolve in its actual delivery/read scope. A module present only in the repository does not satisfy a session-local reference.
- **FR-007**: Keep stable example numbers, suffixes, contract IDs, rule IDs, ordered fragments, good/bad markers and companion relationships. Do not silently renumber, merge, omit or reclassify them.
- **FR-008**: Maintain an auditable one-to-one content move map. Original payload is byte-preserved except explicitly inventoried path/link substitutions; those substitutions must be reversible for comparison. New headings/wrappers/navigation and the approved changes to reading directions are recorded separately, with reversible original/new correspondence. This permits the agreed conditional-reading policy without changing gameplay obligations. No blanket normalization may conceal changed requirements.
- **FR-009**: Retained old paths are compact navigation surfaces, not duplicate full sources of authority. The known all-afterlife-reading versus conditional-reading conflict is resolved only through the already approved navigation policy and its explicit edit inventory. Newly discovered gameplay/contract contradictions remain separate findings requiring a scoped decision; relocation must not silently resolve them.
- **FR-010**: Preserve manifest scenario/contract identities, inputs, expected modifications/absences/unchanged files, validation kinds, explicit exemptions and truthful coverage limits. Update affected path consumers and guards without reducing actual coverage.
- **FR-011**: Any missing/duplicate module, lost/reordered fragment, wrong valid/invalid classification or unresolved reference must fail a relevant bounded preservation/delivery check. Existing runtime proofs must not be relabelled as stronger evidence because documentation moved.
- **FR-012**: Complete and independently accept the afterlife/core/router/delivery block before moving Block_5. Keep unrelated document families, gameplay and evidence history outside both blocks.
- **FR-013**: Each accepted block MUST have fresh, impact-selected verification and independent review, with exact source version, selected/completed counts, failures/skips and delivery limitations. No full-suite, all-category sequence, paid GM run or latency target is introduced.

## Edge Cases

- A source row spans several topics: assign a complete owner and explicit dependencies, preserving all qualifiers; do not cut a clause merely to meet file-size targets.
- A numbered example includes multiple responses or a negative fragment: keep its order and local labels, even when broad topic headings change.
- A module name exists in packaged assets but is absent from the generated session pack: delivery remains incomplete.
- A guard recognizes only an old filename: update its covered file inventory; a passing check that silently stopped inspecting moved text is a failure of this feature.
- Several pending contracts activate different modules: require their union, including shared prerequisites, without duplicated authority documents.
- An old header link targets relocated text: retain an explicit destination mapping and update live references; do not preserve broken links as historical evidence.
- A current file changes after its source snapshot: reconcile against the final frozen base before moving it; do not overwrite the active FR063 writer.
- Previously accepted evidence/source snapshots are historical records: leave their bytes and claims unchanged; update active documentation references only.

## Key Entities

- **Documentation module**: one thematic owner, activation conditions, dependencies, normative content and example references.
- **Content block**: original file/range/ordinal, stable rule/example/contract identity, payload fingerprint, destination and any explicit path-only substitution.
- **Reading route**: situation, common obligations, selected topic dependency union and exact examples.
- **Validation coverage entry**: existing scenario identity, example association, input/expected behavior and evidence limitation; relocation changes paths, not asserted behavior.

These describe documentation and verification records; no new game-state entities are introduced.

## Success Criteria

- **SC-001**: Every block in the frozen scoped source inventory has exactly one documented destination or an explicitly approved navigation replacement; no normative payload is lost.
- **SC-002**: Every advertised scoped link/anchor/module is present and resolvable in its required repository/package/session delivery context.
- **SC-003**: All scoped example/rule/contract identities, ordered fragments and manifest scenario assertions are preserved; differences are limited to recorded document-location changes and new navigation.
- **SC-004**: The approved reading scenarios select all and only required topics/dependencies; unrelated topic bodies are never universally mandated by an entrypoint.
- **SC-005**: The finite affected checks and independent review accept each block with completeness and limits reported; source recovery alone is not reported as runtime validation.
- **SC-006**: Block_5 begins only after afterlife acceptance, and FR063, wound-recommendation policy and earlier evidence are unchanged by this feature.

No speed, token-saving, latency or maximum-file-length result is claimed. File boundaries follow coherent topics and content preservation.

## Verification Plan

- **Documentation/content**: frozen input/destination block comparison, reversible path substitutions, stable IDs/fence order/classification, link closure and exact manifest coverage correspondence.
- **Affected consumers**: focused documentation/source guards, example extraction/validation and actual package/session context-pack delivery/read-scope checks. The plan will name current category owners and a finite selection based on the eventual diff; no broad aggregate.
- **Negative controls**: missing delivered module, broken link, omitted/reordered fragment, duplicated block, lost old-path guard coverage and wrong expected-valid/invalid classification.
- **Preparation/execution**: official selected runner PlanOnly/fresh builds and then selected actual execution only after implementation scope/review gates are satisfied. No commands or passing results are claimed by this draft.
- **Frontend/player gameplay**: no player UI or game-state behavior change; broad console/browser/gameplay reruns are outside scope. GM reading/delivery behavior is the verification target.
- **Platform**: Linux evidence only where actually exercised. No unrun Windows path is declared proven.

## Dependencies and Approval State

- The approved structure/order is fixed by the clarification above; this draft does not reopen it.
- Coordinator accepted/froze bounded ordinary13 and separate APInegative5 at e8bbbf3948637bc4dbd1153be68673e0653b0f42 and released isolated A preparation. Mandatory live pending/new-root/UI/full FR063 remain separately OPEN.
- #1557 and exact revision1 approval are verified; planning independent/coordinator gates and D01 disposition are preserved. This isolated feature establishes the current execution context. SOURCE/runtime/EVIDENCE gates remain required.
- A proceeds only in this published isolated feature with one global source writer. B waits for independent A acceptance. No new issue creation, hook-induced branch changes or primary feature-context change is authorized.
