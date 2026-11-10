# Research Decisions — External Draft

**Source Issue:** [#1557](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1557). **Date:** 2026-10-13. This reuses the existing approved inventory and preflight. Only targeted source/skill reads and local byte hashing were performed; no project scripts, Git, discovery, tests, providers or agents. New planning artifacts are not approved.

## R1 — Artifact workflow without active-checkout side effects

Decision: prepare the seven external artifacts now; future implementation uses `specs/1557-gm-doc-modularization/` in the agreed isolated GM worktree. Source spec semantics are approved; its archived DRAFT/TBD status is historical, not a new approval requirement. ROOT supplies the actual #1557 creation/approval provenance.

Rationale: local `speckit-plan` requires plan/research/data-model/contracts/quickstart and agent context; `speckit-tasks` requires story-organized checklist tasks; analyze is read-only after tasks. Current explicit instruction forbids setup-plan/setup-tasks, before_specify hooks, feature context or AGENTS mutation in active #1553 checkout. `.specify/extensions.yml` has optional plan/tasks/analyze commit hooks and a mandatory before_specify branch hook; none are invoked. Skill instruction to dispatch research agents is overridden by explicit no-new-agents. `analysis.md` records manual read-only findings from completed drafts, not a claim that the repository CLI/hook workflow ran.

Alternative rejected: running setup scripts now with implicit `.specify/feature.json` could target #1553 and disturb active FR063 pins. Existing structure/spec approvals are reused; no repeated concept question is needed.

## R2 — Flat content files and preservation, not rewritten summaries

Decision: keep all approved files flat, preserve whole contextual units and verify exact reverse-reassembly. Rule/example/module IDs are documentation records only.

Evidence: `ExampleDocumentationFixtures.cs:17–23` enumerates TopDirectoryOnly. Markdown valid/invalid inference reads 12 preceding lines (:75–81); XML inference checks enclosing bad example/context (:83–109). Named-fence parser in integration `ExampleDocumentationValidationTests.cs:4453+` stops at next same/higher heading. Moving bare JSON loses these semantics.

Alternatives rejected: subfolders with silent under-discovery; semantic summaries replacing requirements; duplicate whole matrix/example copies; normalizing all whitespace before comparison. Stable numbers/suffixes and payload equality are independently checked so the verifier cannot pass against unused copied bodies.

## R3 — Precise ownership correction

Decision: MATH ASSISTANT at `E_CLI_Afterlife_Turns.txt:11542–11581` belongs to Spiritual_Conflict. `## Shining Faction Materialization worked examples` at 11582–12099 belongs to Shining_Actions, including native_discovery/player_founding/story_hidden/repair. This corrects an overly broad range in the older planning map and changes none of the approved ten themes or gameplay.

Whole numbered/suffixed scenarios and multi-topic rows retain one owner plus explicit dependencies. Detailed byte ranges will be bound to the final frozen base before cutting; planning line positions alone are insufficient. Intro 177–427, Universal Rules, state reminder lists, table headers and separators are explicitly inventoried rather than discarded as boilerplate.

## R4 — Delivery requires actual fixed-list copying

Decision: extend existing daemon docSpecs and session rebasing for the scoped files; verify actual copied bytes and link closure independently from packaging.

Evidence: `game_master_daemon.ps1:1334–1355` names 14 source entries; copied Markdown links are not traversed. `Copy-GmContextPackFile` at 223+ can return null when absent. The session directive at 2658+ says session-local docs are authoritative; bootstrap at 2657 remains manifest/README-only. Project file :29–32 already recursively packages flat assets. A file in the build output is not proof of daemon delivery.

Existing `GmTurnHelperContractTests.DaemonContextPack_RuntimeGeneratesEveryAdvertisedBootstrapArtifact` (:3010+) launches `powershell.exe`, so it is not Linux proof. Future bounded delivery fixture must execute the real context-pack writer under existing supported Linux PowerShell/session publication prerequisites, with no turn_request/model/bridge dispatch, an owned isolated root, exact copy/manifest/link assertions and strict process/root cleanup. Before choosing whether to adapt that exact fixture or reuse the existing portable helper harness, inspect its current supported process seam; no standalone copy algorithm may replace the production writer under test. This is a fixture-design task, not permission to run a daemon now.

## R5 — Finite selection follows changed consumers

Decision: test content/reassembly/link/manifest and real delivery with small finite owners; preserve affected existing scenario/guard semantics. Do not choose broad owners by class name.

Observed owners: `documentation-afterlife-entrypoints-coverage` holds exactly two existing entrypoint/coverage Facts; `spiritual-binding-authority-documentation` holds one actual documentation Fact and one worked-example Fact. Other relevant controls are embedded in broader categories. `gm-daemon-prompt-delivery` is retry/dispatch behavior, not context-pack inventory; no automatic reason to run it. Actual planned/discovered/completed case counts will be fixed after the source diff and method list exist, not guessed from this draft.

## D01 — Existing client-code fallback contradiction (unresolved scoped finding)

**Exact sources and contexts:**

- `OtherGuides/Afterlife_Contract_Matrix.md:37`, afterlife guide ingress: “If the matrix and examples still leave a mechanical contract unclear, or a validation repair points at an exact schema/surface mismatch, the GM may inspect client code as a fallback source of truth for file mappings, allowed fields, validators, canonical state surfaces, receipts, reports, and normalizers.” Same paragraph says fallback never replaces prompts or invents outcomes/bypasses rules.
- `BookOfEternityClient/game_master_daemon.ps1:2667`, `$script:GmSourceFallbackDirective`: “Do not read implementation code such as BookOfEternityClient/**/*.cs during normal play or validation repair; use safe GM probes, validation_repair_request.json.harnessRepairPackets, session state/control files, helper commands, and named copied GM docs instead.”
- `AGENTS.md`, “GM prompt, documentation, and example synchronization guardrail”: “The GM does not read client implementation code during normal play.”
- `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs:3649+`, `AfterlifeDocsExposeClientCodeFallbackWithoutReplacingPrompts`, requires fallback/client-code/canonical text in matrix, TaskGuide, daemon specification; additionally pins FileMapping.cs/Validation/ and forbids the unconditional old sentence “The GM does not need to read client code.” This is an affected real guard, not an inferred future test.

**Scenario/impact:** ordinary or validation-repair GM sees contradictory read authority depending on ingress. Copying the fallback sentence into unconditional common core, or silently dropping it as “navigation cleanup”, would choose policy outside the approved structural move. FR009 requires an explicit scoped disposition.

**Concrete minimal resolution proposed to coordinator (NOT ACCEPTED):** align normal-play and ordinary-repair directions with current AGENTS/daemon: use current request/packet, safe probes, helper commands and copied topic docs; if insufficient, report an unresolved harness/documentation gap instead of treating implementation source as normal GM workflow. Explicitly authorized developer investigation is outside normal GM play and must not be presented as an automatic GM permission. Replace only the exact conflicting ingress sentences, amend the named guard to assert that boundary, and record original/new bytes and coordinator decision in navigation-edits. Preserve all mechanical/canonical restrictions and unrelated examples verbatim.

**Gate:** only affected common-core/read-direction edit tasks T009/T010 depend on this ruling; inventory, content verifier, module ownership, manifest/delivery design/tests and other path work continue. Do not ask the user to reapprove the modularization concept. Until disposition, preserve both before-images as a finding and do not merge contradictory sentences into a single asserted policy. This draft does not create a new source-reading entitlement or claim the coordinator accepted the proposal.

## R6 — Concurrent-source reconciliation

Planning hashes below are actual local bytes, not a verified Git tree/remote claim. ROOT’s current carrier labels are contextual only; final full base SHA and source pins must be reread after the agreed FR063 boundary and before isolated #1557 implementation. #1536 immediate recommendation work may later change the same guide/example/daemon paths. Reconcile the latest accepted delta before moving, and regenerate only affected maps/expected docs bytes. Historical #1536 evidence stays immutable.

## Actual planning source pins

The following table was generated by plain file reads/SHA256 in this consultation. It does not mean any project was built or test executed. Any pin drift before content moves is a reconciliation dependency.

| Path | Bytes | SHA256 |
| --- | ---: | --- |
| `AGENTS.md` | 11662 | `4410cc7fb11213629792b280c9a2ba3bdb1d74316dc7950072ce7c158a9c5e23` |
| `docs/development-workflow.md` | 66432 | `8cc1cb8f4673bf910b0cb3d9c0757b9fdc6facf06e6a2552a6b462c90b270e71` |
| `.specify/memory/constitution.md` | 14081 | `f3e8f4de6a6ac7c82f68ea6f6b37d9dcb95901f59f32b1a7f6353217bc490944` |
| `.agents/skills/speckit-plan/SKILL.md` | 7411 | `b92428f869e93676fc0b77232dd4fecb610294b68bee677d940a239c43766a3d` |
| `.agents/skills/speckit-tasks/SKILL.md` | 10463 | `460eddac0fd7521bed8ac2d5821c98fc0dd4ab3ced4a6d94eb04bc155b021ac9` |
| `.agents/skills/speckit-analyze/SKILL.md` | 10866 | `2740b8aeab8d0104527384ca74106e252e0a20df89355c8f36be25a62130d6d6` |
| `.specify/templates/plan-template.md` | 5176 | `9e30f2f4f60da82326844fe2d0799038f66dbf34ace95f6d69c39672c9706e68` |
| `.specify/templates/tasks-template.md` | 10542 | `fc061a22f6b52a46e3e12e5d006110e5cfda935824b4eb69b1dac9fbc158435e` |
| `.specify/extensions.yml` | 4672 | `5e1ef86bc39cb43001577e895da68b324154a52281565e30ddba62c235c61df1` |
| `specs/1536-complete-wound-materialization/recovery/gm-doc-modularization-spec-draft-20261013/spec.md` | 18686 | `27735ec49aeb39eec51e1764ebff15ffe2e9c2c3cc150138659564587116bace` |
| `specs/1536-complete-wound-materialization/recovery/gm-documentation-modularization-approved-design-20261013.md` | 21489 | `c2d8b7eac8a60cdde1163893904425b132a387dd0c07d54ff12ca4eca52d5ea0` |
| `OtherGuides/Afterlife_Contract_Matrix.md` | 253233 | `e69198e6db8fb00f060184ce48ec9c9aac98b2a5b2301576570bd625092521a5` |
| `Examples/E_CLI_Afterlife_Turns.txt` | 696885 | `bef41fb951303d57fa7a9ae1f6cd7ac49ac53653d557ab4f8cddb849814a56a3` |
| `Rules/Block_5.txt` | 262995 | `895eebaba31c6d2527354c682067588142b00245d2b750f585923a6c63718d62` |
| `Examples/E_Block_5.txt` | 268659 | `c244c3051f5e04bb39c973a08cb270a43500f1ba16c29cc451f134ca53a48d04` |
| `Examples/example_validation_manifest.json` | 182495 | `ca049b16c368726ff7465f6286e3df416b0d9d302008a3faaf219faa934ce3bb` |
| `CLI_Rules_Index.md` | 25781 | `c33838a3a8a0d1dd5ff3305698d6a9ec861c2e3513775f2d0e7b39881f196f29` |
| `CLI_API_Specification.md` | 247635 | `8d08213f077e3b1ab63d008973ad0d47fd9bde947ee73152f85129082a960861` |
| `CLI_Agent_Daemon_Specification.md` | 160255 | `58f19cf556acafec5afe6214ce613f29c64affd085d815620b0252f710ddff5b` |
| `TaskGuides/CLI_Step_Main.txt` | 188300 | `fab4bd38391965d119438b91c1fc5cb4648828187275a942d7af5c6b421e6fe9` |
| `BookOfEternityClient/Launcher/CLI_Launch_Script.md` | 30303 | `e6c2896edf4b227db6742d7d91e26eda3ec1c47fdb3a145b1faa9a8b9d448d39` |
| `BookOfEternityClient/Launcher/Generate_CLI_Launch_Script.ps1` | 31947 | `592e6c607df18e5f0935bf4574ba3a861edb39e70c612f7eaf12653d5ddb555d` |
| `BookOfEternityClient/game_master_daemon.ps1` | 398600 | `f1f9fd80e0e26570990a35ad6009429025d4620e31404ae0aad554ca4c9b445d` |
| `BookOfEternityClient/BookOfEternityClient.csproj` | 5795 | `b1bafa2fffd26263bc821cd668f20d199a2d08a1a6ed1a4f32c69b95a893d59e` |
| `BookOfEternityClient.TestSupport/ExampleDocumentationFixtures.cs` | 12045 | `73eb463d5bf9f209755d431a282eaca37254abb4bcf69c66c4c5855f429eef4d` |
| `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs` | 289155 | `a82fddd96ea243347391022ea5231cdd0b862801ababa70a95e8e66ac1cd06ee` |
| `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs` | 213635 | `54369d5978c67a995f47017e7549f2096494ceeb5a5411bae2058af261e188cf` |
| `BookOfEternityClient.IntegrationTests/GmTurnHelperContractTests.cs` | 339647 | `75527de090426482035f5c4e73a55f3efbcc0eb0ae1950579dab8fac598d7a6e` |
| `BookOfEternityClient.Tests/ResourceContractSourceGuardTests.cs` | 29484 | `9ec838fca12dc682d119bf571fef3985ac1f684e5f9caba7690eb2c07308a52a` |
| `BookOfEternityClient.Tests/EffectMaterializationSourceGuardTests.cs` | 21572 | `13342146bf265bb51e4f4824ea2ff00f9936511943ba280cf15962ec98473112` |
| `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs` | 45253 | `b382a0301c4f56799e2191ffa57739e49c7941734bd96b132923c939fba3bcc9` |
| `tests/categories.json` | 1868067 | `e5473a5c807d166c3bc2d0c42284bbef82a5a33cbb76116f99ee197c2d719cc3` |
| `docs/testing.md` | 18979 | `8c6073ea6bb27be408e4cce3ae577538a85087b1ffa4fba89753c30db9542cd7` |
