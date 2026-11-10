# #1557 — remaining SOURCE conditions: D01, prefix bytes, reading guards, cleanup

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1557. External read-only supplement, 2026-10-13 UTC. Repository inspected: `/workspace/1553-storage-migration-cloud-20261008-recovery-61d3a7f`. No repository writes, Git, PowerShell/project execution, builds, discovery, tests, providers or additional agents.

The coordinator's accepted follow-up is `specs/1536-complete-wound-materialization/recovery/gm-doc-modularization-planning-draft-20261013/coordinator-supplement-followup-review-20261013.md` (parent-reported carrier `ed0d1880e5f732c71a2a4929135391eb4565ae81`). Three planning conditions are closed; D01 follows the existing ban. Block A remains conditional on verified FR063 freeze/final base; block B follows independent A acceptance. This fills remaining concrete SOURCE details, without reopening themes/routing or introducing a runtime framework. **All AST/execution/delivery/cleanup qualification is future and UNRUN.** Current source hashes are read-time observations, not final source/package/DLL pins.

## 1. Explicit D01 before/after

Current full before-image, `CLI_Agent_Daemon_Specification.md:71–81`:

```markdown
## Client code fallback, если контракт заблокировал ход

Промпты, правила, `OtherGuides/Afterlife_Contract_Matrix.md` и `Examples/E_CLI_Afterlife_Turns.txt` являются основным рабочим интерфейсом ГМа. Код клиента НЕ нужно читать для обычного хода и нельзя использовать вместо realm gate, матрицы, examples или запретов правил.

Если после обязательных документов контракт всё ещё механически непонятен, либо repair request указывает на schema/surface mismatch, ГМ может открыть код клиента как fallback source of truth только для технической формы контракта:
- `BookOfEternityClient/Configuration/FileMapping.cs` — какие response fields пишут какие файлы.
- `BookOfEternityClient/Models/GameResponse.cs` и связанные модели — какие поля существуют в ответе.
- `BookOfEternityClient/Services/Validation/` — какие поля, receipts, reports и state roots валидируются.
- relevant services/normalizers under `BookOfEternityClient/Services/` — какие canonical surfaces и side effects ожидает runtime.

Такой fallback разрешает уточнить имена файлов, JSON-поля, allowed values, receipt/report shape, canonical state surfaces и порядок нормализации. Он НЕ разрешает придумывать новые gameplay-системы, закрывать afterlife смысл через Mortal World channels или обходить промпты. Если fallback использовался, кратко укажи в `gm_thoughts_markdown`, какой контракт был уточнён и какое canonical решение выбрано.
```

Replace that entire section with the following **literal complete after-image**:

```markdown
## Если GM-контракт остаётся неясным

Промпты, правила, `OtherGuides/Afterlife_Contract_Matrix.md` и `Examples/E_CLI_Afterlife_Turns.txt` являются рабочим интерфейсом ГМа. Матрица и examples направляют к применимым тематическим документам и примерам; открывай их по текущей задаче. Во время обычной игры и validation repair запрещено читать implementation code клиента, в том числе `BookOfEternityClient/**/*.cs`, чтобы выяснять правила или техническую форму контракта.

Если после обязательных документов контракт остаётся механически непонятным либо repair request указывает на schema/surface mismatch, используй только предусмотренные GM-facing источники:
- текущий запрос клиента и названные в нём скопированные GM-документы, применимые строки матрицы и worked examples;
- `validation_repair_request.json.harnessRepairPackets` для текущего repair, safe GM probes и helper commands в пределах их разрешённого scope;
- разрешённые для текущего realm и pending-контракта session state/control files.

Если точная форма контракта отсутствует в этих источниках или недоступна, зафиксируй недостающую documentation/harness surface в `gm_thoughts_markdown` в рамках текущего протокола. Не восстанавливай её по implementation code и не придумывай gameplay outcomes, JSON-поля, allowed values, receipt/report shape или canonical state surfaces.

Эти источники не отменяют realm gate, pending-контракты, canonical-write restrictions и запреты правил. Нельзя закрывать afterlife-смысл через Mortal World channels или обходить промпты. Соблюдай текущий terminal/repair protocol; этот раздел не разрешает новый ход или дополнительные записи вне его scope.
```

