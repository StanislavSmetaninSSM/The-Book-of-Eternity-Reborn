# T179-Pd.3b Windows worker2 — affected independent SOURCE review

Decision: **ACCEPTED_SOURCE_FIRST_WINDOWS_WORKER2**, subject to the precise publication, recovery and native preparation conditions below. No unresolved material source blocker was found in this bounded correction. This is source acceptance and a conditional first-run grant, not Windows runtime success or final bounded integration acceptance.

Reviewed source/carrier: `8e0a5e40d28b103fa509f40dd7f1a5b293f6545c`.
Scope base: `f9cf1a16b2d186d8ecfc00333f9b04a2e950efe5`.
Fresh Linux compiled inputs: `a09272e91b379adb792450cd9e9d4f2a928d8a9b`.
Prior actual implementation source: `f9bc5cc824cbc374225e64e8fe6c2da4cd8f09c9`.
Retained full recovery base: `7acd4a960fa273e0df2dd83e4998baef79ba94d5`.

Independent reviewer `/root/t179pc_independent_review`, separate from author/root, retains the coordinator-confirmed actual requested configuration `gpt-6-astra`, reasoning effort `xhigh`. Backend model/effort telemetry is unavailable. The reviewer made no repository changes and ran no build, discovery, test, application, network or delegated work. Python/Git operations inspected existing source and documentary bytes only.

## Findings and correction closure

**WIN-C01 is closed.** Workflow lines 112–120 validate distinct raw MD/JSON hashes, then parse the pinned JSON and bind its `reviewedSource`, exact decision, `markdownSha256` and sole released category/count/budget/platform/attempt to the release request. A historical review that merely has a matching file hash but grants a different source or run cannot pass these checks.

**WIN-C02 is closed.** Lines 95–107 require exactly one parent, an exact acceptance-parent SHA, absence of the release file in that parent, and a sole added dedicated release file. Bounded pinned fetch and ancestry checks fail closed. Reviewed-source→acceptance changes are restricted to the task's documentary paths; production, tests, catalog, scripts and this workflow cannot change in that interval. `GITHUB_RUN_ATTEMPT` must be `1`. Release-file edits or a merge child do not satisfy the accepted protocol. The dedicated file is absent at this review target.

**HS-WORKER-F01 is addressed at the source/host level, not yet proven by execution.** The unchanged ordinary backend selector chooses existing WindowsJob for Windows/Auto before the Linux WorkerRelease refusal. The real engine still constructs the existing public pool; no Linux admission, synthetic capability, fake task result, Ready, issuer or production seam was introduced. The concrete workflow admits only actual Windows. Existing quoting/host launch paths and the genuine worker proposal/apply fixture remain the route to be exercised. Launch, job ownership, proposal application and cleanup success on the hosted machine are UNRUN.

**O02 is addressed at source level.** The only C# hunk adds the existing per-test bounded logger and records its settled messages, actual OS and chosen executable. It retains at most 24 messages of 2,000 characters each and is injected through the existing helper parameter. The real Wait/40-second guard/real Escape/join-both handling, failure stack preservation, task/audit evidence, matching request/session/turn/G, successful proposal-applied requirement, direct-no-Ready versus generated-Ready ledger assertions, unsupported HOT disposition, no S/B/M3 assertions and strict owned fixture cleanup are unchanged. Fallback diagnostics are improved without converting a refusal into a positive control.

A broader static directory check found the pre-existing case-only directory alias `Tools`/`tools`; their descendants are disjoint (`Tools/Search-GmMemory.ps1` versus `tools/gm-relay/*`). There are no full-file case collisions, file/directory collisions, invalid Windows filename components or symlinks in the 54,428-file tree. This is not a confirmed checkout blocker. The longest relative path is 379 characters and 12,361 paths exceed 180; the workflow sets Git `core.longpaths=true` and `core.autocrlf=false` before checkout. Actual hosted checkout and native filesystem behavior remain unqualified. The earlier consultation's zero-collision statement concerned full file paths, not all directory aliases.

## Scope, preparation and provenance

The exact non-documentary change is one new 206-line gated workflow, the worker test logger/diagnostic hunk (5 additions/1 deletion), worker catalog responsibility/Windows routing (4/4), and its selection explanation (2/2). Production, runner scripts, unit tests and other fixtures are unchanged. The a092→8e0 changes contain only this workflow's gate corrections and task documentation/evidence, with no compiled input or category/selection change. Rebuilding C# for those later YAML-only corrections would not verify the changed PowerShell guards; retaining a092 compilation provenance is accurate.

Independently checked all **12** raw/local/stored/committed-Git preparation chains, including both complete build logs, discovery logs, exact argv/native metadata, plans and summaries. BOTH/catalog returned native 0 at clean a092: 11,725 methods/files, 777 categories, no unmapped/stale selectors; 133 integration and 35 unit warnings, zero errors. Runner wall time was **126.2165481 s** (integration phase 98.1892055 s; unit 15.7810066 s). Exact singleton PlanOnly returned native 0 in **5.4915584 s**. These are the saved raw values, not earlier approximate messages. Both phases executed **zero tests**; PlanOnly `Selection.Complete=false` is not GREEN and is expected for discovery-only preparation.

