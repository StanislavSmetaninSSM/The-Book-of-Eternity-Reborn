# Approved GM Documentation Modularization Design — 2026-10-13

Status: USER-APPROVED STRUCTURE/ORDER; mapping and design checkpoint only. No file moves,
new gameplay schema, runtime/document delivery change or completed implementation is claimed.
This recovery record belongs to the tracked #1536 cloud handoff:
https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536.
It does not broaden #1536 into the implementation tracker for all GM documentation. The separate
GM feature's applicable GitHub issue and prepared-specification approval remain to be established.

User/coordinator evidence:
- Proposed structure/order: Sentinel_13f1026bc96881918c9a56578f512fca, 2026-10-13 14:09 UTC.
- User acceptance: Sentinel_956b26d890e8819197aee2df392a8657, 2026-10-13 14:10 UTC, «да».
- Approved scope: afterlife ten paired thematic rule/example modules + compact mandatory common
  core and situation router; only active-topic/dependency-union reading; exact repair routing;
  explicit daemon delivery, link/manifest/guard updates and automatic content preservation proof;
  Block_5 follows after the afterlife block. No semantic shortening or authoritative duplication.
- Coordinator explicitly requests a separate GitHub design/mapping checkpoint before further work.

Source/verification checkpoint: f32e14a080e9f1a3ba800dcaa8055e95d3cc6a6c; branch 1536-wound-b2-cloud-20261012.
ROOT reconfirmed all five GM source hashes below against the existing checkout after transport
recovery. Original inventory report payload is preserved verbatim after the marker:
19051 bytes; SHA256 41822128e8df1c691367456562df630e629832b53b02921a7f5c53e9dd34172f.
The historical report's “await approval” wording is superseded by the acceptance recorded above.
The approval applies to the shown structure/order, not to an unshown future spec.md/plan/tasks.

Safe continuation: finish FR063 existing-outcome POST/freeze/EVIDENCE; create an isolated GM
worktree/branch with a single writer, appropriate tracked feature and explicit feature-directory
selection. Preserve this mapping as input; prepare a concrete specification/checklist and obtain
its required user approval before dependent implementation. No main/merge/PR/issues mutation,
provider/model call, Windows run or full-suite operation is included in this checkpoint.

<!-- VERBATIM_READ_ONLY_MAPPING_PAYLOAD:START -->
# GM-документация: измерение и предложение тематического разбиения

2026-10-13. Основание: Sentinel_74e5a1f0c1a88191bb3340cc64334f27, 13:32 UTC — большие rules/examples нужно разделить по темам и дать GM понятное «когда, что читать». Это **предложение структуры для согласования**, не выполненный перенос.

Репозиторий: `/workspace/1553-storage-migration-cloud-20261008-recovery-61d3a7f`. Все пути/строки ниже относительны ему, измерены read-only по текущим файлам. Git identity не проверялась. Никаких project edits, build/discovery/tests/runtime, provider/model calls или новых агентов. Записан только этот внешний отчёт. Активные четыре пути sole writer FR063 не затронуты; immediate-wound-guidance alignment — отдельная работа.

## Реальный объём и первые кандидаты

Строки посчитаны по LF, байты — по исходным файлам без перекодирования; у перечисленных файлов есть конечный LF. В проверенных GM-каталогах самый длинный отдельный файл сейчас имеет **13 233 строки**, а не более 14 000; это всё равно крупный монолит. Важны также байты: одна строка матрицы может содержать большой набор требований.