Preserve the following blank line, `---` separator and `## Что валидирует клиент` section. Current raw before-image interval `[8273,10284)` is 2,011 bytes, SHA-256 `f8a12fe62f7e65204d618d18df36f9ba46d33a67daf53457cc29c94931ab517e`: includes one last LF, excludes the following blank-line/separator. After-image bytes are the second fence body plus exactly one LF, UTF-8 without BOM. Store both exact payloads/intervals in the reversible D01 edit record; re-extract and compare the final-base before-image before applying.

Authority stays `AGENTS.md:117–122`, unchanged `TaskGuides/CLI_Step_Main.txt:23`, daemon `:2662–2663`, ordinary/repair references `:5892/:6314`. This retains the prior audit destination while removing permission to read implementation. “В рамках текущего протокола” grants no extra receipt-only output. Stable matrix/example router paths remain; mechanical link changes are a separate edit class. No live-GM exception, invented terminal action or gameplay rule.

Apply the already accepted matching changes to matrix `:37` and TaskGuide `:432`. Replace the existing `AfterlifeDocsExposeClientCodeFallbackWithoutReplacingPrompts` guard (`BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs:3648–3671`) with assertions for actual active bodies: normal/repair prohibition, current request/packet/probe/helper/document alternatives, bounded gap audit, realm/canonical/no-invention restrictions. Remove the specific old grants and assertions requiring `FileMapping.cs`, `Validation/`, “normally does not need…” and permitted code fallback. Do not forbid every unrelated “fallback” occurrence or count navigation stubs as body coverage. Reverse-reassembly verifies accounting of old bytes; the semantic guard verifies the approved correction.

## 2. Exact prefix and future AST acceptance

Read-time daemon: UTF-8 BOM, **398,600 bytes**, SHA-256 `f1f9fd80e0e26570990a35ad6009429025d4620e31404ae0aad554ca4c9b445d`; 6,849 LF bytes, zero CRLF pairs. Current literal boundary occurs once, at `2667–2670`:

```powershell
Invoke-GmParticipatingConsumer $GameSessionPath {
    Write-GmTurnHelperBootstrap
    Write-GmContextPack
}
```

On decoded text with only the BOM removed: UTF-16 offsets `[195155,195262)`. Original file byte interval `[198005,198112)`, SHA-256 `54235092f2279bcd4b5046e4a8ee8e859e91b62cf7d202e1b9768de3f2046739`. Proposed exact sibling-script prefix: raw bytes **`[0,198112)`**, including BOM, SHA-256 **`ffbd62dbd8cf7b391ff5d038c0bd3c65ec71aac6516c562e352f673f81f23919`**. Last byte is `}`; add no final newline. The following LF and `$script:GmCompactTemplateDirective += $script:FactionMaterializationDirective` (`2671`) are excluded.

This is **Python byte/text localization only**: no PowerShell AST was parsed/executed. The feature's docSpecs/README/directive edits will change offsets/hash, so these values must not become fabricated final pins.

Future adapter checks, all required at the concrete SOURCE gate:

1. Strictly decode the actual packaged daemon's bytes, removing only detected UTF-8 BOM. `Parser.ParseInput(exactText, packagedSourcePath, …)` must return no errors. Search **only root `EndBlock.Statements`**, not recursive `FindAll` or first substring. Require exactly one matching direct pipeline/ordinary command `Invoke-GmParticipatingConsumer $GameSessionPath <scriptblock>`, no redirections/alternate invocation/additional arguments. Its block has exactly two direct no-argument commands in the order shown. The different earlier top-level participating initializer (`156–160`) must not match.
2. Require the preceding top-level definition to be complete `Write-GmContextPack` (closes at `2665`), and the next statement to be the actual compact-directive append (`2671`). Extract through selected `Extent.EndOffset` only. A changed legitimate neighbor needs exact review, never blind boundary extension.
3. AST offsets are UTF-16, not UTF-8 offsets. Compute `bomLength + UTF8.GetByteCount(text.AsSpan(0, EndOffset))`, slice **original bytes**, and verify decoding equals exactly `text[..EndOffset]`. Parse the extracted prefix too: no errors/incomplete nodes; final statement must be the selected one. Do not use `Set-Content`/newline normalization or UTF-8 byte indexing by AST character offset.
4. Write one unique test-owned sibling `.ps1` inside the isolated actual package copy. Retain original parameter block, initializers/functions and both real participating scopes. A small pinned test driver may parse/create the sibling then invoke it with `& <exact-sibling> -GameSessionPath <owned-game_session>` in the same owned pwsh. `$PSScriptRoot` remains the package directory. No function substitution, mock storage, seeded context global, appended directive or checkout helper. The driver is a fixture artifact, no production endpoint.
5. At SOURCE review inspect the entire executable top-level prefix and called writer/helper chain, not just the cut. Windows initialization remains platform-guarded (`70`); the Linux external command is the real packaged `dotnet <package/BookOfEternityClient.dll> --gm-main-operation --root <owned-root>` (`Launcher/gm_main_operation.ps1:20–27`). No provider/bridge dispatch call is reachable in this selected chain. Function definitions/generated here-strings are not calls. Later dispatch functions/watchers/main loop stay excluded; do not replace this with a full-daemon run stopped when files appear.
6. Require actual `operational-resources/CLI_Agent_Daemon_Specification.md`, sibling DLL/deps/runtimeconfig and required Launcher helpers, all resolved inside the package. Fail before launch instead of taking parent-checkout or Debug/Release fallback. Use a fresh owned game_session, normal initializer, no request/active run/worker inventory, and real participating generation/publication. Never hand-seed generation/lease/snapshot or replace publication with copying documents.

Actual close/finalization remains `gm_main_operation.ps1:98–160`; helper entry remains `Program.cs:26` → `Services/GmRuntime/GmMainParticipatingControl.cs:28/:77/:154`. A partial manifest does not establish success: child success, scope return, expected inventory/full bytes/links and confirmed cleanup are all required. The previous supplement's independent literal inventory, source→package→session equality and missing-copy/broken-link negatives remain unchanged.

If the final boundary/dependency chain fails these conditions, stop that fixture and bring its concrete mismatch to the coordinator. No new prepare-only production switch, storage relaxation or runtime expansion is authorized.

## 3. Final reading/dispatch guards remain separate

The prefix includes `Write-GmContextPack`'s rebasing/assignments (`2644–2664`), **not** the following appends (`2671–2681`) or final dispatch. Real file delivery cannot prove final prompt composition. Use bounded source/AST guards against the same full packaged source plus delivered document bodies; reuse the approved seven-family/ten-topic truth table rather than inventing a selector framework.

| Actual seam | Required final guard |
|---|---|
| Directive assignments `2657–2664` plus every later assignment/append/reference to context/doc/source/afterlife/router directions | Ban and scoped reading survive all later writes. Enumerate real writes, not first-match `Contains`. Keep the existing variable name `GmSourceFallbackDirective` if convenient. No restored permission or unconditional read-all-modules direction. |
| `Process-TurnCore` default message `5892` → dispatch `5904` | Actual message includes source/safe/context/doc directions and conditional router, all late additions accounted for before **that same message** is passed. Update active old read-whole-example directions, not just README. Preserve gameplay directives and dispatch correlation. |
| `Build-FirstMortalBootstrapDispatchMessage:1081–1090`; alternative branch `5881–5888` | This bypasses default message creation; preserve its existing source-ban reference and narrow actual first-Mortal contract. Incarnation bootstrap is distinct from harness bootstrap; no forced afterlife modules. |
| Repair message `6314`, later branches `6315+` → dispatch `6361` | Ban and applicable packet/doc reading reach final message; current required-scope/full-resubmission vs in-place rules preserved. No later permissive source override. |
| Terminal failure message `6497` → dispatch `6516` | Preserve source-ban reference, scoped reading, and distinction from validation repair/new ordinary turn; no extra terminal action from D01. |
| `Build-QteEffectResolutionDispatchMessage:5689–5707`, exact requestKind/safePacket `5736–5741` → dispatch `5769` | Specialized builder remains used. Exact safe packet/helper-only receipts/Ready; no Core/router/topic bodies, ordinary request, story/progression or generic completion injection. |
| Generated README `2592+`, manifest/rules `2613–2642`, context directive `2657`, fixed inventory `1334+` | Harness bootstrap reads only manifest+README; copied availability never implies read-all. Every advertised link for applicable later routes resolves in delivered closure. |