The plan contains exactly one integration descriptor for `HotSubmission_ActualWorkerApplyCannotIssueOrdinaryReadyCustody`, two cases, exclusive, frontend false, five-minute category budget. Catalog metadata and the saved CI matrix put only this owner on Windows; the seven historical selections remain on Linux. That whole selection/matrix is not an aggregate execution grant. The workflow consumes a separately released singleton file.

All four current Linux DLLs match the a092 manifest: client copies `d134faf0436de2fac53cb7e4341bfcc5b07140538d004446c83f29bfeae1b8eb`; unit tests `ebcfda920d8d84bceec7d49f02197edb4deec4e148dd2c4ffe0e249ab8d426f4`; integration tests `94442441cb7a67a640e2432b96f6482195829fa1444cb51a0c74af95c9cdab3c`. No binary equality is claimed with f9bc or future Windows compilation. Prior 13 PASS retain their original f9bc source/platform/binary provenance; the first Linux worker2 packet remains two FAIL. The affected hunk does not justify replaying those unchanged 13 cases, protected Late2, prior 131 reader cases, or causal REDs.

Fresh source recovery combines the retained independently verified full 7acd GitHub tree with a NEW complete 8e0 GitHub delta. Independently verified all **172** recovered files against writer bytes, exact Git blob SHA1, SHA256 and executable mode, covering every **169** changed paths and no deletion. The base remains clean, at exact 7acd, with no alternates or sparse checkout; its original full-byte receipt hash is unchanged. This is explicitly full-base-plus-complete-delta recovery, not a newly rebuilt full current checkout. Current receipt SHA256: `bc45682c1480397ed611e28e11800bc988ccaf1acaf09ba79fe9d814a54bbcac`. No reviewer network operation or restored runtime was performed.

Current plan/tasks correctly keep actual release empty pending this decision, preserve the failed packet and prior PASS provenance, and leave broad gates open. No GM prompt/example update is needed for this test-only diagnostic/CI correction: no production or GM-authored/public schema changes exist.

## Exact conditional actual grant

Only `fr063-hot-worker-ready-disposition`, **2 cases**, **5 minutes**, `windows-latest`, attempt **FIRST_NATIVE_WINDOWS_ONLY** is conditionally released. Required sequence:

1. Root verifies this immutable pair, saves/pushes/readbacks it with the exact reviewed source and recovers that acceptance checkpoint. The source/configuration delta must remain within the workflow's documentary allowance.
2. Make a single-parent child commit whose only change is the first addition of `tests/selections/1536-hot-worker-windows.json`, pinning that acceptance parent and this exact MD/JSON pair. Do not trigger a broad PR/main workflow or use a workflow retry as this grant.
3. On the native Windows host, the pinned workflow must first pass source/review/branch/attempt checks, fresh BOTH/catalog with zero execution, and exact singleton PlanOnly with zero execution. It then records four freshly built Windows DLLs, clean source and empty owned-temp prefix before the sole two-case `-NoBuild` actual.
4. Preserve the first outcome without retry. Native argv/timestamps/stdout and full result directories are uploaded with `always()`. After actual execution, raw TRX receipts are parsed and each distinct owned root is physically checked on Windows; non-absence errors fail closed, including when native tests failed. Source, DLL, process/fixture cleanup, exact counts, duplicates and five-minute bounds must qualify. A pre-actual failure releases no actual, and an actual failure remains a failure.
5. Retrieve and freeze/push/readback/recover the complete first native artifact packet. Separate independent EVIDENCE and final bounded integration review are still required. No second run or other actual is granted here.

The workflow's raw artifact and cleanup checks are source-reviewed, not executed by this reviewer. Author-reported YAML/PowerShell syntax parsing and matrix planning do not substitute for native evidence. General Windows/native uncertainty guarantees, isolated unsupported Begin/Ld and other fault seams, full HOT/3b/HS02–16/S/B/M3/F1/T197/new QTE, D4, guided US5, actual DOM/visual, Windows-wide acceptance and D8 remain OPEN.

## Documentary verification

The companion JSON contains 187 exact Git/blob/file pins, 10 source projections, four exact diff argv/hash records, all 12 raw artifact chains, four binary pins, recovery inventory and read-only command results. Inspection artifact SHA256: `c1e80ade64f55cf9802647c7c105ebe59a942580a0220aba382d86c2bd377933`. Workflow raw SHA256: `e2a7332bc16d694cca9044381f226946b285721407b58902833c73913c1bc538`, Git blob `443431270aeacae7a62fd95b740fc3a0534d3fdc`.

Early documentary checker versions stopped on two mistyped historical source paths, then the cleanup-helper path, and an overly broad directory-alias assertion. New uniquely named versions corrected those read-only checks; old scripts and exact diff bytes were preserved. These were reviewer inspection errors, not product/build/test failures or hidden runtime attempts. Final v6 inspection passed; no repository file changed.

**FINALSTOP for this reviewer.** Root may perform only the conditional first Windows protocol above; no actual result is claimed by this report.