| Текущий файл | Строки | Байты | Основные темы / точные anchors |
|---|---:|---:|---|
| `Examples/E_CLI_Afterlife_Turns.txt` | 13 233 | 696 885 | Source wound fragments :3/:49; workflow :177; examples 1–23 :428–7200; conflict 24 :7201; effect modules :7712; capstone 25 :9001; actor profiles 26 :9184; chronicles 26B :10061; Saref 27 :10647; global flags 28 :11455; math :11542; wound continuation/result closure :12100–13233. |
| `Rules/Block_5.txt` / `Examples/E_Block_5.txt` | 3 880 / 2 762 | 262 995 / 268 659 | Units 5.0 :11; characteristics :50; combat :322; derived stats :880; IDs :1121; rarity/level/item rules :1268; outcome/social rules :2254; wounds :3445; time/lore/currency/calendar :3591. |
| `Rules/Block_19.txt` / `Examples/E_Block_19.txt` | 2 612 / 2 329 | 178 596 / 184 476 | NPC data :124; memories :1474; relationship :1762; Fate Cards :1874; progression :2053; roles/needs/companions :2234; trade :2545. |
| `Rules/Block_21.txt` / `Examples/E_Block_21.txt` | 2 555 / 1 980 | 162 378 / 129 087 | Faction data :22; reputation :473; growth :837; bonuses :1285; territory :1411; atomic resources/state :1639; ranks :2006; power/state :2289. |
| `Rules/Block_10.txt` | 2 386 | 149 142 | Item acquisition :92; large item-property ruleset :544; vehicle structure :2340. |
| `OtherGuides/Afterlife_Contract_Matrix.md` | 937 | 253 233 | Universal rules :39; actor materialization :129; decision loop :217; scheduler :238; pending matrix :264; conflict :359; Shining actions :455; lifecycle :513; state reminders :542; coverage :599; faction authoring :628; wound closure/publication :646 onward. |
| `OtherGuides/Wound_Materialization_Contract.md` / `Examples/E_CLI_Wound_Materialization.txt` | 1 524 / 1 797 | 86 487 / 71 550 | Constructor, Mortal treatment, spiritual source/continuation, repair; worked examples. Keep outside first move while FR063 and visibility alignment are active. |

Ingress is also substantial: `CLI_API_Specification.md` **2 433 / 247 635**, `CLI_Agent_Daemon_Specification.md` **915 / 160 255**, `TaskGuides/CLI_Step_Main.txt` **791 / 188 300** (lines/bytes). Splitting the example alone will not fix unconditional read instructions in these entrypoints. No need to migrate all 61 Rules files or 80 text/Markdown Examples in one task.

## Рекомендуемая структура первого этапа

Use existing `CLI_Rules_Index.md` as the main situation router. Keep `OtherGuides/Afterlife_Contract_Matrix.md` and `Examples/E_CLI_Afterlife_Turns.txt` as short navigation/compatibility entrypoints with stable old section links mapped to exact new modules; **not** second full copies. Rule modules remain under `OtherGuides/`; example modules under `Examples/`. Prefer **flat filenames initially**: the current example extractor does not recurse into subdirectories.

Add a compact `OtherGuides/GM_Common_Core.md`: exact shared obligations on current request identity, realm/lifecycle gate, client-owned vs GM-authored authority, reading current pending/repair packet, write/output/terminal order, and ready/error/repair distinction. Extract complete existing obligations; do not summarize away exceptions or replace topic contracts. Intended short reading surface, not a line budget that permits deleting requirements. Every other module begins with `Read when`, `Prerequisites`, `Author here`, `Never author`, and links to its examples. Links/index prose are new navigation; original normative payload remains verbatim in its owning module.

Proposed paired names below: `OtherGuides/<stem>.md` for rules; `Examples/E_CLI_<stem>.txt` for examples. Existing example numbers and contract IDs remain unchanged. Matrix rows with multiple domains retain complete row text and explicit dependencies; the eventual move map must settle exact ownership before cutting them.

