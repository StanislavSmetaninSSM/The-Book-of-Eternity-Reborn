# Spiritual source envelope GM synchronization implementation plan

> For agentic workers: use subagent-driven-development and repository TDD/verification. The complete seven-file change is in the adjacent `2026-09-08-spiritual-source-envelope-gm-sync.patch`; this prose does not duplicate its bodies.

**Source issue:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), T081-B2C-J2-B and T089-T092. User source-envelope rule approved and tracked at 1cb6afd1.

**Goal:** Teach the GM the exact optional canonical special-art declaration, prove the authored example with the production parser, and state the limits of this source-only prerequisite honestly.

**Architecture:** The owning guide defines `profiles[].specialArts[].spiritualWoundEnvelope`; existing matrix and primary CLI guide route to it. One named complete source-art example and manifest row are checked by Fast text guards and the existing Integration example class. B's runtime companion owns the closed parser, profile-validation callers, signed acquisition, and semantic source tests; this plan neither duplicates them nor claims full wound admission.

**Tech stack:** Existing C#/.NET8, xUnit, System.Text.Json and PowerShell7 lane runner. No package, frontend, game setting, new branch or migration.

## Global constraints

- Worktree E:/Games/worktrees/boe-1536-wound-materialization; all patch headers are absolute. Apply edits only with apply_patch. Do not touch .serena, unrelated files, remote state, issues, worktrees, or sessions.
- Main owns this seven-file patch, while the B architect owns metadata only. No C# lane or source/HEAD writer may overlap another lane. Source/API changes remain with B.
- Ordinary/special-without-declaration source cap is neutral IV, guarantee absent, ordinary harmful-strain eligibility preserved. Pre-materialized special restrictions/guarantees obey formula/resilience, destination strain and danger. No caller cap, current audit, prose, DTO or hash grants prior authority.
- Exact declaration owner is the canonical profile special-art row, including player_soul, not soul artTiers. Closed object exactly schemaVersion1, maximumSeverityRank integer0..4, guaranteedSeverityRank null or integer1..maximum. Missing optional whole field is ordinary; null/malformed present field is invalid.
- No automatic wound, dice bypass, extra resource spend, progression or turn. Source preparation is not admitted opportunity/publication. No current test or example may pretend a full signed source or accepted effect/resource prefix exists.
- The named JSON example is a complete source-art fragment, not a complete profile/update/turn. The manifest must describe that exact coverage limit. Existing source learning/materialization/progression preserve the full field; runtime B tests own that behavior.
- Main checks Mortal/afterlife prompt/docs/example boundaries. Mortal mechanical authoring is unchanged; the shared wound guide distinguishes the afterlife-only source field. Matrix, primary prompt and worked example are updated together. Existing daemon/launcher entrypoints already mandate those guides; no new transport/command is introduced by this unit.

## Exact owned files

1. BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.SpiritualSourceEnvelope.cs (new two Fast source guards).
2. OtherGuides/Wound_Materialization_Contract.md (authoritative source-only subsection).
3. OtherGuides/Afterlife_Contract_Matrix.md (routing paragraph).
4. TaskGuides/CLI_Step_Main.txt (mandatory targeted guidance).
5. Examples/E_CLI_Afterlife_Turns.txt (named complete source-art fragment and boundary explanation).
6. Examples/example_validation_manifest.json (one afterlifeEntityProfileCoverage row, honest focused fragment limit).
7. BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.SpiritualSourceEnvelope.cs (new one production-parser example/manifest check through existing class helpers).

## Task 1: Exact GM source contract and executable example

- [x] Parent inspects complete companion and tracks this plan under T089-T092 before source/test/document edits.
- [x] Add only the Fast guard file from the companion. Run Focused Filter FullyQualifiedName~SpiritualSourceEnvelopeDocumentation_. Expected semantic RED is missing exact new source guidance, not a compile/setup failure. Preserve actual artifact.
- [x] Apply the five documentation/example/manifest changes. Run the same Focused selection GREEN. The parser-based Integration file is not added until B's SpiritualWoundSourceEnvelope API has been implemented; do not produce a compilation-only RED.
- [ ] Once B supplies its reviewed production parser, add the final Integration partial. It reuses ExtractNamedJsonFence and the class-level FullValidation lane; it parses the exact JSON actually printed to the GM, not a hand-copied fixture. Run Focused Integration Filter FullyQualifiedName~SpiritualWoundSourceEnvelope_DocumentedArtUsesProductionParser.
- [ ] Run the required Focused AfterlifeDocumentationCoverageTests and one conditional FullValidation after the combined B/runtime/docs checkpoint. Parent schedules one Fast for that combined checkpoint, not another duplicate for this small doc patch. No PreMerge until actual merge.
- [ ] Main inspects diffs/manifest JSON, actual lane summaries/logs/TRXs and independent review. Stage only scoped changes; record real evidence and unresolved later live admission/scheduler/pending work. Do not close top-level tasks or #1536 from this prerequisite.

## Dependency / non-claims

The implementation of spiritualWoundTarget/retraumaWoundRef and resolution.terminalExchange is still being pinned in B. Their exact authored shapes and examples must be added to this synchronization plan/companion before declaring B's complete GM contract synchronized. The current seven-file companion is executable for the canonical envelope only; it is not complete GM synchronization for those additional action fields.

## Actual partial verification — 2026-09-08

All artifacts below are under `TestResults/test-lanes/`. Parent owns these runs and read their actual output; the RED and narrow GREEN summaries/TRX rows were compared by exact test IDs. This is not full unit acceptance while the production-parser example and required combined controls remain pending.

| Control | Artifact | Result | Wall |
| --- | --- | --- | --- |
| New source guards RED | `20260908-172522-447-34528-e167543b8f384ca6a6020efc06a08ff5-focused` | expected0/2; exact missing source section/routing token | 1:07.4330887 |
| Same guards GREEN | `20260908-172730-313-29340-d09f1a7a831b41538f9805d4e4efa805-focused` | 2/2, exact same IDs | 11.2063407s |
| All afterlife documentation guards | `20260908-172957-597-34356-93914132276b49f7b7145bbc5b935ce3-focused` | 127/127 | 15.2640823s |

The initial build had zero warnings/errors. Both subsequent runs used `-NoBuild` against that freshly successful assembly because only docs/examples changed. All used the default5m limit, had no timeout/skips/duplicate IDs, and completed owned-tree cleanup. Sessions11752/96392/22448 are FINISHED; never poll them again. FullValidation, the typed example and parent combined Fast are deliberately not yet claimed.