Guard negatives: remove the final repair ban reference; add a later permissive override; replace conditional router with read-all; inject ordinary/Core reading into receipt-only; remove one expected copied module; break a destination/anchor. Each must reach its intended guard independently of a generic file-hash mismatch. These are proposed finite inputs, **not discovered cases or claimed PASS/RED**.

Existing `tests/fixtures/GmPromptDelivery/daemon-functions.ps1:8–33` substitutes participating/storage functions and later the QTE message builder. Its inert transport-identity tests do not establish real package publication or full final-reading coverage. Do not run its broad category merely for the name or introduce provider dispatch to prove source directions. These remain source/dataflow guards; startup evidence must not claim full dispatch execution.

## 4. Common timeout, concurrent pipes, owned cleanup

Keep this local to proposed `GmDocumentationDeliveryTests`/its adapter; no production helper/supervisor/framework changes. Existing examples need care: `IntegrationTestBoundaryTests.cs:2648–2660` starts concurrent drains but waits unbounded after kill; `BrowserInputRecovery.cs:209–244` has separate windows; production `gm_main_operation.ps1:110–120` can block in final waits/joins. Bound the actual child from outside instead of changing its semantics.

**Concrete proposed local bound:** one monotonic **60s absolute deadline**, with work done by **45s** and the last **15s** reserved for cleanup. It includes adapter parsing/extraction, writer, content readback, pipes and cleanup; ordinary package build is separate official preparation. Every await uses remaining absolute time; no reset per command/exception/exit/stream. This is a fixture proposal for SOURCE review, **not** an observed duration, approved catalog change, raised existing budget or retry allowance.

- Start one owned pwsh with argument-list entries, package working directory, no shell, both pipes redirected. Start both asynchronous pumps immediately, continue concurrently during execution, retain separate partial logs. Never exit-wait then sequentially drain. Overflow/read failure is explicit failure, not silently truncated success.
- Retain pwsh PID + OS birth identity. Real helpers have the exact packaged DLL path plus `--gm-main-operation --root <unique-owned-root>`; two sequential scopes may create different helper PIDs. A small fixture-local Linux `/proc` census retains observed PID/birth ticks/parent chain/exact argv while running and at exit/error. It also locates a surviving orphan by that **complete unique package/root signature**, not just current parent PID. Revalidate identity before killing, never signal a reused PID. Unique package/root are not shared between cases.
- SOURCE proves this bounded helper chain spawns no providers/arbitrary child machinery. Unexpected descendant or unreadable ownership is a failed qualification; no blanket process-name matching, `pkill`, existing-session kill, production Unix process-group abstraction or new supervisor dependency. Use retained Process handles and ordinary tree-kill only for positively owned processes.
- Normal success requires child exit zero, both EOFs within work deadline, no adapter/helper error, exact manifest/inventory/byte/link assertions and zero surviving owned identities. A manifest or parent exit alone is insufficient.
- In `finally` on **all** paths, kill a live owned pwsh tree, then any surviving positively identified helpers/descendants; rescan exact signature until gone or deadline. All exit waits and pipe joins share the remaining common deadline; no unbounded `WaitForExitAsync()`/`GetResult()` after kill. If EOF remains unobserved, cancel/dispose owned readers, retain partial logs and mark failure. Preserve original failure plus cleanup failure; disposing a Process object is not proof of OS exit.
- Delete only owned package/session roots after confirmed process exit, then assert absence. Keep artifacts outside deleted roots. Cleanup receipt records roots, PID/birth identities, exact commands, elapsed work/cleanup, exit/kill outcomes, EOF flags, remaining-process census, deletion and original exception. If ownership/termination cannot be confirmed, keep the root and failed receipt; an attempted kill must never become a claimed successful cleanup.