| Stem / when to read | Rule boundary in current matrix | Whole example boundary in current afterlife file |
|---|---|---|
| `Afterlife_Lifecycle` — bootstrap/incarnation/return/capstone | Realm mode :228; lifecycle/direct-action rows :513; related guard obligations from :39 | Examples **3, 6, 6A, 6B, 18, 20, 21, 25**, keeping each input→output→explanation intact. |
| `Afterlife_Scheduler` — due cycles/catch-up or ordinary living-world activity | Decision loop :217; scheduler/outcome sections :238/:254; scheduler-only restrictions | Examples **1, 16, 17, 19**. Combined scenarios stay whole and link their Guardian/Shining dependencies. |
| `Afterlife_Guardian_Contracts` — trade, consultation, offering, gacha, social commands | Corresponding pending rows :264; routing rows :336; social modes :449 | Examples **4, 5, 7, 8, 12, 15**. Each receipt remains beside its request and negative conditions. |
| `Afterlife_Abodes_Residents` — residents, transfers, foundation, search | Relevant pending rows :264; foundation :315; preview/routing :325/:336 | Examples **10, 10B, 11, 13, 22, 23**. Do not split founder/resident input and resolved output. |
| `Afterlife_Shining_Actions` — core actions, factions, trade institutions | Shining matrix :455; gates :496; forge :507; faction materialization :628 | Examples **2, 9, 14**. Keep numbered sub-actions and native-discovery fragments with full prerequisite context. |
| `Afterlife_Spiritual_Conflict` — exchange/resolve, costs/dice/special arts | Conflict :359; exact relevant terminology link | Example **24** up to its effects extensions (:7201–7711); math examples :11542–12099; `spiritual_wound_special_action_costs_v1` :12100–12185, linked to wounds. |
| `Afterlife_Effects_Resources` — effect application/conditions/resource receipt waves | Resource authority :41 and effect-specific obligations in :359; retain shared quartet/owner restrictions | Named modules :7712–9000 (`afterlife_effect_profile_v1`, AP, roll scope, bounded receipt waves, five conditions). Link existing `Effect_Materialization_Contract.md`, do not duplicate its full universal schema. |
| `Afterlife_Actor_Profiles` — materialize/update actor profile and capabilities | Actor materialization :129; exact actor-state reminders :542 | Example **26** :9184–10060, including its actor materialization bundle. |
| `Afterlife_Story_Memory` — chronicles, outline, threats, politics, Saref, global facts | Relevant canonical-state reminders :542; privacy/lore dependencies | **26B–26E, 27, 28**, including `COMMON INVALID PATTERNS` and all named fragments in their original parent context (:10061–11541). |
| `Afterlife_Wound_Continuation` — spiritual source/decision/dependent A/B | Named closure/publication/private/position sections :646–937, with their prerequisite references | Source fragments :3–176 and closure/private/common/continuation/pending/position/result modules :12186–13233. Retain exact A/B order; link cost module above. Do not alter accepted B2–B5 semantics or evidence. |

The introductory workflow :177–427 must be mapped completely into common core, afterlife routing and the appropriate lifecycle/scheduler owners; no orphan paragraphs. The old matrix coverage section :599 and all old numbered references become an explicit topic→rule→example coverage map. Topic ownership, not equal line chunks, determines cuts. This is a proposed partition, not a claim that every complex matrix row is already independently separable.

For `Block_5`, a separate later bounded move can use paired flat names `Rules/Block_5_<topic>.txt` / `Examples/E_Block_5_<topic>.txt` and keep original XML rule IDs: `Units_Ids` (5.0, 5.8), `Characteristics_Progression` (5.1, 5.7, 5.10, 5.24), `Combat_Foundations` (5.2–5.6, 5.16, 5.19), `Items_Resources` (5.9, 5.11, 5.13–5.15, 5.23), `Narrative_Social` (5.12, 5.17–5.18), `Wounds` (5.20), `World_Chronology` (5.21–5.22, 5.25). Move complete `<rule>`/example groups, preserving CDATA and enclosing applicability instructions. NPC/faction/item blocks follow only after this pattern is accepted; not part of the first bulk move. Note existing `Block_21` has two top-level `21.I` IDs (:812 and :2006): identify moved blocks by original ordinal + hash, not an assumed unique ID; renumbering would be a separate semantic cleanup.

## Как GM выбирает, что читать

