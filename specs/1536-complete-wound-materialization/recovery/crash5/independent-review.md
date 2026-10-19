# CRASH5 — независимый review implementation / PREP

Reviewer: отдельный agent context `review_crash5_implementation`, явно выбранные **gpt-6-astra / high**. Parent реализовывал изменения; reviewer исходники не редактировал и tests/build/discovery/native не запускал.

## SOURCE

Первичный полный diff: approved plan1 `6f2d4d48615db910168d6c1a32841a1ffb037147` → `51f1225b56645d6be1ae7893857404e9561006d3`. Оригинальные требования spec3 и сохраняемые контракты проверены вместе с code/tests/docs.

Единственное замечание P2: test driver ожидал HostExited перед StopAndObserveAsync в natural-stop-race; production в таком случае пропускал S. Это пробел обязательного L9 coverage, не доказанный production defect. Parent подтвердил путь и исправил только test driver/assertions на `5f0c08ae5b9303bca7c0a44b1509edcf89fa974c`: реальный S ставится в ожидание при живом child, затем child/supervisor естественно завершаются и запись в закрытый pipe faulted. Проверяется positive natural root0/EOF/dispose без uncertainty. Native observations не подменяются.

Focused SOURCE correction accepted на5f; новых замечаний нет. Bootstrap B2/callback/A2, sticky uncertainty, original-owner custody, legacy worker/terminal defaults, неизменный C protocol, полные streams, platform guards, stage bounds и исходные игровые assertions проверены. Linux runtime остаётся UNRUN.

## PREP

Reviewer сверил T217-prep.json с шестью текущими DLL; все hashes совпали. Обе XML-enabled builds exit0, changed-source XML diagnostics0. Catalog813categories/11831methods valid. Ordinary1Theory/1descriptor/5cases/15m, contract4descriptors/37cases. PREP0executed,cleanupcomplete.

Отдельно сверены четыре TRX helper run на51f:9 integration common guards,15 unit finite+source guards,6 stages,7 existing managed cases. Все37 executed=passed,0failed/error/timeout/aborted/notExecuted. Повтор после test-only Linux fixture correction не требуется; provenance51f сохранён.

Handoff commands проверены: отдельная native package preparation, fresh XML builds, Linux PREP review, controls перед ordinaryfive, проверка exit codes. Parent дополнительно синтаксически разобрал оба PowerShell блока без выполнения.

**SOURCE + PREP accepted at5f0c08ae5b9303bca7c0a44b1509edcf89fa974c; open findings0.** Это допускает согласованный локальный Windows5. На этой границе Windows5 и весь Linux runtime — UNRUN. Финальный evidence review выполняется после фактического Windows результата.

## T219 first Windows evidence

The same independent reviewer accepted FAIL_INCOMPLETE with no reporting findings: six DLL hashes still bind to5f, metadata checkpoint9313 has no C# change; console cuts1/4/2/0 timeout at220s, exit-1, EOF/join true, CleanupFailure null. No producer73/resumer observed. No TRX: planned5/completed0, distinct console failures4. Category900.034s, total wall921.433s, runner exit124; generic20-minute outer-lane wording is not category elapsed time. Runner owned-tree cleanup true does not mean fixture deletion: cut3 retained directory remains. Raw log/summary copies match. Last scalar stage does not prove the blocked await. No runtime commands were issued by reviewer.

## T227 SOURCE and preparation

Focused independent SOURCE accepted8326d14b1 with no findings. Exact common source42 fixture arguments, ordinary Windows guards, original custody/deadlines, null default for existing consumers, bounded checkpoint mapping, unchanged production/assertions/qualification selection and multiline XML inspected. RED80b confirms6PASS/1expectedFAIL; no runtime probe executed by reviewer.

Parent PREP8326d14b1: XML builds unit40.14s94warnings0errors, integration23.95s96warnings0errors; changed XML diagnostics0. New xUnit1013 is the intentionally public reflection child entrypoint, consistent with existing dispatch helpers. Stage7GREEN allpassed21.579s with owned cleanup. Catalog814/11833 valid; probe1descriptor/1case/5m, ordinary1descriptor/5cases/15m; PREP0executed. Six DLL hashes still match T227-prep.json. Focused independent PREP accepted before the one probe, no findings; reviewer independently matched all six DLL hashes and TRX/catalog/selection counts.

## T227 final evidence / T228 SOURCE

Independent gpt-6-astra/high accepted T227 FAIL_CHILD_TIMEOUT with no findings:1completed/1failed/0skip, runner1/category timeout false,242.070s, child42552 exit-1/EOF/join true, cleanup null and two fixture-removal records. Raw stdout matches join. capture87.740s → validated161.501s proves progress/interval, not an isolated method duration or cause.

T228 SOURCE25ef748ce accepted with no findings. Only sourceprobe enables existing read/mutation hooks. Fixed phases, bounded1024paths*512characters, eight periodic emissions/phase plus boundary snapshots, shared writer64/2048 limits, no paths/payload/newI/O; lock order verified. RED5f32304ce:9cases7PASS/2expectedFAIL. Fresh integration XML45.33s153warnings0errors, changed XML diagnostics0; stage9GREEN9PASS0skip22.577s/cleanup. Catalog814/11835; one probePlanOnly5m,0executed. T228-prep.json binds the three selected integration DLLs; unit sources unchanged/not rerun. Focused independent PREP accepted before the one causal-count probe, no findings: all three selected DLLs, stage9GREEN, catalog814/11835 and probe1/5m verified.

## Final T228 evidence and draft spec4

Separate gpt-6-astra/high review accepted194b5b665, no blocking findings. TRX1completed/1failed/0skip; child220s/runner1/category timeout false/wall242.654s; raw stdout, counts and two owned-root cleanup records agree. All three selected DLL hashes still match. Last post-validated snapshot:1740 rollback initial callbacks,59distinct paths,1681repeats,no path overflow. These are lower-bound callback observations, not completed I/O or isolated timing.

Draft spec4 accepted for presentation to the owner: preserves dependency-change detection, original assertions/budgets, requires causal RED and negative controls, leaves safe validation lifetime explicitly unresolved for planning. This is NOT design/implementation approval for an optimization. Production requires explicit owner approval and the stated Spec Kit phases. Windows5 remains FAIL_INCOMPLETE; Linux remains UNRUN.

Workflow checkpoint: parent owned implementation; independent Astra/high review found one substantive P2 in native race-control coverage, corrected and accepted. Later test-only probes and evidence reviews had no blocking findings. Ordinary Windows plus two hypothesis-specific probes localized and measured actual work; no fixture shortcut or production cache was introduced. No cost/time savings are claimed. The proposed validation lifetime warrants a complex independent review after planning/implementation; the current parent-owned-block plus separate-review pattern is retained.