This makes cleanup an **acceptance invariant**, not an impossible promise to terminate an uninterruptible OS process. Future finite fixture coverage must exercise normal production-prefix exit and a controlled cancellation/timeout with an owned helper surviving its immediate launcher, proving orphan detection/concurrent drain. A controlled process-lifetime negative tests only the adapter and is labeled separately; it cannot stand in for real writer publication or modify the byte-exact prefix. No case counts/results are asserted now.

## 5. Final rebind and remaining gate

After verified FR063 freeze, reconcile exact final source SHA/overlapping docs in isolated #1557 work, retain before-images and this accepted-disposition trail. Rebind packaged daemon, AST boundary/prefix, fixture driver, launcher helpers, DLL/deps/runtimeconfig and actual runtime dependencies to that final source/build. There are deliberately **no binary pins here**: no package/build was qualified.

The next concrete SOURCE review inspects implemented exact boundary/bytes/dependencies, separate final reading guards, independent inventory/negative oracles and common-deadline/owned-cleanup code. Later official finite preparation supplies actual discovery/budgets/counts; first raw failure is preserved. Independent A evidence acceptance precedes B. No changes to FR063 evidence, gameplay/storage authority, provider dispatch, Windows, broad suites, PR/main/merge.

## Read-time fingerprints

Filesystem reads only, not a Git/AST/build verification. Rebind later; earlier supplement retains the wider source inventory.

| Path | Bytes | SHA-256 |
|---|---:|---|
| `CLI_Agent_Daemon_Specification.md` | 160255 | `58f19cf556acafec5afe6214ce613f29c64affd085d815620b0252f710ddff5b` |
| `BookOfEternityClient/game_master_daemon.ps1` | 398600 | `f1f9fd80e0e26570990a35ad6009429025d4620e31404ae0aad554ca4c9b445d` |
| `BookOfEternityClient/Launcher/gm_main_operation.ps1` | 25591 | `9ec146965c358bf28424154c1d9fa1fa5e626cb0b5568911a12b130dfb6ab97d` |
| `BookOfEternityClient/BookOfEternityClient.csproj` | 5795 | `b1bafa2fffd26263bc821cd668f20d199a2d08a1a6ed1a4f32c69b95a893d59e` |
| `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs` | 289155 | `a82fddd96ea243347391022ea5231cdd0b862801ababa70a95e8e66ac1cd06ee` |
| `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs` | 119647 | `87b67f297db504ff79f461238ca9ae4dafd6017886acedb7ad3652af26bfc752` |
| `BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.BrowserInputRecovery.cs` | 30914 | `93403801377336387e5cf20399b54d1aa928ff3e40dc30fe0ab43dbf96559981` |
| `tests/fixtures/GmPromptDelivery/daemon-functions.ps1` | 21914 | `0d0897f348986c585c989a48ac43070b398fd45914852f10065bcd5033715afe` |
| `specs/1536-complete-wound-materialization/recovery/gm-doc-modularization-planning-draft-20261013/coordinator-supplement-followup-review-20261013.md` | 2351 | `2131e42ae80e9f957104280ba4409f774efe4cb598d41987270fdaaee2e3bd7a` |

Prior supplement remains unchanged: `gm1557-delivery-routing-disposition-supplement.md`, 35433 bytes, SHA-256 `4f4127a51e05ce38c9f820d8817fa63153b2c274287196f4e552cb548e916953`.

Literal after-image: 2480 bytes, SHA-256 `c03cdd1d2d9c49b567e4392cad6bf5d4a488de64b1eb8e949eda7e53dc770941` (UTF-8, no BOM, one final LF).

**External preparation complete. AST, implementation, package delivery and cleanup qualification UNRUN. FINAL STOPPED.**