| Situation | Required reading |
|---|---|
| Bootstrap | Existing context-pack manifest/README rule; core/router availability, no instruction to read every topic. |
| Every ordinary turn | Current request + compact common core and existing relevant compact templates; realm/lifecycle row chooses further modules. |
| `progressionControl` due/catch-up | Scheduler module + the particular actor/pending modules actually implicated; choose whole combined example only when applicable. |
| Exact pending request/action tag | Router entry names the owning rule module and exact example number/contract heading. Multiple active contracts produce a dependency union, not a choice to ignore one. |
| Spiritual conflict or wound continuation | Conflict or wound module, dependencies and the **issued** current frontier; do not load unrelated faction/gacha/lore examples. |
| Validation repair | Current repair packet + exact referenced rule/example module; existing packet scope and ownership restrictions stay authoritative. |

The module inventory may be copied in full to a session; that does **not** require reading all module bodies every turn. No new runtime selector engine is proposed: start with explicit navigation and existing conditional prompt/packet references.

## Реальные loader/entrypoint/validation зависимости

1. `BookOfEternityClient/Launcher/CLI_Launch_Script.md:19–24` and generator `Launcher/Generate_CLI_Launch_Script.ps1:76–81` name index/matrix/huge examples; generator also sets initial reading at :298. `TaskGuides/CLI_Step_Main.txt:26–27` still says MUST-read matrix **and examples** for any afterlife turn. By contrast `game_master_daemon.ps1:96` already prefers compact templates and opens the huge example only for uncovered routes. Reconcile these conflicting read directions; changing only a README is insufficient. Preserve all conditional constraints currently packed into that long daemon directive by placing them under explicit topic ownership.
2. `game_master_daemon.ps1:223–247` copies one named file; absence returns null. `Write-GmContextPack` at :1334 uses an explicit `docSpecs` list (:1339–1353); it does not follow Markdown links. :2644–2658 rebases named paths to session-local copies. Add every advertised module/core/router to this bounded list and manifest, and verify session-local link closure. Keeping old index paths alone would leave new targets missing from the delivered pack.
3. `BookOfEternityClient/BookOfEternityClient.csproj:29–32` already packages txt/md/json recursively from TaskGuides/Examples/OtherGuides/Rules. Packaging can include new modules, but it does not solve the daemon's explicit copying policy. No gameplay parser/JSON schema change is implied by a filename move.
4. Repair template refs are literal, e.g. `Core/GameEngine/GameEngine.ValidationAndRepair.cs:5281`, `:6413`; worker read scopes/reference prose are literal in `Services/GmWorkers/GmWorkerBridgeProfileTemplates.cs:152`, `:168`, `GmWorkerTaskPacketBuilder.cs:705`, and launcher/daemon bridge profiles. Update only affected documentation refs/read permissions; preserve command, packet and publication authority. `GmWorkerContractValidator.cs:1545` also recognizes matrix-name text: include it in exact-reference review.
5. `BookOfEternityClient.TestSupport/ExampleDocumentationFixtures.cs:17` enumerates **TopDirectoryOnly**. New flat Examples filenames are discovered; subfolders would silently leave examples out without an extractor change. `NormalizeFile` :118 uses relative filenames; runtime and companion matches at :253/:281 require exact `File` plus `RequiredText`. `Examples/example_validation_manifest.json` has path-sensitive coverage, runtimeScenarios, syntax/shape exemptions and companion references; do not split this machine manifest in the first block.
6. `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs:4453` parses an exact filename + named heading and takes JSON fences in order until the next same/higher heading. Preserve heading levels, all ordered fences, explanatory context and negative labels. Extractor :75–110 infers expected validity from nearby lines/XML tags: moving JSON alone can turn an intentional INVALID example into expected valid.
7. `AfterlifeDocumentationCoverageTests.cs:3614` pins five entrypoints and legacy filenames; :3846 pins the monolithic file and base examples **1–28** plus runtime/exemption coverage. Adapt these to verify the explicit split inventory and unchanged complete coverage, not merely search concatenated indexes for words. Other guards inspect exact section text (e.g. :112) and named paths. `ResourceContractSourceGuardTests.cs:625–641` recognizes exact `Block_5.txt`/`E_Block_5.txt`: moving content without extending its path set weakens coverage. `PromptDocumentationCoverageTests.Wounds.cs:237/:278` and `EffectMaterializationSourceGuardTests.cs:134` also read old files directly.
8. Existing context-pack runtime test `GmTurnHelperContractTests.cs:3010` starts `powershell.exe`. It was **not run**, and cannot be cited as Linux delivery proof. Future focused validation must genuinely cover new advertised files/links in the supported environment; do not require a paid GM/model run for a content move.

## Проверка сохранности и ограничение объёма будущей работы

- Before moving: freeze source bytes and inventory every normative block, XML rule/example, numbered scenario, named contract section, ordered JSON fence and surrounding applicability/INVALID text. Each receives original file/range/ordinal/hash and one destination. Nonsemantic new headings/wrappers/navigation are listed separately. Reassemble extracted payload in original order and compare bytes; do not rely on an LLM saying the meaning is unchanged.
- After moving: one-to-one content inventory, no omissions/duplicate authoritative copies; rule IDs, example numbers (including A/B/C suffixes), contract markers, valid/invalid classification and JSON fence order preserved. Resolve all local links, old anchors and old-path references. If a retained numeric reference spans modules, link the precise module + example, never require guessing a new number.
- Manifest proof: same scenario/contract ID multisets, same runner, prestate/companion payloads, expected writes/absences/unchanged files, validationKind and honest coverageLimit. Only file/line mappings change. Existing focused-fragment exemptions stay focused; do not turn them into invented runtime coverage or delete failing guards to make a move green.
- Delivery proof: every module reachable from the advertised router exists in packaged assets **and** copied session context pack; copied bytes match source; repair/worker refs remain readable within their actual scope. Bootstrap/per-turn prompts select required topics and do not expand to all modules. No need for a new broad replay/prompt framework.
- Future bounded checks: content/hash inventory and link closure first; then actual affected example extraction/manifest/source guards and context-pack delivery checks. Choose the finite catalog owners after the approved diff is known; no entire suite/all-category chain, gameplay reruns or provider call for this proposal. No checks were executed here.

## Что нужно согласовать, и что остаётся отдельно

Recommended first scope: **afterlife example + matrix split and all required navigation/delivery/guard updates**, with the exact source-content move map reviewed before mutation. Approve flat thematic filenames and old entrypoints as short routers. Then move Block_5 in a separate bounded block; NPC/faction/item splitting is a later application of the pattern. The first scope is documentation modularization with necessary path consumers, not a gameplay change.

The user has requested organization; this report does not claim approval of these exact filenames/boundaries. No mass move before structure approval. Do not combine the move with FR063 fixes, initial wound-visibility policy alignment, removal of diagnosis codecs, new mechanics, historic evidence rewrites or performance claims. Any newly discovered contradictory rule remains an explicit finding; do not silently “simplify” it while relocating.

Source pins for this read-only inventory (SHA-256):

```text
Examples/E_CLI_Afterlife_Turns.txt bef41fb951303d57fa7a9ae1f6cd7ac49ac53653d557ab4f8cddb849814a56a3
Rules/Block_5.txt 895eebaba31c6d2527354c682067588142b00245d2b750f585923a6c63718d62
Examples/E_Block_5.txt c244c3051f5e04bb39c973a08cb270a43500f1ba16c29cc451f134ca53a48d04
OtherGuides/Afterlife_Contract_Matrix.md e69198e6db8fb00f060184ce48ec9c9aac98b2a5b2301576570bd625092521a5
Examples/example_validation_manifest.json ca049b16c368726ff7465f6286e3df416b0d9d302008a3faaf219faa934ce3bb
```

**FINAL STOPPED:** read-only inventory/proposal complete; no repository mutation or project execution. Only this requested external report was authored.

<!-- VERBATIM_READ_ONLY_MAPPING_PAYLOAD:END -->
