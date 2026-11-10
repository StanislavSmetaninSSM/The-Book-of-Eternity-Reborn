## Bounded active-reference and retained N2 checks, 2026-11-10

#1536. This checkpoint adds tests only on top of 55299e0; it does not close the
issue or qualify the whole feature. No production code, default selection,
workflow, or gameplay contract changed. Publication was explicitly authorized.

Verified locally through the official test runner:

- `1536-active-item-removal.json`: 1/1 PASS. A canonical active physical wound
  retains an exact item treatment requirement; raw removal of that canonical
  item fails closed, with wound/history/item identity preserved. This is not
  command-only discard execution or authorized treatment consumption.
- `1536-independent-effect-removal.json`: 1/1 PASS. Ordinary removal of a genuine
  wound-owned effect retires the effect while its active source wound and
  fingerprint remain unchanged. No wound/history publication paths are emitted.
  This typed test does not claim filesystem publication or the dispel branch.
- `1536-group-publication-rollback.json`: 2/2 PASS, no skips or duplicates,
  complete runner-owned cleanup. Positive retained mixed N2 publishes through
  genuine cold C3 and common C4, preserves both complete parents, sibling effects
  and immutable wound history, then validates signed47. A separate reached late
  cut after committed conflict/resource/receipt writes restores the entire
  signed46 file inventory, bytes, absence and generation; publication authority
  retires and journal/private transport are absent.

The final N2 tests took 15.997 and 11.877 seconds; runner wall time was 49.595
seconds. Named C4/signed47/cut-and-restore phases took 1.115/0.604/0.847 seconds.
Final integration build: 0 errors, 115 warnings, 92.62 seconds. Exact source
checksums were present in its PDB. Independent reviews found no blocking issue.
An initial N2 normal run also passed 1/1; the final run includes named timers.
A premature rollback PlanOnly inspected the pre-build assembly and reported a
stale selector with zero tests; after build, PlanOnly selected exactly two tests
and execution passed. This was verification ordering, not a production failure.
Earlier item-fixture/compile failures remain in local evidence, not hidden.

Optimization: reuse the existing N2 preparation journey without running its
old unpublished leaf separately. Each new integration leaf needs its own real
cold owner/root; caching current authority or sharing mutable fixtures would
invalidate that contract. The linked-effect test uses a cheap typed fixture.
No unchanged zero tests or broad suites were rerun; existing zero 2/2 evidence
at 55299e0 is not claimed as zero C4 evidence. Fresh birth, zero C4, N3 and
cross-attempt retry equivalence remain outside this bounded result. RECON20
Task8 is only partially evidenced; Task9/10 are not marked complete.

Raw diagnostics remain local and are not committed. Final N2 TRX run identity:
`20261110-070930-219-102-a0267d506b1d439087dcc6b90ae82ddb-categories`.
The combined selection SHA256 is
`654035a06f7c31facdc638e358510feaecb94d4f93001f8ee7fa459866611d97`.
All preexisting remote catalog entries retain their exact original text; only
four narrowly scoped categories are added. Hosted CI is not dispatched.

## Current A5 metadata and hosted CI boundary, 2026-11-08

T121-A5-CI-METADATA / [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536).
The accepted 22-path A5 packet is published at 226996efe2f1b1b544d4f9b88349f1fb9d3f1a52
(parent ab722d, tree 53c6fabd4c3c421f81b88d77768c478255e181b2). The new local
five-file metadata delta is separate: independent SOURCE accepted and local
metadata validation passed; final independent outcome/freeze receipt is separate.
See the current plan for the exact scope, commands and retention blocker.

The next saved-block selection contains common-failure preservation, QTE read
diagnostics, current-partition lease and genuine terminal joint: verified
19 methods / 31 cases, budgets 1+1+2+10 minutes, explicit ubuntu-24.04.
The former 17 clean-start/H0 entries leave this selection only; their coverage and
acceptance are not revoked. Manual preparation profiling stays separate.
A5 now has the requested protective 600-second category limit. Its prior original
Fact PASS 342.8188934 seconds / category 349.5574181 seconds was measured under
temporary 600 seconds, not rerun here; historical 120-second UNKNOWN stays intact.
Older restored-120, UNCOMPILED/UNRUN and pending ordinary-selection statements
below describe their earlier checkpoints, not this verified metadata.

Hosted CI dispatch remains BLOCKED: raw artifact uploads violate local-only policy,
and deleting uploads cannot preserve the newest complete capture on an ephemeral
runner. No approved persistent Linux destination is verified. Workflow/guards,
full diagnostic collection and existing evidence are unchanged. No Windows,
hosted-run, whole-branch/main, all published-joint or failed-abort qualification.
Local schema/matrix checks passed: 1,537 total categories, four metadata entries
changed and 1,533 unchanged; one Linux group retains exact reasons/contracts.
The single official -NoBuild -PlanOnly exited 0 in 16.4762297 seconds runner wall,
with four descriptors, 19 methods / 31 planned cases, zero execution/TRX, default
19-minute budget and complete cleanup. Current 3,067 compiler-source/63 output
pins match the retained owning builds; complete catalog inventory is reused.
No gameplay, build or raw decode occurred. Catalog/selection bytes stayed frozen
through this three-document closeout; final evidence and review are separate.
Reuse of current pinned binaries/inventory avoids needless work without caching
authority, changing fresh checks or claiming speedup. No GM contract changed.

## A5 held-lease correction and original600s PASS, 2026-11-08

T121-A5-QTE-LEASE-PROOF / T121-A5-LEASE-AUTHENTIC /
[#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536).
Independent small proof accepted: original12cases gave10PASS,1genuine reader
lockIOException RED and1wrong namespace oracle. Oracle-only4case correction
passed; then the exact existing-lease propagation from3callers through the shared
reader yielded12/12GREEN,category16.946s/120,runner23.806s/180. Causal body unchanged;
physical guard/implicit refusal, current-input/rights and real source-closing
raw-drift/failed-source refusals all preserved,12owned roots removed.

The separately gated unchanged original fullFact now completes1/1PASS:
test342.8188934s,category349.5574181s/600,runner357.1464859s/900, no skip,
duplicate,underfill,timeout or retry. Genuine common composition and owning source
completion succeed; original acquisition/treatment/heal, linked History/identity,
carrier/effect-lineage, alias/incomplete-acquisition refusal, output and unchanged
bytes assertions complete. ProjectCandidateBounds and SealRegistryCandidate
scopes close once. This qualifies that Fact at600s only, not120s, all published
joint consumers, failed production abort cleanup, whole Engine or all#1536.
Historical253.138s FAIL and120s UNKNOWN remain separate; permanentA5 budget120s
was restored unchanged. The longer PASS executes previously unreached work and
is not comparable speedup evidence.

One new-only streaming audit111.346s verified10,435,563rows,566,712,843gzipbytes,
5,341,452,377decodedbytes, EOF/CRC/hashes/sequences/scopes/snapshot/writer ledgers.
No disk inflate, seconddecode, collection quota or extra observer. Loaded3SHA/MVID
joins, exact nonce-root absence and both officialcleanup flags pass. Fresh
Unit129.674s/Integration82.576s builds were reused only after current6PE/PDBjoins,
3083documents,3067compiler-source and63output pins. CURRENTcatalogb9c14 restored;
HEAD237b087/index840874e8 intentional. External evidence:a5-held-lease-authentic-20261108.
Runtime/audit postpins precede this authorized doc-only closeout; final independent
outcome/freeze receipt is separate. No further test is authorized.

Optimization/freshness: old implicit and new explicit routes share existing
SourceDataOperation DATA; explicit mode also admits declared fast hits under its
distinct raw-path/family/lease-mode key. Live owner/generation/root-version/segment
and original completion/effect fences stay authoritative. Existing held lease
avoids the demonstrated nested acquisition; no new cache, authority reuse,
lock bypass, lifetime relaxation, suppression or catch/retry change. Signed
absence/prefix/suffix algorithms stay intact. No gameplay/GM-authored contract
changed, so no prompt/example edit is needed.

CI limit: dedicated tests/selections/1536-a5-current-partition-lease.json and the
external singletonA5 selection are current for this bounded block. Ordinary
tests/selection.json stayed byte-identical (18entries; neither new reader category
norA5 included). Before future integration/publication, reconcile and independently
review its categories/reasons/contracts against the ENTIRE intended unpublished
change, including this block; dedicated12+singletonA5 are not the whole CI set.
Do not change/run that aggregate automatically. Remaining wound-only gap is the
unqualified permanent120s A5 budget/cost decision, not a reason to retry now.
No publication/stage/commit/reset, R2/H0/browser, old raw deletion or blocked-trash
retry. Raw remains external; no automatic second fix or broader acceptance.

## A5 QTE diagnostics: verified proof and authentic FAIL, 2026-11-08

T121-A5-QTE-DIAGNOSTIC / [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536).
Cheap proof completed and independently accepted: original9case bodies unchanged,
RED5missing-diagnostic FAIL/4PASS → GREEN9PASS plus1new getter-safety PASS with no
prior RED claim. Exact8methods/10cases, category5.687s/60, runner12.908s/120;
4owned roots removed, public exception/issue/JSON controls and local capture pass.
Typed non-Exception DATA stays internal; outward classification/filters, source
lifetimes, cache eligibility, current raw/owner/security checks remain unchanged.
Successful-abort/resource-publication issue-return conversions remain excluded.
Actual adapter branches/Engine optional-HOT routing are not runtime-qualified by
these cheap transport controls. No general ReadResult equality compatibility claim.

The one separately gated original fullFact then completed1FAIL/0PASS, not timeout:
test253.1379978s, category259.7938278s/600, runner267.7727435s/900. No skip,
duplicate, underfill or retry. Its only test delta is the proven local renderer;
all original scopes/assertions and measureStageCalls=false are unchanged.
Primary is now exposed for THIS attempt: IOException opening the nonce fixture's
gm-main-owner.lock with FileShare.None, from current optional receipt read
TryReadCurrentPartition792/800 → implicit ReadFileSync → follow-up quiescence
acquisition2064 → MainAdmission. Secondary is abort continuation-seal validation
→ SourceDataOperation.RequireOpen: "The source operation is no longer available."

Generic IOException bypasses existing ReadResult catch filters, so no typed QTE
diagnostic row is expected or present; the aggregate preserves the original error.
Recompute's failure catch invokes recovery/source invalidation; the later abort
refuses that failed source. source.CompleteAsync and all post-call common/history/
carrier/effect/refusal/byte oracles are unreached. Physical fixture/runner cleanup
succeeded; production reservation abort success and fullA5 remain unqualified.

The stack proves lock-open contention and the no-ambient+missing-or-pending branch,
not the actual handle holder, missing versus pending choice, or cause of ambient
flow loss. Three real helper callers already have a lease: adapter270/741 and
MortalJointRepair partial23. The helper omits it; an existing explicit leased sync
reader preserves fresh validation without reacquiring. This is a bounded source
hypothesis, not an applied fix or proof about the older masked failure/120s timeout.
Next proposal: tiny real signed-absence TryReadPublishedReceiptPartition fixture,
ordinary implicit/explicit held-lease boundary and malformed/stale/foreign/disposed
controls. No new test, fix, catch, lock bypass, cache change or runtime authorized.

New capture verified once by streaming, with no disk inflate or old decode:
8,705,707rows/8,705,702timing events,471,789,927gzip/4,451,094,315decoded bytes;
exact EOF/CRC/hashes/sequences/scope/snapshot/writer ledgers,0open/late/pending/
failed events. Nonce root removed and both official cleanup flags succeeded;
3loaded assemblies match SHA/MVID,4PE/PDBs/3061documents and36outputs remain current.
Fresh Integration89.552s/0errors reused; no unchanged-code rebuild or Plan repeat.
CURRENT catalog restored exactly to e0326698 baseline; permanentA5120/H0600/R2300
budgets unchanged. No cross-exec process-absence or known-cgroup/peak-memory claim.

Evidence stays outsideGit: a5-qte-read-result-diagnosis-20261108 (cheap proof) and
a5-qte-authentic-capture-20261108 (authentic attempt and bounded next-test proposal).
TRX61ac5aba7db698ea64104cbdcc8204eea910426950471bc31a7b75f283202bd8.
All runtime source/policy/output pins passed before this doc-only factual closeout.
Independent outcome/final-freeze review has a separate local receipt. Actual full
A5 is FAIL; historical120s remains UNKNOWN. No speedup or gameplay/GM contract
change, hence no prompt/example update. No staging/commit/push/reset, raw retirement,
R2/H0/browser or other run. HEAD237b087/index840874e8 remain intentional.

## A5 abort masking: bounded RED/GREEN result, 2026-11-08

T121-A5-SOURCE-ABORT / [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536).
The two common-plan catch consumers share one internal failure handler. After a
behavior-equivalent extraction, real tiny-source drift and completed/disposed
source refusals produced the independently accepted causal RED:3expected FAIL/
5PASS,8/8complete, category6.324s/60, runner16.762s/120. Each failure reached only
the missing-aggregate oracle; all normal issue/null/once, SessionReplaced/Overflow
identity+stack and original null/empty-bound controls passed.

The minimal fix aggregates ONLY secondary InvalidOperationException, including
ObjectDisposedException and explicitly excluding SessionReplacedException. Both
original exception objects, ordering, stacks and full diagnostic representations
are preserved. Other secondary families, primary catch filters, distinct prefix
catch, registry/source ownership, fresh fences and DATA lifetimes remain unchanged.
Exact unchanged tests then passed8/8,0FAIL, category5.290s/60, runner13.062s/120.
No timeout/skip/underfill/duplicate/retry in either run; all4owned fixture roots per
run were removed and both official cleanup flags succeeded. No cross-namespace
process-absence claim. Independent SOURCE/PREP and RED OUTCOME were accepted;
final GREEN OUTCOME review has its separate local receipt, not a new source edit.

Fresh owning Unit build0errors/89.572s;3DLL/PDB and2066current source-document joins,
24selected output pins and exact5method/8case Plan agree. The prior full catalog
1535categories/13633methods was retained because identities/selectors/projects
were unchanged. Integration outputs are stale/unqualified after the fix. All
source/runner/test/runtime evidence is outsideGit in a5-source-abort-causality-
20261108. RED TRX62c02c40ac45698ed691269126afcd38f919b69e3d6ef9d606bc4a97dc47ea7e;
GREEN TRXd13beba1618210b9ee946e030ed57ed9e9579fac39bc2fd445c2c899eac9525c.
Linux SDK10.0.402/net8.0.33/PowerShell7.6.8; Windows and genuine registry cleanup
(owned/foreign/open receipt) and whole Engine closing/logger paths are not qualified.

The actual full-A5 primary+secondary pair remains unextracted. The cheap primary
was deliberate ordinary-file drift, NOT the established historical trigger. Full
A5 remains FAIL and historical120s outcomeUNKNOWN. Existing small QTE typed seams
can prove an injected failed-source transition but cannot identify this trigger;
reconstruction may already convert earlier exceptions to issues. Proposed next
step is a separately gated first-boundary QTE issue/exception diagnostic, not a
lifetime change or blind full retry. FullA5/R2/H0/browser remain HOLD.

Optimization: cheap once-only failure handling introduces no repeated expensive
DATA computation, so no cache or speedup claim. No gameplay/GM contract change,
hence no prompt/example update. Latest verified remote ab722dcd7, deliberate local
HEAD 237b087/index 840874e8 remain unchanged; no staging, commit, publication or raw
retirement. This result closes only the proven narrow masking behavior, not A5.

## A5 full-contract one-off600s outcome: FAIL, 2026-11-08

T121-A5-FULL-600 / #1536. Independent SOURCE/PREP and parent gate admitted one
unchanged full terminal Fact under temporary600s, whole runner900s/outer930+30s.
Actual1FAIL/0PASS: test258.6008626s, category264.045817s, runner270.5674121s.
No timeout, skip, duplicate, underfill or retry. Original category bytes restored
exactly to120s; H0600s/R2300s/diagnostic120s remain unchanged. Independent OUTCOME accepted.

The visible exception, "The source operation is no longer available", arises in
CommonPlan.cs563's catch/abort path through continuation-seal validation. It masks
the original common suffix exception, whose cause remains UNKNOWN. Genuine full
fixture preparation/binding returned and candidate/admission scopes ran; no
SealRegistryCandidate scope was recorded. The test's source.CompleteAsync and all
post-call terminal/common/history/carrier/output/refusal/byte oracles were unreached.
This is a completed failed full-contract attempt, not successful qualification or
a semantic assertion RED. Historical120.0346224s/noTRX/incomplete capture remains
UNKNOWN; its cause and phase at timeout are not established by the new result.

Full uncapped passive/gzip/continuing capture verifies8,705,707JSONL rows,
8,705,702timing events/4,352,851closed intervals across541names,471,733,263stored
and4,450,894,909decoded bytes. Strict single-member EOF/CRC/hashes, exact sequences/
scopes/snapshot/writer ledgers pass with0open/late/pending/failures. Same-run native
owned-tree/runtime cleanup succeeds and nonce fixture root is absent. Cross-exec
PID namespaces are isolated, so no cross-call process-absence claim is made.
All3035source/28runtime pins, loaded3assemblies, HEAD/index and catalog stay exact.

PrepareBoundJoint130.899s includes stages31.585s and binding72.509s; subsequent
registry candidate74.832s/admission17.363s are inclusive observations.51bundle
copies total84.876s and1688replays62.816s inclusive; do not add overlapping spans.
No cache change, speedup, historical-cause or peak-memory/cgroup-limit claim.
No gameplay/GM change, so no prompt/example updates. Full source/runtime evidence
is retained outsideGit in a5-full-contract-600s-20261108/outcome-handoff.md.
Proposed next block: cheap causal source-lifetime/abort-masking reproduction,
with fresh ownership/failure semantics preserved. No new test/product edit or
full runtime is authorized here. Old-raw retirement remains separately pending
after unsupported Trash, not failed fixture cleanup. No R2/H0/browser repeat,
staging, commit or publication.

## A5 stage-call timing measured, 2026-11-08

T121-A5-STAGE-SPLIT / #1536: independent SOURCE/PREP accepted the default-false
fixture-only four-timer increment and unchanged one-Fact120s selection. Fresh
Integration XML build77.35s/112warnings/0errors and3057current PDB/source joins
passed; unchanged Unit/catalog evidence was retained, not rerun. The one gated
run passed1/1: test47.6594479s, category52.7258731s/120, runner59.9862853s.
No failure, skip, duplicate, underfill, timeout or retry. OUTCOME review pending.

Measured BuildWoundStages26.720981995s now contains four real call spans:
prepared1.036718610s; effect10.264807368s (including its original inline effect
input composition); final7.986951412s; bundle constructor7.430022705s. Stage
preparation37.920664633s is a broader inclusive parent. Each of the first three
calls contains1574recorded child scopes; its fresh current check is respectively
58.280884ms/60.705683ms/46.315162ms. The constructor has no recorded child scope,
not proof of no internal work. Inner algorithms/CPU/IO/capture contributions
remain unseparated. These results localize wall cost to existing calls, not the
cause of the historical fullA5 timeout or a reason to alter production caching.

All408original named operation start/end counts match the first complete capture;
only four names/eight boundaries were added. Both copy windows still execute one
richer expected-preparation source and18scalar replays. No speedup is inferred
from the lower second-run times. Current owner/generation, detached independent
DATA,58unchanged before-images and normal source/reservation/fixture cleanup pass.
Whole oracle/source-closing/fixture-closing wall costs remain unmeasured.

Full uncapped passive/gzip/continuing capture verifies1,153,736JSONL rows and
1,153,716original timing events,62,242,585compressed/583,350,921decoded bytes,
EOF/CRC/hashes and complete sequence/scope/accounting with no failures/pending/
late/open events. Native owned-tree/runtime cleanup succeeded; fixture root is
absent. All runtime/source pins and loaded assembly joins remain exact. The first
complete capture is retained unchanged through independent review/retention.

No production/cache/fresh-fence/gameplay/GM changes, hence no prompt/example update.
No fullA5/R2/browser/H0 run or publication. FullA5 remains UNKNOWN. No third
instrumentation or production change is authorized by this measurement alone.
Exact source, raw/TRX, interval/child counts and limitations: the external
.../a5-terminal-preparation-20261108/stage-split/outcome-handoff.md.

## A5 terminal preparation diagnostic measured, 2026-11-08

Issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
T121-A5-TERMINAL-PREPARATION. Independent SOURCE/PREP accepted the test-only
stage-only probe, fresh Unit/Integration XML builds, current PDB/output lineage,
catalog discovery1534/13629 and exact one-Fact Plan. The single authorized run
passed1/1: test58.5147703s, category67.2647499s/120, runner75.2176726s. No retry,
timeout, skip, underfill or duplicate. Independent OUTCOME and factual-documentary review accepted without findings.

Initialize took2.026140s; genuine stage preparation46.056349s, including
BuildWoundStages29.708954s. Isolated DetachedCopy intervals took2.328179s and
1.605852s. Each recorded one WoundPlanCache.PreparedExpectedReplay.Source
(82.204637ms/38.952202ms) and18 JointPreparation.Replay calls
(916.420567ms/684.136820ms inclusive). These overlapping nested times are not
added. Terminal projection remains deliberately unsupported: measured fallback
cost is not a cache defect, speedup or exact cause of the old fullA5 timeout.

Full-view/terminal/mutation/currentness/byte assertions remain outside both copy
intervals. Whole oracle and source-closing wall costs were not separately timed;
zero source-closing timing rows does not mean zero cost. The two fresh currentness
calls took254.017ms/178.592ms within those oracle phases. Genuine terminal stages,
independent detached DATA, current lease/generation, unchanged before-images and
normal source-owner/reservation/runtime/fixture closing all passed.

Full uncapped passive/gzip/continuing capture verified1,153,728 JSONL rows,
1,153,708 original events,62,091,527 compressed/583,858,173 decoded bytes; EOF/CRC,
both hashes, exact row/sequence/scope accounting and no failures/pending/late/open
scopes. Native owned-tree/runtime cleanup succeeded and the fixture root is absent.
Timing-worker serialization/write ledger12.5273871s overlaps the test; writer closing
0.0007418s and independent gzip decode1.7796499s are separate observed spans,
not a causal diagnostic-overhead estimate. Current source/runtime pins and loaded
assembly joins match; no production/cache/fresh-fence or gameplay/GM contract
change, hence no prompt/example update.

This PASS qualifies only the narrow diagnostic. FullA5 remains UNKNOWN; resource/
common binding/composition was not entered. R2/browser/H0 were not repeated.
No publication/commit/HEAD/index changes; prior evidence is untouched. Exact source,
TRX, hashes, interval counts and limitations are retained outsideGit in
../reconstruction-evidence-20261107/a5-terminal-preparation-20261108/outcome-handoff.md.
The next cause-first target is the measured BuildWoundStages body; its individual
prepared/effect/final/constructor costs are not separated by this capture. No new
runtime or production change is authorized by this observation alone.

## Current R2/A5 wound-only status reconciliation, 2026-11-08

The later Local physical course checkpoint dated2026-11-03 supersedes a blanket
R2 functional UNKNOWN: it records the complete in-process shared browser command/
queue/handler and Engine course passing twice at source base470cab31e9b6c2e9487775490a0ba567d2ee5e4c.
The second1/1PASS took261.6183978seconds and includes an explicit terminal-heal
assertion, terminal History, active-wound removal and complete fixture/runner
cleanup; the earlier localPASS took263.2112315seconds. Both used the five-minute
category bound with detailed filesystem timing disabled and journey logs retained.
Current committed fixture/helper/checkpoint bytes and category metadata retain
that version. This is a reconciliation of the recorded historical local result,
NOT a new execution or independent requalification of current dirty shared code.

The earlier180-second R2 attempt remains UNKNOWN and did not meet that bound.
DOM/real-browser/provider/fresh-process and wholeT121/#1536 acceptance remain
unproved. The later paragraph does not separately identify independent review.
Its primary /tmp/boe-local-r2-terminal directory is unavailable in this executor;
the recorded TRX5a0301aaa87a8ddc338bb7c999978c34efaf6ca34b8ce9a40350b82b71d6a2bf
and summary8676edc82dd8aaf6ca99f9bf4c41110fecc5671d4c681f47a8b78bb77b46947a
are historical references, not freshly rehashed files. No R2 repeat is justified
merely by the older UNKNOWN wording. Earlier dated status rows remain historical.

A5 joint terminal remains genuinely UNKNOWN:120.0346224seconds/120, no completed
TRX, incomplete uncapped passive capture. Anchor2/output10/controls3 do not qualify
the full common-composition peer. Its old primary raw/root are also unavailable
here, so no precise timeout cause or phase-at-kill is newly asserted. Current
source still requires the full passive/gzip/continuing capture and fresh common
owner/baseline/resource/replay checks. H0's separately authorized600-second budget
does not change A5's120seconds or R2's300seconds. Read-only source diagnosis is in
progress; no fullA5 retry, weakened capture, new cache or production fix is admitted
without a concrete cause/controlled evidence and independent scope/preparation.

No code, build, test runtime, new browser attempt, gameplay/GM contract change or
publication occurred in this reconciliation. A prior separate normal Chromium
capability probe failed with socket() Operation not permitted and was cleaned up;
no bypass/retry was used. That OS-browser failure does not block the in-process
R2 category. Source notes and current pins remain outsideGit under
reconstruction-evidence-20261107/wound-r2-a5-reconciliation-20261108.
Independent documentary review is pending.

# T081-D Task B atomic v4 DATA GREEN accepted, 2026-11-05

Independent SOURCE and separate semantic/execution OUTCOME accept the reader-only
Task B block: **501/501 unique PASS** in three exact Unit selections, not a full
suite. The 217 new v4 rows comprise 152 intrinsic and 65 receipt/History/publication
cases; selected legacy pressure 191 and parent/zero 93 also pass. Their 86 methods
and 14 TRX files reconcile exact discovered Fact/Theory rows, 501 distinct test and
execution IDs, definitions and TestEntry/test-list joins. No skip, duplicate,
exceptional case, underfill, retry or extra runtime occurred.

The atomic nine-file implementation adds the strict v4 codec and immutable complete
pair DATA, one retained comparison, all-parent History anchors, relative v3/v4 log
ordering, full-witness and both fail-closed C3 recognizers. Legacy singular accessors
refuse v4; v1–v3 contracts and the v2-only zero table stay strict. Supported baselines
precede semantic mutations. The original two codec-support RED Facts now reach
success assertions; the additional 215 rows have observed GREEN only, not an invented
semantic RED. Shape/encoding refusals are not deeper semantic coverage.

Fresh installed SDK 8.0.427/runtime 8.0.33 single-node/no-restore builds completed once:
Unit 87.228846s with 157 warnings / 0 errors, Integration 52.889050s with 115 warnings / 0 errors,
each within 175+5s. One 7.789823s full-six provenance verified 3,025 PDB documents
(3,014 disk/11 embedded), 12 fresh originals and 16 companion joins. List/catalog
and all three exact Plans completed with zero execution: 1,457 categories/13,487
methods/files. This does not qualify SDK10, Windows, restore or unselected tests.

The three runtimes used unchanged 55+5s/60s limits. Runner times were 14.9108098,
18.5997318 and37.0099855s; recorder times 16.410806657,20.460460811 and38.747231113s.
All original exits were 0, each pre/post had 5,859 checks with errors=[], outputs
unchanged, and owned-tree/runtime cleanup flags true. Same-namespace/exact-prefix
endpoints found no new survivor. This is not global/continuous/escaped-descendant
or timeout-cleanup proof. Complete source/PDB/raw logs/TRX and prior 108/113/146
preservation sets remain outside Git; no earlier PASS floor was rerun or combined.

Evidence root: `../local-evidence/wound-history-6007-20261105/spiritual-pressure-pair-data/`.
Atomic SOURCE report: `atomic-implementation-r1/independent-atomic-implementation-r1-source-review.md`,
SHA256 `cbcece84aacbe37a2723c34f63173ee46b578e3da4a1adb9c3df3d7ed7cee7c8`.
Semantic OUTCOME: `semantic-green-execution-r1/independent-integrated-semantic-green-outcome-r1.md`,
SHA256 `356fd3f993f0c170e3f4c60a9cc891430600787b91375b612f1f11ffa53b99a4`.
Execution OUTCOME: `semantic-green-execution-r1/independent-three-runtime-outcome-r1.md`,
SHA256 `e925df9d37fd6368ab98b3c0283a82b398f175be834f36df1ddb02d614941d0b`.
The accepted runtime source freeze is 3ef0d85cd96c542adf5e3a4d683e3b97b8faeeccceaa96bff1e0acde1b0023ab;
this subsequent status-only closeout does not retroactively change that lineage.

Optimization: reuse one actual and one ordinary calculation, parse/fingerprint each
parent once, retain one immutable comparison and use existing transient indexes.
One full-six capture after both builds avoids duplicate metadata work. The strict
JSON ingress no longer decodes/parses the same object twice. No new persistent
cache, carrier rebuild or IO was introduced; no measured speedup is claimed.

**Still closed/unproved:** live pair basis/admission, writer/projector, current-owner
availability, genuine capture-issued C3 success, authenticated v4 signed-frame IO,
C1/cold/C4 and next-sign/rollback journeys. Shared TestSupport and the live singleton/v3
guard are unchanged. Retained DATA/unit-authored History are not two genuine births.
Task C/D, III+IV, attribution, recovery/start settlement and stopped transition UNKNOWNs
remain separate. The private client-owned reader adds no GM-authored/playable capability;
Task D retains the required guide/example/manifest synchronization. No publication.

# T081-D Task B initial codec-support RED accepted, 2026-11-05

Task B has begun with exactly two positive Unit tests and one narrow category.
Independent SOURCE accepted their complete actual-only/ordinary-only witness and
full-parent prerequisites. Production and shared TestSupport remain unchanged;
the live pair actual guard is still singleton/v3. Retained DATA is not two births.

The existing successful Unit build was not repeated after interruption: 27.744011s,
27 warnings / 0 errors. Fresh six-project provenance took 8.661451s, verifying
12 original DLL/PDBs, 16 companion joins and 3,016 PDB documents (3,005 disk /
11 embedded). Unchanged Integration lineage was retained, not rebuilt. This is
installed SDK 8.0.427 / runtime 8.0.33 no-restore evidence, not SDK10/Windows/restore.
ListCategories, catalog discovery and exact PlanOnly passed: 1,456 categories /
13,453 methods, no unmapped/stale selector, exactly two nonexclusive Unit Facts,
zero test execution. Independent preparation and metadata/Plan OUTCOME accepted.

The sole authorized runtime completed **2 executed / 0 PASS / 2 FAIL** with original
exit 1: recorder 12.222775s, runner 10.275133s, category 3.874314s, within 55+5s / 60s.
Both passed complete independent actual23/maxIV and ordinary13/maxII or18/maxIII
witness controls, both full III parent parses and the exact ordinary/actual relation.
Each then failed at the desired-success v4 call, test line82, through Witness138
and ReadVersion54 (supported versions1..3), with the frozen FormatException.
Independent semantic and execution OUTCOME accept **codec-support RED only**.
The fixture-side clamp relation passed; production v4 cap, all success/detachment
assertions after line82, semantic negatives, History/C3 and live pair capability
remain unreached. This is distinct from Task A's accepted actual count RED.

All 6,600 runtime boundary checks reconcile, with unchanged source/binaries.
Runner owned-tree/runtime cleanup succeeded and same-namespace/exact-root endpoint
observations found no new survivor. This is not global/continuous/escaped-descendant
or timeout cleanup proof. Full raw/Failed TRX, exact inputs and independent reports
remain outside Git under spiritual-pressure-pair-data. Task A and both stopped
transition FAIL lineages remain intact; no old PASS floor was rerun or combined.

Optimization: reused unchanged Integration/tool/cache DATA and existing evidence
readers, with fresh boundary hashes and per-Fact independent cheap DATA. The two
no-strain witness controls intentionally establish prerequisites. No new cache,
production optimization or speedup claim. Observed costs describe codec-blocked
execution, not supported-v4 semantic work.

Next is the complete atomic v4 DATA/reader/C3/History closure and finite tests with
a valid v4 baseline before semantic mutations. Keep one comparison with two exact
anchors, strict old versions/zero table, and the live singleton guard closed.
Independent SOURCE and fresh bounded verification are still required; no max-version
only patch, retry, production implementation, install/restore or publication followed.

# T081-D Pair actual Task A causal RED accepted, 2026-11-05

The technical owner accepted the independently reviewed bounded v4 design and
authorized Task A only: two desired-success tests with the live pair actual guard
closed. The four-path test/catalog diff passed independent SOURCE. It adds the
ordinary II+2 and ordinary III+2 capped cases, removes only the old pair-refusal Fact,
and preserves the three singleton guards and shared fixture unchanged. No production
or schema change was made. Retained signed parent DATA is not two executed births.

Fresh Integration build completed in 56.018929s under 175+5s, 115 warnings / 0 errors.
The unchanged five-project Unit lineage was independently rechecked rather than
rebuilt. Fresh six-project provenance completed in 8.611097s: 12 original DLL/PDBs,
16 companion joins and 3,015 PDB documents (3,004 disk / 11 embedded). This qualifies
only the accepted installed SDK 8.0.427 / runtime 8.0.33 path, not SDK10/Windows/restore.
ListCategories, discovery and exact PlanOnly passed: 1,455 categories / 13,451 methods,
no stale/unmapped selectors, exactly two exclusive Integration Facts, zero execution.

The sole runtime completed normally with **2 executed / 0 PASS / 2 FAIL**:
recorder 18.291936s, runner 15.996803s, category 9.105259s, within 55+5s / 60s.
Both passed signed/current complete-parent, creation/History/graph and genuine
ordinal-zero owner prerequisites, plus independent ordinary 13/max II or 18/max III.
Each reached the exact singleton-only pair-count refusal with one ordinary
evaluation/resource batch, one current and one signed batch/two parent resolutions,
zero actual evaluations, full inertness and terminal owner revocation. Both then
failed only the shared unconditional desired-success assertion at line 87.
Independent semantic and execution OUTCOME accept this as causal pair-count RED,
not a passing feature or fixture failure. Overflow clamp, actual IV, C1 and v4
consumers are unreached. No C2/cold/C3/C4, selected IV wound or two-birth claim follows.

All 6,338 runtime boundary checks and 12 binaries remained unchanged. The runner
reported owned-tree/runtime cleanup success; the same-PID-namespace/exact-root
endpoint census found no new survivor. This is normal-exit scoped evidence, not
global/continuous/escaped-descendant or timeout proof. Full raw artifacts and
independent reports remain outside Git in spiritual-pressure-pair-actual-admission.
No retry, install, restore, publication or Task B/C/D execution followed.

Optimization assessment: reused unchanged Unit/tool/cache lineage and the existing
retained-DATA fixture, with fresh independent owners per Fact and one ordinary
computation/batch. Removed one redundant test-only inertness read; the unchanged
comparison helper still performs complete inertness before returning. No new cache
or gameplay optimization; observed phase costs do not establish a speedup.
Next proposed gate is Task B's typed v4 DATA/receipt tests and atomically reviewed
readers under the closed live guard, with separate SOURCE/bounded authorization.
Later-ordinal remains stopped with both earlier fixture FAILs and distinct
lifecycle-nonreuse/isolated-cut UNKNOWN. Recovery/start settlement, two genuine
births, III+IV and explicit ambiguous-action attribution remain open. Earlier
DATA9 / owner18 / genuine5 / actual20 remain separate historical lineages.

# T081-D Transition fixture FAIL; diagnostic branch paused, 2026-11-05

The approved seven-file production admission/observation seam and five-file
fixture/catalog package passed independent combined SOURCE. Fresh SDK 8.0.427 /
runtime 8.0.33 single-node no-restore builds completed: Unit 85.291227s (157 warnings,
0 errors), Integration 55.170276s (115 warnings, 0 errors), each within 175+5s.
Whole five-/six-project provenance readers passed in 6.958359/7.945376s. The final
six-project lineage covers 12 originals, 16 companion joins and 3,014 PDB documents
(3,003 disk plus 11 embedded), independently accepted. This is not SDK10, Windows
or fresh-restore qualification.

Metadata/catalog discovery and exact PlanOnly passed without executing Facts:
1,454 categories / 13,450 methods, no stale/unmapped entries, exactly two exclusive
Integration Facts with no frontend. Independent execution review accepted the
current provenance and literal selection before one unchanged 55+5s runtime leaf.
That leaf completed normally: **2 executed, 0 PASS, 2 fixture FAIL**, runner
15.1311539s, category 9.2870162s, recorder 16.6600007s. It was not a timeout,
underfilled selection or cleanup failure. Both intended transition callbacks
remain UNREACHED; these failures are not causal cache RED or gameplay acceptance.

- Routing row stopped after real E0 close: its supposedly nonmatching owner_turn_end
  trigger had already activated in the original prefix. E0 EffectAfter is cumulative.
- Cut row reached real E0, latest owned source admission/retry and opportunity, then
  the strict worsening validator rejected the helper's changed classification and
  recovery blockers. It copied a creation proposal instead of preserving parent43.

The accepted SOURCE review missed these two fixture premises. Positive later-frontier
observation, ordinary cold/warm reuse, target materialization/cut and post-transition
inertness were not reached. Pre-E0 refusal/disposal controls and cut-row admission
have bounded evidence only; no general observation-seam qualification follows.
All 5,053 runtime pre/post pins and all 12 binaries were unchanged. Runner cleanup
succeeded, same-PID-namespace census found no new process and the exact fixture-prefix
census no new root. This is scoped normal-exit evidence, not global or timeout proof.
Independent execution OUTCOME confirms the FAIL. Full raw artifacts remain outside
Git in spiritual-pressure-group-transitions; transition-two-fail-preservation-r1.json
pins 146 files / 265,941,060 bytes (SHA ad807b3574c07056b112afb46ad987066ee4790de4e0f3105cbe073faa26f08d).

No fixture correction, runtime retry, budget increase or publication followed.
Further diagnostic work is stopped by explicit direction. After read-only assessment,
the technical owner chose design-only work for ordinal-zero pair actual: known player
pressure/setback, two retained signed III parents, ordinaryII+2→IV and ordinaryIII+2→IV
with overflow clamping. Prepare complete v4 authority/C1/cold/C3/C4 and causal TDD;
no gameplay change before independent design acceptance. III+IV parents and explicit
ambiguous-action attribution are separate next scopes. The later-ordinal observations
are not automatic prerequisites; retained DATA still does not prove two births.

Optimization assessment: existing installed assets/cache and immutable retained DATA
were reused, while real owners stayed per-Fact. No gameplay cache/key change was made.
The measured failing preparation does not establish full fixture or observer cost,
transition reuse or speedup. Those costs and behaviors remain UNKNOWN. DATA9, owner18,
genuine5 and older actual20 remain separate earlier lineages, not current regression
passes after this seam change. Two historical births, recovery/start settlement,
pair actual, IV/cap/ambiguous outcomes and full FR-025 remain open.

# T081-D Genuine singleton regression floor accepted, 2026-11-05

Independent semantic and execution OUTCOME accepted the preceding exact owner18
comparison-only result. Its retained-DATA singleton/pair controls, nine ordinary
cases, three reachable freshness cases and four actual guards remain separate
from the five genuine regressions newly verified below. Two real version-transition
cases remain UNREACHED; no complete owner20 or first-atomic34 claim is made.

After independent semantic/execution SOURCE and explicit approval, five exact
PlanOnly leaves selected one Integration Fact each. Independent PlanOnly OUTCOME
accepted their unchanged source/output provenance, exact filters, exclusivity and
zero Fact execution. Five subsequent runtime leaves each executed once and passed:

- Existing-pressure publication: runner25.8464843s, category20.1236837s, outer180s.
  Genuine III birth and guard signed44, ordinaryII to singleton actualIII,
  C1/resources and one ordinary/one actual evaluation, one real cold reopen,
  explicit none/C3, common C4, immutable v3/History and next signed45 read.
- Ordinary-owner: runner23.5135522s, category17.9057382s, outer60s.
  Genuine prior-birth immutable template, fresh independent owners, cold/warm
  comparison, detached DATA and exact raw-input invalidation.
- Other-burden: runner21.2989850s, category15.4887210s, outer60s.
  Genuine applicable pressure-roll refusal and failed-result nonreuse.
- Owner-lifetime: runner23.0447967s, category17.5301719s, outer60s.
  Genuine independent owners, serialized overlap and in-flight/queued disposal
  refusal with released/drained barriers and owned-root cleanup.
- Strain-C2 selection: runner47.0636534s, category39.8146432s, outer60s.
  Genuine same-turn III choice, exactly one owned pressure correction, cold
  rederivation and unpublished completion. This Fact does not prove v1 bytes
  or a historical signed-existing v3 parent; it is the same-turn bypass floor.

All five exact TRX bodies passed; no skip, duplicate, underfill, abort, retry,
budget increase, rebuild or restore. Full recorder walls were27.4187911,
25.2341476,22.8920584,24.6212043,48.7502531s. Installed SDK8.0.427/runtime8.0.33
qualification only. No production, test or catalog source changed in this block.
Independent semantic and execution OUTCOME accepted these bounded observations.
C2 has no per-step stdout: its path is supported by the unchanged fully awaited
Passed body, not an invented runtime event trace.

Each pre/post verified4,969 input pins, including current source/cache/assets and
all twelve DLL/PDBs, with no drift or errors. Runner cleanup flags passed and each
same-namespace census found no new remaining processes. The first four fixtures
assert owned-root absence. The fifth shared disposer is best-effort, so a separately
SOURCE-reviewed read-only pre/post census was added outside the tests: /tmp exact
fixture-prefix roots were empty before and after, no new roots or errors. This
is practical scoped normal-exit evidence, not global or timeout-kill qualification.
All ten plans share fingerprintA802B10324631DA6228D98B53F75DB3B731E6633D0F480EA9BC95F9D7B2BD19D;
the prior owner18 B33 difference was independently reconstructed as only its
three post-runtime status-document updates. These new status notes follow runtime.

Optimization assessment: reuse the already fresh, unchanged six-project builds and
accepted catalog inventory; do not repeat broad verification. Genuine independent
owner setup and explicit cold/publication coverage remain intact. Runtime assertions
prove the named ordinary reuse and failed-result nonreuse only. Existing cold seed
validation remains separate; neither total-query single-validation nor speedup is
claimed. The final C2 leaf stayed within its original60s bound; no limit was raised.

Full artifacts remain outside Git under spiritual-pressure-group-owner-green:
observed-genuine-five-plan-r1.json, genuine-five-plan-preservation-r1.json(36 files),
observed-genuine-five-green-r1.json, genuine-five-green-preservation-r1.json(43 files),
the five plan/runtime labels, and independent-genuine-five SOURCE/PlanOnly/semantic/
execution OUTCOME reports plus the separate fixture-cleanup SOURCE report.
Earlier failures and DATA9/owner18/older actual20 lineages remain unchanged.

Comparison-only scope remains1–2 signed III parents; actual remains singleton/v3.
This genuine singleton floor does not prove two historical births, group actual
aggregation/cap, IV support, ambiguous outcomes, missing version transitions or full
FR-025. The recovery/start infrastructure blocker remains open. No publication.

# T081-D Observed bounded owner18 GREEN; independent OUTCOME next, 2026-11-05

After final independent SOURCE and explicit approval, four exact sequential
Integration leaves executed once each: 2/9/3/4 cases, eighteen unique PASS, zero
failures, skips, duplicates or underfilled selections. No other Fact was selected.
Runner walls: 14.1023126/22.9013825/18.9702022/17.0635590 seconds under unchanged
180/60/60/60-second outer envelopes. Each normal completion was inspected before
the next command; no retry, budget increase, build or restore occurred.

The unchanged retained singleton/pair causal controls now both pass, including
the pair previously RED at its final expected-success assertion. The nine new
ordinary cases, three current-owner freshness cases and four actual guard cases
also pass. Interpretation remains bounded by their SOURCE-reviewed assertions:
complete one/two signed III retained-parent comparison; ordinary/cost/roll consumers,
reuse and actual pre-adapter refusal boundaries. Retained signed DATA is not proof
of two genuine historical C2/C4 births. Actual remains singleton/v3; no group
actual, cap, rank-IV support or ambiguous-outcome implementation is established.

All pre/post source/cache/assets and twelve current DLL/PDB pins stayed unchanged.
Exact plans/TRX test names, test IDs, codebase and counters agree. Runner cleanup
flags passed; each own-namespace census found zero new remaining processes and
zero observation errors. This is scoped normal-exit evidence, not an exercised
timeout or absolute/global continuous-lineage guarantee. All four runtime plans
share fingerprint B33D3A53F8D2C84CB6E3706A4CAF8F9372460CB2D0F0B2140E097A204482CE09;
its change from PlanOnly reflects only the intervening status documents.

Optimization qualification is limited to asserted real batch-reader/ordinary
computation counts and invalidation. The existing cold current-seed validation
still performs separate work; no total-query single-validation or speedup claim.
Two genuine version-transition cases remain UNREACHED, and the separate five-case
genuine singleton actual/cold/C3/C4 regression floor has not been rerun on this core.
The previous twenty actual PASS across two lineages remain historical, not a fresh
aggregate acceptance or a substitute for these remaining gates.

Full runtime evidence is external under owner-green: observed-owner18-green-r1.json,
owner18-green-r1-preservation.json (38 files), execute-owner-leaf-1-r1 through
execute-owner-leaf-4-r1 logs/results/pre/post, and their official plans/summaries/TRX.
Independent semantic and execution OUTCOME review must confirm the bounded claim.
Earlier failures, DATA RED/GREEN and retained causal RED remain preserved. No publication.

# T081-D Catalog discovery and exact owner18 plans passed, 2026-11-05

After independent SOURCE and explicit approval, one bounded official ValidateCatalog
completed: 1,453 categories, 13,448 methods/files, no unmapped or stale selectors.
Runner wall was 12.1140628 seconds under the 60-second outer envelope. It closes
the previously deferred complete catalog-ownership discovery check, not a suite run.

Four separately inspected PlanOnly commands then selected exactly 2/9/3/4 unique
Integration Facts, eighteen total, one exclusive descriptor per leaf, no frontend.
Their runner walls were 5.6185149/5.9171446/5.2778605/5.8762811 seconds under the
unchanged 180/60/60/60-second outer envelopes. All plans share one working-tree
fingerprint and match the frozen concrete names, one discovered case per Fact.

Every command used NoBuild and discovery-only/PlanOnly. Facts executed: zero.
The PlanOnly summaries intentionally show zero completed cases and incomplete
execution; their underfilled execution fields are not semantic failures or passes.
Discovery may execute discovery-host/extensions/theory-data code; no claim that
all test-assembly code remained unloaded or unexecuted is made.

All pre/post source/cache/assets/current twelve DLL/PDB and recorded review pins
matched. Runner cleanup flags passed, and each command's own same-namespace
post-census found zero new remaining processes and zero observation errors.
This is practical scoped evidence, not absolute global/continuous-lineage proof.
Both Unit and Integration build/provenance OUTCOME reviews are independently accepted.
Only discovery/planning is newly complete; all eighteen Fact bodies remain UNRUN.

Evidence is external under owner-green: independent-discovery-owner18-source-review-r1.md,
discovery-owner18-expected-r1.json, observed-discovery-owner18-r1.json,
discovery-owner18-r1-preservation.json (39 files), validate-catalog-owner-single-node-r1
and plan-owner-leaf-1-r1 through plan-owner-leaf-4-r1 logs/results/pre/post records.
Runtime requires separate review/approval and stops after the first failure.
No old20 aggregate, two missing version transitions, native births, actual group,
SDK10/current-restore qualification or publication is implied by these plans.

# T081-D Integration build and six-project provenance passed, 2026-11-05

The previous Unit build/provenance and scoped cleanup received independent OUTCOME
acceptance. A separate narrow Integration SOURCE delta and explicit approval then
permitted exactly one Integration build with the same SDK 8 single-node/no-restore
flags, process-only workload-download opt-out and 175+5 second process envelope.
It exited 0 in 51.2460795 seconds: 115 warnings, 0 errors. Its actual build closure
is Integration plus Client and TestSupport; this is not a second Unit/Bridge/CrashHost build.

All 3,051 source files, 1,851 cache files, six assets and reviewed input pins remain
unchanged. The previous ten Unit-closure DLL/PDB outputs also remain unchanged.
The same-namespace pre/post census found zero new remaining processes and zero
observation errors. This is scoped post-exit evidence, not an absolute global or
continuous lineage guarantee; no runtime/timeout cleanup exercise is claimed.

Read-only six-project provenance exited 0 in 7.7124198 seconds under 55+5 seconds:
twelve retained DLL/PDBs, six CodeView/PDB matches, 3,010 source documents
(2,999 disk plus eleven embedded) and sixteen companion-copy checks passed.
The whole reader exited successfully, including assertions after manifest writes.
Current group production and owner fixtures are compiled, but all 18 owner Facts
remain UNRUN. Discovery/PlanOnly, catalog discovery and any runtime require their
separate bounded approval after this result is reviewed. No tests ran here.

Evidence is external under owner-green: independent-integration-single-node-source-delta-r1.md,
preflight-integration-single-node-r1.json, prepare-integration-single-node-build-r1-result.json/.log,
post-build-integration-single-node-r1.json and provenance-integration-single-node-r1/.
Both earlier preparation failures and the prior Unit result remain preserved.
No fresh restore, SDK 10, semantic GREEN, genuine births, group actual or publication claim.

# T081-D Unit build passed; owner runtime remains pending, 2026-11-05

After independent SOURCE acceptance and explicit approval, exactly one direct
SDK 8.0.427 / runtime 8.0.33 Unit-only build used no-restore, single-node, no node
reuse/shared compiler and the supported process-only workload-download opt-out.
It exited 0 in 85.9039717 seconds under the 175+5 second process envelope:
157 warnings, 0 errors, all five expected project outputs. This is not SDK 10,
current-restore qualification, an Integration build, discovery or test execution.

All 3,051 source files, 1,851 installed-cache files, six assets and reviewed input
pins remain unchanged. Read-only provenance completed in 7.0755065 seconds:
ten DLL/PDB copies, all five CodeView/PDB matches, 2,054 source documents
(2,053 disk plus one embedded) and twelve companion-copy checks passed.

The pre/post census ran within the same execution PID namespace, with the same
two baseline PID/start-time identities, zero new remaining processes and zero
observation errors. It does not assert a global census of other executions or
continuous process lineage. Independent outcome/cleanup sufficiency review is
pending. No subsequent discovery, Fact runtime, retry or publication occurred.

Evidence is external under owner-green: independent-single-node-no-restore-source-review.md,
preflight-single-node-r3.json, prepare-unit-single-node-build-r3-result.json/.log,
post-build-single-node-r3.json and provenance-single-node-r3/. The previous
metadata and worker-IPC stops remain separately preserved. Next gates still need
approval: Integration preparation/provenance, exact discovery/PlanOnly, then the
bounded 18 owner Facts. Retained DATA is not genuine births; actual stays singleton.

# T081-D MSBuild worker IPC blocker identified; no retry, 2026-11-05

Read-only diagnosis preserved30 exact-time MSBuild node crash files. Each records
SocketException13 Permission denied while constructing NamedPipeServerStream for
an OutOfProcNode. Runner Parallelism1 governs test descriptors, not build nodes;
the normal build omitted the explicit -m:1 used by earlier successful builds.
This establishes worker IPC startup denial, not a source/compiler or package defect.

Parent requests independent SOURCE confirmation that standard single-node mode
avoids requesting this denied operation, plus assets currency for no-restore and
an exact bounded proposal before action. That review is underway. The candidate
external command adapter is plain non-executable source only; no PATH/toolchain/
SDK/sandbox/permission change, build, restore or test retry has occurred. See
external owner-green/read-only-diagnosis.md and msbuild-node-failures-r2/manifest.json.
All earlier stops/results and unbuilt/unrun group-stage boundaries remain intact.

# T081-D corrected preparation stopped at Build-unit, 2026-11-05

Official ListCategories now passes1453 categories after the independently accepted
relation-object correction. The one authorized corrected preparation then stopped:
Build-unit exited1 in1.2994271s; runner wall1.9671549s/180s, wrapper3.3550703s.
Output says only “Determining projects to restore…” then “Build FAILED” with
0 warnings/0 errors. Discovery and all owner Facts are UNREACHED; compiler/source
failure and restore root cause are not established by this opaque output.
OwnedTreeCleanupSucceeded and RuntimeCleanupSucceeded are both true, no timeout.

The execution chain stopped immediately as directed. No Integration build,
provenance capture, global catalog discovery, leaf runtime, installation or retry
followed. All6 project.assets files,1851 installed-cache files,3051 project source
files and the ten prior DATA DLL/PDB files remain unchanged. Existing source is
independently accepted but still unbuilt/unrun for the group owner stage.

Parent permits only read-only logs/runner/installed-assets diagnosis now. Exact-time
MSBuild node failure files were found under /tmp and are being examined/preserved;
no cause is claimed yet. Any new verification action requires materially new
findings and approval first. Evidence: external owner-green/observed-preparation-stop-r2.json,
prepare-unit-r2-result.json, runner-owner-r1/.../dotnet-test.log and summary.json.
The r1 metadata failure, DATA9 GREEN and earlier lineages remain separately preserved.
No owner18/20 GREEN, actual aggregation or publication is claimed.

# T081-D metadata preparation stopped before build, 2026-11-05

Integrated core/fixture/assertion SOURCE and the limited18-case plan were accepted.
The first official Unit PlanOnly preparation exited1 in1.509405s while loading the
catalog: three new related arrays supplied strings instead of {id,when} objects.
No build, restore, discovery, test Fact or owned runner result directory was reached.
The failure is preserved as metadata preparation, never semantic RED.

Only six relation entries were corrected; category IDs,18-case membership, bounds,
code and fixtures are unchanged. Full static catalog schema audit passed; corrected
SOURCE review and explicit bounded-repeat approval are pending. All3051 project
source-closure files and1851 installed-cache files remain unchanged. No automatic
retry occurred. Evidence: external owner-green/observed-preparation-r1.json and
category-related-correction/. Current catalog SHA256:
c386316e837cf6c5b63cef506e03c447c852752d0b33ea142b7af9a8c083a146.

The coordinator explicitly permits only the installed SDK8.0.427/runtime8.0.33,
not SDK10 qualification or installation. R2 process-only empty offline feed and
normal incremental runner builds are independently accepted; source/PDB/current
binary identities must still be verified. Archive/content-hash differences in the
recovered NuGet cache are disclosed, not repaired or called upstream-equivalent.
The next approved attempt must start with cheap metadata listing before builds.
No owner GREEN, actual aggregation,20-case completion or publication is claimed.
Following entries are historical checkpoints.

# T081-D group core SOURCE frozen; runtime pending, 2026-11-05

The approved comparison-only atomic implementation is written: eight production
files and two new assertion files, with exact inherited before-images retained.
Full group capture/isolation and one homogeneous ordinary DATA core are separated
from a fresh private singleton actual basis; wire/v3/History and broader rules are
unchanged. The narrow opt-in pre-revoke historical observation follows its accepted
SOURCE design. Final integrated independent SOURCE review is underway. Nothing in
this owner stage has been built, discovered or executed yet.

Owner source has exactly18 prepared Facts (existing2, ordinary9, freshness3,
actualguards4), not20. Holistic fixture SOURCE found the IV preparation defect;
the complete four-slot graph and both IV cost magnitudes3 were corrected and
independently accepted before any run. Two genuine version transitions remain
UNREACHED, and five genuine actual/cold/C3/C4 regressions remain a later stage.
The existing r3 causal RED and DATA9 GREEN are preserved independently.

Optimization: shared current/signed batch counters measure batch-reader work only.
The cold current wound seed still performs its preexisting complete validation;
its detached DTO exports no reusable validated proof. Do not claim total current
validation1, physical-IO reduction or speedup. Every row/availability is retained
in dependency snapshots; graph facts are recomputed once per source group inside
each locked snapshot, with no new long-lived cache. Warm group reuse remains
subject to fresh complete owner/input/dependency checks.

External evidence spiritual-pressure-group-owner-green: atomic-core.diff SHA256
c74adebfc32c6717e62920604acf3324ac065240575dddaafc0db6ebba3887b6;
atomic-core-source-manifest.json and atomic-core-source-preflight.md; exact
four-leaf plans2/9/3/4, exclusive Integration, existing180/60/60/60s bounds. Fresh
Unit+Integration builds/provenance and full discovery-only catalog/PlanOnly must
follow SOURCE acceptance before runtime. Stop on unexpected failure; no automatic
retry or budget increase. No actual aggregation, native births, full FR025,
publication or remote recovery claim. Following entries are historical stages.

# T081-D bounded DATA GREEN independently accepted, 2026-11-05

The unchanged nine DATA Facts now pass after the minimal one-view reader refactor:
9/9 unique PASS, no skipped/missing/duplicate cases, runner 9.1929920s under 60s,
complete owned cleanup. The final pair assertion proves the full per-basis counts
[1,1,1,1,2] after complete semantic, singular-parity and nonmutation premises.
This qualifies catalog/identity/History/agreement computation reuse and two exact
parent resolutions, not physical IO, speedup, live group comparison or authority.

Fresh five-project Unit closure build passed in 82.453637s under 180s (87 warnings,
0 errors); all ten binary/PDB identities, 2,054 document proofs, 12 companion copies,
2,112 source files and five pins were independently reconciled. Exact discovery/
PlanOnly/runtime selection and revision agree. All 58 baseline RED artifacts and
204 older retained-owner artifacts remain unchanged; GREEN has a separate 50-file
preservation manifest. Full global catalog validation remains deferred until the
owner source/category freeze, not claimed complete.

Reader SHA256: f9fb5d3b8a94538d0245c6a283e6ba7f6e12fa8875cf95fd415da40731979649.
GREEN TRX SHA256: 78a6a27415bfbd202ae8e669f66ee554ebd6522c2273ac6148c3a9c2d9448329.
Independent OUTCOME SHA256: 5b7ca20e5f1d5f873dbc338648add3dd5b6406561d5e39cc4ea919e371e3e825.
Evidence: external spiritual-pressure-group-view-data, observed-green-r1.json and
independent-outcome-green-r1.md. Owning-runner exit/cleanup is the process evidence;
a separate shell process snapshot cannot establish global process absence.

Next is the already approved atomic group ordinary core/private singleton actual
adapter, not actual aggregation. Its retained owner source is 18 prepared cases;
two real version-transition cases remain separately UNREACHED. The actual-refusal
historical observation seam receives independent design review before editing.
No group core implementation/runtime, two genuine births, broader accepted-rule
completion, publication or remote recovery is established by this DATA block.
The entries below are historical checkpoints at their stated stage.

# T081-D DATA count RED accepted; one-view SOURCE pending, 2026-11-05

Independent OUTCOME accepted the exact nine-case baseline: 8 PASS and final count
RED after semantic prerequisites. Source/provenance, all 2,112 closure files,
204 prior artifacts, the 60-second bound and owned cleanup were independently
verified. Catalog count 2 versus required 1 is observed; the unprinted counter
suffix, physical IO and speedup are not claimed.

The minimal one-view reader extraction is prepared for independent SOURCE only.
Nine tests/helper and live group/actual consumers remain unchanged. A fresh build,
full selected closure provenance and exact nine-case PlanOnly must precede GREEN;
the existing binaries still correspond to the old baseline, not optimized source.
No second runtime has occurred. Baseline evidence has its own preservation manifest.

Accepted OUTCOME SHA256:
915b420b964c021c012271baff5203878f5b58d3319a6c907b4efd21f15bcd5d.
Proposed optimized reader SHA256:
cc07e75744145ffc4b944d241b697c9ce4b0fce054bfa4a28b911033af3b199d.

# T081-D DATA baseline count RED observed; independent OUTCOME pending, 2026-11-05

The independently SOURCE-accepted cheap baseline ran exactly nine Unit Facts:
8 PASS and one FAIL only at the final pair source-count assertion (line 30), after
complete current/signed-shaped pair DATA, singular parity and nonmutation checks.
The displayed actual count prefix is [2,2,2,2,2,...], versus [1,1,1,1,2,...], with
first mismatch at catalog builds. Do not claim an instrumented full ten-value trace.
This is repeated DATA-computation RED, not owner-group or actual functionality.

Fresh Unit build with all normal project references completed in 82.228751s under
180s, 87 warnings/0 errors. Five-project DLL/PDB/CodeView provenance checked 2,054
source documents (2,053 disk/1 embedded), dependency copies and 2,112 unchanged
source-closure files. Exact selected discovery/PlanOnly reconciled 9 methods/cases.
One runner invocation completed in 9.382426s (wrapper 10.912669s), under 60s, with
no timeout, skip, duplicate or missing case and complete owned-tree cleanup.
Full global ValidateCatalog is deferred until Integration owner preparation freezes;
no global inventory or broad regression claim is made. All 204 prior accepted r3
artifacts remain unchanged. No actual/v3 consumer was changed or executed here.

TRX SHA256: 9c2d1be7290c60eb61fa30ee7c2e4bf7dea4728d141cda839eff87e8307855ab.
SOURCE report SHA256: a73c5ff6df5ea51ed3e3df5cc6322170ce1f6c786c9b44714511e09f4bdf8650.
Evidence: spiritual-pressure-group-view-data outside Git; source-baseline-r1,
observed-baseline-r1.json, build/provenance/PlanOnly/runtime retained separately.
Next: independent OUTCOME, then minimal one-view extraction and separate SOURCE
before bounded GREEN. Optimization observed only redundant source computation;
no runtime speedup, physical-IO reduction or long-lived cache claim.

Parallel Integration preparation has 16 new existing-API Facts plus the unchanged
r3 pair/control: 18 prepared, not executed. Two genuine version-transition rows
remain UNREACHED at ordinal0; independent SOURCE identifies a possible earlier
admission/later-ordinal fixture, requiring separate preparation. Correct future
oracle is stale-result nonreuse, not necessarily refusal. Actual refusal inventory
observation and new group counters also remain unimplemented proof obligations.
No framework, birth-provenance, group actual or publication extension.

# T081-D comparison-only implementation authorized; first TDD source pending, 2026-11-05

After the independent design and documentary acceptance, the technical owner
approved 1–2 signed III parents, one complete ordinary evaluation, exact group
isolation, with singleton actual/v3 unchanged. Start the 9 DATA and 20 current-owner
stages; independent SOURCE and an exact bounded plan/evidence precede every runtime.

First preparation: nine cheap typed DATA cases and a minimal real batch adapter
that delegates to existing singular checks. Observational per-call counters count
actual catalog/identity/History work and parent resolutions. The intended cheap
RED is repeated complete view construction after correct pair/parity validation;
it is not a fake implementation, missing API compile error or live group support.
Optimization and owner consumer changes follow their relevant reached proof.
The owner fixtures undergo one coherent source preflight before execution, retaining
all r3 constraints and independent authored expected DATA. All new checks remain
UNRUN. No publication, arbitrary-rank/count or actual aggregation scope expansion.

Design/documentary report SHA256:
d2ad685f82e59866b3555ab8d8a191366bf1b5ddb106741c04b2be52dab5ed3a.
The previous accepted closeout hashes below describe the completed design stage;
this newer authorization changes only the next permitted work.

# T081-D group comparison: independent SOURCE accepted, 2026-11-05

The independent SOURCE review accepted the [comparison-only group design](existing-pressure-group-comparison-design.md).
It preserves the existing query API, validates the full one/two-III parent set,
isolates only its exact strain rows in one ordinary probe, and separates a fresh
private singleton actual basis before ordinary DATA reuse. The legacy foreign
pressure-wound, hypothetical +1/rank and positive-ceiling guards remain closed.
No production/test/schema/category change, build, discovery, runtime or publication
was performed in this design block. Technical-owner approval is still required.

The final proposal includes full current/signed graph and global identity/History
validation, a shared validated DATA batch, complete owner/input/dependency identity,
whole retained-selection invalidation and unchanged fresh physical/source fences.
Review added exact/confusable identity coverage and an independent actual-max0
refusal oracle; ordinary maximum0 with positive actual remains a separate control.
The authoritative resilience input and singular/batch parity assertions are explicit.

Causal/TDD proposal: preserve the qualified r3 pair RED; add only SOURCE-reviewed
cases and use three separately approved stages: DATA 9, current-owner 20, genuine 5.
The 34 cases and ten leaves / 14 minutes protective ceilings are a plan, not execution
permission or a new PASS. Ten additional rows remain explicitly unqualified.
The genuine publication Fact supplies actual/cold/C3/C4/next-signed regression;
there is no automatic old 20, History matrix or entire-family rerun.

Optimization is design-only: existing operation-local cache, one ordinary probe,
one current and one signed validated view per stable window, separate view-build/
parent-resolution counts, fresh checks and defensive copies. Costs and reuse
improvement remain UNRUN/UNKNOWN. No runtime cache or speedup has been implemented.
All 204 accepted r3 evidence files, 1,210 production/policy baseline files and r5
code/test/selection pins were rechecked unchanged before closeout. Existing WIP,
failed prerequisites, retained-DATA limits and the recovery/start blocker remain.

Design SHA256: a020c4dceb2f60386cf015da1fa5d5964ec6749f5c38baa8630b83b3942783ba.
Independent review: spiritual-pressure-group-comparison-design/independent-design-review.md
outside Git; the original architecture freeze was fd2e877dcc9f5bdc6795494126613a51a6575aa798a321a35f04bea6dd42a3db.
Only the design proposal and compact existing task records changed. No publication.

# T081-D retained-DATA current-owner causal RED accepted — 2026-11-05

The unified preflight r5 passed independent whole-fixture SOURCE review. One
authorized r3 execution reached the intended functional boundary: **singleton
1 PASS; pair 1 FAIL only at the final expected-success assertion**. Both completed
independent authored/canonical DATA preflight, signed/current byte/absence checks,
full creation/receipt/History joins, complete effect graphs and real current
intake/resource/closed-prefix owners before querying the ordinary comparison.

The pair returned spiritual_pressure_ordinary_contour_unsupported with exact detail
"one signed current rank-III pressure strain contribution". Its query count rose
once, with no ordinary evaluator or signed-parent-read delta; the singleton's
ordinary evaluator and signed-parent-read deltas were each one. Both retained
unchanged actual-evaluation count, full frontiers/allocations/files/generation,
post-capture inertness and owned cleanup. Source branch analysis plus the singleton
control and complete contribution checks support the cardinality attribution;
the generic diagnostic alone is not an instrumented branch trace.

Independent OUTCOME accepted this bounded retained-DATA current-owner causal RED.
Exactly 2/2 unique cases completed, no skip/duplicate/timeout or underfill; runner
wall 14.2943851 seconds, wrapper 15.7699443 seconds under unchanged 180 seconds.
Fresh successful Integration/Unit build invocations took 6.867814 / 1.791053 seconds
(23 / 0 warnings, zero errors; incremental work, not a clean rebuild claim).
All 2,982 PDB documents, eight binary/PDB hashes and companion copies reconciled.
All eight r5 package pins, 1,210 production/policy files and 156 earlier evidence
files remained unchanged at post-runtime verification. Cleanup left the same 42
temporary roots, no additions/removals. No code changed after the accepted run.

TRX SHA256: c8b1ee752e4f4ab7c819f5fb2ac3c2fb3eaf71a63363124547bf317adf175b5a.
SOURCE review SHA256: d7d5890a721a09882405b7b25e4938f010b78994f70a902abc67cd3bc905914a.
Evidence: spiritual-pressure-group-owner-red outside Git, including all r1/r2
preparation failures and the separate r3 source/build/run lineage.

This is not pair comparison GREEN, group actual aggregation, cap/overflow,
ambiguous attribution, C2/C4/native ancestry or a broad regression pass. Prior
20 unique PASS remain separate: 19 corrected pressure cases plus one unchanged
maneuver across two older lineages. The three genuine-pair prerequisite failures
and recovery/start whole-turn settlement blocker remain open and preserved.

Next: independently design comparison-only group support, including full parent
isolation/dependencies, the shared actual reader and hypothetical+1 evaluator,
while retaining the current singleton actual/v3 guard. No production/schema
change or further runtime is authorized merely by this RED result.

Optimization: complete current signing/intake/prefix/query now ran in each
independent fixture. Query measurements were 259.9726 ms singleton / 55.8876 ms
pair refusal; these are different paths, not a speedup comparison. Stable parsed
DATA reuse remains operation-local; current ownership checks stayed fresh.
No new cache, shared mutable fixture or performance claim; expectedSeconds stays
null and the protective budget remains 180 seconds.

# T081-D retained-DATA r2: stop and audit the whole path — historical, 2026-11-05

**Next bounded block authorized after the whole-path audit:** prepare only the
unified fixture representation/preflight correction, freeze and independently
review the complete package, then execute the same two Facts once under 180 seconds.
Any new refusal before the real owner stops this contour without another correction
or retry. Production/schema and native ancestry remain outside this authorization.

The one separately authorized retry used SOURCE-accepted r4: two valid empty
Mortal effect carriers seeded only before signing, guarded against overwriting an
existing carrier, with exact signed/current empty-root assertions. Fresh Integration
and Unit builds succeeded (52.379942 / 1.852310 seconds; 109 / 0 warnings, zero
errors). Full four-project provenance and exact two-Fact PlanOnly passed.

Observed r2: **2 FAIL / 0 PASS**, complete two-case execution, runner wall
11.3252683 seconds and wrapper 12.7345847 seconds within the same 180-second limit.
No timeout, skipped or duplicate case. Both passed the earlier signed-carrier gate,
then failed line 222 comparing authored parent JSON with canonical serialization.
The parent catalog/identity/History/receipt parse and agreement checks before that
line passed. Per-parent first-create joins, full effect graph, actual intake/prefix
and comparison remained unreached. This is still preparation failure, not causal RED.

Both fixture cleanups and runner cleanup completed; the same 42 temporary roots
remained with no additions/removals. All eight r4 source pins, 1,210 production/policy
files and all 96 earlier evidence files were verified unchanged after execution.
R2 TRX SHA256: 031b85601d7032363e2e34b580091a844a7ad2f4f8780c020d1cf950922ebbef.
R4 SOURCE review SHA256: 6bfa2c7aa34870caeaf505b5c8c07c077e203ef30cce59ff4595a29e27347063.

Independent SOURCE traces the representation mismatch to two omitted optional
recovery anchors: parsing maps absence to null, while canonical serialization emits
both null properties. This explains why semantic History agreement can pass while
raw/canonical equality fails. It is not a production or group-semantics defect.

The owner explicitly stopped one-error-at-a-time corrections and runs. The fresh
independent read-only audit is now complete: full parent field correspondence,
retained creation/receipt/History joins, graph/root/sibling authority, signed/current
coverage, ordinary math/resources and genuine intake/prefix/query were traced. It
found no second definite SOURCE obstruction or missing continuing owner. This does
not establish any unreached runtime pass or revive the recovery/start branch.

One cohesive proposed fixture block authors both null anchors locally before the
first parse/hash and adds complete-envelope fail-fast equality at that existing parse.
Expected remains independently authored DATA, never replaced by actual serialization;
the later signed equality and every graph/owner/counter/inertness oracle stay intact.
Freeze and independently review the complete resulting fixture before any new explicit
one-lineage/two-Fact/180-second execution. The later explicit authorization above
permits only one attempt after SOURCE acceptance; new runtime has not begun.
Audit report is retained outside Git as
spiritual-pressure-group-owner-red/holistic-retained-owner-source-audit-20261105.md.
Production/schema and the actual guard
remain unchanged. Prior failures and the earlier 20 unique single-parent PASS stay
separate; genuine two-birth ancestry and whole-turn settlement remain open.

Optimization: r2 measured only early setup and failure cleanup, not the intended
owner/query operation. Repeated physical preparation is not functional progress or
speedup evidence. The whole-path audit must prefer existing valid DATA/service seams
and eliminate avoidable repeat construction without substituting cached permission.

# T081-D retained-DATA consumer run r1: preparation failure — 2026-11-05

Independent SOURCE accepted the exact two-Fact test package, with only three XML
summary comments added before execution. Fresh Integration/Unit builds succeeded
in 56.410 / 25.313 seconds with 132 / 27 warnings and zero errors; no warnings named
the two added files. All 2,982 PDB documents (2,971 disk and 11 embedded), CodeView
identities and Main/Support test-bin copies matched. Discovery found 1,449 categories
and 13,423 methods/files without executing tests. PlanOnly selected exactly two Facts.

One authorized exclusive run: **2 FAIL / 0 PASS**, runner wall 11.2633603 seconds,
inner category 5.679676 seconds, wrapper 12.5889014 seconds, unchanged 180-second bound, no timeout/skip/duplicate
or underfill. Both Facts failed signed-reader preparation at line 180 for absent
game_state/player/effects.json and game_state/npcs/npc_effects.json. Signed byte/
absence coverage was missing. Graph checks, ordinary input checks, actual intake,
prefix and comparison were not reached; this is not causal RED or aggregation proof.
Both owned fixture cleanups and runner cleanup completed; the same 42 prior boe
temporary roots remained, with no additions/removals. Accepted source pins and all
1,210 production/policy files remained unchanged after the run.

Read-only root-cause review supports seeding valid empty Player/NPC effect carriers
before the existing signer. They already belong to the normal tracked set; adding
paths alone cannot sign absent files. Prepare only that fixture correction, retain
all stronger signed/current/graph checks, and review it independently before any
new bounded-run decision. No production/presence-schema change or automatic retry.
Source r1, builds, provenance, TRX and cleanup evidence remain preserved under
local-evidence/wound-history-6007-20261105/spiritual-pressure-group-owner-red outside
Git. Source review SHA256: 5958cf21c317808f43681b57ddd37d1d87171884f8646121e7b9a155669af16a.
TRX SHA256: a4905a80415bc9184039351d64d5bb58d1a83ef81e37d09f712e429b8fe58349.

Optimization: independent small DATA fixtures retain real current signing and
owned cleanup. Measured preparation was 845.891 / 618.533 ms, cleanup 1.0722 /
0.4836 ms for pair/singleton; later owner/query cost is still unmeasured. No cache,
source-read reduction or speedup claim. Prior 20 unique single-parent PASS across
two lineages, three genuine-fixture failures and the open recovery/start blocker
remain separate. No publication, group actual support, cap or ambiguity coverage.

# T081-D retained-DATA consumer-first RED preparation — historical, 2026-11-05

Read-only recovery/start design gates are complete. Independent SOURCE review found
no proven existing automatic whole-turn settlement owner at the lawful pre-story
cold cut: signed backup authentication and current generation do not prove the
complete current footprint, no-later-story condition, exact restore and correlated
cleanup. Generic rollback DATA and another family's private owner cannot substitute.
Clean completion and inner narrative/interface repair remain distinct from that cut.
The recovery/start infrastructure blocker stays open; no replacement framework is
being implemented.

The technical owner approved the smaller consumer-first proof order. Immediate
work is test-only: two explicitly retained-DATA parent graphs with complete signed/
current identity, History and independent first-create receipt joins; real current
intake/resource/effect/prefix owners; singleton success and a pair expected-success
causal RED at the singular contribution guard. No test source/build/runtime has yet
been qualified. Missing preparation is not semantic RED. Independent SOURCE and an
exact bounded selection precede execution; a separately reviewed design precedes
production. Group actual/v3, cap/overflow and ambiguous attribution remain closed.

The former three prerequisite1FAIL runs remain separate. They reached first genuine
III C2/terminal C4 twice, never the second start/parent. The prior20 unique PASS retain
their two source/build/run lineages, not a new aggregate. Retained DATA signed now
does not prove historical C2/C4 births or cold ancestry; genuine two-birth/native
coverage remains mandatory and blocked.

Local evidence: `local-evidence/wound-history-6007-20261105/` outside the checkout,
with immutable prior `spiritual-pressure-multiple-fixture` inventories and new
`spiritual-pressure-group-owner-red` preparation. Policy/production1210, prior
fixture197 and design5 files were rehashed unchanged before this test-only block.
No production/schema change, publication or fresh runtime result. Optimization
scope is minimal per-case DATA plus actual small owners, not repeated births or
shared mutable fixtures; future group reuse must include both parents and retain
fresh authority checks. Costs/source-count improvements are UNMEASURED.

# Local physical course: terminal healing verified — 2026-11-03

On source base `470cab31e9b6c2e9487775490a0ba567d2ee5e4c`, the existing
three-milestone browser queue/handler and Engine journey passed twice locally
under a five-minute diagnostic bound. The second run adds an explicit
fixture-specific assertion that the completed course prepares terminal healing;
the existing assertions then verify terminal History and removal of the active
wound. Its category passed 1/1 in 261.6183978 seconds, with complete runner and
fixture cleanup. The first category run passed in 263.2112315 seconds without
the new assertion. The shared helper also applies the assertion to console
courses; that branch was not rerun here.

Both runs retain the original gameplay actions, confirmations, waits and oracles.
Detailed filesystem timing hooks were disabled for this course; journey-level
timings and logs remain. The five-minute category bound reflects the observed
cost of this indivisible scenario, not a production optimization. Earlier
180-second timeouts remain UNKNOWN, and the old three-minute bound was not met.
This is not a real Firefox/DOM, external provider, fresh-process or whole-task
acceptance. No gameplay production code changed.

The second run's TRX SHA-256 is
`5a0301aaa87a8ddc338bb7c999978c34efaf6ca34b8ce9a40350b82b71d6a2bf`;
summary SHA-256 is
`8676edc82dd8aaf6ca99f9bf4c41110fecc5671d4c681f47a8b78bb77b46947a`.
Raw evidence remains in `/tmp/boe-local-r2-terminal` outside the checkout.

# T154 mixed item/resource rollback7 — accepted, 2026-11-03

Runtime/source: `25c5ae58f6411214cea03408aa5e1f6b079fe63f`.
Exact existing method GuaranteedMixedItemAndResourceConsumption_EveryPipelineFailureRestoresExpandedRootsAndExactRetryCommitsOnce:
**7/7 PASS**, category **141.3249038 s / original 600 s**, wrapper 147.84649864 s.
All seven original cuts passed: runtime_refresh1/2, wound_post_seal1, wound_output1,
critical_validation1, full_state_validation1 and cleanup1. No timeout, skip, duplicate
or underfill; complete capture and owned/runtime cleanup verified, no new boe roots
or managed owners. All 680 artifact/resource/native records remain unchanged.

Cause was established by one diagnostic exact7 run at
`c74ac9d9641f5ba9fb613d2513182adeacb41960`: 5 PASS / 2 FAIL in115.6593501 s.
The existing per-test logger exposed Xunit.Sdk.SameException in the fixture's
OnPublished -> ObserveOriginalReceipt callback: after verified rollback/rearm,
retry legitimately obtained a new take receipt, while the old one-shot observer
still asserted the first receipt's identity. It aborted npc_core.json publication
and then wound_commands.json compensation, yielding the secondary
mortal_wound_treatment_publication_terminal_release_failed. No production defect
or gameplay-rule change was needed. Both original failure captures remain intact.

The minimal test-only fix calls CompleteVerifiedFailureAttempt only after every
existing byte/existence/count/same-plan/binding/ConfirmedHeld check and lease release,
immediately before retry. It asserts Fired, then clears _armed. Shared callbacks,
first-attempt compensation observation, all other consumers, seven InlineData rows
and original Accepted/once-only expense predicates are unchanged. The existing
logger/Describe assertion message is retained for future failures.

Fresh Integration build at25c5ae58f: exit0, 71.757021533 s, 107 existing warnings /
0 errors; exact selected source and PE/PDB binding verified. Unchanged Main/Support
builds f54481d85 reused. Original compiled outputs and the diagnostic DLL/PDB delta
were preserved with full decode/CRC/member-hash proof before overwrite. Per-case
mutable roots and fresh authority checks remain; no cache, new diagnostic framework,
or speedup claim. No GM/game contract changed, so no GM wording update was required.
Independent Sol 6.1 / xhigh causal, applied SOURCE and final OUTCOME accepted.

Official runner cannot select individual Theory rows; the author authorized one
minimal supported diagnostic7 and the causal fix/confirmation. PlanOnly confirmed
one method/seven cases/exclusive/no frontend/original600s. Confirmation command:

```text
pwsh -NoProfile -File scripts/test-csharp.ps1 -SelectionFile /workspace/tboe-linux-evidence-20261103/t154-mixed-consumption-diagnostics/selection-fixed7.json -NoBuild -Parallelism 1
```

Full raw, causal exceptions, prior FAILs and verified backups remain outside checkout:
`/workspace/tboe-linux-evidence-20261103/t154-mixed-consumption-diagnostics`.

- Fixed post-proof: `071ea20fee86db5c9c3bc421cf124afb27c84cebcd7f54b5f80e7bf55d2e9b17`.
- Summary: `dfad88e419f7055ea6152d3483053c40f85752a0846ee0b580de8e27dfd164ce`.
- TRX: `483fdff9d1266d95d89e64b51981549782cf1d7b79add3ecbeaf19d92fd178d9`.
- Fixed artifact proof: `5ecdd0e3afa59d681286c353552acff2e663c9d6334725a0efe37e14d1867a61`.
- Diagnostic FAIL post-proof: `25fa3539fb5e7f7b0544b0f9664a9b1b45741863bc25e2ee96706d4ccd4c280d`.

This accepts the existing held guaranteed-treatment repair-loop route: exact expanded
raw-root bytes/absence on failure, unchanged expense counts and original authority
rearm, then one Accepted retry with item/energy/capacity transitions each +1, selected
item count1 and durability4/4. Private images are asserted in-process, not emitted as
a separate complete path-image dump. This is not UI/DOM/provider, fresh-process or
Windows qualification. Original rollback24 and free diagnosis e70510c4 remain separate;
no broad turn-lifecycle-repair or accepted rollback24 was repeated. Whole T154/#1536
and unrelated UNKNOWN/HOLD remain open.

# T154: mixed item/resource rollback7 — 5 PASS / 2 FAIL, 2026-11-03

Verification-only runtime source: `6d926c971281d30376f0d9869eb65162be5a29b0`.
Production and test sources are unchanged from requested base
`67f8e046443cd7996ac05037ba071693a5bb9fdc`. Only the narrow catalog/selection
and checkpoint changed; no gameplay, test, probe or timeout was modified.

No exact current receipt was found in the retained 198 TRX, 286 summaries and
230 plans under the 20261102/20261103 evidence roots. Historical B.4 aggregate
acceptance does not prove these current seven cases. The separate free-diagnosis
receipt at e70510c4 proves no real item/resource spend rollback.

Added `mortal-wound-mixed-consumption-rollback` as a duplicate narrow catalog owner
of the existing GuaranteedMixedItemAndResourceConsumption_EveryPipelineFailureRestoresExpandedRootsAndExactRetryCommitsOnce method.
Catalog validation: 1405 categories / 13303 methods, no unmapped/stale selectors.
PlanOnly: one method, seven cases, exclusive, no frontend, original 600-second
category budget; default overall preparation allowance 900 seconds, no override.
The broad `turn-lifecycle-repair` category and accepted original rollback24 did not run.

Fresh successful compiled sources remain Main/Support `f54481d85` and Integration
`06dc53214`; their source deltas are empty. Selected Integration PE/PDB identity
and source SHA256 `cbabcf4868adaa735471a2f3e26823f7d31d2b8a2a6035ee61bd36c9c9d03915`
match current source. All 50 managed files, 624 resource copies and six native copies
match before/after. No rebuild was needed. Each unchanged case retains an independent
mutable fixture and fresh authority checks; no cache or speedup claim. No GM contract
changed, so no GM wording update was required.

One exact execution:

```text
pwsh -NoProfile -File scripts/test-csharp.ps1 -SelectionFile /workspace/tboe-linux-evidence-20261103/t154-mixed-consumption7/selection.json -NoBuild -Parallelism 1
```

Result: **7 executed, 5 PASS / 2 FAIL**, category **124.3477501 s / 600 s**,
wrapper 131.504762874 s, exit 1. No timeout, skip, duplicate or underfill.

| Existing fault cut | Outcome |
| --- | --- |
| runtime_refresh, occurrence 1 | PASS |
| wound_post_seal, occurrence 1 | PASS |
| wound_output, occurrence 1 | PASS |
| critical_validation, occurrence 1 | PASS |
| full_state_validation, occurrence 1 | FAIL at retry |
| cleanup, occurrence 1 | FAIL at retry |
| runtime_refresh, occurrence 2 | PASS |

Both failures reach line 556: retry expected `Accepted`, actual `TerminalRejected`.
All seven passed the preceding fault-fired/first-error checks, exact expanded-root
byte/existence restoration, unchanged item-consume/energy/durability-capacity counts,
and original plan/binding/live ConfirmedHeld rearm. The five passing cases then
prove one Accepted retry, each count +1, remaining selected item count 1 and durability
4/4. The two failed cases do not prove those final retry assertions. The unchanged
test asserts private images in-process; it does not separately emit all path images.
Existing NullLogger output retains no unique inner rejection issue, so the cause
cannot yet be classified as production or fixture failure. No speculative fix or rerun.

SOURCE/PREP and final OUTCOME accepted by independent Sol 6.1 / xhigh as a
complete failing run (5 PASS / 2 FAIL), not acceptance of retry7. Final compact
documentation review also accepted at e8b526e2b. Owned/runtime cleanup succeeded; no new boe temp
roots or managed owners remain, all artifact hashes are unchanged. Full raw is outside
checkout, without quotas/truncation:
`/workspace/tboe-linux-evidence-20261103/t154-mixed-consumption7`.

- Post-proof: `5285f75200e1c12b242656d5ade7c50b4c48349e9c292a9234ce1c036693fe6a`.
- Summary: `fa10249663e891c30a68efb10d4c94d40f29148e64818710030795deb81b253d`.
- TRX: `a189f999fbb79cc5fe066a280bc5778d204f6aea1ffca3975d2c2e87e3d2a838`.

This is a verified failing seven-case result, not accepted rollback/retry7. Whole
T154/#1536 remains open; prior rollback24 and all unrelated UNKNOWN/HOLD retain their
separate status. No UI/DOM/provider, fresh-process, Windows or broad lifecycle claim.

# T154: original rollback24 accepted on Linux — 2026-11-03

This was verification only: no production code, tests, probes or catalog changed.
Runtime source: `4739060a8e48a91adb0a5104c9f4d3892efe2cdc`.

Historical receipt reconciliation found the accepted post-probe F18 run at
`0d16af64926ded3a1492aff1a3648a2d895ff764`: 24 wound cases plus one separate
offscreen case. Acceptance is recorded at `8c49f44fc`, reviewing evidence
`bd496ce6`. Both test and KnownRollbackPublicationCut blobs match the current
versions. Independent Sol 6.1 / xhigh verified the retained 96 wound records;
old raw TRX/log archives were removed under retention. The current run was still
needed: exercised snapshot capture/restore now includes
RequireExactEngineSnapshotInventory and
RequireNoMortalJointInterfaceRestoreAsync(blockSelectedMissing: true).

Current environment: SDK 10.0.402, PowerShell 7.6.8. Selected sources match fresh
successful builds: Main/Support at `f54481d85`, Integration at `06dc53214`.
PE/PDB identity and both exact test/probe source bindings were checked. No rebuild
was needed. All 50 managed files, 624 resource copies and 6 native copies remained
unchanged before/after the run. Each case retained its own mutable fixture/owners;
no new cache, source reuse permission or measured speedup is claimed.

Exact selection: `portable-wound-original-rollback-publication` only.
PlanOnly: one method, 24 cases, exclusive, original 120-second category budget.
Executed once through:

```text
pwsh -NoProfile -File scripts/test-csharp.ps1 -SelectionFile /workspace/tboe-linux-evidence-20261103/t154-rollback-current/selection.json -NoBuild -Parallelism 1
```

Result: **24/24 PASS**, category **16.9696732 s / 120 s**, runner 23.2337024 s.
No timeout, skip, duplicate or underfilled descriptor. All 12 surfaces reached
both before and after boundaries. The 96 complete records independently confirm
24 original engine rollbacks over 12 raw paths each: six present byte images and
six absences, unchanged generation and no remaining journal. All 12 after-cuts
prove changed MemberPublished bytes, matching journal/transaction/index/image
hashes and original outward cause. All 48 unique owned roots were removed and are
physically absent; no managed owners or new temporary roots remain.

Independent Sol 6.1 / xhigh PREP and current OUTCOME accepted. This closes only
the existing original opaque-byte 12-surface storage route. The broader T154
owner-specific item/quest/game-journal/resource-History/lifecycle-cut union remains
open, as do #1536 and all unrelated UNKNOWN/HOLD statuses. No wound gameplay,
live GM, DOM/provider, fresh-process or Windows qualification is inferred. No game
or GM contract changed, so no GM wording update was required.

Full raw remains outside checkout:
`/workspace/tboe-linux-evidence-20261103/t154-rollback-current`.

- Post-proof: `6ae7cad0921f83b4f396490964d3851924630642454ce5143341f4ab93330045`.
- Summary: `15e0b3cfec9f6ffe08048e8ab7c2c15b414f027c3d71f4b49b0c39f6f5952161`.
- Oracle proof: `d4f3f20c2bef6d01f86222a59de570a4a9566b27b88552d347286c9932554d9e`.
- TRX: `ac6191f0d41dee2911eba2855f58584c5c94eeda414314170a586b7c9bc67b11`.

# Main -> wounds integration — 157 distinct PASS, final OUTCOME accepted

Task #1536 compatibility integration, Linux, 2026-11-03. Isolated WIP branch
`1536-merge-main-wip-20261103`; the original wounds ref is still
`daea0ea094c41426b6c4663af713ab9ada0be49d`, main still
`a62cfd90a5300a21524066b3b457eb467ed9c677`. Actual reconstructed merge is
`d70ce667f6308268c8c69723636b6461b5dfcd23`, exactly those two parents;
merge-base `d0241e71e349fbe2020e4a41b6be7c627c81cbb4`. Unavailable bundle commit8543e8df
was not imported or claimed byte-identical. The 18 physical engine bodies reverse
exactly to the conflict-free synthetic merge (SHA256b0b58f25ea2e967554b0befcbee4166406ede9a0c13ec19656729aa5062428aa),
which adds only main's two skill-repair insertions to wounds. Feature remains1536.

All required bounded impact selections now have157distinctPASS, composed across
explicit versions, not one fresh157 run: source38=4@10e18e6a8 +9@a1510d701
+24@fea795999 +1@06dc53214; runtime112@c496e903b; native chronology1@18c2f7965;
chronology controls6@b2d909ba8. New native case TRX21.8566290s/category26.4229944s;
controls category60.6876988s; both original120s budgets retained. Native case
proves actual II->I healing, later I->II explicit re-trauma, local/cumulative
claims, one C3/C4 publication, exact roots/History/payments/replay and three
embedded refusal oracles. Controls cover healed/no-improvement/blocked-worsen,
guaranteed-counterharm original, cost correction and publication rollback.

Build lineages: fresh native package/Main/Support f54481d85; final Unit a1510d701;
final Integration06dc53214. All managed builds exit0, warnings81/23/26/107 unchanged;
50managed artifacts,208inputs624copies,native6,4PE/PDB567bindings verified.
Catalog1404/13303 valid, no unmapped/stale selectors. Eleven default CI categories
are the four T107 leaves, ordinary-output-existing, narrative-policy-pass-sources,
ordinary-output-policy-source, reasoning-prefix-source, partial-source-guards,
mortal-skill-reference-identity and narrow main-merge-prose-guidance. Actual staged
execution additionally uses the documented existing-method leaves for remaining
unit and final settlement source cases, avoiding unrelated/passing replays.
No frontend change/build, live GM, DOM/provider or fresh-process claim.

Independent Astra/high SOURCE, causal RED, source/runtime OUTCOME and native PREP
accepted their bounded stages. Final Astra/high OUTCOME accepted at
`d143d68dd22d34738a431aebe3df257aa9bf3f1f`; all157PASS and scope verified. Merge corrections preserve
source observations/revalidation, exact canonical names and blank-vs-missing.
Inherited source guards were ported to the existing Core owner with exact outer
forwarding; strict historical inverses preserve all frozen hashes, and Roslyn
checks exact typed settlement without new private reflection invocation. Current
immutable compilation is reused within one test analysis; no new cache, authority
reuse or measured speedup. No production change followed f54481d85.

Full raw, build logs/TRX/native capture and original failures remain outside Git:
`/workspace/tboe-linux-evidence-20261103/merge-main`. Combined final proof
cdb9831723a8801657763ec55c3c9b746c94d3b55aef70f039b28b7356da4fd1;
native postproof0cc549a197c7dc74803f77bf33b1a10c3f1359a1499a7347d7e30dcc7a7941ca;
controls postproof1ae662642b6e613fcf23839629d494ead736402eee95c2c7601e0741caf04b50.
Passing-result composition verifies unique test names/IDs, complete TRX, no timeout/
skip/duplicate/underfill, artifact continuity, no new temp roots/managed owners and
owned cleanup. Earlier read-count RED and inherited guard failures remain recorded.
Previous offline N4 Unit remains UNKNOWN; these fresh builds do not reconstruct it.

This does not close US3/US4/#1536, initial SAFE composition, browser physical-course,
joint terminal, DFR/Mortal compatibility/Windows HOLD, first guardian intermittent
or cross-OS DATA limitations. Source recovery so far verifies84listed files from
an empty Git store, not a full checkout. Final source readback and fast-forward
of wounds remain operational gates after accepted OUTCOME and fresh parent checks. Never update main,
force-push, rewrite history, delete branches or close issues.

Final whole selection PlanOnly exactly reconciles all planned methods/case counts
against the157adoptedPASS, without runtime repetition. Reconciliation is external.

Final recovery receipt: 85listed files /13,719,403bytes independently restored
from GitHub commit02f6f697cf6930bc89c051ea3f349e30856ead80 into a new empty Git
object store without old checkout/object/source caches; every byte/blob SHA matches.
Proof26226f3526d186bf67625ca729ac77acb97ed161a86c997aa1100af802b5bfc2,
under the external merge-main evidence root. Complete tree metadata was fetched;
this is limited-file recovery, not a full checkout or runtime restore. Only these
two human-written notes follow that recovery; source/runtime code is unchanged.
Exact current selection reconciliation:130methods/157cases, proof
c3856fcf5c66f6f9e6e8e0e9c54f5422d94350dadfb623fe8ab61a23c170a23a.
The final receipt commit is read back before the authorised non-force wounds
fast-forward; main must remain a62cfd90a5300a21524066b3b457eb467ed9c677.

# T107 grouped composition correction — WIP, uncompiled/unrun

N3a source d747e4518554fb78cb0edf54365d56f2b0afa739 completed one FAIL (TRX
7.1127267s/category10.7139987s/120s), no timeout/skip/underfill or cleanup issue.
Actual stop: OpenC2 / spiritual_c2_conflict_evidence_mismatch, before offer or
replay. Astra source diagnosis confirms a production prefix defect: C1 rebuilt
healingSources[0].turnEvidence.diceClaims with every closed exchange's claims,
changing its first two rows into four after the next roll. Exact-prefix rejection
in AppendC2NextExchangeEvidence is correct and remains unchanged. N3a postproof
5e7117c4158f6fe29345655dbb58b7a0da2a3c303cba157313e97440bf72d2a3;
summary a30e7daa771692f7a32f1e064d6de4bfe88aaaf9ecc6791d4afdaa628cbbc8eb.
All9 captures and45/207/6/525 post-hashes were verified before new edits.

Grouped code now retains the already validated exchange-local healing claim array
inside that single Compose call. The packet still retains every cumulative claim;
no prefix, digest, source, owner or signed-input check is relaxed. Native fixture
requires whole healing row equality, local indices0/1, later source2/3 and packet4.
The separate latest-healing replay restriction is SOURCE-proven, not runtime RED.
Its scoped correction retains the existing latest shortcut; otherwise only an
active exact healing result plus a complete same-original/instance later-worsen
receipt/History chain can reach the current wound. Unique decision/source joins,
explicit target, later exchange/wave/generation, predecessor/current fingerprints,
selected severity, exact opportunity-authority domain, reset progress and unchanged
natural tick are mandatory. Terminal resurrection or foreign/unreceipted suffixes
refuse. Existing blocked counterharm uses the same extracted opportunity composer;
its checks and exact preimage-string guarantee hashing remain intact.

Optimization: C1 reuses its existing per-exchange claims instead of rescanning or
reconstructing them, lifetime one synchronous owner-bound Compose. Replay reuses
parsed canonical catalog/History/resources, one detached injury-row projection and transient
indexes, with selected preimages and state fingerprints computed once. The maximum
four severity ranks imply at most three strictly worsening suffix steps; impossible
longer suffixes refuse before retained-wound work. No persistent cache, repeated
source I/O, new authority or measured speedup claim. Fresh canonical/consumed-input,
resource/scheduler/world/source guards remain outside and before this DATA join.

Existing GM guide/matrix/worked example/manifest/one guidance Fact now explain
client-owned exchange-local claims and the exact later suffix without new authored
fields, second healing or world cycle. Default affected selection is 11 cases in
four leaves: cheap guidance1+ownership guards2+typed detachment1, new real chronology1 with three
after-valid detached refusals inside it, and six existing directly affected native
healing/correction/guarantee/rollback cases. Those clones are not extra test cases.
Original60s/120s budgets remain. Only the newly modified ledger Fact overlaps the old selection; unchanged zero85,
unrelated accepted cases and HOLD are not repeated. Astra SOURCE caught a nonexistent ReadRoot call before builds. The correction
uses a narrow existing-owner accessor returning only detached deep-cloned injury
rows. Both resolved and blocked reads reuse those parsed rows; blocked no longer
reparses the whole receipt JSON. The modified existing roundtrip Fact now calls
typed APIs throughout and proves nested row mutations cannot change ledger state.
No mutable private root is exposed. This adds one meaningful selected Unit case.

All new grouped code/tests are UNCOMPILED/UNRUN pending whole SOURCE,
fresh affected Main/Unit/Integration, exact discovery/Plan/PREP and qualification.
Initial SAFE-stage criterion, author-pending semantics and all UNKNOWN/HOLD remain
open. No general combat/QTE/turn rule or global #1536 closure.

# T107 causal attempt: preparation FAIL, replay untested

Astra/high accepted the bounded DESIGN. One new native partial Fact reuses existing
seed/staging/graph/C2/common helpers without changing accepted modes. Signed pool
15/5 then5/15 gives active II->I healing before explicit later pressure targeting
the same older wound. The fixture requires actual typed margin7/ReduceOne, staged-I
preimage, later generation/wave, canonical inertness, four spends/final OD3/3,
create/treat/worsen History, fresh roots and replay/signed43. Existing replay code
is unchanged; actual failure is not attributed until these premises reach a guard.
The original two-minute native-pair budget is retained in one exact category.

Sol's whole SOURCE found two preparation errors before any build: a nonexistent
staging call and copying client-owned classification.domain into the GM proposal.
Both now use the accepted fixture's exact existing API and two authored fields.
No production check was changed to accommodate the fixture.

At source 0a8bb0bc1bc9185617af011fa0da52d1b68fe4b7, Integration built exit0 /
107 baseline warnings in74.2398646s; 45 managed /207 selected content /native6 /
4 PDB /525 bindings verified. Catalog1395/13246 and oneFact/120s Plan/PREP accepted.
N3-causal completed one FAIL, no timeout: TRX5.3494877s/category8.9242255s. Healing
margin7/ReduceOne premises passed, but OpenC2 refused second pressure tier0/cost3:
the real signed seed is tierII/cost1. Later offer/staged-I/replay assertions were
unreached. This is a fixture failure, not production RED. Both author and SOURCE
review missed that signed seed premise. Preserve original profiles/rules; corrected
fixture asserts both actual pressure tiersII, costs1/1 and final OD3/3. Correction
is UNCOMPILED/UNRUN; whole remaining preparation is being independently rechecked.
All9 capture files, exact planned/completed count, cleanup/noTMP/noowners and
45/207/6/525 post-hashes were verified before the fixture edit. Postproof SHA256
5cc7ab2e0e806749fe06c5f132130e00eef3f5c7cecb8c428b5cbd5ed6700b33;
summary fdbdd11210d183a5853d4f9903a437d894668697cb011753c66d4c40b2db0f14.
Full raw remains N3-* under the established external spiritual-strain-preimage-data.
Fresh affected build and exact Plan/PREP precede a single corrected attempt. No production fix, private-authority shortcut, new cache, combat
rule, failure/aggregation default or old PASS/HOLD repeat. Source/API-shape checks
and after-valid negatives will use existing typed readers, no parallel test stack.
Current manifest status text is documentary; future runtime pins must refresh its
selected content copy rather than claiming unchanged N2 resource hashes.

# Finite row2/row5 reconciliation — no new workload

Independent Sol source trace establishes an exact reachability dependency for
T081-C3-R2B/T107 initial SAFE stage plus same-plan live insertion. Natural recovery
and self-treatment now construct real bundles, but both require signed-original
activeConflict absent. Live source admission requires that same conflict already
active in the signed original; a new one is PriorStartDeclaration pending. Current
owners recheck original images and lease, so staging first cannot bypass this.
Generic overlay support is not a lawful producer. No safety, cycle, realm, conflict
start or general combat rule is changed; the literal composition remains open.
Successful combat healing followed by guard and blocked healing plus same-exchange
counterharm are accepted separately. Later-injury data is explicitly detached,
so it is not an accepted successful-healing -> later-insertion runtime receipt.

Astra's T150 family map found existing constructor, repair, treatment, recovery,
provider, command, History and independent-legacy examples and real consumers.
No missing family was established. One manifest coverageLimit still called natural
runtime acceptance pending despite accepted versioned slices; only that stale status
is corrected. All markers, requiredText and focused-fragment/provider/fresh-process/
combat limits remain intact. JSON structure/membership is checked; no new runtime
or whole T150 closure is claimed from historical aggregate counts.

Sol's T153/T154 map found real catalog/identity/History indexes, guarded bounds,
lineage visit counters and effect/resource routing population counters. No missing
scale subsystem or quadratic defect was established. The old 24-case rollback
matrix is 12 surfaces x before/after with exact present/absent, generation/journal
oracles; its primary historical receipt remains unmatched here. N2 source-only
rollback2 and P1 committed-readback1 are separate accepted complementary cuts.
Whole owner-specific item/quest/journal/resource-History and lifecycle-cut receipt
union remains pending. Missing old prescribed filenames do not establish missing
code. T151/T152 full surface reconciliation also remains open; existing privacy,
markup and text-boundary leaves are not promoted to a complete adversarial matrix.

This documentary block preserves accepted zero85, both author-pending semantics,
all UNKNOWN/HOLD and the original finite scope. No test/budget/cache/production
change or general-project audit is authorized by these findings.

# FR-025 zero-comparison route accepted — exact bounded result

Astra/high independently accepted the full 85-case OUTCOME and checkpoint on
ac24675fc6ac9eee3aae1e3c16d037fb68462018. Native TRX duration is 12.4653742s;
category 16.531074s / 180s. All 33 capture files and 45/621/6/524 current hashes
were checked independently. Sol accepted SOURCE, staged PREP and cheap73/controls11
OUTCOME. The following detailed runtime record retains exact compilation lineages,
proof pins, original failed compile and qualification limits. This closes only the
tracked T081-D-EXISTING-STRAIN-ZERO slice, not parent US3 or #1536. No additional
runtime is needed for documentary closure; current remote/export closing evidence
will be recorded externally without putting raw in Git.

Continue finite original row2: lawful produced initial healing/recovery stage plus
same-plan insertion. Current natural/self-treatment producers now exist, so the old
Sep26 Mortal-only explanation needs tracing against current owners. The accepted
reset test still does not prove that composition. No safety/cycle/realm rule is
changed by this investigation. Pending actor/multiwound semantics, R2/A5 UNKNOWN,
DFR/Mortal/Windows HOLD and all previously stated scope limits remain unchanged.

# Original FR-025 zero-comparison route — observed 85 PASS, final OUTCOME pending

Runtime metadata/source boundary: c80b496c632205b8b194a62d3554ac66e7594217.
Main and Unit compiled eace2b5aa6889fa787975fae514e1ab6fa9bc571; Integration
compiled c80b496c. The only intervening code change is the reviewed native-fixture
JsonObject cast. No Main/Unit code or operational input changed. Builds exited 0
with exact baseline warning multisets 81/26/107; four PE/PDB identities and 524
changed-source bindings, 45 managed artifacts, 621 resource copies and six native
copies were verified. Failed first Integration compilation is retained separately.

Whole SOURCE accepted by Astra/high and Sol/xhigh; exact discovery found 1394
categories / 13245 methods, no stale or unmapped selector. Sol independently
accepted PREP and OUTCOME for cheap73 and source-only controls11, then PREP native1.
All 85 semantic cases and test IDs are distinct across these three exact stages:

| Stage | Actual result | Protective category budgets |
| --- | --- | --- |
| Six typed/affected Unit leaves | 73/73 PASS; runner 24.0768531s | 60s each |
| Existing source-only publication/rollback and refusal | 11/11 PASS; runner 40.2197563s | 180s / 60s |
| Existing III wound, real zero-ceiling comparison | 1/1 PASS; category 16.531074s | 180s |

The real route produces increasing strain, pressure7/maximum0, no opportunity or
wound decision, authenticated source-only C3, common C4, exact player/opposition
payments3/2 and depletion, unchanged parent/Identity/History and a later signed44
read. New v3 comparison survives without inventing an injury source. All 19 new
typed cases pass, including v1/v2 migration, atomic mixed-request DATA, after-valid
order/cross-family refusals, prefix/replay, detached fake-owner refusal and exact
History after later healing. This is not an actual mixed-C3, native IV, Engine/UI
or fresh-process qualification. Earlier partial_success remains C2/cold only.

All selected descriptors/TRX/capture files are complete and hash-checked; no timeout,
skip, duplicate, underfill, new temp root or active managed owner. Cleanup and all
45/621/6/524 post-hashes agree. Full raw remains outside checkout at
/workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data/N2-*.
Compact external proof pins (SHA256):
- Combined85: 0c9e503c84984b1e893ab4afdd5ec196e747601bdcebf53be212d2959d08755e.
- Cheap73: d36280f57d4f2ee3b93af0f93d01478588cefe19a5aa8e7722f4b547e0e4e35d.
- Controls11: 42107308276fd77f7491ab932a7462809dace0f5462f15b3da9090631ed024b1.
- Native1: 0bd598aa8de4a85444c11fabf3f48663bc9c2216953244fab6651b3dec010c6d.
- Native summary: 19022a76edfae9c0a52fa3a7644680f4b9664ffbd104c8b9edf30bdb5707f615.
- Artifact/resource: 1463995f95b010807b600133761d6e194e6e4df44b2f2b85b2befbfdc5259402.
- PDB: 75b2025314277ece9177cdde32c0ab89dca9dd5c1198e7e7efb4c84afc0c9522.

Optimization assessment: existing witness/parent parsed DATA is consumed once per
whole ledger; shared transient indexes and a ready queue preserve request order
without repeated remaining-set scans, proof reconstruction, new I/O or persistent
cache. Current owner, receipt identity, signed/current integrity, History and
publication checks stay fresh. Whole-route timing is evidence of this bounded run,
not an isolated speedup measurement. No aggregation/failure rule was invented.

Final independent OUTCOME and remote closing checks remain pending at this WIP.
The next finite row2 reconciliation found that accepted recovery-reset11 proves
seeded progress -> live worsening -> separate safe recovery, not produced initial
stage -> same-plan insertion. T081-C3-R2B/T107 coexistence stays open; it needs a
lawful producer/owner route, not a cosmetic assertion. Both pending semantics,
R2/A5 UNKNOWN and DFR/Mortal/Windows HOLD remain open. No whole US3/#1536 closure.

# Zero-comparison build boundary — fixture type correction, runtime unrun

Whole SOURCE accepted independently by Astra and Sol at eace2b5aa6889fa787975fae514e1ab6fa9bc571.
Fresh Main completed exit 0 / 81 baseline warnings in 65.95 seconds; Unit exit 0 /
26 baseline warnings in 37.33 seconds. Integration failed compilation in 63.60
seconds: the new native fixture passed a JsonNode where WithOwnerCompanionAfterImage
requires JsonObject. Its already-object receipt is now explicitly read with AsObject;
no assertion, production contract or gameplay behavior changes. Narrow independent
delta review precedes an Integration-only rebuild. Main/Unit source and artifacts
remain reusable with explicit eace2 lineages; all runtime assertions remain unrun.
Full failed build evidence is retained externally as N2-build-integration.

# Zero-comparison grouped SOURCE corrections — WIP, uncompiled/unrun

Independent Astra SOURCE found four issues: missing JsonObject import, invalid
absolute healing exchange zero in the migration fixture, sorting that could hide
original injury-side order, and a quadratic request merge. Sol separately found
that die claims must retain local exchange zero while intent/witness use absolute
exchange one. Corrections preserve those distinct coordinates and all assertions.
The parser now checks each original table's scalar source/slot order before merging;
a new after-valid two-sided fixture reverses only that order. Existing typed turn
coherence proves the exact order fault; root Parse preserves its generic public
refusal code. Both reviewers caught an impossible inner-message assertion before
builds; it was corrected without exposing private diagnostics or changing Parse.
The merge uses transient indegrees/followers and a ready queue:
each request is queued once, each precedence edge visited once, no proof reparse,
additional I/O, persistent cache, authority reuse or measured speedup claim.

Grouped delta awaits independent review at a frozen SHA. Typed DATA selection is
now 19 cases; total affected selection is 85 cases in nine leaves, pending fresh
discovery. Main/Unit/Integration v3 code remains uncompiled and all new GREEN/native
assertions unrun. Accepted historical 76, partial C2/cold bounds, both pending
semantics and old UNKNOWN/HOLD remain unchanged. No new game rule or combat scope.

# Complete zero-comparison WIP ready for whole SOURCE — uncompiled/unrun

Existing v3/C3 implementation now has nativezero1 scaffold reusing realIII birth
and existing fullframe helpers; only oppositionDie parameter added, default12 keeps
the earlier positive case unchanged, new5 gives canonical mixed band/margin-1 and
pressure7/max0. It checks original source calculation/nooffer/no private C2 state,
required completed receipt proof, source-only C3/commonC4, exactresource3/2,
unchanged parent/History, v3zero-onlyrow and nextsigned44. All native assertions UNRUN.
TypedzeroDATA now18 cases: original3positive/9refusal plus immutable request/prefix,
request-order cycle, latestHistory afterheal, fake owner/detachment, empty-v3 versus
nonempty-v2 and a real blocked/paid nonemptyv2 DATA→v3 migration. Migration uses
existing intent/check/codecs and typed6→4 OD, no nativecombat or owner fabrication.
Old SourceOnly6publication/rollback and5refusal cases are selected as directly
changed owner-identity guards; exact old containing-ledger subset/parent7/history9/
GMguide1 cover changed parse/coherence/prefix behavior. No broad suite or oldHOLD.
GMguide/example/manifest/matrix/spec synchronized: no newGM authoredfield, fake
opportunity/none, worldtime/cost rule, actor inference, aggregation or newframework.
Whole independent SOURCE/FIXTURE/SELECTION then freshaffectedbuilds/exact discovery/
Plan/PREP/cheapGREEN precede nativezero. No new acceptance/speedup claim at this WIP.
Previous76 bounds/sourceSHA/unknowns unchanged. Code and current task kept remotely.

# WIP zero-comparison existing-owner implementation — uncompiled/unrun

N1b schema RED3 independently accepted by Sol: valid oldledger and intrinsic
max0/pressure7/increase prerequisites, then unsupportedv3 Parse. Fullraw retained.
Root now implements v3 comparison rows with unchanged positive decision bijection,
existing witness/parent readers, source/coordinate/context/dice uniqueness, original
request ordering/prefix/replay including both injury and comparison families.
Per-table order is checked before a transient request-precedence merge; merged
source ordinals use the existing turn/dice checks. No parsed witness is reconstructed
for this merge and no index/cache/permission survives the whole parse.
V3 may carry coherent empty healing arrays; v2 still requires nonempty healing,
semantic healing predicate uses actual rows. Existing parsed comparison DATA is
reused for any healing same-exchange comparison; fresh owner/History checks remain.
Source-only C3 reconstructs witnesses through the existing ComposeFirstOfferSource,
retains detachedDATA + exact completedreceiptidentity; mixed C3 uses the same
owner-authenticated packet sources. Common receipt proof still binds the exact
completion; no fakeopportunity/none or relaxed guard. Source-only witness work is
once per exhausted owner; no new cache/diagnostic framework or speedup claimed.
All new production UNCOMPILED/UNRUN, whole SOURCE not yet requested. Nativezero/
additional cross-family/History/owner fixtures and GMdoc/example synchronization
remain before completeSOURCE/freshbuilds/cheapGREEN/nativePREP. Previousaccepted76
belongs to its explicit sourceversions, not this WIP. UNKNOWN/HOLD unchanged.

# Zero-comparison structural RED3 — 2026-11-03 UTC

Finalscaffold54306f59e840028c2ed6b25681f17e3f99208af1 Unit36.927997s0/exact26;
45artifacts/621resourcecopies/native6/4PDB517bindings. SOURCE/PREP independently
accepted, retaineddiscovery1390/13237 and freshoneMethod3case/60s Plan.
N1bpositive3 actual3FAIL in3.2527151s (runner7.8275457/wrapper9.056513), exactly
v3Parse assertion28 after each validv1baseline and intrinsic increasing/pressure7/
max0source. Currentrootversion1..2 cannot readv3. Downstreamappend or negative9
notqualified. OneTRX/ninecapturefiles/exactplannedcompleted/no timeout/skip/dup/
underfill/cleanup/noTMP/noowners/allhashesunchanged.
Postd3bca4121c9ca059a1bdc1688ba5860b3d403bb56bfed834e1dfd1ddd42f58cd;
summaryd083606bda1317821512bf8289ac7cdc8189f67577c9ec9faad67d17c6ca0851.
Raw N1b-positive-red remains in established externalroot; no runtime retry planned.
Next minimal v3 implementation follows accepted AstraDESIGN in currentplan/task;
priorpositive76/partialC2cold/typedIV and all UNKNOWN/HOLD unchanged.

# Zero DATA atomic append oracle correction — no runtime attempted

Independent Sol correctly found the future mixed-request append oracle must add
positive43/its legitimate none AND zero comparison atomically to prior42. It must
not append a comparison to an already accepted43 request. Keep immutable request/
prefix guards unchanged; preserve the original before-ledger and add the whole
current batch only to next. Decision count grows only for the real positive source,
never for zero. Also strengthen positive-ceiling refusal with a fully valid positive
witness under a separate prior-injury baseline, avoiding a mere declared-math mismatch.
These are fixture-only corrections under accepted v3 DESIGN, not gameplay changes.
N1/N1a builds remain saved/unrun; fresh finalUnit follows SOURCE delta review.
No positive3/negative9 runtime attempted, no v3 production implementation yet.

# Zero DATA preparation review correction — no runtime attempted

Independent Sol found one positive prerequisite defect before N1runtime: margin-1
requires existing mixed_or_no_effect band, not opposition_success. Correct that
single loop literal in both actual and ordinary while preserving4/5, setback,
harm1/pressure7/max0. Existing operation rules allow setback/position loss with
this valid mixed dice band; no new gameplay inference. No other positive3 blocker.
N1Unitfeec36.343244s exit0/exact26, discovery1390/13237 and PlanOnly executed no
cases. That uncorrected scaffold is not used for runtime. Fresh correctedUnit and
exactPlan/PREP before positive3 schemaRED. Negative9 remainsunqualified; strengthen
positive-ceiling refusal with valid positive witness in the later GREENblock.
No production or accepted76 change, no UNKNOWN/HOLD release or new author rule.

# Zero-ceiling v3 DATA scaffold — no new production change

New typed12 authored: three positive modes (zero-only, prior injury, mixed current
request) and nine after-valid row/context refusals. Reuse existing independent
witness/factories, actual4/5 margin1/pressure7/max0 and full parent/ordinary data.
Existing schema baseline and intrinsic max0/increase are prerequisites. Only
positive3 may run for schema causal RED after independentSOURCE, freshUnit and
exactPlan/PREP. All12 UNCOMPILED/UNRUN; negatives do not qualify before validv3.
No live authority or source-only publication from these DATA tests. Main/Integration
remain accepted e73; previous76 accepted bounds/closing proofc75dd68b4521acbf198be9b09c1104e060f70129efa72e51d7367de8bab2d130
preserved; bothpendingquestions/oldUNKNOWN-HOLD unchanged. Schema-v3 production
and nativezero fixture not yet implemented. Current acceptance target/task inplan.

# Accepted existing-parent positive maneuver lane — 2026-11-03 UTC

Independent Astra/high final OUTCOME accepts nativeIII1PASS with original180s:
TRX13.7093839s/category17.5304893s/runner22.6174924s/wrapper23.860353s. Actual
realIII birth→fresh43 maneuver/setback/guard→coldC2 explicitnone→ownerC3/commonC4
→signed44 passed. Exact3/2 spending/playerdepletion, parent fingerprint and
Identity/History bytes, root availability, retainedv2/no fabricatedparentSourceId,
no private controls remaining all checked. Repeated queries shared one actual
signed-parent computation; no isolated speedup claim. Metadataa956e2479; native
Main/Integration compiled e73a288c2; Unit compiled a956 after the fixture-only fix.
Postc55a7d1f45496f294779da1d0a5b126c4cf89c62e23ce301cd7e8fff60c9ad45;
summary9f71407a0eef99db9298d7e6f75f861a281d36d6d7d1ce76bd2f398a68f23d1a.
OneTRX/ninecompletecapturehashes/45artifacts/621resources/native6/4PDB516bindings,
exactcount/no timeout/skip/dup/underfill/cleanup/noTMP/noowners/hashcontinuity verified.

Sol accepts versioned cheap75 = oldunchanged19 at e73/metadata020 + corrected56
at a956, exactdisjoint semanticcases. Oldphysical33/32PASS1HistoryfixtureFAIL
remains preserved; its joins13PASS1FAIL replaced by correctedjoins14 within56.
This is not one fresh75/76 run. Remaining56 postdd79b537c991066c5e5b8f977877f5c464c06c100a745c5b586f2ad5e9d2d2c0;
summary1d2e438f353b7349b21242d1351081add6b9922c355ed24b5ad6c959ae4364d6;
combined75fc7aaba890687b2e6c9221a49b2f024b2279a223d39c42a87627cd8c8483f420.
Total bounded qualification76distinct across explicit versions. Source19 and later
metadata/fixture2 clean exports/GitHubwholeblob proofs preserved; not raw restoration.
Fullraw/builds/TRX/review/findings remain outside checkout:
/workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data/M3-*,M4-*,M4a-*,M5-*,M5a-*,M5b-*,M5c-*.

Qualified scope: one existing III player wound, one stack/magnitude1, maneuver/
setback versus guard and positive-maxII result. IV is typed DATA only. No Engine/UI/
fresh-process or whole US3/#1536 claim; partial_success remains its earlier C2/cold
scope. Generalcombat/QTE/ACTIVE-F1 excluded; pendingactor/aggregation semantics and
R2/A5/DFR/Mortal/Windows UNKNOWN/HOLD unchanged.

Next original FR025 cell: increasing strain with non-guaranteed maximum0. Existing
source-only C3 cannot retain its comparison in the positive-source/decision ledger;
no fakeopportunity/none or weakenedownerjoin. Independent Astra/high accepts bounded
schema-v3 comparison-only rows in the SAME receipt/source-only owners, closedv2
witness, latestHistory/currentoriginal/replay/prefix and cross-family dice/request
joins. Preserve v1/v2 positive bijections and healing rows; v3 emptyhealing family
must be handled explicitly. No implementation of v3 yet. Cheap typed causal controls
and one lawfulzero source-only C3/C4/signedread representative before acceptance.
This remains finite row1 implementation work, not a new author rule or battle system.

# Cheap gate partial outcome; exact fixture correction

At metadata02072a5ee/compiledsourcee73a288c2: actual M5a-cheap-green stopped after
3of8descriptors: new33 executed32PASS/1fixtureFAIL, no timeout. Positive2 and
refusal17 passed exactly; joins14 had13PASS/1FAIL for change=kind. That row changed
worsen to diagnose without the required diagnosis result, so History parsing failed
before the intended exact-anchor refusal. Change only kind to valid complicate,
preserving a parse-valid chronology with a mismatching latest-kind anchor.
Remaining42 were UNRUN. Keep unchanged positive/refusal19; repeat changed joins14
plus remaining42 (56) after freshUnit/Plan/PREP, not full75. Native1 remainsUNRUN.
Full oldM5a-cheap-green raw/TRX retained; no failed assertion relabeled PASS.

Additional bounded SOURCE audit confirms an original functional gap: increasing
strain with non-guaranteed maximum0 needs comparison-only durable receipt DATA.
Current sources+decisions ledger cannot represent it without inventing a none.
Source-only C3 currently fails its common owner join; passing only identity would
lose historical comparison. Do not weaken that guard. Positive/guaranteed native
representative stays separate and does not qualify max0. Capped broken->broken
is different: no increasing harm source, ordinary history needs no substitution.
This is an implementation remainder in original FR025, not an author semantic
question or new general-combat task. Existing owner/receipt extension is next after
the scoped positive lane; no approval of aggregation or actor inference follows.

# Fresh corrected builds complete; catalog metadata correction

Runtime source e73a288c233e78a951c8aa3309cc87412e63ccf3: Main63.607536s/81,
Unit37.372065s/26, Integration71.943732s/107, all exit0/exact prior warning multisets.
45managedartifacts/207inputs621resourcecopies/native6 verified. FourPE/PDB516source
bindings exact: four additional changed paths (one new file and three newly touched
owners), not the preliminary513 estimate. Artifactcaf41dfe88ce2d496c176c2bf6edc004b72f94ed1944442fb63ff0effffc3b8c;
PDB6fbc1ffaf2ee8d806a64b72719380ee52d8f2d8f5ea6d8786d0390931f9dfbce.
Source19 clean export/GitHubwholeblob proof2ff4e80be5898e653066d02378c1c964234270c78f9d12ec58f2a7224aa4d829.
First catalog M5a-catalog exited1 before discovery or tests: new joins category
related links were strings, whereas strict loader requires {id,when}. Correct
only these three metadata links. No C#/project/embedded input change; reuse fresh
e73 builds with explicit catalog/checkpoint-only delta, no rebuild or runtime repeat.
New33/cheap75/native1 still UNRUN. Sourceaccepted; exact discovery/Plan/PREP pending.

# Signed-existing parent SOURCE accepted — corrected WIP, runtime pending

Independent Astra/high whole SOURCE accepted c5da plus two verified corrections:
effective incoming guard is mandatory in v2/live; raw correction Pointer now comes
from the source-owned initial inventory and existing ResolveRawExchange, including
signed historical rows. Two targeted after-valid guard negatives update both actual
and retained ordinary bytes, so they reach the intended guard. No further concrete
production blocker; v1/current ownership/History/C3-none/common receipt joins retained.
Independent Sol fixture review found no blocker in initial31; guard delta adds2,
so current new33/cheap75/native1=76 are planned, not executed. Fresh discovery and
PlanOnly determine exact counts. First c5daMain build69.345415s exit0/exact81 only;
that binary predates the review corrections, so all three affected projects require
a fresh corrected build. Preserve its outputs/raw separately, no runtime from it.
No final acceptance yet; cheap GREEN and separately reviewed native PREP pending.

# WIP signed-existing parent implementation — uncompiled/unrun

Original FR025/T081-D accepted bounded design now implemented in existing owners:
strict v1 retained; closed v2 III/IV intrinsic comparison plus complete ordinary
validation; current mechanics context authenticates signed/current parent and exact
source-group/roots, retains immutable comparison DATA within existing epoch/ordinal/
draft/dependency-cut only, with fresh IsCurrent/signed actor and whole candidate
checks each query. No separate cache or permission reuse. Actual query/read counters
are read-only; native representative checks repeated queries share one parent read.
Latest History anchors retained after lawful healing; v1 insertion joins remain.
Decline-only v2 now requires owner-issued C3 pair and common completed receipt identity.
New typed joins14 complement DATA17, existing affected v1/guard/history/guide controls.
Worked guide/example/manifest/matrix synchronized; private v2 remains client-owned.
All new production/typed14/native count assertions UNCOMPILED/UNRUN. Whole independent
SOURCE/FIXTURE/SELECTION, fresh Main/Unit/Integration, exact discovery/Plan/PREP and
cheap GREEN before native representative. No cache speedup, full-family or publication
claim. M4a causal RED accepted by independent Sol; raw preserved, pending semantics
and old UNKNOWN/HOLD unchanged. Code is WIP, not an accepted gameplay result.

# Corrected existing-parent DATA causal RED — 2026-11-03 07:09 UTC

Frozen05a4df2897cbd45c5c32d393a359921ee5300cae: freshUnit36.431996s exit0/exact26,
45artifacts/621resourcecopies/native6/4PDB512bindings. Correctedpositive2 completed
2FAIL at3.8543082/60s (wrapper10.380527), exactly ReadIntrinsic:37 rejecting closed
v2fivefields against v1sixfields, after valid parent/ordinary/canonical prerequisites.
No timeout/skip/dup/underfill; cleanup/noTMP/noowners and all hashes unchanged.
Posta757cf4440b257c47be33826dcf24f66317225300efd78c4b6f5bcb715fc8f2e;
summary623a5d240bf912d702a935d821e2d59f1a238f6494e12c2b8c97ce99dc18cf45.
RawM4a-positive-red remains external. This supplies parser causal RED, not GREEN,
currentowner/History/C3 or publication acceptance; negative15/native still unadmitted.
M4 preparation failure preserved separately. Minimal implementation follows the
accepted Astra design; unchanged v1, no author inference/aggregation or combat rule.

# M4 fixture encoding failure — not parser RED

Source5a39a58d2 Unit37.622277s exit0/exact26; artifact45/resources621/native6,
4PDB/512sourcebindings verified. Catalog1387/13230 and exactpositive2/60s Plan,
independent Sol PREP accepted. Actual completed2FAIL in3.4542302s (wrapper9.851882),
no timeout/skip/duplicate/underfill, cleanup and all hashes unchanged. Both parent
and ordinary prerequisite checks passed, but SourceWitness.Payload rejected the
noncanonical actual JSON before v2 parsing. This is a fixture preparation failure,
not genuine parserRED or any GREEN. Post64a9ebdac10f3d69a2b636d1945e927471ef3f92be41d0df3abafc034ab667bd,
summarye9eaebd4d200eab032b3200d2a5a50c8e48a9cfbdf1a1e84b34877e1b5b207c3;
full evidence M4-positive-red outside checkout in the established raw root.
Correct only fixture Encode to existing canonical serialization. Production remains
32ed, negatives15/native unadmitted. Fresh corrected Unit/Plan/PREP before the
same positive2 retry; retained failure is not overwritten.

# Typed scaffold review correction — 2026-11-03 07:00 UTC

Independent Sol found one fixture-only preparation error: the reused cost definition
is already the definition, not a nested definition wrapper. Corrected its IV
component magnitude access before compilation. No other static blocker reported;
positive2 only remains the intended parser RED. All17 still uncompiled/unrun;
production unchanged, M3 evidence and both pending semantics preserved.

# Existing-parent typed DATA scaffold — no production change

NewUnit17 closed-v2 DATA cases authored (III/IV2, after-valid refusal14, detached1),
uncompiled/unrun. Onlypositive2 may be used for the cheap parser causal phase after
SOURCE/freshUnit/exactdiscovery/Plan/PREP; failed valid controls do not qualify the
negative assertions. M3native1PASS/1genuineFAIL retain1137 provenance, no rerun.
No live owner, C3 or native IV claim from DATA. Currentplan holds optimization/key/
lifetime/fresh-check contract; actual parser/consumer/C3 code not started.

# Existing-parent maneuver causal boundary accepted — source1137d273

Independent Sol accepts baseline1PASS and actual1 genuineFAIL on separate runs,
not one aggregate. Runtime1137d273ad08240bd9ebd41b26a8dfefcbee9181; productionMain/
Unit unchanged32ed759. FreshIntegration73.071129s exit0/exact107warnings;45artifacts/
621resourcecopies/native6/4PEPDB511bindings verified. Artifact6307f1fa22784bfe12d02bfe26d91d37a3c882cee34ae9563147bd1568d5a252;
PDB e7c16eb5ca34f94d09383b090e4b7efc1527e7b8572f5018d069508a05eaeae8.
Discovery1385categories/13227methods noexecute, exactsingleton Plans/independentPREP.
Baseline1:12.9320849/60s, wrapper19.720110; realIII birthC4 leaves3OD each, signed43
ordinary live-resource maneuver with nonmatching parent passes/inert/noauthority.
Postd93146758d8ca2b70c45d549a0dcc027b7e6bf3fc302222d077032e67b42306f,
summary1833ac70b2c721c7af35a473fb2e8d5ebc108cbc22cb81938b7c8fa6df33f441.
Actual1:12.9225417/180s, wrapper19.130253, fails atAdvance line55 after realbirth,
signed43/resourcebegin and canonicalinert assertions. Exactly integration_required
and maneuver_changes_strain3->4; no fixture/compile/timeout failure. Later coldC2/
none/C3/C4/signed44 assertions remainUNREACHED, no new publication PASS claimed.
Post25fb0130ebdba632c3897a834d2514ca3884d7b7fcf0df30a282d6690353aea0,
summary6e1cae1350adc439cde1965d7ebc6734c60187b130bb416eac22d5c1afc7253c.
EachoneTRX/ninecapturehashes/exactcomplete/no skip/dup/underfill/cleanup/noTMP/noowners
and unchanged45/621/6/511 independently confirmed. Fullraw/builds/proofs remain:
/workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data/M3-*.
Clean6file source export/GitHubwholeblob proofaa9622b93aa3e87c0ec80e00f3bdfc5b9f28cbdff624c800d9514347e35d86fe;
source-only, not environment/raw restoration. Oldoutputs5 CRC/hash archived, none deleted.

Next: cheap closed-v2/current-parent/History/decline-proof controls and minimal owner
implementation per acceptedAstraDESIGN; keepv1 strict, full ordinaryshape/rules and
all otherburdens. Reuse current parsed DATA only in existing context, fresh epoch/
ordinal/draft/dependency checks eachuse; no newcache/probe/diagnosticframework,
ambiguous actor inference/aggregation, GMfield or generalbattle rule. No production
implementation yet, typedv2 controls UNWRITTEN/UNRUN. US8/T149/partial7 acceptance
and R2/A5/otherUNKNOWN-HOLD remain separate and unchanged.

# Existing-parent maneuver: independent DESIGN, native scaffold only

Astra/high accepts the original single signed-existing III/IV strain/maneuver lane;
contract and v2/current-owner/C3-none/historical requirements are in currentplan.
No author ambiguity is resolved, v1 remains strict and no production changed.
New native scaffold2 Facts uses real one-exchange5/15 birth C2/C4 (3OD each), fresh43
maneuver4/12 versus guard, a pressure-parent ordinary control and corrected current
maneuver target. Future assertions include cold/decline-only common publication,
exact3/2 spending/depletion, parent/History/root preservation and signed44. These
are UNCOMPILED/UNRUN, no source/admission/PASS claimed. New exact category owners
baseline60s/actual180s; selected only these2 for the causal phase. Independent whole
fixture/selection SOURCE, freshIntegration/discovery/Plan/PREP remain before runtime.
No full suite, closed-case rerun, R2/A5/HOLD execution or limit/capture change.

# US8 map and T149 accepted; continue original deterministic wound consequence

Independent Sol SOURCE accepts the six-scenario historical US8 map and original
T149 source-guard completion. No new tests/builds/coverage were added. No T177 or
whole1536 closure; original full browser R2/terminalA5 and prior HOLD remain open.
Current sources/runtime partial7 remain32ed759; acceptance is C2/cold, not C4/UI.

Next independent finite row1 cell is pre-existing single III/IV strain wound with
player maneuver/setback: current consumer requires selected same-request/newIII
parent and otherwise raises integration_required. Ordinary maneuver has a unique
unchanged-strain invariant; this avoids selecting either pending per-actor failure
or multi-wound rule. Bounded DESIGN must establish current root/graph/latestHistory/
source authority and exact ordinary-result provenance through existing owners,
keeping oldv1 strict. No new broad framework, borrowed permission, fabricated parent,
resource priming or GM field. Design only; code/causal tests remain UNWRITTEN/UNRUN.

# T149 ownership and retirement receipt map — existing evidence only

This continues the finite row5 after the independently accepted US8 map. The
original [T149](tasks.md) requires source guards for loose wound fields, mappings,
wrappers/aliases, NPC-effect wounds, raw UI parsing and standalone writers. No
new tests, builds, discovery, parser/cache or gameplay changes were made here.

| Original T149 surface | Existing accepted result and current owning guard/path |
| --- | --- |
| Loose PlayerWoundChanges/NPCWoundChanges and raw dispatch | P2 runtime9d4b03cf87c88939ca5708547dc21ca00a86df9e:16PASS (player raw7/offer1, NPC raw5/offer1, sourceguards2). Dedicated exact-presence refusals preserve lawful absence/adjacent data and bytes. NPC state-only legacy priority3 is separately accepted onb36547e. Not a general QTE flow proof. |
| NPC effect carrier cannot contain wound commands or entries.wounds | N3 runtime5950b74a95eeee1252247dbf20f6265df9f25a5d:20PASS (causal16/sourceguards2/real publication1/early refusal1). Current NPC wounds use npc_wounds.json; legal effect metadata/publication and strict refusal retain exact source bytes. |
| Canonical player carrier instead of loose arrays and dead parser | Accepted R4/R5 versioned11 (canonical9+confinement1 atf21ec22c, inventory1 atef2c7623), then later D1 canonical9/ownership1 and D2 discovery1 at654c3b14f705e6bb1813be3ac72cf07ae29736e6. These overlapping receipts are not one fresh or additive aggregate. Old loose parser is absent; canonical root/error precedence stays. |
| Combatant WoundReference/sourceWoundId, generated effects/hints | W2 runtime0f9e47de4bc2305561bb93ebea233915aa70253e:14PASS, including raw7/offer1, three signed wound-field offer peers in total and documentation2/sourceguards2. Exact legacy type/property refuses; ordinary current effects remain valid. Negative-only example is teaching syntax, not a valid runtime command. |
| Retired wrapper/progress aliases and generic wound activation | S1 runtimeb1e30030edb27e670c566a544a3b2a20f72d6e89:6PASS. Inventory/discovery guards forbid descriptionOfEffects/generatedEffects/healingState/isHealed everywhere in production and bound remaining intentional refusal tokens to exact scopes. Generic wound activation refuses; typed active wound export remains non-public/nonmaterializable, including empty-group fingerprint and caller-generic-export refusal. Independent typed wound_legacy remains supported. |
| Raw UI parsing and direct file mapping/distribution | Current source inventory owns WoundPlayerStatusReadService -> WoundPlayerReadService -> WoundPlayerStatusBlocks for both status consumers; no repair/write from those readers. DirectLooseWoundDistribution guard checks GameResponse/FileMapping field/path retirement and StateDistributor typed command consumption. Inventory/discovery assertions are included in S1's actual6; this map does not claim a separately rerun DirectLoose Fact or full DOM/provider execution. |
| Standalone writer/source bypass | Current inventory owns accepted authority registry/common planner, typed carrier/source authority and exact materialization binding. GameResponse/FileMapping/StateDistributor, EffectAcceptedTurnInputComposer and sourceguard file have NO delta since S1. The generic exporter cannot grant wound materialization; accepted typed command/plan owners remain the write path. No new standalone writer or raw UI path was found in the scoped read; not a claim of full C4/terminal failure-matrix acceptance. |

Root rehashed/parsed primary summaries and TRXs for S1green6/P2green16/N3green20/
W2green14/D2green1: exact original source/counts, allPASS, no timeout/dup/underfill,
complete selections and both cleanup flags. No binary provenance was reassigned and
no workload repeated. Cases overlap across those groups; no summed distinct total.
External read-only proof7b85755618c5c03d7365b452039893875e9f93481398c59c2d40d5943185aa75:
/workspace/tboe-linux-evidence-20261102/us8-t149-receipt-map/T149-existing-primary-receipts.json.
Older R4/R5 and US8 raw were not restored; their accepted documentary receipts stay
explicitly historical. The later L1 current-query extraction reuses typed catalog/
identity/History DATA and adds no UI parser, write path or generic source authority;
its accepted13+4 and partial7 retain their separate versions and C2-only limits.

No residual source-retirement defect or new coverage requirement is established.
Independent Sol verified the primary proof/unchanged sources and accepts original
T149 source-guard completion. Root checked sources and primary receipts; only T149
is marked complete. Broader final review/terminal/rollback/UI remain separate.
Optimization: documentation/static receipt reads only; no production cost/cache or
GM contract change, so no new build or repeated passing selection is warranted.

R2 cause assessment also continued independently, read-only by Astra/high: the exact
three-treatment/two-wait browser fixture is unchanged; retained files prove two
milestones and turn46 response, not final healing. Existing prefix ends after a
152.10s validation scope but lags actual execution; its unclosed treatment scope
is not proof of a stuck treatment. Unresolved boundary is ordinary wait46 returning
and treatment3 opening/confirmation. No specific functional/preparation defect or
admission is established. Preserve UNKNOWN180.1041994/noTRX and retained root;
no retry, limit/capture change, cache or new diagnostic stack. A5 and all other HOLD
and pending semantics remain unchanged. Other independent original work continues.

# US8/T177 six-scenario receipt map — historical acceptance reconciled

Base/remote bfcc7a2aaa4f2f7317fd7b1744b83381e17df070 verified clean, no live
build/test owners. This is the next independent item of the finite wounds-only
remainder, not new scope implementation or a fresh test run. The normative six
scenarios are [US8](spec.md#user-story-8---apply-roll-consequences-to-an-exact-skill-priority-p1).
The existing implementation/receipts are in
[scope plan](../../docs/superpowers/plans/2026-09-05-roll-modifier-skill-scope.md)
and [T167-177](tasks.md). Root inspected current typed tests and consumers; an
independent Sol read-only assessment confirms the mapping below.

| Original scenario | Recorded acceptance / actual existing assertion | Status and exact boundary |
| --- | --- | --- |
| 1. Bind the exact offered/current usable permanent skill | Plan Task9 pure674 (20260905-163006-052-14080-38da855683794e5591e3d3e592f72538-focused); T175 lifecycle16 (20260905-185327-418-29756-57f34ed39be740db8aad93040c34acc2-focused). Current FocusedApply_OrdinarySkillPublishesExactScopeAndCarrierIndexAgreement and wound lifecycle assert exact selector/source/index. | Existing binding acceptance matched; no absent binding implementation established. |
| 2. Reject malformed/new/final-disabled/wrong-owner/duplicate/confusable/idless/unknown scope without allocation or partial publication | Same pure674/lifecycle16 plus repair31 (20260905-185751-161-26656-ed7e0a39f84b4fac98f2311381441c5c-focused). Current authority/structural negatives, FocusedApply and WoundFocusedApply final-authority/changed-selector refusals retain exact before-images. | Existing structural, authority and atomic-refusal receipts matched; no new coverage or relaxed checks. |
| 3. Filter broad/focused/nonmatching/identity-less before cancellation | T177 pure resolver44 (20260906-084733-313-8724-b53c9cb7d9ac4d358adf02652b427210-focused), with earlier normalized-source closure through c1f455ee. Current exact/missing/mismatched/context filtering and repeated/opposing contribution tests use the same resolver core. | Existing reducer acceptance matched; no new cancellation rule or reducer. |
| 4. Derive dormancy on missing/unusable skill; restore only the same identity, preserving wound/effect/history bytes | T175 lifecycle16 and resolver44. WoundFocusedEffect_RestartAndAvailabilityDeriveActiveDormantDormantActive checks actual publication, restart, missing skill, different-case identity, exact restoration and five unchanged wound/effect/identity/History images. Pure resolver separately checks inactive/missing availability. | Existing lifecycle/restore acceptance matched, not a new fresh-process run in this environment. |
| 5. Mortal procedure uses exact sealed RollSkillId for resolved_skill_tier and null for fixed_zero | T175 SkillScopedRoll14 (20260905-185635-191-21872-60a477632d4d4f1fb44d50e7a70130f7-focused); prior Task6 pure46/SkillScoped13/Detached35/ColdClaim20/clone1 closure through887de325/ba633ab1/c1f455ee. Current six SkillScopedRollRows and actual persisted/publication/restart replay test retain exact/null source semantics. | Existing selected-skill and detached/live treatment acceptance matched; not an admission to repeat Mortal compatibility. |
| 6. Russian broad/focused/dormant/missing-skill projection without technical IDs or hidden effects | Plan Task7 RollScope10 and complete projection46, commits2bed5c4c/e33d8a32; T176 parsed examples4 (20260906-075339-943-2656-8c0f567a35154780ba815b419d014bcc-focused) and docs127. Current tests include inactive/missing/invalid/confusable/name-ID/hidden cases. | Semantic projection/wording/privacy accepted. ExplorerEffectPlayerCardBuilder passes Facts into UiKeyValueGrid; browser/console status previews use Name/Summary. This is not a new DOM/provider runtime or proof that every preview renders all scope facts. No concrete defect/new coverage mandate established by that distinction. |

These are recorded accepted results from September5-6, not one aggregate, not a new
Linux6/6 run and not a raw re-verification. Original raw was not restored here;
precise run IDs and accepted review findings remain in the original plan/tasks.
Do not run obsolete Fast/FullValidation/PreMerge commands from that history; current
category policy supersedes them. No test/build/discovery/runtime was executed for
this evidence map and no old PASS was rerun.

Bounded continuity read: EffectRollSkillScopeAuthority is unchanged since35a2c8070;
resolver c1f455ee-to-current differs only in MaximumCapturedRows visibility, not
algorithm; projection e33d8a32-to-current adds periodic_spend/gain branches only,
with scope projection unchanged. Lifecycle ffca1f6b9-to-current has20changed lines:
owned cleanup, newer publication-fault observer/assertions and narrower catalog
preflight. Its historical16 does not establish the current replacement fault-probe
runtime. That exact cut belongs to the existing T154/final shared-boundary receipt
reconciliation, not a new skill-scope gameplay branch or an automatic rerun.

Concrete conclusion: all six original US8 behavior families have matched existing
acceptance; no missing scope production branch is established. T177 parent remains
OPEN for coherent current final review/scans and exact shared-boundary evidence,
not because its unchecked historical aggregate command authorizes another suite.
Next independent required item is T149 ownership/retired-alias receipt reconciliation
from the same finite row5; use existing current source guards/accepted cutovers and
fix only a demonstrated residual path. The T154 probe-version boundary remains
explicit; R2/A5 stay UNKNOWN with no retry admission. DFR/Mortal/Windows HOLD and
both author ambiguities unchanged. Partial_success remains only accepted C2/cold
reopen at32ed759, not C4/Engine/UI. No code/cache/IO/authority or GM contract change;
no build needed for this documentation-only map. Independent document SOURCE and
normal publication/remote/clean-source closing follows externally.

# Accepted original partial-success wound correction — runtime32ed759

Independent Sol SOURCE/PREP/OUTCOME accepted the bounded original FR-022/025 case.
Source/runtime 32ed7595040ce811a3e949ae804ff48fd304fb2c. One existing predicate call
now recognizes partial_success for the same player pressure/maneuver operations;
profile/actor/current generation and all ordinary checks remain unchanged. No new
battle rule, failure inference, aggregation, parent witness/probe/schema or cache.
GM guide/worked example/manifest and typed guidance guard synchronized. No new
Mortal fields, shared protocol or daemon path; existing mandatory guide loading is
unchanged. No I/O/repeated computation introduced and no performance gain claimed.

Fresh Main64.705345/Unit35.070647/Integration68.826687s exit0, exact81/26/107 warning
multisets. Support/CrashHost unchanged; native package unchanged, not a new C++ build.
45artifacts/207inputs621copies/native6/4PEPDB510sourcebindings verified before/after.
Artifact5a99f0e0a4b27c74659662060438c52eb5a2a3107a7e5c601cc12055cb3da068,
PDB79c5caad3936a6b051932a3acec4941f28e849411e09206454f0ed3fb7a74b03.
M1 discovery1383categories/13225methods/noexecute retained; M2 changed no membership.
Exact current PlanOnly selected4descriptors/7methods/7cases, every original60s leaf.
Plan e335cca4635f17d10cf6090eac355a6645681abd65e3c0becc36be47b1432854.

M2-green7:7/7PASS in ONE selected run, fourTRXs/sevenunique cases:
- partial-success pressure/maneuver plus invalid ordinary-impact controls:4PASS,
  category13.2480542/60s. Pressure preserves source maximum0, no invented offer/none;
  maneuver preserves both strains, actual position/costs and cold C2 reopening.
- Existing exact pressure-success/source/none and maneuver-success/cold guards:
  1PASS each,11.4606368 and10.0443962/60s respectively.
- GM worked-example/manifest guidance guard:1PASS,3.2390281/60s.
Wrapper48.002032s. No skip/timeout/duplicate/underfill;12capture file hashes and exact
membership verified; both cleanups true, no newTMP or live managedowners. Unchanged
45/621/6/510 closing checks passed. Independent OUTCOME accepted the actual results.
Post50d8385c115080d83d178e78f0a5297a76b07b10a92dc898f5d1b61f165707b8,
summary05f5b482fc1cda09dd0cae4d612f3027cc4c60595ab93a7be33dabfb15b1643a.
Complete raw/builds/TRX/proofs and verified old15binary archive remain outsideGit:
/workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data/M2-*.
Source-only clean12file export/GitHubwholeblob readback18d4ebd54888426e6dc6f9b7345d816ea498358a37c1db3db8edecfeab987a84;
this is not fullenvironment/raw restoration. No raw/binary upload or data deletion.
M1 genuine2PASS/2FAIL remains separate causal evidence, not part of fresh7PASS.
One mistyped build SHA was rejected before output/archive/Popen; after checking
nonexecution only the exact current SHA was used. No duplicate actual build/test.

This closes only T081-D-PARTIAL-SUCCESS/C2, not C4/Engine/UI/fresh-process or whole
strain/#1536. The six-row finite original remainder below remains authoritative.
Both author ambiguities stay pending and cell-scoped; the three explicit deferrals
and general combat/QTE/defeat/ACTIVE stay outside this work. R2/A5 UNKNOWN and all
DFR/Mortal/Windows HOLD remain. US8 has six canonical scenarios (not the older
summary's eight); T167-176 are implemented, T177 is evidence/final review, not a new
scope subsystem. Independent Sol final3doc receipt review accepted. Publication,
remote and bounded clean source readback close externally after publication; tested
code/resources remain32ed759 and no passing suite is repeated for these docs.

# Original partial-success consequence correction — causal RED retained, GREEN UNRUN

M1 source a476a245440247337d8f725a376da66e4a6eb81d retains unchanged production.
Independent Sol SOURCE/PREP/causal OUTCOME accepted; three scaffold defects were corrected
before compilation: non-disposable source-owner misuse, pressure margin4 maximum0,
and retained completion-owner lifetime. These are preparation findings, not causalRED.
Fresh Integration exit0/71.93s/exact107warnings; Main/Unit retain d5bc lineage. All
45 artifacts/621resource copies/native6 and4PEPDB510bindings checked. Discovery
1383categories/13225methods, noexecute. M1 causal4 completed: ordinary negatives2PASS,
valid pressure/maneuver partial2FAIL specifically integration_required after ordinary
admission/actualIII selection/nonmutation. Category11.1265244/60s, wrapper18.136213s;
oneTRX/fourunique cases/exactselection/fullcapture/cleanup/noTMP/currenthashes verified. No timeout.
Post ebf35644d580343e22592eabcc1b5741aebf6704de104f4514dbd7d94ca78c56,
summary e04bb62308556461eb9a27a16b307494fdda9b9692b0d3ded13567e025f975fa.
Raw/build/proof paths remain outsidecheckout under spiritual-strain-preimage-data/M1-*.

Minimal fix now authored: reuse existing IsSuccessfulArtOutcome for the same player
pressure/maneuver branch. All profile/actor/operation/current-epoch and following
ordinary/source/cost/dice checks remain. No new rule/schema/probe/v2/cache/authority.
GM guide/worked example/manifest/typed guidance guard synchronized for partial8/4,
pressure maximum0/no fabricated offer, maneuver position-only/cold, no A/B.
Optimization: constant-time existing predicate, no IO or repeated work introduced;
no cache/speedup claim. Independent Sol whole SOURCE/tool selection accepted;
affected3builds, exact GREEN7 (new4 + oldsuccess2
+ guidance1) and independent GREEN OUTCOME stillpending. No C4/Engine/UI/freshprocess or
whole strain/#1536 closure. Six-row original remainder and all UNKNOWN/HOLD stay.

# Finite wounds-only remainder — 2026-11-03 04:50 UTC

This bounded source/receipt reconciliation supersedes the broader next-strain
engineering proposal below; it does not create new gameplay scope. Base and remote
36ec72e7e024385f1ee39b9f3cb998c84f26c3eb were clean, with no live build/test owners.
Sol/xhigh reviewed the non-US3 map; Astra/high reviewed original US3 obligations.
The historical b377 commit/tree exists locally but its spec blobs do not; no lazy
fetch was performed and no claim of byte-comparing that unavailable text is made.
Current normative FRs, original dated tasks and accepted receipts are the basis.

| Required result / original basis | Actual gap and finite closure criterion |
| --- | --- |
| US3 wound consequences, [FR-022/025](spec.md), [T080/T087/T081-D](tasks.md) | The selected player pressure/maneuver partial_success defect is now accepted at32ed759 (GREEN7 above); no extra failure-only strain is added. Remaining demonstrated existing-parent/III-IV failed-action cells belong to the original consequence contract, but do not require eleven new frameworks or a combat redesign. Close only after each applicable nondeferred cell has existing or targeted accepted evidence. |
| US3 lifecycle/source combinations, [FR-027/034-036](spec.md), [T081-C2/C3/D/E/C5 and T081-C3-R2B/T107](tasks.md) | Implementation and substantial receipts exist. Reconcile declared ceilings/guarantees, both sides, create/worsen, healing/root retirement, replay and cold cases against those receipts. Initial recovery plus insertion remains an unaccepted composition; use an existing lawful publisher seam if present, never invent general conflict orchestration. A checked parent requires exact matched evidence, not rerunning all accepted children. |
| [US2/T121](tasks.md) complete browser physical course, [FR-060/062/063](spec.md) | Code exists; R2 remains UNKNOWN180.1041994/180s without TRX. Close through cause-backed correction/admission and one complete original-budget result, with milestones, final wound/effects/resources/history and cleanup. Existing prefix/cheap36 is not final healing. |
| US2/US7 joint terminal treatment, [FR-012/063/064](spec.md), [T121/T152](tasks.md) | Code exists; A5 remains UNKNOWN120.0346224/120s without TRX. Close through cause-backed admission and exact joint acquisition+treatment/terminal/publication/rollback evidence. Anchor2/output10/controls3 and later accepted worsening remain separate accepted receipts. |
| Existing story and cross-cutting acceptance, [US1/4-8](spec.md), [T149-154/T177](tasks.md) | No additional absent non-US3 production branch established. Finish the finite scenario/receipt maps: ownership/retired aliases; healing actors/realm/catch-up; navigation/provider/confirm/stale/privacy; exact skill scope; GM examples/text surfaces; indexed bounds; surface-by-cut byte/existence rollback. Fix only substantiated uncovered defects. T150 mechanical legacy example already exists; eight requirement adapters are already connected; neither is a new implementation backlog. |
| Final coherent acceptance, [T159-165/T177](tasks.md), [original scenarios](quickstart.md) | Reconcile actual canonical FR IDs and quickstart scenarios, independently review findings and current diff, align GM/docs/checkpoint, then preserve reviewed sources remotely. Current category policy supersedes old Fast/Full/PreMerge aggregates. Merge/issue closure remain unauthorized. |

Two pending questions are cell-scoped, not global materialization blockers.
Ambiguous per-actor failure concerns evidence from the combat producer: the proposed
new GM field is not an original requirement and is not implemented. Multi-wound
sum-versus-maximum is directly a wound aggregation ambiguity, but only matching
multi-wound cases depend on it. Neither was explicitly included in the author's
three deferrals; do not silently deem either answered or deferred. Continue only
unambiguous single-wound cases. General defeat/T088, combat/QTE/ACTIVE/F1 and the
three explicitly deferred control/counter/art-III effects are outside this execution,
not marked completed. DFR/Mortal compatibility/Windows keep their separate HOLDs.

Selected independent correction T081-D pressure/maneuver partial_success is now
accepted above through existing IsSuccessfulArtOutcome and unchanged fresh checks.
It qualifies C2 owner/cold paths only, with no new parent/schema/cache/failure map or
aggregation. Next independent mandatory item is the six-scenario US8/T177 receipt
map using implemented T167-176 and existing versioned evidence, not rebuilding that
subsystem or rerunning unchanged PASS. R2/A5 still need cause-backed admission.
No honest whole-task duration is yet supported by those unexplained timeouts; the
main uncertainty is connected qualification, not a newly invented game feature.

# Accepted current-parent DATA query / runtime d5bc

IndependentSOURCE/PREP/OUTCOME accepted13typedDATA+4unchanged signedsource regressions
on d5bc3ced3d5b3c8ba843f34f90d6cb2aafbe5b4a, separate60s runs; fresh3builds0/exact
81/26/107warnings,45/621/6/510proof/completecapture/cleanup. Fullreceipt/optimization
and rawL1paths inplan. No existing-parent strain/v2/ordinaryprobemode or newrule
activated. Two precise author proposals remainunanswered in
strain-open-semantics-20261103.md; no recommendation accepted by elapsedtime.
Dependentconsumer widening needs those definitions; independent DATAstepcomplete.
Guardcap/no-roll/healing acceptedlineages and oldUNKNOWN/HOLD preserved; #1536open.
Finaldocreview/push/readback remains, code andresourcesunchanged fromtested d5bc.

# Current-parent DATA SOURCE correction before compilation

Astra found two fixture mistakes beforebuild: LastTransition has noEventRef member,
and its closedJSON cannotcarryeventRef. RemovedextraJSONfield, History row nowuses
Origin.EventRef forcreate andSeverity.LastChangeEventRef forworsen. Production
extraction otherwiseSOURCE accepted, Sol13+4selectionreviewaccepted; deltaSOURCE,
freshbuilds/exactdiscovery/Plan/PREP/runtime stillpending. No testhadbeenrun, no
fabricatedRED/PASS or activation ofunapprovedrules. PriorGuardacceptanceunchanged.

# Current-parent DATA extraction WIP / no new runtime

Existing active-wound query now shared with original re-trauma consumer; complete
catalog/owner/identity/latestHistory checks unchanged, no source/capability issued.
Newtyped13 and exactexisting source4 categories authored; C# UNCOMPILED/UNRUN.
Independent coherent SOURCE/freshaffectedbuilds/exactselection/PREP remain. No
existing-parent strain/v2/probemode/aggregation/outcome rule has been activated.
Two narrow author proposals saved in strain-open-semantics-20261103.md, unapproved;
parent/provenance work proceeds independently. Guardcap acceptancea78/runtime23dcd
remains, allrawexternal and oldUNKNOWN/HOLD intact. Mainplan holds exactfirststep.

# Guard-cap checkpoint closed; shared current-actor strain DESIGN next

Acceptedcheckpointa78c02e3, independentfinaldocreview, remoteSHA/cleanfourfile source
export/GitHubblobs verified; e2be032d2bdbaf9b4f1a6a64f9dac10832f81aa2ff43a0d23a72a2027c1fbb92.
Source/runtime e399MainUnit/23dcdIntegration unchanged, no activeowners. Exact47+2
versionedresults and separatebaseline1 accepted; whole #1536 notdone. Newtracked
T081-D-STRAIN-ACTOR-LIFETIME groups remaining side-aware consumer and ordinary-source/
current-parent provenance, DESIGN/read-only only. Solassessment found operation-aware
failure/side evidence needs precisecontractreconciliation; no rule chosen. Mainplan
has finitegoals, independentparentData canproceed without inventing combat semantics.
No runtime admitted. Threeauthordeferrals/generalcombatoutside/alloldUNKNOWN-HOLDstay.

# Accepted guard-cap wound route — runtime23dcd / productione399

Actualguard1PASS and affectedmaneuver1PASS independently accepted by Astra/high.
ColdC2/commonC4 realIII wound/effects/History/payments/stale +signedordinaryguard43
now qualify the bounded cap path; no A/B/extra source or guardrule change. Cheap47
on e399 and baseline1 on d16 remain separately accepted, not fresh50 or whole #1536.
Currentbuild/proof/timing/raw/failedattempt/optimization/readerlimits in plan.md.
Finaldocs/catalogstatus review and normalpush/remote/readback next; no runtimeleft
in this slice. Nextgroup is remaining actualstrainactor/III–IV/currentparent and
ordinary-source/lifetime integration under existing T081-D, boundedreadassessment
pending. Threeauthordeferrals/generalcombatoutside/oldUNKNOWN-HOLD remain unchanged.

# Guard-cap current: cheap47PASS, actual1 fixtureFAIL before C4

Newcode/runtimee399 has independentSOURCE and cheap47PREP/OUTCOME accepted. Actual
consumer+coldC2 pass their prefixes, then total4 event oracle fails(actual5): normal
opposition3→0 addsresource_depleted afterresource_spent. FullclosedFAIL retained,
no timeout/C4/nextturnPASS. Test-only strict5/4spent/unique exactdepletion correction
written, UNCOMPILED/UNRUN; review/freshIntegrationonly and re-admittedguard next.
Affectedmaneuver stillUNRUN untilguardPASS. Main/Unitcheap47lineage retained with
unchangedusedinput proof, no rerun. Fullreceipts/optimization inplan; rawK2external.

# Guard-cap code WIP — ordinary1PASS / genuine consumer1FAIL retained

Independent Sol/Astra OUTCOME accepted baseline/causal on d16 (5.46/60s and8.10/180s).
No timeout/preparation failure; actualcold/C4assertions not reached. Minimal owned
source-bearing guard proof, complete source equality, cap no-fields and privatev1
parent branch now authored with cheap12/GM sync, UNCOMPILED/UNRUN. Whole SOURCE,
freshMain/Unit/Integration, exactcheap47 and separateactualguard/maneuverPREP next.
No ordinaryguard-rule change, source-free relaxation, afterbroken shortcut or HOLD.
Complete evidence/optimization/lineages in currentplan, rawK1outsidecheckout.

# Wounds-only completion boundary — 2026-11-03 02:17 author status request

No-roll GM choice is accepted native source→coldC2→realC4→signedread on470e/final6d;
actualIII→I/root retirement accepted separately on25ea. No new implicit rules.
Finite remaining workstreams: (1) authorized US3 strain/source/lifetime cells,
currently concrete failedguard cap implementation; (2) full physical browsercourse
R2 UNKNOWN180.1041994/noTRX and jointterminalA5 UNKNOWN120.0346224/noTRX, code
exists but no acceptance or retryadmission; (3) existing woundstory/ownership,
privacy/text/index/bounds/byte-existence rollback evidence reconciliation and
substantiated defects only; (4) coherent final independent review/docs/FR acceptance.
No claim that every unclosed parent is unwritten code, or that only2 tests remain.
Three author-deferred consumers are excluded, generalcombat/QTE/ACTIVE outside
scope. DFR/Mortal/Windows retain separateHOLD, not automatically new implementation.
Main delay is unresolved connected qualification and repeated small fix cycles.
A trustworthy whole-task time range is not yet supported by causes for R2/A5;
no invented percentage/deadline. Continue finiteguard scaffold then existing plan.

# Guard-cap causal scaffold WIP

Two actual Integration Facts and exact categories written, production unchanged.
Baseline proves two genuine sources before the capped C2/C4 expectation; current
scaffold is UNCOMPILED/UNRUN. Independent SOURCE, fresh Integration/selected resource
proof, exact discovery/PlanOnly/PREP precede baseline60s then causal180s. No current
PASS or cap exemption. Runtime470e/no-roll119 versioned lineage remains accepted.

# Next wound-only slice: capped failed guard / design only

No-roll source/materialization accepted on6d847910(code470e), final2file readback
complete35ed82b5e5abb0c83d88760d7b8148d51e7c821728cc907ac7825d6361d0a112.
Next tracked T081-D-STRAIN-GUARD-CAP: independently proved ordinary guard3→broken
with new III burden should stay capped and preserve its real second source through
coldC2/C4/History. Concrete integration gap and bounded design accepted by Astra;
full finite plan in plan.md. No guard fixture/code written or run yet. Existing
source-free guard stays strict, no cap shortcut/new combat rule/old HOLD retry.

# Accepted no-roll GM margin, native C1–C4 — runtime470e

Real voluntary incoming harm with explicit GM10 creates I wound/effect/History via
cold C2/common C4; explicit0 completes without injury. Both pay opponent3 once,
retain original map/no dice claims, reject duplicate authority and validate signed43.
Actual2PASS and affectedcheap45PASS at470e, independent Astra/Sol OUTCOME accepted.
Full evidence/build/proof details and exact119distinct versioned union in plan.md;
not a singlefresh119 or whole #1536/Engine/UI/terminal/fresh-process qualification.
Original failures/raw preserved outside checkout, no raw/binary upload/deletion.
Next reconcile remaining wound-only functional/acceptance scope; do not reopen
old HOLD or author-deferred combat consumers automatically. Real III→I root
retirement accepted separately at25ea/72a; all remaining limits preserved.

# Current no-roll fix — DATA45 passed, producer wiring WIP

Cheap proposed classifier36+preservedresource/precedence9 passed on31371; actualpair
still2FAIL before publication onfd93. Existing two producer eligibility calls now
use current owned no-roll-free wrapper, preserving force and genuine-missing guards.
Wiring uncompiled/unrun, independent deltaSOURCE and fresh affected builds next,
then affectedcheap45 and actualpair180s admission. No no-roll wound PASS yet.
Raw outside checkout; prior67/source5 and oldUNKNOWN/HOLD retain their versions.

# Current no-roll boundary — source complete, publication pair FAIL

J3remaining3PASS/AstraOUTCOME accepted plus oldmissing2 source union; cheap67 retained.
Actual C4pair2 bothFAIL before publication (positive no closed interval, zero open
sourcefrontier). No timeout or no-roll wound PASS. Exact immutable evidence and
free-side resource-classification causal hypothesis are in plan.md. Independent
bounded design review pending; no new production correction yet. Do not rerun PASS
or actuals blindly; preserve all old UNKNOWN/HOLD and completed III→I acceptance.

# Current no-roll source boundary — cheap67 accepted, actual source incomplete

Code is saved; accepted J2cheap67 at0174, plus missing-choice native2PASS. Native
special test failed at its own nested-lease draft write; standard2 and C4pair2UNRUN.
Explicit held-lease byte-write correction is WIP/uncompiled/unrun, no production
change. Complete actual evidence, hashes and finite next steps in plan.md. Do not
count a source assertion prefix or terminal DATA as real wound publication.
Full raw outside checkout under spiritual-strain-preimage-data; old HOLD unchanged.

# T081-D-NO-ROLL whole implementation WIP

Code/GM docs/worked example/manifest and real owner/C2/C4 fixtures now written,
UNCOMPILED/UNRUN. Two initial SOURCE findings corrected (cast, malformed supplied
matchup); cheap current29 preserves original27 causal17PASS/10FAIL plus2new guards.
Final SOURCE, fresh Main/Unit/Integration, exact discovery/cheap GREEN and separate
real PREP remain. Existing H4 III→I acceptance and old UNKNOWN/HOLD unchanged.
Full proposed selection/provenance and optimization assessment in plan.md.

# T081-D-NO-ROLL source WIP after genuine RED

J1 causal27 on91ade:17PASS/10FAIL, independent Sol OUTCOME accepted; exact facts
and proof hashes in plan. Pure source-choice reader and existing producer/witness/
retained validators changed, still uncompiled/unrun. GM sync and whole SOURCE next.
No runtime qualification of new source/owner/C2/C4 yet; accepted H4 separate.
Raw outside checkout, old UNKNOWN/HOLD and author deferrals unchanged.

# T081-D-NO-ROLL scaffold WIP

Astra DESIGN accepted with boundary/RetainedPayloads/authority corrections in plan.
New typed Unit27 and exact category written; production unchanged, uncompiled/unrun.
Next scaffold SOURCE/fresh Unit/discovery/exact PlanOnly/causal RED, then minimal
producer+witness/GM contract and original-owner qualification. Existing H4 accepted
72a/runtime25ea unchanged. No new cache/I/O; raw outside Git; all old HOLD retained.

# Next functional step — no-roll GM margin, DESIGN WIP

Accepted prior checkpoint72a300fdc (runtime25ea) remains clean/accepted. Current work
is only the bounded plan for the approved GM-selected harmfulMargin producer:
explicit per-affected-side values in existing exchange/witness/receipts, empty
no-roll claims, existing calculation/caps/owners and cold replay. Existing lawful
negotiate/voluntary incoming-harm fixture is available. Independent DESIGN before
code; all new no-roll tests/runtime UNRUN. Technical schema is a proposal in plan.md,
not an additional author game rule. Old UNKNOWN/HOLD and author deferrals unchanged.

# Current accepted wound route — 2026-11-03

Linux runtime25ea8b0b5b8ba5339e1b1ab4d0602c7530293ca5: decoder fix cheap12GREEN and
actual III→I/root-retirement/signed44 case1PASS independently accepted. Real combat
healing preserves the original source/create History after old strain roots expire;
no terminal/UI/fullfamily claim.13distinct across2runs on this source, not a combined
fresh run with earlierG4/H2. All3builds0/baseline81/26/107,45/618/6/497 verified,
fullcapture/cleanup/noowners. Exact timings/proof hashes and inherited proof-field
clarification in current plan.md; raw /workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data.
H2nativeFAIL/H3causalRED and compilation failures retained; no evidence erased.

Next functional source gap: explicit GM-chosen no-roll harmfulMargin in authoritative
source/retention/admission, no default0 or invented formula/dice. Author decision
resolved; implementation remains open. Existing source/services/guards and targeted
causal checks must be reused. Author deferrals and all old UNKNOWN/HOLD remain;
whole #1536 is open. Continue wounds only, no general combat/QTE/ACTIVE/F1 expansion.

# T081-D signed-byte correction — genuine cheap RED accepted; production fix WIP

At70253ea1cd0ae021025dade2b77df40d613fc95c, fresh Unit37.716811s/exit0/26baseline,
45/618/6 and4PE/PDB494bindings verified. Catalog1368/13202 valid; exact official
H3-causal12 completed12:7PASS/5FAIL, category3.2505363s/wrapper9.561332177s. All
three published-BOM inputs failed reuse; both invalidUTF8 inputs incorrectly passed
resolver after successful valid/replacement-decoded prerequisites. DoubleBOM2,
identity3 (including same request42at43) and sharedcodec2 passed. No fixture failure,
exception leak, timeout/skip/duplicate/underfill. Sol/xhigh accepted causal OUTCOME.
Post67cb790e81dacd0174a0d14a80d231f13e2371677e72ca0429c3d450ba3c9dee;
summary5d66c77299003546d82f4e96bc29e4201671c5ac70cc75cd08c0593d3f4eeaf6.
All posthashes stable/cleanuptrue/noTMP/owners. Full H2actualFAIL and H3RED retained
outside checkout at /workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data.
Seven source readback proof6300de9d935ca9bccdefb0f2a63aa9f9c54c63cd8c1cf0d1d66b7776fe96bd59.

Astra/high DESIGN accepted the bounded six signed-byte reads across five production
consumers: existing CanonicalJsonUtf8.DecodeOneOptionalBom, fail-closed catches for
InvalidDataException in creation reader/resolver/source-only reducer; ordinary C3
already catches it, prior-wound lookup uses immediately validated immutable images.
No raw byte/hash rewrite, permissive parser or ledger/request guard change. The
separate fixture bug now uses one unique healing43 request constant consistently
for staging/snapshot/published proof and a distinct readonly44 request. Unchanged
first-create/sources/decisions/expired-root/result/spend assertions remain required.
Production diff15insertions/15deletions plus checkpoint is UNCOMPILED/UNRUN. Next
whole delta SOURCE, fresh Main/Unit/Integration, exact cheap12GREEN, then separate
PREP for one actual180s. H2cheap3 methods/helpers unchanged; no justified repeat of
old G4/state/cache suite. Old actualFAIL is not erased or accepted by a cheap GREEN.

Optimization/GM: reuse existing strict decoder, no cache/new framework, extra source
read or JSON parse. Existing repeated parsing in prior-wound lookup is unchanged;
this narrow byte compatibility fix adds no duplicate work and makes no speedup
claim. Immutable before-image bytes, original fingerprints, source/lease/currentness
and History checks remain fresh. This is client-owned decoding of the format the
current writer emits, not a GM payload/command/rule change; guide/example/manifest
remain valid and no GM update is needed. Original explicit harmfulMargin producer
is still open; author deferrals and all older UNKNOWN/HOLD remain unchanged. Actual
weakening/root retirement and whole #1536 are not yet qualified.

# T081-D actual weakening — H2 actual1 FAIL; byte-boundary causal scaffold WIP

Scaffold review found the receipt malformed-byte negative initially failed on an
unrelated unknown field. It now reuses the typed resealing fixture for a retained
coordinate containing U+FFFD; one literal UTF8 replacement character is replaced
with0xFF. The replacement-decoded equivalent must first resolve successfully, then
invalid bytes must refuse without exception leakage. Conflict negative likewise
has a successful decoded-equivalent baseline. No reflection/new sealing algorithm,
production change or runtime yet. Shared test resealer changes private→internal
only; actual signed owners remain outside this DATA fixture.

Astra/high accepted the minimal existing-decoder DESIGN. Cheap selection is now12:
first-offer10 plus two existing CanonicalJsonUtf8 facts (pure shared codec, not Mortal
compatibility runtime). Existing duplicate-request fact also refuses reuse at43,
without adding a case. Production still unchanged; no cheap or native replay has
run. Same immutable signed-byte boundaries/raw hashes and fail-closed diagnostics;
no parsed-result/cache framework or ledger coherence relaxation is required.

H2 sourcec377cd8c2abf3f2e43d3f6ba8e3763f4dca2b35e fresh Integration0/107baseline,
4PE/PDB492bindings; cheap3 accepted PASS. ActualH2-healing1 completed1FAIL in
37.4027720s TRX/40.8061659s category/46.906321182s wrapper, no timeout/skip/underfill.
First real C2/C4 creation and Stage43/snapshot/advance succeeded; line100 first
healing CommitC2FirstTransport fails signed original creation receipt parsing,
BEFORE healing ledger append. No healing C4/root retirement/result/signed44 reached.
Astra/high independently accepted failed OUTCOME;45/618/6/492 posthashes stable,
cleanuptrue,newTMP[],noowners,cleansource. Full raw H2-healing1 retained outside
checkout under /workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data.
Post9f19bd1d39fb2b6e70b4040844f287ee177797f9d415b81f9fa2bb09e4028feb;
summary8cf56c92a7f9cc941d2caca1daa01b36a2f8ede3f228ced9187828228ff7bcc1.

Independent source trace: common C4 writes UTF8 BOM via WriteCanonicalFileAtomicAsync
and EncodeUtf8WithPreamble. The signed creation reader's strict UTF8.GetString keeps
U+FEFF; strict string JSON parser rejects it. Ordinary ReadJsonAsync strips BOM, so
prior assertions succeed. Raw post-C4 bytes were not retained after owned cleanup;
this is a deterministic writer/reader diagnosis consistent with exact failure,
requiring a cheap actual-consumer RED before production correction. A separate
fixture defect reuses request42's ID for healing43: resolver/append correctly reject
reuse. Assign unique IDs consistently to stage43/snapshot43/published proof and44;
never loosen request/ledger coherence. Neither correction has been applied yet.

New cheap scaffold adds seven cases to the existing three first-offer DATA controls:
published BOM at receipt/conflict/both must reuse exact instance, malformedUTF8 and
doubleBOM at either boundary must return refusal, immutable byte fingerprints stay.
New category ten cases/5methods/60s; tests UNCOMPILED/UNRUN, production unchanged.
Next independent scaffold/selection review, fresh Unit only and exact causal10 RED;
then existing CanonicalJsonUtf8.DecodeOneOptionalBom at signed wound receipt/conflict
consumers (creation reader, resolver, C3/source-only reducers, prior-wound lookup),
preserving fail-closed exceptions, strict parser/raw hash/currentness/ownership and
History guards. No new parser/cache/diagnostic stack, byte rewriting or game rule.
Decoder is cheap existing one-pass work, adds no filesystem read or JSON parse and
no caching/speedup claim. Existing immutable owner/readset DATA retained. Then fix
request IDs, whole SOURCE/fresh affected builds/cheapGREEN and separately admitted
one actual180s repeat. Prior G4 accepted29/H2cheap3 keep their source lineages;
new helper code and whole weakening route remain unqualified. Old UNKNOWN/HOLD,
no-roll producer and author deferrals retain their distinct status.

# T081-D actual weakening fixture — source WIP / UNCOMPILED / UNRUN

Fresh H1 Integration on6adbff436b92a5b7dd8e64c0a340916775efda52 failed compilation
in59.799097s with2 test-only API errors at135/136: EffectIdentityState.Parse requires
JsonElement, and its result exposes State/Issues rather than IsValid. Corrected to
a scoped JsonDocument, Assert.Empty(Issues) and typed non-null State, keeping exact
expired-root assertions. No tests launched, no product failure/causal RED claim.
Failed build log and old artifact archive retained outside checkout. Delta SOURCE
and fresh Integration required; all new cheap3/actual1 remain UNRUN.

SOURCE review caught a pre-build test compile defect: ResourceHistoryState.ToCanonicalJson
returns text. The cheap count now uses existing typed Transitions.Count, with no
serialization/reparse. No compiler/test has run on this WIP; this is not causal RED.
The independent route audit confirmed appended overall ordinal3 is turn-local0,
8/8→2/3→0/1 costs and position-adjusted opposed19vs7 (separate healing25vs16),
explicit guard component_003/I graph with pruned trigger IDs. Final delta verdict
pending; new cheap3 + actual1 suffice, no unchanged G4/cache-matrix rerun required.

Base accepted checkpoint6d465cec55a770fa68c0b5bbbe639bdc1635d459, runtime2bf6f7b.
Astra/high accepted all remaining3 OUTCOME and bounded next DESIGN. New code changes
only Integration fixture inputs/coverage, catalog and checkpoint: optional capacity
inputs default to old0/6, nondefaults explicitly excluded from existing prepared
cache, true formula/initial History8 before first snapshot, custom original seeder
sets healingIII and settled clock/scheduler. Existing strain helper optionally
authors corresponding ordinary cost balances before first C2; defaults unchanged.
New actual test uses actual owners for real III parent creation and common C4,
then separate staged signed combat healing43, appends at actual next ordinal,
authors the actual requested I graph with consistent component/trigger references,
executes automatic C2/common C4 and checks expired original effect identities,
replacement graph without strain, retained first-create fingerprint/original source
rows, actual persisted healing result/spend, no safe cycle and signed turn44 read.
It is III→I weakening, NOT terminal healing, Engine/UI or fresh process evidence.

New cheap3 prove ordinary6 and lawful8 resource formula/state/History plus template
exclusion with nondefault focus or opposition maximum; no filesystem/world setup.
No production C# or GM contract change. Existing Main/Unit/Support/hosts stay on
checked unchanged code; only Integration needs fresh build. New C# uncompiled;
all new tests UNRUN. Delta SOURCE must assess complete two-turn setup, signed
ownership, effect-root lifecycle, graph shape, old-history retention and selection
before fresh Integration/discovery/PlanOnly, cheap3 and separately admitted one
actual180s. A fixture failure is not causal RED for production; preserve any failure
and explain it before retry. Do not repeat accepted G4/old72 without a relevant cause.

Optimization: existing immutable prepared tree remains owner/key/lifetime unchanged
for default capacities; custom capacity is deliberately fresh because no established
key/template includes its source inputs, and this single cold native path is the
subject. No new cache or speedup claim. No production source read removed; all live
C2/C4/snapshot/integrity fences remain. Full diagnostics outside checkout; no raw
or binaries in Git. No-roll GM-choice producer and all other existing UNKNOWN/HOLD
remain open; three author-deferred consumers stay deferred, not blockers.

# Current T081-D — connected route29 PASS, independently accepted

Linux source2bf6f7ba144a296cfb162943075dbd463bc1a4aa: fresh Main/Unit/Integration
exit0 with exact81/26/107warnings,45/618/6/488 verified. G4-cheap25 and maneuver1
independently accepted; pressure/publication/rollback each1PASS with complete
capture/cleanup/hashclose, their independent Astra/high OUTCOME accepted.29distinct same-source
cases across5runs, not fresh72/fullfamily. Signed next-turn, top-summary allowance,
mechanics/turn refusal and staleC4 actually executed. G3 causal FAIL retained.
See current plan.md table/proof hashes; raw /workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data.

Next separate existing-owner healing fixture: legal initial8AP/tierIII BEFORE
snapshot, genuine C2/C4 III→I and retired original strain roots, then signed old
history. Code/runtime pending; no manual refill/clear/rootdelete, no actualterminal
claim from DATA. No-roll GM-choice implementation and whole strain/#1536 remain
open; author deferrals and older UNKNOWN/HOLD unchanged. See plan.md bounded design.

# Current T081-D result — real maneuver next-turn FAIL; adapter fix WIP

G3-maneuver1 on9b56154a661b6e66beb004ee782cde29f31e71e1:1FAIL/34.5231809s,
no timeout. C2/C3/C4 assertions before line91 passed; next signed turn fails because
raw display Chaos Sea did not match retained chaos_sea. Astra/high independently
confirmed this narrow adapter defect; whole route remains unaccepted. Complete
closing proof58063306dd0d2a09f82c07c1578d04978ac4a025d1409cf99e38c2343f2f4322,
full raw /workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data/G3-maneuver1.
Minimal existing-normalizer fix and typed display/foreign/malformed regression are
written, UNCOMPILED/UNRUN. Next delta SOURCE, fresh affected builds, cheap9+historical16,
then separately admitted actual180s repeat. Full details/optimization/GM no-update
rationale in plan.md current entry. No healing/removal/full-consumer claim; all older
UNKNOWN/HOLD and author deferrals retain their distinct status.

# Author answers applied — 2026-11-02 20:53 UTC

The four questions are resolved: explicit GM discretion for voluntary no-roll
harmfulMargin (no default0/new formula, retain/validate authoritative choice);
control_burden, counter_burden and III restrict are author-deferred, NOT implemented
or completed and NOT current wound blockers. Their questions belong to the future
spiritual combat revision; registered identifiers stay. IV forbid and the separate
strain consumer continue. All existing runtime UNKNOWN/HOLD remain unchanged.
See resolved author-decisions-20261102.md and formula contract; no-roll admission
implementation/qualification remains OPEN, not an unanswered author question.

The single authorized [GitHub #1536 follow-up comment](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536#issuecomment-6588440901) was posted and independently read back (id6588440901).
No issues are closed. Historical pending wording below is superseded by this
current decision, not a new permission to implement the three deferred consumers.

# T081-D connected route prerequisites — accepted versioned72; actual4 UNRUN

Compiled production and Integration source dbefd92b8219ff5064989dd32373718af556c259;
corrected Unit source592036233a1d7806f2d58b1888e84debff0dbc73. Complex Astra/high
SOURCE accepted; independent Sol/xhigh delta SOURCE/PREP/OUTCOME accepted. Actual
G1 yielded44PASS/8fixtureFAIL/20UNRUN; the one approved private fixture line adds
the existing nonexistent-before fingerprint, with production/assertions/attributes/
membership/resources unchanged. Fresh Unit36.548113s/exit0/26baseline; no unnecessary
Main/Integration rebuild. G2 remaining28PASS: History8/parent7/intrinsic13, three
complete descriptors, no skip/timeout/duplicate/underfill. Category4.0567377/
4.2228876/3.2467234s; wrapper17.639279259s.44earlierPASS +28currentPASS have verified
disjoint semantic case names covering approved72 exactly. This is a two-version
union, NOT a fresh72 run. The8fixtureFAIL and initial20UNRUN remain historical;
actual maneuver plus affected pressure/publication/rollback4 are still UNRUN.

G2 summary9fcff588342a6cfab97b5020b07d4f8a51c5125a65e9a62c5d1e333a0205d7cb;
post10a3c88d034101e92fe76e05fdb364f61a81287c60d1dae35602dc70c0db20ab;
uniond222160c69d2282e492a77d7aaa5a15f8d9574bd535fa5923766b164eddab1e2.
Current artifact4b318ad8b54b3c0d801f19891cc72ca456002d10c4e51213a005829452fd2a4f;
PDB287dbc0cfd2b60917478a250e1c926deb467cfdfae1f4db350054745de39c026;
45managed artifacts/618resource copies/native6/4PE/PDB488source bindings verified
and stable after run. Source recovery4changed files GitHub immutable-ref byte-exact,
proofd273759a0579a94fdd1ca9c67dddb9108fa11f5767b6fb07275c4de97defd1eb; remaining
26source proof unchanged except reviewed fixture line. These are bounded readbacks,
not full clean checkout or raw restoration. Owned/runtimecleanup true,newTMP[],
no active owners, clean frozen source. Raw full TRX/logs and all failed/passing
attempts retained outside Git at /workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data.

Optimization/source impact: only one private Unit History fixture body changed;
Main/Integration/current resource sources are byte-identical to dbefd. Comparison
and full-parent fingerprint remain immutable DATA per existing owner/frame; no
cache/authority/integrity fence was removed and no speedup is claimed. UI/provider/
fresh-process, actual gameplay healing/root removal, no-roll GM-choice producer,
full strain consumer and old UNKNOWN/HOLD remain OPEN; author3deferred effects are
not completion blockers. Next exact singleton plans/resource/PREP, then one real
maneuver C2/C3/History/C4/signed-next-turn in existing180s budget. Only after its
outcome proceed to affected pressure C2/publication/rollback controls; no blind
retry or limit increase. Current checkpoint delta is docs only, so reuse the exact
compiled code with checked delta rather than rebuild for this result text.

# T081-D G1 prerequisite run —44PASS/8fixtureFAIL/20UNRUN, narrow correction WIP

Source/build dbefd92b8219ff5064989dd32373718af556c259 has accepted complex Astra/high
SOURCE after the exchangeAtTurn fixture correction. Fresh Main/Unit/Integration
exit0/exact81/26/107previous warnings;45artifacts/618resource copies/native6 and
4PE/PDB488source bindings verified;26immutable GitHub changed files read back exact.
No new native package code/Support/Bridge/CrashHost source change. Discovery1365/
13196 executes no tests; exact cheap72Plan SHA d8cf5e5042a2889ce761c9707fbeb357c63f086d044cd71e5be942dff24a171f.

One admitted official G1-cheap72 completed52cases:44PASS (manifest1,witness21,
comparator5,guidance1,historical16),8FAIL in HistoryData baseline, remaining
parent7+intrinsic13=20UNRUN. Not timeout/skip/duplicate; whole selection incomplete
because runner stopped at failed descriptor. Owned/runtimecleanup true,newTMP[],
no active owners,clean frozen source/posthashes stable. Full evidence outside Git:
/workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data/G1-cheap72.
Failure post1137495b4937bc2e909683f1b146a792a8109427dc893c392fa482e7780cab9c;
summaryf4083c381fbd514454873273f4aa135ddc7fd83c676756d062cbf3e0bcc7e556.

All8 failures occur before HasExactStrainHistory at test History helper105: the
new fixture reused command-oriented CreateTransition whose beforeFingerprint is
null. Canonical persisted creation History requires the wound-specific sealed
nonexistence fingerprint (WoundHistoryState.ComputeNonexistentBeforeFingerprint,
existing WoundHistoryStateTests.Transition1043); nextOrdinal issue is secondary to
filtering the invalid row. This is fixture FAIL, not causal RED or production owner
failure. Whole new fixture checked for hashes/IDs/ordinals/turn/terminal and chain
rules; synthetic heal requires no typed result and keeps exact before/after chain.
The only C# correction sets beforeFingerprint using that existing function. No
production/assertion/limit change. Corrected Unit C# UNCOMPILED/runtime UNRUN.

Next delta review/fresh Unit only (Main/Integration/dbefd code and operational
resources unchanged), exact remaining28 (History8 plus unrun parent7/intrinsic13).
Do not rerun44 without relevant change; preserve their dbefd provenance. A genuine
72distinct union across versions needs checked disjoint case identities and source
impact proof, not an invented fresh72 summary. Actual maneuver and affected
pressure/publication/rollback4 remain UNRUN until cheap union OUTCOME and separate
PREP. Actual gameplay healing/root removal, no-roll producer and full consumer
remain OPEN. Author3deferrals and all prior UNKNOWN/HOLD unchanged. No raw/binary
commit or old measurement deletion. GitHub is source/checkpoint only.

# T081-D connected route tests/docs — frozen WIP, runtime UNRUN

Astra final SOURCE found a fixture-only marker error before any build/runtime:
HasPriorTurnMarker recognizes exchangeAtTurn, not turnNumber. The raw preselection
fixture now sets exchangeAtTurn42, and its refusal mutates that marker to43.
Production marker policy/assertions are not broadened. No failed runtime is being
reclassified; all current code remains uncompiled/unrun.

Cohesive test delta now includes reused real C2 helper with maneuver option and
ordinary turn42 marker before first selection, full cold A/B + common C4 + signed
next-turn scenario, typed History8 and GM guide/worked example/manifest/guard sync.
Pressure helper default behavior and old first-A assertions are preserved; the
ordinary maneuver preparation is one shared helper, with no post-acceptance setup.
Standalone terminal-History DATA is explicitly not real healing/root removal.
Actual healing/removal and broader family coverage remain OPEN.

Proposed exact selection76 on this source: Unit55 (parent7/intrinsic13/witness21/
History8/comparator5/guidance1), Integration cheap17 (manifest1/historical16), then
new actual maneuver1 and affected old pressure C2/publication/rollback3. Discovery
must establish actual counts. Budgets unchanged for existing categories; new
actual publication180s matches existing C4 controls (entry60s untouched). New pure
History60s and selected historical16/120s are bounded independent guards. No all
suite, old HOLD or general combat/Engine/UI run. Cheap prerequisites precede the
actual scenario; no runtime yet. Current C# UNCOMPILED, final complex SOURCE/PREP
pending; partial Astra/high production review ofd601 found no actionable defect.

Cache/optimization identity: immutable ordinary comparator lives in one selected
C2 capture and is returned only after fresh owner/epoch/ordinal/actor/component/
stack checks. Full parent fingerprint is immutable parsed DATA, not a permission;
new ledger/image/context invalidates by constructing new DATA. Signed history
roots use the existing authenticated snapshot service and must remain covered by
its full manifest/dependency fences including cached frame consumers. There is no
new cache/static registry or diagnostic stack, no measured speedup claim. Effects,
current availability, physical/source integrity and publication gates stay fresh.
No raw/binaries in Git; full evidence remains external. Prior41 and all historical
RED/FAIL/UNKNOWN/HOLD/author deferrals remain exactly limited to their own versions.

# T081-D connected failed-maneuver route — uncompiled WIP

Continued after accepted parent41, not a completed gameplay claim. Current WIP
connects pre-selection ordinary maneuver/setback and actual parent creation to
C2-owned comparison metadata; retains one immutable ordinary comparator instead
of reparsing it on each ownership read. Exact current actor/root/component/stack/
resource epoch checks remain fresh. Private source witness carries the six-field
comparison from the owned selection, and sealed C4 records detached ordinary-after
DATA only after successful actual source admission. Ordinary maneuver comparison
is selected only from live owned mechanics, exact sealed publication, or matching
signed pre-turn ledger/History, never a caller-provided permission.

C3 checks newly appended parent facts against actual owner-sealed insertion and
completed History; publication and signed-origin validation check full first-create
History.AfterFingerprint. Parsed parent canonical fingerprint is computed once
from immutable full envelope DATA and reused, while History/source ownership is
independently checked. No new cache/framework or current permission is retained.
Historical frame reads the two extra signed roots only when its baseline contains
a maneuver strain delta, via existing authenticated snapshot reader once each.
It reuses the existing exact/top-level optional-summary historical comparator and
requires the same unique open instance. Candidate images cannot replace baseline
receipt/History. Malformed shapes retain ordinary refusal, not a new fallback.

All new C# UNCOMPILED/UNRUN, SOURCE pending. Full cold/next-turn/healed history,
changed-parent/source/definition/ordinal/foreign owner refusals and actual C4 must
be qualified before any acceptance. Existing real first-A RED5a and parentE1RED5
remain causal evidence; no blind original rerun has occurred. Next finish cohesive
route tests, GM docs/worked example/guards and complex independent SOURCE review,
then fresh affected builds and selected cheap guards before actual runtime. No
HOLD/old UNKNOWN or author-deferred consumer is enabled by this WIP. No-roll GM
choice producer remains separate OPEN. Accepted code/runtime remainsb1a3 and41.

# T081-D retained parent relation — accepted41, connected owner route next

Runtime/build source b1a3a42b6c72e6940cbf043f3b97c3c4e619ed7e; independent Sol6.1/
xhigh SOURCE, PREP and OUTCOME accepted. Exact official E2-green41: parent7PASS,
intrinsic13PASS, existing witness21PASS; three complete descriptors/41cases, no
skip/timeout/duplicate/underfill. Budgets unchanged60/60/120s; actual categories
3.2126556/3.2165646/3.0433419s, wrapper15.940724278s, summarywall14.4084206s.
Parent negatives now also reach null-state and unchanged-input assertions. The
repeated13+21 are affected controls, not extra unique coverage to add to prior34.

Fresh Main/Unit/Integration0errors, exact baseline warning multisets81/26/107;
build65.197239/35.786218/72.309782s.45artifacts/618resource copies/native6,
4PE/PDB480source bindings; post hashes unchanged, owned/runtimecleanup complete,
newTMP[], no active owners, clean frozen source. Discovery1362categories/13191
methods executes no tests. Artifactproof0568e6c122fb39681d3fc2d8034c1080ed912b844a5ad1866df39a2f2f9ebc28;
PDB544f74464c37dbe8c447b9552b876805ab4f86285477b3266146922373122528;
Planbe488b2ff442413380db5f503aa480db29ca1e1d3ec3edb9cb287c84a1edeab4;
summarycdf1833e6e913c373607bae76c55b228f8c40f1520516ed27930831227b6b98c;
post2e7067c785ac62016e61a8a5fe5564d98539d6f3ab04ea70cb868f5570d9c799.
Raw /workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data/E2-green41;
all earlier causal/build evidence retained outside Git. No raw/binaries staged.
Independent GitHub immutable-ref10changed-file readback matched every byte/blob;
proofa1bc89812b73130cd8ed7cffdd06d5768eaf69cc149db17fa517e38e99a9bd0b.
This is limited changed-source readback, not full clone/clean-checkout/raw recovery.

Parent relation checks same instance, original context, earlier source/exchange,
affected actor, actual materializeIII/nonretrauma, exact wound/transition/rank and
origin source/event/opportunity. Witness validation returns already parsed DATA
once; parsed ledger privately retains it and carries it across detached append.
A temporary decision-bijection index has parse-local lifetime; it is no cache or
permission. No fresh owner/authority fence removed and no speedup claimed. This is
client-owned receipt DATA only, with no new GM-authored field, rule or command;
existing GM strain example/instructions stay unchanged. No additional prompt or
worked example is needed for this internal relation check; actual capability
wiring will recheck the GM surfaces separately.

History.AfterFingerprint/first-create and actual source ownership remain unproved
by this block. No live C2/C3/C4/signed-history adapter exists yet; failed maneuver
full route is OPEN. Next connect actual pre-selection ordinary evidence and full
parent snapshot through existing capture/receipt/History owners and sealedC4,
then validate next-turn and healed-history behavior with required refusal checks.
Author no-roll GM-choice producer/qualification remains OPEN. Control/counter/III
restrict are author-deferred, not completed/not blockers. IV forbid and strain
remain. Old browser-course/terminal/DFR/Mortal/Windows UNKNOWN/HOLD unchanged.

# T081-D intrinsic retained comparison DATA — accepted bounded gate

Built/runtime source: 70322ab17e4a3e8224b161de2556b1cc0abee06d. Independent
Sol6.1/xhigh SOURCE, exact PREP and OUTCOME accepted. D3 official selection:
`wounds-spiritual-strain-preimage-data`13PASS (3methods,60s budget,3.0130158s)
plus `spiritual-combat-guarantee-witness-consumers`21PASS (7methods,unchanged120s
budget,3.0605945s). Total34/34, no timeout/skip/duplicate/underfill; both descriptors
and owned/runtime cleanup complete. New TMP roots[], no active runtime owners,
clean frozen source and all post hashes unchanged. Genuine d174 closed-field DATA
RED remains separate from the earlier D1 fixture canonicalization FAIL and from
real first-A failed-maneuver RED on5a. No already accepted old5/20/C4 set was rerun.

Fresh Main/Unit/Integration XML builds exit0, exact previous warning multisets
81/26/107 (76.872828/37.867460/76.426538s). Preliminary7f08 Main82/Unit26 had one
new CS1573; the XML-only parameter fix is verified and final warning count returns
to baseline.45managed artifacts/618resource copies/native6 and4PE/PDB477source
bindings verified. Only expected changed operational resource is the GM wound
contract. Catalog discovery1361categories/13190methods, no tests executed by audit.
Exact Plan SHA d6dc300ffbd05f6db9014f9b443acefc88cac76286be30ff471b30a25a74fc7d.
Artifact proof25c7f92c4298f7b3997aaf3a57f7c754737ad676d8ed15270c64c07604d51c5b;
PDB proofc8aaa7c06f271940ac5e4acde20fee64fe5fc8264e630304d8ab9da90f161fbf.
Summaryec8c1ea723c8dfa8da3c6be75e104f48d0e8279e22d9879d180c11e24999a303;
post proof3b49d7aab89667663dd435b23491542eb50d2e89710c661ce687a38f3246faa2.
Raw: /workspace/tboe-linux-evidence-20261102/spiritual-strain-preimage-data;
D3-green34 contains both full TRX/capture, prior RED/FAIL/build evidence retained.
No raw, generated proofs, archives or binaries are in Git.

The pure DATA reader retains the exact private actual/ordinary snapshots and full
parent envelope; it reuses the existing exact capped+1 comparator. Only retained
maneuver validation sees detached ordinary-after. Actual dice/cost/control/strain
math remain actual. Rehashed changed/foreign/malformed preimages and changed
actual-exchange inputs are rejected; mutating a returned copy leaves retained DATA
unchanged. A valid-looking foreign parent ID is NOT proved by intrinsic parsing:
parent receipt/first-create History, source ownership and current availability are
still required. No live/C3/C4/historical owner adapters or joins are implemented;
real failed maneuver full route remains OPEN, not qualified by34PASS.

Optimization: one bounded object owns already parsed immutable comparison DATA
for one witness validation, with detached output and no cross-operation reuse.
No new cache/static registry, hook or authority is retained. This cheap parse is
not a claimed speedup; current owner/source/history/effect fences stay required.
Independent immutable-ref GitHub readback verified all14 changed files byte-exact
in a new external directory; proof581a8a2672d0ca27362e00eaceca5df7590f87189682fc64a1f675bc6db8cc8d.
This is bounded changed-source recovery, not full clone/clean-checkout/raw recovery.

Next: connect the genuine pre-selection ordinary proof/full parent snapshot to
current C2 owner, persist exact source witness and prove earlier parent receipt
plus first-create History, then sealed C4 and signed-history adapters. Reuse the
existing capture/receipt/History owners; no new game rule or diagnostic framework.
Do not rerun real first-A until its consumer changes. Full next-turn/healed-history
route needs its own guards/qualification. Author no-roll GM-choice producer remains
OPEN; control/counter/IIIrestrict are deferred, not current blockers. Old UNKNOWN/
HOLD including browser-course/terminal/DFR/Mortal/Windows remain unchanged.

# T081-D retained strain preimage DATA — causal tests WIP

D1 on 8c127c3b7e463f48d42d828df6bb66579b6d01dc is a fixture FAIL, NOT causal
RED: parent-source prerequisite rejected noncanonical semantic exchange bytes
before the new-field subject (TRX0.0463672s, no timeout). Production.Payload already
requires canonical semantic JSON. The two exchangeJsonBase64 fixture encodings
now use the existing Canonical serializer; retained ordinary/parent preimage
strings remain unchanged. No parser/assertion relaxation. Post proof
05cddfbab2acffc4a1f516fb13b2717947697a31e7555e61fd5e4b35b3c60d20 verifies complete
failure/45artifacts/618copies/native6/473bindings/cleanup/clean frozen source.
Corrected C# UNCOMPILED, next attempt UNRUN; field/core/history controls UNREACHED.

Current preserved source5a027b8fc325e176451d28a926d80d4de98065fc has accepted
DESIGN (Astra closure) and a genuine real failed-maneuver first-A causal. Actual
F1 Integration build exit0/107 unchanged warnings/73.625272646s; unchanged Main79d/
Support. Artifact25/resource412/native4/3PDB385bindings verified; discovery1360/
13187 and exact1Fact60s Plan/PREP. One complete FAIL4.0479101s at real III submission:
only integration_required after ordinary2/claims4/soleIII/narration, not fixture
failure. First-A late controls remain UNREACHED. Sol causal OUTCOME accepted.
Post proof fa658dc63523a16d771a096a386bb7abcc0524f1ab9fbaf6fe178ba79079aa8d;
raw /workspace/tboe-linux-evidence-20261102/spiritual-strain-maneuver-failure.
Full hashes/owned cleanup/newTMP[]/no active owners/clean source checked.

Next new TEST-ONLY Unit Fact is written, C# UNCOMPILED/runtime UNRUN: typed pure
DATA fixture validates actual parent-source arithmeticIII, complete three-slot
parent envelope and ordinary failed-maneuver shape/dice/lane before expecting the
existing intrinsic SourceWitness.Validate API to accept the closed preimage and
calculateII without mutation. Current parser lacks this field. A fixture/compile
failure will not be called causal. No source/capability/accepted-history claim is
made by pure composition or hashes. No reflection/new cache/shared mutable state.
One exact Unit case60s; no repeat of previous accepted5/20/C4 or real entry case.

Production remains unchanged; full durable core/live/C3/sealedC4/history integration
and actual next-turn/heal acceptance are pending. Existing parent parser/definition
factory/shared source DATA helper are reused; tiny independent fixture work is
not worth caching. Fresh current-owner/source/history guards remain mandatory.
No new GM capability, so GM implementation sync remains part of the whole route.
All four author decisions and old UNKNOWN/HOLD/wounds-only limits unchanged.

# T081-D failed maneuver durable comparison — design and causal scaffold WIP

Final Astra DESIGN review requires preserving the existing historical comparator:
same-turn/live/C4 exact; only an already permitted prior-turn optional top-level
summary may drift, every other field exact, durable witness/fingerprints immutable.
The spec now states that exact boundary; no new summary/mechanics exception.
Test-only Sol SOURCE accepted on e39; C# still UNCOMPILED/runtime UNRUN. The new
commit changes only the four canonical documents relative to e39, not test code.

Test-only causal entry is now written, C# UNCOMPILED/runtime UNRUN. It uses
existing independent signed5/15/4/12 fixture DATA, maneuver/setback vs guard with
costs3/2 and unchanged ordinary strain/control/lost position. Full ordinary
source2/claims4/soleIII and exact acquisition narration precede real III choice.
Expected RED is only missing consumer after selection; late exact A policy and
other-side/canonical/no-authority guards UNREACHED then. First A alone will not
close the connected C4/history route. One new Integration Fact/60s only.
No production/GM capability change; GM sync belongs to the actual implementation.
Policy baseline is freshly read after C2 phase transition, not reused from the
prior mutable draft epoch. Other setup uses existing stable signed-template DATA
and detached roots. No new cache/source capability or speedup claim. Raw will be
/workspace/tboe-linux-evidence-20261102/spiritual-strain-maneuver-failure.

Accepted checkpoint6d3c6ad173af69a04c15e20c2bca066c64f5d772/source79d keeps the
five successful-operation and affected guard PASS separate. Immutable GitHub
readback10 verified, proof9482fb0c793f6bf0d6ee76d7cf5436f7a26d17fa8095bed745a3fcab4105db58.
No current runtime/build owners or local code changes at this boundary.

Astra/high bounded DESIGN confirmed a finite wound-only implementation: existing
source witness optional strainPreimage6fields, full parent envelope anchored to
existing create History fingerprint (includes definitions), one shared pure DATA
verifier, and separate owner-only live/C3, sealedC4 and authenticated signed-baseline
historical adapters. No parallel ledger/cache, current-catalog substitute, caller
JSON authority, ordinary-by-subtraction or general maneuver/QTE rule change.
Exact new scope is one new III strain/maneuver root before last player setback.
Actual source math retains opposition guard tier/resilience and signed +8 margin;
no zero rule or fake none. All unsupported combinations and author4/HOLD remain.

Finite tracked T081-D steps, not completion claims:
- [x] Test-only actual ordinary maneuver/setback vs guard, real III selection and
      intended missing-consumer causal; preserve acquisition narration.
- [ ] Cheap pure preimage/graph/History joins and forgery/refusal fixtures, no source
      capability from DATA. Define complete key/lifetime/remaining fresh checks.
- [ ] Minimal live/C3/retained witness/completed C4/historical adapters using the
      existing source/receipt/history owners and services.
- [ ] Actual C2→C4, next signed turn and real healing/root removal then historical
      revalidation; exact negatives and regression selection/review.
- [ ] GM guide/worked example/manifest/guards, fresh affected builds, independent
      review and remote checkpoint before acceptance.

Production code remains UNWRITTEN and new runtime UNRUN; technical contract recorded
in spec.md. No old successful selection will be repeated without changed impact.
Full diagnostics stay outside checkout; no new broad performance/HOLD run.

# T081-D successful maneuver strain no-op accepted — 2026-11-02

Source/build/runtime 79d16f95360f8dfb5e59c594ae3cc5ddf2e4f77a: five unique
PASS in four bounded runs (2 + 1 + 1 + 1), not one aggregate or a sum with old
pressure4/private20/C4two. Astra/high DESIGN; Sol/xhigh SOURCE, PREP, causal and
GREEN OUTCOME accepted. Production delta adds maneuver to the existing exact
player pressure|maneuver success contribution predicate. No ordinary matrix,
other contribution, current actor/effect/ownership, dice/cost/source check changes.

Real III strain/maneuver choice now finishes directly after lawful maneuver vs
guard: one original source/one materialize decision, no fictitious second source
or none, unchanged player overwhelmed/opposition clear and control, improved
position, player cost3/opposition guard cost2. Same-process cold validator reopening
preserves completed candidate; canonical7 unchanged and no publication authority.
Affected pressure success retains sourceI/none; failed pressure retains +1, separate
A/B, cold rederivation and other-side/understep/premature-echo refusals/sourceII.
This does not qualify other failures, sides, carriers, Engine/UI or fresh process.

Exact TRX seconds: maneuver8.1474882, pressure8.3929952, setback33.1383809,
manifest0.1618832 and parsed GM guard0.2863001. Each descriptor60s; no timeout,
skip, duplicate or underfill. Complete planned/executed counts and owned cleanup;
no new TMP roots or active owners. Fresh Main/Unit/Integration exit0 in
65.969176195 / 35.618303158 / 70.171665556 seconds, unchanged warning multisets
81/26/107. Support/hosts are unchanged verified dependencies, not new builds.
45 artifacts, 206 resource inputs/618 copies, native6, four PE/PDB pairs and
471 source bindings checked before/after all runs. Only guide/example/manifest
and nine copies changed. Actual catalog discovery1359/13186 executed no tests.

The first d9 run is a preparation FAIL (3.6472458s, missing exact acquisition
narration), not causal RED. Corrected c954 run is genuine causal FAIL3.9905717s:
only integration_required after full ordinary source/III/narration admission;
late completion/cold/candidate controls were UNREACHED then and reached now.
Production guards/assertions/budgets were not relaxed for either result.

Full raw: /workspace/tboe-linux-evidence-20261102/spiritual-strain-maneuver-success.
Artifact proof6ee383428579030350d16e08adb8cbd80765f120ea881b922e8d8143af196c4f;
PDB proofb5d18e6238d6f9d6f6f0767e8ff47820379d5014b807097acd00bd181d48d06b;
disjoint5 proof05397d331eb8047ad975edd1d994f94734bf1f2b35a5d35ab17cd544f19d3e97.
Previous actual Main/Unit88fd and Integrationc954 own15 archived with complete
member/hash/gzip EOF/CRC proof; no raw, saves, recovery or outputs deleted.
No raw/TRX/diagnostic JSON/archives/binaries in Git. Optimization: existing owned
context/projector and full signed-template DATA reuse with detached roots; no new
cache, IO or ordinary reconstruction for zero modifier. Fresh fences preserved.
No speedup claimed. GM guide/worked example/manifest/parsed guard synchronized.

Next connected wound-only slice: exact failed maneuver +1 through live C2, actual
harmful witness, common C4 and next-turn historical validation. Astra read-only
DESIGN found a real technical dependency: existing receipts retain actual exchange
but not the proved ordinary preimage; C2 checkpoint is removed after publication.
Use a closed optional comparison in the existing source witness/receipt and sealed
completed-mechanics path, authenticated matching accepted baseline for history.
Only maneuver lane may see proved ordinary after; chronology/harm math/echo see
actual corrected result. No caller JSON, subtracting one, current-effect substitute
or blanket historical exemption. Final representation/design, code and runtime
remain OPEN; a private live PASS alone will not close this route. Existing
PrepareHarm uses opposition guard tier and signed harmful margin+8 in the candidate
case, not maneuver tier or invented zero; its actual new-source result remains
UNRUN. This is no new author rule or general QTE/ACTIVE work. Four author decisions,
all old UNKNOWN/HOLD, whole profile and #1536 parents remain OPEN.

# T081-D player pressure/success strain no-op accepted — 2026-11-02

Source/build/runtime: 88fd5c6187d2109841bfa83d16463ac97414fb23. Four unique
PASS in three exact leaf runs (2 + 1 + 1), not an aggregate or previous20 + 4.
One new success case and three directly affected existing guidance/manifest/setback
checks. Astra/high DESIGN and Sol/xhigh SOURCE, PREP, causal and GREEN OUTCOME
accepted. Production change is four lines in the current contribution consumer.

A real selected III wound no longer blocks the player's exact pressure/success:
the authenticated matching contribution adds no failure-only strain modifier.
Ordinary signed dice/cost/chronology/actor/effect/source/final echo remain required;
other contributions are still checked. Actual source I/opposition offer and none,
completed candidate, ordinary player overwhelmed and seven canonical nonmutation
checks pass. No common-plan/publication capability is created by private completion.
The directly affected setback peer retains +1, separate A/B, cold rederivation,
other-side/understep/premature-echo refusals, source II/none and no compounding.

TRX durations: new success 8.6021414s; setback 34.6753180s; parsed GM guard
0.2369651s; manifest inventory 0.1893396s. Each descriptor remains 60s. No timeout,
skip, duplicate or underfill; complete counts, owned cleanup and no new TMP roots.
Fresh Main/Unit/Integration exit0: 66.274723368s / 35.288982898s / 70.526159810s;
warning multisets unchanged 81/26/107. Unchanged Support/hosts are verified
dependencies, not new builds. Actual 45 artifacts, 206 resource inputs/618 copies,
native6, four PE/PDB pairs/470 bindings verified before/after all three runs.
Only guide/example/manifest inputs and nine copies changed. Catalog discovery
1358 categories/13185 methods; no tests executed by discovery.

Causal source 3ecf8cae6277dbe392c714f98ce7f2cd248b7fca had one complete FAIL
in 4.117225s: only integration_required after lawful ordinary source III+I and
actual III selection. Later candidate controls were UNREACHED then, reached now.
No compile/setup failure was called causal RED; limits were not changed.

Full local raw: /workspace/tboe-linux-evidence-20261102/spiritual-strain-success.
Artifact proof efc23b656ebf86d2d4577a0e750f31255bef9864f0be61c08f8ac96d8cd08014;
PDB proof 6f1f3e723a103275ce88c6251a83626c6413de07a696347a979f42d2a658cbfd;
disjoint4 proof 9ed75b4d7ce8ae89e49dab47ab3f78399a478d06a05e5d7424e7f7b96a8a8274.
Original outputs15 preserved with full member/hash/gzip EOF/CRC proof; no deletion.
Optimization: existing current context/projector and full signed-template reuse;
no new cache, IO or ordinary reconstruction for zero modifier. Fresh authority
fences and detached mutable roots preserved. No measured speedup claim.
GM guide/worked example/manifest/parsed guard synchronized; no Mortal/launcher
protocol change. No raw, TRX, diagnostic JSON, archives or binaries in Git.

Next wound-only step: exact player maneuver/success no-op with strain/maneuver.
Astra DESIGN accepts a lawful opposing guard; pressure would be a matrix counter
and must NOT be used or made legal. Ordinary proof must show one original III
source, maneuver cost3, opposing guard cost2 (3→1), unchanged strains, improved
position and direct private completion without a fictitious none. Code/runtime
for this next case are UNWRITTEN/UNRUN. Other outcomes/sides and whole profile stay
OPEN. Four author decisions, old UNKNOWN/HOLD, Engine/UI/fresh-process and #1536
parents are unchanged; accepted private20 and C4two remain separate versions.

# T081-D strain actual C4 publication and rollback accepted — 2026-11-02

Test/build/runtime c32d8c4c5df5b762fbb487fce75b9b740d88be99, productionMainbc7
unchanged: TWO new unique PASS in separate180s leaf runs. Prior private20 remains
onbc7, not repeated or retagged. Astra/high DESIGN and actual positive OUTCOME;
Sol/xhigh whole SOURCE (one definitions-image coverage finding fixed before build),
PREPs, actual rollback OUTCOME and receipt consistency. No new production mechanism.

Positive actual fresh signed III C2/A/B/sourceII/none -> retained current C4 owner
and same plan -> common NormalizeAndValidateWithPlanAsync. Canonical definitions/
resource state/history, wound/effect identities/carriers/history, source receipts
and owner companions match actual plan after-images. Independent one player-soul
III wound, broken root/secondexchange and2 source/decision(materializeIII,none)
predicates pass. Current published notification checked; private checkpoint/pending/
command consumed. Foreign authority take and second take of consumed authority fail;
bytes unchanged after refusal. This is actual common C4 publication, not Engine/UI.

Rollback: actual committed resource/receipt in two transactions then exactly one
receipt readback cut; original injected exception preserved. Full witness verified
83 signed FILE images(17present/66absent) restored, plus witness PRE-PUBLICATION
generation restored and active journal absent; no validated/publication authority.
This is controlled in-process compensation, not an OS-crash/fresh-process test.

Exact selection: wounds-spiritual-strain-publication1:37.9903598s/180s, wrapper
44.620017207s; wounds-spiritual-strain-publication-rollback1:34.2184793s/180s,
wrapper41.149468874s. Two Facts/methods/descriptors, NoBuild/Parallelism1. No skip/
timeout/dup/underfill; both cleanuptrue/newTMP[], no active owners, clean runtime.
Budget is the new broader C4 leaf's existing3min rollback analogue, not a change
to the accepted private60s. Shared real C2 setup is necessary fresh authority,
not a reused completed checkpoint; oldFactbody preserved through typed helper.

Fresh Integration74.971855086s/exit0/exact107 warnings. Mainbc7/SupportECE actual
used dependencies unchanged, not fresh builds.25artifacts/206inputs412copies/
native4/3PEPDB382 sourcebindings unchanged before/after; resource changes0.
Catalog1356/13184 discovery-only; exact two PlanOnly1case180s leaves accepted.
GM no-update: test-only qualification of existing C4 contract, no new wire/rule.
Optimization: existing immutable full-key template, independent roots, scoped
C2/source/resource/journal/publication owners. Deliberate rollback read hook keeps
fresh fallback; no new cache/passive observer or speedup claim.

Full raw /workspace/tboe-linux-evidence-20261102/spiritual-strain-publication:
P1-artifact-resource-proof02763180c25744f69f825dfaea9be52fd555c51424691aa095325653da87570a;
P1-pdb-proof84921f7028989c5fb40983df1f3414028b170e1bb6614e5660d86d5e37d35bbe;
positive post281e7abfad2f70cacb84e778d6e5bcd41fef8e8de4478b809789cfe15e0fe534;
rollback postee134c8a2c99e5d820983ad0c32abdae63e60542cddad63d45e08393013fab4e;
disjoint2 integrity9c9848d47f699bd7e03ee47354a87f47e86f84715e891a7f53e559b832725fa2.
PriorIntegration5 archive74c2d65bbab38f636ea784d0ef34cd3f82bf369c56b18b5050fbcdd7bbf0defe
verified members/hash/gzip EOF/CRC. No raw/saves deleted or uploaded.

Next independent canonical case: exact player pressure/success is not a failed
operation and gets no new strain step. Astra DESIGN accepted a narrow no-op after
current authenticated contribution/actor/op checks, leaving all ordinary checks and
other contributions intact; causal/genuine-source proof and setback peer needed.
No success implementation yet. Partial_success/countered/other sides/failure
families remain outside that next slice. Four author decisions including proposed0
stillpending; whole strain/US3/#1536/Engine/UI/freshOS and all old UNKNOWN/HOLD open.
General combat/QTE/ACTIVE/F1/T088 remain outside the wounds-only task.

# T081-D strain C4 common-publication qualification WIP

Accepted private20 remains onbc7/source checkpointbd54; do not repeat that selection.
Astra/high DESIGN accepts a test-only next slice through existing C4 owners, no new
production mechanism: independently signed III -> A/B -> sourceII/none preparation
then actual ValidateAcceptedTurnRawResourceMaterializationAsync and common
NormalizeAndValidateWithPlanAsync. One positive and one committed-readback rollback
case, each its own exact leaf180s. This is a new broader workload/budget aligned
with existing portable-original-committed-rollback3min, not an increase of private60.
Actual discovery/Plan/PREP and whole SOURCE are required before runtime.

Existing private Fact body extracted unchanged into typed helper accepting one
fixture; lease/session owners are disposed inside. New tests reuse this SOURCE
sequence on their own fresh signed roots, never copied completed checkpoints or
mutable state. Positive checks actual matching authority/plan and all canonical
after-images, independent III/strain/receipt semantics, foreign and consumed-owner
refusal. Rollback uses existing CurrentCommittedPublicationWitness and real receipt
readback fault after committed resource/receipt, original exception identity and
complete signed file before-image/absence restoration plus the witness
pre-publication generation/journal restoration (no OS-crash claim).

Current change is test/catalog/selection/checkpoint only; new C# UNCOMPILED and
both cases UNRUN. Expected qualification PASS, not an assumed consumer RED. If the
real C4 owner rejects this result, preserve the first actual failure and diagnose
within wound scope; do not relax source/history/authority checks. Fresh Integration
only after review, with unchanged Mainbc7/SupportECE actual dependencies and all
resource/native/PDB bindings checked; no gratuitous Main/Unit or private20 rerun.

Optimization: existing immutable full-key template and existing source/resource/
journal/publication services; independent mutable roots. Fresh C2 completion is
necessary authority for each publication, not reusable from an earlier fixture.
Deliberate rollback read-hook retains the normal fresh fallback; no false passive
observer/cache claim. No new cache or speedup claim. GM no-update: test-only
qualification of existing common C4, same wire/proposal/route; accepted strain
guide/example and manifest unchanged. Raw entirely outside checkout in
/workspace/tboe-linux-evidence-20261102/spiritual-strain-publication.
Four author decisions, parent/#1536/whole-strain/Engine/UI/fresh-process limits and
all old UNKNOWN/HOLD unchanged. General combat/QTE/ACTIVE/F1 remains outside scope.

# T081-D STRAIN-PRESSURE-C2 accepted bounded result — 2026-11-02

Code/build/runtime bc7c5217bc49fc1afc38655d609dc1a55116fc28:20 unique PASS
in THREE disjoint bounded runs18+1+1, not one aggregate invocation. Independent
Astra/high production design/SOURCE and actual strain OUTCOME; Sol/xhigh test/GM/
selection SOURCE, delta SOURCEs, PREPs, cheap18/binding OUTCOMEs and receipts.
No parent/whole-profile closure follows from these bounded accepted children.

A genuine III wound now supplies one owned extra strain step for a direct-root
last player pressure/setback with unchanged ordinary player strain and one newly
selected applicable contribution. The ordinary source/resource result is proved
BEFORE selection and rederived during saved-choice replay; no caller baseline or
correctedAfter−1. Current actor/ordinal/epoch, wound/root/definition provenance,
stack/magnitude and exact frozen siblings are checked. The consumer requires
min(broken,ordinary+1). A exchange correction and B final echo are separate;
full actual source/resource admission precedes B permission. Unsupported operation,
side/carrier/combination remains fail-closed; generic integration guard retained.

Actual positive1: genuineIII → overwhelmed→broken A → committed A progress →
new-validator cold B reconstruction/changed-A refusal → B resolved → real sourceII
(pressure14 from harmful margin8) → explicit none → repeated completed_unpublished.
Exactly2 source/decision rows (materializeIII,none), seven canonical images unchanged,
no common-plan authority. This is SAME-PROCESS new-validator reconstruction and
private completion, NOT fresh OS process, accepted publication, Engine/provider/DOM,
whole strain profile or all US3. Binding peer actual mixed A/B/cold control passes;
its final B remains uncommitted, separate from the new strain case's private completion.

Exact selection (NoBuild/Parallelism1):
- wounds-spiritual-strain-comparison6 + wounds-spiritual-strain-policy-regressions7
  + spiritual-binding-control-boundaries4 + mortal-terminal-guidance-manifest-regression1
  = cheap18; category4.4487086/3.6172625/3.6470283/3.6216945s, original60/120s.
- wounds-spiritual-strain-c2-selection1:42.4165657s/original60s, wrapper50.614344865s.
- spiritual-binding-final-control1:39.0079288s/original300s, wrapper46.536808962s.
Six categories/nine methods/six descriptors. No skip/timeout/dup/underfill; cleanup
true/newTMP[], no active owners, clean runtime tree. Baseline58a1PASS and actual
causal9a7 one integration_required FAIL are separate lineages, not added to20.
One mistaken Plan-path invocation stopped before output/child creation; no extra run.

Fresh Main70.896169639s/Unit36.733297177s/Integration76.716063764s exit0, exact
unchanged warning multisets81/26/107. Earlier CS0103 and then CS8604 are preserved
preparation corrections, not causal RED or GREEN. Existing typed source ActorKey
now internal with unchanged normalizer body; actual player_soul key reused. Only
its previously matching World70 pin updated; all old9 mismatch paths unchanged.
45 artifacts/206 inputs618 copies/native6/4PEPDB468 source bindings match before/
after all runs. Only3 GM inputs/9 copies changed: guide, worked example, manifest.
Manifest61/61 exact inventory and parsed example are tested; no new wire fields.

Full raw /workspace/tboe-linux-evidence-20261102/spiritual-strain-burden:
B5-artifact-resource-proof e4bad8afd13c747b6b05eb25cdb6d74b462b7a28a91c1ebe7e2aa1e993213b79;
B5-pdb-proof e7223f00d06fdbff89e5dbacf878bcd4e51ee9254d1c4aad8acd735163c71bbc;
cheap18 post b958923989d46d12c8fa82a43446e09029c1d647d48349243ff33d6035447268;
positive1 post4f31bb42773a43c0aca74154e0caa6a80effad35660d827be94e0d74b12f9e65;
binding1 post26b1d35328292e6755c0fbf6a3ff383f860c885e45a6b9c6c048b4ba386cf971;
20-case disjoint integrity a85e31eac305d303569d26f21a19527cb5b1f77b2ef3f726ee7e73b7c227da19.
Prior own15 archive8449592b289c472457b8bcc818ca95367a1f292bbadbc19780ee1016b3b06ba4
and B4Main5 archive5343482a8be8407dcd903a064f398cddc164ee09e714369540503d71fd82e918
verified exact members/hash/gzip EOF/CRC; no raw/saves deleted or uploaded.

Optimization: existing immutable full-key prepared template with separate working
roots and existing source/resource/service/journal owners; scoped private comparison
DATA only. Cheap request-shape filter avoids new ordinary probes on unrelated
selections. Fresh ordinary/replay checks remain necessary authority, not cached
permission. No new cache/diagnostic stack or speedup claim; equal-work comparison
is not inferred from RED8s versus the larger GREEN42s. No limits increased.

Next remaining T081-D work is broader strain failure/operation/carrier/lifetime
coverage and final eight-profile reconciliation; this child does not invent those
rules or close the profile. Four author decisions in author-decisions-20261102.md
remain pending, including proposed harmfulMargin0. Existing source-family gaps,
R2 browser/A5 terminal/DFR/Mortal compatibility UNKNOWN, Windows/cross-OS DATA and
all recorded HOLD remain separate. General combat/QTE/ACTIVE/F1/T088 stay outside
this wounds-only task. #1536/US3 and parent tasks remain OPEN.

# Strain warning-clean correction — owning builds still pending

Main52467532a5532051807290f0aeed3e3a43054f4d compiled exit0/64.827821297s,
but82 warnings include one new CS8604: nullable conflictId output entering the
private original-comparison record. The current narrow guard rejects null explicitly
after the existing inventory check; no permission or game rule is inferred.
Source delta review and fresh Main build required before Unit/Integration and
runtime. Their first builds/runs have not started. Original causal9a7 RED and
baseline58a PASS remain separate; all parent/author/UNKNOWN/HOLD unchanged.

# Strain build correction WIP — no runtime claim

First Main build at1bb12f7b22f94a3cc34ae2dc43b33aeca5b54a64 failed CS0103
at new StrainDependency:ActorKey was private to the source-session type. Unit/
Integration builds and GREEN runtime never started. Existing exact ActorKey is
now internal for this real consumer, called through SpiritualWoundSourceSession;
its canonical player key is player_soul:player_soul, not player:player_soul.
Normalization/body/alias/confusable rules unchanged. Only its previously matching
World70 source pin updated after exact delta inspection; baseline9 mismatches stay.
This is new-code compilation/preflight correction, not another causal RED.
Narrow independent delta review then three fresh affected builds/proofs/Plan/PREP.
All author decisions and parent/UNKNOWN/HOLD boundaries unchanged.

# T081-D strain consumer bounded implementation WIP — causal RED accepted

Independent Sol accepted source/runtime9a7bd7441439431257c11e0ab5e516b91e8e2689:
1 actual causal FAIL at selection disposition line64, after genuine III offer/legal
3 slots, seven unchanged canonical images and no-common-plan. Only diagnostic
spiritual_wound_profile_integration_required at exchangeLog[1]; no prep failure,
timeout/skip/dup/underfill, cleanup true/newTMP[], wrapper14.635433029s/category8.2771797s.
Summarya37b605268c9ba9e64b8b76587419c4bff130f7ea2b8c303e33d5a4bd427a33a;
TRXd5696b236359bde52ba22d0c6c12d30100a5e9378af79155d26094ac70ecdd4b;
postproof5bac16f253be59a7a4622e2481ec6b1d2e1b763177e1abe4539040936cdbdafe.
25artifacts/412copies/native4/369bindings unchanged. Later policy checks UNREACHED.

Current production WIP uses existing C2 selection/scoped owners: one direct-root
last player pressure/setback ordinary comparison is freshly proved BEFORE actual
materialization through the shared discarded source/resource probe, then retained
only as private DATA and rederived on cold saved-choice replay. Exact new wound
source/root/actor/ordinal/current context and single applicable contribution are
required. Only one after.playerSideStrain step to min(broken,ordinary+1); unproved
profiles/combinations/side/carrier remain fail-closed. No universal failure rule.
A must actually validate before separate B echo permission; actual corrected source
and witness/offer are retained, not suppressed. Existing binding authority stays
separate. Typed final-strain diagnostic aligns continuation with initial admission;
no generic error bypass. New C# UNCOMPILED, SOURCE/runtime pending, not accepted.

New Unit comparison5+GMexample1; changed-policy cost/force7 and binding DATA4;
new real III C2 A/B/cold/new-source/none1 (60s) and affected actual mixed-binding
A/B control1 (existing300s). Affected exact complete GM manifest inventory1 also selected (existing60s).
Proposed20 distinct cases, exact catalog/Plan required.
Baseline1 not separately replayed; its writer extraction is source-equivalent.
Three fresh affected builds Main/Unit/Integration after whole SOURCE; unchanged
Support/Bridge/CrashHost inputs will be verified, not rebuilt without cause.

GM contract, worked comparison and focused manifest updated; no new wire fields
or author decisions. Optimization: existing immutable prepared template, source/
resource/journal/service owners and scoped DATA; requested-profile prefilter avoids
ordinary probes when no strain is requested. Fresh ordinary probe/replay is required
for source/resource authority, not replaceable by cached permission. No new cache,
reflection, IO stack or speedup claim. Full raw outside checkout at
/workspace/tboe-linux-evidence-20261102/spiritual-strain-burden; no raw uploads/deletes.
All four author decisions, parents and existing UNKNOWN/HOLD remain open; wound-only.

# T081-D strain C2 selection: causal scaffold WIP

Accepted baseline/source-cap preparation58a5 remains separate1PASS; current test-only
extraction reuses its exact raw draft builder with independent signed roots. New exact
Fact requires real C2 first pair/III offer and a3-slot wound: one strain/pressure,
one roll/maneuver and one cost/guard(+2), so only strain changes the next pressure.
Canonical resources/wounds/effects/History/profiles and no-common-plan are checked.
Expected causal RED is unsupported consumer after actual III proposal, not fixture
or compiler failure. Exact1Fact/1case60s, no separate baseline rerun. Later dependent
field/policy controls remain UNREACHED until that selection can proceed.
No production changed; new C# UNCOMPILED/runtime UNRUN, SOURCE pending.

Astra/high narrow design assessment: retain owner-rederived ordinary last pressure/
setback comparison immediately before materialization, reprove via existing discarded
ordinary/source/resource probe and rederive on cold selection replay. Binding proof
stays binding-only. Exact current actor/ordinal/contribution provenance authorizes
only min(broken, ordinaryAfter+1), not caller subtraction. Correction A is only the
exchange strain; terminal echo B requires actual A plus full source/resource proof.
Corrected strain can create a new real source: do not suppress its C2 offer. Unknown
profile/combination remains fail-closed. No new cache; existing selection/scoped
owners and comparison DATA lifetime. No production implementation accepted yet.

Next SOURCE, preserve current Integration5, fresh Integration using unchangedMain816/
SupportECE, artifact/PDB/catalog/Plan/PREP then one bounded causal case. All raw at
/workspace/tboe-linux-evidence-20261102/spiritual-strain-burden. No GM-update for
this test-only scaffold; future consumer rule guidance will be synchronized. All
four author decisions, parent tasks, UNKNOWN/HOLD and wounds-only scope unchanged.

# T081-D strain burden: ordinary signed baseline accepted — 2026-11-02

Source/runtime58a5c0904f96bb17d7838df906bc62c7efacb1e9: exactly1 Integration
Fact/case PASS, original60s, NoBuild/Parallelism1. Actual ordinary session validated
both pressure/setback exchanges and claimed four unique signed dice5/15/4/12.
First player source: true margin10, tiers0/0, pressure20, maxIII. Second ordinary
setback preserves overwhelmed while losing position to opposition_advantaged.
The four checked profile/conflict/resource/History images are unchanged and no
accepted mechanics plan is minted. This is preparation/source-cap headroom only:
NOT causal RED, admitted III offer/selection, strain consumer, C2/cold/publication,
Engine or general US3 qualification. No game-rule or production change.

Fresh Integration81.440729769s/exit0/exact107 previous warnings; actual Main816/
SupportECE dependencies are unchanged, not new builds. Actual25 artifacts,
206 resource inputs/412 copies/native4,3PEPDB/368 current source bindings matched
before/after runtime. Catalog1351/13179 is discovery-only. Plan1 descriptor/1case/
60s; category5.0536712s, test1.5146876s, runner10.4566679s, wrapper11.793649411s.
No skip/timeout/duplicate/underfill; both cleanup true/newTMP[], no active owners,
clean runtime tree/verified remote. Independent Sol SOURCE/PREP/OUTCOME accepted.

Raw remains /workspace/tboe-linux-evidence-20261102/spiritual-strain-burden.
Artifact05c835670a394bf13cc06d8ed8b24672fde8f2b7dc88de5d6077d3149b948959;
PDB3c3533ffc3dcd1a7185641d5c2b1eadee47aece09dbbf76263b404fc4be1c448;
Plan35bd44b131f3a617e60c69c044145abde7bc1e1a7284a4e18fcc56c1961ff2e5;
summaryb21b46bb9feedf70e2395bf161aa7174e0e15319854a6c4fa77e4fd0a8ee5437;
TRXc8a38a07a1d6957323d69ce34aea4fde3dc9f25beffa5b1c0868c7c017d680d6;
postintegrity47679a71243177814d5b1991ca8adf1ff2e402a083893b7ec74bb2de6c547e27.
Previous Integration own5 preserved losslessly in11,261,984B archive,
SHA1ff4b4c14c0434fcf0b9b4f989ea35abd0f91ca85dd7bbb94754602fde19ed51,
with exact member hashes/gzip EOF/CRC. No raw or saves deleted/uploaded.

Optimization: existing full-key immutable prepared fixture with independent roots
and existing typed source/lease owners; no new cache, reflection or caller-supplied
authority. Actual runtime above is cost evidence for this fixture, not a speedup.
SOURCE corrected a guessed closed position token before build/runtime; not a RED.
GM no-update rationale: test-only ordinary preparation, no authored contract/rule.
Next bounded technical step is actual III C2 insertion and an owner-rederived
ordinary-result comparison before exactly +1 strain can be accepted. Binding-only
comparison stays binding-only. No universal failure predicate or correctedAfter−1.
Four questions in author-decisions-20261102.md remain PENDING, including proposed0;
strain +1/capbroken itself needs no author decision. All parent tasks and previous
UNKNOWN/HOLD, source-pin/cross-platform boundaries and wounds-only scope stay open.

# T081-D strain burden: ordinary baseline/source-cap preparation WIP

Base905002e40f0f73cdbe53a89acf55372972ed1d9d is remote verified and9 changed
immutable GitHub files matched full bytes, bounded recovery proof
 e647b770825c922554b5bfd8121f0ec1c8c39acd7fd6c8c89100d01bf10cd2ed
under /workspace/tboe-linux-evidence-20261102/mortal-foreign-wound-effect-projection.
Accepted formatter GREEN4 remains on816, not repeated for this new independent work.
Author packet4 questions pending, no0 or new control/counter/restrict rule chosen.

Next technical step under the already defined strain +1/broken rule is test-only:
actual complete original-intake frame, original tier-zero profiles/mirrored soul and
real signed dice5/15/4/12; no authority mutation after signing. GM candidate first
pressure setback gives player overwhelmed/actual margin10, expected pressure20 and
destination capIII. Second exact pressure/setback keeps overwhelmed, loses contested
position to opposition_advantaged and retains real incoming pressure/dice/cost audits.
Actual BeginSpiritualWoundSourceSessionAsync must validate BOTH exchanges, claim
four unique signed dice and expose exactly one owned prepared source cappedIII.
Profiles/conflict/resources/history bytes and no-common-plan flag are checked.
No reflection or new production test getter; existing typed API and immutable
prepared-fixture mechanism are reused with independent working state.

This baseline is a candidate until actual SOURCE/build/Plan/PREP/run succeeds.
Expected PASS proves only ordinary baseline and source calculation headroom, NOT
an admitted wound opportunity, III selection, C2 correction, new consumer, cold or
publication. Test must not report a prep failure as causal RED. First actual C2 pair
and later source/selection use existing typed CommitSpiritualContinuationDraftFirstPairAsync,
ReadC2NextSourceAsync or PrivateSession adapter; never legacy OriginalCaptureField.
The binding-only comparison remains binding-only. A later adjacent pressure baseline
comparison/strain correction needs its own causal proof and independent review.

SOURCE caught an unsupported guessed position token before build/runtime; all
three test literals now use the actual closed opposition_advantaged enum. This
is preparation correction, not a production failure or consumer RED.
Current new Integration C# UNCOMPILED, baseline runtime UNRUN, production unchanged.
Exact1 Fact/60s category only; next SOURCE, preserve prior Integration own outputs,
fresh Integration (current Main816/SupportECE verified source-equivalent), current
artifact/PDB/catalog/Plan/PREP then one baseline1. Full raw external at
/workspace/tboe-linux-evidence-20261102/spiritual-strain-burden.
Optimization: existing full-key immutable prepared template and actual source owners,
no new cache or caller evidence; independent roots, original authority/fresh checks
retained. No speedup claim. GM no-update for this test-only preparation; no new rule
or authored schema. All parent/UNKNOWN/HOLD/wounds-only limits remain.

# Mortal known-wound consequences formatter fix accepted — 2026-11-02

Independent Sol/xhigh SOURCE, PREP and OUTCOME accept GREEN source/runtime
81606617ea9f8d41385e82c21c7a37bf15a1d81c:4 exact unique Unit PASS/2 descriptors/
3 methods, NoBuild/Parallelism1, original60s each. Categories:
wounds-mortal-foreign-effect-projection2 NPC/combatant and
wounds-mortal-foreign-effect-player-controls2 existing affected Self controls.
New cases reach four hidden/wrong-source/definition/owner variants per row, both
late stale-target controls and ASCII technical-ID privacy assertions. These are
internal controls, NOT additional test cases. Category3.8867746s/3.6211855s,
runner12.9158427s/wrapper14.39936915s, exit0; no skip/timeout/dup/underfill,
both cleanup true/newTMP[], clean runtime tree/no active owners.

Separate causal runtime736e4bdfad3500c77e9a57c1f7f51f9568f6d0fd:2 intended FAIL
at typedContains line67 after real accepted wound/effect identity/catalog/History,
current typed card, four typed negative controls per row and unchanged original
JSON DATA. Later stale/ID assertions UNREACHED then. Summary93ee52d4516af430226065c967361e59c1e1694f9c51ed0ffc6898d8c29d208e;
TRX46db77503a7b56c9cb582ceaca6560b7ebaaa545efd7d8151bc1504ca5df1491;
primary integrity004d2486f0c438c2563967f5c505a043b2ad4aa4aee92d2401625140d2508fb8.
SOURCE caught a default-encoder Cyrillic absence oracle before any build; typed
Facts replaced it. A misplaced source-note was moved from historical S1 to its
actual current block. Neither preparation correction is a causal production RED.

A known physical NPC/combatant card now retains its exact visible owned effect:
closed player/npc/combatant Mortal effect-owner mapping replaces the hardcoded
player kind. Unsupported kinds remain fail-closed, every source/definition/ownerID/
target/realm/visibility/hidden-complication condition is unchanged, and afterlife
remains afterlife_profile. Existing contract already requires visible owned facts.
No new GM schema/API/game rule/cache/live IO/authority or publication change.
Scope is formatter DATA/read-schema only, NOT actual menu/consent/FS freshness,
Engine/native/browser/packaged host or whole US5 qualification. Current real menu
producer supplies MortalTargets from validated canonical facts; direct typed unit
DATA does not issue that authority or prove the menu producer.

Fresh Main74.028719s/Unit39.291176s exit0, exact unchanged warning multisets81/26.
Postrun30 artifacts/412 resource copies/native4/3PEPDB351 source bindings unchanged;
206 resource inputs pinned, no GM resource changes. Support/CrashHost ECE remain
unchanged dependencies, not new builds. Source Projection is not in70 World pin
map; no pins changed, old9 mismatch boundary remains. Catalog1350/13178 is actual
new-method discovery only, not runtime coverage. Exact Plan4 has no overlap.
Artifact0836f6a43316abbb459b04b32f20ba508249201f19a362250a4257a4d8a9226f;
PDBbb052cccc7d1421c1e6037b23b1c6bce56b68c7577bddbdba3fe897ea9119174;
Plan5175e4713516376278fdff75c37dcbfd48d445477a6813305209674bded2d7d6;
summary29975e3383ebd9f5528abca5579d9ed65a5e23fc65a943c5667e686405429387;
newTRXd459150f59e375d709fd0bb8607ce4c495ad9f20682132e3fdc447db51cf52ef;
playerTRXc0b1093b0b39f0aca0ef5807838bd47e5bfb7db492e1619168b49f061d69441b;
primary integritya79c56e7adb90efcacc9f97326040d3e376ad93dfb80c55c9af81b0d41fabd3e.
Full raw remains /workspace/tboe-linux-evidence-20261102/mortal-foreign-wound-effect-projection.
PreviousUnit5 archive4517754B/SHA92820655948cd7822af8aa419287ad0dca12bf5021c17c621a1f327219794fb8;
causalMain/Unit10 archive16350113B/SHAf17acead5b5d1a70037542cb364d4c6f8d4ed2d03e8a4ed2ce41f4f8136a7a70;
member/hash/gzip EOF/CRC verified; no unique evidence, raw or saves deleted.
Optimization: existing prepared-catalog argument reuses stable wound/identity/
History DATA within each independent case; effect variants get real independent
snapshots. Owner mapping computed once per card, no new cache/lifetime/IO or speedup
claim. No artifact/raw/diagnostic JSON uploaded to Git.

One compact author packet (Astra/high consistency accepted) is
[author-decisions-20261102.md](author-decisions-20261102.md): four pending decisions,
not approved amendments. Parent relays in main conversation; no task-local question
resubmission. HarmfulMargin0 is proposed only. Strain is NOT a fifth author decision:
failed declared non-safety operation adds1 to owner, capped at broken, per spec.
Control/counter/restrictIII and dice-free margin stay unresolved; protected safety,
position/B2–B5/source caps are not reopened. General combat/QTE/ACTIVE/F1/T088 stay
outside scope, all broader parent/UNKNOWN/HOLD remain.

Next independent existing T081-D technical step: test-only exact pressure/setback
baseline proof through the real signed ordinary owners before a new III strain
burden is chosen. Existing ReadOriginalBindingComparisonAsync/PreparedContinuation
is the pattern, but its last-control-only binding method must NOT accept pressure
under a false name. First prove a lawful ordinary baseline with headroom, actual
opposed dice/incoming standard pressure and all other old effects preserved. Then
prepare source-reviewed adjacent pressure comparison and bounded C2 correction:
only actual after.playerSideStrain=min(broken,ordinaryAfter+1), unchanged outcome,
other side, dice/cost/before/control/position; matching final echo only after actual
exchange proof. Cold comparison must reprove full baseline/selection/current source,
not subtract1 from a caller result or trust a fingerprint. Exact-once, saturation,
success/unrelated operation and altered-baseline controls required. Initial player
pressure/setback subset does not close all operations, opposition or whole profile.
This is an independently assessed design outline, not production implementation or
runtime admission. General Engine/DFR/HOLDs are not prerequisites for cheap scaffold.
Earlier WIP below is superseded by actual accepted formatter receipt.

# Mortal foreign consequence mapping correction WIP

Independent Sol SOURCE/PREP/causal OUTCOME accepted runtime736e4bdfad3500c77e9a57c1f7f51f9568f6d0fd:
2/2 actual NPC/combatant intended FAIL at final typedContains line67, after real
metadata/current card,4typednegativecontrols and originalJSON preservation. No
prep failure, skip/timeout/dup/underfill, cleanup true/newTMP[], clean. Later stale
and technical-ID controls UNREACHED. Wrapper10.036162238s; summary
93ee52d4516af430226065c967361e59c1e1694f9c51ed0ffc6898d8c29d208e;
TRX46db77503a7b56c9cb582ceaca6560b7ebaaa545efd7d8151bc1504ca5df1491;
primary integrity004d2486f0c438c2563967f5c505a043b2ad4aa4aee92d2401625140d2508fb8.
Fresh Unit38.725437s/exit0/exact26 baseline warnings;30artifacts/412copies/native4/
3PEPDB351bindings all unchanged afterrun. Main d0c/SupportECE were unchanged,
not fresh builds. Catalog1350/13178 discovery-only and exact causal Plan accepted.

Production now derives Mortal effect owner from the CLOSED player/npc/combatant
mapping, unsupported target kinds fail closed. Afterlife remains afterlife_profile.
Every ownerID/target/realm/sourceID/definition/visibility/hidden-complication filter
is unchanged. Computed once per card; no extra IO/cache/permission exception, no
speedup claim. No World pin changes: Projection is not in70-map, old9 stay.
Formatter/current typed DATA only; actual menu/consent/FS freshness/publication and
browser rendering are outside this Unit proof. Existing contract already requires
visible owned consequences; no GM wire/API/game-rule change, current guides remain.
New production C# UNCOMPILED/SOURCE pending. Fresh affected Main/Unit, exact Plan/
PREP then GREEN2 + existing affected player2 under original60s each; tests unchanged.
Full raw at /workspace/tboe-linux-evidence-20261102/mortal-foreign-wound-effect-projection.
No raw/binary uploads/deletions. Four author decisions still pending; strain rule
already defined and separate technical consumer plan is read-only, no code yet.
All parents/UNKNOWN/HOLD/wounds-only scope remain unchanged.

# Independent wound-only work: Mortal foreign consequence formatter scaffold

Base0b4dd7e9777245ecd55bf73bbb70cc43a608703e remains remote verified, clean and
with no active owned runtime at resumption. Full author decision packet is now
[author-decisions-20261102.md](author-decisions-20261102.md): four real choices,
not five. Strain +1/cap broken on declared failed non-safety action already has a
rule; ordinary-result reconstruction/failure/exactly-once are technical work.
No question is sent from this task again; parent will relay in the main chat.
No proposed0 or other new rule is accepted. Existing E/other HOLD remain.

Separate real source-level display defect (T119/T121/T151): Mortal known target
passes npc/combatant targetKind to shared Project, while effect ownership filter
hardcodes player. Actual EffectMechanicsSnapshot stores NPC/combatant carrier kind,
so valid known visible consequences are omitted, contrary to projection contract106.
Sol independently confirmed reachability and formatter-only test sufficiency.

Finite plan: first add two independent real-catalog/snapshot rows for NPC/combatant;
all valid wound/identity/history/effect owner/target/source and visible card premises
precede the final missing-consequence oracle. Hidden/wrong source/definition/owner
controls stay; original JSON DATA (not disk bytes) unchanged. Typed current targets
are trusted formatter DATA, never consent/menu/FS freshness/publication authority.
Existing test helper now fills exact top-level combatant carriers; no shared mutable
state/cache/IO or production hooks. Source review then fresh selected Unit build,
actual catalog/Plan/PREP and one causal2/60s. After accepted RED only, minimal closed
Mortal target-kind→effect-owner mapping, preserving every other filter, source/owner/
definition binding and hidden-complication exclusion. Fresh affected Main/Unit,
exact GREEN2 plus player-preservation2, independent review/receipt/remote checkpoint.
SOURCE caught an escaped-Cyrillic negative assertion before any build/run;
changed it to direct typed Facts absence, so privacy controls are non-vacuous.
No new authored GM/API/game rule; new C# UNCOMPILED, causal runtime UNRUN,
production unchanged. Full raw external at
/workspace/tboe-linux-evidence-20261102/mortal-foreign-wound-effect-projection.
Optimization: current parsed catalog is passed through the existing prepared-catalog
Build argument per scenario: only the detached effect input changes, while exact
wound carriers/history/identity remain stable within this one case. No catalog
rebuild is needed for each effect variant. Typed images reused per independent fixture;
actual effect snapshots rebuilt only for isolated changed inputs, no new live reads
or cache/lifetime/publication exception, no speedup claim. Broad privacy tests not
repeated where unchanged afterlife branch remains literal-equivalent.

# Current US3 wound-consumer remainder: four incomplete profile branches

Independent Sol/xhigh bounded read-only mapping and primary source inspection
confirm all8 registry/projector/UI descriptors exist, but these are not equivalent
to executable next-exchange mechanics. Actual SourceAdmission calls
ValidateCurrentWoundContributions; SpiritualWoundMechanics.cs:71 emits
spiritual_wound_profile_integration_required for the unsupported matching branches.
No publication bypass or fresh runtime failure is claimed by this inspection.

| Exact profile | Existing consumer / real remaining state |
| --- | --- |
| spiritual_roll_hindrance | Actual next-exchange disadvantage, lawful no-roll preserved; existing GEN accepted child. |
| spiritual_action_cost_burden | Existing owned ordinary/special/completed/dependent/recovery/force cost consumers; accepted children, not fresh aggregate. |
| spiritual_position_burden | Actual effective-position/live/retained/terminal/dependent consumers; accepted position/B2–B5 children, no repeat needed. |
| spiritual_control_burden | Registry/projector/UI only; live contribution still integration_required. Consumer unfinished. |
| spiritual_strain_burden | Registry/projector/UI only; extra wound-induced strain consumer unfinished. |
| spiritual_tempo_burden | Actual current-player tempo denial against retained frontier; accepted child. |
| spiritual_counter_burden | Registry/projector/UI only; payoff-step consumer unfinished. |
| spiritual_art_restriction | forbid branch implemented; restrict branch still integration_required. Existing rank-IV8rows present, exact accepted receipt not reconciled here. |

Existing normative spec table defines control one adverse step at II–IV, extra
strain on a declared failed non-safety operation at III–IV capped at broken,
counter one payoff step per slot, restrictIII/forbidIV. But the existing approved
2026-09-20 Remaining profile integration findings in plan explicitly leaves exact
control incoming/escape/same-rank transition, ordinary no-wound strain baseline and
failure predicate, typed counter axis/step/zero outcome and restrict admission
condition unresolved. Do not silently supply those rules or treat registered
payload/magnitude as their execution. No new rule or code implemented by this map.
This narrows earlier incomplete audits: US3 has actual unfinished consumers in
addition to pending source binding and unaccepted combinations; it is not 'only
final tests'. Wider operation/result/dependent/cold matrices remain unreconciled.

E dice-free harmfulMargin question is already submitted and ANSWER PENDING,0 not
accepted/implemented; nonstandard/passive/champion binding remains separate.
Only direct wound semantics are at issue, never general aggression/defeat/T088/
QTE orchestration/ACTIVE/F1/T197. S1 source cleanup6 and T150 authored example1
remain accepted on their explicitly separate runtime lineages below; no test rerun.
Current R2/A5 UNKNOWN and DFR/Mortal/Windows/World9/other HOLD remain unchanged.

# US3 T081-E: dice-free wound math decision pending — 2026-11-02

Accepted source/artifact checkpoint33a6ed48e53234bd846d818f7d7057ea4b249413 is
remote verified; eight changed immutable GitHub files matched complete bytes/blob
identities, external M2-final-remote-source-readback-proof.json SHA
dfaf2f29db48587285e73f0c3532b41ce8894969953c1519fbc0836e5a12f90f
under /workspace/tboe-linux-evidence-20261102/terminal-mechanical-legacy-documentation.
This is bounded changed-file recovery, not full restore or runtime replay.

Independent Astra/high exact design assessment narrows the initial 'production gap'
classification: this is an explicitly deferred actual-source authority boundary,
not a demonstrated defect in current fail-closed pending behavior. Current code:
Validation/ValidationService.SpiritualWoundSourceAdmission.cs:731–739 leaves lawful
voluntary incoming harm without diceAudit as AppliedSourceBinding; SourceWitness
requires actual validated totals/selected dice and independently recomputes margin.
The accepted general checker permits explicit voluntary non-contest without audit;
it does NOT supply a numeric harmfulMargin. Contract afterlife-spiritual-wounds-
and-healing:28–65 and original B1 design:141 require actual sealed inputs and forbid
invented opposed margin/calculation/die claims. No approved replacement value or
calculation for this dice-free branch was found. Tier, paid cost, strain delta or
voluntary flag alone do not establish that missing wound-math input.

One author question submitted asynchronously, ANSWER PENDING: for already accepted
voluntary harmful strain without contested roll, should harmfulMargin be0 or some
other explicit value/computation?0 is proposed only, NOT accepted or implemented;
other tiers/strain/caps stay unchanged. This question concerns maximum wound severity,
not withdrawn T088/repeated conflict, aggression, defeat or general QTE behavior.
No new rule, source/witness exception, build or runtime was introduced here.

Nonstandard/passive/champion actual source binding is a separate unfinished cell:
no universal mapping from effect magnitude/coordination/cost to appliedArtTier or
margin is approved; the proposed dice-free0 would not automatically solve it.
Dependent E changes remain held for the exact needed contract; continue independent
T081-D eight-profile consumer/evidence mapping, no existing PASS rerun or general
system implementation. R2/A5 UNKNOWN, all older HOLD and parent tasks remain open.
The full current accepted T150 and wounds-only remaining receipts follow below.

# Existing T150 mechanical legacy authored example accepted — 2026-11-02

Independent Sol/xhigh SOURCE, PREP, causal OUTCOME and GREEN OUTCOME accepted.
Runtime/source d0c49bff26c9ddcfa61dcc938e751245540cfca3: exactly1 unique Unit Fact
PASS,1/1 descriptor/case, category mortal-terminal-guidance-causal, NoBuild/
Parallelism1 and original60s. Category3.4408048s, runner8.5336381s, wrapper9.931795813s.
No skip/timeout/dup/underfill, both cleanup true, newTMP[], clean runtime tree and
no active owners. All three cloned refusal controls execute inside this ONE Fact,
not three extra test cases. Production parser accepts full mechanical draft and
rejects copied legacyId, missing effectDraft and external definitionRef; all old
route/cosmetic/mode/selection roundtrip/nonmutation/guidance checks remain.

Actual causal sourcef6313d7b15da32cd7cec1107338d63e16f9935d6: exactly1 intended FAIL
at line87 (missing mechanical typed collection) after old valid-route/mode/cosmetic
premises; future3 refusal controls UNREACHED there. Summary7b10de363758306bc5a56110edef7830b0017dc46632d4143636d104e0c9f6d2,
TRX68b37669a5942c361733e44f4a3700b05554681506063ff63637e3c3993a24a5,
primary integrityb6db5518bc15cc5bd34520292d10ead9096deccc3fc87ca2cb1a0c91747bc48e.
This was a missing GM-authored example branch, not a healing production defect.
Earlier SOURCE caught selection version/schemaVersion preparation typo before
build; actual metadata reader/matrix check passed after correction, not a fake RED.
SOURCE also corrected retained-request prose to normative actual-confirmed-request
language, so no exact new Engine/native/browser result is implied by this example.

The SAME existing terminal family now includes cosmetic pale_scar and one complete
contracted mechanical_effect legacy with1 definition/1 application, closed empty
parameters, links=[] and local refs only. Complete provider capability for both
branches, separate confirmation and client ID/source allocation remain required.
These are illustrative consequences of that authored program, not default healing
rules. Manifest changes only3 fields in its existing row. No production, API,
entrypoint, prices/duration, scope/authority or general QTE/conflict changes.
Existing GM entrypoints already reference this precise terminal family; worked
example/manifest synchronization completes this bounded artifact gap.

Fresh resource-owning Main72.590825s/Unit38.052373s exit0; exact unchanged warning
multisets81/26.30 copied artifacts/206 source inputs412 resource copies/native4,
3PEPDB349 bindings all verified and unchanged after GREEN. Exactly the two intended
Example/manifest resource inputs and4 copies changed; all other pins remain.
Support/CrashHost ECE are unchanged dependencies, not new builds/host qualification.
Artifact0d0a8848913a8a1c7d4b8aee7c84aa497fc53adcbe8012f16bf51d73b379e3cd;
PDBfa1996f679540631e433989a8f1f152249ccf349d14d3c29f5b6a4de77a3b4d0;
Plan28de8dd4ce53dffb61c79841949faf6e8fc049423a6ca9d88e7d685bba070a5a;
summary4bffbd5e6d990be92d5ccbea0cb90729031fc13c2f3c5c73aa96e3ca9e77a9e5;
TRX0d26b8b57b5dbae58ce7052a28cf10c91ce57608f918d77a75bbaf613890e9b9;
primary integritye98f871147e8742aa5474499bd04c8362b2d328036ccd3b721c6fcaa74d52bfb.
Retained catalog1348/13177 has unchanged membership; actual current metadata and
exact Plan resolve this one existing Fact, no redundant broad discovery/runtime.

Full builds/logs/TRX/plans/proofs and previous binary archives remain outside Git at
/workspace/tboe-linux-evidence-20261102/terminal-mechanical-legacy-documentation.
S1 prior own10 archive16346297B/SHA6e7fb465b88b44f49fa5b29977cb8c1de78e4f7b9a5683ef50f58622e7305683;
M1 causal ownUnit5 archive4517726B/SHA98aa3d5ea9b9e4694ce578f254551e91b1c70d561c6d79c4fd19688d57f7fc2a;
all members/hash/gzip EOF/CRC verified, no deletions. Optimization: reuse existing
loaded example/manifest and parsed route; independent clones protect original DATA.
No extra production reads/cache/lifetime/authority exceptions or speedup claim.

Finite T150 read-only family mapping found actual constructor/scope, repair,
Mortal/spiritual diagnosis, procedure/course/guaranteed, eight spiritual effect
profiles, active/natural/entity recovery, Elyara/public/negotiated/faction service,
target/detail/History and cosmetic-legacy examples/manifest/typed guards. Some
owners are outside ExampleDocumentationValidationTests.Wounds.cs; planned filename
absence is not missing implementation. This new authored mechanical branch fills
one confirmed gap, not fresh combined execution of every family. Independent Saref
survival example/runtime stays separate #1571, outside new wound implementation.

Finite direct US3 reconciliation by independent Astra/high (read-only, historical
receipts not rerun; older broad 'no production gap established' is now narrowed):
- C2 checkpoint/pending/original/journal/saved decisions/private transport and cold
  replay are implemented; reconcile parent before/staged/absent/fresh-owner/zero-side-
  effect predicates with accepted R1/C4 children. No OS-process-kill claim.
- C3 reducer/common assembly and terminal/reused-display-ID R3 are accepted. R1/R2A/
  R3 parent reconciliation remains with R2B, not a new terminal reducer.
- D create/worsen, signed origin/seals, guaranteed worsening/satisfaction, terminal
  comparison, preview and position B2–B5 accepted; full eight-profile consumer and
  operation/result/dependent/cold matrix still needs exact receipt mapping.
- E ordinary/unrestricted/capped-special ceilings IV/IV/II have an accepted lifecycle
  child, but lawful dice-free incoming harm currently yields AppliedSourceBinding
  pending and no PreparedSpiritualSource. Nonstandard/passive/champion similarly
  needs actual binding. Exact existing code ValidationService.SpiritualWoundSourceAdmission
  at731–739 and ordinary-valid SourceOwner_DiceFreeVoluntaryIncomingHarmRemainsPendingActualSource
  test127–185 establish this unfinished source-binding route, not a publication bypass.
  Next inspect exact canonical authority/ceiling inputs and existing owners; never
  invent opposed margin, guarantee or general conflict rules to close it.
- C5 cold recovery docs child is accepted (historical docs140/examples5); final current
  matrix/control reconciliation waits for D/E/R2B, no absent documentation system.
- R2B/T107 initial safe healing/recovery then live insertion remains unaccepted:
  existing producers are real, same signed safe+combat is unlawful, sequential route
  needs a genuine existing separate-start publisher seam, not bootstrap overwrites.
Next finite code investigation is only E's wound-source binding; whole general
start/escalation/defeat/aggression/QTE/ACTIVE/F1 systems remain outside scope.
No broader audit, new rule, diagnostics stack, full DFR or accepted PASS replay.

Whole T150/T149/T071/#1536 are NOT closed. The wounds-only remaining map below still
applies, except this mechanical example leaf is now accepted. R2 browser-course and
A5 joint terminal remain UNKNOWN without retry admission; DFR/Mortal/Windows,
first Guardian/cross-OS DATA, World9 and other prior HOLDs remain explicit. General
aggression/defeat/QTE orchestration/ACTIVE/F1/T197 are outside scope, not completed;
withdrawn T088 question needs no answer. Earlier WIP below is superseded.

# Existing T150: complete mechanical legacy example correction WIP

Primary M1 causal onf6313d7b15da32cd7cec1107338d63e16f9935d6 completed exactly1 FAIL
at PhysicalGuidedDocumentationTests.cs:87 (missing typed mechanical legacy row),
after all existing valid-route/mode/cosmetic premises. No timeout/skip/dup/underfill,
both cleanup true/newTMP[], clean; all30 artifacts/412copies/native4/349bindings
unchanged. Wrapper10.709948087s. Three future cloned refusal controls UNREACHED.
Summary7b10de363758306bc5a56110edef7830b0017dc46632d4143636d104e0c9f6d2;
TRX68b37669a5942c361733e44f4a3700b05554681506063ff63637e3c3993a24a5;
primary integrityb6db5518bc15cc5bd34520292d10ead9096deccc3fc87ca2cb1a0c91747bc48e.
Independent Sol/xhigh causal OUTCOME accepted. This is a real authored-example gap, not a
healing production failure or the earlier metadata preparation typo.

Minimal correction adds one full mechanical_effect legacy to the SAME terminal
route, preserving cosmetic pale_scar and the two-fence/three-selection family.
One complete current #1535 action_control definition, one local application,
closed empty parameters and empty source links; independent client allocation/source
and complete provider capability remain explicit. No default heal consequence,
price/duration rule, new mechanic/API/entrypoint or source/permission exception.
Manifest points to the full authored branch. Independent SOURCE found only
one precision issue: retained-request wording implied an exact new runtime result.
Changed it to normative actual-confirmed-request wording; no new runtime is claimed. Current guides already link this exact
terminal family; no parallel format or new prompt command. Test assertions unchanged.
Optimization: existing complete definition language reused, one stable example read/
parse and independent mutation copies; no production IO/cache or speedup claim.
Next SOURCE, preserve causal binary, fresh resource-owning Main/Unit, exact Plan/
PREP one Fact60s GREEN, independent OUTCOME/receipt/remote verification. Current
new example/resource inputs UNBUILT and typed GREEN UNRUN. Full raw at
/workspace/tboe-linux-evidence-20261102/terminal-mechanical-legacy-documentation.
Every parent, R2/A5 UNKNOWN, DFR/Mortal/Windows/World9/other HOLD remains open;
no general aggression/defeat/conflict/QTE work. Independent memory remains #1571.

# Existing T150 mechanical legacy worked example: causal scaffold WIP

Base2cbe1526471ef8f0105e91260bc400209d7e9af3 is remote verified; seven changed files
matched immutable GitHub bytes (bounded recovery, not full restore), external proof
06f6d11dad99bb625b52621d0597b06a07d658a051434b1d23140439dd0d9b1c.
S1 accepted6 remain on runtimeb1e; not repeated for this next documentation block.

Goal: give GM one complete lawful mechanical_effect heal legacy draft under existing
T150/FR-086/087 and contracts/mortal-wound-treatment.md:383–392. Production parser,
planner, terminal History-backed source and effect owner already exist. Cosmetic
pale_scar stays. Missing artifact is authored mechanical branch, not a new game rule.
Approach: extend the existing terminal route with one additional local mechanical
legacy and full closed definition/application DATA. Keep the two-fence family and
all three mode examples; no parallel format or copied permanent IDs/source links.
A separate duplicate full route was rejected as needless example divergence.

Finite plan (native inline execution; existing task/location, no new task IDs):
- [ ] SOURCE review this scaffold, preserve previous binaries, fresh Unit only.
- [ ] Exact discovery/Plan/PREP, run the existing one-Fact category60s once. Expected
  causal RED: actual valid route/cosmetic/three-mode premises pass, mechanical typed
  row absent. Unknown-field/ref controls remain UNREACHED until the branch exists.
- [ ] Add complete mechanical draft to the same worked family; preserve cosmetic,
  clarify illustrative full provider capability and current client allocation. Update
  this family's manifest description/requiredText. No API/entrypoint/game rule change.
- [ ] SOURCE, fresh resource-owning Main/Unit, exact Plan/PREP and same one-Fact GREEN:
  actual closed parser accepts full draft and refuses copied legacyId, missing draft,
  external definitionRef; original example remains byte-equivalent after cloned tests.
- [ ] Independent OUTCOME, compact receipt, exact staged paths, commit/push/remote SHA
  and bounded recovery. No full terminal/browser/old PASS or HOLD repeat.

Optimization: existing loaded example/manifest and parsed typed route reused within
this independent test; three detached mutation copies are required to preserve the
original. No production IO/cache/lifetime changes or new speedup claim. Raw remains
/workspace/tboe-linux-evidence-20261102/terminal-mechanical-legacy-documentation.
SOURCE found and root verified selection metadata typo before any build/runtime:
version was used instead of existing schemaVersion. Corrected exact key; actual
Read-TestCategoryCatalog/Get-TestCategoryCiMatrix metadata-only check exit0,
1348 categories and one Linux selection, no frontend or tests. This is preparation,
NOT the expected mechanical-example RED. Full log stays outside checkout.

Current change: Unit guard/category/selection only; example and manifest unchanged,
new C# UNCOMPILED, causal runtime UNRUN, SOURCE pending. Parent T150/#1536 and all
R2/A5/DFR/Mortal/Windows/World9/other UNKNOWN/HOLD remain. Independent memory #1571
is outside this wound implementation; no general aggression/conflict/QTE work.

# Existing T149 source cutover accepted; wounds-only remaining map — 2026-11-02

Independent Sol/xhigh SOURCE, PREP and OUTCOME accept source/runtime
b1e30030edb27e670c566a544a3b2a20f72d6e89: one actual fresh group of6 exact unique
Unit Facts, category wounds-canonical-source-cutover,1/1 descriptor complete,
original60s. Category3.8528599s, runner9.7760736s, wrapper11.384411582s, exit0;
no skip/timeout/duplicate/underfill, both cleanup true, newTMP[], clean runtime tree.
The unreachable generic isHealed fallback is retired; supported typed wound export,
ValidateWoundBindings and independent terminal History-backed wound_legacy remain.
This is dead-path cleanup/characterization, not a new causal gameplay or cold proof.
The four obsolete aliases now have explicit resurrection bans; honest source
inventory no longer requires a phantom LegacyLoose category. No World pin changed.

Fresh Main65.713625s and Unit37.430118s exit0; unchanged warning multisets81/26.
Post-run30 copied artifacts/206 inputs412 resource copies/native4/3PEPDB349 source
bindings unchanged. Source-equivalent Support/CrashHost dependencies are not new
builds; Integration is unchanged/unselected. Catalog1348/13177 is discovery only.
Artifact proof9ccbbfcf4259424c0d8f7e48026507f16bf613a53aac02241b7b380f9fa89fff;
PDB5501ca1f7d44e3e5f9b090aa5c9de1382a77f18ee2c757c03a0d7b7e54840015;
Plan842009c7ace8367c3778d7fee87819671b89bca09adb9cab634a2fe138e35aae;
summary91573501e13205c287fa9c3c0114cb9cc6e25af2c5c7201cf94571df207c3012;
TRXd57faca482047d34de28c580d25dd0f74982ba028dafb296482c5b22ddc4323f;
primary integrityeae308fc41624f0131c92d8a1b1aa373f177925594b7aa058eb00a9369a255fd.
Full raw remains /workspace/tboe-linux-evidence-20261102/canonical-wound-source-cutover.
Previous10 owned outputs are losslessly archived/CRC verified, SHA
60fc310a35c6a60f27054273126d7ae56160787b45f95e806828348fd94462e5;
no raw/saves/recovery deleted. Optimization: no new live IO/cache/lifetime or authority
exception; guards reuse the same source images. No performance improvement claimed.
GM no-update is justified for this non-live fallback; existing typed contract stays.

Author scope remains ONLY wounds, acquisition/state/treatment/diagnosis/direct
recovery/display and necessary persistence/verification. Withdrawn T088 question
needs no answer and grants no rule. General aggression/defeat/QTE/ACTIVE/F1/T197
are outside this execution, not completed. Existing QTE/NPC cuts admit/refuse wound
data only. Current bounded Astra/high audit does NOT prove a new missing gameplay
system or whole #1536 completion. Empty parent checkboxes are not missing code.

| Existing task / boundary | Actual remaining state and next finite action |
| --- | --- |
| T149/T071 | Current source/raw cuts accepted on their separate recorded lineages; reconcile whole ownership matrix before parent closure. No retired fallback recreation. |
| US3 T081-C2/C3/D/E/C5 | Live wound witness/completion/validation implemented; match wound-specific source/ceiling, side, create/worsen, guarantee, lifecycle/replay/cold and eight-profile cells to existing receipts. Do not redo accepted B2–B5/source-ceiling children. |
| T081-C3-R2B/T107 | Healing/natural producers and live insertion exist. Same signed safe-cycle+combat bundle is unlawful. Sequential actual safe publication then actual separate conflict start needs an established existing publisher seam; bootstrap rewrites are not proof. No general conflict system authorized. |
| US2/T121 browser course R2 | UNKNOWN180.1041994s/180, no TRX. Retained root proves milestone2/turn45 and second ordinary wait46 with response, not milestone3/heal. Prefix lags actual execution; exact internal timeout cause unproved. No retry admission. |
| T121 joint terminal A5 | UNKNOWN120.0346224s/120, no TRX. Accepted anchor2/output10/controls3 do not replace full peer; needs cause and separate unchanged-budget PREP. |
| US1/US4–US8, T093–147/T167–177/T159 | Major acquisition/healing/recovery/diagnosis/provider/navigation/scope paths exist. Reconcile realm/actor/progression/catch-up, compensation, stale/confirm/error, lifecycle and UI scenarios against actual receipts; do not infer missing shared runtime from a parent checkbox. |
| T150 | Actual examples/manifest/typed guards cover constructors, repair, diagnosis, treatment, recovery, providers, commands/History and cosmetic healed legacy. Narrow confirmed gap: full GM-authored mechanical_effect legacy draft plus typed example guard. Implement that existing contracted branch next; not a new mechanic. Independent Saref survival example/runtime remains separate #1571. |
| T151/T152 | Russian label, privacy/recursive projection, escaped text, controls/display bounds and actual App desktop/mobile leaves exist. Reconcile remaining current bidi/confusable/size/hidden-field requirements without inventing Unicode policy. Controlled HTTP DOM does not prove backend/provider/packaged/fresh-process saves. |
| T153/T154 | Index/bounds/rollback code and tests exist. Reconcile exact scale cases and surface×cut×byte/existence matrix before closure; no measured new complexity defect established. |
| T155–165/T177 | Applicable FR/quickstart/scenario reconciliation, final independent review/finding closure and source/docs/remote acceptance remain. Current category policy supersedes historical aggregate Lane/npm verify commands; no merge/issue closure authorized. |

Separate unchanged boundaries: DFR300.1806894s UNKNOWN with69 component PASS on
their own lineage; Mortal compatibility/ordinary-envelope UNKNOWN; Windows deferred,
first Guardian intermittent/cross-OS DATA; T609 parked; old9 World pin mismatches.
Historical20261025 raw unavailable; current R2/A5 preserved. None is closed or
automatically retried, and unrelated general systems are not wound prerequisites.
Thus neither 'all code finished' nor 'only two tests remain' is established.
Earlier WIP below is historical and superseded by this actual accepted receipt.

# Existing T149: final generic wound-source fallback cleanup WIP

Base54e4c108bd6b6933e348f13246185d89fb720af6 is remote-verified and receipt consistency
accepted;14 immutable changed-file GitHub blobs matched, external proof
5d22f2c446ab6b228200aa29f9aa6c895cf4074433b75fcc31008775519b9d48 (bounded readback,
not full restore). Previous fresh14 accepted onruntime0f9 remains separate.
Independent Sol source assessment: IsSourceCurrentlyActive has exactly two callers,
one literal hazard and one from closed descriptors, none wound. Canonical wounds
use separate typed catalog/active lifecycle/non-public export. Generic isHealed
branch is unreachable; replace it with explicit false for unsupported generic
wound activation, remove the last obsolete boolean syntax. No supported behavior
change or fake causal RED. Current ValidateWoundBindings remains byte-identical,
reclassified as accepted typed protection; independent wound_legacy remains solely
terminal History-backed, unchanged. Do not delete these actual authority checks.

Inventory removes the phantom LegacyLoose category requirement instead of requiring
an obsolete surface forever; current binding/generic refusal have meaningful owners.
The fourth retired token isHealed joins explicit no-resurrection checks using the
same source images, no new reads/cache/allowances. Generic denial assertion handles
LF/CRLF whitespace. Composer is NOT in the retained70 World pin map; no pin was
changed or invented. Old9 mismatches stay, no World qualification. A local edit
script's incorrect pin-presence assumption stopped after the two source edits;
actual state checked before continuing; no build/runtime had started.
GM no-update: no live wire/game capability change and no isHealed in active guides/
rules/examples; current typed-export contract already governs. No new task IDs.

New C# UNCOMPILED/SOURCE pending. Exact existing-method Unit6 selection: persisted
canonical export, empty group fingerprint, loose scan refusal, caller generic-export
refusal, semantic ownership and discovery.60s protective bound, no broad classes/
legacy cold/source-binding/raw/QTE/HOLD repeats. All fixtures are existing pure
independent DATA; no test-only source verdict or permission substitution. Optimization:
no live extra work, IO/cache/lifetime or authority exception; no speedup claim.
Next SOURCE, preserve owned binaries, fresh affected Main/Unit only (Integration
unchanged/unselected), current proofs/catalog/Plan/PREP6 then runtime. Full raw at
/workspace/tboe-linux-evidence-20261102/canonical-wound-source-cutover outside checkout.

R2 read-only reconciliation (no retry): frozen retained root confirms milestone1
turn43 and milestone2 turn45 publication on same active course, then second ordinary
wait turn46 with GM response. Third treat/heal absent, wound active. Timer prefix
lags actual execution and cannot locate the final internal call; outer second
Engine.ProcessPlayerTurn not proven complete. Existing180.1041994s timeout remains
UNKNOWN, no causal retry/limit change admitted. Full raw untouched, no new diagnostics
stack. General conflict start publisher seam for separate safe->combat journey is
still not established; no general orchestration implemented under this wound scope.
Parents, World9 and every prior A5/DFR/Mortal/Windows/other HOLD remain open.

# Existing T071/T149: legacy combat wound-effect refusal accepted — 2026-11-02

Independent Sol/xhigh SOURCE, PREP and primary OUTCOME accept runtime/source
0f9e47de4bc2305561bb93ebea233915aa70253e. One actual fresh addressed group14/14 unique
PASS,7/7descriptors/10methods, exit0. Exact categories: wounds-combatant-legacy-raw-response7,
wounds-combatant-legacy-qte-offer1, wounds-npc-legacy-qte-offer1,
wounds-player-legacy-qte-offer1, wounds-npc-canonical-retirement-source-guards2,
wounds-legacy-effect-documentation2 (separate Unit/Integration). NoBuild/Parallelism1,
original60/180s. Wrapper41.592873s/runner40.1553743s, no skip/timeout/duplicates/
underfill. Both cleanup true/newTMP[], no active owners, clean runtime tree;
post-run45copied artifacts/618resourcecopies/native6/450sourcebindings unchanged.

Legacy wound type and exact source property (including null) now produce precise
private dedicated raw refusal, rather than a fake ID/duration repair route. All6
causal target rows and current adjacent/control/bytes pass. Three genuine signed
QTE peers reach exact refusal code/path while preserving JSON/request/manifest/
authority/current source bytes. Current canonical active-effect branch, late
canonical validation, every non-wound rule and all source/permission checks stay.
Only obsolete wound type/field/hints and one enum item were retired; no new game
rule, general conflict/QTE work, direct writer or publication-bypass claim.

The original W1 source557da697 causal6FAIL/1PASS is retained, all7 complete with
actualissues=[] at first Error oracle AFTER controls/real wire dispatch/typedcarrier/
byte premises. Pascal-wire SOURCE finding was corrected before that first build;
no ignored-field fakeRED. No gameplay assertion or timeout was relaxed for GREEN.
GM effect guide, sole negative effect_legacy_rejection_v1 worked example and
manifest teach current client-derived wound effects; negative syntax is not a
production-valid example or application command. Positive legacy scanner passes.
No new authored capability or afterlife matrix/entrypoint change. Meaningful source
ownership/enum exclusion guards pass; one semantically reviewed previously matching
PrivateImplementation pin changed, all old9 World mismatch paths remain unchanged.

Fresh Main64.811942s/Unit37.844120s/Integration80.024699s exit0, exact unchanged
warning multisets81/26/107.45 current copied artifacts/206inputs618copies/native6,
4PEPDB450bindings incl newly changed PrivateImplementation source. Support/CrashHost
ECE are unchanged source-equivalent dependencies, not new builds/host qualification.
Exactly3 approved GM resource inputs changed and9 selected-owner copies match;
all other resource pins unchanged. Catalog1347/13177 is discovery-only.
W2artifact9b9287c17ad76ff2e8021a39812edb7b8da689f1fccb785fb57971a6629b3c4c;
PDB2319a4b75992984f69b7ce94d8ec5380cec63bb387b88f9c93d6c0b68684ab18;
summary30872be6554070d8151e013678f0e175dc05770d202e917d9e9bd5c872b29755;
rawTRX8a94c90b46e191a74d9afd0d713c207eb5f50f3294889286ba45c5f131d7658b;
newQTETRX8de8709febea4923afc2c2c6b331e28ec90e5db6fda6ad50e81813924c0dbe5f;
root14TRXintegrity453cc559bb42cd0b1e206987168a03f7107aacf5ae1b938c5e0d54e526134b94.
Full seven TRX/summary/build/source/resource/raw and previous verified own15 archive
(27,612,495B/SHA905a37c8ff40ffabeac66938f862ee070685f0243efc27d9d1e6eb21895f417d)
remain external under /workspace/tboe-linux-evidence-20261102/combatant-legacy-wound-response.
No unique evidence, saves/recovery/raw deleted; no raw/binaries uploaded to Git.
Optimization: existing parsed element/required type and already-read source DATA,
no new production IO/cache/lifetime/authority exception, no speedup claim.

Remaining T149 source labels were independently reconciled read-only: generic
isHealed fallback is unreachable from the actual closed source descriptors/hazard
callers; current1536 wound export uses typed catalog/active lifecycle/non-public
source. Small dead-code cleanup can remove that last marker. Conversely,
EffectSourceAuthority.ValidateWoundBindings is actual typed source-bound protection,
not a missing implementation or removable fallback; its LegacyLoose label is stale.
Independent wound_legacy comes separately from terminal History and is unchanged.
Next minimal source step may retire only dead branch and reclassify current binding,
with explicit isHealed resurrection ban and no requirement for a phantom remaining
legacy inventory category. No fresh gameplay gap is inferred from those labels.

Wounds-only task boundary remains: no aggression/defeat/ACTIVE/F1 implementation;
withdrawn T088 question needs no answer. R2 browser-course/A5 joint terminal stay
UNKNOWN with no automatic retry admission, historical DFR/Mortal/Windows HOLDs
remain. Safe->new conflict wound journey still needs an established actual separate
start publisher, no fabricated same-turn safe bundle/fixture canonical patch. Parent
T071/T149/#1536 remain open; receipt SHA/ref/bounded source readback checked separately
from runtime0f9e47de. No merge/force-push/branch deletion/issue closure.

# Existing T071/T149: legacy combat wound-effect correction SOURCE WIP

Actual W1 causal is independently accepted on557da6971d60d5b842a8ab3f9c071a6af4f2d046:
7/7 complete,6 actual missing-Error FAIL/1 controlPASS, all6 issues=[] at line82.
Lawful controls, exact lowercase wire/side/collection dispatch premises, typed empty
carriers and byte checks reached first; code/hint checks UNREACHED. No timeout/
dup/underfill, cleanup true/newTMP[], clean. Wrapper12.606729s/runner11.2587599s.
Summaryd599516af073d6fc6064edf7c0f47e634d551edcac5cb4bbf1a6c5e79e805089;
TRX6f727ae0986783703a6757239bb2e8f9314ef84897c8bca44309ebbce6dbb799.
Full raw /workspace/tboe-linux-evidence-20261102/combatant-legacy-wound-response/W1-red7.
W1 fresh Integration68.530141s exit0/107 exact prior warnings,25copied artifacts/
206inputs412copies/native4,3PEPDB367bindings; Maina7/SupportECE unchanged, not3fresh.
Catalog1346/13177 discovery-only; exactraw7/60s admitted by independent PREP.
Earlier Pascal-case fixture finding was corrected before build; both side and all4
collection dispatch checks now prevent vacuous ignored-field RED.

Minimal production delta: only the legacy combat effect fallback refuses old wound
type (same prior case-insensitive semantics) or exact legacy source property, including
null; private Actual, precise effectType/source-property path and wound-specific
repair. Common primitive checks and every non-wound branch retain order. Canonical
active_effect branch and late canonical validation are unchanged. Remove the old
whitelist entry, target/duration special cases, optional legacy source parser and
stale999 repair advice; preserve all other effect types/duration rules/sourceSkill.
No direct writer, wound lookup bypass, new rule or general combat/QTE orchestration.
No publication bypass is claimed: late canonical refusal already exists.

Semantic inventory now owns the exact legacy constants/refusal and enum retirement,
retains the global three-alias ban and checks enum exclusion using already-read DATA.
One previously MATCHING PrivateImplementation pin changes only for this reviewed
one-entry enum removal: old54222A4E... ->437F96F7... . The exact old9 World mismatch
paths stay unchanged, no broad mechanical rebase or World qualification. External
W2-source-pin-delta-proof SHA256bf2a6c9129889615d230dc824af2f4bca50459e15cbaffdb7b7179bc4584c57c.

GM synchronization: existing effect guide explains early wound refusal and client-
derived effects; sole negative effect_legacy_rejection_v1 worked section includes
complete invalid combat wound input and correction rationale. Manifest preserves
focused-fragment scope (not production-valid example execution) with exact new
negative teaching text. Positive legacy-route scanner scope stays strict. No new
authored capability/afterlife matrix or prompt entrypoint; current direct cutover
already forbids nonempty combat carriers. No new task IDs.

C# correction UNCOMPILED/SOURCE pending. Planned addressed14: raw7, newQTE1 plus
existingNPC/playerQTE2 after shared-fixture change, semanticguards2, exactdocs2.
Original60/180s bounds, no old root raw cases or general aggregate. Optimization:
reuse parsed element/required type, one tiny presence check, no new IO/cache/key/
lifetime or authority exception; source tests reuse retained images for assertions.
All fresh source/permission/history checks remain. Next SOURCE, preserve prior
owned artifacts, fresh Main/Unit/Integration and changed-resource copies, exact
catalog/Plan/PREP14 then runtime. All prior UNKNOWN/HOLD and parents stay open.

# Combat wound-effect scaffold SOURCE wire-key correction, runtime UNRUN

Independent Sol review caught Pascal DTO property names instead of exact lowercase
JSON enemiesData/alliesData. Corrected new raw/QTE input and expected paths before
any build/test. Add existing-error prerequisites for both side dispatches (required
image_prompt missing) and all4 side/collection paths (scalar effect row) so ignored
or misspelled fields cannot fake the intended raw admission RED. These controlled
invalid copies grant no new validation result; original sources stay unchanged.
No production correction or diagnostic-code-only RED, no weakened oracle/cache.
New C# remains UNCOMPILED; delta SOURCE and fresh Integration still pending.

# Existing T071/T149: legacy combat wound-effect raw causal scaffold WIP

Base45638298ac220de18edee424643873871fa53c7f remote checked; prior parser retirement
accepted10+1 distinct groups,7 immutable GitHub changed-file blobs matched. External
proof50418641b61d6310671ae4427fad14d5d098e6a7712aeb226b006ed1a884df9f; bounded
readback only, not full restore. Current source gap independently confirmed by Sol:
public ValidateResponse reaches legacy WoundReference/sourceWoundId admission and
stale999 hints. DTO/map preserve nested data, but late canonical effects already
reject the carrier: no publication bypass claim. Current GM direct-cutover rule
already forbids nonempty direct combat carriers and magic durations.

Test-only new raw7: type4 across both sides/collections, source-property string/null2,
absence/current neighboring raw1. Real FS generation, complete raw actor/effect
controls, typed empty combat effect carriers and exact actual source/JSON bytes
precede each Error oracle. No fake accepted owner or substituted validator verdict.
One genuine existing signed-QTE nested peer reuses typed helper/production serializer;
its optional expected path/code args preserve existing root-player/NPC assertions.
Extra current empty battle sources are seeded before first snapshot and protected.
Initial selection raw7/60s only; QTE1 compiles/discovers but runtime UNRUN/180s.

No production change yet, all new C# UNCOMPILED. Next independent scaffold SOURCE,
archive current Integration own artifacts, fresh Integration/proofs/catalog/exact
Plan/PREP causal raw7; only after actual RED minimal wound-only refusal/enum/hint
cutover with meaningful inventory and GM negative example/manifest reconciliation.
Review all already-known fixture/doc dependencies together, not serial runtime finds.
Existing effect_legacy_rejection_v1 is the sole explicitly negative legacy-example
section; use it, not new positive legacy snippets elsewhere. Existing canonical
late-refusal peer can be selected narrowly after relevant changes.
Optimization: existing minimal per-test context/source reads, no full world or new
cache/framework; tiny fresh parsing/guards need no cache or authority exception.
No general combat/aggression/defeat/QTE orchestration, new game rule or task IDs.
Parents and all previous UNKNOWN/HOLD remain; raw only outside checkout under
/workspace/tboe-linux-evidence-20261102/combatant-legacy-wound-response.

# Existing T071/T149: dead player parser and retired-alias ban accepted

Independent Sol/xhigh SOURCE, PREP and primary OUTCOME accept this bounded
wounds-only refactor. Supported caller inventory is fixed:10 literal player paths,
only the exact canonical wounds path enters this helper. Strict object/root Error,
message/code/path and WoundCarrierCatalog/error order stay. Unreachable loose
array/object branches and their sole wound object parser are removed; no current
canonical/GM/game rule changes. Retired generatedEffects/healingState/descriptionOfEffects
now have an explicit no-resurrection guard, not missing discovery coverage.
Combat/WoundReference scopes are retained for the separate reachable legacy debt.

Actual evidence is11 distinct PASS in TWO groups, not fresh11:
- D1 source/runtimea7b2a6e493c60939eb072064ad12a2fc0c423543: canonical Integration9
  plus ownership guard1 PASS; discovery1 FAIL on stale positive-token fixture.
  All11 completed, no timeout/dup/underfill, cleanup true. Failure kept verbatim.
- D2 source/runtime654c3b14f705e6bb1813be3ac72cf07ae29736e6: only corrected discovery1
  PASS,1/1 complete, exit0, category3.8479218s/60s, wrapper10.840231s /
  runner9.2655585s. No skip/timeout/duplicate/underfill. Both cleanup true, newTMP[],
  no active managed owners; post-run artifact/resource/source hashes unchanged.
No raw/QTE16, canonical9 or ownership1 repeat was needed for D2's test-only delta.
The independent reviewer confirmed the missed positive-token requirement; all3
zero-hit aliases were examined together and remain explicitly forbidden using the
SAME already-read source images. No new IO/cache/allowance/fingerprint framework.
The new category metadata related-link schema was corrected and actual reader
passed before fresh Unit; this was not a game runtime failure.

D1 fresh Main66.600565s/Unit34.659216s/Integration74.364620s exit0, warning81/26/107
exact prior multisets;45verified copied artifacts/206inputs618copies/native6,
4PEPDB448bindings. D2 fresh Unit36.810026s exit0/26 unchanged warnings,30verified
copied artifacts/206inputs412copies/native4,3PEPDB348bindings. Maina7 and Support/
CrashHostECE remain source-equivalent previous builds, not new builds; no packaged
host claim. Complete catalog1344/13173 is discovery-only. D2 exactPlanone Unit
method/case/60s; NoBuild/Parallelism1, original budgets unchanged.
D2artifact088f205c509d657ce56427372ff426a003c645243857a3a763b6b190f060952f;
D2PDB382b4edf37078ca5e8a1e639c7e6da55ca5dc536cba00d3f203e5fefa137d4af;
D2summaryb9de7a42abe5cdb96144bf9cd08ac32b977f5986dd38f40a934980ca58f3e724;
D2TRX8e9edd85d80ecdfd69302c9190c724d02501134db312cd6cb3bbf00fde74b055.
Full raw/build/source/primary evidence:
/workspace/tboe-linux-evidence-20261102/player-loose-parser-retirement.
Previous own15 and D1 Unit own5 are losslessly archived/member+gzip CRC verified;
no raw, user save, recovery or unique evidence deleted. No raw/binaries in Git.
Optimization: no new production work, retained strict fresh parsing/catalog and
permission boundaries; dead source code/inventory entries removed, no speedup claim.
GM no-update: current direct-cutover contract unchanged; no new authored field or
capability, alias syntax already forbidden. Source/receipt consistency and remote
SHA/bounded immutable source readback are checked separately from runtime versions.

Next independent wound-only code step remains existing T071/T149: causal raw
WoundReference/sourceWoundId retirement and stale999 repair hints. Current public
raw validator accepts them, whereas later strict canonical effects refuse them;
no publication bypass is claimed. Use current minimal real-FS fixture and existing
explicitly negative effect example, not new general combat/QTE mechanics.

Technical R2B/T107 reconciliation accepted by bounded Astra/high DESIGN: afterlife
healing/natural producers exist. Literal same-signed safe bundle plus combat violates
current safe-cycle/no-cycle guards; adding naturalOwner to capture is not justified.
Existing combat healing already uses chronological live composition. A sequential
safe publication -> actual accepted conflict start -> new signed harmful source
needs a real existing start publisher seam, not reducer+fixture canonical writes.
That seam is not established; no journey runtime admitted or broad start system
implemented. This is a precise pending qualification dependency, not a new author
rule or missing-healing claim. Parent tasks remain open.
Wound-only scope, withdrawn T088 question, World9 and all R2/A5 UNKNOWN/DFR/Mortal/
Windows HOLDs remain. No merge/force-push/branch deletion/issue closure.

# Discovery correction category metadata reviewed and read successfully

Independent Sol SOURCE found the new related link used a string instead of the
catalog's required id/when object. Corrected only that metadata before any new
build/runtime. Actual Read-TestCategoryCatalog in scripts/testing/TestCategoryCatalog.psm1
succeeds with1344 categories (metadata only, not discovery or test execution).
Unit correction unchanged; no production code changed sincea7b2a6e4. New SOURCE
delta verification and fresh Unit/runtime remain pending; no failed game result
is hidden by the metadata correction.

# Player parser retirement D1 partial outcome; explicit retired-alias guard correction WIP

Runtime/source a7b2a6e493c60939eb072064ad12a2fc0c423543 completed11/11: Integration
canonical9 PASS, ownership1 PASS, discovery1 FAIL. No timeout/duplicates/underfill,
both cleanup true/newTMP[], clean tree. Wrapper18.403626s/runner17.108056s.
Independent Sol/xhigh partial OUTCOME accepts this exact result, not fullGREEN11.
Summary5c43b995b6bb36eb442b976ea8e5d770335a14a68a1a24470e28e3cc34c90aba;
integrationTRX2acee379bb46529bb16af7047bb1458b17e686ae8892f83d4ba30bd157cce797;
UnitTRX83a8eafde2ee49a8ac5a9a92a122622d6d737700d7a271013857d136e87100b0.
Full primary /workspace/tboe-linux-evidence-20261102/player-loose-parser-retirement/D1-green11.

The guard still required positive production hits for deliberately removed aliases.
It failed first at descriptionOfEffects; later checks were not reached. Root source
inventory examined ALL remaining tokens: exactly descriptionOfEffects/generatedEffects/
healingState are now0; all other retained tokens have current hits. Independent
review confirms this missed fixture requirement and minimal correction: retire3
positive tokens but explicitly forbid resurrection using the SAME already-read
source map. No lost negative coverage, allowance, extra IO/cache or product change.
Fresh Unit and exact discovery1/60s will address only this correction; prior10PASS
remain separate. New source correction UNCOMPILED/SOURCE pending. No new task IDs,
no broader run, no causal-product-failure or speedup claim. Original D1 failure kept.

D1 builds Main66.600565s/Unit34.659216s/Integration74.364620s exit0, baseline81/26/107
warnings exact-multiset match.45used/206inputs618copies/native6,4PEPDB448bindings.
Artifact930d29f434154835ad07090f73eb7d8388daf2876ce1b5963b236610404bae42;
PDB396c3084d17118b7061fce94a4c338432a436148f96c07633c1b3ce85915de52.
An external verifier baseline-path typo was corrected before this proof/PDB/PREP;
no game test ran in that failed verification command. No runtime/limit relaxed.

Wounds-only continuation remains: reachable WoundReference/sourceWoundId raw
legacy effect admission is independently source-confirmed; later strict canonical
validation already refuses it, so no publication-bypass claim. Its stale999 hints
need a separate causal correction. Next after current discovery acceptance.
Existing T081-R2B/T107 journey needs a real separate conflict-start publication
before the next signed harmful source; no suitable existing publisher seam found
yet. Safe/no-cycle rules stay, no general conflict system implemented for the test.
Parents/World9/R2/A5/DFR/Mortal/Windows remain open with prior boundaries.

# Existing T071/T149: unreachable player loose parser retirement WIP

Accepted predecessor5416e381453395600be761e2a0dfddee862fa57c has fresh16 PASS on
9d4b03cf runtime, independent receipt consistency and10 immutable changed-file
GitHub blobs matched. External bounded readback proof SHA256
776f46c73aeca9f69d28073fc4ec685b8ea736b488ab3e15780429f73e56faff; not full restore.
Sol read-only caller/Compile assessment found no supported noncanonical caller:
ValidatePlayerStateFiles has10 literal paths, only exact canonical wounds literal
enters the wound validator; raw dispatcher is already removed. The generic loose
branches and their sole ValidateWoundObject are now unreachable. Remove those
obsolete woundName/generatedEffects/healingState aliases; preserve the exact
current object/root/catalog behavior and issue order in a canonical-only private
helper. This is refactor/characterization, not missing-behavior causal RED.

Only player validation and semantic source inventory change; retarget existing
canonical path allowances, remove5 obsolete alias scopes, keep combat WoundReference
scopes with their next real method boundary. No broad combat/QTE rule, cache,
canonical write, extra source read, changed GM capability or new task IDs. Current
GM direct-cutover contract already forbids these aliases; no prompt/example update.
New code UNCOMPILED/SOURCE pending. Existing category wounds-canonical-player-carrier-root
selects9 Integration JSON-kind/current-carrier rows +2 Unit ownership/discovery
controls, unchanged60s per descriptor. Raw/QTE16 unchanged and not repeated.
Source/persistence/fresh-authority checks and every UNKNOWN/HOLD stay intact.
Optimization: remove dead code; existing parsed root/strict catalog path unchanged,
no new IO/cache/lifetime/permission or speedup claim. Fewer source inventory entries,
no added rereads/framework. Next SOURCE, affected builds and exact11 PREP/runtime.
Raw root /workspace/tboe-linux-evidence-20261102/player-loose-parser-retirement.

Separate T081-C3-R2B/T107 source/design reconciliation: current afterlife producers
exist, but literal same-signed safe bundle plus combat conflicts with approved
safe-cycle/no-cycle separation. Do NOT add naturalOwner to capture merely for the
old checkbox. Genuine combat healing is live chronological; next legal journey is
published safe healing/recovery then a NEW signed combat turn with restored baseline.
No new game rule/author question; source/evidence map in progress, parent stays open.

# Existing T149/T071: exact legacy player/NPC wound input accepted — 2026-11-02

The current scope is wounds only. General defeat/aggression, withdrawn T088 rule,
QTE/ordinary-turn orchestration and ACTIVE/F1 remain outside this implementation;
their project tasks are not marked complete. Wound acquisition/state/treatment/
diagnosis/recovery/display, persistence and necessary integrity checks stay in scope.

Independent Astra/high SOURCE, PREP and primary OUTCOME accept runtime/source
9d4b03cf87c88939ca5708547dc21ca00a86df9e. Actual addressed16 unique PASS in ONE
fresh group: player raw7, player genuine-QTE peer1, affected NPC raw5/NPC-QTE1,
semantic inventory/closed-projection guards2. Five of five descriptors/10methods/
16cases complete, exit0, no timeout/skip/duplicate/underfill. NoBuild/Parallelism1,
original60/180/60/180/60s budgets. Wrapper32.913938s/runner31.571298s. This does not
relabel old NPC20/state3/App or other historical results as a fresh broader run.
All actual refusal, dedicated classification, lawful adjacent response, exact
submitted JSON, canonical bytes and signed source/authority assertions reached.
Both cleanup flags true, newTMP roots[], no active managed owners, clean runtime tree.

The actual P1 causal6FAIL/1PASS is retained separately on8f30bf18: admission4 lack
Error (empty object has Warning only); null/scalar2 already refuse shape but lacked
dedicated Code. Valid canonical effects fixture corrected before that build after
independent SOURCE finding; this fixture finding is not a product failure. Full
raw/summary/TRX/build/source evidence remains outside checkout in
/workspace/tboe-linux-evidence-20261102/player-legacy-response.

Production reuses the existing exact-presence refusal, now shared by the two
client-owned retired property names. Only player raw dispatch changed; its sole
unused loose dispatcher is removed. Canonical carrier branches and production QTE
are untouched. No general unknown-field/casefold ban, fallback, automatic game
rule, direct writer or publication-bypass claim. Existing woundDecisions/source/
permissions/current authorization remain authoritative. Inventory owns both
refusal seams meaningfully; no mechanical source-pin rebasing, World9 stays open.
GM no-update rationale independently accepted: active guides/examples already use
offered woundDecisions, no exact retired field remains in active GM content; this
adds no authored capability/field and changes no afterlife matrix behavior.
Optimization: reuse parsed root and already-read source images for added assertions;
one tiny property probe, no new production IO/cache/key/lifetime or authority
exception. Source inventory keeps ordinary per-entry reads; no speedup claim.

Fresh builds on9d4b03cf: Main72.869832s/Unit37.088738s/Integration70.462221s all exit0,
warning multisets81/26/107 match prior.45 used managed outputs,206 resource inputs/
618copies/native6;4PEPDB448sourcebindings. SupportECE source-equivalent dependency
is not a fourth fresh build; unselected packaged hosts are not qualified. Complete
catalog1343/13173 is discovery-only. P2artifact SHA256
5526eeff5dc725ead5d6d07fee693eaf05c8fc8391cf435a52fdfd5e4b7c46cb;
P2PDB6e70f6cd04f7348745f39fb98741a23a6f25a289ec09350e1abdfb4979575dee.
Summary74a43ed93dacba24e6e8aa381a04eff37b7632a9408a12d13bf25664cf1ac4fd;
guardsTRX6a03ae045cf53680667ce051e789bbc2197945b9b2d09e9c529e901f37195dad;
NPCQTEbbfea4d31b9aa0af6391580d5b4a447695a5a4bc0bb014f57a1bd6a2b800c4fd;
NPCrawe087447c63a886645ff62a125675014a3c336b17202a668fd9f1ecbf3ce7e3fd;
playerQTEe11d5a7ccffea51710394e9ed87d14359b05426984771ce60c792f674caa4668;
playerrawa0fc07762256dc40e1c63146d74e0aaadd7503fa5c27f8ab02a63d15eca273ad.
Root independently verified all16 unique TRX rows and completed ownership cleanup.
Current pre-rebuild own15 artifacts are losslessly archived/CRC/member-verified;
no raw, saves, recovery or unique evidence deleted, no diagnostic output in Git.

T149/T071 and whole1536 stay open. Next direct wound step is source reconciliation
of existing T081-C3-R2B/T107 lawful initial healing/recovery stage plus live spiritual
wound insertion: afterlife producers now exist, so do not repeat the obsolete
September no-producer assumption or fabricate a stage. Sol bounded read-only
assessment is in progress. Other wound-specific remaining cells/examples/privacy/
rollback/index/scale still need exact reconciliation. Full physical browser R2 and
joint terminal A5 remain UNKNOWN; DFR/Mortal/Windows HOLDs do not auto resume. No
merge/force-push/branch deletion/issue closure. Receipt SHA/ref/bounded source readback
is verified separately; test source stays9d4b03cf, not a later documentary commit.

# Existing T149/T071: causal player raw defect fixed in source, runtime GREEN UNRUN

Source8f30bf1824808af707c2f25f961e0ad43d3072d4 actual P1-red7 is independently
accepted:7/7 complete,6 expected FAIL/1 PASS, no timeout/duplicates/underfill,
both cleanup flags true, newTMP roots[], clean tree. Arrays2/named object lack any
issue; empty object gives only Warning:4 real admission failures at line67 after
valid canonical/lawful/byte premises. Null/scalar2 reach existing Error/single/path/
severity and fail only Code=null at71; RepairHint UNREACHED. Current control PASS.
Wrapper13.349420s/runner11.965594s. Summary SHA256
9c4ce1aee5e4fe5f1ad7d7c964be00e012f3abfb0da2c54f2bfaf47716d4b138;
TRX04e388f21cc7d0f367a8e844e80f84945782ae9654f1de80627218fbcbf8089e.
Full capture /workspace/tboe-linux-evidence-20261102/player-legacy-response/P1-red7.
Fresh Integration76.155930s exit0,107 unchanged warning multiset;25 used managed,
206inputs412resourcecopies/native4;3PEPDB366bindings, Main190/SupportECE unchanged.
Catalog1343/13173 discovery-only; exact Plan raw7/3methods1descriptor60s admitted
by independent PREP. Builds/discovery are not qualification.

Minimal production delta: reuse the existing exact-property-presence refusal for
both retired player/NPC commands. Replace only player raw dispatch; remove its
now-unreachable private loose dispatcher. Preserve canonical validation branches,
current woundDecisions, source/authority/freshness and all general QTE behavior.
Same dedicated code/private Actual/current wound repair, no payload disclosure.
Inventory now owns the player refusal explicitly and checks old dispatch absence
using its already-read source image; no mechanical source-pin rebasing. Existing
nine World pin mismatches remain open; player source was already one of those.
Active Rules/TaskGuides/OtherGuides/Examples have no exact retired response tokens;
current worked wound guide already requires offered woundDecisions. No new GM
field/capability or afterlife matrix behavior: no prompt/example update required.
Optimization: already parsed root, one tiny exact property probe, no new IO/cache,
no retained mutable data/authority, all fresh boundaries intact. Current managed
own15 before rebuild archived losslessly and gzip EOF/CRC verified (27,602,871B,
SHAa3659d14536b8b9eb5f0950c7e271a04f45d726a6bcf5a9dae36b2fd28176bb8),
no deletion. Raw and source/proof archives stay external.

SOURCE pending; C# correction UNCOMPILED. Next fresh Main/Unit/Integration builds,
current artifact/source proof, discovery/exactPlan/PREP addressed16: player raw7,
playerQTE1, NPC raw5/NPCQTE1 (shared-helper dependencies), changed semanticguards2.
No unchanged canonical/state3/App20/fullworld replay, publication-bypass claim,
new game rule or broad QTE/ACTIVE/T088 work. Existing UNKNOWN/HOLD/parents stay open.

# Player raw scaffold SOURCE correction — runtime UNRUN

Independent Astra/high found the new fixture seeded NPC-style entries in the
player effect carrier. Corrected to current activeEffects[] and added the existing
typed EffectMaterializationContract.ValidateCarrier(Player) prerequisite before
write. No production change, test-oracle relaxation or new helper/framework; this
pre-build fixture finding is not a product failure or causal RED. New source must
be freshly built; previously accepted NPC outcomes stay separate.

# Existing T149/T071: player legacy raw input causal scaffold WIP — 2026-11-02

Base c7e8b5cbff9f2c194902472a1e473d890b81f189 is remote-verified; NPC narrow
receipt correction independently accepted,12 immutable changed-file GitHub blobs
matched (bounded readback, not full restore). External proof SHA256
6eee4ef5fa568388c9fb4804ae449188b22f6fd82f011a8084a14532e39c1ade.
Wounds-only next result: reject exact retired playerWoundChanges before raw
admission/typed projection, preserve woundDecisions and current adjacent commands.
No publication bypass is claimed. Sol independent read-only trace confirms four
loose array/object forms lack an Error; null/scalar already have generic Error.
Test-only scaffold7 plus genuine signed QTE peer1; initial selection raw7 only.
No production changes yet, all new tests UNRUN. Existing QTE helper is reused for
both exact wound fields with per-test state, full baseline and source byte checks;
shared-helper NPC peer will regress only after relevant changes. Canonical player
validation remains unchanged; parent T149/T071 stay open. No new task IDs/rules.
Optimization: existing minimal real-FS generation/context and parsed input; tiny
fresh validation has no worthwhile cache. No extra world, callback, cached permit,
new diagnostic stack or weakened checks. Current GM contract already demands
woundDecisions and no legacy command; review will confirm no-update rationale.
Plan: independent scaffold SOURCE; archive owned current Integration artifacts;
fresh Integration build/proofs, exact discovery/PlanOnly/PREP raw7 causal; minimal
shared existing legacy refusal after actual RED, semantic source/inventory review,
affected builds and addressed GREEN/QTE/NPC peers. Existing UNKNOWN/HOLD unchanged.
Raw root /workspace/tboe-linux-evidence-20261102/player-legacy-response; no raw in Git.

# Existing T149: narrow NPC wound-input refusal accepted, 2026-11-02

Current task is wounds only. General QTE/ordinary orchestration, defeat/aggression,
T088's withdrawn proposal, ACTIVE/F1 and independent Saref memory are outside this
implementation; their project tasks are not marked complete. Patient consent and
all current wound source/permission checks remain mandatory. No new game rule.

Independent Astra/high DESIGN/source-assessment, SOURCE, PREP and primary OUTCOME
accept the narrow fix. Exact old NPCWoundChanges now produces a dedicated raw
legacy Error for every value shape; it cannot masquerade as a valid NPC command
or receive an obsolete array-repair hint. Current NPC rename/absence stays valid.
Only wound-input dispatch changed: general QTE code is unchanged. Its genuine
signed ordinary offer peer now refuses that wound field before typed projection.
Canonical NPC legacy-only/mixed retains the strict carrier error; metadata and
actual carrier bytes remain intact. Intentional legacy token is constrained to
a meaningful refusal owner; obsolete migration/structured allowances are removed.

Evidence is not one fresh11 run:
- C1 causal at b7c68a8b58d122be5f9d9e899f01ef3bf27b413e:4 expected FAIL/1 PASS,
  complete5, arrays2 actualissues=[] and two distinct null/scalar classifications.
- C2 at190c6427192f28689b4f77dcd6c0b9eced10eab1: changed semantic Unit guards2
  PASS; QTE fixture baseline1 FAIL; raw5/state3 UNRUN. Original failure is retained.
- C3 atb36547e001020b794f7823ed33442380152f8c5b: remaining9 unique PASS,
  3/3 descriptors/6methods/9cases complete, exit0, wrapper19.457888s /
  runner18.230666s. Exact categories wounds-npc-legacy-qte-offer1,
  wounds-npc-legacy-raw-response5, wounds-npc-legacy-state-priority3;
  NoBuild/Parallelism1, original180/60/60s. No skip/timeout/duplicate/underfill.
Together11 distinct PASS across two GREEN groups, no whole T149/#1536 closure.
All strict QTE baseline/current/request/manifest/authority/offer byte and raw
refusal assertions are reached; source guard/data priority are independently
confirmed. These are admission/read-only checks, not a publication-bypass proof.

C2 builds source190: Main63.376760s/Unit35.386800s/Integration74.507839s exit0,
warning multisets81/26/107 unchanged;45 used outputs/206inputs618resourcecopies/
native6,4PEPDB/447sourcebindings. C3 fixture-only Integration72.601722s exit0,
baseline107warnings;25usedoutputs/206inputs412copies/native4,3PEPDB365bindings.
Main/Unit retain190, SupportECE; only Integration is fresh inC3. Exact discovery
1341categories/13169methods-files has no runtime. All bound hashes rechecked.

The only failed follow-up was fixture authoring: default typed serialization
emitted optional null scoreModel/scoreDeltas. The fixture now uses the SAME
SharedJsonOptions profile as production at all three sites; strict guards,
source/binder, assertions and budgets stay. No production scoring change.
Optimization: existing parsed root and operation-local source strings reused;
no new IO/cache/lifetime/key or fresh-permission exception. Private repair Actual
contains no submitted payload. Current guides already specify woundDecisions and
direct cutover, so no new authored field/example is required for this refusal.
One Lifecycle pin was semantically reviewed and updated; nine pre-existing World
pin mismatches stay unmodified/open, no full World-source closure PASS claimed.

Full raw: /workspace/tboe-linux-evidence-20261102/npc-legacy-response.
C3summary422071af5fba0236cf0330b387feab0c6a98ef67366b8028e8a371a8e2e7032e;
QTEtrx3e34b062d9c72c018d07e0e636a2e980cd2b0f284c058cad73f5b04225a77966;
rawtrx1d7f84c00fde38a2b591c5ce13ae4e2260d6c7ca22988ff4039e9b1297b8b457;
statetrx76c4387d0a962b91adb2912e03d153d246fb49834f9894061c20dbf46ac937ab.
C3artifactfba023c6ebcae7cd31f0ec10990b3fe088e891a9894bf9a1892ad7edcfeff0cc;
PDB3506f4917534a751f3431452c160993e2b6ecb8278d5584cf9d76b0c1f73fd1a.
Both cleanup flags true, no new TMP roots/active owners, tree clean after runtime.
Raw and prior own-artifact archives remain outside Git; no unique data deleted.

Remaining wound-only work: raw playerWoundChanges source-admission debt confirmed
by read-only trace (not runtime yet); remaining direct spiritual-wound combinations;
full physical browser/course and joint wound terminal UNKNOWN, plus wound-specific
examples/privacy/replay/rollback/index/bounds and final scenario reconciliation.
No general QTE/ACTIVE/defeat task is inferred as a dependency. R2/A5 and historical
DFR/Mortal/Windows HOLDs stay honest and are not automatically resumed. No merge,
force-push, branch deletion or issue closure. Final receipt commit/ref/readback is
checked separately; runtime source remains b36547e, not a later documentary SHA.

# Wound-input C2 partial result; fixture serializer correction WIP

Runtime/source190c6427192f28689b4f77dcd6c0b9eced10eab1 completes only3 of11:
source guards2 PASS, QTE1 FAIL at its strict baseline, raw5/state3 UNRUN. Runner
stops as configured; no timeout/skip/duplicate, cleanup complete/newTMP roots[].
Wrapper18.243494s, runner16.906197s. Summary
f739a0b7b428eee7ce5a60574098104fdce0f4850f9f41c70540429b47d43257;
guards TRX248a362b33a85959c49432b580c4dff9c591bb6aeeee1b6e165269d4e9f57dab;
QTE TRXa2db34c4af02be87eafcec37060fd538548778103055056ea15ac8ae0d9855d0.
No wound refusal/publication oracle was reached in QTE; this is not a product RED
or a partial GREEN11. All original raw stays in npc-legacy-response/C2-green11.

Cause verified in production: QteSceneService.JsonOpts is the existing
SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, whose WhenWritingNull omits absent
scoreModel/scoreDeltas. The fixture used default Serialize(offer), emitting both
as JSON null; raw validation correctly refuses them. Minimal test-only correction
uses the exact production profile at all three serialization sites. No field is
removed after acceptance, no validator/assertion/limit/source/signature is relaxed.
New serializer/cache framework is not added. Shared options are only read; per-test
source/root/offer ownership stays independent. General QTE mechanics are unchanged.

After independent partial OUTCOME and delta SOURCE: fresh Integration only,
unchanged Main/Unit/Support source/output proof, exact Plan/PREP remaining9
(raw5/QTE1/state3). Do not repeat accepted unchanged Unit guards2. Each current
wound-input case still must complete with actual primary results. Original
60/180/60s bounds remain. New fixture correction is UNCOMPILED/UNRUN; whole
World-source guard and all historical UNKNOWN/HOLD stay explicitly open.
Current task remains wounds only; withdrawn T088/aggression and broad ACTIVE/F1
are outside this work, not completed. No new game rule or live GM/save test.

# Wound-only raw NPC correction SOURCE accepted — fresh builds next

Independent Astra/high accepts 679e3ff557f570ab57f43dde84fb8824fd9082aa plus
this explicitly reviewed single Lifecycle pin update. The two-line allowed-key
removal has no new reads or changed non-wound policy. Nine pre-existing World
source-pin mismatches remain explicit and unmodified; the full World guard is
not claimed PASS or waived. No generic source/cache qualification is added.
Nonblocking wording corrected: withdrawn proposal concerned a post-defeat /
repeated-aggression restriction, not the existing mandatory patient consent.

The whole wound-only production/inventory/fixture/selection block is SOURCE
accepted: exact legacy response refusal, canonical legacy priority and valid
metadata/current NPC behavior. GREEN11 admission still requires fresh affected
Main/Unit/Integration, used dependency/resource/native/PDB bindings, exact catalog/
Plan and separate PREP. All previous budgets and raw retention remain. No T088,
aggression/general QTE/ACTIVE implementation or old HOLD runtime is authorized.
New production/guards and the single pin are UNCOMPILED/UNRUN; no GREEN claimed.

# Current owner scope: wounds only — supersedes broad remainder audit, 2026-11-02

Owner narrowed this task at11:00UTC to wounds: acquisition/state, treatment,
diagnosis, directly related recovery, display and necessary validation/persistence.
General aggression/defeat rules, general ordinary/QTE orchestration and standalone
systems are outside current implementation. T088's author question is withdrawn;
its proposed post-defeat/repeated-aggression restriction is NOT accepted and must NOT be implemented. No answer
is required. Historical T088/ACTIVE/F1/T197 tasks stay open outside this scope,
not falsely complete. A broad ACTIVE/F1 route is not a mandatory wound dependency
without an actual direct source/consumer requirement being established first.
Saref memory1571 remains separate. The prior60-line whole-project audit is retained
as history, superseded for current scope and execution order by this entry.

Revised wound remainder: finish this narrow legacy NPC wound-input refusal;
reconcile direct spiritual-wound source/cap/per-side create/worsen/guarantee and
initial-healing-plus-insertion cells already tracked under T081/T107; close the
full physical browser course and joint wound terminal UNKNOWN only after their
cause-backed admissions; map wound diagnosis/healing/recovery/UI/History/replay/
compensation scenarios to current evidence. Finish wound-only examples, privacy/
terminology/untrusted text, ownership/index/bounds and byte/existence rollback
coverage plus final focused review. Do not reimplement accepted journeys from
empty parent boxes. General DFR/T609/Mortal compatibility/Windows HOLDs are not
silently released or converted into new required non-wound implementation work.
Any direct dependency must be justified narrowly; existing UNKNOWN remains UNKNOWN.

C1 raw5 causal OUTCOME is independently accepted at source/runtime
b7c68a8b58d122be5f9d9e899f01ef3bf27b413e:5/5 complete,4 FAIL/1 PASS,
11.591793 s wrapper /10.436654 s runner, no timeout/duplicate/underfill, both
cleanup flags true/no new TMP roots/no active owner. Arrays2 fail only missing
Error with actualissues=[] after genuine controls/byte checks; null/scalar2
pass Error/path/severity and fail classification expected_array. RepairHint
assertions are UNREACHED there. This is not publication-bypass evidence.
Summary87004dc8a9b3f57d9e4340ddcc288c2fd8437e437d207a2175d3cbdf1c3246cf;
TRX79b9f329b04e74d72bb37cb70045e414d3991fde2269ababb7f1073c40687c94.
Full raw remains /workspace/tboe-linux-evidence-20261102/npc-legacy-response/C1-red5.
Fresh Integration exits0/71.322146s/baseline107 warnings; current25 managed outputs,
206 resource inputs/412 copies/native4 and3PE/PDB/365source bindings verified.
This is one fresh build; dependencies retain their explicit current lineages.

Only wound-input production WIP is authored: dedicated exact NPCWoundChanges
refusal instead of identity-only acceptance; remove its obsolete structured/state
allowances. On exactly canonical NPC effects with that property, reuse the existing
carrier validator on the already-read root before generic missing-key early exit,
preserving duplicate/root priority without changing other files/nonlegacy ordering.
QTE mechanics are unchanged; its public offer peer checks only this wound field.
Semantic inventory retains one intentionally forbidden-field constant under an
explicit refusal owner, not a migration allowance. New negative source checks
reuse existing operation-local read strings. No cache/source key/lifetime, fresh
authority check, file IO, allocation, scheduler, general gameplay or GM field changes.
Current guides already use woundDecisions/direct cutover; exact legacy names have
zero matches in active Rules/TaskGuides/OtherGuides/Examples and known daemon entry.
No new GM example is necessary for refusing an already unsupported wound route;
independent final SOURCE still assesses this rationale.

SOURCE and fresh Main/Unit/Integration are pending before GREEN11: raw5 + genuine
QTE1 + canonical state3 + changed semantic guards2. Existing budgets60/180/60/60s;
previous T14920/B1/App are not repeated wholesale. All new production/guard C#
is UNCOMPILED/UNRUN. A read-only70-row World source-pin baseline found9 mismatches
already atb7 before this patch. Current changes touch two entries; their narrow
impact requires review, not mechanical rebaseline of unrelated World code or a new
general qualification loop. No whole World source-closure PASS is claimed.

# T149 raw NPC response refusal — test-only WIP, 2026-11-02

Existing T149 / FR-013 / FR-099 continues from e927e1d6. No new task or author
rule: exact NPCWoundChanges presence is retired. Production and semantic source
guards stay unchanged until independent SOURCE, fresh owning build and actual RED.
The public ValidateResponse path currently accepts empty and identity-valid arrays;
null/scalar already refuse shape, so their dedicated legacy classification is a
separate oracle, not missing-admission evidence. Proposed refusal is Error at
response.NPCWoundChanges, code wound_materialization_legacy_response_unsupported,
without an effectChanges repair hint. Genuine absence/current-NPC controls precede
target oracles. Raw JSON and existing canonical carrier bytes are checked directly.

Prepare five raw Integration rows in three exact methods, selected alone at 60 s.
Prepare but leave unselected: one public accepted-QTE-offer peer (180 s), reusing
LiveTurnPreparationService, the existing ordinary signed-source binder and a valid
typed offer; and three canonical NPC state rows (60 s) covering legacy-only/mixed
strict-carrier priority and legal metadata. Keep the existing unsupported-root and
personality tests unchanged. No publication-bypass, Engine, live GM or save proof.
Each row owns mutable real-FS state; tiny raw parsing adds no worthwhile cache.
QTE preparation is fresh once per test and retains actual source/owner checks.
All new C# is UNCOMPILED/UNRUN; protective bounds are not measured costs. Root owns
build/discovery/Plan/PREP/runtime and persistence. GM-facing update impact is assessed
with the later reviewed refusal; this scaffold changes no authored/runtime contract.
Accepted T14920/B1 four, R2/A5 UNKNOWN, all HOLDs and pending T088 semantics remain.

Scaffold now owns wounds-npc-legacy-raw-response (5 rows / 3 methods / 60 s),
wounds-npc-legacy-qte-offer (1 / 1 / 180 s) and
wounds-npc-legacy-state-priority (3 / 2 / 60 s). Only the first is selected.
Existing qte-rules/npc-social-trade wildcard owners and all earlier selectors stay
unchanged and unselected. Source expectation for raw5: two missing-admission FAIL,
two classification FAIL, one control PASS; these are not executed results.
Metadata-only Read-TestCategoryCatalog and Get-TestCategoryCiMatrix exit 0:
1,341 categories and exactly one selected Linux category. External primary log:
/workspace/tboe-linux-evidence-20261102/npc-legacy-response/scaffold/metadata-reader.log.
No build, discovery, Plan, test, commit or push was performed by the scaffold writer.

# Reconciliation accepted; T088 author semantics pending; continue existing T149

2026-11-02: independent Astra/high and Sol/xhigh accept consistency of the whole
remaining-work audit at 811060956af6cb6bd7fe4748c22b9ed20512d575. This is an audit,
not a SOURCE acceptance of all ACTIVE code, whole-feature completion or HOLD release.
B1 receipt at2aa36647 and source/runtime eb879e2d remain independently accepted.
Full six-file immutable remote readback matches exact bytes/git blobs, proof
1be7a5febc656b5bb9df048f0711c09bbd863bf1aebc77a548016edd4163a756 outside Git.

T088 has a concrete missing game rule, not an implementation-permission problem.
Existing combatConditions/controlState are transient: resolution clears activeConflict,
recentConflicts is bounded history, and afterlife_combat_condition targets only a
spiritual_conflict_side for exchange/uses/scene lifetime. They cannot enforce the
required post-conflict bound. FR-037 does not define the same-aggression key, exact
forbidden actions, or a lawful removal predicate. Metadata-only scope/end strings
would not implement it. A single author question is pending through the task UI:
proposed first strict option is no new conflict by the defeated side against the
winner until explicit confirmed winner consent, without an automatic duration.
This is a proposal, NOT accepted rules; no silence or elapsed time approves it.
A different exact restriction/removal event may be supplied. No cooldown/cycle,
permanent actor effect, Saref memory rule or new authority is invented meanwhile.

Independent permitted continuation uses existing parent T149 / FR-013 and FR-099;
no new task IDs. First only a coherent test scaffold for raw public ValidateResponse
presence and its genuine QTE offer consumer, plus current canonical NPC state refusal
order/metadata. Real missing-admission array cases must be distinguished from existing
null/scalar shape refusals with a different classification. Genuine baselines and
actual input/byte invariance precede final oracles; no claimed publication bypass.
Use current typed helpers/public APIs and real pending source where needed, no new
reflection or fabricated authority. Separate cheap raw causal cases from genuine
QTE/state peers, document exact owning categories and pre-runtime budgets. Production,
semantic sourceguard retirement and previous accepted GREEN20 remain unchanged until
actual causal evidence and independent review. No live GM/save or HOLD runtime.

# Whole #1536 remainder reconciliation — 2026-11-02

Read-only audit of canonical spec/contracts/tasks, current production and accepted
leaf receipts at 2aa366476a5a102e07f311baa92ecf370713fd3a. Root owns this summary;
independent Astra/high examined US3/DFR and Sol/xhigh examined US4–8/cross-cutting.
No new task, test, production edit, author decision or HOLD release follows from
this audit. Parent boxes and old September notes are not current code inventories.
There is more remaining than a pair of tests; no percentage or completion date is known.

## Concrete implementation and connected-route remainder

| Existing IDs | Actual remainder and source | Next completion criterion |
|---|---|---|
| T079/T083/T088, FR-037, T081-E | Confirmed missing mandatory bounded non-training defeat outcome. AfterlifeSpiritualConflictState.HasCompleteResolveResolution accepts result/opponent evidence without the required scope/end condition. Existing soul-dissipation authority is separate. | Complete T088 against the approved conflict rules: a lawful bounded outcome prevents immediate same-aggression retry, is retained/applied, and absent/invalid scope or end refuses. Preserve training and explicit optional dissipation; no invented restriction/tariff. |
| T149/T071 | Canonical player and NPC carrier retirement is accepted, but reachable ValidateResponse -> ValidateNpcContract still identity-validates NPCWoundChanges. QTE terminal responseFragment calls that raw validator. This is source-confirmed raw-admission debt, not a proven publication bypass. Generic structured/state allowances also remain. | Genuine public raw-response and QTE causal evidence, then explicit legacy refusal before typed projection plus semantic source guards. Preserve canonical legacy-only error priority, metadata and other NPC sections; no effectChanges repair hint for wounds. |
| T179/T192, T178–T198 lawful ACTIVE / FR063 | Real N/I observations, existing v3 basis/consumer pieces, retained common calculations and pure action/time prerequisites exist. Full S/common/M3/F1 live+cold owner chain and complete active continuation remain unfinished. MortalJointActionBoundary explicitly remains detached T192a; current MortalJointInactiveTail refuses new QTE and unprocessed stats/levels. | Complete the existing staged genuine-source -> common/M3 -> fresh/cold proof route without activating a partial route. Reconcile newer N/I/DFR components before implementing any old planned primitive again. |
| T180–T183; T184–T189; T193–T196 | Connected ACTIVE automatic/projection/story/no-offer completion, QTE capture/accept/decline/ordinary F1 result, selected terminal/post-QTE/cleanup and complete ordinary family/action-time composition are not established by their accepted pure prerequisites. See design/lawful-active-joint-tail-b-implementation-plan.md and lawful-active-joint-tail-b-ordinary-outcomes.md. | Finish actual owning consumers and atomic/replay/cold joins in the dependency order already specified; complete lawful ordinary outcomes, not a guessed whitelist. T190/T191/T198 then own connected UI/platform acceptance. |
| T197 | Separate MortalDeath -> LifeEvaluation continuation/design remains incomplete/conditional in design/lawful-active-joint-tail-b-death-handoff.md. | Resolve its existing target/source/restore contract dependencies and required design/implementation gates. T069's typed death_contour handoff is already implemented and must not be reimplemented or called missing. |

## Implemented substantial behavior, incomplete overall acceptance

| Existing IDs | Current status | Next criterion |
|---|---|---|
| T076–T092 / T081-C/C2/C3/D/E/C5 | Spiritual runtime is present: SourceWitness calls opportunity math; admission, per-side decisions, guarantee_satisfied, pending/checkpoint/replay, live insertion and common publication have bounded accepted receipts. The base T084 note saying math has no runtime caller is historical. | Reconcile remaining exact operation/result/eight-profile/start/terminal/preview/docs cells, including result-closure/position dependencies; implement only proven missing cells. |
| T081-C3-R2B / T107 | Initial healing/recovery together with live spiritual insertion remains an unaccepted combination. Current combat-healing reducer exists, so the old September claim that no producer can exist is not a current proof. | Trace today's lawful producer through this exact combination and qualify it without fabricated stages or relaxed realm/source checks. |
| T063/T070/T071–T075, T121-PHYSICAL-GUIDED | Mortal diagnosis and all three guided treatment modes have production paths and bounded native/handler/console/rollback evidence. Full browser course and linked terminal/native frontiers remain unaccepted. | Map actual mode/cut evidence and finish only missing connected acceptance under its gates; do not reimplement the shared physical handler. |
| T093–T112 / US4 | Arts/resolver, safe self-healing, natural recovery, combat self-healing, Guardian/resident/Shining aid, world-cycle/progression/History/replay exist with accepted bounded journeys. T112-SOURCE-PINS is closed. | Reconcile all eight US4 scenarios, actor/realm/progression/catch-up and differing-current counterharm compensation. Whole US4 is not yet accepted; missing receipt is not proof of missing code. |
| T113–T130 / US5 | Target choice, foreign detail, History, aliases/shared handlers, spiritual and Mortal diagnosis, cards and actual App read-only route exist. Eight spiritual requirement adapters are implemented: item_quantity/resource_quantity/consent/facility/location/quest_state/effect_state/environment. Their141 PASS span seven lineages, not all-eight native/UI. | Reconcile navigation bound and complete target/method/confirmation/result/stale/error parity. Actual-App desktop/mobile controlled HTTP proves frontend DOM/emissions, not backend/packaged/live-provider/save authority. |
| T131–T147 / US6 | Quotes/payment, Elyara current locality/life, Shining healer role/access, public and negotiated aid are implemented and accepted in bounded scopes. T142 historical-faction exception is resolved. | Close whole scenario/evidence mapping and any genuinely missing consumer; no new economics or repeat of accepted journeys solely for parent boxes. |
| T167–T177 / US8 | Scope union, offered/current authority, RollSkillId, common reducer, dormancy/lifecycle/replay/rollback/catalog/GM cutover T167–T176 are implemented. | Reconcile whole T177 verification/review against eight US8 scenarios and exact existing receipts; do not invent a missing scope runtime from that parent checkbox. |

## Cross-cutting acceptance and separate holds

| Existing IDs / gate | Remaining obligation or limit | Next criterion / admission |
|---|---|---|
| T148 / #1571 | M01 proves historical metadata survival only; E01 real independent memory-suppression survival is unqualified. GitHub issue1571 is verified open on2026-11-02. | Keep separate follow-up scope; its existence satisfies tracking only. Do not implement the independent memory runtime or make all #1571 an automatic blocker of wound development. |
| T150, T151, T152 | Guidance4, Russian Help label, markup/privacy/C0 output controls and display boundary4 are accepted leaves. Full examples/manifest, whole Russian/privacy surface and adversarial bidi/confusable/oversized/recursive matrix are NOT_RECONCILED. No new concrete leak is asserted by this audit. | Requirement -> real consumer/example/parser -> existing exact case/receipt map; fix only proved omissions. Preserve current Unicode rules; four parser cases are not renderer or full-T152 acceptance. |
| T153, T154 | Indexes/bounds and real rollback machinery exist. Whole scale/complexity and wound/effect/resource/item/profile/scheduler/journal/quest/output failure-cut union are NOT_RECONCILED. | Map indexes/cases and surface x cut x byte/existence oracle to receipts; no assumed performance defect or blanket repeated suite. |
| T121 browser R2 | Full browser physical course TIMEOUT180.1041994/180s, no completed TRX/case; prefix retained in current browser-read-recovery/R2-browser-course. Accepted cheap36 and unrelated App reads do not replace it. | No blind retry. A cause-backed bounded correction/admission, fresh bindings/resource checks and independent PREP must precede another original-budget full course. |
| T121 joint terminal / T152 A5 | Full joint terminal TIMEOUT120.0346224/120s on c9c9103b6, compiled9383e6e7; no TRX. Anchor2/output10/controls3 are separate accepted evidence, not full terminal PASS. Current prefix/root retained in notification-controls/A5-terminal1. C9/C10 native UNKNOWN remains distinct. | Preserve UNKNOWN and exact captures; no new retry is admitted. Establish a concrete cause/correction and separate unchanged-budget PREP before the full peer. |
| T597/T598/T601/T602, DFR | Last historical full positive TIMEOUT300.1806894/300s, no completed case/TRX/report.69 distinct component PASS are accepted separately. | Full positive remains HOLD; only after separate justified admission, fresh complete preparation and original300s/full capture, then dependent invalid-prefix/source-drift/v1/Prepared0/Published2 cold gates. |
| T609-WIRE/RED/GREEN; T603/T607/T608 copy-cost | Parked source/cost/optimization qualification, not missing gameplay. Helper/component evidence does not prove full cause or overall budget fit. | Do not resume automatically. If separately reprioritized: genuine two-Compose causal source reduction with fresh/error semantics before activation; no inclusive-time speedup claim. |
| Mortal compatibility / broader ordinary envelope | Historical three-case180s timeout and broader ordinary60s/no-TRX UNKNOWN are not replaced by later bounded Mortal diagnosis/healing PASS. Old20261025 raw is unavailable in this recovered machine. | Preserve historical status/indices, identify recoverable evidence and only then cause-backed original-budget PREP if this HOLD is released; no fresh PASS inferred. |
| Windows / first Guardian / cross-OS DATA | Windows deferred until materialization completes; new shared-code Windows PASS is absent. First Guardian intermittency and historical Windows DATA newline compatibility remain open. | Platform-specific final qualification and targeted issue resolution, not substitution by Linux/generated fixtures. No Windows job/artifact/CI retry automatically. |
| T155–T158/T163/T177 inherited commands | Old Fast/FullValidation/RegressionIntegration/PreMerge and npm verify prescriptions are superseded by current category policy. | Use justified exact categories/frontend scenarios, never execute retired aggregate commands as supposed mandatory work. |
| T159–T165; T166 | Whole FR/acceptance/quickstart matrix, coherent final independent review, resolved findings and final source/example/artifact/PR checks are unfinished. Current work is checkpointed, not feature-PR accepted. | Complete existing functional/gated obligations, reconcile current evidence and final review/diff/checks. Merge/force-push/issue closure remain outside current authorization. |

Evidence availability: /workspace/tboe-linux-evidence-20261025 is absent; historical
raw/binaries are not claimed recovered. Current full raw stays outside Git at
/workspace/tboe-linux-evidence-20261102. Latest completed blocks: T14920 PASS on
5950b74a and T1524 PASS on eb879e2d, distinct runtimes. New task creation, HOLD
execution, broad tests and speculative production edits were not used for this audit.

Execution order after this reconciliation: address real functional contracts
(T088 bounded defeat and the independently confirmed raw NPC legacy refusal), then
remaining connected US3/ACTIVE dependencies in their existing plans; reconcile
implemented story matrices without replaying unchanged accepted sets. Keep explicit
R2/A5/DFR/Mortal/Windows admissions separate. No claim that these two short fixes
alone complete #1536. This audit creates no new game rule or author approval.

# T152 display text boundary — accepted bounded qualification, 2026-11-02

Source/runtime eb879e2deb6eadeda91eb61a463a28ebbb4d8aa3. Independent Sol/xhigh
SOURCE, PREP and OUTCOME accepted; root parsed the actual summary and all TRX rows.
Exact wounds-display-text-boundary / NoBuild / Parallelism 1: one method,
one descriptor, four unique PASS, no skip/timeout/duplicate/underfill, exit 0,
wrapper 9.222979 s. Both display.name and display.acquisitionNarration retain the
complete 2000-character text; 2001 characters produce one exact field/code refusal
and no envelope. The serializable caller fixture stays unchanged; this is not
borrowed-memory, canonical-storage, rendering or publication evidence.

Current behavior was already implemented. No production fix, new limit or causal
RED is claimed. Fresh Unit build: exit 0 / 31.850959 s / baseline 26 warnings,
exact warning multiset unchanged. Thirty used managed outputs, 206 resource inputs /
412 copies and four native copies match. Three PE/PDB owners and 348 current source
bindings verified; only Unit was freshly built. Main is compiled at 5950b74a and
Support/CrashHost at ece672d7, with unchanged actual source/dependency identities.
Catalog discovery: 1,338 categories / 13,163 methods-files; no tests executed.
Plan: one descriptor / one method / four rows / unchanged 60 s. Cleanup passed,
no new TMP roots or active owners. Pure tiny typed fixture/parsing work adds no
source IO; a cache/shared setup has no useful expensive reuse. GM no-update:
existing authored text contract and game behavior are unchanged.

Full raw: /workspace/tboe-linux-evidence-20261102/display-text-boundary/B1-green4.
Summary SHA256 8f4c1825aec9939cc26b2e544267bc5d06c55eb353dba4c4e7cb0d4a8e371b74;
TRX a8b45047ba7734e45d1a68cca85ea54bccb51003a04e7029a17a4eb4b4f6ea81.
Artifact proof 719b0bea12b716402ffc34ec2dfc8b26f9e362e00112cf7a007ef5aa8681ea0f;
PDB proof 4628df4b123896da3d343fded1ad1effda39014ad023e3cfbc877e0fd88ce046.
Only T152-DISPLAY-TEXT-BOUNDARY closes. T149's twenty passes remain on separate
5950b74a runtime; these are not one fresh twenty-four-case run. Parents T149/T152,
R2 browser/A5 terminal UNKNOWN and all DFR/Mortal/Windows HOLDs remain open.
Parent requested a full remaining-work audit before further implementation; no
new tasks or heavy/HOLD runs start during that audit. The independently found raw
NPC-response allowance is assessed, not implemented or runtime-qualified yet.

# T152 display text boundary — qualification WIP, 2026-11-02

Tracked T152-DISPLAY-TEXT-BOUNDARY / FR-076 / data-model 7.3. Bounded read-only
reconciliation found no new proven missing player-visible capability; unclosed
parent boxes do not justify reimplementing accepted treatment/provider/diagnosis
flows. A separate specific coverage gap remains: the actual display name and
acquisition narration parser use the existing 2000-character limit without exact
2000/2001 tests. Existing History and transition-summary bounds are different fields.

Four independent typed rows use WoundContractTestData and the actual parser,
prove a genuine baseline, preserve complete accepted text, require one exact
field/code refusal above the boundary and keep source JSON unchanged. No new
limit/production/rendering rule, reflection or Engine. This is qualification of
existing behavior; expected baseline PASS is not a causal RED claim. Source and
metadata are UNCOMPILED/UNRUN, independent SOURCE pending. Exact Unit leaf is
wounds-display-text-boundary, four rows / one method / 60 s. Fresh Unit only;
unchanged Main/Support/Bridge/CrashHost source and real dependency copies must be
verified before NoBuild. Pure small parsing/factory work has zero source IO;
cache/shared mutable state adds cost without meaningful stable expensive reuse.
GM no-update: no authored contract or game behavior changes. T149 accepted on
runtime 5950b74a, receipt a47f59cc; no repetition. Whole T152 and every HOLD stay open.
Raw will remain under /workspace/tboe-linux-evidence-20261102/display-text-boundary.

# T149 NPC canonical wound retirement — accepted bounded leaf, 2026-11-02

Runtime/source: 5950b74a95eeee1252247dbf20f6265df9f25a5d, Linux x64.
Independent Astra/high DESIGN, SOURCE, PREP and actual OUTCOME accepted. Root
inspected the full diff and all four TRX. Only T149-NPC-CANONICAL-WOUND-RETIREMENT
is closed: top-level NPCWoundChanges and exact entries[i].wounds cannot enter the
canonical NPC effect carrier. Current NPC wounds remain in npc_wounds.json.

N2 causal RED at 602e0f46: 16 complete, 12 expected FAIL / 4 preserving PASS;
three actual planner controls allocated one effect and one transition each.
N3 GREEN: 20 unique PASS, 4/4 descriptors and 20/20 cases complete, no skip,
timeout, duplicate or underfill; wrapper 30.176542 s, runner 28.946354 s, exit 0.
Exact selection: wounds-npc-canonical-retirement-causal (16),
wounds-npc-canonical-retirement-source-guards (2),
wounds-npc-canonical-retirement-publication (1), and
wounds-npc-canonical-retirement-early-refusal (1), Parallelism 1 / NoBuild.
Unit leaves keep 60 s; both Integration leaves keep their preselected 180 s.
Previously UNREACHED failed-plan/zero-allocation assertions now pass, together
with actual input immutability, error order, pristine/current schema and legal
metadata. Both real-IO consumers pass: successful publication and early refusal
with absent common authority plus exact carrier existence/byte preservation.
This proves early refusal, not a former publication bypass or an elapsed speedup.

Fresh Main / Unit / Integration builds exit 0 in 58.006287 / 32.952835 / 66.401339 s;
warning multisets match the former builds: 81 / 26 / 107. Verified used artifacts:
45 managed outputs, 206 resource inputs / 618 copies, 6 native copies, four code
owners' PE/PDB identities and 444 current source bindings. Support/Bridge/CrashHost
are unchanged dependencies, not fresh builds; old unused host copies are excluded.
Full catalog discovery: 1,337 categories / 13,162 methods-files, no tests executed.
Exact Plan: four descriptors / twelve methods / twenty cases. External verifier
initially omitted the existing CrashHost directory mapping; its failure and source
are retained, mapping corrected, complete proof passed without rebuilding/rerunning.

Optimization: the existing full-carrier planner key includes all relevant fields,
presence and null values; owner/lifetime/invalidation and fresh guards unchanged.
No new cache or IO. Source guards reuse their already-read operation-local images;
negative Integration byte proof deliberately performs fresh before/after reads.
GM no-update is independently accepted: direct schema-v1 cutover and separate
npc_wounds examples already specify this contract. No new authored/game rule.

Full raw: /workspace/tboe-linux-evidence-20261102/npc-canonical-retirement/N3-green20.
Summary SHA256: 0dc911653a15f4143c36b6f99285600d648394d544518444c1a04927cfccca67.
Artifact proof: 6882d26ff1a864dc49489ee5f07a1f9d3b29a4e83f1b6d84f26764d020ac1de6.
PDB proof: ddd433e7d05f1d3377e6bea83c9ab664fd2a30d7fecc45c701524a78f3d3d35c.
All four TRX hashes are in the external root-trx-integrity-proof.json. Owned/runtime
cleanup passed, no new TMP roots or active owners; no diagnostic data was deleted.
Immutable remote readback of all nine WIP files matched full bytes/git blobs:
proof 4d643ffd9f65d1f5bd9e9468be4e7d83b79edffd911d97f38167e182b002b4d5;
this is bounded readback, not a full clean restore. Final receipt commit is read
from Git history and its remote SHA is checked separately.

Parent T149, T152, A5 terminal / R2 browser UNKNOWN, DFR/Mortal/Windows HOLDs,
Saref #1571 and the overall #1536 remain open. No accepted unrelated set repeated.
Next independent residual qualification is the existing oversized display-text
admission boundary (T152 / FR-076); inspect actual prior ownership before adding
only missing cheap typed coverage, without a new rule or performance project.

# T149 NPC canonical wound retirement — accepted causal result, production WIP

Actual N2 at 602e0f46addb39b2147b088fe8ad1857031b2d24 completes 16/16 in 9.719289 s:
12 expected FAIL / 4 PASS, no timeout/dup/underfill, full cleanup/no new roots.
Eight catalog cases miss the root/entry error; three planner cases miss it after
genuine controls and actually allocate 1 effect / 1 transition each. Later zero
assertions were UNREACHED. Separate priority failure follows passed original
nonlegacy ordering; it is not an acceptance/bypass proof. Astra accepts OUTCOME.
TRX 8b392733d41893667d473e23739de1ca9607b56d3f28461307e5e5a2711031dd;
summary 79863ffc7e0c439d86afbd91c2ed1d7dda4da12224a3e9f9d988654e9eb1e1c9.

Production draft now closes non-metadata root fields to schemaVersion/entries,
refuses exact nested wounds presence before that entry ID/effect checks and drops
only the legacy TryLocate normalization. Duplicate-first/nonlegacy order, pristine
null creation, metadata and fresh authority/history checks stay. Two source
inventory allowances become strict integration seams with real anchors and
operation-local no-old-alias assertions; remaining legacy owners are not hidden.
Existing planner cache key already deep-clones every NPC carrier property, so
newly relevant presence/null values remain dependencies; owner/lifetime unchanged.
No new cache/IO, migration or game rule. GM no-update is reviewed: current direct
cutover guide and worked NPC examples already use the separate npc_wounds carrier.

Fresh Main/Unit/Integration builds, dependency/resource/native/PDB bindings and
coherent full discovery/Plan/PREP precede GREEN 20: Unit 16 + source guards 2 + two
independent Integration peers. Unit bounds 60 s; each real-IO Integration leaf 180 s,
chosen before runtime and below former broad 10/15-minute owners. Negative peer
retains original no-common/semantic checks and adds exact carrier existence/bytes.
Production/guard/integration changes are UNCOMPILED/UNRUN. No App/HOLD rerun.
Full raw remains outside checkout in npc-canonical-retirement under
/workspace/tboe-linux-evidence-20261102; source/build/review lineages remain explicit.

# T149 NPC carrier causal preparation — warning-only correction WIP

First fresh Unit build at5cc6dcd5 exits0 in36.066402s; baseline26 warnings plus
two new CS8620 annotations in the new ordered-error expected tuple arrays. Source
correction explicitly types both expected arrays as (string,string?), preserving
all values/order/null refusal semantics; no suppression or assertion weakening.
New source is not yet compiled. The first five Unit artifacts are archived with
full member/hash/gzip proof outside checkout; no runtime has executed.

Initial selected PDB verifier failed at ownership accounting: linked
BookOfEternityClient.Tests/WoundContractTestData.cs belongs to TestSupport, not
Unit. Existing T2 actual Compile inventory confirms that sole cross-directory
binding. Include unchanged Support in current PE/PDB proof and explicitly guard
its linked input before rebuild. No product source defect or fresh Integration
qualification is inferred. Integration source remains UNCOMPILED/UNQUALIFIED.
Root SOURCE delta, fresh Unit build, selected bindings/Plan/PREP remain pending.
Full preparation data is outside checkout in npc-canonical-retirement/N1-build-unit
beneath /workspace/tboe-linux-evidence-20261102. All other HOLDs unchanged.

# T149 NPC canonical wound retirement: coherent test-only scaffold WIP

2026-11-02 UTC, base ea9c44ee1e5f5694e4240d134bbc0b909503f6f6, #1536 /
T149-NPC-CANONICAL-WOUND-RETIREMENT. Astra DESIGN delta accepts retirement of
both known unsupported allowances: any top-level NPCWoundChanges is root legacy
error; exact entries[i].wounds presence is entry legacy error before its ID/effects.
Duplicate-first/current nonlegacy ordering, legal metadata, source/owner guards
remain; no blanket unknown/casefold/recursive ban. Production and semantic
sourceguard inventory are UNCHANGED until accepted actual causal RED.

Selected one exact60s Unit leaf:8 methods/16 source rows. Root4 + current2 +
order1 + planner root2/nested1 + nested values4 + legal metadata positives2.
Expected baseline12 FAIL/4 PASS:11 genuine missing-refusal failures plus one
classification/order failure on already-malformed input, not acceptance causality.
Order case first proves unchanged nonlegacy errors, then duplicate-first/nested
precedence. No actual result is claimed. Planner rows first prove genuine base
plans, fresh-cache misses, actual allocations and unchanged complete inputs;
required error precedes failed-plan/zero-allocation oracles, which can be UNREACHED.
No reflection/Engine/new prep API; original CreateInput/NpcRoot/Effect helpers stay.

Both old Unit positives are honestly renamed and preserve legal non-wound metadata.
Old owner memberships survive exact selector renames. Known Integration source
impact is prepared now but UNSELECTED/UNRUN until post-RED GREEN: normalizer's
published mixed-root positive now uses legal metadata; legacy payload refusal
expects earlier carrier error while no-common/original-byte assertions remain.
These two Integration source bindings are changed/UNCOMPILED; retained binaries
are historical, not current proof. Root owns coherent SOURCE/owning compilation /
current catalog/Plan/PREP before selected Unit RED, and later Integration execution.
Root archived75 managed outputs externally before future overwrite; no deletion.

Static count/owner/helper/inverse checks and git diff --check pass. Actual catalog /
selection metadata reader exits0:1,334 categories, exact Unit8-method leaf/60s;
this is not discovery. Primary metadata/static logs are external under
/workspace/tboe-linux-evidence-20261102/npc-canonical-retirement/N1-scaffold/.
C# UNCOMPILED; builds/discovery/Plan/PREP/runtime UNRUN, cost UNMEASURED. Fresh
trivial JSON enumeration needs no production cache/new reads; no speedup claim.
Existing late Integration refusal is preserved, so no publication bypass is claimed.
Parent T149, accepted T118/T150, A5/R2 UNKNOWN, T148/#1571 and all HOLDs unchanged.

# T118-ACTUAL-SHELL-READONLY accepted — bounded final receipt

Independent Astra DESIGN/SOURCE/PREP/OUTCOME accepts only this leaf. Production
App/ShellProvider now has a qualified read-only route from Continue through Help,
active wounds, known healed History, active-back and local scene-back, using
controlled typed HTTP responses. Russian Wounds→Раны mapping is the only production
change. No authored GM capability/wire/rules changed; GM no-update is intentional.

Separate accepted lineages, not one fresh aggregate:
- SSR label1 PASS at f0965ecb8e31abc16a765d0a9eb5b29205a20148; unchanged causal
  test15.806159ms / wrapper2.711535s, after actual F1 heading-only RED.
- Compile-only1 at210c45de0893feb79fa9a497244e1f545ac68dfe, adapter1.1175553s /
  wrapper2.916980s; not a runtime Fact. Exact source frontend inventory49 files,
  no missing/stale owner, catalog metadata1,333. Compiler inputs unchanged later.
- DOM desktop1440×1000 F8 PASS16.261767s and mobile390×844 F9 PASS11.562581s,
  both runtime4a79f72335e4f77f6b6ddcd6d0ee3377c08e16f6 and unchanged60s bounds.
  Each process0/driver.exit0/report.pass,30 completed GET and3 exact read-only POST,
 5,683 unchanged source/dependency/public/config pins,6 reviewed PNG, no page errors,
  violations or horizontal overflow. Keyboard/focus, literal markup, hidden rawJson
  refusal and local final Back are proved on reached screens. Narrow cards use
  normal vertical scrolling; a full card in one mobile viewport is not claimed.

Both cases have complete CRC-valid traces (desktop163/mobile369 members), closed
browser/context, terminated owned Vite group and no active owner. All12 original
PNGs were independently reviewed and show readable content/no blank dossier.
F4 early path FAIL/F5 locator FAIL remain; F6/F7 are only observed automated
protocol PASS with rendered qualification withheld. Their evidence is preserved.

Full local diagnostics, never GitHub:
/workspace/tboe-linux-evidence-20261102/actual-shell-readonly.
F8 report4fd56010a69ae870fc148c1383bbb176d74ae742df4cf982d530d8cb74ff90c4;
trace1e6746fd881569342a03d19d8be73e7e5b480485139101b76742a596e83a3d23.
F9 reportd625bc351cf0fcd0c028235f3f1f45a0239002a4ac0fc2127501eb3da07b5de0;
trace245e5e5b9bc92fd73ec7390468b7bd6b0a03f52db93ad371cc955da8f9107d7c.
F3 summary0433de17b337d47e3fe166c7f880c960c3c72482802d15122ab6d03894271df9.
Fresh F2 production build/typecheck exits0/4.909546s,113 hashed outputs; production
inputs remain identical. Test config adds only JSON resolution; test types are
separately established by F3. No unnecessary managed/SSR28/old DOM13 rerun.

Optimization uses existing immutable public templates with independent case DTOs
and existing Vite dependency reuse; fresh mutable DOM/finite-animation checks
cannot be cached. No service/cache/diagnostic framework was added, no speedup is
claimed, and no budget/animation setting was changed. Raw, saves and recovery were
not deleted. Backend authority, packaged/native fallback hosting, live GM/provider,
canonical nonmutation and save/fresh-process qualification are outside this proof.
Parent T118/T151/T158/#1536, A5/R2 UNKNOWN, DFR/Mortal compatibility/Windows and all
other HOLDs remain open. Next bounded actual-code retirement is T149 legacy NPC
effect-carrier allowance; source acceptance is observed, publication bypass is not.

# T118 actual-App — accepted finite-animation design, SOURCE WIP

Independent Astra accepts F7 protocol PASS with rendered qualification withheld,
and the bounded whole-content design: after the existing outer guard, poll a
fresh subtree getAnimations list until every finite animation is finished and
not pending; exclude only infinite decorative loops. Preserve5000ms assertion /
60s runtime bounds; no forced finish, sleep, settings or production change.
Both active and active-back now require the actual literal-markup content and
no rendered b element. A new explicit Требуется лечение fact assertion applies
to both; it was not present in the original first-active assertions. This is a
current-path readiness guard, not a universal detector of JS animations. Source
is authored; frozen SOURCE/new desktop PREP and all-six-PNG review remain pending.
F3 types/F2 build and all wider HOLDs remain; mobile stays UNRUN. No runtime started.

# T118 actual-App — whole animation readiness reassessment, no new runtime

F7 automated protocol PASS on a5b0a628749fcd7115922c1318fdea2efe79f422 takes
14.328742s, process/driver0,30GET/3POST,154-member valid trace and full cleanup.
Root inspected all six PNGs: active-back has readable page header but invisible
wound dossier, whose independent finite CSS entrance is not covered by the parent
motion check. Rendered qualification is withheld. This second visual-readiness
finding stops per-widget trial fixes; a bounded whole-content finite-animation
DESIGN review is pending, not another admitted attempt. No permanent product
failure is inferred. F7 raw/PNGs/trace stay in actual-shell-readonly/F7-desktop-*
under /workspace/tboe-linux-evidence-20261102. Mobile remains UNRUN; F3 types/F2
build retain their lineages. Production, budgets and all UNKNOWN/HOLD unchanged.

# T118 actual-App — automated protocol PASS, visual acceptance withheld

F6 atc1d57551648274231324661fb8b6a5cdd4ca2efe reports automated PASS in11.662682s:
process0/driver.exit0/report true, all30 GET/3 exact read-only POST requests
complete,6 screenshots,136-member CRC-valid trace,5,683 unchanged input pins and
full owned cleanup. Root visual inspection finds active.png blank; its retained
DOM snapshot has the content motion owner at opacity0/blur8px. Later History is
readable. This establishes insufficient render readiness in the test, not a
permanent product blank-screen defect. Whole rendered acceptance is WITHHELD;
no fresh desktop/mobile qualification is inferred from the protocol PASS.

Minimal test-only correction waits for the actual single content motion owner
to reach opacity1/blur0 before each existing text/layout/screenshot check. Existing
60s runtime/default assertion bounds, animation settings and production stay
unchanged; no arbitrary sleep, new stack/cache or reduced-motion substitution.
All six existing screen boundaries share this owner; the complete remaining path
was reviewed instead of adding one special-case wait. Mutable DOM readiness is
fresh per screen; caching it would be incorrect and adds no useful benefit.
SOURCE/new desktop PREP pending; F3 compile/F2 build retained without reruns.
Full F6 raw stays in actual-shell-readonly/F6-desktop-case plus envelope beneath
/workspace/tboe-linux-evidence-20261102; report SHA
bde0d4f0146633ac74e9f831b3ce4100c4ddf814c454c750e65c76b3287f7516;
trace90951940b8b2a599659d20597af927fc1309f65b1ff0bcc15cf16967b346db48.
Prior failures, mobile UNRUN and all wider UNKNOWN/HOLD remain unchanged.

# T118 actual-App desktop — retained locator failure, correction WIP

F5 on402596fc6007801a56fecf0f99e572e4320e97d0 reaches the real App History
response, then completes FAIL in14.630026s without timeout. Its heading focus
locator matches both the valid page H1 and panel H4. Focus failure of the product
is not established; the selector itself is ambiguous. Completed request records,
three screenshots and the complete143-member CRC-valid trace remain outside
checkout in actual-shell-readonly/F5-desktop-case and its envelope. Source and
all pinned dependencies are unchanged; no violations/page errors, all owners
closed, no active process remains. The whole desktop case is not qualified.

Correction scopes all three intended actual-mode page-heading focus checks to
level1; earlier controlled-host mode and every behavioral assertion remain.
Remaining History text/action, active-back and final scene selectors were checked
against their actual components; no further source mismatch was found. SOURCE
and fresh desktop PREP remain pending, mobile UNRUN. Types/production/configs
are unchanged: retain accepted F3 compile-only1 and F2 build on explicit lineages,
no unrelated PASS rerun. Prior F4 early pin error and all UNKNOWN/HOLD are retained.

# T118 actual-App preparation correction WIP — first DOM attempt did not start

F3 compile-contract1 is accepted on source210c45de0893feb79fa9a497244e1f545ac68dfe:
complete1/1, adapter1.1175553s / wrapper2.916980s, exit0, no timeout/source drift,
cleanup true. This is compile-only, not a runtime Fact. Summary SHA256:
0433de17b337d47e3fe166c7f880c960c3c72482802d15122ab6d03894271df9.
F4 desktop attempt fails in0.435556s during source-pin preparation: relative_to
frontend rejects the repository-level tests/categories.json. Vite/Chromium and
all UI steps are UNRUN; no requests/trace/driver-exit receipt was produced. No
policy denial or timeout occurred; no active owners remain. Full original output
is retained outside checkout at actual-shell-readonly/F4-desktop-envelope and
F4-desktop-case beneath /workspace/tboe-linux-evidence-20261102.

Correction changes only that exclusion-path calculation to repository-relative.
Read-only enumeration now covers5,530 actual inputs including catalog/selection,
retaining the same .vite/.vite-temp exclusions; no browser was launched by this
proof. Independent Astra accepts the causal failure and one-line SOURCE fix;
new PREP and an explicit desktop attempt remain pending. Types,
factories, configs, category membership and production are unchanged, so accepted
F3 compile and F2 production-build evidence remain on their stated lineages;
no unchanged successful checks are repeated. Mobile is UNRUN; all broader HOLDs
and label-only acceptance remain unchanged.

# T118 actual-App scaffold WIP, compile and DOM unrun

2026-11-02 UTC, base 76ed2b7d91367409c8cb1740ad18d3b1708bc088, #1536 /
T118-ACTUAL-SHELL-READONLY. Accepted label F1 RED/F2 GREEN remains at f0965ecb;
no SSR1/28, old controlled DOM13 or production build repeat. New pure typed packet
factory reuses existing public contract templates/woundCase/Wounds helper with
fresh DTO copies, quiet scene image prompts/gallery and hidden rawJson sentinel.
One new exact compile-only contract/60s is selected, not a runtime Fact. Existing
test config gains only resolveJsonModule for its newly imported JSON chain; no
actual compile failure is asserted. All production and old SSR/host source stays.

Existing driver gains --actual-shell desktop|mobile, ordinary index/main/StrictMode /
App/ShellProvider only, not the fixture host. Each separate60s attempt owns strict
loopback Vite5177, Chromium/context, route state and external complete HTTP ledger /
trace/screenshots. Routing precedes navigation: six exact GETs, three exact read-only
POST bodies/order with completed requestfinished refresh barriers, initial StrictMode
bootstrap resampled after menu networkidle; no /assets proxy, unknown API or external
traffic. Continue/final Back local; actual Russian menu, active/History focus and
real Tab-to-History transition, literal text/privacy/overflow. Vite group and browser
closing are explicit; no sandbox/auth changes. Preflight requires687,865,856 reserve
plus2,908,000,000 headroom bytes; final reserve retained, diagnostics uncapped.

Static Python AST/JSON/source checks pass, original driver main/record ASTs and old
owners unchanged. git diff --check passes. Primary static log is external:
/workspace/tboe-linux-evidence-20261102/actual-shell-readonly/F3-scaffold/static.log.
No npm/install/build/typecheck/driver/browser execution by writer. Code remains
UNRUN/UNTYPECHECKED, cost UNMEASURED; root persistence/SOURCE/compile-contract and
separate PREP precede either DOM attempt. No cache is useful for fresh controlled
DTOs; existing installed assets are reused. No GM-authored wire changes, so no GM
update. This proves no backend/canonical nonmutation/provider/GM/save/fresh-process
or packaged hosting. Parent T118/T158, A5/R2 UNKNOWN and all broader HOLDs remain.

# T118 actual-shell read-only — Russian Help label accepted, App pending

2026-11-02 UTC, #1536 / T118-ACTUAL-SHELL-READONLY / FR-075. Independent Astra
DESIGN/SOURCE/PREP/OUTCOME accepts the bounded HelpView label correction on
f0965ecb8e31abc16a765d0a9eb5b29205a20148: actual F2 singleton1/1 PASS,
15.806159ms test / 2.711535s wrapper, complete1/1 descriptor, exit0, no timeout,
duplicate or underfill, source unchanged and owned/runtime cleanup true. Both
final Russian heading/no-Wounds oracles pass after the original premises.
Earlier F1 at1e13e67d9bd11a8c1049f3fb9a7bb04d2734de64 is one causal FAIL at
line42, Wounds versus Раны; final absence oracle was UNREACHED. Failed Vitest
produces no successful adapter TRX, so its summary underfill is retained honestly.

One production entry GROUP_LABELS.Wounds = Раны fixes the default menu; icons,
routing, permission and gameplay remain unchanged. No cache benefits this trivial
constant lookup; existing grouping and independent fresh DTO preparation remain.
No GM-authored contract changes, so GM documentation requires no update. Ordinary
fresh npm build includes production typecheck and exits0 in4.909546s with113
hashed outputs and retained existing large-chunk warning; this is not test-type,
packaged host or actual-App DOM proof. Locked npm ci preserves package/lock bytes.

Full raw remains outside checkout:
/workspace/tboe-linux-evidence-20261102/actual-shell-readonly.
F2-green1 summary SHAd464c22ddd471745dfc6fe8b0776111d4d184e09fcdbbae43818a051227799c6;
Vitest96fb7c5c0702e8b03ce54981be6980d3288c27dadcdbb403390735cf81531271;
TRXb57cde15ddfdd8cfd529510ca937f1d5d5a5c7c7309f6c39148e2387a8273825.
F1 catalog discovery valid1,332/13,155; exact F2 Plan SHA
93da6ab0f9679f895665535666314059410ce9655cc26040e23119adbbd4bfee.
Next is the separately reviewed test-only actual App/ShellProvider read-only
scaffold, fresh typed packet contract and SOURCE/PREP before desktop/mobile DOM.
No repeat of SSR1/28 or controlled DOM13. T118-ACTUAL-SHELL-READONLY, its parents,
A5/R2 UNKNOWN, T152 terminal and all broader HOLDs remain open. Vite index is not
packaged hosting/backend/canonical nonmutation/provider/save freshness proof.

# T150-GM-TERMINAL-GUIDANCE accepted — final bounded receipt

Source/build/runtime ece672d746af3ed19a51c148aad28e3b0cad830f; independent Astra
SOURCE/PREP/final OUTCOME accepted. Actual T2-green4:4/4 unique PASS,3/3 descriptors,
18.8036975s wall,exit0,no timeout/underfill/duplicates. Physical1 takes0.1667134s,
manifest1 Integration0.1663605s, Prompt2 Unit0.1496357/0.0012817s. Owned process /
runtime cleanup true, no active owner/new TMP or joint fixture. Summary SHA256:
b435ccddc480a347eb6b410ad4f64d87476631f864944080c8c446a74e1e42f8;
Physical TRXe38f43ab7b353866a8239566fe9db688553277ea5c0d5f66530491ac9687ccdb;
manifest TRX7b14f6efba925eeae2195f8591bdacf31d4a5aeb9d47064dbc7701920397bd47;
Prompt TRXd737d7a37844b6fc28749bf40246d7f9d140622518cfe146bd76ebcc713d903f.
Primary output is outside checkout in
/workspace/tboe-linux-evidence-20261102/gm-terminal-guidance/T2-green4.

All six fresh owning builds exit0 at this source; warnings81/23/14/0/26/107
retain file/code/message multiset after allowing line/column shifts. Six PE/PDB /
437 source bindings,75 managed artifacts,206 resource inputs/1,236 copies and12
native copies verify. Exactly3 declared GM inputs changed; all18 copies are fresh.
Artifact/resource proof SHA256:
ddcc051e8c7d5fa2a7ef8fc6c2623964c8c8ae9fc7341bf8fb1a68a55f907986;
PDBf3f50df4a60c0267376b28e8aa0d8d95fa6420d6eb09c0f783a748a48b0b4c2a.
Catalog1,331/13,154 valid/no stale or unmapped; exact Plan3 descriptors/4Facts /
60/60/60s. Nine changed source/config/docs files fetched at immutable remote match
commit and checkout, bounded readback proof SHA256:
23ed972e8dafd4cc93dfb4d60848f34a09ae86197a93026fc1c87f4b4e58a25d.
This is not a new full-tree restore. Previous75 binary cohort is retained in
T1-managed-before-guidance-correction.tar.gz with complete member/EOF/CRC proof;
no old raw, test DATA, recovery or user saves deleted.

Accepted correction covers the two stale claims in guide/example, two manifest
RequiredText entries plus its coverageLimit, and both Prompt guards. All authored
fenced blocks unchanged; sealed terminal heal/independent legacy and typed death
handoff/later direct-death consumer are correctly separated. No gameplay/schema /
production change; family-local pure coverage is not runtime proof. Existing
loaded text/validators are reused without new reads/cache/Engine. Only this T150
leaf is closed; parent T150/#1536 stays open. A5 terminal/R2 browser remain UNKNOWN,
no new run admitted; all retained output10/anchor2/control3 lineages and other
DFR/Mortal compatibility/Windows/HOLD boundaries remain. Current checkpoint delta
is documentation only, so unchanged compiled C# must not be rebuilt merely for it.

# T150 coherent guidance correction WIP: accepted T1 RED, GREEN unrun

2026-11-02 UTC, base 7d44af679c6eacab1d41774ce3884a4b7192b560, #1536 /
T150-GM-TERMINAL-GUIDANCE / FR-086/087. Independent Astra accepts actual T1:
one Fact FAIL, wall 7.6884812s / test 0.1966239s, first new DoesNotContain at
PhysicalGuidedDocumentationTests line 77 / position 40555 after all genuine typed
terminal/current manifest/mode/roundtrip premises. Second refusal UNREACHED.
Summary SHA256 89e010839c3521be5301a888c95d870ed6e8a302846ef48d839843158adce9d6;
TRX SHA256 af124bdd2377599ec1b25688778e86db3fb0c032da9693428acc2c9ce95a84d9.

Coherent correction now covers both guide/example claims, both manifest RequiredText
entries and the stale family-local coverage limit, plus both affected Prompt guards.
Existing selected sealed terminal heal/independent legacy are distinct from T069's
implemented typed death_contour handoff without direct life/death mutation and
the later direct death-outcome consumer outside this executor. Pure family controls
do not execute or qualify runtime terminal heal/legacy; existing worked example only.
All JSON worked routes/mechanics/schema/return slots and previous negative checks
remain; original Physical causal assertions are unchanged. No production change.

Selection is exact GREEN4: Physical1 + Prompt2 Unit + existing manifest1 Integration,
three 60s leaves, old owners unchanged/unselected. Local document DATA and existing
typed setup are reused; no extra expensive fixture, cache or diagnostic mechanism.
C# UNCOMPILED; discovery/Plan/PREP/GREEN UNRUN, cost UNMEASURED. Actual catalog and
selection metadata reader exit 0: 1,331 categories, three exact leaves/four source
Facts/60s each/related0. Static inverse proof preserves all worked JSON, original
Physical Fact, every prior Prompt negative check and old category owners; manifest
changes exactly two RequiredText fields and one coverageLimit. git diff --check
passes. External primary metadata/static logs are under
/workspace/tboe-linux-evidence-20261102/gm-terminal-guidance/T2-correction/.
Root owns persistence/SOURCE/six fresh owning builds and actual copy proof for
the three changed runtime guidance resources. Old binaries are archived
externally before rebuilding; no deletion. Parent T150 remains open. Accepted A5
TIMEOUT/UNKNOWN admits no further full-terminal retry; output10/anchor2/controls3,
T121/T152/R2 and all broader HOLDs retain their exact prior lineages and boundaries.

# Historical T150 causal scaffold, before accepted T1 RED

2026-11-02 UTC, base c9c9103b6c06cbe806cbab8eb4d7faeb5a31aa57, #1536 /
T150-GM-TERMINAL-GUIDANCE / parent T150 / FR-086/087. Only the existing pure
PhysicalGuidedDocumentationTests worked-selection Fact gains two exact stale
terminal heal/legacy claim refusals after all existing typed route, current
manifest, three mode, roundtrip and selection nonmutation premises. One Fact is
expected RED at its first new refusal; the second can be UNREACHED, not another
observed failure. No production, Examples or example manifest correction yet.
After accepted real RED, guidance must distinguish existing sealed heal and
independent legacy publication from the later direct death-outcome consumer.
T069's typed death_contour handoff remains implemented; no new gameplay rule.

New exact mortal-terminal-guidance-causal leaf selects only this Unit Fact/one
source case/60s protective bound. All old owners remain unchanged and unselected.
Both assertions reuse the already loaded example; no extra read, expensive setup,
Engine, cache or diagnostic framework. Cost UNMEASURED. C# UNCOMPILED; discovery,
Plan/PREP/runtime UNRUN. Actual Read-TestCategoryCatalog/Get-TestCategoryCiMatrix
exit 0: 1,329 categories, one exact Unit Fact/60s/related0. External primary log:
/workspace/tboe-linux-evidence-20261102/gm-terminal-guidance/T1-scaffold/metadata.log.
Static inverse proof preserves all old test bytes/assertions and category objects;
Examples/manifest unchanged, both target claims present once. git diff --check
passes. Root owns SOURCE, persistence, fresh Unit build and causal qualification.

Independent Astra A5 OUTCOME is accepted as TIMEOUT/UNKNOWN, not feature closure.
Actual runtime c9c9103b6c06cbe806cbab8eb4d7faeb5a31aa57, compiled Integration
9383e6e7b2eb999144b88f4d48f055bca97bdc72; wall 124.9174643s, terminal timeout
120.0346224/120s, zero TRX. Incomplete gzip EOF-false prefix is 347,869,833 stored /
3,357,791,117 decoded bytes / 6,582,614 rows. New 135-entry root matches its manifest;
old A2 frozen135, fault and R2 evidence are preserved, no active owners.
Resource end 66.2547s, Bind begin 66.3822s, CalculateArtifacts end 105.6404s,
last saved record 116.5975s; no observed Bind/Prepare end. Buffering prevents a
phase-at-kill or whole-timeout cause claim; no speedup or further retry admitted.
Primary raw remains external under notification-controls/A5-terminal1.
Summary SHA256 f71238d12d7294eb51b558ab85ac0834853f5589a4d0ad6cc6887f7abd07ca7e;
stored prefix SHA256 327f0124f01745ef7b7b883b7797307258335c54563d8681bcbe3dc677b5b81c.
Accepted output10 are retained across N2 diagnosis2/remaining7/A4 worsening1;
anchor2 at 92c561f0 and continuing controls3 at 9383e6e7 remain separate lineages.
Parent T150, T121/T152, R2 UNKNOWN and every broader HOLD stay open.

# Earlier A4 accepted gate, before the A5 TIMEOUT/UNKNOWN

Compiled Integration/runtime9383e6e7b2eb999144b88f4d48f055bca97bdc72,
fresh build74.805064s/exit0/107 warnings; exact previous warning multiset retained.
Other five owners remain92c561f0eccfd3f8bd086850057319903168274c, C# unchanged.
Six PE/PDB/437 current bindings and75 artifacts/1,236 resource copies/12 native
copies verified. Artifact SHA256:
bdd826e9eb1c864b6a541764fca0b2261ca36544c2940c2f39033cbe05c957dd;
PDB6530c3578f0e3b18faaf20450df94e67b907c6a97b76167d6155d9dd360146cc.
Current catalog is unchanged from validA2 (1,328/13,154); no owner/attribute/method
change. Exact A4Plan2descriptors/4Facts/60+60s passes, runtime0. Six938 source/docs
files were independently downloaded at the immutable remote and match source;
bounded readback proof2f2f58228cc6e5518a3434931aafcb0f4aef8ce509503f9ce68125c2e7915aa4,
not a new full-tree restore.

Independent Astra SOURCE/PREP/OUTCOME accepts A4-cheap4:4/4 PASS in21.6046858s,
exit0/no timeout/underfill/duplicates. Controls3 take0.0969298/0.3248915/0.1221830s;
worsening1 takes8.2203739s and reaches all original identity/effect replacement /
History/output assertions. Summary SHA256:
dc9b1f005af77ed67c05865b143caf845d970a5dd486c3e375860eaf65535ab1;
controls TRXdfb1705e4036fe61d2eafad718bdd1821805354adb4dc95fb55fc223bf7becef;
worsening TRX83943010db30fffc20e6501459528540ec5a35ba03f67e02b93277d97ccde928.
Four gzip streams have full EOF/CRC/strict UTF-8/stored+decoded bytes/hash/rows
proof; three are complete with roots removed. Fault Fact intentionally has lost
batch/incomplete accounting, closed gzip and retainedrootbb2d933ea9644c179ec2d7e929ecc530.
That is an expected refusal-policy proof, not complete successful diagnostics.
The only other component root is old A2 terminal89067951..., all135 frozen entries
unchanged. No live owners/new private TMP roots. Primary raw remains external in
notification-controls/A4-cheap4; verification SHA256:
2d05b3051c2da5eb3d4f5f8d9252f13ba7e4581f4ff07e2a7d8a270bd595290d.

Original notification selection now has10 distinct output PASS on retained explicit
versions; no fresh10 or full T152 claim. Anchor2 pass remains separate. A2 full
terminal TIMEOUT/UNKNOWN and its incomplete capture/root are preserved. A5 exact
singleton Plan on9383e6e7 is1case/120s, runtimeUNRUN; no retry is admitted yet.
This checkpoint changes only canonical plan/tasks/findings. Preflight must prove
that doc-only delta versus9383e6e7, retain current compiled/artifact/source pins
and avoid unnecessary rebuilding. After separate PREP, at most one unchanged120s
full terminal attempt with the existing continuing recorder; no cache/workload /
assertion/budget change or speedup claim. T121/T152 and all broader HOLD stay open.

# A2 partial GREEN and A3 fixture failure — next test-only WIP

All six owner builds are fresh at92c561f0eccfd3f8bd086850057319903168274c,
exit0; warnings81/23/14/0/26/107 exactly match earlier same-environment multisets.
Six PE/PDB pairs/437 current bindings and75 artifacts/1,236 resource copies/12
native copies verify. A2 artifact proof SHA256:
eff24312612a505a61cbd4be4429b95c2befca51dd12a0457ab305de309f3cd9;
PDB proof289e11853476e77e26ec13575ee9a85b007375c5e14e5526699c07e7d73a6ef3.
Catalog remains valid1,328/13,154; exact A2Plan three descriptors/four cases at
120/120/60s. Accepted SOURCE2125755f1 differs from compiled92 only by the reviewed
one-sentence catalog correction; six-file remote readback and changed-catalog
remote readback match, neither is a new full-tree restore.

Actual A2-green4: anchor2 PASS, full terminal TIMEOUT120.1100771s/UNKNOWN/no TRX,
worsening UNRUN; exit124/wall188.0783897s, completed1/3descriptors2/4cases.
Anchor durations47.3963305/12.4361659s, including all exact allocation oracles and
seven honest constructor refusals. Full anchor captures independently verify
EOF/CRC/strict UTF-8/hashes/counts/closing and actual roots removed. Terminal's
134,582,144B incomplete gzip prefix (no report/footer) is retained, SHA256
f5dc46eb16d006081caa2a6884760ee5679264d73d26229dc9d76e1d465edf28;
actual135-entry owned test root remains, inventory preserved. No live owners or
new private-TMP roots, but no fixture-removal/complete-capture claim for terminal.
Astra accepts this partial OUTCOME only. Summary SHA256:
f13aceea9b6f8bbae1d52805bb91b30f1d0cb20423468325ccf77e3cf6e6f002;
anchor TRX83b225fb0b3f4d9875e55025dc3a5d18194e573303990687d996e3047bebcf97;
partial-capture-verification SHA256:
cea394dc1c26c8cf7bee065665c5b8b35b09d67c7c66dcc1837505de9cdb7d84.

A3-worsening1 was separately admitted on unchanged92 and completes1/1 FAIL,
10.3328887s wall/1.5133067s test, no timeout, owned cleanup/no new TMP roots.
Composition succeeds; first PublishAsync fails pending_turn_snapshot_reader_coverage_missing
for soul_state.json. Summary2dad7bad3a8caf4a871de798359842e5f9ab11d32792060cfe92de131039eed1;
TRXfd030c400d1cfa4dbbff3119f7f5f047c90454c84cc1a053d2c5e15f304f47d0.
Primary captures/manifests/raw stay external under notification-controls/A2-green4
and A3-worsening1. No evidence, saves or recovery data are deleted.

The local setup correction now includes the Mortal realm source before the
existing real signed occurrence/snapshot helpers for both turns, exact public
opportunity binding and no redundant earlier turn43 capture. Existing route
clones/assertions/global helpers are retained. Full terminal changes only its
constructor opt-in to existing continuingPassiveTimings; default fresh DATA,
all events/fields/clocks/gzip/accounting/closing/guards and120s remain. These C#
changes are UNCOMPILED/UNRUN, not accepted results. Three existing file-only
controls plus worsening1 are the cheap gate; their intentional fault retains
its root/incomplete report by contract. No terminal retry before OUTCOME/PREP.
No speedup or complete timeout cause is established; last saved timestamp60.263s
cannot identify the executing phase at kill. GM no-update: no authored/gameplay
contract changes. T121/T152, old output9/R2 UNKNOWN and every broader HOLD remain.

# Joint terminal causal2 accepted; production correction WIP

Actual runtime b18e77224237e228c02d8eb16f1cc5893a95adb4, source build same SHA
for fresh Integration only (73.1872027s,107 warnings/0 errors). Its warning multiset
matches the previous same-environment Integration build; no changed-file warning.
Other five owners remain unchanged at894c9fb719e393a914ccbb601fbcbdc8d433325f.
Six PE/PDB pairs and436 current source bindings,75 managed artifacts,1,236 resource
copies/12 native copies verify. Catalog1,328/13,154 valid, no unmapped/stale;
PlanOnly one theory/two cases/120s. Seven changed files were freshly read back at
immutable remote b18e77224 and match commit/checkout; this is a bounded readback,
not a new full-tree restore. Proof SHA256:
2f94b1a6656838f6738426d8fc0a2b8da56e541fe822c3ce7d5d426c671f87b8.

Independent Astra SOURCE/PREP/OUTCOME accepts actual causal2: nonterminal PASS
13.0197822s, terminal final anchors.Success FAIL46.6857568s, wall68.1536476s,
exit1, both complete/no timeout/duplicate/underfill. Terminal failure is the exact
changed-union code at line137 after all authority/clock/union/Before/heal-child
History and source/bundle nonmutation premises. All7 independently resealed
copies refuse at bundle constructor/finalPlan; they do not reach anchor API.
Nonterminal allocation oracles PASS; terminal allocation oracles remain UNREACHED.
Summary SHA2567ec4d43d2b51a147991ad266a451ae7abe6680153a9c4748bf3aa7bd8eec6476;
TRX SHA256b50847675ce5852ea87d5c379b0cea4fe22373015de60f4c4bb288b02593730d.

Both complete captures have full EOF/CRC, strict UTF-8, stored/decoded hashes,
all attempted rows and closed timing/diagnostic receipts independently verified.
Nonterminal:47,741,612 stored/456,046,165 decoded bytes/902,817 rows.
Terminal:48,191,662 stored/461,943,975 decoded bytes/915,425 rows.
Both actual registered fixture roots are absent; no new private TMP root or live
owner remains. Primary raw is external under notification-controls/A1-red2;
causal-capture-verification SHA256:
5b766ce118d452a2455036efd04d2832e80d4a21fab5977db5c53c0df5b26183.
Latest75 binaries are losslessly archived before production rebuilding:
A1-managed-before-terminal-correction.tar.gz,102,619,887B, SHA256
ec064bddc72160fb2757c03b0717f3feb296ca289f827ef63b351c3106555020;
all member lengths/hashes and full gzip EOF/CRC verified, no deletion.

Admitted minimal production correction is now authored but UNCOMPILED/UNRUN.
Exact old canonical Before and same sealed terminal view distinguish rightful
delete/AfterNull from preserved nonterminal update. Full final replay/current
owner/source/clock and single new-CREATE allocation remain. Existing reducer/DATA
is reused; no new cache/read/diagnostic mechanism or performance claim. GM contract
is unchanged. Next SOURCE and fresh affected builds, exact four-case Plan/PREP,
then anchor2/full terminal1/worsening1 at original120/120/60s. Nine output PASS
retain their earlier mixed-version lineage. T121/T152, R2 UNKNOWN and all broader
HOLDs remain open; neither causal RED nor compilation is feature acceptance.

# Joint terminal anchor causal scaffold — source-only WIP, not production fix

2026-11-02 UTC, basef04bffaeb736d295bdec60c157dc4fdab3668af7. Parent delivered
Sol/Astra DESIGN acceptance for the bounded scaffold. New partial uses unchanged
genuine PrepareJointStagesAsync before anchors: two independent terminal/nonterminal
cases with existing issuer/lease/stage ownership. Replay/current owner/clock /
exact union/old-before/terminal execution and History precede seven independent
resealed copy checks, honest first-refusing-boundary labels and original source /
bundle nonmutation. Final actual anchor acceptance is the intended terminal RED;
exact one CREATE/no old allocation assertions are UNREACHED until success.

One new exact Integration theory/2 source rows/one120s descriptor is selected;
cost UNMEASURED and fit belongs to PREP. No production, preparation API, reducer,
clock/source guard or new diagnostics/cache changes. Full terminal output peer
remains mandatory separately after correction. Existing worsening test changes
only its two local treatment payloads to current complete template; every prior
assertion/global helper is unchanged. Its original60s exact owner remains
unselected pending its separate addressed retry. C# UNCOMPILED; writer performs
no build/discovery/PlanOnly/runtime or commit/push; root persists/reviews first.

Astra accepted current partial output evidence: nine distinct PASS across N2
8b5dd57f9ef483a76b778788ebeb148aad6d8845 (diagnosis2) and remaining8 runtime
f04bffaeb736d295bdec60c157dc4fdab3668af7 (7PASS/1worsening fixtureFAIL,
all8 executed32.0404094s, exit1, no timeout/underfill, cleanup true). Distinct
terminal-anchor failure at N2 and worsening fixture failure remain open; T152
is not accepted. Remaining8 summary SHA256
217b31f89805b51368c222353d40472b852376505cd9b1e4ac9bde11ad6b5cd3;
causal5 TRX98f7b921e6dece8e7608ea2f40be4bd2afe56be5064d4f07eb7bdc26d6dafdf6;
worsening TRXd10d1017cbc777a6acca9356d484cce0a474bb97f943a4310e925d12290593de.
Root verified primary identities; all raw stays external in notification-controls /
N2-green11 and N2-remaining8. Passing9 are not selected again; historical8UNRUN
wording below is superseded by these exact outcomes. R2 UNKNOWN and HOLDs remain.

Minimal future contract/fixture audit/optimization/GM no-update rationale are at
plan top. Next coherent SOURCE, root's Integration-only build/exact plan/PREP and
causal2, then separately admitted minimal production correction and final peers.
Writer checks: git diff --check exit0; actual Read-TestCategoryCatalog and current
selection-schema reader exit0 (1,328 categories, exact new theory1/2 source rows /
120s/related[]). Static comparison proves old category entries, all worsening
assertions/global helper and unchanged stage preparation API; production diff is
empty. These metadata/source checks are not compilation, discovery or runtime.

# T152 N2 partial outcome — diagnosis2 PASS, terminal anchor FAIL, remaining8 UNRUN

Actual runtime 8b5dd57f9ef483a76b778788ebeb148aad6d8845 executes two descriptors /
three cases before fail-fast: diagnosis2 PASS, terminal-joint1 FAIL, eight cases
UNRUN. Exit 1, wall 66.5957310s, no timeout; process cleanup succeeds. The terminal
failure precedes every changed Project call: genuine fixture preparation reaches
MortalWoundCanonicalAnchorPlan.CreateJointRepair, then reports
accepted_mechanics_joint_anchor_contribution_invalid / changed union. Current
production requires an old-wound update, whereas the sealed terminal joint final
uses delete after complete treatment/heal. This is an unresolved terminal anchor
closure, not a C0 projection failure or established missing fixture field.

Full terminal diagnostics are complete and verified independently: 43,349,326
stored gzip bytes, 414,599,748 decoded bytes / 821,081 rows, full EOF/CRC and
stored/decoded SHA256 match the report. DiagnosticComplete, TimingComplete and
closing flags are true, no diagnostic/closing failures. Actual registered owned
fixture root is removed. Private-temp before/after sets retain only the old R2
root; no new fixture remains. Primary N2-green11 summary SHA256:
dde904a4d98be3e5d84b20f4dd374b437085553491e33dc3fea7679e045765d5;
terminal TRX SHA256:
d27a7c502ba2d80d70e94b413eb15574bb1a9cfd3cac475f5b077fe846146262.
All capture/proof files remain external under notification-controls/N2-green11.

Next: independent partial OUTCOME/PREP for the exact eight unrun cases on the
unchanged compiled source; do not repeat diagnosis2. Separately review the narrow
T121-PHYSICAL-JOINT-TERMINAL-ANCHOR contract before modifying its production guard.
Require the complete genuine sealed terminal delete/final History union while
retaining nonterminal update, fresh clock/owner/current-source and new-create
allocation rules. No generic delete allowance, guard weakening or new diagnostics.
T152 acceptance stays open; all R1/T149 and R2/HOLD boundaries remain.

# T152 safe projection built and exactly planned — GREEN pending

Independent Astra/high SOURCE accepts 894c9fb719e393a914ccbb601fbcbdc8d433325f.
All six required owners freshly build at that source, exit 0; warnings remain
81/23/14/0/26/107. Actual six PE/PDB identities and 434 source bindings match;
all 75 managed artifacts/transitive copies, 206 resource inputs / 1,236 copies
and 12 native copies pass current verification. The six changed source/docs
files were freshly downloaded from GitHub and independently checked by writer
and root; limited six-file readback proof SHA256:
38cc0d9829cb46f632fe45f0f9e4e1158926c68f0f4751c95b2419dd1fb6143c.
N2 artifact proof SHA256:
9bd2481857bc1f9e215ec94a8811bca4c024327595453bd7f54ab0a126842ec1.

Fresh catalog validation passes: 1,327 categories / 13,153 methods, no unmapped
or stale selector. Exact PlanOnly passes with 5 categories / 6 descriptors /
7 methods / 11 cases, zero execution and complete owned cleanup. Diagnosis2 /
causal5 / worsening1 retain60s; terminal-joint1 and shared-projection2 retain120s.
Plan SHA256:
e4066cd48554dd2914e412f266429d5a57d502a862f95c28fd4ed1f9154bf80d.
Primary N2-build-*, N2-pdb-proof, N2-catalog and N2-plan remain outside checkout
under /workspace/tboe-linux-evidence-20261102/notification-controls. No passing
runtime or whole-T152 claim follows from compilation/discovery. Next independent
PREP and one addressed GREEN11 with full existing peer capture and owned cleanup.
The 75-artifact pre-build archive and all previous raw remain intact. R2 UNKNOWN,
T149/R1 acceptance and every other HOLD retain their stated boundaries.

# T152 shared safe projection authored — SOURCE/GREEN pending

Independent Astra/high accepts exact N1 causal OUTCOME (four final-oracle failures,
one preserving PASS, all raw binding and nonmutation premises). The minimal
production delta now changes only WoundPlayerNotification.Project: call existing
WoundPlayerProjection.Text once and derive plain, escaped console and encoded
browser values from that same safe string. All caller admission, original claim,
canonical wound/scene DATA, visibility and authority logic remain unchanged.
This code is UNCOMPILED and GREEN is not claimed.

Root and an independent Sol read-only impact review traced the common helper
through acquisition/worsening, live spiritual combat, spiritual healing, Mortal
diagnosis/recovery and terminal treatment/legacy-summary output. Finite selection
is new causal/preserving5 plus actual shared-Project peers6: spiritual-owner1,
acquisition-escaping1, diagnosis2, terminal-joint1 and worsening1. Source estimate
is 11 cases / 7 methods / 6 project descriptors across five categories; exact
discovery remains pending. Existing 60/120-second bounds are preserved. Ordinary
treatment3 from the broader old peer category are not selected: they assert empty
notifications and never call Project. Their caller/binding code is unchanged.

The terminal peer uses a genuine joint owner and reduce_legacy setup with two
legacy effect batches; its output checks success, healed prefix and /раны. It
executes the legacy-summary loop, but has no separate per-summary text/count/
escaping assertion, so none is claimed. Other live-spiritual/combat templates use
the same helper and represented name/progress inputs; no heavy lifecycle/HOLD
replay is justified by this pure presentation delta. Old category owners remain;
new exact leaves only avoid unneeded broad/wildcard cases. Actual catalog reader
and CI selection metadata validation pass before publication.

Optimization: one existing bounded text-policy pass per projection value, reused
by all three representations. No I/O, parse, cache, permission, mutable shared
fixture or new sanitization policy is added. Raw equality checks still precede
projection. GM no-update: FR076 already requires safe rendering; author schema,
game rules, timing/cost, request/response authority and narrative ownership are
unchanged. New 75-artifact lossless archive before rebuilding is verified through
full gzip EOF/CRC and all member bytes: 102,618,589B SHA256
d9cdb6e38928b6d8ae31e0a2437b93ba61c12ef7868163b554e8d229d5b549a0.
Archive/proof are external under notification-controls. Next independent SOURCE,
fresh affected owner graph/pins/exact Plan/PREP and addressed GREEN. Parent T152,
accepted T149/R1, R2 UNKNOWN and broader HOLDs retain their stated scope.

# T152 causal RED5 complete — OUTCOME pending

Independent DESIGN/SOURCE/PREP accepts exact scaffold runtime
fea2b14e120408e94ecba2ed8ebee212981fd39e. Fresh Unit build exits 0 in 34.70s,
26 warnings / 0 errors, no changed-test warning. Five other R4 owners remain
unchanged. Current six PE/PDB pairs / 434 source bindings, 75 artifacts, 1,236
resource copies and 12 native copies pass. Fresh full catalog is valid:
1,325 categories / 13,153 methods, no unmapped/stale selector. Exact PlanOnly:
one Unit descriptor / two methods / five cases / 60s, zero execution.

N1-red5 completes all five rows in 7.4290193s, exit 1: exactly four intended
final-oracle FAIL and one LF/tab/Unicode PASS, no timeout/skip/duplicate/underfill.
Every Parse/raw-control admission, exact Claim, Compose success/no issues and
root/canonical wound/claim nonmutation premise passes before the four failures.
Name rows retain C0 in notification plain/console/browser-decoded projections;
narration rows retain it in the corresponding narration projections. Failure
messages contain only fixed safe labels. No terminal/browser renderer is executed.
Process cleanup succeeds; no new resource-fixture root remains. Production is
unchanged. This establishes the actual projection defect, not a parser/setup error.

Primary raw: /workspace/tboe-linux-evidence-20261102/notification-controls/N1-red5.
Summary SHA256 d9673090d75cc22dbeed7b34380fa84a6e4414d162adc5efc60561db9aa2e378;
TRX SHA256 c10bf6ff60df22772175c4eaae3b25ccc0e0b0f6e12e2332262119c538355b73.
Build/pins/catalog/plan are in sibling N1-* directories/files. Plan SHA256:
1095d6c8be37d5b3cdeadf1e7a4577a6170a340d5cd391159e9cb92e05944c11.
N1 artifact proof SHA256:
0fd9c3b73d4ec7918dd355f9b56a6c0a00f7966d87159b105db91550e715c6cf.

Next independent causal OUTCOME, then the accepted minimal shared Project fix.
Impact includes acquisition/worsening, spiritual healing/combat, Mortal diagnosis
and terminal treatment partials; address existing output peers before GREEN
admission, without replaying broader Engine/HOLD aggregates. No canonical/claim
rewrite or new text/Unicode policy is permitted. T149/R1 acceptance, R2 UNKNOWN
and all other HOLDs remain unchanged.

# T152 notification controls — five-row causal scaffold WIP

2026-11-02 UTC, confirmed base/remote96a0caec0a31345bff38f5d8727f6cc45bbe2762.
Tracked T152-NOTIFICATION-CONTROLS, parent T152 remains open. Only existing
WoundAcquisitionOutputTests, finite category/current selection and canonical
plan/tasks/checkpoint change. Production WoundPlayerNotification and all other
runtime source remain untouched; accepted T149/R1 and R2 UNKNOWN are preserved.

Four genuine pre-Parse JSON rows (field name/narration x safe numeric code27/8)
prove raw admission, exact Claim/Compose success/no issues and unchanged raw root,
canonical wound and claim before checking all player projections for forbidden
controls. Browser is also decoded independently. One genuine LF/tab/Unicode
positive compares fixed expected text across plain/console/browser. Safe case /
failure labels never print the C0 payload and no terminal/browser executes it.
Expected4RED/1PASS is only a proposal: parse/binding/setup rejection is not the
intended RED and would block the proposed production correction.

The next minimal design reuses WoundPlayerProjection.Text once per projection
AFTER original binding, then markup/HTML encodes the safe text. No canonical rewrite,
new cache/sanitizer/language/bidi policy, GM schema/mechanic or broader test replay.
Compile/fixture ownership, concrete consumer trace and optimization/GM rationale
are in the current plan. New exact60s leaf owns only new2 methods/5 source rows;
all pre-existing category entries and owners stay unchanged and unselected.

Actual Read-TestCategoryCatalog plus Get-TestCategoryCiMatrix metadata-only check
exit0:1,325 categories, selection schema valid, leaf methods2/budget60s/related0.
First pwsh process exit134 before module/catalog work because its default cache
was under read-only /home/agent; retry uses root's already prepared task-local XDG
data/cache/config/runtime paths without changing HOME. No build/discovery/test ran.
C# UNCOMPILED; owning Unit build/PDB/catalog inventory/PlanOnly/PREP/runtime are
UNRUN by writer instruction. Next independent SOURCE then root's real causal RED;
no production patch or acceptance before those gates.
Static writer audit: git diff --check exit0; new methods2/source rows4+1=5 and
numeric-only C0 case inputs checked; source contains no literal forbidden C0.
All pre-existing test/helper bodies and category entries remain unchanged; no
production diff. These source/metadata checks are not C# build, discovery or RED.

# T149 canonical player-carrier correction accepted

Independent Astra/high final OUTCOME accepts T149-CANONICAL-PLAYER-CARRIER:
11 distinct PASS on the explicit R4/R5 runtimes below, complete primary results
and cleanup, no fresh-aggregate claim. Root checked the primary evidence and
marks only this bounded task complete. Parent T149, noncanonical loose aliases,
R2 browser UNKNOWN and all other HOLDs remain open. The obsolete scaffold paragraph
misplaced beneath T152 has been removed; its historical source/RED provenance
already remains in this checkpoint. No production change accompanies acceptance.

Next T152 scope: typed Parse/Claim/Compose tests must prove interior C0 controls
are removed only from player notification/narration projections, after exact raw
binding. Reuse existing WoundPlayerProjection.Text: retain its newline/tab/Unicode
policy, then escape once for console/browser. No terminal control payload is
executed, no canonical claim/wound data is rewritten, and no new policy or cache
is introduced. The source finding is not yet a runtime RED or implementation.

# T149 remaining inventory PASS — final bounded OUTCOME pending

Independent schema delta/PREP accepts exact singleton runtime
ef2c7623a7cd556c49e0780f23c25fd7100cb1d7 after corrected catalog validation:
1,324 categories / 13,151 methods, no unmapped/stale selector. The singleton
completes 1/1 descriptor/method/case in 8.2239244s, exit 0, no timeout, skip,
duplicate or underfill; process cleanup and explicit fixture absence pass.
Primary R5-inventory1 summary SHA256:
a7fb3b7e134cc41fd49fa83ac4ebe8deb70e08a03db4e8fb7cfd258963217be0;
TRX SHA256:
4ecb43ae5cc5efd1a4f180928e75f8cbe8ec236bbd34ad43988dad8e0326b8d2.

The bounded T149 evidence is now 11 distinct PASS on two reviewed versions:
typed Integration 9 and Unit confinement 1 at f21ec22c, corrected inventory 1 at
ef2c7623. It is not one fresh eleven-case run. Main/Support/Bridge/CrashHost /
Integration retain source 9dc61ba9f; Unit is built at eaec6901f, with only the
reviewed anchor literal changing. Final bounded OUTCOME is pending. Parent T149,
all other source surfaces, R2 browser UNKNOWN and broader HOLDs remain open.

# T149 Unit built; catalog link schema correction before discovery

Delta SOURCE accepts restored unselected root ownership and the exact singleton
leaf on eaec6901fb02d49ec956856dd705a9293c9939d7. Fresh Unit-only build at that
source exits 0 in 34.65 seconds, unchanged 26 warnings / 0 errors. Five other
owner outputs remain R4; current six PE/PDB pairs and 433 source bindings match,
as do 75 artifacts, 1,236 resource copies and 12 native copies. R5 artifact proof
SHA256 64b32f5039a8421fac907c13b00a4f1b3bd173bad6504f7498a9a33787e2ab02.

The first R5 catalog/Plan commands stop in Read-TestCategoryCatalog before
discovery, result-directory allocation or test execution: the new optional
related link was a string, but the existing schema requires an id/when object.
This metadata defect was missed by the preceding static review. The unnecessary
link is now removed (explicit empty related array), preserving all selectors and
ownership. The actual catalog reader accepts all 1,324 categories. No C# source
changed, so no repeated Unit build is needed. Primary failed logs remain external
in R5-catalog/R5-plan; fresh corrected catalog and exact PlanOnly are next, then
independent PREP. Singleton runtime is still UNRUN; prior ten PASS are unchanged.

# T149 partial OUTCOME accepted; catalog ownership correction WIP

Independent Astra/high accepts R4 Integration9 + Unit confinement1 PASS and the
sole stale inventory-anchor failure, and accepts the one-line partial modifier
repair. It blocks the first catalog narrowing: replacing the root category's
selectors would leave all three new Integration methods unmapped. This was a
metadata error, not a failure of the ten passing runtime cases.

The full wounds-canonical-player-carrier-root category is now restored unchanged
from the reviewed R4 plan and remains unselected. A separate finite
wounds-canonical-player-carrier-inventory category owns only the failed Unit
method at 60s and is the sole current selection. All nine typed rows retain
their catalog owner; no passing case needs replay for this correction. Current
catalog has one additional category and unchanged methods; fresh full catalog
ownership validation is required after the Unit build, before exact singleton
Plan/PREP. No production or fixture behavior changes. Delta SOURCE remains open.

# T149 behavioral GREEN9; one stale inventory anchor correction pending

Actual runtime f21ec22c2e4e83398384c160c8cadd9e04d3fafb completes both descriptors
and all 11 rows in 15.7671557s: 10 PASS / 1 FAIL, exit 1, no timeout, duplicate,
skip or underfill. All typed Integration 9 PASS, including both formerly accepted
bare arrays, scalar/null 5 and genuine canonical 2. Exact source bytes remain
unchanged. The Unit legacy-confinement method also PASS. Process cleanup and
explicit new-fixture absence are proven. Overall GREEN11 is not claimed.

The remaining Unit inventory method fails one textual declaration anchor:
PendingTurnSnapshotPathPresenceV1 is now internal static partial class, while the
inventory requires internal static class. The class remains in the same source
file; its companion DiagnosisObservation partial extends the existing closed
presence owner. Current LegacyClosedLogicalPaths, ClosedLogicalPaths and original
reader APIs remain present. A complete static audit of all 67 inventory entries
finds only this one missing anchor, not a sequence of undiscovered failures.
The correction adds only the actual partial modifier to that exact class anchor;
no production, scope allowance, fingerprint or authority check is changed.

Current finite selection is only the failed Unit inventory method, one row at
the same 60s category budget. The ten passing rows retain the R4 runtime lineage
and are not repeated for this test-only literal change. Five R4 Unit-owned current
artifacts were preserved before its future rebuild: verified gzip EOF/CRC and all
exact member bytes, archive 4,508,054B SHA256
bab82457f821c7dc4f38d540007489a757b13d5ec7bc786fb23e8195ee2f2a75.

Primary external R4-green11 summary SHA256:
e6d8d43907b7fdd019f2ae5d26a5a1712a37484a654d1cbd59990f2d2d9e9431.
Integration TRX SHA256:
59ca91ddff4b3e7500f75937b8a4a87489c7a1b716467e3129ba9ac38f797466;
Unit TRX SHA256:
9f5dbf036eec2083fa9fa92f0cd1c6451e325712b923d82587e0fa3471d31dd2.
All raw and archives remain outside checkout. Independent partial OUTCOME and
delta SOURCE are next, followed by fresh Unit-only build/exact PREP and one-row
confirmation. Optimization/GM: literal inventory repair adds no production work,
cache or authored contract. T149 acceptance stays pending; R2 and other HOLDs stay.

# T149 correction built and exactly planned — GREEN pending

Independent Astra/high SOURCE accepts implementation
01b1a01f4f4a9c166f6aa8e385f291fa80c73114 plus doc-only correction
9dc61ba9f9e323b87064aecb41e8553a800aca84. All seven changed files at 9dc61ba9f
were freshly downloaded from GitHub into a new empty directory and checked by
both writer and root against exact bytes, SHA256 and Git blob identities. Remote
SHA before/after matches; this proves those seven files only, not a full clone.
External readback proof SHA256:
60e3728ed2e07379b057f5c8347c3440c4fc8c9a44e6e07b626a89be585a530b.

Fresh source 9dc61ba9f builds all six owners successfully: Main 63.770642s,
Support 8.057978s, Bridge 3.498230s, CrashHost 2.423188s, Unit 36.489286s,
Integration 72.113917s. Warning counts remain 81/23/14/0/26/107; no warning names
the changed validator, inventory guard or new typed fixture. All six PE/PDB pairs
match and 433 current source bindings pass (the newly changed Main validator adds
one binding to the prior 432-source set). All 75 managed artifacts/transitive
copies, 206 operational inputs / 1,236 copies and 12 native copies are checked.
R4-artifact-resource-proof.json SHA256:
7801883c1bb4dab5a0fd1630802765b2f74e7c0d3fde0ef2563ffd08ece27e61.

Current exact PlanOnly exits 0: one category / two project descriptors / five
methods / eleven cases, unchanged category 60s, zero runtime and complete owned
cleanup. Plan SHA256:
f073a223be808092538719e33f1b21a153998f35436b330ef847a5b9e75026b3.
Full catalog inventory retains valid R3 lineage (1,323 categories / 13,151 methods):
no method, attribute, source membership or class changed; two existing methods
were added to this finite selector and are freshly discovered by current PlanOnly.
No whole discovery/test aggregate is repeated. Primary R4-build-*, R4-pdb-proof
and R4-plan evidence is external under browser-read-recovery. Next independent
PREP and one GREEN11, with explicit fixture absence checks. Source acceptance and
successful compilation alone do not close runtime, parent T149 or broader HOLDs.

# T149 canonical-first correction WIP — SOURCE and GREEN pending

Independent Astra/high accepts the exact causal RED9 on runtime
603c45e82112e58ee4a838152c443fdc55343635: array failures 2 / preserving PASS 7,
all rows complete, immutable input premises and explicit fixture absence proven.
The following minimal production correction now exists but is UNCOMPILED/UNRUN.

ValidateWoundsContainer dispatches the exact canonical PlayerPath first. Every
non-object root receives an Error on that root with the existing diagnostic code
wound_carrier_invalid_root. Objects enter the identical existing catalog Build
body. Generic noncanonical arrays, object handling and the playerWoundChanges
caller remain unchanged; no accepted-turn publication bypass is claimed repaired.
The existing source inventory now explicitly owns this strict canonical seam;
its legitimate loose-caller allowances are retained, not mechanically removed.

Finite GREEN selection is the unchanged typed Integration 9 plus two existing
Unit semantic inventory/legacy-confinement methods: 11 cases / 5 methods / two
project descriptors in the same 60-second category. The Unit methods are needed
because their shared inventory changed. Existing pure catalog object validation
is unchanged; no whole suite or old browser36 replay is selected. Fresh affected
Main/Support/Unit/Integration outputs and actual build-only dependencies are needed
before NoBuild, then source/PDB/resource pins, exact discovery/PlanOnly and PREP.

Optimization: the existing fresh source read and JsonDocument parse are unchanged;
a cheap root dispatch precedes only catalog conversion/validation. It adds no
read, parse, cache, lifetime or shared DATA;
strict object parsing and all fresh ownership/integrity checks remain unchanged.
No new speedup or source-count claim. GM no-update: the accepted closed schema-v1
contract already rejects legacy player roots; this adds early enforcement without
new authored fields, rules, costs, cycle, response, pending or output contracts.
R3 managed archive and primary RED evidence remain external and unchanged.
Next independent SOURCE; runtime acceptance and parent T149 stay open. All R1/R2
and broader HOLD limits remain as recorded, with no new browser retry.

# T149 causal RED9 complete — independent OUTCOME pending

Actual clean runtime 603c45e82112e58ee4a838152c443fdc55343635, compiled Integration
0235df8fceadc875b03fb86d1a923c3b00bac796 with unchanged five other owners.
After independent PREP, exact category wounds-canonical-player-carrier-root
completes all 9 cases / 3 methods / 1 descriptor in 9.3483924 seconds, exit 1:
7 PASS / 2 intended FAIL, no timeout, skip, duplicate or underfill. Both bare-array
rows fail only the final line-28 Assert.Contains(root Error): actual issues are
empty. All earlier input/shape/byte invariance premises pass. Scalar/null 5 and
genuine canonical 2 controls PASS. This is a real missing-refusal RED, not a newly
invented diagnostic-code failure. Production remains unchanged.

Owned-tree/runtime cleanup both succeed. Explicit private-temp prefix inventories
are empty before and after; no new boe-resource-materialization-* root remains.
Primary external attempt: R3-red9 under the current browser-read-recovery raw root.
Summary SHA256 f258b50e037e522c0a866c9301f284dd784e9d11125069e316e1e9089d77930c;
TRX SHA256 32118b7715af8e875091dd3b59f48d86fd65e1c3790d008280342f30621f1d60.
Independent OUTCOME must accept these exact failures before the minimal canonical
dispatch correction. GREEN and parent T149 remain open; no browser or HOLD retry.

# T149 scaffold built and exactly planned — RED runtime pending

Independent Astra/high accepts bounded DESIGN and scaffold SOURCE
75f8a92c425f48b161c67b3ffaeb3ffec161b04f; receipt-only checkpoint
0235df8fceadc875b03fb86d1a923c3b00bac796 preserves that source. Fresh Integration
build at 0235df8f exits 0 in 68.67 seconds, 107 warnings / 0 errors, no warning
from the new fixture. All five other owner inputs/outputs remain unchanged.
Current six PE/PDB pairs and 432 changed-source bindings match source; all 75
managed artifacts, 206 operational inputs / 1,236 copies and 12 native copies
are checked. No native runtime or gameplay qualification follows from these checks.

Fresh ValidateCatalog exits 0: 1,323 categories, 13,151 methods, no unmapped or
stale selector. Exact PlanOnly exits 0 with one Integration descriptor / three
methods / nine cases / 60 seconds, zero executions and complete owned cleanup.
Arrays 2 are intended causal failures; scalar/null 5 and canonical 2 preserve
already working behavior. A different failure requires investigation. Production
and the existing loose-caller inventory guard remain unchanged.

Current managed outputs are preserved before the next production build: external
R3-current-managed-outputs.tar.gz, 89,961,516 bytes / 75 members / 288,489,817
logical bytes. Complete gzip EOF/CRC and every member length/hash match; archive
SHA256 fc6c5df3e926d9ef70e23dc6c286c0b36453c3698e8335c6738fb9d732402d81.
No current output or other evidence was deleted.

Primary evidence: /workspace/tboe-linux-evidence-20261102/browser-read-recovery,
R3-build-integration, R3-pdb-proof.json, R3-catalog and R3-plan. Artifact proof SHA256:
03b992a40209577f049262804d9ca4ebb2d0499c476bdd5ccbe8fafccd0c46d3.
Exact plan SHA256:
94a9d8afbb594b5f9beb3c5874dcba73699e2ae8d491fe928a3e6b4b2d553fde.
New private temp namespace baseline is recorded externally; because existing
fixture disposal is best-effort, OUTCOME must also verify that no new resource
fixture root remains. Next independent PREP and one exact causal RED9, before any
production fix. R1 acceptance, R2 UNKNOWN and all other HOLDs remain unchanged.

# T149 canonical player-carrier causal scaffold — source-only WIP

Frozen scaffold source75f8a92c425f48b161c67b3ffaeb3ffec161b04f was pushed and
full remote SHA confirmed. All six changed files were freshly downloaded by exact
commit from GitHub into new /tmp/boe-t149-scaffold-source-restore-75f8a92c4;
each matches its Git blob SHA and bytes. This is recovery of those six source /
metadata/checkpoint files only, not a full clone, compilation, discovery, RED or
old raw recovery. No implementation edit followed the freeze; this receipt is
checkpoint-only. Independent coherent design/SOURCE and root verification remain.

2026-11-02 UTC, base/confirmed remote1969e53ae1a276f9bbcf99b326fc54383d1fbd9b.
Tracked T149-CANONICAL-PLAYER-CARRIER; parent T149 remains unchecked. Canonical
minimal design, exact two-caller trace, fixture/Compile ownership, optimization
and GM no-update rationale are at the top of plan.md. No production implementation
or existing source-guard allowance changes occur in this scaffold.

One new Integration test file uses existing ResourceMaterializationTestContext and
TestSupport-owned WoundContractTestData. Three exact methods/nine authored rows:
actual PlayerStateFiles canonical-path selection must refuse bare arrays2;
scalar/null5 preserve generic root Error; genuine catalog-valid empty/populated
carriers2 stay accepted. Every row checks source bytes unchanged after the actual
validator. The causal oracle is a real Error on the canonical root, not a new
code/message. Only those two missing-refusal rows are expected to be causal RED;
setup/compile/underfill/timeout or child-error failures are not accepted evidence.
One exact60s category/current selection owns the scaffold; no old accepted set
or broad catalog category is replayed. Existing malformed-object/unknown-field
catalog controls remain unchanged. All later ownership/publication fences remain.

R2 OUTCOME has now been independently accepted by Astra/high as TIMEOUT/UNKNOWN;
the primary runtime/source/budget/prefix/cleanup values below are unchanged.
No browser retry or broader HOLD release. Current scaffold is C# UNCOMPILED and
all builds/restore/discovery/catalog/PlanOnly/runtime are UNRUN by instruction.
Next is independent bounded design/SOURCE, then root's fresh owning build/exact
PlanOnly/PREP/causal RED before any production fix authorization.
Writer static checks: git diff --check exit0; catalog/selection JSON parse with
unique IDs and exact three selectors; source count2+5+2=9; public test parameters
are primitive; one actual typed phase call in the private helper. Production and
existing legacy guard diffs are empty. These checks prove source consistency only,
not compilation, discovery, intended RED or runtime acceptance.

# R1 accepted; R2 full browser course TIMEOUT / UNKNOWN

Independent Astra/high accepts all primary R1 GREEN36 results and their exact
selection on runtime 49e521205b5a84dcc6bd2ae3accd1a5a1839fcd9. Its separate
PREP admits one unchanged browser course on clean runtime
11ca0dc310b47f4bacc2fd9825dcc9603f45ed6e, using the same compiled source cohort.
No timer, collector, assertion, ordinary wait, separate confirmation or 180-second
budget changed. Fresh launch proofs and full output are external.

R2-browser-course exits 124: actual category 180.1041994 / 180 seconds, wrapper
185.7070208 seconds. One planned descriptor/case, zero completed TRX/cases;
therefore UNKNOWN, not a completed gameplay FAIL or PASS. Managed-tree/runtime
cleanup succeeds, but fixture cleanup is unproven: the actual registered root
remains, with 280 inventoried entries and 1,056,557 regular-file bytes. The runner's
generic final message mentions its eight-minute outer ceiling; the primary
CategoryResults records the actual three-minute category timeout above.

Full available compressed prefix: 1,244,545,158 bytes, SHA256
8068ec18b9aa157de788b05eb784aabbc54283ec400f0c07750fe06e19a25f55.
No report/footer/CRC/full-capture qualification exists; decoder EOF is false.
Read-only folding of existing macro timers observes 12,161,411,897 decoded prefix
bytes / 23,667,403 complete newline rows. It performs no new recording and is not
universal row validation. First ProcessConfirmedAttempt closes at 76.787321877s,
then OrdinaryAcceptedWait closes at 8.757262515s; a second separately confirmed
attempt begins at event elapsed 88.4498424s and has no observed closing event.
Nine closed CrossReferences intervals total 1.778202006s; nine Meta intervals
total 2.263180197s. These nested inclusive intervals are not added to journey
duration, and differing historical prefixes/environments do not prove a universal
elapsed saving or the full timeout cause. No new browser retry is admitted.

Primary new evidence root:
/workspace/tboe-linux-evidence-20261102/browser-read-recovery/R2-browser-course.
Summary SHA256: 45295815b7ba06780da69bf8b7e9965e23657fc548f19047283216836f5546d4.
Retained-root inventory SHA256:
441565711035e40f285217e602e170958faa21bfb5bbfe163fc5c8067b002254.
Existing-timer prefix fold and its full output remain external. No old raw, saves,
recovery or unique evidence was deleted; free space remains about 27.0 GB.
Independent Astra/high accepts R2 OUTCOME as TIMEOUT/UNKNOWN against
1969e53ae1a276f9bbcf99b326fc54383d1fbd9b; no full-course acceptance or retry.
Accepted R1 remains separate. Broader native,
C9/C10, DFR/Mortal/Windows HOLDs and #1536 acceptance remain open.

Next independent functional correction is T149 canonical player-carrier
validation: verify the observed bare legacy-array early return before the strict
carrier branch, obtain a real typed PlayerStateFiles RED, then retire that bypass
without changing accepted-turn integrity or introducing save compatibility.
Existing raw timers suffice for this bounded R2 localization; no further
diagnostic framework or performance experiment is prerequisite to T149.

# R1 GREEN36 complete — independent OUTCOME pending

2026-11-02 UTC, actual clean runtime
49e521205b5a84dcc6bd2ae3accd1a5a1839fcd9; source/build cohort is unchanged from
the verified frontier below. Independent Astra/high PREP accepted exactly this
36-case selection and the guarded existing-runner launch. The fresh sequential
run exits 0 in 64.7598949 seconds: 11/11 descriptors, 16 methods, 36/36 PASS,
no skip, timeout, duplicate or underfill. Both owned-tree and runtime cleanup
succeed. Actual attempt R1-green36 started 02:37:21.922 UTC and ended
02:38:28.204 UTC; no source change or repeated prior-environment test occurred.

Exact case groups: causal 4, borrow 4, existing Meta readset drift 1, new safety
23 (failure/close 5, freshness 2, instance 6, late debt 2, original/owner 4,
mutation/fallback 4), legacy exception 4. Every group has its own complete TRX.
The four causal cases retain equal ordered findings, source DATA/absence, bytes,
full nested Meta readset and original/owner premises in both control orders.
Actual automatic/manual physical counts are 5/5 for CrossReferences and 6/6 for
Meta, with all 10/10 actual source reads in each pair. This closes the previously
accepted missing-reservation RED (historically 196/5 and 309/6); it establishes
bounded physical-work parity, not a stopwatch speedup or full browser result.

Primary raw directory:
/workspace/tboe-linux-evidence-20261102/browser-read-recovery/R1-green36.
Complete summary SHA256:
6e8ea5751ec7d26e77656519dce8dd945d51dfa9cd39332d3f34e49eb2716e55.
Causal CrossReferences TRX SHA256:
d9d61c286219ca0a1ea2a3a03a8a10550c6f5211a819a706fb2c84d941f83cf4;
causal Meta TRX SHA256:
27ba439144e6eebdcee3bcb2bc737dae4c767d8a50f195549b7fa599b305166f.
All full raw remains outside Git. No old capture or unique evidence was removed.
Independent OUTCOME and separate one-case browser PlanOnly/resource/PREP are next;
the original full browser course is still UNKNOWN, and broader HOLDs remain.

# R1 fresh build/discovery frontier — runtime pending

2026-11-02 UTC. Remote task branch and clean local HEAD were checked at
afe5f4374a41b6d0962771121c2170fbfdeb6254. Independent Astra/high delta SOURCE
accepts the fixture-only bool mapping; all 36 source rows and existing budgets
remain unchanged. Integration-only C1 now exits 0: 107 warnings, 0 errors,
69.12 seconds. No warning names the new BrowserGameStateReadReservationTests.
The first five successful owning outputs remain at
87339cf53eb0fa4722b4216afdfc42abea7f5701; their project inputs are unchanged.
The failed initial Integration log remains available and is not a functional RED.

Fresh build warnings by owner: Main 81, TestSupport 23, GMBridge 14,
PortableStorageCrashHost 0, Unit 26, Integration 107. XML documentation was enabled
with only the documented missing-member 1591 suppression. Main's 69 XML warnings
are pre-existing source; no unavailable old warning multiset is claimed rechecked.
GMBridge is a real build-only Unit dependency (ReferenceOutputAssembly=false),
so its standalone output is checked without requiring its DLL in Unit's folder.

The six actual Compile inventories and PE/PDB identities match; 431 changed-source
bindings since b43e1bca match current source bytes, including linked helper owners.
All 75 required managed DLL/PDB/XML/deps/runtimeconfig artifacts and transitive
copies match their fresh owner. All 206 declared operational resource inputs
match 1,236 output copies; 12 supervisor/manifest copies match the fresh package.
External proof R1-artifact-resource-proof.json SHA256:
4e9a0f7eae2684b18425e54b97f8879d4db8090a28b0dd2b43c5acb66a1639ca.
PDB proof: R1-pdb-proof.json; helper SHA256
9fbc5b41e90ca3ba7ac50e8add2e8327e0515619f65b84669b2eceb8e1d04993.
These proofs are build/source integrity, not native or gameplay qualification.

Fresh ValidateCatalog -NoBuild exits 0: 1,322 categories, 13,148 methods, no
unmapped methods or stale selectors. Exact SelectionFile PlanOnly exits 0:
11 descriptors / 16 methods / 36 cases, no runtime executed. Ten category
budgets remain 60 seconds and legacy-exception remains 180 seconds; full browser
remains 180 seconds. Both discovery operations report complete owned cleanup.
All new raw is under /workspace/tboe-linux-evidence-20261102/browser-read-recovery,
with R1-build-*, R1-catalog and R1-plan preserving full output. Old raw/binaries
are still unavailable. Next: independent PREP, then exactly the selected 36 GREEN
cases. Full browser admission/outcome is separate and still pending; older
UNKNOWN/HOLD and historical acceptance boundaries are unchanged.

# R1 owning builds — Integration fixture accessibility correction WIP

2026-11-02 UTC, base87339cf53eb0fa4722b4216afdfc42abea7f5701 was confirmed
against the full remote task-branch SHA before this bounded edit. Independent
Astra/high SOURCE1a581b206 remains accepted; no production code changes here.

Root's fresh XML/native-package build graph on87339cf53eb0fa4722b4216afdfc42abea7f5701:
Main, TestSupport, GMBridge, PortableStorageCrashHost and Unit all exit0. Compiler
warning counts are respectively81/23/14/0/26. Main includes69 existing XML warnings
exposed by GenerateDocumentationFile; these are not new runtime defects. The five
successful owning outputs are retained and are not rebuilt for this fixture edit.
Integration exits1 after4.54s with exactly4CS0051 errors,0warnings. No C# runtime,
discovery, catalogue, PlanOnly or tests have executed. This compilation failure
is not a functional RED. Primary external log:
/workspace/tboe-linux-evidence-20261102/browser-read-recovery/R1-build-integration/build.log,
SHA25631d167a63ca4ef6d374952bfc44b8ea539c37f03def51a362f0032f0da690c86.

The four public automatic theory methods exposed internal GameStateValidationPhase
in their public signatures (original lines331/382/417/456). Fixture-only correction
uses public bool meta with explicit local typed mapping false→CrossReferences /
true→MetaMiscStateFiles. All twelve affected input rows retain the same phase and
secondary bool, order, method names, assertions, counters, premises and budgets.
Other public API signatures in this single fixture file use public framework /
xUnit types or primitive bool/string; no other occurrence of this issue found.
Production enum visibility and all production files remain unchanged. Category /
selection metadata is unchanged because names and36 source rows are unchanged.

Current correction is source-only UNCOMPILED/UNRUN; writer ran no compile, restore,
discovery or runtime. Static diff and source-row/signature/body equivalence checks
are separate evidence only. Next: narrow independent Astra delta SOURCE, then root's
fresh Integration-onlyC1, preserving the five successful owner outputs. Existing
PDB/resource/catalogue/PREP/GREEN and full browser admission gates remain open.

# Recovery SOURCE accepted — earlier pre-build frontier

2026-11-02: independent Astra/high accepts reconstructed SOURCE
1a581b206bb928cbab34802d274551edd5cc8121 againstc3a3eee2a9eed60089c0b5333bdd3d0e6201698c.
It verified the three production files, complete lifetime/debt/exception/cache
contract, unchanged reader fallbacks and historical8 impact, and exact11 categories /
16 methods/36 source rows. No substantive finding; no runtime or browser admission.
Receipt75a4e687ecd088b4885563a5e4ada99558f36165 changes only the checkpoint.

Root restored the same official hash-verified SDK10.0.402, .NET/ASP.NET8.0.32 and
PowerShell7.6.8 privately under /workspace/tboe-toolchain-20261102. Native package
was freshly compiled from unchanged native source atc3a3eee2a: four linux-x64 ELF
assets and source/binary hashes match; package manifest SHA
9f9679b77a4480ad842d4cece21d84f8f1d63c9d0fb9b57bea59b5c4a9aed457,
minimum glibc2.38. This is a build/ELF proof, not native runtime qualification.

Initial dependency restore stopped before project work at NuGet-Migrations mutex
creation. The installed runtime's documented user-scoped IPC location needed an
owned0700 XDG_RUNTIME_DIR; task-local configuration fixed this without changing
HOME, global mutex scope, security checks or the project. Both selected restores
then exit0. Full bootstrap/raw remains outside checkout under the new evidence root.
No C# compilation, discovery, tests or browser retry has run in this environment.

Next fresh build graph: Main, TestSupport, GMBridge and PortableStorageCrashHost
(the two latter are real Unit-project dependencies), then Unit and Integration.
Build each required owner once in dependency order, with the fresh native package;
preserve all XML/compiler diagnostics except the documented missing-member1591
noise. Then actual PE/PDB/source/resource/native pins, catalogue, exact PlanOnly,
independent PREP and addressed36-row GREEN. Old broad runs are not restarted.

# Recovered environment frontier — earlier WIP

Canonical recovery/next action is at the top of plan.md. Remote base
f9138c817bb06b5a20c5737d0b39b3160be608e7 was confirmed before this edit; complete
31290-file source tree is materialized in the new isolated task worktree.
Previous uncommitted fix/raw/binaries/archives/toolchain/helpers are unavailable.
New external raw root /workspace/tboe-linux-evidence-20261102; nothing below
claims old raw was recovered or reverified in this environment.

Astra/high accepted the historical causal4 OUTCOME after f913 push: only final
oracle4FAIL, complete correctness/readset/owner premises,196/5 CrossReferences and
309/6 Meta physical checks, source10/10, both orders, no timeout/underfill, cleanup
true. Guards17 retain14PASS/3 fixtureFAIL (borrow4+drift1 PASS, owned12=9/3).
The narrowly approved legacy fix asserts ReadWorldValidationTextAsync's actual
async frame and preserves every other assertion; all4 rows require fresh rerun.
Historical four builds/PDB4/430bindings/27artifacts/44resources and exact warning
multisets remain prior-environment receipts only. The following pending/UNRUN
paragraphs describe their earlier point in time and are superseded by this entry.

Current reconstruction is C# UNCOMPILED and all builds/discovery/catalog/PlanOnly /
runtime UNRUN by explicit instruction. This bounded WIP first reconciles recovery;
next source-only implementation/tests/catalog delta goes to independent Astra/high
SOURCE before root's fresh owning builds/PREP/GREEN. No full browser or deep suite.

# Reconstructed wrapper/debt source — coherent WIP, UNCOMPILED/UNRUN

Source-only handoff commit1a581b206bb928cbab34802d274551edd5cc8121 was pushed
and full remote ref confirmed. Its10 changed files were independently downloaded
from GitHub by exact commit into new /tmp/boe-browser-source-restore-1a581b206;
every file matches its Git blob SHA and bytes. This proves recovery of those10
files only, not a fresh full checkout, compilation, tests or old raw/artifacts.
Initial recovery frontier WIPc3a3eee2a9eed60089c0b5333bdd3d0e6201698c is also
remote-preserved. Parent receives the frozen implementation for independent
Astra/high SOURCE; no implementation edits followed this source freeze.

The current delta implements only the accepted closed phase/World reservation
lifetime. It isolates/restores faction and Guardian context caches, retains the
nested Meta full readset, performs full original end proof then fresh both-root
publication-debt refusal under the real lease, closes and checks original owner
before appending local ordered findings. Cleanup preserves primary exceptions;
no evidence recovery/delete or ordinary DATA/browser guard relaxation is added.
Manual causal control has the same debt closure. The reviewed legacy4 async-frame
assertion changes alone; all other original assertions remain.

Current source selection is11 categories/16 methods/36 rows: causal4, borrow4,
existing drift1, new safety23, legacy4. Exact family variants/manual-vs-automatic
limits, unchanged historical8 peer impact rationale and optimization/GM assessment
are in plan.md and tests/categories.json. All protective60/180s limits are unchanged.
Raw output remains outside Git at /workspace/tboe-linux-evidence-20261102; previous
raw/artifacts are unavailable. No builds/restore/discovery/catalog/PlanOnly/tests,
cleanup/native compilation or fullbrowser/DFR/Mortal/Windows work was run by writer.
Static source/diff/JSON/selector counts are the only current verification; no new
physical/source counts, elapsed savings or GREEN are claimed. Independent coherent
Astra/high SOURCE and root's fresh owning builds/discovery/PREP/GREEN remain open.
Static writer self-audit: git diff --check exit0; both catalog/selection parse;
unique category IDs, exact selected method names, source InlineData/Fact row counts
(11/16/36) and unchanged60/180s budgets checked by Python. Legacy fixture diff is
exactly one assertion line. Existing Meta borrow/readset body, MainRunFence and
SourceDataOperation are unchanged. No diagnostic/generated file is staged. This
is source inspection, not C# compilation, discovery, runtime or independent review.

# B1RED causal4 — intended final-oracle RED; independent OUTCOME pending

Both exact causal leaf runs completed on runtime
e24536df4e59835fc7cc2104cf79bbd66ab1e398 / compiled
9d021ae82930099a1a77bf6ae9ad6ff18ade322d with no source changes between them.
All4 planned cases execute and fail only the final missing-reservation work
oracle at BrowserGameStateReadReservationTests.cs:61. Every preceding ordered
issue/DATA/absence/current-original byte/readset/physical-owner premise succeeds.
No timeout; complete selection and both owned/runtime cleanup pass in each run.

| Selected phase | Both control orders: subject/control physical verifications | Source reads in each arm |
| --- | --- | --- |
| CrossReferences |196/5|10/10|
| MetaMisc |309/6|10/10|

Counts include opening/full-end/Dispose/owner-after-close. These independent small
fixtures show repeated-admission work, not elapsed speedup or full-course PASS.
Primary external TRX: B1RED-causal-cross2 SHA
4ea7896c5ea74f44fd6125cba51363bd17a4d8fcdfae40da91144e328dab5cd5;
B1RED-causal-meta2 SHAacf6d23733bb1f58b4679670ab1a9ab7675682014ff50b69cc8f2fcd2558f734.
Post-run27 artifacts/44 resource copies/four daemons remain stable, no owners;
free703504384B. Earlier guards remain14 PASS/3 stale-stack fixture FAIL, not17PASS.
Independent OUTCOME/next implementation admission is pending. The exact legacy
async-boundary assertion still needs its reviewed correction and four-row rerun.
No outer automatic reservation/debt wrapper or full browser retry has begun.

# B1RED guards — 14 PASS / 3 FAIL; earlier causal frontier

Actual runtime65ac18d1cadbddedb5d44b8c1b41042083f0af3b, compiled source9d021ae82:
all17 planned guards executed,14 PASS/3 FAIL, no timeout,43.6025559s wall;
both runtime/owned-tree cleanup succeed. Borrow API4 and exact Meta readset drift1
PASS. Existing owned peers12 yield9 PASS/3 FAIL. Actual post-run27 artifacts /
44 resource copies/four daemons remain stable, no live managed owners,
free707645440B. This is not17 PASS or acceptance of the whole scaffold.

All three failures are the read/cancel/read-and-close rows of the existing
MetaControlShapes_AsyncFailureIsImmediateOriginalAndDoesNotPublish at line139:
the stack assertion expects ReadOrdinaryFileSnapshotCoreAsync. Full stored
WorldReadComponent evidence shows the same original exception (reached1,
SamePrimary=true, ResultPublished=false), through the actual async leaf
ReadOrdinaryFileSnapshotUncachedAsyncT607Original and ReadWorldValidationTextAsync.
The combined read/close row also records Calls1/SameSecondary=true. The expected
CoreAsync method is a non-async Task-forwarder in unchanged OrdinaryReads.cs;
its missing frame is a stale fixture oracle, not evidence of a replaced exception.
Assertions after the failed stack assertion are unexecuted in those rows and
still need verification after the reviewed fixture correction.

Primary TRX hashes: borrow4 ab4ff8a1927180060f5119eedfe290c34d3ce50d9f983e896e5b6ad726bc2c85;
drift1 b0cdbc615fd8dd1b1b5bc4e5d28da2f428f4f2be86d7361c92c09bd38471d4ac;
owned12 43cc293fcf1ed7f5e407922204e8e7d1aa035177cf3a8ce650bcb171f48a2f81.
Full primary raw remains external in B1RED-guards17. Independent OUTCOME and the
minimal stack-oracle repair/admission decision are pending. No retry, outer
production optimization, causal RED or full browser result is claimed.

# B1RED scaffold preparation — earlier built frontier

2026-11-02 UTC, same Linux checkout/branch. Independent Astra/high delta SOURCE
accepts9d021ae82930099a1a77bf6ae9ad6ff18ade322d after the mutable-hook fixture,
owner-after-close observation window and bounded borrow lifecycle selection were
corrected. Root checked the actual diff and remote SHA; the writer's exact10-file
blob readback is limited source recovery, not a new full checkout qualification.

All four fresh managed owning builds on that exact source exit0: Main123.580s,
Support16.413s, Unit63.603s, Integration123.683s. Commands use the existing
dotnet build --no-restore -m:1 -p:UseSharedCompilation=false -nr:false
-p:BuildProjectReferences=false path. Warning code/message multisets exactly match
their baseline:12/23/26/102 compiler warnings (24/46/52/204 logged), no additions.
All4 PE/PDB pairs and430 changed-source bindings verify;27 artifacts,11 resource
files/44 copies and4 daemon copies are current. External preflight SHA
10710d5d54570b756133b5d121fdcf53be4503133d5b0445811075fe107b998f;
PDB proof SHA ba61dfa29801bfefcea8765d5db4f36e7cf3e3c187f63a067f72b29838a9e6da.

ValidateCatalog exit0:1315 categories/13138 methods, no unmapped/stale selectors.
Exact five-category PlanOnly exit0:5 descriptors/8 methods/21 cases, zero executed.
Breakdown: causal CrossReferences2/Meta2, drift1, typed borrow API4 and only the
three addressed existing owned-peer methods12. Four category budgets60s; owned
peers180s. Plan SHA07441a1a1e44f91ed332c2e5f13235ac59c33e5870408a12fb36fcb81aad1a84.
All source/artifact/resource fences and owned cleanup remain valid; free711737344B
at the post-plan fence, above the unchanged687865856B start reserve.

Storage: separately reviewed lossless substitutions RESERVE2/RESERVE3 preserve all
bytes of38 ended plain logs in verified gzip, with manifests before unlink and
post-operation archive-hash/original-absence checks. RESERVE3:20910838→2340823B,
manifest SHAde47af634018e3a7e4cd9ccbb4c4fd63991a1dba46cb8b4fe862c3e039e3999a.
No TRX/JSON/capture/save/recovery/current-bin evidence was removed. Only proven
reproducible ended obj duplicates/reference outputs were retired; Main/Support
reference inputs remain. The full pre-build managed archive remains preserved.

Independent Astra/high PREP21 accepts cf89f9b04f5ddbb5a46dfc050306714a7ba4d50c
with immediate source/artifact/resource/owner/reserve fences before each run.
Its factual correction distinguishes compiler warning counts from unique
code/message counts; the complete warning multiset comparison remains exact.
Next: execute the17 non-causal
preservation/owned controls separately from the two2-case causal categories;
expected RED is only each final missing-reservation work oracle, never compilation,
setup, data/readset/ordered-issue drift or timeout. Causal/runtime are UNRUN.
No measured benefit, outer automatic wrapper/debt implementation, full browser
retry, native timeout resolution or parent/HOLD closure is claimed. Full raw is
outside checkout under us5-guided-diagnosis/B1RED-*; no generated output is in Git.

# Historical scaffold correction — superseded by B1RED preparation

Initial bf33 SOURCE was NOT accepted. Independent Astra found the always-installed
mutable after-read hook disabled future production eligibility; its other findings
required counting owner-after-close and finite controls for actual borrow/dispose.
All three are addressed in source only: causal mode installs only passive canonical
read/physical counters and requires eligible current-source reuse; mutable callback
exists solely for Meta drift. Scope observation includes opening/end/Dispose and
RequireOwnerAfterClose. Production eligibility/ordinary DATA guards stay unchanged.

Current finite source selection: CrossReferences2 + MetaMisc2 + Meta readset drift1
+ typed borrow API4 + three exact existing owned peer methods×4=12, total21 source
cases/five finite categories (four60s, legacy peers180s). The existing owned-peer
protective budget is preserved; the full browser180s contract is unchanged.
Typed API cases compare actual owned/borrowed Meta work,
refuse second borrow/premature outer close then release and close normally, and
refuse same-root foreign manager/different processing/disposed original borrowing.
Drift1 retains exact inner exception/readset refusal and outer release/currentness.
No blanket historical pin changes, outer production wrapper/debt or remaining
later safety cases. Corrected delta SOURCE is pending; C# UNCOMPILED and every
build/discovery/catalog/PlanOnly/RED/GREEN/runtime remains UNRUN by instruction.
Static source/diff/JSON/finite selector checks do not replace those gates.

Prior coherent source7f461b3658d865099c59ee1ea66ddbcbc1b107e0 was pushed/ls-remote
verified and its10 changed files freshly downloaded via GitHub blob API with exact
blob-SHA/byte equality in /tmp/boe-browser-source-restore-7f461b365. No full checkout
or execution claim. Parent owns final independent SOURCE/integration and runtime.

# Browser source read reservation scaffold WIP — original source preparation

Base af1389065f8aede25abf3ae72875e1c19a3cb58d, tracked
T121-PHYSICAL-GUIDED-BROWSER-READ-RESERVATION / issue#1536. Accepted DESIGN is in
plan.md. Typed actual browser staging calls the existing participating owner,
claim/backup/snapshot/publish producers. Narrow Main-access lookup borrows only
the same manager/original's current ready World reservation; foreign/parallel/
closing use refuses. Nested Meta retains current/full-end/fresh readset proofs
and leaves physical closing to its outer issuer. Ordinary recovery/source DATA
eligibility and the outer production phase wrapper remain unchanged.

Independent per-case Integration fixture authors CrossReferences2 + MetaMisc2
(control order reversed) + exact Meta readset drift1; three finite60s categories
and current selection own these5 source cases. Complete issues/DATA/absence/byte
premises and fresh physical/source counters precede the final intended causal
oracle. No actual RED, measured saving or functional/browser acceptance is claimed.

C# UNCOMPILED; all builds/discovery/catalog/PlanOnly/runtime UNRUN by instruction.
Only static source/diff/JSON checks are permitted before coherent independent
SOURCE. Root owns subsequent reserve/artifact preservation and verification.
No raw/save/recovery/bin evidence touched; external raw remains
/workspace/tboe-linux-evidence-20261025/us5-guided-diagnosis/.
No new cache/collector, quota, ordinary guard exception or GM-authored contract.
Accepted141/9e590 and every prior DFR/Mortal/Windows/native/browser UNKNOWN stay.
Static verification: git diff --check and JSON/category-ID/exact selector/source-row
checks pass (2+2+1 authored). These are not C# discovery or execution evidence.
Ruling: use the explicitly selected clean task checkout as sole writer; no new
worktree or duplicate plan/ledger. Existing plan/checkpoint are the durable ledger.
The direct no-build/no-runtime instruction defers the TDD execution gate to root;
the new causal tests were authored before component/borrow implementation.
WIP bf33ec64c9b565c3ab08f7db7ad1423ecf8f9f8a was pushed and exact remote ref
confirmed. Its10 changed files were independently downloaded via GitHub blob API
into /tmp/boe-browser-source-restore-bf33ec64c and compared byte-for-byte/blob-SHA.
This confirms only those source files, not a full restored checkout/build/runtime.
Final source-only correction keeps observation armed through RequireOwnerAfterClose
as well as physical Dispose; no physical closing work is excluded from the oracle.
Root's separately verified operational findings: P3GREENC14BROWSERRESERVE2 replaced
16 ended plaintext logs (28918030B) losslessly with3223522gzipB; full EOF/CRC and
decoded SHA/archive hashes/original absence checked. External manifest SHA
6de8c75b971018c6c61df08777882c337e8ad0ed8593a82f83996194e86229ee;
free712658944B remains above687865856B start reserve. The full current managed
archive is28445063B, SHA7422665f7c7ca9d88e70d5d1c02b2886bd89d72732faf4ec4650d0284b781a5a.
These are root's operational proofs, not builds/runtime by the scaffold writer.
Review focus: existing broad WorldReadPhase/MetaControlShapes hash-pin fixtures
also pin the changed Meta source and already had3/20 historical pin mismatches at
base. They are outside the finite5 scaffold selection; no blanket repin or PASS
is claimed. Root/Astra must judge affected later guard qualification separately.

# Canonical spiritual diagnosis adapters — independently accepted bounded result

All eight exact canonical requirement kinds are connected: item_quantity,
resource_quantity, consent, facility, location, quest_state, effect_state and
environment (Soul Quest is not an agreement). Operation-owned detached DATA and
closed ordered proofs retain actual realm/actor/locality/source dependencies;
view/cancel remain inert, confirmation/current/original/publication authority fresh.
GM guides, scene schema, worked example, registry/manifest/source guards are aligned.
No new tariff, consumption permission, parallel cache or diagnostic stack.

Root and independent Astra/high accept141 distinct PASS across seven runtime groups:

| Group | PASS | Actual runtime source | Compiled changed owner |
| --- | ---: | --- | --- |
| Eight sources ×two realms causal |16|f79ca2ed05d263e0f5af95c7a9b75dc19249d33e|Unit c576861ea767643be7c01097fa888ac8705f6c52|
| Admission retained successful cases |47|c80a531154373b892c5f8b4287f2f4a9cf8aaf34|Unit c576861ea767643be7c01097fa888ac8705f6c52|
| Source/privacy/stable-tier/GM/Mortal controls |71|d3714547b0bd8f62aa7e450e7fe812732328d429|Unit c576 /Integration f79|
| Exact purpose and signed resource/quest observation |4|ce37ccb2fc8874a8a54e40b67829b8b9195bb01d|Unit6d81da656947a0020563112a32817062d81cabe8|
| Resource no-fee/refusal/publication |1|e6ecd3c3652e4c90226694304c7d4d6c01efef64|Integratione14b07f4d433661f75f1c67b83d7ef8c3ea35bd7|
| Qualifying effect terminal natural removal |1|e6ecd3c3652e4c90226694304c7d4d6c01efef64|Integratione14b07f4d433661f75f1c67b83d7ef8c3ea35bd7|
| Canonical console fresh/stale confirm and atomic rollback |1|e70510c4fb871430cb9ada11cd301be266719bc4|Integration38eb79d3cf4d3cea4948b017d812f50291a73ec7|

This is not one fresh141 or an all-eight native/UI matrix. Main/Support retain fresh
c576 outputs. Latest Unit/Integration builds exit0, exact26/102 unique baseline
warnings; Main12/Support23 baseline warnings. All4PE/PDB pairs,426 source bindings,
27 artifacts,44 resource copies/four daemons verify. Catalogue valid1310categories /
13133methods,zero unmapped/stale; all executed selections match their exact plans.
No successful group is repeated without impact. No skip/timeout underlies accepted
PASS; original text/BOM/unsupported-fee/observer fixture findings remain preserved.

Native resource26.0213885/120s proves fee rejection with unchanged signed/canonical
bytes/no surviving authority, exact ready/absence restoration, then one free real
publication with unchanged full resource definitions/ledger/History. Native effect
25.769966/120s admits its actual active effect, completes terminal natural healing /
removal, and retains exact original/knowledge/natural History and replay. Both prove
exact common output/delete/forgery refusal and actual same-plan image-build count1.
Operation-owned resource parsing/composition and original three-kind fast-path reuse
have real source-work/invalidation controls; no wall-time speedup or lawful changed
player resource quantity is claimed. No DATA cache substitutes for fresh authority.

Console24.2972319/180s,wall31.0084686,primary TRX
de082ee6f970d0fa9388387e7649307647da10f83c8b4d8d08e4e29eaf68314f:
all held/cut History/linked natural/whole-turn rollback assertions pass. Full capture
3221stored/17172decodedB,20originaltimingevents+1message,stored SHA
c5682c28d3abc023caac044caa474d391fd796abff0aa5e4af78458fee3b7747;
EOF/CRC/footer/hashes/pairs/ticks/sequences/aggregates and actual fixture cleanup
independently verified. Final closing fence has no live owner and715235328freeB,
above unchanged687865856start reserve. All prior incomplete physical captures stay.

Raw: /workspace/tboe-linux-evidence-20261025/us5-guided-diagnosis/.
Exact external A2GREENC4-accepted141-union.json SHA
6e43fe7e9a7581df3f0196e1520939fd90e83d60353b8c5383f09203fea8f32d
binds every unique name/testId/executionId, primary SHA and source/owner lineage.
Astra/high accepts source/corrections/PREP/each OUTCOME/union; Sol/xhigh accepts
narrow fixture/mapping/decoder corrections. Independent Sol/xhigh accepts final
canonical text atf064244c67e55113847fd1802b2db412253487d3 with no findings;
this does not request more runtime. Final metadata fence confirms the exact four
text-only paths, all426 source bindings/27pins/44resource copies/fourdaemons,
no live managed owner and711430144freeB; code/binary provenance above is unchanged.

Limits: no new DOM/provider/fresh-process, original physical browser-course or
C9/C10 native UNKNOWN replacement, broad DFR/Mortal compatibility/Windows PASS,
whole FR071/T121/US5/#1536 closure, first-guardian or cross-OS DATA resolution.
No new clone/restore, merge, force-push, branch/issue deletion or raw upload.
Continue the unresolved physical browser-course/native functional acceptance under
its exact canonical requirements, preserving current holds and already accepted code.

# Canonical console observer SOURCE/build complete — one confirming case next

Independent Astra/high accepts38eb79d3cf4d3cea4948b017d812f50291a73ec7;
Sol/xhigh accepts five C4 helper mappings and prior Integration4 preservation.
Fresh Integration-only build exits0 with exact102unique/204logged baseline warnings.
All4PE/PDB pairs,426source bindings,27artifacts,44resource copies/fourdaemons match;
Unit6d81 and Main/Supportc576 remain unchanged. Prior Integratione14 evidence is
preserved11008005B SHAa70285760348ad069fd106a6cbac7efc770cb55970ada05e1003d20bf65c5a0e,
fullEOF/CRC/4member hashes. Ended reproducible obj/ref duplicates matched retained
bins before retirement; no raw/saves/recovery/current-bin removal.

Fresh catalogue valid1310/13133,zero unmapped/stale/execution. Console-only exact
PlanOnly exits0: one descriptor/one case,180s; no runtime yet. Independent PREP and
immediate fresh fence remain required. The exact byte equality at the corrected
observation boundary is still UNPROVEN until that runtime; prior140 PASS and the
complete failed console capture retain their own provenance. No broader HOLD lift.

# Canonical console rollback FAIL — existing boundary observer correction WIP

Runtime05c01dee19989bc05f840e35513b8118aff77222 / Integratione14: console1
FAIL30.3247751/180s, no timeout, exact completed primary TRX SHA
59ac804da37802598bc1132b6d87e674617652bd7b476b9cdb0fb80cdfac721f.
The actual request-consumption fault cut occurred, but the path-specific read hook
never filled compensationActual. Later held/outer rollback assertions did not run;
this is not accepted rollback. Independent Astra/high confirms the observation gap,
not a proven product compensation defect. Do not generalize it to all trusted-local
reads: the existing-file path does call the read hook; absence returns earlier.

Full diagnostic capture is independently byte-checked despite the test FAIL:
3286 stored bytes /17175 decoded bytes,20 original timing events+1 message, exact
EOF/CRC/footer/hashes/pairs/ticks/aggregates/assembly pins and actual root cleanup.
Stored SHA c163c989d9168d6b3811695dd9726ce8822f36fff74ecd8814f44684862efc60.
This complete capture proves diagnostic integrity only. Native2 OUTCOME is accepted;
140 previous distinct PASS retain their own versions and are not repeated.

The canonical-only fixture correction reuses its existing LocalPublicationObserver:
capture protected bytes once on the first following IntentStaged after the cut.
Automatic trusted inner Recover has finished; the following transaction has not
applied a member. This is not claimed to follow all later normalizer compensation.
Keep exact held-image equality, cut History/linked natural result, original fault
and whole-turn restoration assertions. Old three-kind peers retain their old hook.
No new production API/collector/cache, skipped guard or larger limit. Narrow SOURCE,
one fresh Integration-only build and console-only exact PREP remain required.

# Canonical native2 GREEN — source authority survives lawful natural lifetime

Both native singletons run on e6ecd3c3652e4c90226694304c7d4d6c01efef64, compiled
Integration e14, Unit6d81, Main/Support c576. Resource1 PASS26.0213885/120s,
TRX8b0130be41b05d938203028366bf253eefc92e9d04bc3a0b49a55e51be212810;
independent Astra/high OUTCOME accepted. Effect1 PASS25.769966/120s,
TRXbd174b81ff973606de84b7a6cfe443c878b5b1ba8ad0039f9235848f8c22150a;
independent OUTCOME pending. Both exact1/1 descriptors complete, zero skip/timeout /
underfill, exit0, cleanuptrue and all27/44/4 pins/resources/daemons unchanged.

Resource probe proves no fee, no mutation or surviving authority on rejection,
exact ready/command restoration, then free knowledge/natural publication with full
unchanged definitions/ledger/History. Effect proof admits an actual active required
effect, completes terminal natural healing/removal, preserves original diagnosis /
knowledge/natural History and completed replay. Both keep exact common-output,
foreign/missing/forged/delete guards and actual same-plan image-build counter1.
No general player quantity-change, speedup or wider runtime qualification is claimed.
Together with prior138, this is140 distinct PASS on the explicit lineages, not one
fresh140. Console canonical resource confirmation/rollback1 remains UNRUN until
native2 OUTCOME/PREP and a fresh fence, original180s/full capture. Old physical
browser/native UNKNOWN and DFR/Mortal/Windows HOLDs remain separately open.

# Canonical free-resource correction SOURCE/build accepted — runtime PREP next

Independent Astra/high accepts the actual e14b07f4d433661f75f1c67b83d7ef8c3ea35bd7
fixture correction; independent Sol/xhigh accepts five C3 phase mappings and saved
Integration4 evidence. One fresh Integration-only build exits0, exact baseline
102unique/204logged warnings. Main/Support c576 and Unit6d81 stay unchanged. All4
PE/PDB pairs,426 source bindings,27 artifacts,44 resource copies and four daemons
verify. Saved prior Integration4 is11006741B, SHA
040ea05a5b294fd613d79ed43b0778750619a11f3fb2e8eee8fc63177cee993a,
fullEOF/CRC/4member hashes,decoded38993920B. Ended obj/ref duplicates matched before
retirement; current bins/raw/saves/recovery untouched.

Discovery and exact three-singleton PlanOnly exit0 with zero executions; resource1
now names the actual no-fee/refusal proof. Budgets remain resource120/effect120 /
console rollback180 seconds. Runtime all3 remains UNRUN on this candidate pending
independent PREP/fresh fence, with console after accepted native2 OUTCOME. Original
unsupported-fee FAIL and prior138 distinct PASS are retained, not relabelled or
repeated. Production, authority guards and every broader HOLD remain unchanged.

# Canonical resource native FAIL classified — fixture correction WIP

Actual runtime e57cb94fc87cef4e513b9392c9106190d45a6753, unchanged Integration f79:
resource-native1 FAIL7.6805938/120s (no timeout), complete primary TRX SHA
3a9e4481927f3215b76214e97a14fd4daf5ff3900978363fc2129124b49411ee.
Preparation stops at spiritual_diagnosis_cost_forbidden; publication and subsequent
assertions did not run. Independent Astra/high confirms an unsupported fixture
command: FR071 allows no OD/Feathers/items, and an explanatory reason cannot grant
an independent action. Previous SOURCE acceptance missed that earlier admission
guard; generic amount arithmetic was conditional, not admission evidence. No
production defect or diagnostic timeout is claimed. All138 prior PASS stay retained.

The corrected native fixture explicitly probes one forbidden fee: preserve exact
canonical/signed original images and valid ready bytes, require the exact refusal,
unchanged protected roots and no common/effect/wound/open spiritual authority; restore
only authored fee absence/ready bytes under the held lease before one real free
preparation/publication. Complete resource definitions/ledger/History must remain
unchanged, all previous exact common output/delete/forgery/replay assertions stay.
No player quantity-change benefit is proved; non-player automatic currency income
cannot stand in for unified player AP. No new economic or resource producer rule.
Only Integration test/catalog/selection/checkpoint change; no production/cache edit,
new collector or budget increase. Narrow delta SOURCE, preserved Integration4,
one fresh Integration-only build, exact discovery/PlanOnly/PREP are required.
Effect1 and console1 remain UNRUN/held. All broader HOLDs remain unchanged.

# Canonical purpose/observation4 GREEN — native/publication acceptance next

Runtime ce37ccb2fc8874a8a54e40b67829b8b9195bb01d, Unit compiled6d81, other owners
unchanged:4/4PASS, two complete descriptors. Actual authenticated resource absence,
SoulQuest snapshot/decode and old-form fast path pass; corrected purpose refusal
now reaches every zero-draw/canonical-byte/retained-request assertion. Categories
6.8165565/60s and5.4384767/60s, wall20.1617844s,exit0, no skip/timeout/underfill;
cleanuptrue,27pins/44resources/4daemons stable, no owner. Independent OUTCOME pending.
Retained causal16/admission47/remaining71 plus these4 are138 distinct PASS across
explicit lineages, not one fresh138. The original text-only FAIL stays preserved.

Current exact remaining PlanOnly exits0 with three descriptors/three cases and
zero execution: canonical resource native1/120s, terminal-effect native1/120s and
expanded-source console confirmation/rollback1/180s. Require dependent OUTCOME,
independent PREP and immediate fresh owner/artifact/resource/reserve fences before
one sequential execution. No production change, new diagnostics or successful
repeat is proposed. Canonical feature/task acceptance and all broader HOLDs remain
open until their own evidence. All raw, archives and primary TRX stay outside Git.

# Canonical Unit-only correction built — exact four-case PREP next

Independent Sol/xhigh accepts the two fixture lines, finite purpose-category split
and five external C2 phase mappings. Unit source6d81da656947a0020563112a32817062d81cabe8
build exits0, exact baseline26unique/52logged warnings; Main/Support c576 and
Integration f79 remain unchanged. All4PE/PDB pairs,426 source bindings,27 artifacts,
44 resource copies and four daemons verify. Ended reproducible obj/ref copies were
matched to retained bins before retirement. Previous Unit4 evidence is preserved
with fullEOF/CRC/member hashes in4404323B, SHA
1a0829100932eb2f241c22fb4d41fda3b2ce1dacc726bfbf5a0850081079753a.

Catalogue discovery and exact PlanOnly exit0 without execution. The intended narrow
selection is purpose guard1 plus observation3, two descriptors/two categories,60s
each. Runtime remains UNRUN pending independent PREP and immediate fresh fence.
Remaining71 OUTCOME is now independently accepted with exact primary TRXs, pins,
cleanup and selection; causal16/admission47 retain their original versions. No
successful repetition is needed. Native2/console rollback1 remain dependent UNRUN.
The existing full streaming capture verifier is adapted only to the actual canonical
rollback case/root and macro display; independent Sol accepts its full unchanged
EOF/CRC/hash/event/pair/tick/aggregate/cleanup guards. No collector/format change,
quota, timeout increase, product/cache edit or broader HOLD release.

# Canonical remaining71 PASS — two Unit fixture corrections WIP

Runtime d3714547b0bd8f62aa7e450e7fe812732328d429:71/71PASS across six complete
descriptors/five categories, wall44.7738866s, every category below unchanged60s.
No skip/timeout/underfill, exit0, cleanuptrue; pins,44 resource copies and four
daemons unchanged, no live owner. Independent OUTCOME pending. Together with the
retained causal16 and admission47 this is134 distinct PASS across stated versions,
not a fresh134 or accepted canonical diagnosis execution.

Only two Unit fixture lines now change: exact expected generalized purpose refusal,
and existing strict one-optional-BOM decoder around actual snapshot bytes before
unchanged full JSON equality. Zero-draw/canonical/pending byte assertions remain.
The purpose singleton has a finite category split from admission47 so confirmation
can execute only this case and observation3. No production/cache/guard/budget change;
these trivial test assertions need no new reuse. Independent narrow SOURCE and one
fresh Unit-only build required; other three accepted owners are unchanged. Before
replacement preserve the current Unit4 binary/config evidence, then verify all
PE/PDB/source/resources and exact four-case PlanOnly/PREP. Native2/console rollback1
remain UNRUN pending dependent cheap gates. No broader HOLD is released.

# Canonical admission controls — partial OUTCOME accepted, pending cases retained

Independent Astra/high accepts causal16 OUTCOME and the subsequent partial
admission48 result at runtime c80a531154373b892c5f8b4287f2f4a9cf8aaf34:
47 PASS,1 FAIL, remaining71 UNRUN after fail-fast. No timeout/skip; cleanup true,
pre/post pins identical. Admission TRX SHA
1909bd2af4d25574afed42bf76ef249a81cd2064e101d862a5cdb83fbd00e2f0.
The single failure expects the former diagnosis-specific exception text; the current
generalized purpose guard correctly refuses before source reads/dice. Later zero-draw /
byte invariance assertions were not reached, so this case remains FAIL until its
minimal fixture correction and separate confirming execution. Production is unchanged.

Next execute only remaining71 across five categories/six descriptors, admitted by
independent PREP against the saved exact all-plan with unchanged60s bounds and a
fresh source/artifact/resource/owner/reserve fence. Preserve causal16 and admission47
provenance; no passing repeats. Then group the exact-message and already confirmed
BOM-decoder fixture corrections for one Unit-only build and narrow verification.
Native2/console rollback1 remain UNRUN pending dependent cheap gates/PREP. Canonical
adapter feature acceptance and all broader HOLDs remain open.

# Canonical causal16 GREEN — primary evidence verified, OUTCOME pending

Runtime f79ca2ed05d263e0f5af95c7a9b75dc19249d33e / Unit+Main+Support source
c576861ea767643be7c01097fa888ac8705f6c52, Integration f79: all 16 cases PASS,
eight actual source kinds in both realms. Availability and subsequent closed proof,
real request and required-natural program assertions all complete. One descriptor,
16/16 selected/executed, no timeout/skip/underfill, category 6.9133048/60 seconds,
wall 16.0563082 seconds, runner/summary exit0 and managed/runtime cleanup true.
TRX SHA b9e50d764a89978dfc445935a58aa00121cd5d6bcc71d90388e6139519ebf5dc.
Closing fence keeps all 27 pins, 44 resource copies and four daemon copies stable,
no live managed owner; free 785108992 bytes above unchanged start reserve.
Independent OUTCOME pending. This is DATA/admission, not signed feature acceptance.

All four PE/PDB pairs and 426 source bindings pass. Main/Support/Unit retain their
fresh c576 outputs; Integration-only f79 lint rebuild exits0 with exact baseline
102 unique /204 logged warnings. Catalogue discovery valid:1309 categories,
13133 methods,0 unmapped/stale,0 executions. Exact PlanOnly has141 cases across12
descriptors/11 finite categories,0 executions. No broad or old successful test rerun.

Before runtime, a minimal actual .NET8 probe confirms a test-only BOM defect in
PendingTurnSnapshotReaderTests.DiagnosisObservation.cs: raw JsonNode.Parse(byte[])
rejects 0xEF, while the fixture writer emits a BOM. Observation3 remains UNRUN/held;
the existing canonical decoder correction is not yet applied. Next accept causal16
OUTCOME and prepare the independent119 cheap controls; then group fixture fixes and
one fresh Unit build, preserving accepted results without needless repetition.
Native2/console rollback1 still UNRUN pending their dependent cheap gates/PREP.

# Canonical four-project build — zero errors, one Integration lint correction pending

All four managed projects build from c576861ea767643be7c01097fa888ac8705f6c52
with exit0. Main12/24logged, Support23/46 and Unit26/52 exactly match their warning
baseline multisets. Integration has103/206 vs102/204: the sole new xUnit2031 is
Assert.Single(Where(...)) in the new ordinary-cost history assertion. Its correction
uses the same predicate overload; no guard/assertion meaning changes. Only Integration
needs a fresh warning-clean build after narrow review. No runtime or feature GREEN.

Ended obj/ref duplicates were verified against retained current bins before retirement.
The first new Integration4 outputs are also preserved with fullEOF/CRC/member hashes
in11090785bytes, SHA7220b5817721e9961e41017ef6d2c12fb8d1814f0cd121ae02d88a94719f2fd2,
decoded38993920bytes. Earlier full bin/resource archive remains untouched. Next narrow
lint/phase review, Integration-only build, all4PE/PDB/source/resource bindings, exact
catalogue/PlanOnly and independent PREP before cheap runtime. Other HOLDs remain.

# Canonical corrected SOURCE accepted — four fresh builds next

Independent Astra/high accepts correction17abadc66..2cc9ff51c8313512d1295b0e31e37a058a488d63:
all six concrete findings closed in source, same-plan DATA reuse/actual work counter
and finite addressed selection accepted. The earlier cost-burden caveat is withdrawn:
this independent generic action_cost/DirectCost without DerivedAmount preserves1,
not the separate spiritual-conflict burden. Runtime assertions must still prove it.
Four fresh managed builds, discovery and exact PlanOnly are admitted; C# remains
UNCOMPILED and every new GREEN/native/UI/rollback runtime remains UNRUN until PREP.
The prebuild fence confirms remote2cc9ff51c, clean tree, no managed owner,71 preserved
binary/resource paths unchanged and807788544freeB above687865856start reserve.

# Canonical SOURCE correction WIP — six findings, still uncompiled/unrun

Independent Astra/high declines SOURCE17abadc66 on six concrete defects: missing
authenticated source/absence producer-reader integration; incomplete exact write /
required-delete closure including typed owner outputs; absent separate Shining
resident roster; confusable hall customStates admission; mixed-path stabilization
guards; and insufficient ordinary-cost/ready metadata assertions. Same-plan callback
work also repeated unnecessarily. These are implementation findings, not test PASS.

Corrections now use one finite diagnosis observation selector at both real snapshot
producers and a coherent reader whitelist/form. Only new canonical paths add a
client-owned boolean hint; actual kinds come from freshly bound staged request and
selected profile/path. Old-three-kind projection/form/read fast path remains.
Publication retains exact produced paths, required deletes, legitimate unchanged
omissions and existing typed owner-transition application. Stable old-three-kind
facts remain independently checked after natural changes. Same-plan immutable
image reuse follows fresh owner/cursor/fingerprint checks. New controls exercise
real signed resource absence/quest coverage, unchanged old fast path, resident /
container/mixed tiers and one expanded-source console confirmation/rollback journey.
All corrections require delta SOURCE and fresh builds/runtime; no issue/task closure.

Independent Sol/xhigh accepts the five external A2GREEN helper phase mappings:
four fresh owners, unchanged reserve/strict warning/owner/cleanup guards. Current
71 binary/resource paths are preserved as23 deduplicated members in a28456335-byte
archive, SHA9d2500872e4cb67a79c2b9a5d6501bd9497a4d694985d49a97a9877a5eec9702,
decoded92375040bytes with fullEOF/CRC/member hashes. Reviewed recoverable closed-log
substitution ADAPTERS1/2 replaces120 closed plaintext log files; no measurement is deleted:
108079441 original bytes are preserved in12078376 gzip bytes with manifests and
full readback. Free space after archive remains809582592B above687865856start reserve.
All evidence stays outside checkout. No managed build or runtime is currently running.

# Canonical source/execution closure and addressed controls WIP — SOURCE pending

The connected implementation now includes declaration-derived current/original /
persisted dependency sets, all eight closed proof kinds, exact realm-owned scene
validation and GM guidance/examples/registry. The existing common validation callback
binds the actual completed plan to the same live diagnosis owner; publication requires
that same plan/fingerprint and complete relevant output/absence agreement. Resource
owner output DATA is reused by the normalizer after fresh owner/plan checks, avoiding
a second parse/composition there. Original/current/physical/history guards remain.

New controls cover16 changed-source/detached-proof/source-work cases,16 ambiguous /
foreign inputs and8 malformed scene cases. Existing causal16 now additionally checks
actual request and required-natural program DATA after its availability oracle.
Two separate native singleton fixtures target lawful independent ordinary resource
cost and terminal natural removal of a qualifying effect, plus forged/missing common
output refusal and completed replay. New C# and all new outcomes remain UNCOMPILED /
UNRUN pending coherent independent SOURCE, preserved binaries, sufficient reserve,
fresh four-project build, discovery, exact PlanOnly and PREP. No production or feature
PASS is implied. The retained physical/browser and other HOLD boundaries stand.

# Canonical eight-source implementation WIP — uncompiled/unreviewed

After accepted causal16 RED, source code now connects the eight declarations to
operation-owned actual canonical DATA, exact current scene dialect, closed ordered
historical evidence and declaration-derived source/absence inventories. The menu
retains/reuses its read-set under the existing owner; admission performs no IO.
Resource proof reuses the actual definitions/state/history/owner composer once,
not Ink Feathers or a fabricated Mortal context. Existing three-kind proof shapes
and their no-extra-source inventory are retained.

This is an intermediate implementation, not SOURCE/GREEN/feature acceptance.
New C# is uncompiled. The validated common-plan lifetime/publication binding,
negative/freshness/source-count tests, GM schema/docs/examples/guards and addressed
fresh builds/runtime remain pending. No old successful runtime is repeated and
the two full browser-course UNKNOWN results and other HOLDs remain unchanged.

# Canonical causal16 RED accepted — implementation next

Independent Astra/high OUTCOME accepts primary TRX/hashes/all16 unique kind/realm
pairs, final line51 only, complete selection and cleanup/stable pins. This is RED,
not16PASS. The finite scene/source design is accepted for implementation.
The existing validated-plan callback/issuance chain resolves the separate natural
recovery lifetime constraint; exact integration and cheap refusal/transition
controls are recorded at the top of plan.md. No production implementation, new
build/runtime or feature completion yet. Proceed with the same existing owners.

# Canonical diagnosis causal16 — all16 intended final RED; OUTCOME pending

After independent PREP and immediate current fence, runtime6c6a2cf8125cadc5038f5f7ec65d7e79810373fd /
compiled Unitd115e9cf102d34129481e3aa978cde0f95f7eba4 executes all16 unique cases:
8 kinds ×2 realms,1 method/1 nonexclusive Unit descriptor. All16 FAIL only the
intended final choice.Available assertion at line51, custom missing-condition
message. Every bootstrap/mechanics/canonical source/identity/History/H0/read-inertness
premise passes. Category6.3224977/60s,wall14.2179832s, no timeout/skip/underfill/
duplicate IDs. Summary/runner exit1 is expected test failure, not PASS; category
Completed=false reflects that exit, while all16 planned cases/descriptors complete.

Primary TRX SHA40545c38589d04ce8c632e38c8ea2a230759aa8f9e63b5f06affc2afa0457b3b;
external verified-causal-red.json records exact case pairs, unique IDs, final stacks
and stable owning pins. Managed process cleanup succeeds; fresh closing fence
21:42:35UTC has stable27artifacts/resources/daemons, no live owner,746467328freeB.
No new production code, feature GREEN or native/UI/publication acceptance. Independent
OUTCOME pending before implementation; no failed-case rerun solely for this report.

Separate functional lifetime review identifies a required constraint: newly checked
conditions are proved at actual pre-diagnosis admission; lawful natural recovery may
remove a required wound effect and ordinary income may change resource values.
Final publication must bind complete lawful common resource/effect outputs and all
fresh owner/source checks, rather than re-admit against incomplete original fallbacks
or demand that a healed effect remain active. The exact existing common-result binding
is the next bounded DESIGN detail; no guard omission or arbitrary field exemption.

# Canonical causal16 — built, discovered and exactly planned; runtime UNRUN

Accepted coherent SOURCE/correction and operational review preceded one fresh
Unit-only build fromd115e9cf102d34129481e3aa978cde0f95f7eba4: exit0 in70.622648s,
26 unique /52 logged warnings exactly match baseline. Main/Support retainRDRd3,
Integration retainsBROWSERb4. Actual4PE/PDB pairs/421source bindings/27artifact
pins/10resources/40copies/4daemons agree; exact ended obj/ref duplicates retired,
current bin/dependency refs preserved and old Unit4 archive remains verified.

Fresh catalogue validation exits0:1302categories/13122methods,0unmapped/stale,
zero test execution. Exact PlanOnly exits0 in6.5130353s:16cases/1method/1Unit
spiritual-diagnosis-canonical-causal descriptor, unchanged60s. Only these new
causal inputs are selected; no old passing case or build is repeated. Both ended
generic logs remain fully recoverable verified gzip with external manifests.

Fresh21:35:05UTC fence has allpins/resources stable, no live managed owner and
748703744free bytes above unchanged687865856start reserve. Independent PREP must
admit one expected-RED runtime; actual failure must be the final missing diagnosis
availability after every canonical-source/identity/history/H0/inertness premise.
No16result, feature GREEN, signed/native/UI qualification or new production code
is claimed here. Raw remains external underus5-guided-diagnosis/A1RED-*.

# Canonical diagnosis causal16 — coherent SOURCE accepted, build pending

Independent Astra/high reviewed all eight source setups and exact16-row/one-method
Unit selection. The sole concrete finding was the effect identity transition's
missing creation eventRef; it now copies the actual effect chronology as in the
existing fixture, preserving mechanics validation. All resource bootstrap/owner,
quest/relic, exact locality and scene prerequisites remain strict. SOURCE accepts
the scaffold plus that line once frozen here. Causal RED still requires all16 to
reach the final missing-availability oracle; setup/compile/timeout is not RED.

Actual previous RDR d3be1948 Unit four outputs are preserved in a full EOF/CRC/
member-verified 4388432-byte archive, SHA-256
e1b774ca45879d0940719ebd7b8a3535f4b04d5ea016b5b64cb044f1a231edc1,
decoded13926400 bytes. Runtimeconfig is the actual unchanged owner output captured
here; other members also match existing pins. Six helpers only add the A1RED
Unit-only phase/current mixed-owner mapping; operational review pending. Current
outputs remain in place. No new build/runtime or production change yet.

# Canonical diagnosis adapters — existing-API causal scaffold WIP

Independent Astra/high accepts finite DESIGN at71884b1a: exact realm-owned scene
schemas, five existing source-owner mappings, deterministic source dependencies,
closed ordered proofs and fresh lifecycle checks. No author question is needed
for this technical representation; no new access/economic/consent rule is added.

New Unit-only scaffold uses existing CreateState/SpiritualDiagnosisSourceData APIs:
16 cases, eight kinds in both realms. Actual typed bootstrap quartet/owner agreement,
accepted wound effects, unique complete Soul Relic, Soul Quest, exact locality and
proposed closed scene rows precede the final missing-availability oracle. Reads
must leave every input root unchanged. This is DATA/menu causality, not signed
execution or native/feature acceptance. Production code is unchanged. New C# is
UNCOMPILED; coherent fixture/SOURCE, fresh Unit build, discovery/exact PlanOnly and
independent PREP are required before one expected RED selection. Other successful
cases/builds are retained, not repeated. No new browser/performance runtime.

# Eight canonical diagnosis adapters — finite source contract proposed

Independent mapping review accepts the direction and identifies exact owner /
lifetime / deterministic dependency requirements. It confirms that an explicit
schema adaptation inside existing realm-owned locality objects is a technical
choice within the tracked task, without new economic or consent rules. Three
closed spiritual_diagnosis_* row schemas and the exact current container are now
proposed in canonical spec/plan for final DESIGN delta review. No gameplay code,
new store, fixture result or adapter acceptance yet. Other source mappings use
existing Soul Relics, resource authority, local scope, Soul Quests and effects.

# Canonical diagnosis adapters — source matrix / DESIGN proposal saved

No new gameplay code yet. Existing owners are identified for resource quantities,
accepted effects, exact local scope, Soul Quests and player Soul Relics. Three
setting/consent kinds require an explicit review of canonical afterlife placement;
the plan marks this unresolved rather than inventing a store or interpreting aid
agreements as arbitrary consent. Proposed proof/lifetime/invalidation and causal
selection are in the existing canonical plan. All eight adapters remain unchecked.
Next independent DESIGN, then implement source-backed agreed behavior. Current
accepted physical frontier and all browser/DFR/Mortal/Windows boundaries persist.

# Bounded physical course frontier accepted; independent diagnosis adapters next

Independent Astra/high verifies the phase interpretation and precise source-contract
blocker. T121-PHYSICAL-GUIDED-BROWSER-TIMING closes only localization, never a browser
PASS. Both original and instrumented full browser course remain UNKNOWN and retained.
The two browser DATA exclusions and pre-hit pending-publication recovery boundary
stay unchanged; no guard removal, retry, new collector or generic optimization chain.
Current category descriptions now accurately distinguish accepted console1/rollback3
and open browser course; selectors, membership, requirements and budgets are unchanged.
No successful build or test is repeated for this status reconciliation.

The next independent functional task is the already registered eight canonical
spiritual-diagnosis requirement adapters. Source owner/proof mapping and DESIGN
come before implementation; current three-kind route, every fresh source/permission
check, inert reading/cancel and safe-cycle semantics remain. This does not lift any
old HOLD or imply US3/US5/#1536 completion. All current outputs, full/incomplete raw,
retained roots, manifests and verified archives stay external and preserved.

# Browser timing prefix — phase localized; no retry or product change

One reviewed linear readback of the retained instrumented capture completes:
838794938 stored bytes, SHA-256
`30b32863d1016b0c201d509aa52c9aff54439953f067cf00ea8f7b9380a3e9e4`;
15432376 complete timing rows / 7974990180 readable decoded bytes, 0 messages,
0 sequence gaps or pair faults. Gzip ends with EOFError, no report/footer; no full
capture or PASS. Raw and the 231053-byte analysis remain external. Analysis-only
message classification/normal-exhaustion defects were found by independent review,
corrected and reviewed before the sole fold; collection itself never changed.

Instrumented first attempt completes in 115.982112285 seconds; its Finalize.Total
is 102.264281731 seconds. AcceptedValidation.Total is 50.749372310 seconds, with
ResourceAndTreatment 10.575050656 and RefreshCanonicalState 5.867500012 seconds.
Two sequential completed GameState checks in that finalization take 25.102825759
and 25.686159331 seconds. These are phase observations with instrumentation cost,
not original speedup or proof of one subsystem causing the old 110.668 seconds.
Do not sum nested inclusive intervals. First ordinary wait completes in 10.008631
seconds. The second attempt remains open in accepted validation / GameState.

Source inspection finds ReadGameStateDataAsync intentionally falls back when
HasBrowserOriginalOperation is true; 21 fallback scopes / 45.183930 inclusive
seconds appear across the retained prefix. Other eligibility conditions may also
apply; this is a concrete reuse/lifetime candidate, not a demonstrated safe fix.
Do not remove that browser guard, current owner/authority/source/history checks,
change limits or resume parked generic performance work. Next bounded independent
phase assessment and smallest existing-service causal/design proof if justified;
otherwise keep the explicit browser qualification limit and advance independent
agreed functionality. Complete console course and rollback3 stay accepted separately.

The 20:46:28 closing fence confirms all 27 pins/resources/daemons stable, no managed
owner and 762687488 free bytes after verified storage recovery. An optional attempt
to inspect the analysis process's `/proc/639180/fd/0` progress was denied Permission
Error; it was not retried or bypassed and did not block the completed fold.

# Instrumented browser course — timeout / UNKNOWN; captured prefix retained

Runtime `251031dc9650351e52dfe430deb577e5cd2f9908`, actual Integration b4db33c2f
and unchanged other RDR d3be1948 owners. After independent PREP/current fence,
the one instrumented browser course reaches its original category timeout at
180.123566 / 180 seconds, runner wall 189.6645368 seconds. Summary exit124 /
wrapper1, no primary TRX, 0 completed cases. Managed-tree/runtime cleanup succeeds;
fixture completion does not: retained root has 220 files / 1126642 logical bytes.
Frozen hash inventory is external. No browser PASS, completed gameplay FAIL or
retrospective cause/latency claim follows from this instrumented result.

All generated capture bytes remain external, 838794938 stored bytes, no report /
complete footer proof. Prefix analysis is pending. A late growth burst exceeded
the prudent storage estimate: free space fell to 225021952 bytes after timeout,
below the nominal base reserve. The timeout, not ENOSPC, is the recorded stopping
condition; no collection quota or truncation was introduced. No further runtime
or build is admitted from this state. The original start guard was unchanged;
future capture admission must account for this newly measured storage cost.

Under standing authorization, only archived closed plain logs were replaced by
full verified gzip using the unchanged reviewed helper. Eleven additional bounded
batches / 660 files have closure, identity, complete decode, length and SHA proofs;
all manifests/archives remain external. No unique capture/TRX/JSON/save/recovery/
bin or old measurement was removed. Free space restored to 762777600 bytes above
the unchanged 687865856-byte start reserve. Both browser UNKNOWN roots/prefixes
remain intact, as do accepted complete console/rollback results. Next read-only
linear analysis of this existing prefix and independent outcome/phase assessment;
no blind retry, new diagnostic collector or production optimization is admitted.

# Instrumented browser course — independent PREP accepted

Astra/high independently accepts exactly one existing browser-course Fact, one
exclusive Integration descriptor, unchanged 180 seconds, using current b4db33c2f
Integration and retained RDR d3be1948 other owners. Build/warnings, PE/PDB/source,
resources, exact zero-execution plan and storage proofs accepted. Immediate fresh
current fence required before runtime. Unknown intrinsic timing overhead and prior
UNKNOWN remain explicit; no other runtime, optimization or HOLD release admitted.
Runtime is still UNRUN at this checkpoint.

# Browser phase timing — built and exact singleton planned; PREP pending

Fresh Integration-only build from `b4db33c2fd34c1d0c60e6b502de1cf8b18923019`
exits 0 in 125.914138 seconds. Existing 102-warning / 204 logged code-message
multiset is identical. Main/Support/Unit retain unchanged RDR d3be1948. Actual
four PE/PDB pairs, 420 source bindings, 27 artifact pins, 10 resources / 40 copies
and four daemons all match. Only exact ended obj DLL/PDB and duplicate ref outputs
were retired after verification; current bin and dependency refs remain intact.
Preserved actual previous Integration102dcf7f archive remains fully recoverable.

Complete catalogue 1301 categories / 13121 methods is reused because the only
executable delta is one existing hook assignment: no test signature, attribute,
selector, member or project changes. Current exact PlanOnly exits 0 with zero
execution: browser full course Fact, one exclusive Integration descriptor, one
case/method, unchanged 180 seconds; plan wall 8.5285231 seconds. Its ended generic
plain log is replaced by exact verified gzip, manifest SHA-256
`0725d7fb125a7094922066181f50ab6a0228f38b321b9f168d2645cc2da40c13`.

Fresh 20:38:10 UTC fence has unchanged pins/resources/daemons, no live managed
owner and 1065955328 free bytes, above the unchanged start reserve. Independent
PREP must evaluate one instrumented diagnostic journey; its extra intrinsic timing
cost remains unknown and there is no original performance/cause or browser PASS
claim. Immediate current fence is required after the checkpoint before runtime.
All earlier UNKNOWN/root, full console/rollback and wider HOLD boundaries remain.

# Browser phase timing — SOURCE and build preparation accepted

Independent Astra/high accepts rollback3 OUTCOME and the exact one-line browser-only
passive hook at `7f36e289cfb2364019e983efdb54db4123dacc60`. Existing recorder/transport
proofs and direct wiring review suffice; no product or console/rollback assertion
changed. The new code is still UNCOMPILED and no instrumented runtime is admitted.

Operational review accepts all six phase-helper deltas, unchanged owner restrictions,
source/resource/pin fences and reserve. Actual Integration102dcf7f four artifacts
are preserved in a full EOF/CRC/member-verified 11004086-byte archive, SHA-256
`47546c31234f6cbc0f934e55c2e32edd020446599f163f8e5d9087cbd53c416a`, decoded
38983680 bytes. The runtimeconfig has the unchanged prior RDR hash; other members
match the actual MENU proof. All current output files remain in place before build.

Under the standing recoverable-cleanup authorization, four bounded batches replaced
240 old closed plain logs by independently verified gzip: 412525516 original bytes
remain exactly recoverable in 46188556 bytes. Every closure/exit/hash proof and
manifest is retained externally. No capture/TRX/JSON/save/recovery/bin or unique
result was removed, no old measurement retired. Free space is 1066819584 bytes;
this is prudent headroom, not a guarantee of future capture completeness. No
quota/truncation or reduction of the unchanged 687865856-byte start reserve.

Next fresh Integration-only build, exact unchanged warning baseline, current
PE/PDB/source/resource bindings and exact browser singleton PlanOnly, then PREP.
Main/Support/Unit remain unchanged RDR d3be1948. Prior console/rollback/clock PASS
are retained on their actual lineages, not repeated. Browser course remains
UNKNOWN; no production optimization or whole-feature/HOLD closure is inferred.

# Publication-cut rollback3 — 3 PASS; minimal browser timing WIP

Runtime `3d422b59bc085fdb54583716273b7dfac005a0af`, unchanged mixed qualified
owners. Exact original-budget category completes 3 PASS / 1 method / 1 exclusive
Integration descriptor in 86.6806018 / 180 seconds; wall 96.0270196 seconds. No
failure, skip, underfill or duplicate IDs. Genuine procedure/course/guaranteed
publication cuts after selection consumption verify exact held compensation,
linked History/terminal identity and unchanged turn plus full original restoration.
Primary TRX SHA-256 `cc5feb4678b80f3e0c6a04761a8b2322b0397e52859fdea784283ffad494c955`.
Each of three complete captures has 12 original events + 1 full message, verified
EOF/CRC/hash/pairs/sequences/closed footer and actual root absence. Closing fence
20:30:44 UTC has all pins/resources/daemons stable, no active managed owner and
712900608 free bytes. External raw: `P3GREENC14COURSE-rollback3`. Independent
OUTCOME pending; this does not close the browser course or whole physical feature.

Accepted bounded DESIGN for the unresolved browser-course cost is now tracked in
tasks/plan. Test-only WIP adds one existing sealed passive accumulator hookup only
when browser=true; no product, GM, cache, callback, assertion, budget or console/
rollback execution change. Existing intrinsic synchronous timings also activate;
volume/overhead remain unmeasured. No assignment-mirroring test is added; existing
recorder/transport proofs remain on their actual earlier versions. Current new
C# is UNCOMPILED, SOURCE/PREP and any instrumented runtime pending. Preserve current
Integration artifacts and obtain prudent storage before build/full capture. No
repeat of accepted console/clock/rollback controls solely for this observation.

# Physical publication-cut rollback3 — PREP accepted, runtime UNRUN

Independent Astra/high accepts one existing `mortal-physical-guided-rollback`
category: procedure/course/guaranteed, 3 cases / 1 exclusive Integration descriptor,
unchanged 180 seconds. Exact current PlanOnly is zero-execution; qualified mixed
binary owners, cut/compensation/whole-original oracles and COURSE helpers remain
unchanged. Course cut is its first genuine milestone and needs no wait. The fresh
20:28:09 UTC fence proves all artifacts/resources stable, no live managed owner,
and 715083776 free bytes above the unchanged start reserve. Immediate current
fence required after this checkpoint. No passing selection/build is repeated.

Browser-course evidence and retained inventory were independently verified as
UNKNOWN, with no basis for a repair-loop attribution. Existing passive hook wiring
could expose disabled Engine phase timers, but also adds nested intrinsic timing
cost; no implementation or new diagnostic runtime has been admitted yet. Neither
that investigation nor browser acceptance is required for these independent cuts.
All raw/captures/saves/unique evidence remain external and retained; only closed
plain generic logs were substituted by fully verified recoverable gzip.

# Browser course — timeout / UNKNOWN; console course remains accepted

Actual runtime `40eb4d66536f883fa7609df08f5b0102963bae27`, same qualified mixed
binary owners (Main/Support/Unit d3be1948, Integration 102dcf7f). After independent
console OUTCOME/browser PREP and immediate unchanged fence, one exact browser
course exceeded its original 180-second category budget at 180.2188051 seconds;
runner wall 191.1703857 seconds, summary exit124 / wrapper exit1. No primary TRX,
0 completed cases, no browser PASS or gameplay FAIL attribution. Managed-tree/
runtime process cleanup succeeded; fixture cleanup did not complete and its
root is retained unchanged. The runner's generic lane footer says 8 minutes,
but the actual descriptor and CategoryResults enforce the original 3 minutes.

Full generated prefix remains external in `P3GREENC14COURSE-browser1`: 1536 stored
bytes, SHA-256 `aa5a4b49bf0fe12562a2a74da07aca8061156263018107f634373df3ff9cef7e`,
14 complete timing events, incomplete gzip/no report/footer. It proves first
confirmed attempt END at 110.668285659 seconds, first ordinary wait END at
10.295985196 seconds, then second confirmed attempt BEGIN and GM-response END.
The last attempt is open; no completed three-stage result is inferred. Console
same first attempt was 28.619470418 seconds; this difference is real, its cause
is not yet established. Do not sum overlapping intervals or blame a subsystem
without evidence, change budgets, or blindly repeat the browser case.

Retained root `/tmp/boe-mortal-physical-journey-50455239d973497bbb61b16229211972`
has 220 files / 1126656 logical bytes; external frozen file/hash inventory is
`timeout-prefix-retained-root-proof.json`. Unique prefix, root/save, TRX/JSON and
all earlier evidence remain intact. Console complete three-stage course remains
independently accepted separately. Next bounded read-only localization through
existing named timings/consumers and independent review; publication-cut controls
remain an independent pending functional qualification, not a browser PASS.
All broader HOLDs and DOM/provider/fresh-process limits remain unchanged.

# Console course accepted — browser singleton PREP accepted, runtime UNRUN

Independent Astra/high OUTCOME accepts the complete console course at runtime
`494049134a78995bef996c72946dea2212ca23c5`: primary TRX, exact one-case selection,
all 28 original events / 14 pairs, sequence/PID/ticks and operation aggregates,
zero-message counters, actual assembly hashes, complete closing and actual root
absence were independently checked. The three milestones, two accepted waits,
separate current-menu confirmations, costs, wound, History and replay all remain
in the executed test. The prior result receipt is accurate and remotely saved at
`0460d6c5946f74654aafba3b8a50778e004b3a91`.

The same independent review accepts exactly one next shared browser-handler/queue
course: `BrowserPhysicalCourseSeparatelyConfirmsEveryMilestoneAfterAcceptedWaits`,
1 Fact / 1 exclusive Integration descriptor / unchanged 180 seconds. Current exact
PlanOnly passes with zero execution in 7.0245377 seconds. Existing qualified binary
owners, original course oracles and reviewed COURSE helpers remain unchanged;
no rebuild or repeated PASS. Immediate fresh source/resource/owner/reserve fence
is still required before this actual runtime. Browser course remains UNRUN here.

The two ended generic plain logs (console runtime and browser plan) were replaced
only by full EOF/CRC/hash-verified recoverable gzip, independently checked. Their
manifests are external; every capture/TRX/JSON/save/root/archive/bin is retained.
The 20:20:57 UTC fence has unchanged 27 artifacts, resources and daemons, no active
owner, and 715907072 free bytes above the unchanged 687865856-byte start reserve.
Browser DOM/provider/fresh-process, rollback3, whole feature and all prior HOLDs
remain open. No other runtime is admitted by this bounded PREP.

# Full console course — 1 PASS, independent OUTCOME pending

Runtime `494049134a78995bef996c72946dea2212ca23c5`; actual Main/Support/Unit
remain compiled from `d3be1948e50ebbaccc9fe088501bbccdf52fb68b`, Integration from
`102dcf7f524c41dbbf70c8fd4efa77e10b855671`. Accepted independent metadata/helper/
PREP and the immediate unchanged source/resource/owner/reserve fence preceded
exactly one full console course. No build or previously passing case was repeated.

`ConsolePhysicalCourseSeparatelyConfirmsEveryMilestoneAfterAcceptedWaits` passes
1 case / 1 method / 1 exclusive descriptor, 0 failed/skipped/underfilled/duplicate
results. Category 156.5860411 / 180 seconds, wall 164.2433656 seconds. The actual
Engine journey verifies three separately confirmed treatment milestones, both
ordinary accepted waits, TooEarly inertness, newly opened current menus, unchanged
wound bytes during waits, exact declared item costs, persistent course identity,
terminal wound result, History, exact replay and ordinary owned cleanup. This is
a complete console course, not an extrapolation from the clock component tests.

Primary TRX SHA-256 `fe88dfba4d7ea00740b040abfcffbd577f2d933c15b08fe7b6063e047a6b9cbd`.
Full passive capture has 28 original timing events, complete pairs/sequences,
closed writer/footer and exact EOF/CRC/decoded proof. Stored SHA-256
`31465df1996d3b972f6c20fb8c0e91555b7d8b9bf38677ca4af67e4a5f3034f8`;
decoded SHA-256 `94ae28a10571b37b6098810291bf992b5e686a9ad623ee4c21fd8196048f19ab`.
The actual owned fixture root is absent; closing fence 20:19:53 UTC confirms all
27 artifacts, resources and daemons unchanged, no active managed owner, and
714608640 free bytes above the unchanged 687865856-byte start reserve.
Raw remains outside checkout under `us5-guided-diagnosis/P3GREENC14COURSE-console1`.
Old timeout/prefix/root remain preserved and are not relabeled PASS. No speedup
claim, broader HOLD release, browser-course/rollback or whole-feature acceptance.
Independent OUTCOME pending; next exact browser-course plan/PREP and one original
180-second journey, followed by the remaining functional publication-cut controls.

# Full console course — exact current preparation, runtime UNRUN

C14 closure/status is remotely saved atb0b0d8e0433f3e787b5e0f3d7a3088701095d64e.
Qualification-only COURSE phase retains Main/Support/Unitd3be1948 and Integration
102dcf7f; no executable source changed and no successful build was repeated.
Fresh actual four PE/PDB pairs,420 source bindings,27artifacts,10resources/40copies
and4daemons match. Current exact PlanOnly passes1case/1method/1exclusiveIntegration
descriptor: ConsolePhysicalCourseSeparatelyConfirmsEveryMilestoneAfterAcceptedWaits,
existing180s category, zero execution. Complete catalogue1301/13121 remains
unchanged; no selector/attribute/test membership changes. Three existing helpers
only add this phase/mixed-owner mapping; independent delta/PREP pending.

Whole preparation evidence reuses the reviewed complete Engine fixture and C13
controlled response, actual new33-distinct clock gates and read-only retained
first-wait original eligibility. Old root remains221files/900395logical bytes,
unchanged and preserved; no new root/save or result is synthesized. Current
original must still be produced normally; old fixture data/binaries are not reused.
Full current capture and cleanup remain mandatory, no event/byte cap or budget
increase. The fresh fence20:10:16 UTC has allpins stable, no live managed owner
and716697600 freeB > unchanged687865856 reserve. The completed plan's plain log
is fully recoverable gzip; no raw/capture/TRX/JSON/save/recovery/bin removed.

Next independent status/helper/PREP, immediate unchanged fence and one genuine
console-course runtime. Its pass must prove all3 separate confirmations, both
ordinary waits/normal cleanup/current menus, exact declared costs, final result/
History and replay. Browser course/remaining rollback cases and whole-feature
acceptance stay open; old course UNKNOWN and all broader HOLDs remain preserved.

# C14 ordinary clock accepted — next full console course

Independent Astra/high final OUTCOME accepts remaining7 and complete native
capture at runtime29a55ce5/Integration102dcf7f, with retained reader19@2023bbdb
and native7@4a71e70a. T121-PHYSICAL-GUIDED-ORDINARY-CLOCK closes only this
33-distinct bounded dependency, not a single fresh33, whole course, physical
feature or epic. All production Main instances ared3be1948. Canonical spec/task/
plan status now reflects this accepted state; no game rule or executable source
changes in this status update. Earlier errors/UNKNOWN/raw remain preserved.

Next one console complete three-milestone course, using current qualified bins
and C13 ordinary GM wait channel, existing180s category. Retained original turn44
eligibility is confirmed read-only as below. Current exact PlanOnly/resource/PREP
must precede runtime; old course root/raw will not be overwritten or deleted.
Browser course and remaining rollback journeys follow by dependency; all other
HOLDs/Windows and DOM/provider/fresh-process limits stay explicit. No unchanged
passing case or build is repeated solely for this status change.

# C14 remaining7 —7 PASS; final OUTCOME pending

Runtime29a55ce581ba940d296cf706c155d5e0a9725c01 / compiled Integration102dcf7f,
Main/Support/Unitd3be1948. After independent PREP/current unchanged fence,
remaining7 completes7PASS/0FAIL/0skip,7cases/6methods/4descriptors, no timeout,
underfill or duplicate IDs. Causal37.7944903/60s (actualcase8.2139440s), docs
10.095422/60s, portable committed rollback97.2739513/180s; wall209.9383038s.
The actual original/clock/repeated intake/progression/repeated controls/next-menu
roundtrip now passes in full, including initial pre-original menu/JSON-inertness.
Both spiritual original C4 and Mortal recovery later-write exact rollback/retry
controls pass. This does not relabel the older three-case Mortal compatibility
HOLD, full Engine course, browser DOM/provider/fresh-process or whole epic.

Primary causal TRX SHAa77be5686b4e567145e02459941e470e5dbf3d89376b2a4c9a70fa4d2e6130b4;
all four primary TRXs/unique IDs/actual selection/cleanup are in external
P3GREENC14MENU-remaining7/verified-outcome.json. Its complete native capture is
16846824B, decoded153885334B,304854 timing events+1full message, storedSHA
ef1d3b9f53aee4b1d66da8a3537abb9aae924987e68c1f97099d4e1c859264c1,
decodedSHAa7393257936a3429216b18cfdddd1d6b427cc3c9db5544398bd80b81c81103c8.
Full EOF/CRC/hashes, original PID/tick/sequence pairs/every operation aggregate,
closed writer/footer, no late/open scope and actual root absence are verified.
Named inclusive sections: setup4138.148296ms, raw1223.938512ms, repeat319.459825ms,
progression57.745264ms,nextmenu145.515801ms,closing2.219065ms. Do not sum overlapping
intervals or claim speedup against the earlier setup/different incomplete work.
Closing fence19:59:52 UTC: all27pins/resources/daemons stable,noowner,716959744B free.

C14 frontier is33 distinct PASS across explicit runtimes: reader19 at2023bbdb,
unchanged native7 at4a71e70a, remaining7 at29a55ce5; not one fresh33. All production
Main instances ared3be1948; Integration differs only by reviewed causal readiness
fixture. Old failures/partials/raw remain preserved. Final independent OUTCOME
pending; only then mark bounded clock task accepted and prepare one full course.

Read-only audit of retained original full-course failure root confirms that the
actual first ordinary wait is within this clock correction's domain: turn44,
Mortal World, no diagnosis/treatment/spiritual hints, no original/current wound
command or pending resolution, full five-field original clock and control both
1260. No retained save/root was modified. This is eligibility evidence, not a
new accepted course or permission to reuse old binaries. Full course still needs
current exact plan/resource/PREP and original180s bound; no blind retry.

# C14 causal menu premises — built; remaining7 PREP pending

Independent Astra/high SOURCE accepts102dcf7f524c41dbbf70c8fd4efa77e10b855671,
including exact initializer extraction, causal-only pre-original readiness,
unchanged producer/final oracles and retention of reader19/native7 results.
Fresh Integration-only build exits0 in126.536934s, baseline102 warnings/204logged
unchanged. Main/Support/Unit retain fresh unchanged d3be1948. Four actual PE/PDB
pairs,420 source bindings,27 artifacts,10resources/40copies and4daemons agree.
Only exact ended obj/ref duplicates retired; current bin/dependency refs stay.
Previous actual Integration4 preserved:11003326B archive SHA
d49cb49fd6ad1d20d02799c2262851a9a597c36bd2b5f74a976668d75e80a3e7,
fullEOF/CRC decoded38983680B/all4member hashes; independently verified.

Current exact PlanOnly passes7cases/6methods/4descriptors: causal1 +docs3 +portable
rollback3, existing60/60/180s category bounds, zero execution. Complete catalogue
1301/13121 is reused: no test signature, attribute, selector or member addition;
only private helper/body and the causal category's responsibility text changed.
Independent PREP must confirm this bounded reuse. Reader19 and unchanged native7
stay accepted on their explicit prior versions; no repeated passing selection.

Standing recoverable-log authorization supplied necessary reserve:40 old closed
plain logs from20ended runs replaced by full verified gzip,74855366→8367021B,
manifest SHAdcacec74dd13fbdd617a5101f2bf9e2030736293fafb3a2f56f14b9f8664c758;
independent full restoration/closure proof accepted. No capture/TRX/JSON/saves/
recovery/bin/unique evidence removed and no old measurement retired. Current plan
log also remains fully recoverable gzip. Fresh PREP fence19:54:17 UTC has allpins
stable, no live owner and738820096 freeB > unchanged687865856 reserve. Raw/proofs
remain external under P3GREENC14MENU-* and their manifest-listed old run paths.

Next independent PREP/current fence/one remaining7 runtime and strict full
capture/primary TRX/cleanup/OUTCOME. No whole causal GREEN or full course result
is claimed by compilation. Full course and broader HOLDs remain unadmitted.

# C14 clock14 attempt —7 PASS, final menu fixture FAIL,6 UNRUN

Runtime4a71e70a766d31f2856c14b874c796bc02e4112d / all compiled ownersd3be1948.
After accepted reader19 OUTCOME and immediate unchanged fence, actual afterlife2
and ownership5 all PASS, including every source corruption and exact failed-write/
postcommit compensation/foreign-drift control. Causal1 reaches numeric1740 from
original1260+480 with empty issues, preserved calendar and original/unrelated
bytes, no command/double delta, repeated raw admission, actual progression apply
and repeated next controls1740. It then FAILs only the final public /лечить menu
WoundCommandState IsType(null). Whole causal case remains FAIL, not GREEN; docs/
rollback6 remain UNRUN. No timeout: category9.3447551/60,20.5695817/60,10.7208746/60;
wall58.2732592. No full-course admission or whole14 PASS is claimed.

The native component fixture lacks the non-target display carrier roots required
by WoundPlayerReadService. Its complete accepted-state graph supports calculation,
not the broader five-carrier public projection. Existing accepted PhysicalGuided-
Menu initializer supplies exactly those missing roots before resource bootstrap.
Independent bounded design confirms the remaining identity/history/effect/skill/
provider/location/item/resource initial graph is already supplied. Test-only WIP
extracts that exact missing-only initializer, applies it only to causal1, and
adds available-projection/accepted-effects/public-menu/JSON-inertness readiness
BEFORE the unchanged sole original producer. The final postwait menu and clock
assertions stay unchanged. No production guard or fixture byte is patched after
acceptance; seven PASS fixtures keep their unchanged producer and preparation.

All six captures are full and verified:1415542 timing events,77530038 stored
bytes,712134097 decoded bytes, original sequences/PID/tick pairs/all aggregates,
EOF/CRC/hashes/footer, actual assemblies and root closing/absence. Primary strict
TRX/IDs/observations/cleanup are in P3GREENC14RDR-controls14/verified-partial-outcome.
json. Their clock and partial assertions do not convert the final FAIL to PASS.
Next test-only delta SOURCE, preserve actual Integration4, fresh Integration build
and exact causal1 plus remaining6 plan/PREP. Retain reader19 and seven unchanged
native controls; no new Main rebuild or successful case repeat without impact.

# C14 reader19 —19 PASS; OUTCOME pending before clock14

Runtime2023bbdb515da59b57ab78d46efbb48165f65444 / all compiled ownersd3be1948.
After independent PREP and fresh unchanged fence, reader19 completes19PASS/
0FAIL/0skip,19cases/7methods/1descriptor, no timeout/underfill or duplicate IDs.
Category8.7277618/60s,wall16.4900401s. Genuine explicit lease with suppressed
ambient flow accepts valid bytes and refuses missing manifest/authority/original/
current context without nested acquisition. Actual declared DATA priming and
passive source/hit counts pass; selected drift and unrelated A drift still make
the owning operation reject, while valid narrow B remains independently readable.
Detached bytes and observed/default failure-priority controls also pass.

Primary TRX SHA28c40f01604fdb76998c382804b6ca071cf6248a247dc5d2b8ed31fddc91ef53;
external P3GREENC14RDR-reader19/verified-outcome.json records exact completed IDs/
selection/cleanup. Closing fence19:32:56 UTC: all27pins/resources/daemons stable,
no active owner,762146816 free bytes > unchanged687865856 reserve. Full raw is
outside checkout; no new native full-capture qualification is claimed by Unit19.
Independent OUTCOME remains pending. Controls14 PREP is conditional on its
acceptance plus immediate unchanged fence; controls14/causal clock/full course
have not yet rerun on this source. Prior6PASS/1readerFAIL/7UNRUN remain historical.

# C14 non-retiring reader candidate — built; reader19/clock14 runtime UNRUN

Independent Astra/high SOURCE accepts d3be1948e50ebbaccc9fe088501bbccdf52fb68b.
The default reader keeps its actual explicit lease and uses the existing
non-retiring cache bypass, preserving all pending dependencies. Observed source
branches and validation/hash/presence/failure ordering remain unchanged.
Four fresh Main/Support/Integration/Unit builds exit0 in
134.009042/15.089804/127.829664/65.766417 seconds. Warning multisets exactly
match baseline12/23/102/26 (24/46/204/52 logged occurrences); no new warning.
Four actual PE/PDB pairs,420 source bindings,27 artifacts,10resources/40copies
and four daemons agree. Only verified ended obj/ref duplicates retired; current
bin evidence and Main/Support dependency refs stay. All four compiled ownersd3be.

Official complete inventory passes1301categories/13121methods, zero stale or
unmapped selectors and zero execution. Actual exact plans: reader19 is19cases/
7methods/1Unit descriptor/60s; controls14 is14cases/10methods/6descriptors/
5categories with existing60/60/60/60/180s bounds. Both are PlanOnly, zero execution.
The former must pass before admitting the latter in this same source epoch.
Prior6PASS/1readerFAIL/7UNRUN remain a separate old runtime; shared reader impact
justifies this finite recheck, not any broad69/perf/compatibility/HOLD rerun.

Previous actual mixed32 preserved and independently read back before overwrite:
63545522B, SHA96339a4bcf63dd5d4737766b10a40bba160d6b50c8dfb5664126224297a6d3cd,
fullEOF/CRC decoded198912000B, exact32 member hashes and correct b36/cf provenance.
Under existing autonomous closed-log authorization,60 additional old plain logs
from30 completed runs were losslessly replaced by verified gzip, preserving
115390193 original bytes in13865468 bytes. Frozen identity/closure/hash/restoration
manifest SHA42267908baf545e3763b5401259d81bae42c45448116031ee5e906a77dc0d301;
independent readback accepted. No capture/TRX/JSON/save/recovery/current binary or
unique evidence removed, no older measurement retired. Four newly closed generic
logs similarly retain full recoverable gzip. Raw/proofs all remain external in
us5-guided-diagnosis/P3GREENC14RDR-* and the old run roots named by the manifest.

Fresh closing PREP fence19:29:47 UTC: all pins/resources/daemons stable, no live
managed owner,764297216 free bytes > unchanged687865856 start reserve. Next
independent PREP and immediate fence for one reader19; only on its full accepted
outcome and a new unchanged fence may controls14 run. Full course and broader
HOLDs remain unadmitted. No new runtime PASS, speedup, DOM/provider/fresh-process
or whole-feature completion follows from these builds and plans.

# C14 reader freshness review correction — WIP UNCOMPILED / UNRUN

Independent SOURCE found the first fresh-scope choice unsafe: ForceFreshRecovery-
Validation retires the surrounding whole-operation touched/key set before it has
settled an unrelated consumed source. It is replaced before any compilation/run
with existing non-retiring SuppressValidationReuse, active whenever the source/
validation owner can supply cached DATA. This bypasses physical reader cache
selection without dropping the owner's pending dependency/failure obligation.
No new suppression/cache framework or source-session behavior is introduced.

The two selected-drift rows now require the still-owning source operation to fail
on completion; a new primed unrelated-source row requires valid narrow snapshot
read B followed by owner.VerifyAsync rejecting changed A. Real passive source
counts and hits prove the priming; current files remain unchanged by the reader.
Proposed cheap category is now19 cases/7methods (8new), all UNCOMPILED/UNRUN.
This supersedes the earlier WIP description of retiring surrounding DATA. The
accepted native missing-original failure and all6PASS/7UNRUN remain unchanged.
Next final bounded SOURCE, preservation/builds/exact discovery/PlanOnly/PREP.

# C14 reader lease/freshness correction — WIP UNCOMPILED / UNRUN

Following the genuine missing-original failure and independent Astra DESIGN,
the existing default reader now consistently passes its actual held lease to
manifest/authority/current-context/selected-original/rollback-artifact physical
reads. No existence-precheck workaround or nested acquisition is introduced.
The existing ForceFreshRecoveryValidation scope retires provisional source and
recovery DATA only for the default authority-bearing protocol; observed-session
branches, their transport/lifetime, parse/hash/presence checks and failure order
remain unchanged. No new cache/API/presence schema or game/GM rule applies.

Seven new small typed rows prove valid and missing sources without ambient
execution-context flow, and real existing declared source hits followed by raw
physical drift/absence. Passive source counters/timing DATA (no callback) require
that the priming actually hits; fresh reader must still refuse changed bytes.
Together with existing detached/default/observed/priority controls, the proposed
Unit category is18 cases/6methods, protective60s; actual discovery/PlanOnly pending.
Then the shared-reader change justifies rechecking the affected controls14; no
other unchanged accepted case, broad69, course or HOLD is automatically admitted.
The native sourceguard failure is retained causal evidence; no redundant RED run.

Optimization: existing whole-operation DATA owner remains outside the fresh
reader protocol; existing fresh scope invalidates it at the actual authority
boundary. Key/namespace/lease/source checks remain physical there. Integrity
cannot reuse provisional bytes as permission; this is not a measured speedup.
GM/example update not needed for this internal reader repair: the already updated
ordinary wait emission/client publication/recovery contract is unchanged.
Next coherent SOURCE (including busy/source-lifetime and finite selection),
preserve actual needed binaries, fresh affected builds and exact current plans/
PREP before any runtime. All new source/tests remain UNCOMPILED/UNRUN.

# C14 finite runtime —6 PASS,1 reader failure,7 UNRUN

Runtime544b5863d3b0d6e6ca04e52091cdeb22f771543a, compiled Integrationcfbae21,
other three ownersb36df4f. Exact admitted14 stops after7 complete cases: afterlife2
PASS, malformed/null command control1 PASS, publication before-write/known-after-
commit/foreign-clock controls3 PASS; sourceguard1 FAIL,7remainingUNRUN. No timeout,
wall55.9385199s; category10.1510467/60 and29.642078/60. No causal clock GREEN,
complete14 or full course is claimed. Primary TRX/IDs/selection/closing proof is
P3GREENC14FIX1-controls14/verified-partial-outcome.json.

The source guard first correctly rejects original drift, then deleting the signed
selected original triggers IOException after a nested gm-main-owner.lock acquire.
PendingTurnSnapshotReader.cs388 uses an unleased public sync read despite its
explicit held lease; a missing file takes the public quiescence fallback and
tries to acquire the same main ownership again. This is an actual negative-path
reader defect, not clock arithmetic, fixture setup, policy refusal or harness
timeout. Independent Astra DESIGN accepts consistent existing held-lease reads
through the default core/context/callback, preserving all validation/ordering and
sourceSession branches. Existing fresh-authority boundary is required so those
explicit reads cannot substitute provisional SourceDataOperation DATA.

All five native captures, including the failing case, are complete and verified
by the accepted linear readback:1107806 timing events,60336700 stored bytes,
557106669 decoded bytes, all original message/sequence/PID/tick pairs and every
aggregate, EOF/CRC/hashes/footer, actual assemblies, closing and root absence.
The primary test error is separate from diagnostic completeness. All raw and
prior failures stay external. Next bounded default-reader lease/freshness fix,
cheap explicit-no-ambient/primed-source/default-vs-observed controls, SOURCE,
fresh affected builds/plan/PREP and causal sourceguard GREEN. Shared reader impact
justifies revisiting affected prior6 PASS; no broad69/perf/HOLD retry is admitted.
No clock-source guard will be weakened to hide missing original evidence.

# C14 original preparation corrected — Integration rebuilt;14 runtime UNRUN

Independent Astra/high SOURCE accepts cfbae21aacbbe8a42098b2f43f7e3c9e403be614:
the existing standalone fixture preparation now signs actual scheduler/control
and authenticates its exact schedule plus active conflict before the current
test action. No production guard/assertion/budget changes. Fresh Integration-only
build exits0 in126.943046s, baseline102 warnings/204 logged occurrences unchanged.
Main/Support/Unit retain their unchanged fresh b36df4f owning builds. Four actual
PE/PDB pairs,418 source bindings,27 artifacts,10resources/40copies and4daemons
match. Only ended exact obj/ref duplicates retired; current bin and dependency
refs stay. Previous Integration4 preserved:11002005B, SHA
cc36abc8ff65804f0937f0f4dbe3ac0d7652818a4e0afecb387e8df4e5e66f61,
fullEOF/CRC decoded38973440B and all four member hashes, independently approved.

Current exact PlanOnly again passes14cases/10methods/6descriptors/5categories,
zero execution. Complete catalogue1300/13118 is retained from b36df4f: catalogue,
test signatures/attributes/selectors are unchanged; only the existing method's
initial producer body/imports differ. This bounded inventory reuse requires PREP
confirmation, not another successful full discovery without an affected reason.
The previous two fixture failures and12UNRUN remain distinct, no clock GREEN.

To preserve the unchanged reserve and complete future output, six older closed
plain runner/dotnet-test logs from live-n-T586 and two dfr-prefix attempts were
replaced by fully decoded/hash-verified gzip copies under existing autonomous
closed-log authorization. All six exact byte images remain recoverable; their
closed summaries/exit/owned cleanup, frozen identity and original/archive hashes
are in P3GREENC14FIX1-six-historical-closed-logs-format-manifest.json (SHA
33da89324d8c66b71bd099e3a81dc2917ca57ef0209f2ee167736b473ba6d5ea).
66582637 original bytes→21905579 archived bytes; no capture/TRX/JSON/saves/recovery/
binary or unique evidence was removed. Two newly ended generic logs were also
fully recoverably archived. Free792186880B after cleanup, reserve687865856 intact.
These are format substitutions, not retirement/replacement of older measurements.

Next independent current PREP/fresh fence and one exact controls14 invocation;
then classify all completed cases/full captures/cleanup before course admission.
All existing broader HOLDs and original failed/partial measurements remain open.

# C14 controls first attempt — fixture2 FAIL, remaining12 UNRUN

Runtime1c871c9baf13ea2917511f0c1ad6bb738bdd6b11 / all compiled ownersb36df4f.
Independent PREP and fresh unchanged fence admitted the exact14 once. Runner
stopped after the first category:2 complete FAIL/0PASS,12UNRUN, no timeout,
category8.8203354/60s,wall25.0032448s. Both failures are the same earlier
pending_turn_snapshot_reader_coverage_missing for signed progression_schedule,
not clock parsing/publication. The original default conflict fixture predates
standalone natural-cycle dispatch and omitted this source. No native clock
capture/body or later category ran; no clock GREEN or completed14 is claimed.
Primary TRX/unique IDs/error and complete owned cleanup are strictly recorded
in P3GREENC14GREEN-controls14/verified-fixture-failure.json; all original evidence
remains. No production guard, assertion or budget is weakened.

The test-only correction uses the existing SpiritualResourceCurrentReadView
standalone preparation pattern: actual scheduler/control before the sole original,
then authenticated exact schedule bytes and original active-conflict proof before
any current exchange/stale clock. Complete initial setup is reviewed together;
no post-accepted file insertion or fake receipt. Main/Support/Unit production
sources remain unchanged. Next bounded delta SOURCE, preserve actual Integration4,
fresh Integration-only build and exact current plan/PREP; no passing case to repeat.

# C14 reviewed clock candidate — fresh builds; controls14 runtime UNRUN

Independent Astra/high SOURCE accepts b36df4f7620359f37484b7594ed33b85abaff113,
including the corrected ordinary/afterlife boundary, finite ownership faults,
shared rollback inventory and synchronized Mortal GM contract/worked example.
Four fresh Main/Support/Integration/Unit owning builds all exit0 in
110.285549/13.815918/120.181133/67.078567 seconds. Warning multisets exactly
match baseline12/23/102/26 (24/46/204/52 logged occurrences); no new warning.
Four actual PE/PDB pairs,418 source bindings,27 artifacts,ten resources in40
copies and four daemons agree. All four compiled owners are b36df4f, not the old
mixed source. Only ended exact standard obj and ref duplicates were retired;
current binaries and Main/Support dependency refs remain. Actual previous mixed32
and all raw evidence are preserved in the independently verified archive below.

Official catalogue discovery passes:1300 categories/13118 methods, no unmapped
or stale selector, zero tests. Exact controls PlanOnly passes:14 cases/10 methods/
6 descriptors/5 categories, exclusive owning Integration/Unit selections, zero
execution. Cases: ownership5, plain-afterlife exclusion2, causal clock1,
documentation3 (Unit2+Integration1), original committed rollback3 (Theory2+Fact1).
Existing category bounds60/60/60/60/180 seconds are unchanged. Old three-case
Mortal compatibility HOLD is not selected or relabelled. This is not runtime PASS.

External proofs: P3GREENC14GREEN-build-*, pdb-bindings/artifact-preflight,
catalog, controls-plan and final-prep-source-fence under the existing external
us5-guided-diagnosis root. Fence18:47:24 UTC: all pins stable, no active owner,
753537024 free bytes above unchanged687865856 start reserve. Analysis-only linear
full-capture readback was independently reviewed; it preserves full decoded
messages/events, original PID/ticks/pairs, every aggregate, root/closing and
assembly links. No collector, quota or loss applies. One closed-log helper lacked
this phase allowlist: its first call stopped before mutation, not policy denial;
the single phase addition awaits review, original logs still present.

Next independent PREP, immediate fresh source/resource fence, one exact14-case
runtime, strict primary TRX/full capture/cleanup and OUTCOME. Only then consider
full course in the existing180s budget. No full-course retry, old PASS repetition,
new cache, speedup claim or DFR/Mortal compatibility/Windows HOLD release follows
from compilation. New clock intake remains unqualified until actual GREEN.

# C14 final SOURCE delta — afterlife exclusion before clock parsing

Independent review found one additional WIP regression: parsing a stale Mortal
clock before current-realm exclusion could reject an unrelated plain afterlife
intake. The current realm is now checked first; its one captured soul image is
reused as DATA and freshly compared at publication/retain. Two existing-fixture
original afterlife controls require actual ordinary common-plan construction
with malformed/non-object stale Mortal clock left byte-identical. No afterlife
GM rule changed. All candidate code remains UNCOMPILED/UNRUN.

Corrected exact selection expectations (to verify by actual PlanOnly): causal1,
ownership5, afterlife exclusion2, physical guided docs3 (2 Unit +1 Integration),
original committed rollback3 (one2-row Theory +1Fact). Earlier shorthand2 for
the latter categories was method count, not completed cases; no result is claimed.
The five helper phase deltas and actual mixed32 archive have independent approval.
Archive63488020B SHA c1e12c33ebf223dd554a953453f13bdbb45ec2f1f6bab18c4d383683c0a71e8e,
fullEOF/CRC decoded198778880B, all32 member hashes and exact mixed build provenance.
No active managed owner; free762880000B after archive, reserve unchanged687865856.
Next final delta SOURCE, four fresh affected owners, bindings/catalog/exact plan/
PREP, then finite controls/causal GREEN before any full course retry.

# C14 ordinary clock candidate — coherent SOURCE review pending

Production WIP3b7d38ac4759f72c9406f326a02bcdbc64ffe57e is remotely verified.
Finite tests now cover malformed/duplicate/mixed/overflow commands and null,
original bytes/absence/current request/control/detached authority drift,
before-publication failure, known post-commit error with exact restore/retry,
and foreign-clock preservation with uncertainty. Five new native cases in three
methods reuse one owned initial original per test and existing passive gzip.
The accepted causal Fact reuses its exact extracted initial producer; its body
and intended oracles are unchanged. No synthetic/post-accepted clock patch.

GM contract and existing worked course example now distinguish GM delta emission
from client numeric publication; one cheap doc guard is added. This is a Mortal
ordinary-wait correction, no afterlife GM wire change. Existing two-method
portable-original-committed-rollback is selected because the shared rollback
inventory/baseline filter changes; it is NOT the old Mortal compatibility HOLD.
All new code remains UNCOMPILED/UNRUN. Final SOURCE/fixture/selection review and
current actual binary preservation precede fresh four-owner builds. New runtime
requires actual bindings/resources/catalog/exact PlanOnly/PREP and immediate
unchanged reserve. Full course/rollback journeys stay unadmitted until GREEN.
Optimization remains private operation DATA plus fresh mutation/retain guards,
no new cache or measured speedup. Full raw external; old results unchanged.

# C14 ordinary clock publication — WIP, UNCOMPILED / UNRUN

Independent Astra/high accepts the exact native causal RED recorded below.
The initial production implementation now joins the shared raw-resource entry
only after specialized/QTE classification, before ordinary effect/wound intake.
It derives checked Int32 minutes from the authenticated physical original and
current delta, preserves its validated calendar, and retains only on the two
ordinary successful exits. Known later refusal/error compensates only the exact
owned clock after-image; uncertain publication/foreign drift fails closed.
CanonicalAccumulatedFiles now includes the clock so both normalizer rollback
and Engine baseline loading retain its original bytes. The shared progression
mutation schema and specialized/capture clocks are unchanged.

Focused independent inspection found and corrected three WIP defects before
compilation: unsupported optional-presence selection for WoundCommandPath,
timeChange:null regression, and an unclassified invalidation failure during
compensation. Original command absence now uses the existing detached complete
manifest/rollback inventory, with exact current guards; no presence protocol
extension. Delta-null remains the existing no-delta contract. Further tests,
GM/example synchronization and final coherent SOURCE review remain pending.
This is remote preservation only, not production qualification or course admission.

Optimization: one private operation-scoped original/before/after DATA owner;
existing source, coordinated publication and authority invalidation services.
Fresh original/request/control/current/source checks remain at mutation and retain
boundaries. No new cache, source sweep, diagnostic stack or speedup claim.
Raw stays outside checkout; unchanged start reserve687865856 bytes. No old
PASS/UNKNOWN/UNRUN/HOLD is relabelled. Next: finite malformed/source/rollback
controls and docs, final review, preserve actual current bins, fresh affected
builds/exact discovery/PlanOnly/PREP, then causal GREEN before full course.

# C14 genuine ordinary clock — exact causal RED; OUTCOME pending

Runtime a3dc3686fccd16099a34a6e05bae8340b895d45b / compiled Integration
4c0b88dc5f7e63c64bded67f62b4840a9447a613, other owners C12 source390907.
After independent PREP, P3GREENC14RED-causal1 completes exactly1 expected FAIL,
zero PASS, 1/1 case/descriptor, no timeout/underfill; category8.738687s/60s,
wall17.5543173s, actual case3.5893603s. It reaches only the intended missing
numeric-clock Assert.True atOrdinaryClock.cs90. Actual raw-intake diagnostics
are EMPTY; genuine original/control/delta/calendar/private admission and complete
unrelated-source/original-byte premises passed. Original1260 + command480 should
be1740, but current clock remains command-only. Later progression/menu assertions
are unreached GREEN obligations. No game or course PASS is claimed.

Primary TRX SHA-256
e461b95efc3ec065e521f92f8c564856e31c3c48ef8a8a4b5d8fdc1b90b7b451;
strict verified-causal-red.json records exact method/IDs/observation/selection.
Full native capture is8342300B, decoded76444300B,152302 consecutive timing events
and one complete diagnostic message. Stored SHA-256
74ba162e7645df057533b7d487a9ea964cb9e1f337d793a1edd3e872b4a002d2;
decoded87a309ba97f7fc0a0e32f786c4d8333940e64e93ec243ba2c5f91b10aaf327f2.
Streaming linear readback verifies full EOF/CRC, original PID/timestamps/tick
pairs, every operation's counts/totals/maxima, closed writer/footer, no late/open
scope and actual root absence. No event cap/truncation or new collector applies.
Existing native fixture setup1865.045488ms and raw intake964.633629ms are named
inclusive intervals, not a sum or a general speedup claim.

All pins/resources/daemons and runner cleanup verified; closing fence17:50:13 UTC
has no active owner and825778176 free bytes before generic-log retirement. Only
that ended generic log is fully recoverable gzip; original TRX/capture/JSON and
all prior course/RED evidence remain external and untouched. Independent OUTCOME
pending. Next after acceptance: minimal shared pre-intake clock implementation
with fresh original/source guards, exact rollback coverage and finite negatives/
GM synchronization, then fresh affected builds/GREEN before any full course retry.
No production clock change or new runtime admission follows from this RED.

# C14 genuine clock scaffold built — exact causal1 PREP pending

Independent Astra/high SOURCE accepts4c0b88dc5f7e63c64bded67f62b4840a9447a613:
initial calendar completion precedes the genuine original; clock-only ReadCurrent
retains original control; actual existing raw-resource entry and all RED/GREEN
boundaries are typed and explicit. Six helper phase-only changes are independently
verified. No production code, shared mutation schema or rollback inventory changed.

Fresh Integration build exits0 in146.460967s, 0 errors / baseline102 warnings
(204 logged code/message occurrences). Main/Support/Unit retain unchanged C12
source390907 builds. Four actual PE/PDB identities,414 source bindings,27artifacts,
ten resources in40copies and four daemons match. Ended standard obj/ref2+2 alone
retired; all current bins/dependency refs remain. Prior accepted C13GREEN
Integration4 preserved in full verified archive10981966B, SHA-256
bccb3bcf11407d5c26e8e7325c8bcc7fa0147dc8d4fab1bf0f67df6832c1c430.

Official discovery1298categories/13113methods has no stale/unmapped selectors and
zero execution. P3GREENC14RED-causal1-plan exits0: exactly1case/1method/1exclusive
Integration descriptor/60s, zero execution. Catalogue and plan generic logs are
fully decoded/hash-verified recoverable gzip; every other raw/result is retained.
The last fresh fence has no active managed owner, stable27/resources/daemons and
836546560 free bytes above unchanged687865856 reserve. Full capture uses the
existing passive/native path without broad ambient contexts or any record quota.

Next: independent PREP, immediate fence and only this one baseline. Classify actual
raw-admission diagnostics and full capture/cleanup before calling it causal RED.
An unrelated refusal, setup failure or timeout is not that RED. The expected
numeric-clock oracle precedes all GREEN-only repeat/progression/menu assertions.
No full course/browser/native family/rollback retry or production implementation
is admitted by this build; existing PASS/UNKNOWN/UNRUN/HOLDs stay unchanged.

# C14 ordinary-clock causal scaffold — UNCOMPILED / UNRUN

Independent Astra/high accepts the preceding five metadata changes and recommends
the existing ValidateAcceptedTurnRawResourceMaterializationAsync(lease) core after
physical continuity/resource-command checks, before owner/source/wound planning.
This is shared by Engine and direct native callers; final scheduler and normalizer
core/preflight are too late. Production placement remains conditional on the real
baseline. Existing shared AcceptedTurnOutcomeMutation must remain unchanged.

Necessary implementation guards are recorded before code: fresh physical original/
authority/current request/control/realm (never cached lookup override alone), original
numeric/control agreement, checked existing Int32 range, preserved validated full
calendar, no mixed/invalid command, no specialized diagnosis/guided/completed path,
exact current/source guards and operation-owned compensation on known failure.
WorldTimePath is absent from intermediate NormalizerRollbackTrackedFiles, so exact
rollback coverage must be explicit. No new clock service/cache/game rule applies.

The first new native Fact calls the existing raw-resource validator, not a new stub
or missing API. It reuses DiagnosisOwnershipFixture without signed treatment or
diagnosis input. That fixture's generic component clock has incomplete calendar;
only this test's original-producer callback supplies complete declared calendar
DATA BEFORE the sole genuine LiveTurnPreparationService original, keeping its
actual numeric source. No post-original/post-accepted fixture edit is used to
invent time. The actual GM command under the live lease is timeChange480.

Before the intended numeric-clock oracle: original/control/private admission,
unchanged original bytes/control, complete unrelated JSON images and returned
intake diagnostics are captured. A setup, unrelated refusal or timeout is not
causal RED. Numeric value/calendar, no command, repeat intake, actual progression
publication, repeated next controls and actual next-menu readability are GREEN-only
obligations after that oracle; this does not claim a complete Engine course.
Existing passive accumulator/gzip/native fixture lifetime records the full stream
without broad ambient contexts, arbitrary observers, quotas or a new transport.

Finite scaffold plan:
- [x] Coherent SOURCE/fixture/selection/helper review; preserve current Integration4.
- [ ] Fresh Integration-only build (other owners unchanged), bindings/resources,
  discovery/exact PlanOnly/PREP; one case in an exclusive 60s category.
- [ ] Classify the actual baseline and cost from primary TRX/full capture/cleanup.
Only then choose the smallest owned implementation and finite negatives/docs/
regressions. No production source, rollback inventory, budget or old PASS changed.
Optimization: reuse existing fixture/recorder/source owners; no Engine setup or
new cache. Original/current source guards remain fresh; raw stays external.

# C13 writer accepted — C14 ordinary clock integration DESIGN pending

Independent Astra/high OUTCOME accepts runtime234fe38a / compiledIntegration47aa:
two complete writer GREEN, exact primary TRX/output/clock-command/report/byte/NPC/
cleanup proofs. C13 closes fixture emission only. Two accepted browser journeys,
retained console PASS, current-native UNKNOWN and full-course UNKNOWN keep their
exact lineages. No full course, rollback or browser course has been retried.

T121-PHYSICAL-GUIDED-ORDINARY-CLOCK now tracks the actual functional dependency:
a normal accepted wait must leave numeric canonical time readable by next menu
and recovery, consume its delta once, preserve source/authority/history/rollback,
and retain calendar metadata without inventing a calendar epoch. The source
trace confirms schedule/report-only publication and no numeric clock conversion.

Bounded design review must select the existing actual accepted source/publication
point. A menu-only scheduler fallback would duplicate clock authority and leave
other consumers inconsistent; an absolute fixture rewrite would hide the supported
ordinary-command gap. Reuse the existing accepted pipeline and original/control
DATA instead. Placement is not yet approved: publishing only at the final scheduler
step may be too late for wound/recovery validation. Review exact call order and
original-source availability before assigning a code change or running the course.
No new clock service, persistent cache, snapshot rewrite or relaxed guard applies.

Finite next proof is a cheap real ordinary-acceptance roundtrip at1260+480=1740
(or matching fixture2880+480=3360), asserting numeric source, next-menu readability
and no repeated delta. Needed negatives cover current/original drift, duplicate
commit, afterlife exclusion, mixed/invalid/overflow time and unavailable baseline.
Use existing typed fixtures/owners; no synthetic accepted receipt. After DESIGN,
record exact methods/impact selection here, then causal RED, minimal owner fix,
GM/example synchronization, SOURCE/build/GREEN and one unchanged180s course retry.
No production change or new game rule is implemented by this status/design block.

Optimization: clock parsing/arithmetic is small; reuse already owned parsed DATA
within that one operation, with exact source guards at publication. No new cache
or cross-operation authority is justified. Raw and unique old evidence remain
external; no further deletion or broad perf/DFR/Mortal-compat/Windows run is involved.

# C13 corrected writer — complete 2 GREEN; OUTCOME pending

Runtime 234fe38a49ba425d4fc0585f18e929ea01d5e807 / compiled Integration
47aa6358d962e3985a21dba8d15020d8083a5b9a, other owners unchanged C12 source390907.
After independent PREP, P3GREENC13GREEN-causal2 completes 2/2 PASS / 1 descriptor,
no timeout/underfill, category 6.5005702s/60s, wall 14.7592488s. Primary TRX
SHA-256 693bed36349c08de7c05307221adf8d45f4d8b99ad7b48a6d354ad3dd033ca08;
unique IDs/executions, exact both pending-before-turn rows, stable pins and cleanup
verified. Actual public interface validator accepts without mutating any captured
artifact; command-only timeChange480 resolves from2880 to3360. Exact ready paths,
original-only report obligations, unrelated NPC bytes, no wound command and actual
cheap-root absence pass. Full source/diagnostic output remains external in
verified-outcome.json / verified-writer-artifacts.json and original TRX.

Only the ended generic log is replaced by full verified recoverable gzip. Post-run
fence has no active managed owner, all27/resources/daemons stable and 851763200
free bytes before log retirement. Both older RED rows and all original course
partial/evidence remain preserved. No measured performance improvement is claimed.
Independent OUTCOME pending. This proves the fixture artifact channel only; full
course remains UNKNOWN and must not be retried until numeric accepted-clock
publication/next-menu/once-only delta is proved and corrected through its owner.
No production clock fix has been applied, no manual clock patch or budget change.
Next: accept bounded writer outcome, then cheap real ordinary-clock causal control
and minimal existing-owner correction. Other PASS/UNKNOWN/UNRUN/HOLDs stay exact.

# C13 corrected wait writer built — exact GREEN PREP pending

Independent Astra/high SOURCE accepts 47aa6358d962e3985a21dba8d15020d8083a5b9a.
Fresh Integration build exits 0 in 137.408534s with baseline 102 warnings / 204
logged code-message occurrences. Main/Support/Unit reuse unchanged C12 source390907
builds. Actual four PE/PDB identities, 413 source bindings, 27 artifacts, ten
resources in 40 copies and four daemons match. Reviewed ended Integration obj/ref
duplicates alone retired; all bins, dependency refs and prior verified RED archive
remain. Production code and the causal Theory itself are unchanged.

P3GREENC13GREEN-causal2-plan exits 0: same 2 cases / 1 method / 1 exclusive
Integration descriptor / 60s, zero execution. Full catalogue discovery1297/13112
retains its actual c55f source: catalogue, selection and test attributes are byte
unchanged, so that passing discovery is not repeated; fresh exact PlanOnly uses
current compiled code. Plan log is fully recoverable gzip; all other raw retained.
Last source fence has no active managed owner, all pins stable, 854106112 free
bytes above unchanged 687865856 reserve. Only this artifact GREEN awaits PREP.

Independent bounded consumer tracing confirms a separate ordinary-time integration
gap before any full course retry: ProgressionScheduleService accepted mutation
publishes schedule/report only; cleanup/state refresh does not turn the command
into a numeric currentTimeInMinutes. Next treatment menu and accepted-state clock
checks require that numeric source. Thus this cheap GREEN can prove only correct
GM command emission and resolver3360, not accepted course continuation or once-only
consumption. After GREEN, add a bounded real ordinary-acceptance roundtrip proving
numeric clock, next-menu readability and no repeated delta, then correct the
existing owner through independent design/source/causal review. No new game-time
rule, post-accepted fixture patch, limit increase, broad perf/DFR or native retry.
All current PASS/UNKNOWN/UNRUN/HOLD distinctions remain.

# C13 wait-channel correction — SOURCE pending; UNCOMPILED / UNRUN

Independent Astra/high OUTCOME accepts the exact two-row causal RED above, not
course/gameplay PASS. The unchanged RED Integration epoch is preserved externally
as four actual artifacts in a full member/hash/length/EOF-CRC verified archive,
10982232 bytes / SHA-256
46aa52d085fbd67a63f61d726e8ae8326d3919e559c5a7f02b201f091177c180.
Current binaries, original RED TRX and interrupted course evidence remain.

Only the existing controlled GM writer's wait branch now writes command-only
{timeChange: elapsed} to WorldTimePath during the live response, declares that
path in ready.filesModified and keeps interface_updates closed. No absolute
currentTimeInMinutes/setWorldTime, manual after-acceptance clock edit, recovery
change, new rule, extra report or production guard change is introduced. Nonwait
writer behavior, all journey assertions and the two-row regression are unchanged.
This is a fixture-channel correction, not proof of whole accepted clock/course
completion. Full-course admission still needs the actual ordinary-time/scheduler/
next-menu contract, fresh build/GREEN and independent PREP.

Next: SOURCE review of this exact wait-only delta, fresh Integration-only build,
actual PE/PDB/source/resource bindings and exact two-row GREEN. No repeat of
accepted procedure/guaranteed console/browser cases. No new expensive setup or
cache applies; all broader UNKNOWN/UNRUN/HOLD states remain open.

# C13 wait writer — exact causal two-row RED completed; OUTCOME pending

Runtime 5557d983bf968205e0580a8bfc5bbc5f7c8df219 / compiled Integration
c55f00857dbd6db04b0f1159c64dbc169a805c7c, other owners C12 source390907.
After accepted independent PREP, P3GREENC13RED-causal2 completes exactly two
expected FAIL / zero PASS, 2/2 cases and 1/1 descriptor, no timeout/underfill.
Category 8.734817s/60s, wall 19.6656476s; per-case 0.6642232s and 0.1447053s.
Both fail only Assert.Empty(issues), MortalWaitResponse.cs line100, with exact
interface_updates_unknown_field / timeChange. Complete earlier writer/ready/
original report/NPC/byte-inertness premises pass. Corrected clock assertions below
that oracle remain unreached. This is causal RED, not gameplay acceptance.

Primary TRX SHA-256
34911f7b9d4a776a44a3e22a29d8dd4c3b843276fffe1b149dd5131c06cec5ec,
unique IDs, both pending-before-turn values, actual interface timeChange480 /
clock2880, incomplete old ready declaration, unchanged owning pins and runner
cleanup independently checkable in external verified-causal-red.json. Actual new
cheap fixture prefix is empty after both finally blocks. Closed generic log alone
is fully recoverable gzip; complete TRX/output/JSON, old course partial/root and
all unique evidence remain external. Closing fence has no active managed owner,
all27/resources/daemons stable, 864092160 bytes before generic-log retirement.

Next after independent OUTCOME: preserve this Integration binary epoch, correct
only the controlled GM wait channel, independent SOURCE, fresh Integration build
and same two-row GREEN. No course retry before GREEN/PREP, no nonwait PASS repeat,
no production guard, clock rule, new cache, assertion or budget change.

# C13 causal scaffold built — exact two-row RED PREP pending

Independent Astra/high SOURCE accepts c55f00857dbd6db04b0f1159c64dbc169a805c7c,
its minimal two-row writer test, category/selection and six bounded helper phase
adaptations. Writer is still unchanged. Fresh Integration build exits 0 in
159.496386s, 0 errors / baseline 102 warnings (204 logged, exact code/message
multiset). Main/Support/Unit retain their C12 source 390907 builds; their project
sources are unchanged, not newly built here. Four actual PE/PDB identities,
413 source bindings, 27 artifacts, ten resources in 40 copies and four daemons
match. Ended-owner standard obj/ref duplicates alone retired with manifests;
all current bins/dependency refs and prior four-member C12 archive remain.

Official discovery: 1297 categories / 13112 methods, no stale/unmapped selectors,
zero execution. P3GREENC13RED-causal2-plan exits 0: only the new wait writer
Theory, exactly 2 cases / 1 method / 1 exclusive Integration descriptor / 60s,
zero execution and complete runner cleanup. Catalogue/plan generic logs are
fully recoverable verified gzip; every other result/raw/unique artifact remains.
The new cheap fixture-root prefix is initially empty. Last fence has no active
managed owner and all pins stable; free space 866418688 bytes above unchanged
687865856-byte start reserve.

Next: independent PREP, immediate fresh fence and only this causal two-row run.
Acceptance requires both cases to reach the intended Assert.Empty interface
oracle after writer completion, exact observed unknown-field issue, immutable
output/NPC bytes and original-report premises; setup, timeout or cleanup failure
is not the required RED. Corrected clock/ready assertions remain after that oracle.
No full course/browser/native/rollback retry or gameplay PASS follows from build.
All old results, partials and HOLDs remain exact and separate.

# C13 wait-response causal scaffold — UNCOMPILED / UNRUN

Independent Astra/high accepts C12 course as timeout/UNKNOWN with an actual
fixture rejection, and the minimal wait-channel correction/test design. The new
tracked T121-PHYSICAL-GUIDED-COURSE-WAIT adds two minimal actual writer rows,
without Engine/world/resource bootstrap or production authority. Writer remains
unchanged for RED. Existing public interface validator, byte-inertness, exact
original-control report obligations and unrelated NPC bytes precede the intended
closed-interface RED oracle. Corrected command-only clock and exact ready-path
assertions follow it, so they are GREEN obligations, not alleged RED premises.
The cheap new category is exclusive Integration / 2 cases / 60s; SOURCE/build/
discovery/PlanOnly/PREP/runtime are pending. No complete course retry yet.

Optimization assessment: the test writes one small response and parses each
needed artifact once per independent row; no expensive world creation, repeated
source reconstruction or reusable cross-operation DATA is introduced. No cache
is appropriate for this small fixture correction. Fresh publication/authority/
History/report guards and all original journey assertions stay unchanged.
No new game/GM contract: existing canonical documents already put normal wait
in timeChange, mapped to world_time, with original-control report obligations.

C12 Integration's four actual compiled artifacts are preserved before any rebuild:
full member/hash/length/EOF-CRC verified external archive 10977099 bytes,
SHA-256 cd88b15a01a817f170f2b8f185225cf800e187384b525f20b09655b6e6ae679c.
All original bin files, older archives, raw and interrupted course root remain.
C12 two browser PASS remains accepted with exact versions; no repeat applies.
Next: coherent SOURCE review, fresh Integration-only scaffold build and exact
causal2 preparation. Main/Support/Unit production sources are unchanged at390907.

# C12 console course — timeout / UNKNOWN; concrete wait fixture rejection

Runtime 2f52db50968c7eb9811a2e0d8f18eab326ea135c / all four compiled source
390907d4d376e70ec99d49653ec2b9fe22b54c19. P3GREENC12-course-console1 times out
180.125405s/180s, wall 188.7609288s; summary 124 / wrapper 1, no TRX and zero
completed cases/descriptors. UNKNOWN, not a failed completed test or course PASS.
Runner processes ended/cleanup complete; interrupted fixture remains preserved.

Existing full readable timing prefix shows first confirmed treatment returned in
34345.596541ms, then the first OrdinaryAcceptedWait begins and its controlled GM
response returns; no wait end exists. Actual retained validation_repair_request
on turn 44 rejects interface_updates.json.timeChange with
interface_updates_unknown_field. The writer puts the 480-minute normal wait in
an unsupported output field and marks responded, so no repair is supplied. This
is an observed fixture-contract rejection, not evidence of slow course arithmetic
or a production validator defect. The old timeout remains unchanged.

Partial gzip 1327 bytes / SHA-256
74a79e83bd61d0588b2b0306ae8c19106d8d95acfa594bdb176e11e8ff84cb37
has ten readable timing events and EOFError; no full footer/report/cleanup proof.
Its root /tmp/boe-mortal-physical-journey-7d640718a9344493a8f3b0d94f9d26dd,
complete file inventory/hashes, actual repair request and original records remain
external and unmodified. verified-partial-readback.json records the exact prefix
and retained images. Only its ended generic log has a fully recoverable gzip copy.

Next bounded fixture correction: use the existing ordinary world_time timeChange
command during the controlled GM response (and declare its path in ready), never
patch time after acceptance. Keep the interface output closed and original-control
report obligations unchanged. First add a minimal direct actual-writer regression
for no-pending versus already-pending reports, with the existing public interface
validator and frozen clock resolver; prove the specific rejection before correcting
only the wait branch. No production guard, time rule, assertion or limit changes.
Independent SOURCE/PREP/OUTCOME and fresh owning build remain required. Browser
2 PASS stays accepted and will not be repeated for this wait-only fixture change.
Rollback/browser course are still UNRUN; advanced native UNKNOWN and all HOLDs stay.

# C12 browser modes accepted — next complete console course and rollback

Independent Astra/high OUTCOME accepts both actual browser cases: procedure at
runtime bbb47929b62113f317467b2a2bcea3a8ffe617ac, guaranteed at
b59f7991244274adf249e657f6f2a76ffbb93d94; both use all four compiled source
390907d4d376e70ec99d49653ec2b9fe22b54c19. Each has complete primary TRX,
full original passive capture and actual fixture cleanup. These are two separate
PASS results, not the old grouped result or a fresh whole-feature qualification.
Task/catalogue descriptions now reflect only this bounded acceptance; selectors,
budgets, requirements and executable sources are unchanged.

Next finite work remains under T121-PHYSICAL-GUIDED: existing full console course
first, then conditional publication-cut rollback. Independent selection review
finds no dependency on the unresolved rich current-legacy/removal/joint fixtures;
these simple mode journeys still enforce their own current common-settlement guards.
Course must complete three separately confirmed milestones and two real ordinary
accepted waits, reconstructing current windows/History without post-accepted file
patches. Rollback covers first publication cuts for three modes, exact held-state
compensation and separately exact whole-original restoration with no accepted turn.

P3GREENC12-course-rollback4-plan exits 0: exactly 4 cases / 2 methods / 2 exclusive
Integration descriptors, zero execution; course 1 case / 180s, rollback 3 cases /
180s. Existing successful cases are excluded. Current four builds/PDB/source and
full catalogue remain applicable: no compiled code, selector or budget changed.
Only checkpoint/task status and the two browser responsibility descriptions change.

Finite continuation:
- [ ] Independent PREP and immediate source/artifact/resource/owner/space fence;
  run only complete console course at its existing 180s bound, preserve all output.
- [ ] Strict case/TRX/full-capture/cleanup OUTCOME; failure/timeout is not PASS.
- [ ] Conditional separate rollback PREP/fence/run at existing 180s, then OUTCOME.
Browser course has not been admitted; its five-turn cost is unmeasured. Do not
infer fit from earlier browser timings, raise a failed bound or split assertions.
No native retry, repeated console/browser PASS, new diagnostic stack or cache is
part of this continuation. No mechanics/GM payload changed, so no GM update applies.
All broader HOLDs, native UNKNOWN and whole feature's open obligations remain.

# C12 guaranteed browser — complete 1 PASS; OUTCOME accepted

Runtime b59f7991244274adf249e657f6f2a76ffbb93d94 / all four compiled source
390907d4d376e70ec99d49653ec2b9fe22b54c19. After accepted independent procedure
OUTCOME and conditional guaranteed PREP, immediate fresh fence passed. Separate
P3GREENC12-browser-guaranteed1 completes 1/1 case and descriptor, 1 PASS, no
underfill/timeout, category 148.1683785s/180s, wall 157.9641652s. Primary TRX
SHA-256 068be2b5fd0f456e2c6f165aec3c30a0a9f016d39a778db6fc6ed5c4044d78c3,
exact method, unique IDs, stable pins and runner cleanup verified. The complete
unchanged journey proves current guaranteed capability, no new procedure roll,
cancellation/separate confirmation, common terminal heal, exact items/time/History,
current completion and retry-safe replay. This is shared browser queue/handler,
not DOM/provider/fresh-process qualification.

Full passive gzip has 12 original consecutive events / six matching scopes,
valid stored/decoded hashes and lengths, EOF/CRC/footer, closed writer, no open or
late scopes, and actual owned-root absence. Journey 141881.047868ms and confirmed
attempt 138935.152470ms are inclusive, not summed or claimed as a speedup.
Only the closed generic log was replaced by fully verified recoverable gzip;
TRX/JSON/capture and all unique evidence remain external. Post-run fence at
16:34:12 UTC: no active managed owner, all 27 artifacts/resources/daemons stable,
881848320 free bytes, unchanged reserve 687865856 bytes.

Independent Astra/high OUTCOME accepts both separately completed browser cases. Two separate runtime revisions differ only in this checkpoint; same
compiled code and two complete cases, not the original grouped run. Old grouped
2 UNKNOWN remains. No console rerun, native admission, course or rollback run.
Next after review: exact preparation for the existing complete console course
and publication-cut rollback controls; browser course needs its own admission.
The full physical feature, eight adapters and all broader HOLDs stay open.

# C12 browser procedure — complete 1 PASS; OUTCOME accepted

Runtime bbb47929b62113f317467b2a2bcea3a8ffe617ac / all four compiled source
390907d4d376e70ec99d49653ec2b9fe22b54c19. Independent final Astra/high PREP
admitted only separate procedure first, then guaranteed after procedure OUTCOME
and a new immediate fence; each exclusive Integration singleton keeps 180s.

P3GREENC12-browser-procedure1 completes 1/1 case and descriptor, 1 PASS,
no timeout/underfill, category 135.8573635s/180s, wall 143.3315179s. Strict primary
TRX SHA-256 125fdd6db0b915bb014882f8bd86f52c76dbe4f6f647f95f4f72d465f5caa068;
unique test/execution IDs, exact planned method, stable owning binaries and runner
cleanup verified. The unchanged complete shared journey asserts cancellation,
separate confirmation, actual signed procedure roll/retained request, common
publication, exact items/time/History, current completion, replay and cleanup.

Full original passive gzip verifies stored and decoded hashes/lengths, EOF/CRC,
12 consecutive events / six exact original pairs, no open scopes/late events,
closed writer/footer and actual owned-root absence. Journey 128585.980586ms,
ProcessConfirmedAttempt 124714.081118ms; inclusive scopes are not added. This is
actual shared browser queue/handler acceptance, not DOM/provider/fresh-process.
Generic log alone is recoverable gzip under its frozen per-run manifest; all
TRX/JSON/capture and unique evidence remain external. Post-run fence 16:29:37 UTC
has no active managed owner, 27 pins/resources/daemons stable, 882241536 free bytes.

Independent Astra/high OUTCOME accepts the procedure. At this earlier receipt,
guaranteed singleton was UNRUN; old grouped browser
UNKNOWN, retained console PASS, advanced native UNKNOWN, course/rollback UNRUN
and broader HOLDs remain unchanged. No speedup or full physical-feature claim.
Next: bounded OUTCOME/conditional guaranteed PREP, then immediate fresh fence.

# C12 browser modes — fresh preparation verified; final PREP pending

Linux source 390907d4d376e70ec99d49653ec2b9fe22b54c19. Independent Astra/high
SOURCE and PREP-foundation reviews accept the exact nine-path change, unchanged
shared journey, restored C10 production semantics, and retained C11 causal RED.
Four fresh owning builds exit 0: Main 145.014729s, Support 15.508859s,
Integration 161.015670s, Unit 82.539094s. Warning code/message multisets equal
baseline 12/23/102/26 (24/46/204/52 logged). Actual four PE/PDB identities,
412 source bindings, 27 artifacts, ten resources in 40 copies and four daemons
match. Reviewed ended-owner standard obj/ref duplicates alone were retired;
all current bins and Main/Support dependency refs remain. Prior C11 compiled
32-member evidence is fully recoverable from its verified external archive.

Official C12 discovery reports 1296 categories / 13111 methods, no stale or
unmapped selector and zero execution. Exact P3GREENC12-browser2-plan exits 0:
2 cases / 2 methods / 2 exclusive Integration descriptors, zero execution,
180s each. Only BrowserPhysicalProcedureUsesTheActualConfirmedModeAndSettlement
and BrowserPhysicalGuaranteedUsesTheActualConfirmedModeAndSettlement are selected.
PlanOnly's zero completed cases are expected and are not a runtime PASS.
Catalogue and plan generic logs are fully decoded/hash-verified gzip with frozen
recovery manifests; every other raw/result/capture is retained outside checkout.

Next: independent final PREP, immediate fresh source/artifact/resource/owner/space
fence, then separate procedure and guaranteed browser singleton invocations.
Each needs actual complete TRX, full capture, root cleanup and OUTCOME review.
Combined maximum is 360s plus separate runner overhead, versus old grouped 180s;
this is explicitly changed allocation, not speedup or old grouped PASS. No native,
console, course or rollback admission follows automatically. The retained console
2 PASS and old grouped browser 2 UNKNOWN keep their original versions/status.
No gameplay/GM contract changed; no prompt/example update or new cache applies.
All raw is external under /workspace/tboe-linux-evidence-20261025/
us5-guided-diagnosis/P3GREENC12-*. Last fresh fence has no active managed owner,
all pins stable and 882954240 free bytes above unchanged 687865856-byte reserve.
DFR/Mortal compatibility/Windows and the broader feature's open gates remain HOLD.

# C12 functional browser-mode acceptance — SOURCE and builds accepted; runtime UNRUN

Goal: finish actual queued browser procedure and guaranteed treatment acceptance.
Replace only their old two-row Theory wrapper by two named Facts calling the same
RunMortalTreatmentJourneyAsync(mode,true,false); complete shared body, helpers,
hooks, fresh guards, inputs, cancellation/confirmation, publication, items/time/
History/replay, passive full captures and actual cleanup stay byte-for-byte.
No console/course/rollback/complex-native case is changed or repeated by this step.

Independent Astra/high DESIGN accepts semantic category decomposition under
AGENTS90–93/104–106 and testing158–171. Procedure/dice and guaranteed capability
are distinct existing contracts. Each singleton keeps180s, but combined maximum
is360s plus separate-runner overhead rather than old grouped180s. This is changed
budget allocation, not unchanged total budget or speedup. The old grouped timeout
and both UNKNOWN results remain; no previous accepted PASS is being repeated.
If a singleton times out, retain UNKNOWN/investigate, no automatic further budget
increase or assertion fragmentation. DOM/provider/fresh-process remain unproved.

The rejected assembly-only scaffold is retired: its new dormant entry/counter,
new causal Fact/category and pure helper exposure only. Three earlier C# files
reproduce exactab740 C10 bytes; no gameplay assertion/previous fixture is removed.
Full48539source, all4compiled provenance and accepted causal RED/TRX/raw/cost
findings remain preserved. No cache/prototype was wired; selected-graph handoff
has not been implemented or measured in the expensive consumer. This avoids an
unproven optimization chain; the advanced native cost remains an explicit open
qualification result, not a claim of completion.

Finite plan under T121-PHYSICAL-GUIDED-BROWSER-MODES:
- [x] Source review proves exact source restoration/shared journey equality and
  exactly2cases2descriptors with explicit180+180 allocation/catalogue alignment.
- [x] Preserve current compiled32 evidence; four fresh owning builds/warnings,
  actualPEPDB/bindings/artifacts/resources and exact discovery/PlanOnly/fences.
- [x] Independent PREP then separate complete browser procedure/guaranteed runs,
  each strict TRX/full-capture/cleanup OUTCOME, no other automatic admission.
GM/player mechanics are unchanged; no prompt/example update applies. All unique
raw/saves/recovery stay external/retained and all broader HOLDs remain open.

# C11 causal RED accepted — assembly-only optimization NOT ADOPTED

Runtime9c2475b608ee6add06ac1dc23f2cdc337baf5951 / all4compiled48539ba7:
exact1Fact completes1FAIL (expected0/actual1 atTerminalCopyCost.cs65), no timeout,
case35.7233127s/category46.4859709/60s/wall58.2579488s. Primary TRX, unique IDs,
all3closed observations, earlier genuine-terminal/DATA equality/isolation/fresh
preparation/effect/handoff assertions, stable pins and owned cleanup independently
accepted. This is causal RED, never gameplay PASS. Full raw, TRX and strict
verified-causal-red.json remain external; generic log is recoverable gzip.

Full-constructor/direct-candidate/copy-of-copy observations826.265818/710.047388/
548.656521ms each execute one final replay/actual assembly. Actual assembly costs
only1.731209/2.475439/2.084732ms in this small workload. That does not justify the
conservative assembly-only prototype. No prototype implementation or production
wiring proceeds; the component entry still delegates unchanged normal copying.
Differences between these repeated unchanged paths are not a measured speedup.

Each observation records118 actual selected-graph compilations,48AgreesWith and
70retained recompositions. No complete key trace proves all inputs identical.
Bounded source review confirms compilation includes fresh admission/private-policy
checks plus graph construction; no selected-graph domain cache is already wired.
Request/output fingerprint alone is insufficient to bypass those checks. Existing
cache architecture may be used only after full key/dependency/owner/isolation proof.
A local agreed-graph handoff in one Finalize call is a possible narrower design,
but its contribution to this actual expensive consumer remains unmeasured; no
new micro-optimization or native retry is admitted merely from that observation.

C9/C10 native UNKNOWN, browser UNKNOWN and dependent native/UI/feature HOLDs stay.
Closing27artifacts/resources/daemons stable, no live owner,57old zombies,
free949456896B above unchanged687865856B reserve. All unique captures, retained
fixtures, saves/recovery and compiled evidence remain. T609/DFR/Mortalcompat/
Windows stay parked. Further work must target a concrete functional result or
measured material cost, without a new diagnostic framework or weaker guards.

# C11 scaffold built — exact causal1 PREP pending, runtime UNRUN

Independent Astra/high SOURCE accepts48539ba7099fcce9626e6612870ec3456c0b1d71:
actual passive-only assembly counter, dormant candidate and genuine rankI terminal
causal control. Removing marker/stub reproduces prior production source hashes;
normal eligibility remains joint-exclusive, no ordinary retained copy is enabled.
Caller scan is lexical assurance outside copy measurements, reinforced by actual
independent callsite review. No prototype/native run or speedup claim follows.

Four fresh owning builds at48539ba7 all exit0: Main160.876356s, Support26.495980s,
Integration179.146151s, Unit96.914624s. Warning multisets exactly baseline12/23/102/26
(logged24/46/204/52). Independent review verifies four actual PE/PDB identities,
414 bound sources,27 artifacts,10resources40copies/fourdaemons, and ended-owner
standard2/ref1,1,2,2 retirement proofs with all bins/dependency refs retained.
Prior C10/C8 actual32 managed outputs remain in full EOFCRC-verified archive.

Official discovery1296categories/13111methods adds exactly one Fact, zero
unmapped/stale and zeroexecution. Exact PlanOnly selects only new causal Fact,
1case/1method/1descriptor, exclusive Integration60s, zeroexecution. Closed generic
catalogue/plan logs are full-verified gzip with recovery manifests; all other
raw/captures/TRX/JSON/saves/recovery remain. Latest observed free949895168B;
unchanged startreserve687865856B and immediate fresh checks remain mandatory.

All raw P3GREENC11-* stays external. Expected causal RED is still UNRUN: only an
actual assembly-count failure after complete terminal/equality/isolation/windows
can qualify it; compile/setup/timeout is not that RED. Final independent PREP
pending. Existing whole-file Meta pin was already stale before C11 and remains
HOLD, never silently updated or claimed covered. Other UNKNOWN/UNRUN/HOLDs stay.

# C11 terminal private-copy causal scaffold — DESIGN accepted; not wired

Tracked subtask T121-PHYSICAL-GUIDED-TERMINAL-COPY addresses measured repeated
terminal bundle copying during the102.734s Compose. Existing joint retained-copy
architecture is the reference; widening its eligibility alone is forbidden.
Owner/key: one genuinely constructed source bundle and its private sealed input,
prepared/effect/final graphs, exact companions and complete recomputed fingerprints.
Lifetime is that object/copy operation, no static store/TTL/dictionary/new cache.
Returned mutable DATA remains independently detached. Current companion/admission
and full graph/history seals remain fresh; no cached publication permission.

Independent Astra/high DESIGN accepts a dormant component candidate first.
Initial scaffold delegates the candidate to unchanged normal DetachedCopy; no
production eligibility/cache behavior changes. Add one passive-only timer at the
actual AssembleWoundContributions implementation, never an arbitrary observer.
Use existing genuine diagnosis fixture with pre-ownership rankI terminal-heal
callback/empty legacies, actual resolver/publication and pure bundle test helpers.
Aggregate-only windows measure full-constructor reference/direct candidate/copy
of copy outside setup. Assert true terminal state, complete DATA equality/isolation,
all three observations and fresh preparation/effect/handoff before the intentional
zero-assembly oracle. A compile/setup timeout is not causal RED. Setup cost is UNRUN.

Finite plan:
- [x] Publish/review only the counter, dormant candidate and causal test/category.
- [ ] Four fresh owning builds, exact discovery/PlanOnly/resource/PREP, one bounded
  causal RED showing actual assembly/replay rather than an empty or renamed wrapper.
- [ ] Implement conservative shared terminal admission for the component candidate:
  continuation→exact original baseline→whole existing contribution checks remain
  fresh; only stable final assembly may be reused. Initial/public/unqualified
  constructors and production DetachedCopy stay full replay. SOURCE then cheap
  GREEN/equal-work counters/cost, isolation and complete refusal-parity controls.
- [ ] Only after worthwhile measured benefit/correctness and independent review,
  consider production wiring, affected regressions and native/UI admission.
If assembly savings are negligible, keep it unwired and do not launch native.
T609/fullDFR and other parked routes remain separate; no promise of speedup.
GM rules/state/payloads are unchanged by a dormant internal test entry and timer;
no prompt/example update applies. Full C9/C10 evidence and all current bins remain.

# C10 native comparison — TIMEOUT / UNKNOWN; Compose completes in102.734s

Runtime43b262af2950b64195ee2f60c445fc88b5783c33 / compiledIntegrationab740e306:
current-legacy1 times out120.1064997/120s,wall129.5280599s. Summary124/wrapper1;
no TRX, zero completed case/descriptor: UNKNOWN. Runner owner/process cleanup is
complete, actual interrupted fixture53files remain. No next native/UI run admitted.

The separately labelled observation retains all actually generated direct/intrinsic
filesystem timing events, full writer and assertions; only C9's two new ambient
contexts are absent. Its13959682B gzip partial remains unmodified; no report/footer
or full EOFCRC exists. Complete readable prefix251623events/127159884decodedB
ends in EOFError, without sequence/pair faults. Setup4086.254005ms,
Admission3518.051871ms, InitialCompose102733.963940ms; TerminalAssertions begins
at110528.1048ms and remains open at the readable prefix end. The nested common
membership phase and normalization/publication are not seen in that prefix.

Thus reduced observation did not finish the same protected case. Both C9 and C10
results remain distinct UNKNOWN; this is no measured production speedup and does
not attribute all cost by subtraction. Last C10 consumer counters show3075.787947ms
serialization and1559.804049ms writes, concurrent with producer; inclusive and
observer timings must not be summed/subtracted to invent unobserved work.

All raw/strict timeout/prefix/retained-root proofs remain external P3GREENC10-native1;
C9full diagnostic source, actual Integration4 archive and240328257B partial remain.
Fresh closing27artifacts/10resources40copies/4daemons stable, no live managed owner,
57ended zombies, free1016811520B above unchanged687865856B launch reserve.
The closed generic log alone was losslessly replaced with recovery manifest;
no unique capture/result/save/recovery/binary retired.

Next is a bounded read-only assessment of repeated private bundle copying/final
replay within InitialCompose, using the existing sealed-copy mechanism and cheap
causal/source-count proofs before any production change. No new diagnostic stack,
unsafe cache, dropped fresh guard, limit change or blind retry. T609/DFR/Mortal/
Windows and other unresolved feature/qualification HOLDs remain parked separately.
Independent C10 OUTCOME interpretation is pending; functional native acceptance
and removal/joint/UI stay open. C8 console2 and guard5 PASS are not repeated.

# C10 built and exact discovery complete — runtime UNRUN

Independent Astra/high SOURCE accepts ab740e306eec8e2e211f4fa61f3018268568d242:
exactly the two approved ambient-context deletions; all other411 source bindings,
game assertions, coarse/CommonMembership/Intrinsic filesystem timing, transport,
exception closure, fresh integrity/authority and120s bound remain unchanged.
Accepted C9 capture3 proof at9eaad89e is retained without repeated execution.

Fresh affected Integration build atab740e306 exits0 in164.415310s with102baseline
warnings (204logged occurrences, exact multiset). Main/Support/Unit retain their
unchanged C8d70 source/builds; the exact global7path proof and four PE/PDB pairs/
412bindings/27artifacts/10resources40copies/fourdaemons agree. Standard obj2 and
ref2 retired only after ended successful owner, exact duplication/identity proof,
no active owner and preservation of all27 bins/MainSupport refs. C9 Integration4
archive retains actual full-internal version bytes; no unique binary was lost.

C10 exact PlanOnly selects only the current-legacy Fact,1method/1case/1descriptor,
unchanged120s, zeroexecution. Its closed generic log is now a full-verified gzip
with restoration manifest; all TRX/JSON/captures/fixture roots remain. Latest
observed free1031176192B; launchreserve687865856B remains unchanged.
External evidence P3GREENC10-* contains build/warnings/helper/retirement/source/
PEPDB/artifact/PlanOnly proofs. Independent final PREP and immediate fence are
pending before one comparison. Lower volume or native completion is not promised;
C9 UNKNOWN is not replaced, and no production speedup/cache/feature PASS is claimed.

# C10 bounded observation plan — DESIGN accepted; source UNCOMPILED / UNRUN

Goal: locate the same current-legacy native work with the existing coarse phases,
without enabling the two new broad ambient contexts around InitialCompose and
TerminalAssertions. C9full internal source/results remain pinned below and its
actual Integration4 outputs are preserved in a full EOFCRC-verified archive
P3GREENC9-preserved-integration-before-C10.tar.gz. The240328257B partial capture
remains explicitly incomplete, never a replacement for complete evidence.

Independent Astra/high DESIGN accepts removing exactly those two test-only
EnterOperationTimingContext lines. Eight direct coarse scopes plus CommonMembership,
existing intrinsic filesystem contexts/passive hooks, null observer, same window,
all original events actually produced, timestamps, full gzip drain/closing and
all gameplay assertions/120s remain. No filter, quota, truncation, new transport,
DATA reuse/authority change, production cache or semantic guard is introduced.
Do not call this absence of all internal events:218214 original events already
precede InitialCompose; lower volume and completion are not promised.

Alternatives assessed: repeating unchanged full-internal C9 adds no cause;
production retained-bundle/replay reuse is broader and needs its own immutable
DATA/fresh-guard proof. Choose only the two-line observation adjustment permitted
by performance.md155–158 after preserving the full diagnostic version/results.
GM/player contracts are unchanged; no prompt/example synchronization applies.

Finite steps under existing T121-PHYSICAL-GUIDED-NATIVE-TIMING:
- [x] Remove exactly the two ambient contexts and review the source/metadata diff.
- [x] Fresh Integration build, baseline warnings, PE/PDB/source/artifact/resource
  proof and exact legacy-one discovery/PlanOnly; retain unchanged C9 cheap3 proof.
- [ ] Independent PREP/fresh reserve fence, one120s current-legacy comparison,
  strict TRX/capture/cleanup readback and independent OUTCOME before any next run.
Ruling: use this existing compact canonical checkpoint for the plan/ledger;
no duplicate planning tree, raw inside checkout or unrelated full suite.
No game/cache speedup is claimed; all prior UNKNOWN/UNRUN/HOLDs remain.

# C9 instrumented native outcome — TIMEOUT / UNKNOWN; phase localized

Runtimef89c0b120eba1fdd7eec0419207367c6d576dab3 / Integrationcompiled7fc64f7f:
current-legacy1 times out120.21925/120s,wall127.3620891s. Summary124/wrapper1,
no TRX, zero completed cases/descriptors: UNKNOWN. Runner processes are closed;
actual interrupted fixture53files remain, not claimed disposed. No retry occurred.

The unique240328257B gzip partial capture and initial registered-root/assembly
receipt are retained externally. Full streaming read reaches4428488 complete
original records/2268187509 decoded prefix bytes, then EOFError; no report/footer
or complete EOFCRC proof exists. Readable prefix has no sequence gaps/pair faults,
with12open scopes at its end. Full capture version/code and all available original
bytes remain preserved; this incomplete result never replaces complete evidence.

Original macro boundaries: FixtureSetup3108.748299ms, Admission1770.215293ms;
InitialCompose begins at5004.5397ms and remains open through114902.2319ms.
TerminalAssertions/Normalize/Transaction/CurrentSettlement were not reached in the
readable instrumented prefix. Twenty bundle constructors and19detached copies
complete;22prepared and22final replay validations complete, with additional open
ones. This localizes repeated work; it does not prove the original C8 phase/cost.
Last consumer counters record20754.957759ms build/serialization and18647.587774ms
write work. Concurrent producer/drain and nested inclusive timings overlap; do not
sum them or subtract them to invent an uninstrumented runtime or causal speedup.

All raw and strict receipts remain at P3GREENC9-native1, including
verified-timeout-unknown.json, native-prefix-readback.json and retained-root
inventory. Closing27artifacts/10resources40copies/fourdaemons stable, no live
managed owner,55ended zombies. Unique partial capture/root are untouched.

Authorized lossless replacement of221 exact earlier ended generic logs restores
headroom:646269508originalB, netfreeincrease571600896B. Frozen manifestSHA
84e42fba9945f6bb863aac6fac2716a55c2263120cfdd4a5e7bfc9583559d0e4 binds
all per-run original identities/hashes and full gzip EOFCRC recovery proofs.
Only generic original logs retired; TRX/JSON/captures/saves/recovery/current bins
retained. Latest closing free1044471808B; unchanged launch reserve687865856B.
No broader runtime, production optimization/cache, speedup or feature PASS follows.
Next bounded DESIGN must separate newly enabled internal timing cost from native
work using existing recorder/scopes; no new diagnostic stack or assertion/budget
change. Removal/joint/UI and broader HOLDs retain their prior unresolved results.

# C9 capture gate — 3/3 PASS; one instrumented native PREP pending

Runtime9eaad89e2d34444ce5183b6187892cb968aa82a2 / Integration compiled7fc64f7f:
shining-faction-service-capture-wiring completes exactly3Facts/3PASS/1descriptor,
8.6253075/60s,wall20.6934210. Independent Astra/high OUTCOME verifies primary
TRX/unique IDs/no errors or skips, stable pins and complete owned cleanup.
All three actual gzip captures fully decode with exact hashes/length/rows and
EOFCRC: two positives retain complete2/2 timing events, and the intended prebind
negative correctly refuses timing completeness while retaining all original output.
Actual roots are absent. This is the capture prerequisite, not native game PASS.

To restore headroom,12 exact earlier ended physical generic logs were replaced by
lossless gzip after independent scope review and fresh frozen identity/hash checks.
All per-run full decoding/EOFCRC recovery manifests and proofs remain external;
aggregate P3GREENC9-prior-closed-physical-log-retirement-proof.json binds frozen
manifestf4a3581dea71ecc90f21b452df6f5f15aa355e40329f357139460ff37a2a539f.
Net free-space increase24993792B; observed free714067968B. All27 binaries,
TRX/JSON/captures/saves/recovery remain retained. No repeated approval is needed
for authorized equivalent ended-log replacements; no unique evidence is removed.

Next is only one instrumented current-legacy Fact in its unchanged120s category,
after independent final PREP and an immediate fresh687865856B reserve fence.
Eight coarse scopes/nested membership and existing internal timers answer which
native phase consumed its previous unknown prefix. Full passive capture remains
required; a killed/incomplete capture or lost TRX is UNKNOWN. No native retry,
removal/joint/browser/rollback PASS, optimization or speedup is claimed here.
All previous HOLDs and bounded C8 console2+guard5 lineages remain unchanged.

# C9 fresh preparation complete — runtime UNRUN

Fresh affected Integration build at7fc64f7f041340c5be7fc3ffaae9bdfb5be0edc6
exits0 in140.466857s,102 baseline warnings (exact code/message multiset).
Actual global seven-path delta from C8d70c38a contains only three Integration C#
files plus four metadata paths; Main/Support/Unit and build configuration are
unchanged and retain their successful C8 outputs. Four actual PE/PDB pairs,
412 changed-source bindings,27 artifacts,10 operational resources/40 copies and
four daemon copies match. Prior C8 Integration4 outputs were preserved first in
an independently verified exact-member lossless archive; no current bin retired.

Official catalogue discovery:1295categories/13110methods, zero unmapped/stale,
zero test execution. Exact PlanOnly selects4cases/4methods/2descriptors: existing
capture-wiring3 at60s and current-legacy1 at120s. Runtime must use separate calls:
cheap3 first, then only conditionally admitted legacy1 after OUTCOME/PREP; catalog
ordering is not the requested argument order. Both remain runtime UNRUN.

Author-approved exact6 generic original logs were freshly identity/hash/EOFCRC
verified and retired with all gzip retained; actual release17084416B. Further
closed generic logs may use the same full-verification format replacement without
new questions. Catalogue and PlanOnly original logs are now verified gzip copies,
with recovery manifests and retirement receipts; every TRX/JSON/capture/save/
recovery/current bin stays retained. After successful ended Integration build,
only its exact standard obj2 and reference2 were retired with reviewed proofs;
all27 bins and Main/Support reference inputs remain. Unchanged start reserve is
687865856B. Latest observed free689508352B; every new launch needs a fresh fence.

All proofs remain external under the existing raw root, P3GREENC9-*; archive is
P3GREENC8-preserved-integration-before-C9.tar.gz with its exact-member manifest.
Independent helper/storage PREP accepted; final runtime PREP is pending. No new
speedup/cost or gameplay acceptance claim. Prior console2+guard5 PASS retain C8
lineages; native/browser UNKNOWN and all dependent UNRUN/HOLD results remain.
Optimization: existing passive recorder/window/gzip transport and unchanged DATA
reuse, fresh guards/assertions and budgets; no production change/new cache.

# C9 native terminal timing — SOURCE accepted / UNCOMPILED / UNRUN

Independent Astra/high SOURCE accepted code27d92160693165e58ffd01116f7d520aa1b7db68;
the earlier P2 below is resolved. Root checked the actual diff, JSON and whitespace,
then pushed and verified the exact remote SHA. No C9 build, discovery, PlanOnly or
runtime has executed, and no cost/speedup or whole-feature acceptance is claimed.

Test-only implementation follows the accepted bounded DESIGN on clean e53c2177.
Only current terminal legacy/removal opt in; all other physical/diagnosis fixture
calls retain null/default timing. Existing passive accumulator/window and full gzip
LifecyclePassiveTimingDiagnostics capture bind the private actual root before the
first event. Eight coarse NativePhysical scopes locate setup, admission, initial
Compose, terminal assertions, normalization, transaction, current settlement and
closing; one nested CommonMembership scope separates the added common helper.
Pre-root pure scenario construction is outside FixtureSetup. Existing internal
WoundBundle/WoundPlanCache timings inherit only synchronous lexical contexts;
OperationTimingObserver stays null and no context crosses an await.
Nested inclusive intervals must not be added as independent costs.

Binding/arming/FSM construction/directory setup are inside owned-root cleanup.
Factory failure retains the original exception, independently attempts lease/root
closing and records actual closing failures. Normal fixture closing also attempts
both resources; capture completion follows all functional/transaction/fixture
scopes. No automatic capture disposal, authority export, new transport or callback
is added. Existing assertions, aliases, full fresh integrity/source/owner fences,
NullLoggers, mutation/refusal cases and120s native bounds remain unchanged.

Independent SOURCE on c23b8fd9 found a P2 in the instrumented transaction close:
the old bare finally could mask a body/assert/Advance/Complete failure if actual
disposal also failed. The test-only correction retains the first exception,
attempts actual transaction disposal exactly once, records a close failure and
attaches it to the body exception when both fail, then rethrows the original via
ExceptionDispatchInfo. Filters restrict this preservation to capture opt-in;
noninstrumented peers retain their original exception semantics. Assertions and
production transaction code are unchanged. Delta SOURCE is accepted at the code
SHA above; all C9 build/discovery/runtime results remain UNCOMPILED/UNRUN.

Optimization assessment: instrumentation passes only original timing scalars; it
does not retain or reuse game DATA or change cache eligibility. Static repeated
copies/replays remain hypotheses about cost; no optimization/partition/speedup
claim. GM/player contracts are unchanged, so no GM prompt/example update applies.
Existing shining-faction-service-capture-wiring3 is added to selection at unchanged
60s: blocked gzip BEGIN, original message and prebind refusal. No new test body.
Source-estimated inventory53cases/32methods/28descriptors/27categories is UNRUN
pending official fresh discovery/PlanOnly; this is not an all-category launch plan.

The source writer performed no build/test/runtime/Git or evidence retirement.
Root published the WIP and independent SOURCE is accepted; owning builds,
discovery/PREP and capture gate remain pending. No retry is admitted.
Root separately observed686301184B below unchanged687865856B start reserve by
1564672B. Six new closed logs are independently gzip-verified; their originals
remain untouched pending separate author approval. External manifest
P3GREENC9-six-closed-log-format-manifest.json SHA256
5cd9a1d62abb317eff440441b9c4be5cc60c481634832d7f77950350fcc3e87c records
17084416 allocatedB potential release. This does not authorize deletion or launch.
Original native legacy UNKNOWN/removal+jointUNRUN, console2PASS, browser2UNKNOWN,
rollback3UNRUN and independent course/broader HOLDs retain their exact lineages.

# C8 browser category timeout — both UNKNOWN; rollback3 UNRUN

Runtime6b1b265a9560346555660e1f0760ad5439a55278 / compiledall d70c38a:
browser2/rollback3 invocation stops in browser category at180.2643411/180s,
wall188.9259917s. No TRX or completed descriptor/case exists; both browser case
results UNKNOWN, rollback3UNRUN. No successful case is inferred from a closed
capture and no category budget is increased. Existing console2PASS and guard5PASS
remain separate complete evidence, not whole-feature acceptance.

Guaranteed-browser capture is complete:12 original events/6 named scopes, full
EOFCRC/stored+decodedSHA/length/pairs/sequences/footer/writer and actualrootabsence
verified. Its Journey137651.495488ms/process135028.519310ms are observed costs,
not a completed test PASS. Procedure-browser capture has no report/footer proof;
partial bytes and its actualroot remain retained. Exact inventory/status is in
external P3GREENC8-browser-rollback5/verified-timeout-unknown.json. All original
logs and partial/full captures remain; no additional raw retirement beyond11.

Runner processes are closed; this does not mean the interrupted procedure fixture
was disposed. Closing hashes27artifacts/10resources40copies/fourdaemons stable,
no live managed owner,53endedzombies,free688304128B above unchanged687865856B
reserve. No more UI or legacy retry is admitted. Only about438272B remains above
that start threshold, so any new build/preparation needs a fresh space assessment.

Independent DESIGN accepts a minimal test-only native timing plan for3unresolved
questions (initial Compose returns? assertion/normalization/transaction cost?
current/tamper suffix reached?). Existing passive accumulator/gzip writer only,
8 coarse scopes+nested common-membership; typed fixture opt-in limited to current
terminal variants, actual private root bound/armed before firstevent, setup under
rootcleanup protection, original exception preserved, completion after fixture
resources. ThreadStatic context only synchronous, no observer callback or new
cache/authority; no partition/optimization claim from static clone counts. No
implementation or retry has yet occurred; all budgets, freshguards and HOLDs stay.

# C8 real console treatment — 2/2 PASS; legacy timeout stays UNKNOWN

Runtime7f38f7345d0e032339165af50dcbd012da1f5d27 / compiledalld70c38a:
P3GREENC8-console2 completes both procedure/guaranteed cases,1descriptor,
category89.9839199/180s,wall98.2618424s. Cases38.0608261/46.1356685s.
Zero failures/skips/timeout/underfill/duplicateIDs, stable pins, complete cleanup.
Actual handler/cancel/separate confirm, original/retained draw, real Engine/common
publication, time/resources/items, healed wound/History, no-reroll replay and final
cleanup assertions all complete. This is console handler qualification, not DOM,
provider or fresh-process acceptance; course/browser/rollback remain unadmitted.

Both original full passive gzip captures have exactly12events(6complete named
scopes) each. Full byte decode/EOFCRC, stored+decoded hashes/lengths, original
pairs/sequences, writer/footer closure and no-follow actual fixture-root absence
are verified at P3GREENC8-console2/verified-full-captures.json alongside strict
TRX proofverified-outcome.json. Procedure Journey37825.704277ms and confirmed
process35303.548463ms; guaranteed Journey45951.816002ms/process45374.043485ms.
GM response and closing are nested scopes, not additive independent costs.
Closing27artifacts/10resources40copies/fourdaemons stable; no live managed owner,
51zombies,free690327552B above unchanged687865856B reserve. Exactly7 C8 complete
PASS now(earlierguard/recovery5+console2), not a fresh whole50 or sum of historical
lineages. Earlier current-legacy TIMEOUT/UNKNOWN and removal/jointUNRUN are retained.

Read-only cost assessment finds finite repeated plan detachment/replay/fingerprint
work in the complex native fixture, but duration/timeout location is not proved.
No production optimization, assertion removal, timing-stack addition or retry is
admitted from that hypothesis. Next independent OUTCOME/cost/selection review may
release bounded browser/rollback or course evidence separately. Source remains
frozen and full diagnostics/unchanged budgets/launch reserve remain mandatory.
No additional uncompressed raw retirement beyond authorized11 has occurred.

# C8 current legacy — category timeout / UNKNOWN, no TRX

Runtimee75e2516b27cb99e7310b2ee1a7c3616d5e4f55c / compiledd70c38a:
first current-legacy category times out at120.2022102/120s;wall127.8526810s.
No TRX or completed-case result exists: first case UNKNOWN, removal/joint2UNRUN,
zero PASS. Summary exit124, wrapper exit1, owned child exit137; the generic lane
11-minute text is retained but does not describe the actual category budget.
No repeat or budget increase is admitted. Earlier fresh5PASS stays separate.

Post-pins equal pre-pins; runner owned-tree/runtime cleanup true. Fresh closing
rechecks27artifacts/10resources40copies/fourdaemons, no live managed owner,
51old/ended zombies,free692387840B above unchanged687865856B reserve.
Raw P3GREENC8-current3/verified-timeout-unknown.json and all logs remain complete
for this observed prefix, not a completed positive measurement. No old raw is
replaced or removed. One surviving candidate fixture root in the invocation's
creation window is inventoried read-only at surviving-fixture-inventory.json:
/tmp/boe-mortal-diagnosis-owner-358e5dd957b4474aa82a19ce8105c9b7 (53files).
Its wound History still shows only create and clockT0; this does not establish
which in-memory substage completed or any publication PASS. The root is preserved.

Next: bounded causal/cost assessment using existing named-timer mechanisms, with
no blind retry or parallel diagnostic framework. Independent UI/other-case scope
may be assessed separately; current3 and whole physical acceptance stay open.
No new build/runtime has been admitted. DFR/Mortal/Windows and other HOLDs remain.

# C8 first shared-consumer gate — 5/5 PASS

Runtime211ca03b6ff72003639963f86370b64d689e888d / all4 compiled
 d70c38a146d1dcd965bee96cc4c662ec5e64f3fb: P3GREENC8-cheap5 completes5
cases/5methods/3descriptors, zero failures/skips/timeout/underfill/duplicates.
Category spiritual-natural-recovery-overflow31.9967215/120s; World closure
6.4333891/60s; semantic source guards5.4571377/60s. Whole wall57.7098705s.
The genuine spiritual multi-generation/terminal recovery peer passes the shared
ordered-membership extraction. This does not qualify the pending Mortal current3.

Root strict TRX/count/unique-ID/pin/cleanup verification is external at
P3GREENC8-cheap5/verified-outcome.json. Fresh closing verifies27artifacts,10resources/
40copies/fourdaemons; no live managed owner,49oldzombies,free694382592B above the
unchanged687865856B reserve. New C8 complete results are exactly these5 cases;
historical distinct PASS is not added arithmetically or relabelled fresh.
Next requires independent OUTCOME/PREP and immediate unchanged fence for only
current-legacy/current-removal/genuine-joint3, each original120s bound. UI9 and
broader HOLDs remain unadmitted. No further logs beyond approved11 are retired.

# C8 four fresh builds and exact discovery complete; runtime PREP pending

Fresh compiled source d70c38a146d1dcd965bee96cc4c662ec5e64f3fb (code238a4419)
for Main/Support/Integration/Unit: all exit0, durations131.225541/18.190797/
142.486920/75.820589 seconds. Warning multisets equal baseline12/23/102/26.
All four PE/PDB pairs and412 changed-source bindings match;27 artifact hashes,
10 operational resources/40 copies/four daemons agree. No C8 test has executed.

Official catalogue validates1295 categories/13110 methods, zero unmapped/stale.
Fresh full PlanOnly confirms50 cases/29 methods/27 descriptors/26 categories and
zero execution. Its exact first-gate subset is guards4 + spiritual overflow1:
5 cases/5 methods/3 descriptors, budgets60/60/120s by existing categories. Only
that bounded subset is proposed for independent PREP; current3/UI remain unadmitted.
The full plan is inventory evidence and does not request rerunning all50 cases.

Approved standard obj duplicates and owner reference outputs were retired only
after successful owner end, exact fresh identity/hash checks and no live owner.
Main/Support dependency refs and all27 current bins remain; proof scopes are
1/1/2/2 reference outputs plus2 standard obj per owner. No additional raw was
retired after the authorized11. Extra binary archive is explicitly DEFERRED,
with no C8 archive/EOF/CRC claim; current bins and all old archives are retained.
Raw/proofs: existing /workspace/tboe-linux-evidence-20261025/us5-guided-diagnosis/
P3GREENC8-* (builds, retirement, PDB, preflight, catalog, full-plan and export status).

Optimization remains immutable operation DATA reuse with all fresh seals and
source/owner fences; no new cache, reduced checks or speedup claim. Immediate
unchanged687865856-byte start reserve and independent PREP precede runtime.
Historical PASS, incomplete/failed attempts and all broader HOLDs retain their
recorded versions and limits. This checkpoint is build/discovery evidence only.

# C8 authorized log retirement executed; launch reserve restored

Author explicitly approved the exact11-manifest uncompressed log retirement on
2026-11-01. Same environment and clean147e86e57 remote were read back first;
no active managed owner or uncertain prior deletion was found. All11 original
identities still matched the frozen manifest. Original/gzip hashes, lengths and
full EOF/CRC/byte equality were freshly checked before each exact unlink and
retained gzip decoded again afterward. Exactly11 originals are absent; all their
bytes remain recoverable through the unchanged manifest path/hash mapping.

Actual free space rose from682549248 to707031040 bytes (+24481792 allocatedB).
The unchanged687865856-byte start reserve is restored. All27 retained binaries
remain unchanged. No other raw, TRX, JSON, capture, save, recovery, archive or
source was retired. This approval covers only those11 files, not future logs.
Fresh preflight/retirement proofs remain external:
P3GREENC8-authorized-11-log-fresh-preflight.json and
P3GREENC8-authorized-11-log-retirement-proof.json under the existing raw root.

Source238a441984f8788e48eb661d98c028be73fd1f7b remains independently accepted,
but C8 is still UNCOMPILED/UNRUN. Next: fresh Main/Support/Integration/Unit owners,
actual PE/PDB/source/artifact/resource proof and exact discovery/PlanOnly, then
independent PREP for guards4/spiritual overflow1, current3 and subsequent UI cost/
acceptance. Prior PASS is not repeated without an affected dependency. Original
budgets, no-new-clone/restore and all existing HOLDs remain. Optimization remains
the reviewed reuse of immutable operation DATA without new cache or source reads;
this storage action makes no product speedup claim.

# C8 SOURCE accepted; builds and qualification still blocked

Independent Astra/high SOURCE accepts code238a441984f8788e48eb661d98c028be73fd1f7b
without material findings. Root review corrected the initially incomplete payload
negative control: constructor tampering now follows an honest staged copier/private
execution positive; genuine completed copies use the proof-preserving API. No new
completion authority was exposed. The common terminal correction is SOURCE accepted,
not compiled or runtime-qualified. All four owning projects need fresh builds and
provenance before current3, spiritual overflow1 and guards4; UI cost/acceptance follow.
No new full restore was attempted, per the instruction against new clone/restore.

Last successful builds remain Main/Support/Unit at
 a958b520fe5839c590f2621443e27de89fc18e50 and Integration at
69fdca68080cb724df0f599b2f68a63b29a8f401. These binaries do not verify C8.
Recent complete accepted independent1 is runtime
da95d7e7f221ac49621d37e70e09cd92e8529ce7 / sourcea958b520,39.441707/60s.
The last current3 remains1FAIL/2UNRUN at actual common membership; console2 remains
an earlier2FAIL menu-prefix result, with seven other UI rows unrun. No source
review or static test assertion is counted as PASS. Historical37-distinct frontier
and the separate eight adapters/other epic remainder remain explicit, not a fresh
whole-feature result. DFR/Mortal/Windows and other existing HOLDs stay in effect.

Disk decision is pending the author. Fresh observed free bytes at this receipt:
682602496 < unchanged687865856 launch reserve. No build/runtime is admitted.
All safe ended-owner intermediate retirement is proved outside checkout. The Node
cache cleanup produced no observed free-space increase. Unique raw evidence,
TRX/JSON/capture, saves, recovery and current binaries remain preserved.

Concrete lossless-format proposal: exactly11 ended generic dotnet-test.log files
from C5/C6/C7, enumerated (no wildcard retirement) in external
/workspace/tboe-linux-evidence-20261025/us5-guided-diagnosis/P3GREENC8-closed-log-format-manifest.json.
Manifest SHA256 ded9a92b1c2b8baf0d1819a441c0762996fe3502a72b69e6510c4eb3fe0799a6.
Originals total24421934B; gzip copies2759140B; hypothetical logical saving21662794B.
Both root and independent reviewer verify all original identities/hashes and full
EOF/CRC/byte-for-byte decode. ALL ORIGINALS STILL EXIST. Removing only these verified
uncompressed copies would retain every failure, timestamp and discovery byte in
lossless gzip, with original path/length/SHA mapping and gzip -dc restoration.
Actual reclaimed space remains unproved. The qualification readers use summary/
plan/TRX; literal .log consumers require decompression from that mapping. This is
not a new full replacement measurement, so original retirement remains PENDING
explicit authorization under the user's raw-retention constraint. No source,
binary, capture, archive, TRX, JSON, save or recovery deletion is proposed.

# C8 ordered common membership correction — UNCOMPILED / UNRUN

Tracked T121-PHYSICAL-GUIDED terminal correction, based on e43259925 and the actual
C7 current-legacy failure below. Source now admits terminal treatment through its
own sealed staged execution, not the recovery family or the generic all-active
loop. It reads that private execution once, checks completed base/final ancestry
and the entire flattened result vectors, and joins each application to one actual
prepared root (source/target/carrier, slots/materialization and PriorRootEffectId).
Selected wound prefix, independent legacy and joint acquisition remain distinct.
The shared pure comparison retains exact allocated create/terminal transitions,
create-before-retire, unchanged full terminal identities and absence from every
current map; survivors retain canonical current membership/materialization checks.
Recovery/spiritual extraction and their predecessor rules remain genuine and
unchanged; no recovery owner/stage, witness, allocator or authority is invented.

The two existing settlement Facts now reuse actual completed common Compose and
its genuine detached copy, reject staged-without-completion, and first prove an honest staged lifecycle copy/private-execution read. Source/
target (canonical parsed DATA), missing/changed retirement, resurrection and
materialization payload changes then refuse against that SAME staged lifecycle
seal. The fixture copier has no completed-boundary proof; completed positives use
DetachedCopyOf only. These are staged constructor/payload refusals, not fabricated completion proofs
or claims of reaching an inaccessible later branch. Existing current-reader
well-formed mapping/lineage/materialization negatives remain. The existing joint
Fact uses its real bound common owner and original complete stages through common
composition, including acquisition membership, exact original History prefix and
linked suffix, copy/refusals, omitted alias and missing acquisition-row checks.
Canonical bytes/claims keep their existing operation-bound assertions; no second
world or publication was added. Added common/joint suffix cost is UNMEASURED.

Finite proposed fresh selection: current-legacy/current-removal/joint (3 existing
Facts, unchanged120s each), spiritual-natural-recovery-overflow (one existing typed
Fact, unchanged120s) and the already selected semantic/source guards. The recovery
peer exercises genuine multi-generation and terminal common membership. The old
reflective Mortal boundary methods/HOLD aggregates are excluded; diagnosis published
recovery is an exact-replay reader, not claimed a fresh common-membership proof.
No case was added: full selected source estimate becomes50cases/29methods/
27descriptors/26categories only because one existing affected peer is selected.
Official discovery/PlanOnly and independent SOURCE/PREP remain root-owned. Simple3
and independent1 earlier PASS lineages are retained only by reviewed scope, with no
fresh PASS claim or automatic replay. All UI9 and current3 completion remain open.

Optimization: existing detached bundle views are captured once per family/helper;
prepared root pairs, ordered vectors and catalogs are one-shot operation DATA.
The full key is the genuine prepared/effect/complete payloads plus accepted turn,
canonical staged/final catalogs/identities and sealed family references. Lifetime
ends with this pure comparison; no shared cache, file read/parse contour, owner,
lease or permission is introduced. Each invocation still verifies complete seals,
ancestry, vector order and current canonical maps; changed DATA cannot retain the
lifecycle proof. The typed controls reuse the same completed plan/fixture and do
not sweep sources again. No source-read reduction or speedup is claimed. No GM
wire/game rule changes: existing accepted terminal program and examples stay exact.
No source-pin literal is changed; unrelated stale Meta literals remain untouched.

Static-only checks: JSON parse/selected-owner resolution and git diff --check.
No build, discovery or runtime was started by the writer. ALL starts remain HELD:
last observed686170112B < unchanged687865856B reserve; the failed combined fence
and separate integrity proof remain external. No storage action is proposed or
performed by this source block; root alone owns a separately reviewed storage
proposal. Raw/saves/recovery/binaries and all prior HOLDs remain untouched.

# C7 actual common membership refusal; subsequent launches resource-blocked

Runtime5cf3b512e2850829db0a8c3251e2b3ce2851dc2c / Integration69fdca680:
current-legacy passes initial canonical graph, typed capability-three/default-one
premises, signed actual request and reducer. Real common composition then refuses
accepted_mechanics_wound_final_effect_membership_invalid for a newly created
prefix wound application. Category65.9929185/120s,wall72.7084649s;1FAIL/0PASS,
current-removal/joint2UNRUN, no timeout, stable pins, complete cleanup. No new
complete PASS. External P3GREENC7-current3/verified-partial-failure.json retains
exact TRX/IDs/error. The generic final membership consumer handles ordered recovery/
spiritual-healing stages, but terminal treatment falls into an all-creations-active
loop even when its sealed later heal retired a prefix creation. This is a concrete
production integration gap, not the earlier fixture or capability failure.

The launch fence passed at688168960B with the unchanged687865856B reserve. After
full output, observed free686170112B blocks ALL further build/runtime starts.
The original combined closing fence exits1 on reserve and is preserved as failed.
Separate complete closing proof rechecks27 artifacts/410 source bindings/10resources,
40 copies/four daemons, exact checkpoint-only source delta and no live managed
owners(49 old zombies); cleanup is complete and eligibleForAnotherStart=false.
External closing-integrity-resource-block.json is not a combined-fence PASS.
No reserve/budget is lowered and no raw/saves/recovery are removed. Independent
read-only design/source work may continue; fresh build/test admission remains pending.
All previously accepted lineages and other HOLDs retain their explicit limits.

# C7 fixtures compiled — runtime PREP pending

Fresh Integration source69fdca68080cb724df0f599b2f68a63b29a8f401 exits0
in104.438031s; warning multiset remains102. Other three owner builds remain C5
sourcea958b520. Actual4 PE/PDB pairs/410 source bindings/27 artifacts/10 resources,
40 copies/four daemons agree. Astra SOURCE accepts the grouped fixture change;
C6 current3/C5 UI9 exact discovery descriptors are retained under reviewed unchanged
test/InlineData/selector/category/budget membership, not claimed fresh C7 discovery.
No runtime has executed at C7. Current3 and corrected console2 need fresh PREP.

Approved ended-owner standard obj and own refint/ref outputs were retired under
fresh identity/hash checks, preserving allbin and Main/Support reference inputs.
External P3GREENC7-build-integration proofs show free688529408B after retirement.
The exact540 regenerable Node compile-cache files were removed under an independent
resource review, but observed free space did not rise; no saving is claimed.
Source/proofs remain external in P3GREENC7-*. Raw/saves/recovery are untouched,
additional binary export is deferred, and the656MiB start reserve is unchanged.
The next fresh preparation/start fence must pass; no reserve reduction or assumed
space recovery is permitted. A later insufficient reserve blocks further launches,
not an excuse to omit output or declare unrun tests passed.

# C7 scoped initial capability and rendered label — UNCOMPILED/UNRUN

Fixture-only correction on a7d4d2527907aab401db8f3b23fce7b267250f6b. The actual
C6 current result below remains 1 FAIL / 2 UNRUN; console remains 2 FAIL before
confirmation. Neither failed prefix is a treatment-execution or full-cost PASS.

Only current reduce_legacy and terminal joint seeds opt in through the existing
SeedCanonicalComponentData.beforeResourceBootstrap callback. The async fixture
forwards an optional callback with null default; no later original/current image
is patched. Actual initial field_medic_01 / skill_guaranteed_care_01 retains its
exact capabilityRef, physical domain, ranks 1–4, active source, mastery 3, heal and
legacy limits. Its explicit aggregate reduction limit is declared as 3 for the
authored IV→III→I→heal outcome; generic CreateTreatmentSkill remains at 1.
Both seeds verify the full typed actual source/outcome coverage, including one
cosmetic/two mechanical legacies, then reject cloned default-limit-1 DATA against
the same outcome. The callback writes only that initial provider declaration,
before resource bootstrap and either authenticated original. Removal, all UI seeds
and unrelated fixtures retain their prior default. No production rule, capability
inference, authority, claim allocation, cost or time behavior changes.

The shared UI helper now requires exact rendered Общие условия: instead of the
incorrect uppercase substring. Cost, course-dose/window, cancellation, separate
confirmation and whole-image inertness checks are retained. The analogous course
Условия этого этапа, окно включительно and Ещё рано literals match the production
menu, and existing browser answer keys match the actual prompt/write APIs. This
finite readback does not qualify unreached UI suffixes or other runtime behavior.

Initial NPC/wound roots and parsed outcome DATA are read once and reused for the
positive and detached lower-limit negative; no cache, diagnostic transport, new
world, counter or speedup claim. Case/category/budget/GM contracts stay unchanged.
Accepted independent 1 / simple 3 retain their original lineage, with no repeat.
All code here is UNCOMPILED/UNRUN; only source/member and whitespace checks were
performed. Root owns persistence, independent SOURCE, fresh owning build/PREP and
runtime. Raw, saves, outputs, archives, recovery and all HOLDs remain untouched.

# C6 console first prefix — 2 FAIL; no treatment execution yet

Runtime08bcdca300d5e8e19ce2a67db16e33f640d206ca / Integration133e1eead:
both procedure/guaranteed rows fail at SelectPhysicalJourneyActionAsync line24,
which searches uppercase Условия case-sensitively. The real production reason
already renders Общие условия: plus projected conditions/cost; this is an oracle
label mismatch, not missing player information. Neither row reached confirmation
or Engine processing. Category7.0802433/180s,wall13.4489198s,2FAIL/0PASS,
no timeout/skips/underfill/duplicate IDs; stable pins and complete cleanup.

Both complete failed-prefix captures were independently decoded to EOF/CRC, with
stored/decoded hashes, original timing pairs/sequences, closed footer/writer and
absent actual fixture roots. These prefixes do not measure full UI processing or
admit browser/course/rollback cost. External P3GREENC6-console2/verified-partial-failure.json
retains exact errors/TRX and per-capture proofs. Closing free689037312B,no live
owner; raw remains untouched. Next coherent fixture correction combines the two
explicit capability-three seeds and the exact rendered condition-label assertion,
with all semantic/confirmation/inertness assertions retained. No product change or
further runtime is admitted by this checkpoint. Current3 and all UI9 remain open.

# C6 current settlement — capability refusal, 1 FAIL / 2 UNRUN

Runtimedfc20507b3c83e4794ac16e7ba56acbba8a1407e / Integration133e1eead:
the reaction graph now passes initial canonical History preparation. The actual
retained physical allocation refuses mortal_wound_treatment_capability_source_missing
before publication. Category6.494355/120s,wall13.6021652s; no timeout, unique actual
case, stable pins and complete cleanup. Current-removal/joint remain UNRUN; zero
new PASS. External P3GREENC6-current3/verified-partial-failure.json preserves TRX.
The bounded next investigation must reconcile the entire altered severity-IV
current-legacy fixture with its genuine guaranteed provider capability, outcome
range and full initial admission, rather than repeat the failed run after a lone
field guess. Do not weaken production capability or authority. Independent1 and
simple3 remain accepted by their unaffected lineages; UI9/HOLDs stay unadmitted.

# C6 fixture correction built — current3 PREP pending

SOURCE133e1eead79291fb359da3ad2f3d49c7273b2c7a accepted independently.
Fresh Integration exits0 in104.828279s; warnings remain102 baseline. Other three
owners remain the unchanged C5 a958b520 builds, not newly rebuilt. Four actual
PE/PDB pairs,410 source bindings,27 artifacts,10 resources/40 copies/four daemons
agree. Exact fresh current3 PlanOnly selects3 cases/methods/descriptors, no tests,
original120s per category. Membership is unchanged; C5 official catalogue/full49
and UI9 PlanOnly are retained inventory evidence, not fresh C6 runtime results.

After ended/no-live-owner and fresh identity/hash checks, the four exact Integration
standard obj DLL/PDB plus own refint/ref compiler outputs were retired. Runtime bin
is preserved; reference DLL pair equality does not mean equality to runtime DLLs.
Main/Support dependency references and all27 currentbin hashes remain intact.
External manifests/proofs are in P3GREENC6-build-integration; free695422976B after
retirement. No raw/saves/recovery/restore inputs were touched; export stays deferred.
Independent PREP plus immediate unchanged656MiB reserve/source/owner fence precedes
one current3 invocation. There has been no C6 runtime. Independent1/simple3 remain
accepted by unaffected lineages; no repeat is proposed. UI9 and broader HOLDs stay.

# C6 reaction fixture correction — UNCOMPILED/UNRUN

Bounded fixture-only correction on 9386b339fae06ad911740aa1735d081a26eded9a.
AddTerminalLegacyReaction inherited stackKey=bleeding / policy=stack from both
generic definition builders. EffectReactionContract.ValidateDownstreamDefinition
requires distinct source/downstream stack coordinates or explicit replace on a
shared coordinate; the diagnostic terminal_child_restore/bleeding/stack describes
this conflict, not a composite authored definitionKey. The same source graph also
requires expansion >= 2 for the reaction plus its reachable leaf. maxExpansion is
a recognized field whose value 1 was invalid, not an unsupported/unknown field.

The initial fixture now declares distinct parent/child stack keys and maxExpansion 2.
The real owner_turn_end trigger, canonical apply_definition route, source-bound
lifetimes, downstream amount 7 / bounds 1–10, all other definition policies and actual
descendant/current-materialization refusals remain unchanged. No production guard,
method/case/category/budget or GM contract changes. There is no new fixture world,
owner, authority, cache, counter framework or measured speedup claim.

Finite caller/preparation audit: current-legacy and the genuine joint seed share
this one helper before CreateCurrentWoundHistory, bootstrap and either original.
Current-removal uses the already accepted guaranteed remove/heal DATA with its
independent action-control legacy and no reaction edge. All nine physical UI rows
use SeedGuidedPhysicalInitialWorld, whose terminal outcomes have empty legacies;
they do not call this reaction helper. Their ordinary source/wound definitions and
full initial History are still validated by the existing canonical preparation guards.
No post-original or post-accepted bytes are patched. This is source preparation
readback only, not runtime qualification of current 3 or UI 9.

The failed C5 proof below remains unchanged. Accepted independent 1 at compiled
a958b520fe5839c590f2621443e27de89fc18e50 / runtime
da95d7e7f221ac49621d37e70e09cd92e8529ce7 is unaffected and is not replayed.
Only source/diff checks performed by the writer; both paths remain UNCOMPILED/UNRUN
until root persistence, independent SOURCE and owning build/PREP. All raw, current
outputs, saves, archives, recovery and existing HOLDs remain untouched.

# C5 current settlement preparation — 1 FAIL / 2 UNRUN

Runtime69e3c385448a7f6ffd297f8b7ca28093854a7ae9, compileda958b520:
current-legacy fails before original/publication in CreateCurrentWoundHistory.
The authored event_reaction/child definitions share a non-replace stack coordinate
and declare an insufficient expansion value1; exact failure paths are
payload.definitionKey and payload.maxExpansion. Category5.0869583/120s,
wall11.5811569s; no timeout,
unique actual case, stable pins, complete cleanup. Current-removal and joint
were not executed. Strict proof: external P3GREENC5-current3/verified-partial-failure.json.
This is a fixture-schema preparation failure, not a claimed product failure or
PASS. Independent1 remains accepted separately. No repeat before bounded cause
and complete remaining fixture preparation review. UI9 exact PlanOnly is complete
(9 cases /5 methods /5 descriptors, zero execution); UI runtime is still UNRUN.
Closing: no live owner, stable27 artifacts/resources, free688529408B. Reserve is
unchanged; new work must pass its own resource gate. No raw/saves/recovery removed.

# C5 independent terminal legacy — 1 PASS; current3 PREP pending

Runtime da95d7e7f221ac49621d37e70e09cd92e8529ce7 / compiled source
a958b520fe5839c590f2621443e27de89fc18e50: one exact independent-root Fact
passes, category39.441707/60s, wall45.2958020s. Planned/completed1, unique IDs,
zero failures/skips/timeouts/underfill, stable pins and complete cleanup. Its full
actual common publication, independent cosmetic/mechanical codec/refusals, linked
History, removed wound and final identity/carrier/source roles pass. This is a
complete native case; UI/current3 qualification is not implied. External strict
proof: P3GREENC5-independent1/verified-outcome.json; fresh closing fence shows
27 artifacts/resources stable, no live owner, 49 old zombies and694386688 freeB.
The previous failure remains untouched. Frontier is now37 distinct PASS across
explicit lineages, not one fresh37.

Exact P3GREENC5-current3-plan selects current-legacy, current-removal and joint:
3 cases /3 methods /3 descriptors, zero execution, each original120s bound.
Next requires independent OUTCOME/PREP and immediate unchanged start fence.
UI9 and broader holds remain UNRUN/unadmitted. The entries below describe the
initial preparation state; this latest receipt supersedes their UNRUN wording
only for the one completed independent-root case.

# Physical guided treatment — current checkpoint (#1536)

This is the current compact continuation receipt for T121-PHYSICAL-GUIDED,
READ-CALLBACK and TERMINAL-HEAL. The historical sequence and failed evidence
remain in [plan.md](plan.md); this receipt does not close their parent tasks.
Keeping subsequent receipts here avoids repeatedly storing the large historical
plan as new Git objects. No history, raw evidence or accepted result is removed.

## C5 routing/allocation correction — built, runtime UNRUN

Code/source: a958b520fe5839c590f2621443e27de89fc18e50. Independent Astra/high
SOURCE accepted the complete nine-path change. Independent legacy allocations
remain complete sealed executed results, separate from current wound-routing
aliases; exact actual prefix terminal retirement preserves joint acquisition.
No lineage guard, authority, allocator, game rule, GM wire or budget is weakened.
Existing current/removal/joint controls retain strict routing/allocation refusals.
GM prompts/examples need no further change for this representation correction;
the guided capability and terminal examples were synchronized in the prior block.

Fresh Main / Support / Integration / Unit builds all exit 0 in 94.204 / 11.555 /
91.818 / 47.654 seconds. Warning multisets equal baseline (12 / 23 / 102 / 26
unique build warnings). Actual four PE/PDB pairs, 410 source bindings, 27 artifact
hashes, ten operational resources with 40 copies and four daemon copies agree.
Official catalogue discovery: 1295 categories / 13110 methods, zero unmapped or
stale selectors. Full physical PlanOnly: 49 cases / 28 methods / 26 descriptors /
25 categories, zero execution. Exact independent-root PlanOnly selects one named
Fact / one descriptor, exclusive Integration / 60 seconds, zero execution.

Additional binary export is explicitly deferred; no archive or EOF/CRC proof is
claimed for this epoch. All current bin outputs and earlier archives remain.
After each successful ended owner, only its two exact standard obj DLL/PDB
copies were retired after fresh identity/hash equality to retained bin and no
live managed owner. Separately reviewed four exact refint DLL duplicates were
retired after all builds ended, preserving all four obj/ref counterparts and all
27 bin hashes. Neither cleanup touched raw, saves, recovery or restore inputs.
The unchanged build/run start reserve is 656 MiB; fresh fences remain mandatory.

All raw/provenance is external:
/workspace/tboe-linux-evidence-20261025/us5-guided-diagnosis/P3GREENC5-*.
Resource/helper/retirement/PDB/preflight/catalog/full-plan/singleton-plan proofs
remain there. No raw, TRX, generated analysis or binary is repository content.

Optimization assessment: reuse existing sealed execution DATA and already copied
views; no new cache or source sweep. Routing/allocation checks are bounded and
retain fresh integrity/authority. No measured speedup is claimed.

Retained frontier: 36 distinct PASS across explicitly different lineages, not one
fresh run. The simple procedure/course/guaranteed terminal3 remain runtime
6efe7e156ac2ed1ee231ec443f444fbaa96c69e5 / compiled
ebfb65dd17120ca17a2f83a6c1260a38359da24e. The newly added assertions have not
retroactively run. The previous independent legacy failure and all original raw
remain preserved. Next: independent PREP, immediate fresh fence, one independent
root runtime; then current legacy/removal/joint3, then console/browser/course/
rollback9, each only after its prerequisites. No runtime is admitted by this WIP.

Remaining whole-feature work also includes the exact eight spiritual diagnosis
proof adapters: item_quantity, resource_quantity, consent, facility, location,
quest_state, effect_state, environment. quest_state means a quest. This is not an
exhaustive epic remainder: US3 lifecycle/conflict and US7/recovery/cross-cutting
acceptance still need actual-code/evidence reconciliation, not blind checkbox
completion. Existing DFR/Mortal/Windows HOLDs, guardian intermittency and cross-OS
DATA limitations remain open; none is relabelled PASS or automatically retried.

## Display text boundary qualification (T152)

On source based on b43726ec7494e4c7600733ca31330212201a4a41, six
independent parser rows were added for description, prognosis and the first
visibleSymptoms element at the existing 2000/2001-character boundary. The
existing name and acquisitionNarration rows were retained. The exact ten-row
selection passed 10/10 in 3.0396306 seconds (60-second category limit); no
skips, duplicate identities or underfill, and owned/runtime cleanup completed.
The fresh serial Unit build passed with 155 warnings and zero errors; no warning
baseline equivalence is claimed. Independent review found no source defects.
The old four-row receipt remains historical and does not qualify the new rows.
These are isolated small in-memory fixtures; caching is not warranted and no
production rule, UI behavior, Unicode policy or spiritual-combat rule changed.
This is not full T152, UI, engine, or whole-feature qualification. The A5 joint
terminal treatment remains unqualified under its original timeout; aggregate-only
diagnostic work is separate from acceptance.

# T152 negotiated console confirmation literal text — isolated WIP, 2026-11-04

Base6007e82d304f90931a846cad14a4859e40faac58. Separate local copy
`wounds-text-surfaces-6007`; no publication. Existing accepted projection/privacy,
status rendering, notification controls, display10 and provider gameplay receipts
stay versioned and are not repeated. A5 remains UNKNOWN.

Concrete uncovered FR-076 / SC-008 boundary: the plain confirmation built by
WoundApplicationService.ConfirmationFor includes accepted compensation ReadableTerms.
The console caller passed that string directly to IExplorerConsole.Confirm, which
Spectre interprets as markup; the recording wrapper also removes markup. Prior
provider tests used ordinary terms and a fake Confirm that only captured text.
They did not prove this rendering boundary.

The minimal correction escapes the complete plain string exactly once with the
existing GameInterface.EscapeMarkup immediately before the wound console Confirm.
A typed internal UI seam holds the same existing call and is used by the real
command, so cheap tests need no game world or reflection. Stored/service terms,
selection identity and validation, browser rendering, general styled Confirm
calls, current ownership, cancellation and treatment staging remain unchanged.
No new Unicode policy or GM-authored capability/field is introduced; existing
FR-076 already requires this literal text rendering, so no GM schema/example
change is needed. This is one linear escape per explicit prompt; a cache would
add ownership complexity with no repeated expensive source to reuse.

Causal RED: fresh Unit build0errors/86warnings,84.14s in SDK8.0.427/runtime8.0.33.
`wound-treatment-confirmation-text`17/17 completed: eight expected markup failures
and nine passing controls,8.1170556s command wall, complete owned process cleanup.
Both direct and recording branches expose balanced-tag/doubled-bracket text loss
and unknown/open-bracket parser failures; exact raw selection remains unchanged.
The temporary wiring-text guard is removed from the final16 behavioral cases:
the separately requested existing negotiated Elyara console journey will prove
real consumer wiring with bracket terms and all original assertions/180s bound.
GREEN and actual journey remain pending at this WIP entry. Raw evidence is outside
Git at `/tmp/boe-wound-confirmation-6007-20261104/`.

## T152 negotiated confirmation — addressed GREEN and actual console result

The minimal one-time escape and complete source/test selection received independent
SOURCE review with no blocking findings. Fresh Unit build:0errors/49warnings,
69.68s; fresh Integration:0errors/108warnings,50.65s. These are incremental build
counts, not a clean-warning claim. SDK8.0.427/runtime8.0.33 on Linux; two existing
newer-analyzer compiler warnings remain. No Windows/SDK10 qualification claimed.

Inventory-only ValidateCatalog passed1409categories/13322methods-files;
exact PlanOnly selected two descriptors/17cases (16unit+1integration), executing
zero cases. `wound-treatment-confirmation-text` completed16/16 PASS in8.1772287s
command wall with complete owned cleanup. Both direct and recording variants
render balanced markup, unknown/open brackets, doubled brackets and ordinary
Unicode literally using the real Spectre Confirm parser; exact decoded prompt,
recorded text, retained raw terms and yes/no/default cancellation are checked.

The sole directly affected `negotiated-healing-elyara-console` original journey
also completed1/1 PASS: TRX33.2654888s, category37.1541742/180s, command42.4296769s.
Only this existing case receives bracket-bearing negotiated terms. Actual Engine
offer43, natural History, normal cleanup, guided cancel/read/separate confirm,
attempt44, exact unchanged wallet/terms/draw/wound/cycle/participant memory and
inert replay assertions are retained. It additionally renders the actual captured
confirmation with Spectre and compares exact plain text. Other accepted provider
journeys were not repeated; this is not new browser/packaged/Windows qualification.

The configured original passive-event capture is complete:23 decoded JSONL rows,
22/22 events attempted/enqueued/drained/stored, zero pending/failed/open scopes,
window Closed/Complete=true and no late completions. Gzip footer, stored/decoded
bytes and SHA-256 were independently rechecked. No diagnostic failures; fixture
root removed and independently absent; owned-tree and runtime cleanup succeeded.
The three loaded assembly hashes and frozen source/catalog/build hashes still
match after execution. Records SHA-256:
880AA39F5B74918B3981883FCC337FC0A7005202D09F57A4DBE23B98A3D0F536.

Evidence outside Git: `/tmp/boe-wound-confirmation-6007-20261104/` contains source
and build hashes, RED/GREEN/build/catalog/plan/journey logs, full TRX, configured
original events, initial/report receipts and `final-verification.json`. No raw
artifact or binary is included in this change. Local `git diff --check` passes.
No commit or publication was performed. Independent final OUTCOME review accepted
this isolated console block after checking RED/GREEN TRX, the Elyara journey,
gzip/events and cleanup. The hash statements above describe the state immediately
after that run, not later combined checkout builds. TestSupport has its actual
capture hash and was compared then; it had no separate frozen pre-run baseline
manifest entry. Whole T151/T152/#1536 and A5/R2 UNKNOWN remain open.

## T151 physical browser wording — bounded design

FR-075 requires semantic console/browser parity. Independent source review finds
that AddPhysicalTreatmentChoices feeds the same menu as physical diagnosis, while
ExplorerWoundCommandResultBuilder never tests MortalTreatment. Physical-only
choices inherit spiritual one-cycle promises; mixed choices inherit diagnosis-only
confirmation; unavailable physical choices and NPC targets inherit spiritual/self
labels. BrowserWoundWriteService also promises a normal cycle on false confirmation
and successful queueing. PromptForm displays these supplied strings unchanged.

Use the existing typed selection distinction only. Correct physical labels and
defer common helper/confirmation text to each option's stated conditions; do not
promise a check, duration, roll, automatic wait or cycle that a particular route
does not declare. Keep diagnosis-only/spiritual behavior and exact option Reasons.
The write service's generic messages should describe separate confirmation and
queued processing, not a mechanic. Empty/unknown menus have no realm signal and
are outside this bounded correction. Do not add inference or a new frontend flow.

TDD plan: independent small selection DATA tests exercise all three treatment modes,
mixed/disabled ordering and NPC refusal; real file-free-world early write refusal
checks missing/false/wrong-type confirmation without entering action validation.
Run the existing spiritual assessment leaf as the directly neighboring branch.
After cheap GREEN, strengthen only the existing physical browser procedure journey
with actual prompt, Boolean-false inertness and queue-result assertions, retaining
all Engine/settlement/replay checks and the original180s limit. No accepted provider,
diagnosis, course, A5 or broad aggregate is repeated. Independent final review required.

Cost assessment: this is cheap one-menu string selection over retained DATA, no
repeated expensive computation. No cache is justified. Test preparation uses fresh
independent typed records and a tiny filesystem only for the writer's early guard;
one existing real journey proves wiring. No ownership, generation, source, effect,
publication or cancellation check changes. No new GM-authored field/capability:
the existing contract already requires correct labels, so GM examples stay unchanged.

## T151 physical browser wording — SOURCE accepted, consumer outcome UNKNOWN

Fresh RED build0errors/26warnings,24.45s. The first22-case run completed with17
causal text failures,2 diagnosis controls and3 fixture setup failures: its byte
snapshot tried to read the held runtime lock. Only the fixture snapshot was fixed
to cover canonical files, excluding `.boe_runtime`; no production fix preceded
the causal rerun. Fresh build0errors/26warnings,25.06s. Final RED22/22 completed:
20 expected wrong-text failures and2 controls PASS,9.0874186s, complete owned
cleanup. This is canonical-file preservation coverage, not runtime immutability.

The production diff only selects physical menu text and replaces the writer's
unconditional cycle promises with action/condition/queued-result wording. It
preserves all current confirmation, prompt fencing and atomic publication code.
Independent SOURCE review found no blockers and accepted the narrow selection.
Fresh Unit build0errors/86warnings,80.35s; Integration0errors/108warnings,49.50s.
SDK8.0.427/runtime8.0.33, existing newer-analyzer warnings remain; no SDK10/Windows
or clean-warning claim. Inventory-only validation passed1410categories and
13328methods/files. Exact PlanOnly:3descriptors/34cases,0 executed.

Addressed GREEN completed33/33 PASS (new physical text22 + neighboring spiritual
assessment11),13.0293571s command wall, complete owned/runtime cleanup. Typed
physical-only/mixed/disabled choices retain their exact labels, values, Reasons,
required flags and source selections; diagnosis-only/spiritual controls pass.

One original `mortal-physical-guided-browser-procedure` run was started with its
unchanged180s category limit, original full passive events and all existing
settlement assertions. It now also checks the actual prompt, Boolean-false
confirmation retaining the same session and unchanged canonical images/no queue,
then true confirmation's queued-result text. A subsequent process-output poll
was denied by the execution environment. It was not retried or worked around;
the test was not restarted. Read-only local inspection after the original budget
found no TRX, summary or terminal report. Therefore this is UNKNOWN/incomplete,
not PASS and not an observed category timeout.

The partial records file is167052644bytes, last modified2026-11-04T12:41:49Z;
`gzip -t` fails with unexpected EOF. Its fixture remains present. Initial PID129
is not visible in the current namespace, which is not a process-cleanup receipt.
Fixture/process/capture closure is unverified. All6 frozen source/catalog hashes
and3 baseline build hashes still match; actual loaded assembly hashes remain in
the initial receipt. Keep this incomplete evidence; it replaces no complete run.
No further profiling, rerun, assertion omission or budget increase is authorized
by this result. Consumer/outcome acceptance remains open.

Evidence: `/tmp/boe-wound-physical-copy-6007-20261104/` holds the RED/GREEN and build
logs, inventory/plan, partial original capture and `interrupted-run-state.json`.
No raw diagnostic, commit or publication is included. The prior isolated console
correction and typed terminal-cache experiment are preserved independently.
Independent partial OUTCOME/documentation review verified the33 unique passing
cases, causal RED, manifests and the explicit missing consumer/cleanup evidence.
It accepted the reporting, not the incomplete actual journey. Read-only runner
inspection found local TRX/passive-file destinations and no explicit upload call;
the saved environment has DOTNET_CLI_TELEMETRY_OPTOUT=1. The denial did not identify
a concrete endpoint/payload, so no external transmission or permission is assumed.

## US5 physical terminal History — read-only contract finding

The producer publishes a primary treat row with MortalWoundTreatmentPersistedResult
and a linked terminal heal row with no transition result. ProjectHistory accepts
only recovery/spiritual/combat stage types and therefore omits this valid healed
record. US5 scenario2, FR-068 and SC-006 require it. Existing physical terminal
publication assertions prove stages/identity/carrier removal but do not call the
player projection; accepted evidence is not revoked or repeated.

A safe reader must reuse the already validated effect catalog plus durable
EffectIdentityState returned by EffectMechanicsSnapshot.Build, within one opening
operation. Extract only shared pure resolution/graph/allocation/terminal joins from
the existing completion implementation, not the payment/items/current-completion
pipeline. Preserve exact wound/owner/realm/turn/event/transition/ordinal joins,
actual effect allocation and retirement identities, both independent fingerprints,
visibility and carrier absence. Display the actual terminal before severity (I
after IV→I), not the initial request severity. Scan/index History once and reconstruct
each result once; no persistent cache, synthetic guided wrapper or summary authority.

Completeness blocker: guided results carry CurrentMinute and course mode evidence
carries ResolvedAtGameTimeMinutes. Plain procedure/guaranteed results and effect
identity schemas carry no historical publication minute. A legal stabilize prefix
before heal writes that minute into RecoveryAnchor.AnchorMinute, included in the
whole-wound fingerprint. No existing durable witness was found in this bounded
trace. Current clock or an invented zero is not evidence. Clock-independent plain
programs can reconstruct, but guided-only or selective omission is not complete
SC-006 support. A genuine durable clock witness or an explicitly approved contract
change is required before complete implementation; no History code was changed.

Future causal fixtures must be genuine strictly parsed accepted output for guided
and plain producers: full History prefix, healed identity, empty wound carriers,
effect carriers and durable allocation lineage. The existing terminal semantic
input fixture lacks these outputs. Do not strip a guided witness and call it a
plain-producer fixture; do not rerun A5 to manufacture evidence. A5 remains UNKNOWN.

The bounded search also checked ordinary/joint completion sources. Current world
time and progression schedule are mutable; progression report/pending originals
are consumed by cleanup; accepted story stores real UTC, not game minutes. The
retained mortal_qte_decision_publication marker does contain original progression
images, but is one current joint/QTE object, not an archive, and is absent from
ordinary standalone treatment. It is not a universal historical dependency.

The minute-only proposal is withdrawn after independent review: a legal terminal
prefix may include apply_deterioration, whose detached preparation also requires
its genuine bindingFingerprint and sourcePath. These are present in guided v2,
but not in the plain request/receipt. None may be invented or replaced by current
values. Hash agreement alone does not establish their historical provenance.

Reviewed source-design direction, not an implemented schema: a separate closed
History version around the unchanged raw request/receipt, containing all three
historical inputs (publication minute, binding fingerprint, source path) and a
whole-envelope seal. Keep Create(resolution) for raw command/payload DATA and
existing guided v2 unchanged. Explicit version dispatch must reject unknown
versions without fallback. A producer factory must receive the genuine preparation
and current accepted owner, check preparation/resolution agreement and exact owner
binding/clock/source coordinates, then copy DATA; no arbitrary-long factory.

The same complete envelope must be used at all five relevant sites: ordinary
MortalTreatmentPublication, terminal/joint view, joint final view, and both joint
marker expected-result comparisons. Preserve all inner receipts, existing stage
fingerprints, suffix/lineage proofs and live admission/publication checks. No old
immutable bytes may be silently rewritten or resealed. The actual repository
AGENTS.md at HEAD6007 contains the pre-release current-contract cutover policy;
no accepted compatibility exception has been identified. Supported schema/fixtures
must be evaluated explicitly; historical facts missing after cleanup cannot be
backfilled or promised by a compatibility fallback.

TDD implementation sequence under the existing US5 task: first feed actual composed
terminal after-images and accepted effect DATA through the current player projection
at AssertTerminalPublicationStages. Then cover full historical input provenance,
stabilize/adverse prefixes, guided and true plain producer fixtures, unknown/malformed
versions, foreign preparation, exact parent/child/identity/allocator joins and
visibility. Use the existing WoundHistoryState indices rather than another cache;
carry already parsed effect DATA once per open and retain detached compiled DATA
within that opening only. No full payment/completion pipeline or A5 rerun.
The first causal helper is test-only. After independent SOURCE review and an
initial-carrier prerequisite correction, the fresh integration build passed with
0 errors/108 warnings in55.14s. The existing independent-root singleton established
causal RED:1/1 executed, all carrier/effect/resource/projection availability and
empty-active premises passed; only Assert.Single(projected.History) failed with an
empty collection. TRX32.3748156s, category36.3730793s/60, wall42.1207622s, no timeout;
owned/runtime cleanup completed. This is I-to-I missing-entry evidence only, over
actual composed after-images, not completed publication or IV-to-I discrimination.
Evidence: /tmp/boe-wound-history-6007-20261104/red/.
The next true ordinary nonguided IV-to-I fixture and strict detached v3 parser
tests are test-only and UNRUN pending independent SOURCE review. Synthetic parser
metadata proves lexical shape/seals only, never genuine historical provenance. Browser acceptance remains
separately UNKNOWN; its frozen-manifest checks describe the inspected pre-History-
test state, not later rebuilds of the combined checkout.


#### Physical History implementation checkpoint (local, acceptance pending)

True ordinary nonguided IV-to-I-to-heal baseline completed its actual typed owner,
request, common composition, exact treat/heal joins, rematerialized root allocation
and retirement, healed identity and all display premises. Only empty player History
failed. Common composition16.667s; runner32.3310132s, no timeout, owned fixture and
runtime cleanup complete. Complete composed (not published/completed) DATA is
retained in /tmp/boe-wound-history-6007-20261104/parser-plain-red/. The runner stopped
after this expected failure, so the parser was run separately.

Parser first50-case attempt had48 fixture JSON-parent ownership errors and2 passing
controls; it was not causal. After only detaching the source node, fresh unit build
0errors/26warnings23.33s and RED50 complete yielded49PASS/1expected missing-v3
failure in8.7276606s, cleanup complete. Expanded known-version/raw/strict-shape/
seal/copy controls then passed62/62 after production implementation. Fresh combined
unit build0errors/86warnings81.38s. Neighbor projection32/35 failed in three
unchanged fixtures before production, at strict parsing of empty diagnosis.check.
Only that fixture field is replaced by the existing complete diagnosis builder;
all privacy assertions remain unchanged. Its exact one-line patch is retained
outside Git. No broader GREEN or full acceptance is inferred.

Local implementation now adds the separate closed History v3, explicit version
dispatch and genuine owner/preparation factory. Raw command creation and guided v2
remain separate. The joint continuation owns one private frozen result, includes
its seal in the original continuation fingerprint, and returns detached DATA to
both final views and both marker comparisons; live checks remain fresh. The
standalone producer captures the same complete envelope from its genuine owner.

Reader work reuses the existing opening-owned source maps and EffectMechanicsSnapshot
Build's already parsed catalog/index, with the original fresh fallback for arbitrary
source callbacks. No new persistent cache or permission reuse is added. Existing
History/source indices avoid per-wound whole-History and per-root global allocation
scans. Historical reconstruction uses only the pure outcome and exact wound/
identity/lineage joins, never the current payment pipeline. The shared compiled
selected graph serves both wound assembly and terminal lineage within that
comparison. Strict parsing and later comparison intentionally reconstruct fresh
private frozen terminal DATA rather than assume the complete companion graph is
immutably owned across every supported mode. The current strict fixture uses
ImmutableArray/ReadOnlyCollection/cloned JsonElement and exposes no ordinary mutable
nested node; no fake alias mutation test is claimed. Broader reuse would require a
complete ownership proof. This conservative integrity cost is not a speedup claim. Actual source-read
counts and cross-opening mutation/malformed refusal are pending bounded tests.

Next: independent production SOURCE review; fresh integration build; original
60-second plain and independent-root leaves with full original assertions, then
only directly necessary guided/joint/adverse coverage. The plain fixture adds
stabilize before IV-to-I-to-heal to prove genuine RecoveryAnchor minute, same
owner factory negatives and cheap exact-join refusals without another world setup.
Browser consumer/cleanup and A5 remain separately UNKNOWN. No commit or push.


Independent SOURCE review confirmed a historical-only audit suffix gap: current
guided settlement requires terminal heal to be latest, whereas supported History
permits nonterminal legacy/archive audit rows preserving the terminal fingerprint
and advancing identity ordinal. A typed synthetic archive DATA control over the
genuine composed prefix is prepared; it is not claimed as published archive output.
The strict guided current boundary must remain unchanged. Reviewer confirmed that
independent legacy birth proof is outside this narrow name/severity/healed-summary
display: exact healed snapshot/identity already bind deterministic declared refs,
and no legacy state is displayed. No complete legacy provenance claim is made.

SOURCE also confirmed a callback fallback regression in the draft reader change:
status-only arbitrary callbacks lost the original preliminary read pass. That pass,
order and original spiritual-conflict companion are restored for both modes. A
new false-mode invalid-skill callback control joins the existing two retained-menu
controls. Its causal run will use a freshly compiled test assembly against the
known pre-fix game DLL A852D00EE8ABB331EF90BD73F3728AC44B30FE81C50C0CDD8F3268EAA1276D02.
The exact two pre-fix source files match the original portable PDB document SHA256
hashes, and the PDB GUID matches the DLL CodeView GUID. Current fixed source is not
represented as already tested by that old binary. Full production rebuild follows
the causal run; both source/evidence variants remain outside Git as needed.


2026-11-04 History causal controls and source repair (local only):
The corrected privacy fixture and final lexical controls passed 98/98 (63 envelope,
35 projection), wall14.2744009s, complete cleanup. The original three privacy
assertions were not weakened; only their invalid initial diagnosis DATA was made
conformant using the existing builder.

Fresh test-only compilation against the verified old A852D00E game DLL/PDB yielded
the expected callback RED: two original retained-menu controls passed; only the
new false-mode invalid-skill control failed its named availability oracle. Three
complete, wall10.8311179s, cleanup complete. The ordinary plain case then reached
genuine v3/stabilize/IV-to-I History, factory clock/foreign-preparation controls,
all eleven exact-join refusals and both invalid-suffix controls; only the final
PHYSICAL_HISTORY_ARCHIVE_CAUSAL positive failed. One complete, wall32.4609364s,
cleanup complete, no timeout. Its composed (not published) capture and TRX remain
in /tmp/boe-wound-history-6007-20261104/callback-suffix-red/.

The source repair separates exact linked terminal joins from current-settlement
latest-row requirements. Historical display verifies the full unchanged
legacy/archive suffix; the guided current wrapper still requires the heal child
to remain latest. The original fallback pass is restored in both reader modes.
These two repaired sources are currently UNBUILT/UNRUN; fresh production build
and bounded GREEN remain required. A separate genuine ordinary procedure adverse
fixture is prepared for source review, not yet run. Browser and A5 remain UNKNOWN.


2026-11-04 fresh History repair verification (partial, not full acceptance):
Independent SOURCE approved the historical suffix repair, unchanged strict current
wrapper and genuine adverse policy fixture. Full fresh unit build:0errors/86warnings,
81.56s; integration:0errors/108warnings,54.41s. Main assembly SHA256
65B4645B9CA6D41AC857DE69225C19616F0CD2F9EF909A870445261EC7DE0950.
Frozen source/build manifest, catalog and nine-descriptor143-case PlanOnly are in
/tmp/boe-wound-history-6007-20261104/history-repair-green/.

GREEN139/139:63 strict envelope,35 projection/privacy,36 read,3 callback and2 menu
controls; wall38.8605499s, complete selection and runtime cleanup, no duplicates.
The new read control measures eight shared source reads exactly once per opening,
observes valid changes on the next opening, then refuses malformed current DATA.
This establishes operation-limited reuse, not an end-to-end speedup.

Plain IV-to-I/stabilize and guided independent-root then passed2/2 in69.5960074s,
both existing60-second leaf bounds preserved, cleanup complete. The plain positive
includes genuine minute, eleven exact-join refusals and supported archive suffix;
the captured output is composed, not published.

Adverse first attempt failed before History at the coordinated resource probe,
not at a History oracle: procedure still had ProvisionalHeld despite empty resource
claims. One FAIL,wall44.5479945s,cleanup complete; fail-fast left the other two
leaves unrun until their separate successful run above. Source confirms ordinary
procedure requires command persistence then confirmation even for not_required
resources. Only the adverse fixture now uses the existing typed stage/readback/
confirm/ConfirmedHeld sequence and the same response for common Compose. All
previous assertions remain; this fixture-only correction is independently SOURCE
approved and awaits fresh compilation and one bounded execution.

The existing strict current-removal regression failed before RequirePublishedAsync:
normalizer returned null transaction and the original IsType assertion preceded
its issue assertion. One FAIL,wall54.5285364s,no timeout,complete cleanup. Its full
passive capture closed404214 rows/21726615 compressed bytes with footer and digest
receipts; source/artifact diagnosis is underway. This does not establish a failure
or acceptance of the changed strict current helper. No repeat is yet claimed.
Adverse, strict current, and actual joint coverage remain pending. A5 and browser
remain separately UNKNOWN. No commit or push.


Subsequent SOURCE diagnosis narrows both integration fixture failures:
- current-removal is guaranteed without resource cost, so the production normalizer
  correctly publishes on the ordinary branch without a coordinated transaction.
  The fixture now checks normalization issues/plan agreement first, then follows
  the real ordinary progression Validate/Apply branch. Transaction-requiring modes
  retain their previous advance/complete/dispose and later current/Finish checks.
- adverse stage/confirm correction compiled (0errors/108warnings,53.43s) but the
  next attempt failed in4.08s of test execution/11.4840635s wall before confirmation:
  its extra byte parser did not accept the normal writer's UTF-8 BOM. Exact byte
  equality is retained; a BOM-aware StreamReader now decodes those same in-memory
  bytes without another source read. This was a test transport error, not History
  or production rejection. Cleanup was complete.
Both latest fixture deltas await SOURCE/fresh build and bounded runs. The production
assembly and all prior successful History evidence are unchanged.


2026-11-04 current boundary GREEN and historical comparator design correction:
The fixed existing current-removal fixture passed1/1,wall63.9585943s, unchanged
120-second leaf limit and full cleanup. It reached actual published guided
RequirePublishedAsync, the shared Finish consumer and all existing refusal checks.
Closed capture431044 rows/23203060 compressed bytes; actual gzip/decoded hashes
and footer independently matched. Production SHA remains65B4645B...7DE0950.

Adverse subsequently passed genuine coordinated admission, common composition
(32.907s), actual physical History, terminal-before I, effects/identity and archive
suffix. Its metadata counterfactual then failed: a changed outer binding with a
recomputed outer seal remained parseable. One FAIL,wall48.2586468s,cleanup complete.
This is not a violation of the receipt contract: receipt fingerprints cover
declared operations, not the original prepared policy/intents. The previous test
assumption was wrong. The original seven-field genuine capture is preserved in
/tmp/boe-wound-history-6007-20261104/adverse-mechanics-green/ without enrichment.

A reviewed narrow strengthening is now under TDD: closed v3 additionally retains
the genuine outcomePreparationFingerprint and compares it with the already
reconstructed preparation. Default binding is independently derived from sealed
request coordinates. The comparator detects reviewed metadata-only reseals while
it remains unchanged; it does not authenticate a fully re-sealed data graph.
SourcePath cannot independently be reconstructed from request coordinates. Policy
intents depend on it; no-policy outcomes do not necessarily do so. The genuine
factory remains the source of captured provenance. Raw terminal and guided v2
formats stay unchanged. No production implementation or new acceptance is claimed
yet; cheap typed codec RED precedes the change. The final joint producer is held
until this contract is stable. A5/browser remain UNKNOWN; no publication.


Preparation-comparator TDD: test-only unit build succeeded0errors/27warnings28.86s
against unchanged65B4 production.75 cases completed in20.9365846s with67PASS,
seven expected causal FAIL (six valid8-field positives and required-new-field
acceptance), plus one unrelated test Int32-to-Int64 mutation error. That constant
was corrected to1261L; it is not called causal RED. Cleanup complete. The new raw
policy INPUT is a labelled detached lexical fixture, not an enriched old capture.

Minimal production delta is now local in PersistedResult.History only: capture
the supplied genuine preparation fingerprint; v3-only deterministic default
binding/event comparison; compare the already-reconstructed preparation with the
retained comparator. No additional outcome reconstruction, resource pipeline or
source read is added. Raw and guided2 formats remain unchanged. This new source
is not yet built/tested; final SOURCE and bounded GREEN remain pending.


## 2026-11-04 bounded physical History result (local, no publication)

Current Main DLL SHA256 is
16F9400164381927623B7D60C484CD0EC83B64F665CFC335A319F189FD0D6676; application,
unit and integration copies agree. Fresh unit build:0errors/87warnings80.13s;
integration:0errors/108warnings53.95s. Final source/build manifest and copied
assembly hashes are in /tmp/boe-wound-history-6007-20261104/history-final-green/.
Independent review verified all50 manifest entries at the execution freeze, all38
changed compiled C# document checksums, PDB-to-DLL identifiers and the copied
detached INPUT fixture. Only this checkpoint and tasks closeout text were updated
after that freeze; source and binaries remain unchanged.

Final named bounded execution, with all original leaf budgets unchanged:
-151/151 codec/projection/read/callback/menu controls,wall45.9805519s.
-4/4 actual adverse/plain/current/guided-independent leaves,wall163.7039920s.
  Category elapsed42.973/26.080/53.762/35.302s within60/60/120/60s.
  Genuine ordinary captures have eight fields, exact provided preparation hash,
  initial III/IV and terminal-before I; both are composed_not_published DATA.
  Adverse metadata-only binding/source changes with fixed inner/preparation now
  fail strict parse. Plain eleven refusals and supported archive suffix pass.
  The genuine published guided current result reaches RequirePublished/Finish
  and all preserved refusal controls. Full passive gzip:431044 rows, complete
  paired scopes, no pending/failed/gap/late events, complete closing and cleanup.
-Nonterminal joint Linux producer1/1,wall88.6420751s within its existing5-minute
  category; phase-reader1/1,wall10.8595520s within its existing120-second category.
  Both completed with full cleanup. No matching current Linux record existed
  before the one authorized producer. The new external record pins this exact
  Main SHA and archive66B7DFF01E87F9F2C5B953EAF0F289C8687D14C15A9CCB4E83418119351FCA5B.
  The ZIP has83 unique members, all CRCs and82 payload length/digests valid. Its
  real nonterminal treat row contains the eight-field v3. Reader uses unchanged
  binaries and the real generated archive, not rewritten old Windows DATA.

Total157/157 across eleven named descriptors; no skips, underfill, duplicate test
IDs or timeout in these final runs. Evidence for the separate joint pair is in
/tmp/boe-wound-history-6007-20261104/joint-nonterminal-v3/. The record/ZIP is inert
observed DATA, not a capability or recovery qualification. This is nonterminal
joint coverage, not terminal-joint/A5 acceptance. Final independent SOURCE,
OUTCOME and closeout review accepts all157 unique passing cases within this
bounded scope, including the exact generated archive, phase-reader trace and
fresh completion/marker guards. No broader acceptance is inferred.

Optimization assessment: existing opening-owned DATA maps share eight actual
source reads exactly once per opening, with changed/malformed next-opening and
arbitrary callback refusal controls. Existing History/effect indices and the
within-comparison compiled graph are reused. Fresh ownership/current checks stay
fresh. The retained preparation comparison adds no second outcome reconstruction,
source I/O or payment pipeline. Conservative private frozen reconstruction remains;
no whole-opening single-compilation or end-to-end speedup claim is made.

Scope boundaries remain: full T151/T152 and the entire wound feature are not closed.
Terminal-joint/A5 stays UNKNOWN; the earlier browser procedure outcome/cleanup
stays UNKNOWN after the denied poll, which was never retried. No schema fallback,
old-byte rewrite, invented historical minute or full-reseal authentication claim.
Console isolated acceptance and browser cheap controls retain their original
source/provenance boundaries. No commit, push or publication.

Local trees at this checkpoint (all pre-existing work preserved):
-wounds-local: b43726ec7494e4c7600733ca31330212201a4a41;19 dirty paths,8 untracked.
-wounds-a5-aggregate: same b43726ec7494;12 dirty paths,3 untracked.
-wounds-stage-profile-6007: non-Git staging copy; not treated as a checkout.
-wounds-terminal-capture-6007:6007e82d304f90931a846cad14a4859e40faac58;9 dirty,4 untracked.
-wounds-terminal-capture-profile-6007: same6007e82d;15 dirty,7 untracked.
-wounds-text-surfaces-6007: same6007e82d;47 dirty,13 untracked. This combined
 console/browser/History tree does not include the separate terminal-capture cache
 proposal. The two older b437 trees were not reset or treated as6007 equivalents.

## 2026-11-04 guided-v2 null-minute refusal (separate local block)

Tracked under #1536/US5. An otherwise valid closed guided-v2 envelope with
currentMinute=null escaped ParseGuided as NullReferenceException instead of the
existing mortal_wound_guided_publication_result_invalid issue. The minimum fix
uses RequiredHistoricalMinute once after the unchanged shape/seal guard and
passes that validated local value to reconstruction. No catch broadening,
coercion, default minute, schema change or new fallback. Raw/v2/v3 serialization,
terminal/witness/receipt checks and live authority boundaries remain unchanged.

Independent test SOURCE and implementation SOURCE accepted. Causal RED on the
previous Main16F9400164381927623B7D60C484CD0EC83B64F665CFC335A319F189FD0D6676:
86 executed,85 PASS and exactly one null-minute NullReferenceException; all
seven neighboring malformed-type/range controls and the three typed0/1260/
Int64.MaxValue controls passed. RED wall19.3056867s, complete selection and both
owned/runtime cleanup flags true. This was a parser failure, not fixture setup.

Fresh affected unit-project build:0 errors,23 warnings,49.80s. Final app/unit
Main SHA256749B52E78CD6A389C1E37FB330B8AC5AAD573F6C160CE8C86367A1CC39209F86.
ValidateCatalog passed with no stale/unmapped entries; fresh PlanOnly selected
exactly one descriptor and86 cases. Addressed wound-treatment-history-envelope
GREEN86/86 unique cases, wall19.8442302s/category14.4986791s under the unchanged
60s category limit, no timeout/duplicates, complete selection and both cleanup
flags true. Final independent SOURCE/OUTCOME and closeout review accepted this narrow block.

Evidence is local-only at
/tmp/boe-wound-history-6007-20261104/guided-minute-guard/: baseline file/hash
snapshot, red and green build/plan/run logs, actual TRX/summaries and source/build
manifest. Of the previous38 manifested C# sources, only Guided.cs and the
existing HistoryEnvelopeTests.cs changed. The earlier157/157 acceptance remains
bound to its frozen16F940 source/build; it is not a fresh157-case run on749B52.
Integration binaries were intentionally not rebuilt or reused as current proof.
Only checkpoint/tasks text is updated after the narrow green execution manifest.

Optimization assessment: a scalar check on the already parsed object is cheap;
reuse its validated local minute, with no cache, additional source I/O, owner
lifetime or outcome reconstruction. Tests use independent detached lexical DATA
and existing typed production parsing, not GameEngine/long integration setup.
No speedup claim. The existing category covers the changed parser plus raw/v2/v3
and seal/witness refusals; unrelated heavy consumer runs are not justified.
This restores an existing client-owned malformed-DATA refusal, so no GM-authored
field, gameplay rule, prompt or worked example changes are needed.

All six local trees and prior experimental work are preserved. A5/terminal-joint
and browser outcome/cleanup remain UNKNOWN; no denied poll or heavy scenario was
repeated. Full T151/T152/US5/#1536 is not closed. No commit, push or publication.

## 2026-11-04 Mortal equal-name target menu — bounded local acceptance

US5scenario4/FR073 and finite remainder row5 require player-visible target
distinction without technical identities. Independent read-only audit found that
BuildMortalDiagnosisMenu creates identical available Name/Context rows for distinct
current NPCs, bypassing the existing afterlife public-context completion. Actor
canonical projection validates unique IDs, not unique display names. Both UI
consumers forward these labels; console numbers are not contextual identity.

Initial actual OpenAsync causal RED on Main749B52:2/2 completed, distinct-name
control PASS and identical-public-context row FAIL only at target availability
after canonical actor/current location/visible output/inertness premises. Wall
11.1396251s, no timeout, complete owned/runtime cleanup. Evidence outside Git:
/tmp/boe-wound-history-6007-20261104/mortal-target-names/red/. The later expanded
test-only gate adds different public roles and fresh role-drift refusal, plus
existing normalization/Self controls. That gate used unchanged Main749B52.
Its actual integration3 gave distinct-name PASS and two causal failures only at
ambiguous availability/public-role label equality. Fail-fast left shared19 UNRUN
in that combined invocation; the separate unchanged-source shared19 then passed
with full cleanup in8.9447095s. No complete22 RED or added gameplay failure claim.

Chosen minimum design: extract/reuse the existing per-menu public name/context
completion, preserving Self first and FormKC/OrdinalIgnoreCase behavior. Reuse one
index over already loaded, validated exact current scene NPCs. Only ordinary NPC
detail's non-debug fields role/occupation, shortDescription/description and
appearance may enter public context, using existing plain-text/whitespace policy.
Do not read plans/history/personality/appearanceDescription or invent traits.
Bind the resulting public context into the existing ephemeral target fingerprint
so a fresh contextual change invalidates an old selection. Identical public
contexts remain visibly unavailable; different public roles can distinguish them.

A separate policy or IDs/ordinals as distinguishing labels was rejected: existing
shared behavior already supplies the required safe outcome. Blanket refusal of
all equal names was rejected because the normal NPC card may expose lawful public
differences. No actor-authority/schema change, persistent cache, new I/O, lifetime
extension or GameEngine setup. One source read per initial Open is instrumented;
no elapsed speedup claim. Selection is the new actual Mortal menu leaf plus the
affected cheap Chaos target-contract owner. Provider-method labels are a separate
consumer and are not claimed repaired. Prior157/86 retain their source boundaries;
A5/browser UNKNOWN and no-publication instruction remain unchanged.

Implementation and independent SOURCE/OUTCOME accepted: existing shared completion
is used by the Mortal target menu and unchanged-semantic Chaos/Shining callers.
One per-menu index derives only the public whitelist from validated NPCsInScene;
all other actor/source/admission logic remains unchanged. An exact public-context
change invalidates the prior target fingerprint before selection. This is a
client-owned display adapter over existing NPC fields: no GM-authored schema,
mechanic, command or example changes are required.

Final fresh Unit build0errors/87warnings85.19s and Integration0errors/108warnings
57.95s. All three Main copies are SHA256
3D927D21C57A6823441BEDAACBB8DEB6392B2DE8339E4853764FFB386A7E5EA1.
ValidateCatalog valid with no stale/unmapped entries; fresh PlanOnly exactly
2descriptors/22cases. Actual addressed GREEN22/22 unique cases, all executed:
Mortal real-menu3 plus shared target-contract19, wall18.6386213s, category
5.055543s/3.8169808s under unchanged60s limits, no timeout/skip/duplicates, complete
selection and owned/runtime cleanup. Private marker/coordinate absence, Self-first
order, same-name safe refusal, public-role distinction, changed-role fresh rejection
and first-Open NPCreadcount1 all pass. Captured canonical JSON byte/existence
invariance excludes .boe_runtime bookkeeping; it is not whole-filesystem inertness.

Full source/build freeze and original results are outside Git under
/tmp/boe-wound-history-6007-20261104/mortal-target-names/green/. All59 entries
(51 changed/untracked sources plus8 binary/PDB files) match before documentation
closeout. Only checkpoint/tasks text is updated afterward. Active text tree now
has51 dirty paths/14 untracked, still detached6007e82d; other five trees are untouched.
The previous157 cases remain at16F940 and86 at749B52; neither was rerun or silently
reattributed to3D927. Provider-method labels are not covered by this target-only
correction, and no native/DOM/browser-course/A5 acceptance is inferred.
Final independent narrow closeout-documentation review accepted this bounded block.
No commit/push/publication.

## 2026-11-04 Mortal provider names — bounded local acceptance

The preceding22-case target acceptance excludes provider-method labels. Before
this correction, Self selection listed physical-treatment and diagnosis choices
using only provider.DisplayName, even if that NPC was ambiguous in target navigation.
This violated the same FR073 player-visible distinction in a separate consumer.
Independent audit confirms the source path and genuine guaranteed-provider
capability fixture strategy; no behavior is inferred from target availability.

Plan: cheap independent canonical providers with distinct skill identities, actual
Open→SelectSelf→Confirm; treatment/diagnosis each cover identical public context,
different public roles, distinct names and a separate changed-name refusal that
reaches old Confirm independently of contextual-label failures. Prove requirement/capability authority
before the named output oracle, hidden identity/privacy and canonical JSON
byte/existence inertness. Then minimally complete target DATA before building
choices, reuse its exact-owner map for provider label/ambiguity, and compare the
physical/diagnosis choice's actual shown name during fresh Confirm. Preserve
self/player identification, all requirement/source/owner gates and selection
codecs. No full provider fingerprint: it would bind unrelated provider wounds;
no label-only dispatch or guessed identifiers.

Optimization assessment: reuse the one operation's already loaded/validated
source DATA and existing public completion; no extra source read, persistent
cache, second parsing or Engine/original setup. Current Confirm/staging continue
to open fresh data. First-Open NPC read count and exact JSON inertness are tested;
no speedup claim. New bounded provider leaf plus affected target3, not unrelated
shared-policy/heavy/A5/browser runs. Client-owned display adapter only; no new
GM-authored mechanic, schema, command, example or persistent receipt field.
Baseline files/hashes and RED/GREEN evidence remain outside Git under
/tmp/boe-wound-history-6007-20261104/mortal-provider-names/. No publication.

Actual causal RED on unchanged Main3D927 completed8/8:2 distinct-name controls
PASS and6 intended failures only at ProviderNames.cs91/99/147 (each treatment and
diagnosis). Wall13.7206018s, category8.0886087s/60s, no timeout/skip/duplicates or
underfill, both owned/runtime cleanup true. Initial6-case compilation was never
executed; final8-case fresh test-only build0errors/108warnings53.90s, catalog valid,
PlanOnly1descriptor/8cases. Actual RED and fixture independently accepted.

Minimal three-file producer/Confirm SOURCE accepted: target order is preserved,
exact-owner public DATA map is local to one menu, self/player wording stays
independent, physical ambiguity gate follows course availability calculation.
Confirm compares shown physical/diagnosis name alongside existing exact selection
and fresh availability; nonphysical branches and persisted codecs are unchanged.
Fresh Integration full build and addressed provider8 + affected target3 GREEN completed;
independent PREP/SOURCE/RED/OUTCOME accepted within this bounded scope.

Actual GREEN11/11 unique IDs/names,2/2descriptors complete: provider8 plus target3.
Wall20.0788788s, categories9.2832777/5.2294656s under unchanged60s limits, no
timeout/skip/duplicate/underfill; both owned/runtime cleanup true. Fresh full
Integration build0errors/154warnings111.62s; catalog1415/13358 valid, exactPlan11.
Main app/integration SHA256
5C5979B47ECAA9FDFD93DEDB94F07D513A4A65519BF714DBFA485DAFA8931310.
The unselected Unit Main remains3D927; no fresh unit qualification is claimed.

Tests reach genuine Open→SelectSelf→Confirm: two valid named providers, public-role
distinction, ambiguity refusal before selection, exact provider preservation, and
stale displayed name/role plus newly ambiguous state refusal without canonical
JSON changes. First-Open NPCreadcount1 remains proven. JSON inertness excludes
.boe_runtime bookkeeping. Selection/display DATA grants no treatment authority.
Staging/current owner/publication mechanisms remain source-unchanged; this run
does not execute an original, Engine, treatment payment/publication or native/DOM.

Evidence /tmp/boe-wound-history-6007-20261104/mortal-provider-names/green/ contains
actual build/catalog/plan/run logs, TRX/summaries and62-entry freeze (54 changed
source paths plus8 selected binary/PDB artifacts). Independent review verified
all62 before documentation closeout; only checkpoint/tasks change afterward.
Current active text tree has54 dirty paths (39tracked/15untracked), detached6007e82d;
all five other trees remain untouched. Previous157@16F940,86@749B52 and22@3D927
retain their own boundaries; the overlapping target3 is not added as new unique
coverage to that22. A5/terminal-joint/browser UNKNOWN and broader US5/T151/T152
acceptance remain unchanged. No commit/push/publication. Independent narrow closeout-documentation review accepted.

## 2026-11-04 Mortal resource requirement privacy — bounded local result

The next independently audited scenario is a player selecting a visible NPC's
known physical wound. WoundPlayerProjection.Resource permits owner_visible when
OwnerRole=target, even though that target is not the player. It prints the private
resource name and declared quantity, not a current balance. Existing contract
specs/1543-unified-resource-authority/contracts/resource-projection-and-cutover.md
section2 permits owner_visible only to the owning player's view. This is finite
US5 privacy/T115/T152 work, not a new resource or gameplay rule.

The same display boundary is called from card methods/diagnosis and executable
physical common requirements, course milestone conditions, selected costs and
diagnosis costs. Fixing only the card would leave adjacent disclosures. Proposed
minimum: supply the exact target and, where actually selected, provider owner to
the shared requirement renderer, using existing IsSelf coordinates and existing
resource projection visibility semantics. Recommendations have no known provider
and cannot infer one. Preserve Self/player_visible positives and generic unknown
text for foreign owner_visible, hidden and gm_only. No balance, authority, resource
availability rule, codec or persistent schema change. Do not broaden to unrelated
item or NPC privacy policies.

First obtain cheap independent projection RED, then a file-light genuine current
Open→SelectTarget menu RED reaching card and choice descriptions. No GameEngine,
original, payment or publication is needed. Reuse parsed catalog/current DATA;
owner-aware display formatting is trivial and does not warrant persistent cache.
Expensive parse, requirement resolution and fresh permission fences stay intact.
Measure actual initial menu source reads and preserve canonical JSON bytes and
existence. Private diagnostics stay outside Git under
/tmp/boe-wound-history-6007-20261104/mortal-resource-privacy/.
Client-owned privacy formatting only; no GM-authored field/mechanic/example change.
All earlier local work and versioned acceptance boundaries are preserved.
A5/browser UNKNOWN remains; no publication or denied-operation retry.

Actual baseline RED completed on unchanged Main5C5979: existing projection owner
with new8 gives43 total,42PASS/1 expected foreign owner_visible tuple failure at
ResourcePrivacy.cs141; wall10.2623173s. The separate actual menu8 gives5PASS/3
expected aggregate ownership failures at integration ResourcePrivacy.cs164;
wall12.1824814s. Both runs complete their exact selection with no timeout, skipped
cases, duplicates or underfill and both cleanup flags true. The two provider-role
failures prove that actual player-owned requirements were incorrectly hidden as
well as the foreign-target disclosure. No earlier fixture failure. Unit build
0errors/41warnings29.99s, Integration test-only build0errors/108warnings50.79s.
Fresh catalogue and combined PlanOnly were2descriptors/51cases; RED ran as two
separate complete invocations to avoid fail-fast hiding the second result.

Minimal production delta now passes exact owner coordinates through all listed
renderers and reuses ResourceProjectionService.IsVisible unchanged, exposed only
as an internal pure helper. The only per-provider work moved is small display
formatting; route parse, simulation, window computation and requirement authority
remain where they were. The two existing narrow resource-audience controls are
selected alongside projection43 and actual menu8 for GREEN, not resources-state
aggregate or unrelated earlier target/provider/heavy acceptance. Independent
RED OUTCOME and implementation SOURCE review accepted; the fresh final
preparation and GREEN results are recorded below. xUnit truncates the printed aggregate failure list, so
RED is not a complete emitted per-surface violation inventory. A passing final
aggregate must check every named surface; no extra RED rerun is warranted.

Final addressed GREEN: 53 unique cases passed, all 3 descriptors complete.
Actual menu 8 plus existing visibility-policy 2 plus projection 43; wall
23.9985933 seconds. Category times were 6.8605581/60 seconds, 3.2151979/60 seconds
and 4.024314/180 seconds respectively, with original budgets unchanged. No timeout,
skipped cases, duplicate IDs or underfill; owned-tree and runtime cleanup both true.
The empty final aggregate verifies every named card/common/milestone/cost/diagnosis
surface; it does not claim a per-surface diagnostic event stream.

Fresh Unit and Integration builds succeeded with 0 errors, 87/108 warnings and
93.46/55.04 seconds. All three Main copies have SHA256
AF6380F12FF4366B692A86272FB158124B52A94F8C598387E0338933E758F87F.
Catalogue 1417 categories / 13360 methods is valid with no unmapped or stale
selectors; the completed PlanOnly was exactly 3 descriptors / 53 cases.
Independent PREP accepted all 70 frozen source/build entries before execution.
They still match after execution; only checkpoint/tasks text changes at closeout.

Actual initial Open reads the NPC source once in each menu case. No extra I/O,
parse or authority revalidation was introduced by the display helper; trivial
audience-dependent strings are formatted for the actual provider. No persistent
cache or speedup claim. Availability and all mutable-authority boundaries remain
unchanged. The menu fixtures intentionally have no allocated pool for the added
resource, so they prove declared-condition display on disabled choices, not an
executable treatment, balance, payment or publication. Canonical JSON bytes and
existence are unchanged by views; .boe_runtime bookkeeping is outside that oracle.

Evidence is local-only under
/tmp/boe-wound-history-6007-20261104/mortal-resource-privacy/: baseline snapshots,
RED and GREEN logs, actual TRXs/summaries and the 70-entry freeze (58 changed source
paths plus 12 selected DLL/PDB artifacts). The active tree is still detached at
6007e82d with 58 dirty paths (41 tracked / 17 untracked); the other five trees and
the separate cache experiment are preserved. Earlier accepted 157@16F940,
86@749B52, 22@3D927 and 11@5C5979 retain their own source/execution boundaries.
This 53 includes existing projection cases and is not 53 new distinct feature
cases on top of those receipts. Full US5/T151/T152, terminal-joint/A5 and browser
acceptance remain open or UNKNOWN as recorded. No denied poll, heavy retry,
commit, push or publication. Final independent OUTCOME and narrow
closeout-documentation review accepted.

# T120/US5 public afterlife provider names — source-confirmed bounded gap

After resource-privacy closeout, independent read-only reconciliation found a
separate current FR073 consumer: two living visible local Shining faction healers
with different exact identities but equal names, tiers and prices generate equal
available paid treatment labels for Self. ProjectVisibleRoster preserves their
names; ProjectCurrentCurrencyServices forwards them; BuildPublicServiceChoices
constructs labels independently of target completion. ForTarget(Self) retains both.
The existing same-name roster test proves identity separation, not readable choices.

Selected scope is public services only. Reuse localized public role/tier from the
existing roster pass and existing normalized name/context completion on admitted
service providers. Do not infer service authority from patient-target eligibility,
especially for visiting Elyara. Remaining indistinguishable providers are disabled;
distinct public context remains usable. Fresh Confirm must compare the shown service
label because quote equality does not bind provider display names. Quote formats,
pricing, payment, source admission and publication guards stay unchanged. Private
aid has separate retained-grant conditions; multiple simultaneously valid equal-name
aid offers were not proven and are not claimed defective or fixed here.

TDD plan: fresh per-case small Shining service DATA plus actual Open/SelectSelf/
Confirm, current admission and quote premises before the missing-display oracle;
same-name/same-context, public-tier distinction, distinct-name and fresh name or
ambiguity drift, with exact private bindings and canonical inertness. Independent
fixture review precedes bounded RED, then minimal production and addressed GREEN.
Existing Elyara menu controls cover the directly affected public-provider neighbor.
No Engine, payment publication, denied browser poll, A5 retry or full suite.

Optimization: carry the already projected public role/tier from the caller-owned
roster pass; no extra I/O, source reconstruction or persistent cache for these cheap
strings. The current read owns DATA only; confirmation and staging keep fresh
source/authority checks. Test mutable roots are independent per case. Runtime is
UNRUN at this source/design point. Previous accepted blocks retain their own
version boundaries. All changes remain local; publication is not authorized.

Causal RED completed on unchanged AF6380 Main: five unique cases, distinct names
PASS and four expected failures only at the final stale-display, ambiguity and
context oracles. Actual wall 10.8131766 seconds; category 5.2824279/60 seconds.
Both source reads occur once on initial Open. All quote, admission, privacy and
whole-tree inertness premises pass; all five owned roots are actually absent.
Selection complete, no skip/timeout/duplicate; owned and runtime cleanup true.
Independent SOURCE and actual TRX/summary RED OUTCOME accepted. Test-only Integration
build after the strengthened fresh-availability premise: 0 errors, 108 warnings,
52.91 seconds. Local RED sources/binaries and source/build hashes are retained at
/tmp/boe-wound-history-6007-20261104/public-provider-names/red/.

Minimal production delta is three files: ShiningFactionHealingSupport.Services
carries DescribeRoleAndTier from its existing public roster projection;
WoundApplicationService.Service reuses normalized CompleteTargets display policy
for independently admitted services, then conjoins its availability with unchanged
wallet eligibility; WoundApplicationService.Confirm also compares the shown name
for paid services. Transient display rows are never exported as targets or treated
as locality, wound or publication authority. Selection and quote formats are intact.
This is client-owned display/fresh confirmation behavior over existing public DATA,
not a GM-authored capability or schema; no GM prompt/example change is required.
Final production SOURCE, fresh affected builds and the new five plus existing
Elyara-menu/Shining-access controls remain pending; no GREEN is claimed yet.

Independent production SOURCE accepted the three-file delta. Initial full Unit
build then ended with MSB6006, csc.dll exit 137 after 181.66 seconds, before any
GREEN execution. This is a build failure, not a behavioral RED. Observed /tmp RAM
filesystem pressure (4.6/4.9 GiB used; about 1.1 GiB available memory) is consistent
with resource exhaustion, but the exit code alone does not prove the kill cause.

The already closed joint-nonterminal-v3 evidence was copied to
/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/joint-nonterminal-v3.
All 17 files, 1,926,753,914 bytes and directory membership were verified by complete
SHA256 against the source and rechecked before removing the reproducible RAM copy.
The previous /tmp path is a symlink to that verified disk copy, so existing evidence
references remain readable. The complete copy manifest is beside the disk directory
as joint-nonterminal-v3-copy-manifest.json, SHA256
b12554110df590049441a1904576d65864add87032291a0df382fed53f6d158a.
No unique evidence, code, fixture or game save was discarded. /tmp then had 2.2 GiB
free and available memory about 5.5 GiB. The failed build was not interrupted and
its original log remains; only fresh affected builds are retried after this change.

Fresh retry builds succeeded: Unit 0 errors / 87 warnings in 83.10 seconds,
Integration 0 / 108 in 54.34 seconds. The three selected Main copies share SHA256
FD84A848086A0B85A4B4E84AD039ABD1A22A72775CE2950E7D0C2D1D8E5AF802.
Catalogue 1418 categories / 13361 methods is valid; exact PlanOnly has 3 descriptors
and 51 cases. Independent PREP accepted all 73 frozen source/build entries and
the test body is byte-identical to causal RED.

Addressed GREEN: all 51 unique cases passed in 22.4039535 seconds: actual public
provider menu 5, existing Elyara menu 11 and Shining access DATA 35. Category times
are 5.0246691, 3.655244 and 3.4128984 seconds respectively, each within its unchanged
60-second budget. All 3 descriptors completed; no skipped, duplicate, underfilled
or timed-out cases. Owned and runtime cleanup both true; all five actual menu
fixture roots are absent. All 73 frozen entries still match after execution.
Only checkpoint/tasks text changes during closeout; no extra runtime is required
for this note. Final independent OUTCOME and narrow closeout-documentation review accepted.

The actual menu proves readable public-tier distinction, safe same-context refusal,
exact quote preservation and confirmation of valid distinct choices. Roster-only
name changes leave quotes equal, but an old displayed choice now refuses; a newly
ambiguous provider also refuses. Initial Shining/roster source reads remain one
apiece; views and confirmation leave the owned tree unchanged. No performance
speedup claim, current quote/payment publication, private aid grant, native UI,
DOM, broader faction-name context or whole FR073/US5/T151/T152 qualification.
Existing A5/terminal-joint and browser UNKNOWN boundaries remain unchanged.

Evidence remains local at
/tmp/boe-wound-history-6007-20261104/public-provider-names/: baseline snapshots,
causal RED and frozen baseline binaries, failed and successful build logs, exact
catalog/plans, three GREEN TRXs/summary and the 73-entry source/build freeze.
Current active detached 6007e82d tree has 61 dirty paths (43 tracked / 18 untracked).
Previous accepted 53@AF6380 and earlier blocks retain their own execution/source
boundaries; this 51 includes existing controls and is not 51 new feature cases.
No commit, push, denied poll or heavy retry was performed.


# US4 Shining combat self-healing prerequisite — source feasible, runtime UNRUN

The eight original US4 scenarios were reconciled against current source and recorded
acceptance. Existing Chaos combat, safe self/healer, failed-session natural recovery,
persistent-owner recovery/progression and consenting-entity paths are implemented.
Initial IV0 persisted twenty/four-cycle chronology remains an evidence limit, not a
new arithmetic defect. US8 already has its accepted six-scenario map above; no new
skill-scope branch or repeated matrix is justified.

US4.2 / FR-046 is broader than the accepted initial Chaos combat slice. Current
menu, combat staging, scheduler, binding/spend and receipt explicitly refuse Shining
combat self-healing. The initial design documents that bounded exclusion, not a
change to the original full-story requirement. Read-only independent review traced
an existing generic Shining-capable raw validation, realm-derived OD composition,
common normalizer and final conflict validation route. A new general combat system
is not implied. The prior Shining source-only test ends without a validated plan
and does not supply the physical Shining state required by final mode validation.

Next bounded T095-SHINING-PUBLISH-GATE: initial canonical active Shining state,
soul/profiles/conflict and rebuilt resources before the signed snapshot; one real
ordinary pressure publication with both OD6-to-3, exact exchange and final zero
errors. Only after these premises, open the actual treatment menu for the original
known player wound and check an available combat choice. An earlier setup or
publication failure is not a healing RED. Guards and all production remain unchanged
until the existing complete route is proved. Independent fixture review precedes
execution; one new 60-second singleton, no broad inherited ten-minute category.

Optimization: reuse the existing typed canonical wound fixture and owning publisher,
with independent mutable state and initial DATA only. No Engine, new cache, duplicate
publication pipeline or extra validation framework. Runtime/cost/cleanup are UNRUN.
The existing FD84A848 baseline and all previous local changes are preserved; no
publication or held browser/A5 retry is authorized by this block.

The prerequisite was executed on unchanged FD84A848 production after independent
SOURCE acceptance. Fresh test-only Integration build: 0 errors /108 warnings,
53.95 seconds; catalogue valid1419/13362 and exact PlanOnly1/1. The single case
failed only at its intended available-combat-choice oracle (line84), after actual
ordinary publication, both persisted Shining OD6-to-3, resource History, final
conflict validation, unchanged original wound/History/schedule, readable self/H3
and whole-tree menu inertness all passed. Wall12.6351767s, category within60s,
no timeout/duplicates/underfill; finally root absence and both runner cleanups pass.
Independent actual PREP/RED OUTCOME accepted. All73 manifest entries match; six
physical frozen copies retain two sources and Main/Integration DLL/PDB, not73
physical copies. This proves an existing complete generic route and missing entry,
not healing staging/C4 or new conflict startup. Evidence: local shining-combat-healing/red
under /tmp/boe-wound-history-6007-20261104/. No repeated run is needed.

Next authorized scope is the same standard-art player-self direct-duel healing in
ordinary active Shining, through existing staging and C2/C4 only. Actual realm
must bind soul/conflict/schedule/intent/source/wound/spend/receipt/current replay.
Closed formats and fingerprints retain their fields and old Chaos meaning; no new
framework, foreign target, unwilling aid or safe cycle. Pending/sealed/package,
backlog/catch-up, stale source and cross-realm substitutions must remain refusals.
Independent design review checks exact current scheduler semantics before editing
production. Causal actual staging-to-C4 fixtures precede that change; no current
GREEN or complete Shining healing is claimed.

Actual staging/scheduler RED is now independently accepted on unchanged FD84A848.
Fresh test-only Unit build: 0 errors /27 warnings,23.36s; Integration: 0 errors /
108 warnings,54.11s. Catalog valid1421/13371; plan exactly14 unit cases and2 native
cases. The first invocation stopped after scheduler13PASS/1FAIL (only settled
Shining positive,line19),14.5485609s; it correctly reports incomplete combined
selection and did not execute the pair. The separate pair invocation executed
both cases and failed only at actual Stage.IsStaged,line129,with
spiritual_combat_treatment_source_invalid. Both canonical Shining/scheduler
premises, whole-tree refusal inertness and callback disposal/root absence passed.
Pair wall10.9915728s, original120s category limit, complete2/2,no timeout/duplicates,
both runner cleanups true. Independent PREP/OUTCOME verified manifest74/74 and
frozen15/15. Shining-negative baseline passes do not prove semantic causality;
C2/C4 remained unreached. Evidence is shining-combat-healing/staging-red beside
entry RED; earlier immutable captures were not overwritten.

The current eleven-file production delta has independent SOURCE acceptance:
actual canonical realm is propagated through existing intent/source/binding/Spend/
receipt; raw schema1 shapes remain unchanged. Shared Guardian/Resident contours
must remain settled in both realms; Shining's three active contours must equal
the shared ordinal while inactive Chaos may only lag within bounds. Current
Shining must be an actual parsed object, active (case-insensitive per current
contract) and without an incarnation package. Existing captured roots serve
staging/original; current replay and menu read the actual mode, and active-menu
read failures fail closed. No capability is inferred from a paid amount or DTO.
All existing C4 original/current/physical guards remain intact.

Optimization assessment: the stage already reads Shining among its scheduler
sources, so the new mode check reuses those roots and no second parser or cache
was introduced. Menu uses its existing operation-owned Root reader and replay
requires one fresh current-mode read because original bytes do not establish
current availability. Cheap independent scheduler/intent/mode DATA precede the
necessary cold native pair. New negative controls reuse each test's own genuine
baseline, restore exact bytes, and never share mutable fixture state. No speedup
is claimed. Added fixture and GM synchronization review/build/GREEN are pending;
no current Shining healing outcome or broader feature acceptance is claimed.

First post-change cheap gate caught a codec regression before any native execution:
3PASS/1FAIL across three completed descriptors (20.0453035s), then fail-fast left
realm22 and every native case unrun. Both guidance cases passed; the failed
existing program test reached Intent.Create and reported a missing control realm.
Root cause: ProgressionControl.CurrentRealm is serialized as currentRealm by its
JsonPropertyName attribute; the new guard incorrectly looked for PascalCase.
The model already defaults to Chaos Sea, so an earlier empty-default assumption
was also incorrect. The exact correction changes only both intent reads and the
new negative mutation to currentRealm; an explicit serialized-key premise guards
against recurrence. Independent targeted SOURCE review accepted this correction.
The first green/manifest95/frozen37 records 5E6CA496 and is retained as failed
attempt evidence, not the corrected source. Fresh corrected builds and cheap24
are required before native work. Guidance2 keeps its own unchanged-source receipt;
no unrelated guidance or heavy scenario is repeated merely to hide this failure.

Corrected cheap gate accepted independently: realm22/program2 all PASS,24 unique
cases in2 complete descriptors,11.7074943s, exit0/no timeout/duplicates and both
cleanup checks true. Main7FBE29D4, Unit9EFF86FF; fresh builds Unit50warnings/0errors
73.41s and unchanged-source Integration incremental0/0,1.52s. Corrected catalog
valid1421/13373 and plan5descriptors/33cases. The separate native run then executed
all3 cases: actual menu PASS, both pairs failed at later independent boundaries,
31.5650542s, complete cleanup. This menu result is accepted on its7FBE source,
not a completed healing publication.

Blocked pair reached actual C2 SubmitDecision and correctly refused two authored
proposal effect definitions still carrying the old Chaos-only fixture default.
The minimal fixture correction changes only detached consequenceDefinitions[].
definition.allowedRealms to the exact before.Owner.Realm, before submission, after
asserting that owner's realm. Signed original and canonical afterimages are not
rewritten; the production graph validator remains unchanged. Healed pair reached
C4 accepted-output binding, where WoundPlayerNotification still hardcoded the
Chaos player owner. Independent diagnosis confirms that this is a separate real
wound-only consumer defect. Its correction uses actual insertion.Input.Binding.Realm,
requires one of the two canonical afterlife realms and preserves exact player
owner, parsed result and complete stage sequence checks. The full native healed
body additionally checks the genuine single published notification and /раны link.
No new author rule or general combat framework is needed.

These findings were isolated before another run. Next affected selection is the
same Shining pair2 and the still-unrun existing Chaos6; accepted cheap24/menu1
remain separate7FBE receipts and guidance2 remains its unchanged-source5E6 receipt.
Current source/build evidence for this last bounded correction is green-output;
new runtime remains pending. Earlier green and green-corrected artifacts retain
their failures and exact version boundaries without being overwritten.


## T095/T102/T103 ordinary Shining combat self-healing: bounded final acceptance

2026-11-04: independent final SOURCE and OUTCOME accepted the original bounded
player-self, standard-art healing route inside an existing ordinary Shining direct
duel. Actual menu entry, staging, C2 decision, C4 publication, durable replay and
accepted notification are now covered. All previous local changes are preserved;
no commit, push, remote recovery claim or broader feature closure is made.

Final affected Integration build has 131 warnings / 0 errors, 99.18 seconds.
Catalog validation is complete (1421 categories / 13373 methods-files); PlanOnly
selects exactly two descriptors / eight cases, each under its existing 120-second
category bound. Main SHA256 is
31a23df3246c5cae742ebad4fecc8de865d8ed0117ea6d633a772f716a462ae1;
Integration SHA256 is
ad3a4c13d7989b3d6296c06b9e689cc2fc240d30e4f01154dde7d761a66c9f17.
The final manifest has 96 entries and 38 physical frozen files, not 96 copies.

- Shining pair: 2/2 PASS, wall 30.4447637 seconds, category 24.3570915 seconds.
  Real staging/C2/C4, same-realm Spend/receipt/History and current replay pass.
  Healed output is exactly one genuine notification with the /раны link; blocked
  treatment remains paid with no healing stage and actual I-to-II counterharm.
  Seven actual staging refusals and four current-mode replay controls retain
  exact restore/inertness and current authority boundaries.
- Existing Chaos chronology controls: 6/6 PASS, wall 49.9600678 seconds, category
  44.56957 seconds. Healed, no-improvement, blocked, guaranteed counterharm,
  dependent-cost correction and publication rollback retain their assertions.
- Both final invocations have exact complete selection, no timeout, duplicates or
  underfill, and both owned/runtime cleanup checks are true. Actual owned fixture
  roots are absent. No further test execution is needed for this bounded delta.

The accepted receipt union is 35 unique PASS / 7 descriptors, not one run and not
35 tests on the final binary: guidance2 remains on Main5E6CA496, cheap24 plus
actual menu1 on Main7FBE29D4, and final native8 on Main31A23DF3. Every descriptor's
TRX/hash/version and invocation boundary is recorded in accepted-receipts.json.
Failed Pascal-key and authored-realm/output attempts remain retained and excluded
from that count; partial invocations are not described as complete aggregate PASS.
Independent review checked the actual TRX, summaries, manifest and cleanup.

GM contract synchronization covers the wound materialization guide, afterlife
matrix, CLI specification/operations and step prompt, daemon guidance, worked
Shining example, example manifest, focused contract guard and existing design
appendix. The unchanged schema1, cost5/minimum2, original dice, paid blocked
outcome and no-safe-cycle rule remain explicit. No new author rule or general
combat/OD framework was introduced. Optimization assessment remains as above:
reuse captured source DATA and menu reader; one required fresh replay-mode read,
asserted by the negative controls. Final category timings are measured costs,
not proof of a general speedup.

Closed evidence is retained outside the checkout at
/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/shining-combat-healing.
The adjacent shining-combat-healing-copy-manifest.json records all 259 files,
794301011 bytes, full membership/length/SHA256 verification and accessibility;
its final SHA256 is
fc3186ce3ca888b8c2d968764a85f2d396ea269a2a6a3b158860f3eb2a5d5ce9.
The old /tmp/boe-wound-history-6007-20261104/shining-combat-healing path is a symlink
to that directory. Only the completely verified duplicate was removed after all
runs ended; no unique evidence, fixture, save or source was deleted. At closeout
/tmp has about 2.1 GiB free and the workspace disk about 15 GiB free.

Local trees remain: wounds-local and wounds-a5-aggregate at b43726ec7 with
11 tracked/8 untracked and 9/3 respectively; wounds-stage-profile-6007 is non-git;
wounds-terminal-capture-6007 and wounds-terminal-capture-profile-6007 are at6007e82d
with5/4 and8/7; active wounds-text-surfaces-6007 is at6007e82d with66/20. These counts
include existing work and are not a claim that this block changed all those files.

This closes only the named ordinary Shining player-self slice. New conflict
startup, foreign/unwilling targets, special healing arts, full Engine/UI and all
US4/US5/FR073 are not qualified. A5, terminal-joint and denied browser boundaries
remain UNKNOWN; T151/T152 and #1536 are not closed. Initial IV0 persisted
twenty/four-cycle chronology is still an evidence limit, not a demonstrated
arithmetic defect or an automatic new implementation backlog. Next action is
read-only reconciliation of the original finite wound plan with existing receipts
to identify one concrete remaining user scenario before any further implementation.


## T120/US5 Shining patient names — source gap and bounded plan

Independent read-only reconciliation found an original US5.3/.4 defect: current
Shining resident targets all receive the same generic context, so two equal public
names are disabled even when the ordinary visible roster distinguishes their
roles. WoundTargetSelectionService:54–61/123–131 is the affected patient consumer;
accepted paid-provider51 is independent. Admission already proves visible current
faction/hall, living materialized local profile and exact resident membership, and
includes residentRole in its fingerprint. Both existing roster renderers expose
the localized role; no hidden summary, ID or guessed description is needed.

Plan: use the existing per-test patient fixture without Engine, seed equal names
before its sole resource bootstrap, prove both exact admissions and public roster
roles, then call real OpenAsync and SelectTargetAsync. Cover different roles,
remaining ambiguity, distinct names and stale role. Only after intended RED carry
the public localized role through existing admitted DATA and shared completion.
Reuse the already resolved resident row; no second roster/materialization pass,
new source read, persistent cache or treatment authority. Fresh selection still
reopens source and checks existing exact binding. Addressed shared target and
Mortal controls plus existing Shining read-once controls bound regressions.
All implementation/runtime remains UNRUN at this planning point; no heavy journey,
held browser/A5 run or publication is part of this block.


The new patient-menu RED completed exactly4 cases: distinct-name and identical-role
ambiguity controls PASS; public-role availability and fresh changed-role display
fail only their intended final oracles (lines116/112). All exact admission/visible
roster/wound/privacy/read-count/inertness premises pass; the stale case also proves
the existing current binding already refuses the old selection. Wall11.1916648s,
original60s category, no timeout/duplicates/underfill, both runner cleanups true
and owned fixture-root inventory unchanged. Independent source review accepted
the fixture. No production behavior was changed before this RED.

Fresh test-only Integration build:109warnings/0errors53.76s on SDK8.0.427/runtime
8.0.33, unchanged Main31A23DF3; the one new nullable fixture warning is corrected
with Assert.IsType for the already required faction object before GREEN. Initial
CLI HOME/XDG and missing SDK10 targeting-pack setup attempts never ran tests and
are retained separately; no SDK10 or Windows qualification is claimed. All97 RED
source/build manifest entries match. Evidence is directly on disk outside checkout:
/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/shining-patient-names.

Minimal implementation now carries the catalog-localized public role from the exact
already-validated resident row in the operation-owned admission into target context.
The raw role remains in the existing admission fingerprint; no schema, permission,
consent, lifecycle or current-source check changes. No extra roster validation,
materialization, source read, cache or provider gate is introduced. A source-only
review and addressed GREEN30 (new4, current-menu4, shared-target19, Mortal-target3)
remain pending. This client-only text completion reuses already public roster
wording; the GM command/request/result and authored mechanics do not change, so
no new GM example or prompt rule is required.


### Shining patient navigation — bounded accepted result, 2026-11-04

Independent SOURCE, PREP and final OUTCOME accepted this two-file display correction.
The same-name player-visible residents now remain selectable through distinct
known roster roles. Identical public context still refuses; role drift invalidates
the old selection and a new opening shows the new role. Actual Open→SelectTarget
reaches the exact known patient detail without consent or treatment authority.
Privacy, current binding, read-once, radiant-head, Chaos and Mortal controls pass.

Fresh owning builds: Unit87warnings/0errors80.18s; Integration131/0 96.28s. The sole
review correction was an XML summary on the new internal DATA property, applied
after the initial Main compilation. Integration rebuilt that exact final source;
final Unit incremental synchronization0/0 1.69s brought all three Main copies to
SHA256 8dd1df07a307c3c65fa339fcfeadd90e49234207e7015bbad74d793112d47fa8.
There is no clean-warning or SDK10/Windows claim. The nullable fixture assertion
is stronger and its behavioral oracles unchanged from RED.

Catalog1422/13374 is valid; exact PlanOnly selected4descriptors/30cases and executed
none. The sole addressed GREEN invocation then completed all30 unique IDs/names:

- new Shining patient names4: category6.2277438s /60s;
- existing Shining resident/radiant current-menu4:6.0309274s /120s;
- existing Chaos/shared target contracts19:3.8154857s /60s;
- existing Mortal target-name controls3:5.4694632s /60s.

Total wall32.9153073s, exit0, no skip/duplicate/underfill/timeout,4/4 descriptors
complete; both owned/runtime cleanup flags true and the owned fixture-root
inventory is unchanged. All98 source/build hashes and physical frozen files match
after execution. These30 include26 existing controls, not30 new features. The
previous combat35 acceptance remains versioned separately and is not rerun or
merged into this count. No further tests are needed for this documentary closeout.

Exact command: scripts/test-csharp.ps1 with -SelectionFile pointing to the retained
shining-patient-names/green/selection.json, -NoBuild and -Parallelism1. Original
category budgets were unchanged. Summary SHA256:
5e7a54f898c0272c7a571d82f0a62597524483d05c45e9251257fe0a3a9dde49.
Raw build/environment-attempt logs, RED/GREEN TRXs, plans, source/binary freezes and
bounded diff are directly retained outside checkout under
/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/shining-patient-names.
The final evidence-inventory.json enumerates157 files excluding itself,310331118
bytes, with per-file size/SHA256; inventory SHA256:
5b8221e199bf5c8c5a1bd19f794a328b0551218b1380e022f01d3257b66df2ed.
No unique artifact or source was deleted and no extra RAM copy needs removal.

Optimization review accepted existing operation-owned resident DATA reuse: only
one fixed localized string is retained, no new IO, roster/materialization pass,
provider gate or cache. Current admission/fingerprints are untouched. Actual source
counts remain one read per selected root at Open and another at Select; no general
speedup is claimed. GM mechanics/schema/commands and existing public roster wording
are unchanged; this client-only correction needs no new GM rule or example.

Only checkpoint/tasks change after the final98-entry freeze; other96entries remain
unchanged. Active wounds-text-surfaces-6007 remains detached6007e82d with67tracked
and21untracked dirty paths, including all earlier work. The other five local tree
HEAD/status counts recorded above are unchanged. No commit/push or remote-save
claim. This accepts only current navigation, not actual Engine/DOM/provider,
treatment/payment/C4 or full US5/FR073/T151/T152/#1536. A5, terminal-joint and the
denied browser run retain their existing UNKNOWN boundaries.

## T097/T105–T107 US4.5/.6 — initial IV0 persisted chronology plan

The original scenarios and SC-009 require uninterrupted H0 twenty-cycle and H5
four-cycle healing from critical/progress0. Accepted arithmetic52 already proves
the numbers; accepted actors/overflow prove two later cycles and IV7/IV14 seeds,
not either complete IV0 persisted chronology. No new production defect is known.

Bounded plan, approved for preparation on2026-11-04: add two independent Facts in
AfterlifeSpiritualNaturalRecoveryTests.InitialCritical.cs. Reuse the real existing
fixture once per Fact, PrepareLaterNaturalCycleAsync and PublishPreparedCycleAsync;
never reseed an intermediate after-image or reuse the fixed turn43 report. Rewrite
the current explicitly authored graph response every cycle, including empty graphs.
Verify the current signed manifest/control/projection/receipt before the next turn,
immutable History prefixes, exact wound/effect/identity joins, one tick per cycle,
unchanged resources, terminal on the required last cycle, inert first/terminal replay and
one empty following cycle. No Engine, extra numerical calculator or production edit.

Execute H5 alone under120s after independent SOURCE/fresh Integration build/catalog/
PlanOnly; assess its complete outcome and costs before admitting H0 under300s. No
automatic retry or budget increase. Existing two-cycle peers took32–43s, not a
measurement of twenty cycles. Two bootstraps,24actual publications+2empty cycles;
fresh test build estimated1–2minutes. All new execution remains UNRUN here.
Evidence goes directly outside checkout to local-evidence/wound-history-6007-20261104/
initial-iv-recovery-chronology under the workspace; disk15GiB and/tmp2.1GiB free at
planning. Full per-cycle evidence and passive aggregate section costs have no
record/byte cap; no full timing-event or speedup claim. Mutable cycles require
fresh sources/owners, so cross-cycle caching is inappropriate. Existing stable DATA
and graph helper are reused; no new cache or repeated world bootstrap. No GM runtime
contract changes are intended; no new GM example is required for test-only coverage.
Original A5/browser/terminal-joint/whole-feature limits and all prior local work stay.

Independent Sol6.1/xhigh SOURCE accepted the new fixture and120/300s categories
before execution. It reuses exported PublishedSpiritualCycleData and effect identity
DATA instead of rereading/reparsing the same validated roots, and preserves scenario
plus cleanup failures in the external outcome. Per-cycle passive scopes allow
H0 cost admission. Expected history totals are H5 five recover+one heal+one seed
create=7; H0 twenty recover+one heal+one create=22. Natural result counts are4/20.
The first integration compile108warnings/0errors53.97s belongs to pre-review fixture
source, not runtime evidence. Final reviewed build and exact discovery are pending.

### Initial-IV chronology — H5 accepted, H0 UNKNOWN at original bound

Final reviewed Integration build108warnings/0errors54.49s; unchanged Main SHA256
8dd1df07a307c3c65fa339fcfeadd90e49234207e7015bbad74d793112d47fa8. New fixture
52f2995a792c93ea0b1853a80188cab09833c35d0ce8e96bf69e7ce565bb3db3 is bound to actual
Integration DLL ea72a9d31c574a8021db792af8cc03a193c76e7909a7ec6edeec3bd72e22a6e7
by its matching portable PDB GUID/document digest. Independent PREP verified all102
source/build pins and7 physically frozen changed files. Catalog1424/13376 valid;
separate PlanOnly selections each1descriptor/1case, no execution.

H5 sole actual run:1/1 PASS, category42.6119938/120s, wall48.5828023s, test38.174171s.
Independent OUTCOME accepted exact IV0→IV6→III4→(II4→I0)→healed; four natural results,
five recover rows plus one heal and one initial create=7 History rows. Every old
row is unchanged, the next before is the actual prior final, and exact signed
request/snapshot/projection/control/current settlement, resources, effects and
identity agree. First and terminal retries are inert, the fifth empty cycle adds
no History. Both runner cleanups true and the owned fixture root is absent. All
passive scopes closed,0open/late; setup0.898s, preparation1.354s, publication35.037s,
current witnesses0.170s, assertions/capture0.083s, replay0.191s, empty cycle0.347s.
Nested inclusive costs are not added. Cycle costs8.562–9.499s led to conditional
H0 admission around190s, explicitly an extrapolation with growing-History risk.

H0 sole actual run reached its original300s category bound:300.0919361s, wall
306.2048662s, exit124/TimedOut,0/1 completed. No TRX or fixture finally/outcome exists;
there is no failed assertion, twenty-cycle PASS or complete named-cost capture.
Fifteen complete per-cycle snapshots preserve actual IV0→III0 at8→II0 at14→II1
at15,16 History rows/15 natural results/no terminal. These are valid observed
prefix DATA, not acceptance. Snapshot completion intervals grew from about7s to
19s by cycles8–9; these are wall observations, not named inclusive timings. The
complete H5 remains a separate accepted result and does not qualify H0.

Runner process/runtime cleanup flags are true and no live dotnet/pwsh/testhost
was observed, but fixture cleanup did not run. Original root is retained:
/tmp/boe-spiritual-healing-lifecycle-8af4541024dc4771aa7296ca665c07db.
Its41files/1399594bytes were copied read-only to the external evidence's
 tier-0/preserved-fixture; full source-before/source-after/copy size+SHA inventories
match. The original is not deleted. No final-state validity or completed cycle16
is inferred from this interrupted fixture. All102 source/build pins matched after
the run; only this checkpoint/tasks closeout changes subsequently.

All raw logs, plans, current snapshots, source/binary pins/freezes and the retained
fixture copy remain outside checkout at:
/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/initial-iv-recovery-chronology.
No production changes, budget increase, repeated run, commit or push. H0 remains
UNKNOWN. Independent Sol6.1/xhigh H0 outcome classification and documentary closeout
are accepted, not gameplay acceptance. Next allowed work is
read-only classification of the observed growth from existing evidence/source,
then reconciliation of another concrete original wound scenario, not an automatic
optimization or a new heavy run. Full US4/T097/T105–T107/#1536 and A5/browser stay open.

Final external inventory:110files excluding itself,63608977bytes; SHA256
5dbf7e9e1850b3bb8b3354b769bf1667f80f36a7c923896761b99ea369618ace. Independent
review verified every file, plus retained original/copy fixture membership and SHA.
Active tree remains6007e82d at67tracked/22untracked; all other five local trees retain
their prior HEAD/status counts. No raw output entered Git.

Read-only cost classification, independently checked: normalized History payload
in the retained H0 snapshots grows24490→95551→189208→314607→332774bytes at cycles
1/4/8/14/15. Completion intervals at4/8/14/15 are10.576/19.112/31.373/31.289s.
This is not established test-only overhead: real GameEngine.SessionAndSnapshots:269
calls the same publication preparation via CanonicalStateNormalizer.MortalItems:1759
and ValidationService.SpiritualNaturalRecovery:98. The spiritual reducer parses the
complete before-History, validates retained+new rows and reparses after-History
(WoundAcceptedTurnPlannerCore.SpiritualHealing:68/95/98); WoundHistoryState:235/243
and TransitionResults:250 reconstruct typed old results, whose codec recomputes
its stage program. This is a plausible production growth mechanism, not measured
causal attribution. H0 lacks named aggregate timings, so H5's publication fraction
cannot be transferred to it. No wrong wound result or quantified Engine/player
latency defect is established. Record shared-production performance/qualification
risk only; no mandatory cache project, larger budget or retry follows.

### T081-D guard-success acquisition consumer — bounded plan

Independent source/evidence reconciliation establishes one direct FR-025 gap:
SpiritualWoundMechanics:71-87 permits failure-only strain no-op for successful
player pressure/maneuver but not guard. ValidateGuardRule:4454-4464 explicitly
prohibits successful guard strain worsening; IsSuccessfulArtOutcome includes
success/partial_success. Original/current strain comparison readers only accept
setback, so they cannot repair this no-op. Accepted guard-cap covers setback only;
accepted pressure/maneuver success and real III-to-I retirement remain unchanged.

Plan: one new four-case, independent-fixture category under60s. Each positive
first proves a lawful two-exchange signed ordinary source, selects one actual III
strain/guard wound via current C2, and requires completed/cold-reopened candidate
with exact ordinary strain/tempo/costs and no second artificial source/decision.
Negative controls retain ordinary successful-strain-worsening and missing-tempo
refusals. Retarget the existing sibling cost burden away from guard; never bypass
ordinary cost admission. No production change before causal RED. Then only the
confirmed existing-predicate extension, GM example/guard and directly affected
GREEN, with independent review. No new outcome table, opposing-actor inference,
aggregation, deferred effects, general combat or C4/Engine run.

Preparation reuses established minimal signed context and current typed owners,
not GameEngine or a new cache. One bootstrap per independent case; no shared
mutable state. Current owner/source/effect validation stays fresh, parsed packet
DATA is reused within assertions. Expected cost from similar C2 cases is under
one minute per four-case category, not measured acceptance; build roughly1minute.
No limit increase or automatic retry. Evidence is outside checkout at
/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/spiritual-strain-guard-success.
At planning disk15GiB and/tmp2.1GiB free. All runtime is UNRUN; previous work stays.

Guard-success preparation/RED: first fresh Integration108warnings/0errors54.57s,
Main8dd unchanged. Initial catalog accidentally requested automatic restore/build
and failed at0.63s with0warnings/0errors/no test and unchanged binaries. Correct
-NoBuild discovery against the fresh assembly is valid1425categories/13377methods,
no stale/unmapped. Exact PlanOnly1descriptor/4cases, no execution.
First actual4:2negativePASS, one genuine success RED at integration_required and
one partial-success fixture failure at ordinary exchange_no_state_delta. The
latter is not a production RED. Category8.69527/60s, wall14.3487143s, completeTRX,
no timeout/duplicates/underfill, both cleanup flags true and no new fixture roots.

Independent review identified/accepted the minimal input repair: partial success
authors permitted overwhelmed-to-fractured improvement, satisfying the existing
nonblocked semantic-delta rule without creating harm. No rule or production change.
Fresh corrected Integration108warnings/0errors53.84s and exact PlanOnly retained.
Corrected four-case RED: both legitimate successful guards fail solely at
integration_required after genuine ordinary/sourceIII/C2 and nonmutation premises;
both ordinary negatives PASS. Category10.9355172/60s, wall16.6026018s,
complete uniqueTRX and cleanup. All109 pins unchanged; no fixture root remains.
Exact frozen source and portable-PDB/DLL binding are retained for both versions.

Minimal production delta now adds only guard to the existing successful-player
operation predicate. IsSuccessfulArtOutcome and subsequent ordinary/source/failure
branches are unchanged. Guide, worked example, manifest and typed guidance guard
are synchronized; no new schema/GM authority or Mortal contract. Constant-time
existing predicate introduces no IO, repeated computation or cache. The reviewed
GREEN selection is new4+pressure1+maneuver1+guidance1+capDATA12=19, five existing60s
leaves. Prior C4 is not rerun: setback remains false under the unchanged success
predicate; cheap guard-proof regressions are DATA only, not new C4 acceptance.
The two new invalid controls are ordinary pre-admission negatives, not wounded-
current negative lanes. Independent Sol6.1/xhigh whole SOURCE/GM synchronization and corrected causal RED
OUTCOME accepted. Fresh builds and targeted GREEN remain pending.

### Guard-success actual GREEN — local closeout

Fresh affected Unit build50warnings/0errors73.34s includes the new Main; subsequent
Integration build0warnings/0errors1.28s is successful incremental build/copy, not a
new Integration compile. Corrected fixture binary854e5d42/PDB05aa5473 remains bound
to sourceb05060cd. Main SHA256
2dfc4cd016dca5157abe57cf4844382db004d7221ffeeed91efd938335d79c4a matches all three
Main/Unit/Integration copies. Unit95a114d2 contains the updated guidance guard.
Portable PDB document digests/GUIDs bind all three changed C# sources. Independent
PREP rehashed109pins and20frozen files, and confirmed actual GM consumers read the
exact checkout through BOE_REPO_ROOT, not an obsolete content copy.

GREEN19/19 PASS in one actual invocation,5/5descriptors,55.182219s wall, exit0.
Exact leaf times/budgets: new guard4=14.8751453/60s; pressure1=12.2536116/60s;
maneuver1=10.2485961/60s; guidance1=4.0189177/60s; capped-guard DATA12=3.4397273/60s.
All five TRX contain19unique IDs/names, no skipped/timeout/duplicate/underfill.
All109source/build pins remain unchanged after execution. Both runner cleanup
flags true, no new or remaining resource-fixture root and no live test processes.
Summary SHA256 f06c489efba245a0dbcbdc70ef2d6afdee601f358734f252963fdcce37ef1a4b.

The two new positives actually reach private completion and cold reopening, retain
the exact ordinary second exchange/strain/tempo/costs, one acquisition and one
materialize choice, without canonical writes or an invented second offer/none.
Ordinary-invalid negatives remain pre-admission controls. This qualifies only the
selected III player acquisition/C2/cold route. Prior C4, root-retirement and IV
DATA receipts stay separate; no new C4/Engine, full profile, realm matrix, general
combat, H0/A5/browser or whole-feature acceptance. No production API/schema/new
cache, user outcome rule, hidden actor inference or aggregation was introduced.

All evidence remains outside checkout under spiritual-strain-guard-success at the
path above. Final inventory107files/280823786bytes (excluding itself), SHA256
ce4b3b020cfe33bf2dd8f767b4c45ac1e27948f64030ce44155ebed4f5f992cd. Active tree
remains6007e82d at69tracked/23untracked; other five trees retain their prior
HEAD/status counts. Only checkpoint/tasks change after the109pin runtime freeze.
Independent Sol6.1/xhigh final OUTCOME accepted all19actual cases and the exact
cleanup/provenance limits above. Independent final documentary closeout accepted
the complete inventory, six tree states and exact source/binary freeze boundary.
No commit or push.

## T081-D-SUCCESS-FAMILIES: confirmed successful acquisition consumers (2026-11-04, local)

FR-022/025 requires strain to affect failed operations only. The current player
success path previously handled pressure, maneuver and guard. Independent source
and evidence reconciliation found genuine signed producer precedents for binding,
force_binding and recover_spiritual_power. This block covers all six exact
success/partial_success rows for those three families. Counter, break_binding,
force_incarnation, incarnation_resistance and champion_coordination remain
UNPROVED here for both successful outcomes; registry membership and partial
ordinary-rule examples do not qualify them. They are not added by analogy.
Previously accepted III-to-I consequence removal/retirement remains separate.

Existing-parent pressure/setback remains a technical gap. Resetting its ordinary
strain to before would be unsound: unlike maneuver, pressure can already lawfully
increase own strain. Before clear, ordinary strained requires wounded fractured;
resetting to clear could wrongly accept strained. An overwhelmed/broken cap case
cannot distinguish this. A retained genuine ordinary baseline is needed. No new
author rule, fabricated preimage or widening of the existing guard is used.

**Fixture and causal evidence.** Each positive first proves complete signed
ordinary admission, two checked exchanges, exact claims 0–3, one owned III source,
no pending requirements and the exact ordinary exchange. Real C2 selection and
cold reopening must then preserve it with one choice, no artificial source/none
and canonical nonmutation. Binding uses player tier 3 seeded in canonical soul
and profile before signing, ready leverage, explicit initial none control and a
one-step hindered result. Force binding keeps two restrictions. Recovery uses
zero-cost player gains 3/2, opposing gain 3, and unchanged strains. Three negatives
check missing binding control, narrow force-binding payoff and false recovery gain.

Independent SOURCE approved fixture
597591806e71babccdd5c70bfc46c7cc9e596ec2791983c5f3435bc9cd42edbb.
An earlier 69F942 fixture built in 51.98s (108 warnings, 0 errors) but executed no
tests. Review then replaced recovery's anonymous seeder with the existing named
trusted seeder. Corrected Integration build took 50.50s (108 warnings, 0 errors);
ValidateCatalog reported 1427 categories / 13379 methods with no stale/unmapped
entries. Exact PlanOnly selected two descriptors / nine cases.

The initial RED invocation failed fast after binding: six cases executed, two
negative PASS and four causal FAIL solely at line 155's C2 completion oracle with
integration_required. All lawful-source, ownership, dice and nonmutation premises
passed first. Binding took 16.3010743s of 60s, wall 21.8393029s. That invocation
remains incomplete 6/9. The never-started recovery leaf ran separately: three cases,
one negative PASS and two identical causal FAIL, 10.8925522s of 60s, wall 16.6970178s,
complete 1/1. Together these are nine unique executions, not a repeated selection.
Both invocations have complete TRXs, no timeout, both cleanup flags true and no
remaining fixture roots. All 110 source/build pins were unchanged at each close.
Independent actual PREP/RED OUTCOME accepted. Main 2DFC4CD0 was unchanged;
Integration 22F38439 / PDB 7D76286C bind the exact corrected fixture.

**Minimal correction and GREEN.** The sole production delta adds those three
names to the existing successful-player predicate. Exact actor/operation matching,
current owner/effect, ordinary validation, success classification and every
failure/correction branch remain unchanged. GM guide, worked example, manifest
and typed guidance guard are synchronized. There is no new schema/control surface,
author outcome or general combat framework; existing guide/example consumers need
no new matrix or launcher entry. Independent final SOURCE accepted exact source
cf8904da9dbcca611877d7f121102814e80287733f6e64b0d860debc93ebc8ee.

Fresh Unit/Main build: 71.86s, 50 warnings, 0 errors. Successful Integration
incremental build: 1.34s, 0 warnings/errors. Integration retained the corrected RED
22F38439 / 7D76286C binaries and loaded the new Main; this is not a new Integration
compilation. All three Main copies equal
83b4f0c340dcff28117d6c00b9370ce6ebbd4d90f9650859de1f309183d5a585.
Unit is 47EB7ECC. Three source/PDB document digests and CodeView GUIDs were
independently verified. Exact PlanOnly selected 26 cases / five descriptors;
independent GREEN PREP accepted.

Actual GREEN: **26/26 PASS**, five/five descriptors complete, wall 69.4889256s,
exit 0, no timeout. Original 60s limits remain: binding 6 took 20.5198493s; cap DATA
12 took 3.8260675s; guard 4 took 15.6614357s; guidance 1 took 4.2264103s; recovery 3
took 15.0721021s. Five TRXs contain 26 unique IDs/names, no skipped/duplicate or
underfilled cases. All 110 pins remained unchanged; both cleanup flags are true,
with no fixture roots or live test processes. Summary SHA:
58ab38572052f7c0743dd85fdc169968223b4ed4eefdfc322ac63f6aa8767ae6.
Independent final GREEN OUTCOME and documentary/inventory closeout accepted.
Only checkpoint/tasks changed after the runtime source/build freeze.
The new negatives prove ordinary pre-admission refusal. Cap 12 is DATA, not new
live C4 evidence. Positive completion is completed_unpublished, not publication.

**Optimization and retention.** The token predicate performs no new IO or
reconstruction and needs no cache. Recovery uses the existing immutable prepared
fixture service: key = seed version, ordered signed dice, exact initial balances
and signing tuple; hooks/custom callbacks are excluded. Each case gets its own
independent copy and fresh current owners, leases and cold C2 read. Binding's
custom pre-sign authority intentionally stays fresh. No mutable owner is retained,
no cache framework is added, and no new computation-count or speedup claim is made.

Evidence is outside checkout at
`/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/spiritual-strain-success-families`.
Pre-reuse build, corrected REDs, GREEN, source/build manifests, PDB proofs, actual
logs/TRXs and cleanup summaries are separate. Inventory: 109 files excluding the
inventory, 323200246 bytes; exact membership/readability/size/SHA verified.
Inventory SHA: 8747fd3c3f77b6b01bf602afae834cbb2c561cdbe6edd4de2222bcaee67fab2c.

All six trees are preserved: wounds-local at b43726ec7 (11 tracked / 8 untracked),
wounds-a5-aggregate at b43726ec7 (9/3), wounds-stage-profile-6007 non-git,
wounds-terminal-capture-6007 at 6007e82d3 (5/4),
wounds-terminal-capture-profile-6007 at 6007e82d3 (8/7), active
wounds-text-surfaces-6007 at 6007e82d3 (69/24, 93 paths). Only the active tree changed.
Free space: /tmp 2.1 GiB, workspace 15 GiB. No evidence deletion or publication.
No full profile/FR025, C4/Engine/UI, five unproved operations, existing-pressure,
H0/A5/browser or overall wound completion is claimed.

## T081-D-RESPONSE-SUCCESS: genuine player response gates (2026-11-04, local)

The next FR025 pass examined the five remaining successful operation families.
Counter, break_binding and incarnation_resistance have explicit current-rule
success/partial_success semantics, but no prior complete signed-source/C2 receipt
was found. Force_incarnation and champion_coordination remain outside this block:
known force producers use authority/no_effect or Guardian-loss contexts, while
champion coordination requires an allied lead and a supporting soul. The current
wound player actor is the actual leadContestant, not a supporter selected by a
sideModel label. Neither missing route is invented here. No stronger independent
wound defect was established by this pass; the suspected guard/tempo conflict is
already handled by the existing owned-tempo grant guard, with prior evidence.

Independent source tracing established that a canonical pre-sign control is the
legitimate original before-image. SourceAdmission reads the verified snapshot,
starts from its controlState and requires exact continuous current before/after
snapshots. It does not require locating controlId's historical birth exchange.
The fixture therefore seeds an actual supported opposition bound control before
signing, preserves it through the first harmful pressure, and removes it in the
next successful break_binding or incarnation_resistance. The latter starts from
force_incarnation control, not ordinary binding; current incoming pressure remains
legal because the actual existing forced control is removed. This is one supported
resistance variant, not a requirement for every resistance operation.

Counter uses a distinct lawful baseline: tier1 already signed in both soul and
player profile makes cost3 affordable after the first pressure spends6->3; tier0
would cost4. Incoming pressure and contested->player_advantaged provide real payoff
without another wound. Break/resistance tier0 cost3. Both strains stay overwhelmed/
clear after the first real III source. Each positive checks two complete ordinary
exchanges, exact dice claims0–3, a single owned III source and no pending requirement
before the C2 oracle. Actual signed control/tier DATA is read from the existing
source owner's ReadOriginal API, not inferred from the live draft or reread through
a new snapshot owner. First control carry and second before are exact.

One new theory has ten independent rows: six positives (three operations × two
successful outcomes), counter without payoff, break without a control change,
resistance clearing ordinary binding (both required diagnostics), and a foreign
second-before controlId. Negatives are ordinary pre-admission checks. The positive
oracles require current and cold C2 completion with the exact ordinary exchange,
one choice/no artificial source or none, exact final control/strain/position/cost,
and canonical nonmutation. This is completed_unpublished, not C4 publication.

Independent SOURCE approved fixture
A1419E435C91CD8D4D262E58118FC0988E22A262B326286C1C60873362DAB1D4.
Fresh Integration build:51.46s,108 warnings,0 errors. Catalog1428/13380 valid with no
stale/unmapped entry; exact PlanOnly1 descriptor/10 cases/60s. Main83B4F0C3 stayed
unchanged. Integration3A65CB72/PDB819EBA6D bind the fixture source and matching
CodeView/PDB GUID93FE34C5. Actual RED executed10/10:four negatives PASS, six causal
FAIL only at line165 integration_required after all original/source/III gates.
Category18.712686/60s,wall24.1785557s,exit1/no timeout; complete1/1, no skips or
duplicate IDs/names. All111 source/build pins remained unchanged, both cleanup flags
true, no fixture roots or live test processes. SummarySHA
4c3625eaa83af7e7e9e7c8562d01982b73ec91ff56c6474e54add0fb19d54f76.
Independent actual PREP/RED OUTCOME accepted.

The production correction adds only counter, break_binding and incarnation_resistance
to the successful-player no-op. Actor/operation/current owner/success classification,
ordinary validation and every failure branch remain unchanged. SourceSHA
baf0908a52307ca0cb746f7e7b05ab32d99aec918be07bfb12ff88dddbc9cd82.
GM guide/worked example/manifest and the existing typed guidance guard are updated;
no new control/schema/entrypoint/afterlife matrix contract is needed. Independent
final SOURCE accepted the exact delta and addressed GREEN26/4 selection: new10,
unchanged recovery3, guidance1 and cap DATA12, each at its original60s limit.
Fresh Unit/Main build completed in74.19s,50 warnings,0 errors; Integration build
completed in1.33s,0 warnings/errors by incremental reuse of the reviewed RED fixture
binary, not a new Integration compile. All three Main copies are D9FACEEF2900A699;
Main PDB A8051428 binds BAF0908A, Unit ADE9DD50/PDB97A0ADBB binds the updated guide
guard, and Integration3A65CB72/PDB819EBA6D still binds A1419E43. Independent PREP
rehash and parsed PDB/CodeView checks accepted all111 pins and20 frozen files.
Exact PlanOnly26/4 completed without executing tests.

Actual GREEN26/26 PASS,4/4 descriptors,wall58.6740505s,exit0/no timeout. Per-leaf
costs at their original60s limits: cap DATA12=3.8582727s, guidance1=4.417615s,
recovery3=14.2570678s, response10=26.3050454s. Four actual TRX contain26 unique
IDs/names, no skipped/failed/duplicate/underfilled cases. Both cleanup flags true;
no fixture roots or live test processes remain; all111 runtime source/build pins
still match. SummarySHA
87525b653b3402485411b0ef096fc1120b115e3bf2e59519ae6489165c2ed6ff.
Independent runtime OUTCOME accepted after reading actual four TRX, summary,
source/build pins and cleanup. Final independent documentary/inventory review
accepted, including all78 files/229770068 bytes and exact six-tree status.

Optimization: this constant-time token extension adds no IO or reconstruction and
needs no cache. Original control/tier assertions reuse retained source-owner DATA;
no duplicate snapshot read or authority reuse is added. Per-case custom canonical
control/tier and dice are signed freshly through the existing fixture factory;
no arbitrary callback is declared cache-safe, no mutable fixture is shared, and
current leases/owners/cold readers remain fresh. No speedup claim is made.

Evidence is retained outside checkout at
`/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/spiritual-strain-response-success`.
Prior blocks and all trees remain preserved. Existing-pressure baseline provenance,
champion/force authority contexts, broad failure semantics and whole FR025 remain
open. H0/A5/browser remain UNKNOWN; no rerun, budget increase or publication.

Current six-tree inventory after this block: wounds-local at b43726ec has11 tracked
changes/8 untracked paths; wounds-a5-aggregate at the same b43726ec has9/3;
wounds-stage-profile-6007 is non-Git; wounds-terminal-capture-6007 at6007e82d has5/4;
wounds-terminal-capture-profile-6007 at6007e82d has8/7; active
wounds-text-surfaces-6007 at6007e82d has69/25. No tree was reset, overwritten,
committed or published. Final available space: /tmp2.1GiB, workspace14GiB; no
cleanup deletion was necessary. Only checkpoint/tasks change after runtime freeze.


### T081-D remaining owners and IV-forbid8 — bounded verification plan

Read-only independent source reconciliation on the unchanged D9FACEEF runtime finds
no lawful player success force producer. Existing meaningful force is Guardian
coercion; player response is incarnation_resistance, already qualified separately.
Force is not a standard/special art (AfterlifeEntityProfileState:181–208); harmful
nonstandard force remains AppliedSourceBinding (SpiritualWoundSourceAdmission:699–709)
without a current fulfillment owner. Syntactically accepting success tokens or
changing actor/tier cannot substitute for authority. The accepted no_effect force
cost route is not a successful force gameplay proof. Keep the current guard.

Existing-parent pressure has no independent ordinary after in current owners.
SpiritualExistingStrain:21–29 admits maneuver/setback only; its restoration at57–62
is justified by ordinary maneuver's unchanged-strain rule. Pressure:4388–4444 does
not determine failed own-strain amount. The new-materialize C2 owner retains an
ordinary comparison immediately before that decision (SpiritualC2SavedDecisionDraft:
443–455), not before applying an already-active parent. Source/frontier/receipt DATA
otherwise contains actual authored after. With ordinary clear→clear versus
clear→strained, the correct extra-step results differ; reset-to-before or inferred
actual-minus-one does not prove which ordinary result occurred. Keep this guard;
no new result rule or general combat mechanism is introduced.

Next independent direct FR022/025 cell is rank-IV spiritual_art_restriction=forbid,
which the owner did not defer. Current consumer SpiritualWoundMechanics:49–56 and
existing OriginalSpiritualGeneration_RankFourWoundForbidsMatchingArt8 are implemented.
At planning, no exact accepted IV8 receipt was found in current plan/checkpoint/tasks
or available local TRX/summaries. Plan's20260920 tempo21 is18generation+3ordinaryguard, not IV8.
This identified an execution-evidence gap, now closed by current8 below, not a
demonstrated production bug.
Roll/cost/tempo and actual severity-generation/removal already have separate accepted
receipts; no repeat or implementation follows merely from their parent checkboxes.

Plan: keep the existing eight rows/helper unchanged and give that exact method a
standalone category instead of executing the broad82-selector spiritual-wounds-state
owner. Four positive controls cover declined wounds and nonmatching restrictions;
four expected refusals cover matching guard on both sides, including no_effect.
The existing helper materializes real IV/four distinct restrictions, advances the
next owned exchange and checks exact source/dice preservation and canonical
nonpublication. The contour is original-owner insertion/next-exchange in Chaos Sea,
not C4, cold completion, Engine or all-realms acceptance. Fixed120s leaf replaces
this method's prior broad600s selection bound; no automatic increase/retry. Fresh
successful Integration build, exact compiled-source/PDB pins, catalog and PlanOnly
preceded execution after independent SOURCE/selection review. Actual current8 passed;
this is verification of existing code, not a RED/GREEN production change.

Optimization: unchanged cases already use the established complete-conflict context
and existing immutable preparation services; all mutable files/owners are per-case.
No new source read, cache or shared authority is added. Reuse current unchanged
compiled inputs only after successful build/hash/PDB checks; no speedup claim.
At preparation /tmp2.1GiB/workspace14GiB are available, so no deletion is needed.
External evidence: /workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/spiritual-art-forbid-current8.
Prior trees/evidence remain intact, no production/test source edit or publication.

Independent SOURCE and selection review accepted the unchanged fixture, exact
single-FQN extraction and fixed120s leaf. Its inherited OriginalCaptureField private
reflection observer is unchanged; this block adds no test or reflection usage.
Fresh successful Integration build1.18s,0 warnings/errors is incremental reuse, not
recompilation. Integration3A65CB72/PDB819EBA6D and MainD9FACEEF/PDBA8051428 stay exact.
Four compiled source pins (fixtureAE523B4A, ConflictFrameFA1400F9,
SourceContinuation620A7D3D, mechanicsBAF0908A) agree with source/PDB/CodeView.
Independent PREP accepted all114 pins; catalog1429/13380 has no stale/unmapped
entries. PlanOnly is exactly1descriptor/1method/8cases with no execution.

Actual8/8 PASS,leaf14.0992467/120s,wall19.7077773s,exit0/no timeout. One actual TRX
has8unique IDs/names, no failed/skipped/duplicate/underfilled cases. The four expected
refusals and four positive controls reached their existing assertions. Selection
complete1/1; both cleanup flags true, no resource fixture roots or live test processes;
all114runtime pins remain exact. SummarySHA
7d26613af174f5e9651bdcdeed6f706906ee1bb9cad1b1646815ba92951aa25a.
Independent OUTCOME and final documentary/inventory closeout accepted. No gameplay or
GM-authored contract changed, so no new GM example/prompt/source-guard change is needed.
Six trees retain the previous recorded HEAD/status, including active69tracked/25untracked.
Only checkpoint/tasks change after the runtime freeze. Force/current-pressure proof
limits, all author deferrals, H0/A5/browser UNKNOWN and the whole-feature boundary
remain. No subsequent runtime, budget increase, source edit or publication.


### T154 owner-specific receipt reconciliation — read-only, 2026-11-04

This map narrows older blanket “owner-specific union open” statements without
inventing a new matrix or repeating accepted tests. No production/test/catalog
change, build, discovery or runtime occurred. Existing T154/T177 remain open.

Newer plan:3322–3422 already accepts three exact singleton receipts: focused-skill
Wound History publication plus explicit Engine rollback; required inventory40;
and procedural wound_output1 compensation/rearm. Their actual local summary/TRX
hashes were reread: focused corrected1PASS9.0781722s (TRXb321d24d), inventory1PASS
5.0546372s (48e2e66e), procedure1PASS20.5059736s (e807f77a), all original180s bounds
with complete selection/cleanup. Initial focused1FAIL before probe arming remains
separate (c4a5b063), not a failed rollback assertion. Exact test/probe hashes match
current files; runtime is b43726ec plus each recorded working tree, not a fresh
6007/current-WIP execution. Shared production has later independently versioned
changes. Thus old US8 lifecycle16 is not the only focused-probe evidence, and this
map must not reopen that accepted singleton or imply universal current qualification.

- Quest / Guardian project journal: existing
  CompleteWoundPlan_PostPublicationRetainedRootDrift_RestoresWholeLocalTransaction7
  corrupts an excluded retained root at resource-definitions write. Its regular_quests
  and guardian_project_journal rows restore the selected original bytes/existence
  and retain the wound command. They do not author a quest/project-journal entry,
  assert every root/generation/retry, or cover soul quests/quest History as owner
  publication rows. These seven drift rows have historical documentary acceptance
  within normalizer10 (tasks:13761–13802, run20260829-052453); their old raw is absent.
  Inventory40 separately includes these roots but restores
  present opaque bytes, not actual owner mutations or publication failures.
- Actual gameplay memory already has accepted owner cuts: ResidentAid and ResidentHelp
  common-publication failure journeys (plan:16326ff,16395ff), plus Shining resident
  and radiant patient failures (plan:11912–11992). The fixtures witness actual
  normalized memory, compare captured staged bytes/absence, then restore the accepted
  grant/offer world where that outer oracle applies. These are not a missing journal
  subsystem or proof for every gameplay journal owner. Their accepted receipts are
  documentary here; referenced older workspace raw paths are absent in this environment.
- Resource History already has an accepted physical-after-write owner cut in
  CommonPublicationFailureAfterPhysicalWrite_RestoresExactTurnSnapshot. The selected
  member reached disk; KnownRollbackPublicationCut checks changed bytes/matching SHA, complete
  captured before-images/absences, generation and no publication journal. Historical
  resource4 belongs to runtime66f655ee; its preserved12-case summary also includes
  unrelated location8, which is not added to wound acceptance. Exact test/probe hashes
  match current files; exact old TRX is absent. This resource-only fixture contains
  no wound command, so it does not qualify every composed wound/resource scenario.
- Original opaque storage24 and guaranteed mixed item/resource7 remain accepted on
  their own lineages. The latter includes successful once-only retry; the procedure
  singleton instead proves same claims/plan rearmed, not successful procedural retry.
  Current raw paths for those two runs are absent, not grounds to downgrade accepted
  receipts or repeat them. Historical storage24 additionally retains96 hash-matching
  records (including24 exact twelve-path rollbacks and48 cleanup records); its25-case
  summary includes a separate offscreen case and is not a new wound25 result.
  The historical storage manifest is specs/1553-portable-local-storage/recovery/
  storage-migration-known-rollback-20261009/manifest.json (7f9a32d2), sections
  woundOffscreen/locationResource12. Its wound summary1ac13f64 and combined
  location/resource summaryd8fafa66/plan492971ba match retained bytes. Resource4 is
  an exact descriptor inside that combined plan, not a separate independently
  hashed resource-only invocation.

Source anchors: CanonicalStateNormalizerTests.Wounds:131–204;
ExplorerWoundSelfTreatmentTests.Aid:77–94,392–408 and ShiningPatient:227–243;
ResourceMaterializationLifecycleTests:21–72 / KnownRollbackPublicationCut:23–71;
EffectSkillScopeLifecycleTests:957–1061; procedural lifecycle:115–183;
GameEngineTurnLifecycleTests.Wounds:40–98. Quest/journal normalization precedes
accepted mechanics, while retained agreement excludes actual write/delete members;
opaque storage and inventory coverage cannot substitute for those owner semantics.

Remaining precise question: match any genuine owner-authored quest/project-journal
mutation during a wound turn to its exact existing publication/rollback receipt,
and reconcile relevant current shared-runtime changes. This read did not find that
receipt or demonstrate a production failure. Do not turn absent rows in one helper
into a new all-owners×all-cuts workload, infer quest fulfillment from healing
consideration, or retry any unchanged accepted/UNKNOWN case. T154/T177 stay open.

Four closed local evidence parts were copied outside checkout as three roots,
including focused/fixed once:83files/30692733bytes. Original before/after and copied
membership, sizes and SHA256 agree; no original was removed. Full copy, manifests
and read-only primary rechecks are under
/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/t154-owner-reconciliation.
The two author-question drafts with game examples/rule references are saved there,
not sent to the user or adopted. Optimization: source/receipt reads only, no new
runtime/cache/authority or speedup claim. Independent documentary closeout accepted.
All prior open/UNKNOWN, preserved trees and no-publication restrictions remain.


### T154 regular quest owner publication — SOURCE/FIXTURE preparation, 2026-11-04

Scope: one existing regular quest receives only `newDetailsLogEntry` beside one
real signed Mortal wound acquisition. The existing distribution, raw validation
and accepted common refresh own all writes. Prefer a new narrow test-only partial
and an exact two-row positive/late-fault leaf over modifying the accepted drift7
or constructing a new Engine/combat/quest framework. No production defect is
asserted and no production change is authorized by this fixture preparation.

Plan: seed a valid quest before `CreateSignedAuthorityAsync`; validate the exact
public response and a foreign quest-ID refusal; distribute through StateDistributor;
retain raw item/resource admission; then use the real common normalizer. The
positive row must produce exactly one append with all other quest data unchanged
and genuine wound/effect/History. The fault row uses existing
KnownRollbackPublicationCut at Wound History MemberPublished, captures actual
normalized quest/wound/effect bytes before compensation, and checks every captured
normalizer root byte/absence plus generation and cleared publication journal.
This is staged-before-image compensation, not pre-turn Engine rollback.

Proposed source: WoundMaterializationLifecycleTests.QuestRollback.cs, one two-row
method owned by new `wound-quest-owner-rollback` (exclusive, no frontend, 120s
protective limit; cost currently UNMEASURED). Selected build/catalog/PlanOnly and
runtime are UNRUN and will be reported before execution. Existing focused
rollback180s is only a budget comparison, not new timing evidence. No old PASS or
UNKNOWN case is reselected. Initial free space: /tmp2.1GiB, workspace14GiB.

Optimization: one fresh fixture per row is required because publication changes
canonical state and live authority. Reuse existing builders and cut/cleanup
helpers; no new cache, GameEngine setup, state shared between rows, or repeated
full-world validation. Hooks retain ordinary fresh validation. No GM/public
contract change or new worked game rule is introduced, so prompts/examples stay
unchanged. Independent SOURCE/PREP accepted the saved217-line fixture SHA256
0d625136a5e3021284e4e8b58838b0c9e982bdf3c8b81be259d6f8ce3cdf08d7.
The review corrected the new test's expected exception to the exact original
batch-cut failure, rather than an individual-write wrapper. Positive-only final
joins use strict identity/receipt parsing, the actual create transition and the
declared owned effect. Mid-cut checks do not assume that later identity/receipt
members have already published. No blocking finding or runtime result is claimed.

Pre-edit comparison confirms all1196production .cs unchanged, no added production
file and no missing pin. Prior1429category and47selection entries are unchanged;
only the new exact leaf is prepended. Current active tree:69tracked modifications,
26untracked files; the only new source in this block is the fixture. Build,
catalog/discovery, PlanOnly and execution remain UNRUN after the source gate and
budget report to the parent. The120s leaf bound does not include the separate
fresh-build/discovery preparation; it will not be raised automatically. All
previous open/UNKNOWN remain. External pre-edit pins:
/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/t154-regular-quest-owner-gate.

First runtime amendment: fresh unit build0warnings/0errors1.88s; integration
108warnings/0errors50.34s. Catalog1430categories/13381methods valid, exactPlanOnly
1descriptor/2cases. First execution completed2/2FAIL in18.9466727s wall
(category13.6985328s), no timeout and both runner cleanup flags true. Positive
passed common refresh and quest/wound/effect/identity assertions, then failed the
new receipt-nonempty oracle. Fault reached actual History MemberPublished and
returned CanonicalStateWriteException, so the incorrect Same(originalFailure)
oracle failed before full compensation assertions. Both owned roots are absent.
This is not a production RED and does not yet establish compensation acceptance.

Independent diagnosis retracts the earlier batch/receipt inference: signed formal
source presence is not QTE receipt issuance. ValidationService.WoundMaterialization
PlanAcceptedMortalQteReceipt971–976 requires adapter=qte and the QTE terminal
health-damage profile; this helper is formal/mortal_formal_injury_v1. The real
direct accepted-refresh route therefore has no occurrence/receipt companion and
uses individual writes. Fixture-only correction restores exact wrapper/path/inner
identity, adds wound-origin opportunity/event joins, and requires unchanged raw
occurrence/receipt bytes and no corresponding owner companion. It does not change
the producer, invent QTE consumption, relax ordinary validation or change production.
Captured bytes now print before assertions. Corrected SOURCE/build/runtime are
pending; same two cases and120s bound, with no unchanged rerun. Initial source/build
hash pins, logs/TRX and receipts remain in the external gate root; the first
manifest and fixture copy are historical pins and are not overwritten.

Corrected SOURCE accepted for fixture SHA256
1dde9771a2a004dabfa9162a1e36b56306a39340f2a6f125d6a272766ac8a6e1.
Fresh integration build108warnings/0errors57.06s and exactPlanOnly1descriptor/2cases
passed. Method/category membership did not change, so the already accepted full
catalog discovery was not repeated. Main remainsd9faceef2900a699f792f6f0c4ca6fbd35bdaeb6a172fafe7e6c620baeeb101a;
corrected integration4f695c09f05b7e6709ae0012af054aeb327390f9e9826d9353d0551ba873f888.

Actual corrected runtime:2/2PASS, positive5.8728562s and late-cut4.1182615s;
category13.50192s, wall19.3318096s, protective120s unchanged. Selection1/1descriptor
and2/2cases complete, exit0, no timeout/duplicates, both runner cleanupflags true.
The actual raw late-cut receipt has86before/after paths(24present/62absent), all
bytes/existence restored, unchanged generation, absent final publication journal,
and one physically changed History member with verified SHA. Both owned fixture
roots are absent. Positive raw output contains seven after roots; its unchanged
occurrence/receipt before↔after equality is established by the pinned compiled
assertions/PASS, not a separately emitted positive before dictionary. This evidence
boundary is preserved and does not justify another run.

No production file or old category/selection changed; active tree69tracked/26untracked,
other five trees unchanged from the recorded counts. Independent OUTCOME and
documentary audit accepted the exact semantic/byte/hash evidence, compiled source
and binary pins, cleanup, and stated evidence limits. The complete external
evidence inventory is saved beside the run artifacts. This closes neither project-journal
nor outer Engine rollback, fullT154/T177, H0/A5/browser or any other UNKNOWN.
Optimization remains fresh independent per-case authority with existing setup/cut
reuse; measured costs above, no cache/speedup claim. No commit/push/publication.

## T154 Guardian project-journal owner: preparation (2026-11-04; outcome below)

The remaining owner-specific receipt is not an inferred production defect. Existing
QTE journal-restoration coverage compares normalized text and has no composed wound
publication witness; accepted drift7 concerns foreign root corruption. Neither is
repeated. Mortal realm segregation forbids simply adding this owner to the accepted
regular-quest fixture.

Prepared `WoundGuardianJournalPublicationLifecycleTests.cs` uses the existing genuine
Chaos Sea self-treatment fixture (rank I, tier1, die20) and its signed one-cycle request.
Before bootstrap/signing, it seeds a supported existing Guardian/project and one journal
entry. Initial Guardian DATA is canonicalized through the existing typed authority
projection, not changed after signing. The real public `guardianProjectUpdates` changes
only `activeState`: no numeric progress, completion, reward, aid payment or new rule.
The real normalizer derives the journal entry; its random ID is observed, never invented.
Public positive/foreign-project refusal and canonical initial tracker/journal gates precede
publication. Staged materialized arrays intentionally remain the original DATA until the
normalizer; a canonical after-image gate is not substituted for public command admission.

Two independent rows target ordinary common refresh: success, and the existing changed
Wound History `MemberPublished` cut after tracker/journal/profile/effect writes. The latter
must prove the actual derived append plus terminal wound/effect/History at the cut, then
exact complete staged-root bytes/existence, generation, journal removal and owned cleanup.
This is direct common-refresh compensation, not outer Engine rollback or C2/C4 signed-original
compensation. No claim of full T154/T177, feature completion, Windows/cold replay or closure
of A5/H0/browser UNKNOWN follows from it.

At preparation, proposed `wound-guardian-journal-owner-rollback`: one Theory/two cases, peer budget
120s, cost UNRUN. Fresh integration build, discovery-only catalog validation
and exact PlanOnly are required before its sole runtime attempt. At this preparation point SOURCE was pending;
no build/runtime had begun and production was unchanged. At preparation: /tmp2.1GiB free, main disk14GiB
free, memory5.5GiB available. Evidence outside checkout:
`/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/t154-guardian-journal-owner-gate`.

Optimization assessment: reuse existing typed self-treatment, initial Guardian projection,
bootstrap, publication and exact-cut helpers. Each fault/control has fresh mutable state,
signed authority and lease; cross-row DATA/authority reuse is unsafe and unnecessary for
these two narrow cases. Root observations are captured once per relevant phase and shared
by the assertions. No new cache/framework, duplicate world setup, full GameEngine, or speedup
claim. GM-facing contracts are unchanged; no new prompt/example semantics are introduced.

### Bounded execution (2026-11-04 21:29 UTC; independent OUTCOME accepted)

Independent SOURCE accepted fixture SHA256
`1b8a3b279b8542e8f69ad6b8ddf539a94dec49f36d8a8e8125fb913b160c5b13`.
A staged canonical after-image assertion was removed on source evidence before any build/run:
StateDistributor legitimately retains original materialized arrays beside new commands;
public command admission is the correct pre-normalizer gate. No runtime failure or production
RED was claimed. The canonical initial tracker and public foreign-project negative remain.

Fresh Integration build: exit0,108baseline warnings,0errors,50.02s. Catalog discovery:
1431categories/13382methods, valid, no tests executed. Exact PlanOnly1descriptor/2cases.
The sole runtime invocation was:
`scripts/test-csharp.ps1 -Category wound-guardian-journal-owner-rollback -NoBuild -Parallelism 1 -TimeoutMinutes 2`
using the retained local .NET8/PowerShell environment. Actual2/2PASS, no skip/duplicates/timeout,
exit0, complete1descriptor/2cases; category17.0992721s,wall22.5689987s. Fault row4.3769562s,
success9.2959680s. Both runner cleanup flags true; both actual owned fixture directories absent.

The fault receipt contains104exact paths:41present/63absent. Every raw before/after byte or
absence matches, generation matches and active publication journal is absent. Its actual
uncommitted12-member local journal was cut at changed History member index7; journal SHA256
`17f6799a648c68cb47ec965d7eeeef82db749c9520509011b319fad1a2bd9e56`.
At-cut raw5roots independently expose two journal entries(old preserved/new derived), the
updated project state, zero active wounds/effects, expired old effect identity and one terminal
heal in three History rows. This is real publisher output before compensation, not invented
after-images. The successful row retains all104before images plus7after roots and the compiled
oracles establish exact tracker, journal append, healed identity, retained self-attempt and
one Guardian cycle; canonical resource values/history remain unchanged.

All1196production-source hashes remain unchanged. Build/source pins and complete TRX/summary,
raw emitted receipts and extraction live in the external evidence folder above. Independent
OUTCOME/final documentary verification accepted; no whole T154/T177 or old UNKNOWN closure.

## T154/T177 bounded current shared-boundary reconciliation (2026-11-04)

This read-only pass answers the concrete question left by the earlier owner map,
not a new all-owners-by-all-cuts matrix. No build, discovery, runtime, production,
fixture or category change occurred. Independent read-only review found no new
functional defect; final documentary/proof closeout accepted.

| Required boundary | Exact accepted receipt and present limit |
| --- | --- |
| Wound/effect and focused skill | Focused corrected singleton plus original storage24 retain exact physical-cut/original-snapshot oracles. The current focused fixture/probe is the same recorded source. Its historical runtime is not relabelled as current-WIP. |
| Resource/item | Guaranteed mixed item/resource7 includes exact rollback and successful once-only retry. Resource4 has a real Resource History after-write cut but no wound command; location8 in its combined invocation is not added to wound acceptance. |
| Profile/scheduler | Accepted spiritual aid/memory owner journeys and current Guardian self-healing2 exercise actual profile/owned effect/cycle outputs. Current Guardian fault preserves the exact staged profile/schedule/report/command within104captured paths. This is not every actor/realm/continuation. |
| Quest/project journal | Current regular-quest2 proves an existing append beside genuine Mortal acquisition and86path rollback. Current Guardian2 proves the derived project-journal append beside genuine self-healing and104path rollback. Both production inputs remain current; neither implies completion/rewards, soul-quest fulfillment or the new owners' outer Engine/C2/C4 coupling. |
| Output/owned retry | Procedural wound_output singleton proves exact compensation and rearmed same claims/plan; mixed7 separately proves successful guaranteed retry. These distinct assertions are not conflated into a successful procedural retry claim. |

The old paragraph saying an owner-authored quest/project-journal receipt was not
found is historical: its precise missing-receipt question is now resolved. Original
opaque inventory images and foreign-root drift7 are still separate and were not
rerun. Excluded soul quests, rewards and owner/cut combinations are qualification
limits, not evidence of a missing producer, production failure or automatic new
mandatory tests.

**Actual source continuity, not only matching Git names:** preserved focused,
inventory and procedure `source-before.json` runtime manifests each match current
FileSystemManager41, GameEngine59partials plus the root GameEngine.cs, normalizer30
(including AcceptedTurnCanonicalStateRefresh) and AcceptedMechanicsPlan-prefixed17.
The shared publication/compensation machinery therefore has no demonstrated drift
from those accepted runtimes. Other later wound/History/read/combat changes exist;
this does not make all historical composed scenarios freshly qualified.

For T177, all six original US8 behavior families retain the accepted map above.
Sixteen named scope/procedure/lifecycle/guard files are byte-identical to6007 HEAD:
exact offered/current authority, common reducer, detached source, cache/transcript,
procedure checks/seal validator, response composition, staging, scope projection,
and owning lifecycle/typed/source guards. The changed EffectMechanicsSnapshot
read-DATA export and WoundPlayerReadService callback fallback match the accepted
History157 freeze exactly. Nineteen of its20production pins still match; the sole
later Guided.cs change and its test match the separately accepted guided86 null-minute
guard. These are separately attributed receipts, not a new243-case aggregate or a
fresh run of all skill-scope consumers. No new scope algorithm, actor/skill authority,
RollSkillId, repair, cancellation or projection gap is demonstrated by this pass.

Bounded verdict: the identified owner-receipt and current generic-boundary questions
are reconciled. There is no cause-backed new test or production correction to start
from this reconciliation. T154/T177 parent boxes remain open for whole intended-change
final acceptance and the explicitly retained qualifications, not for obsolete aggregate
commands. A5/H0/browser and other UNKNOWN/HOLD, author decisions, platform limits and
no-publication remain unchanged. A broader acceptance cannot be inferred from these
partial receipts; any later test needs a specific changed/uncovered behavior first.

Compact source/runtime hash comparisons are saved outside checkout at
`/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/t154-t177-final-reconciliation/source-continuity-proof.json`.
Previous evidence inventories remain intact: owner-map92, regular-quest46, Guardian25.
Optimization assessment: reuse existing receipts/manifests, source images and accepted
helper boundaries; only read/hash the relevant stable DATA. No new cache, world setup,
shared mutable fixture, source computation benchmark or speedup claim. GM contracts,
examples and runtime semantics are unchanged by this documentation-only reconciliation.

## Final intended wound-only FR/scenario reconciliation (2026-11-04)

This is a bounded source/evidence reconciliation, not final feature acceptance.
The exact current normative index contains 103 FR (including 035A and 043A) and 59
numbered scenarios across eight user stories. Every ID has one classified row,
a versioned evidence reference and an explicit boundary in the external map.
`accepted_versioned` means the described accepted receipt, not a fresh current-WIP
run, whole-FR completion, readiness percentage or permission to resume a held test.
Missing fresh execution alone is not a functional defect. The current wound-only
scope above still supersedes the old broad remainder audit.

The intended combined change has 41 changed/new production C# files from 6007;
all 1196 current production C# hashes match the latest quest/Guardian freeze.
The separate terminal-capture cache experiment is not part of this combined tree.
Source continuity is recorded only for files actually present in each historical
manifest. Later edits are listed explicitly instead of transferring an old result
to a new binary. Shared T154/T177 and T149 maps are reused; no blanket owner/cut,
actor/realm or malformed-input Cartesian matrix is created by their exclusions.
No build, catalog discovery or runtime was executed for this reconciliation.

### Versioned local receipts, never an additive current test total

| Accepted bounded block | Executed Main SHA prefix and actual accepted scope |
| --- | --- |
| Literal console confirmation |8c435ed9: 16 parser/render controls and one Elyara console journey. |
| Physical browser wording |2ee97fb9: 33 cheap controls; the actual procedure consumer/cleanup remains UNKNOWN. |
| Physical terminal History |16f94001: 157 unique cases in eleven descriptors, including nonterminal joint producer/reader; not A5. |
| Guided-v2 null minute |749b52e7: 86 addressed parser cases; not a rerun of 157. |
| Mortal target/provider names |3d927d21: 22; 5c5979b4: 11 with overlapping target controls. |
| Requirement-resource privacy |af6380f1: 53; declared-condition rendering, not resource payment. |
| Public afterlife provider names |fd84a848: 51; navigation/quote-label controls, not payment publication. |
| Ordinary Shining combat self-healing |35 unique receipts span 5e6ca496, 7fbe29d4 and 31a23df3; only final native 8 use 31a23df3. |
| Shining patient navigation |8dd1df07: 30; patient/context/read-current controls, not treatment publication. |
| Initial IV0 chronology |8dd1df07: H5 four-cycle plus fifth-empty case PASS; H0 twenty-cycle UNKNOWN at 300s. |
| Proven strain success families |2dfc4cd0: 19; 83b4f0c3: 26; d9faceef: 26. These overlap and retain C2/cold/publication limits. |
| IV art forbid, quest, Guardian journal |d9faceef: 8, 2 and 2 separately; exact earlier owner/phase boundaries remain. |

Older accepted requirement families retain their original commit/source references;
missing historical raw files are not described as recovered. In particular,
History 157 and guided 86 do not become a 243-case run. The copied unselected Unit
Main 7fbe in the final Shining manifest is not the executed native Main 31a23.
Current binaries/source hashes and all seventeen local manifest comparisons are
recorded outside checkout, with no regenerated gameplay evidence.

### What remains and why

- **No new proven wound-only gameplay defect** is established by this source/map
  reconciliation. Already implemented and accepted slices are not repeated merely
  because their parent task is open. Whole T150/T151/T152/T154/T177 and #1536 are
  not declared accepted by this result.
- **UNKNOWN execution:** H0 initial-IV twenty-cycle completion; full terminal-joint/A5;
  the denied browser procedure outcome and fixture/process/capture closure. The
  older full browser-course, DFR, broader Mortal and platform qualifications keep
  their own recorded UNKNOWN/HOLD. No unchanged retry or larger limit follows.
- **Missing proof/owner context, not an invented rule:** an already-active pressure
  consequence lacks an independent ordinary-before result; successful force/champion
  cases lack the required lawful owned source in the inspected routes. Current
  fail-closed guards remain. A safe cycle and combat cannot be combined into one
  signed safe bundle; that prohibited combination is not a new uncovered scenario.
- **Two author decisions remain:** how to prove which participant's declared action
  failed when the accepted result is ambiguous, and whether matching strain burdens
  from several wounds add one step each or only one total. Exact rules, examples and
  unapproved proposals remain in `strain-open-semantics-20261103.md`. No new field,
  failure interpretation or aggregation rule has been adopted.
- **Explicitly excluded/deferred work:** control/counter and rank-III art restrict
  runtime meanings; general defeat/aggression (the T088 question is withdrawn),
  general ACTIVE/F1 orchestration and independent Saref memory #1571. Their original
  tasks are not complete, but they are not automatically current wound blockers.

The sole concrete next assembly finding is in `tests/selection.json`: it has 49
unique valid category IDs but omits two already accepted nonterminal v3 consumers,
`fr063-joint-linux-applied-preparation` and
`fr063-joint-progression-data-integration`. Their existing 16f940 producer/reader
receipts and exact source dependencies already exist. The smallest next correction
is to record those categories and reasons, preserving their template/environment
and 5-minute/2-minute metadata. This is a selection-map omission, not a gameplay bug,
new run authorization, current Windows qualification or terminal-joint acceptance.
No selection change has been made in this reconciliation.

Independent final SOURCE/evidence-map and documentary review accepted this bounded
reconciliation. No new runtime or whole-feature acceptance is implied.
The complete 162-row map, normative index, 17-block version registry and source/tree
pins are retained outside Git under
`/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/final-fr-scenario-review`.
Only checkpoint/tasks prose changes in the checkout during this block. All six
local trees, earlier evidence and held fixtures are preserved; no publication.
Optimization assessment: reuse accepted receipts and frozen source DATA; fresh
read/hash comparisons are cheap and need no cache. No gameplay setup, mutable
owner reuse, performance benchmark or speedup claim is introduced.

## Nonterminal joint selection-map correction (2026-11-04; historical, reverted below)

The following records the accepted local metadata experiment. Its two-ID selection
change was subsequently reverted by the CI-integration blocker section below.
At this historical point, the preceding review's selection omission was corrected. Exactly two existing
entries were added to `tests/selection.json`: `fr063-joint-linux-applied-preparation`
and `fr063-joint-progression-data-integration`. All previous 49 entries are unchanged;
the experimental set then had 51 unique IDs. Reasons/contracts identify the actual frozen
HistoryResult/common-marker and phase-reader consumers, with explicit nonterminal
limits. The catalog, 5-minute/2-minute budgets, Ubuntu/Windows metadata, template
requirements, production and test sources are unchanged.

The already successful owning builds remained exact: all 1196 production source
hashes, seven binary hashes and all prior changed source paths match the baseline.
Only selection metadata changed before verification, so no build was repeated.
Using the existing local .NET8/PowerShell environment, both metadata commands used
`-NoBuild` and a two-minute command preparation bound:

- `-ValidateCatalog`: valid, 1431 categories / 13382 methods-files, no unmapped or
  stale selectors; exit0, 10.6768869 seconds, no timeout.
- `-SelectionFile tests/selection.json -PlanOnly -Parallelism 1`: 51 categories,
  52 descriptors / 494 planned cases; exit0, 8.8805632 seconds, no timeout. Each
  added category has exactly one existing method/case and its original 5/2-minute
  category budget. No method or category was silently substituted.

Both summaries report zero executed/passed/failed tests, zero completed descriptors,
no category/adapter execution results and successful owned/runtime cleanup.
`Selection.Complete=false` is expected for discovery/PlanOnly, not a partial runtime
PASS. Neither command ran test bodies, regenerated an applied-data template or
retried A5/H0/browser. Existing held entries in the plan are not permission to run them.
The earlier History157 producer/reader receipts remain on Main16f940; these checks
are not new gameplay, current combined runtime or Windows qualification.

One future CI prerequisite is explicit: the unchanged Linux producer requires
`BOE_TEST_LINUX_APPLIED_RECORD` and an external `BOE_TEST_DIAGNOSTIC_ROOT` with a
new record destination. The current default workflow does not set those paths.
The Windows phase-reader's tracked template exists, but this block does not execute
or qualify it. Catalog/PlanOnly and this selection correction therefore do not claim
runnable hosted-CI integration. No workflow, environment permission, artifact transfer
or template preparation was changed in this bounded task.

Independent SOURCE, metadata OUTCOME and documentary review accepted the exact
two-entry correction and the verification boundaries above. Evidence/baseline, exact diff, logs, plans and
summaries are outside checkout under
`/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/joint-selection-closeout`.
Optimization assessment: reuse unchanged successful builds and accepted versioned
receipts; only refresh metadata inventory/selection. No new cache, computation path,
mutable fixture, runtime benchmark or speedup claim. Only selection and checkpoint/
tasks prose change; all six trees and existing evidence remain preserved. No publication.


## Nonterminal joint CI-integration blocker and selective rollback (2026-11-04)

The two-entry selection experiment above is not CI-ready. Read-only diagnosis of
`.github/workflows/dotnet-ci.yml` found that it sets neither
`BOE_TEST_LINUX_APPLIED_RECORD` nor `BOE_TEST_DIAGNOSTIC_ROOT`. The selected Linux
producer (`JointBundleComponentTests.LinuxApplied.cs`, lines 23–31) requires a new,
absolute record destination outside checkout before it creates its fixture. The
record must be inside the authorized external diagnostic root; the default
`TestDiagnosticPaths.Root` is checkout-local `TestResults` and fails that guard too.
Thus reaching this producer under the present workflow without supplied paths
causes a deterministic setup refusal, not a new gameplay or History failure.
The Windows phase-reader uses its tracked template independently; no cross-OS
archive transfer is required or authorized by this finding.

Merely setting a hosted-runner temporary path does not meet the existing retention
contract. No permitted persistent external diagnostic destination was established.
The latest complete verified measurement must survive until a verified replacement;
raw logs/TRX/diagnostic archives cannot be uploaded to GitHub or treated as disposable
scratch. The workflow's existing raw `TestResults` artifact uploads are a separate
pre-existing policy conflict, not permission to upload these new records. No CI
security, retention, permission, action, cleanup or upload rule was changed here.

Following the explicit instruction to revert only this latest selection addition,
`tests/selection.json` is restored byte-for-byte from the saved original 49-entry
file (SHA256 `d2f51bbdca7fbccb9902a7e407e69c3e85d2168a0ce454454fd769d7bb0a19c3`).
Only `fr063-joint-linux-applied-preparation` and
`fr063-joint-progression-data-integration` are removed. All previous entries,
ordering, reasons, contracts, whitespace, catalog metadata and category budgets are
preserved. The accepted historical 51-ID catalog/PlanOnly evidence and History157
producer/reader receipts remain intact and retain their original source versions.
They do not constitute current CI or terminal-joint/A5 qualification.

The selection omission remains an explicit open CI-integration blocker. A later
bounded integration must establish an allowed persistent external destination,
allocate fresh owned external paths (without precreating the producer's record),
route build-warning lookup to the actual diagnostic root, and preserve failure,
security and retention rules. Cleanup may delete only verified owned transient
scratch, not retained complete diagnostics, gameplay saves or uncertain fixtures.
Do not re-add these categories merely on the basis of a successful PlanOnly check.
The original 49-ID set is restored, not certified as whole-feature or hosted-CI ready.

The restored selection was checked with unchanged successful compiled inputs and
`-NoBuild`, using the same two-minute command preparation bound:

- `-ValidateCatalog`: valid 1431 categories / 13382 methods-files, no unmapped or
  stale selectors, exit0/no timeout, 10.7184214 seconds.
- `-SelectionFile tests/selection.json -PlanOnly -Parallelism 1`: exactly 49 IDs,
  50 descriptors / 492 planned cases, exit0/no timeout, 9.0012176 seconds. Every
  retained descriptor's methods, count, adapter and budget matches the earlier
  51-ID plan; only the two removed one-case descriptors and ordinal names differ.

Both summaries have zero tests executed/passed/failed, zero completed descriptors,
no category/adapter execution or build phases, no TRX, and successful owned/runtime
cleanup. `Selection.Complete=false` is the expected metadata-only result, not a
runtime failure or PASS. Existing held selections do not authorize execution.
Independent final diff/metadata/documentary review accepted this bounded rollback. The exact snapshot,
selective diff and new logs/plans/summaries are retained outside checkout under
`/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/joint-selection-ci-blocker`.
All prior `joint-selection-closeout` evidence bytes remain unchanged. No build,
gameplay test body, CI job, old timeout/poll retry or publication is authorized or
performed by this block. All other local changes,
previous raw evidence and held fixtures remain preserved. Optimization assessment:
reuse unchanged successfully built inputs and accepted source/receipt DATA; only
metadata inventory is refreshed. No cache, runtime benchmark or speedup claim.

## Existing-pressure prerequisite: real birth, guard recovery and retained below-cap state (2026-11-04)

This local test-only qualification is part of FR-025/T081-D. It does not implement
existing-parent pressure, change production guards, or extend general combat.
`AfterlifeResourceCutoverTests.SpiritualPressureGuardRecovery.cs` adds one Fact in
`wounds-spiritual-pressure-guard-recovery`, using the existing 180-second bound.
The original 49-entry CI selection is unchanged; its separate external-path/
retention blocker remains open.

The actual common-owner publications establish pre-sign player capacity10 from
SpiritFocus tier3 and opposition capacity9, then turn42 pressure/setback dice5/15
creates a real III parent with trauma20 and AP7/6. Its three consequences remain
pressure strain+1, counter hindrance and guard cost+2. Fresh signed43 guard/
partial_success dice8/4 legally reduces conflict strain III→I and pays4/3, leaving
AP3/3. No strain increases and no harmful source/offer/none is invented. The real
exhausted source-only C3/common C4 route publishes the state; the full parent wound,
semantic identity, wound History, source/root metadata and prior source/decision
receipt rows remain unchanged. A fresh signed44 snapshot equals the actual published
bytes and passes the existing conflict phase reader. No third exchange is authored.
This qualifies persisted below-cap state in the real in-process path, not a new
cold no-offer resume, complete GameEngine/UI journey or pressure application.

The first run was **one fixture failure**, not production RED: category13.6949607s /
runner18.7924213s, no timeout, cleanup complete. The exact-bytes loop, which included an overstrong whole-file EffectIdentity
immutability oracle, failed after the C4, AP/strain and full-parent premises. The failed loop did not log its particular path, so that first artifact
alone cannot identify the differing file; its owned root was properly removed.
Source diagnosis established that this root's existing `on_owner_turn_end`,
`consumeUses=false` trigger lawfully appends identity history. This is separate from
the guard's +2 AP formula. The corrected fixture preserves every old entry field
and transition prefix, and allows only one actual non-consuming turn43 trigger
append per parent root, joined to returned ResourceTriggerExecutions, accepted
activation, the real lifecycle event, official event derivation and allocated
transition ID. Exact wound identity/History bytes and planned/persisted effect
identity equality remain. The complete before/after identity images are retained
in the new TRX output. No production correction was made.

Verification on unchanged Main SHA256
`d9faceef2900a699f792f6f0c4ca6fbd35bdaeb6a172fafe7e6c620baeeb101a`:

- Initial fresh integration build:48.92s,108 warnings/0 errors; fresh unchanged
  unit incremental build:1.72s,0 warnings/0 errors.
- Catalog discovery:1432 categories/13383 methods-files, no stale/unmapped,
  exit0/no timeout,11.7115878s. Initial PlanOnly:one descriptor/one case,4.9658886s.
- After the fixture-only oracle correction, fresh integration build:48.61s,
  108 warnings/0 errors. Fresh PlanOnly again selected exactly one case with the
  same 180-second category budget,6.2958039s. Catalog membership did not change.
- Corrected runtime:**1/1 PASS**, category15.5152451s / runner20.9264593s;
  test11.4015327s. Exit0, no timeout or duplicates, selection complete1/1,
  owned-tree/runtime cleanup true and actual owned fixture root absent.
- Named passed output:fixture setup951.775ms, birth publication6730.8765ms,
  guard publication2281.2519ms, signed44 completion at11382.2837ms total.
  These are phase observations, not independent additive timings or a speedup.
- Corrected fixture SHA256
  `7852d67752787238cfd5c4464c3199046662fb08aee77a33e6524c88caa98466`;
  integration DLL `ffbbf5a218c4d9379d96b3d914333a7b036c32d9ea022be043fd197a23490245`.
  Portable-PDB source checksum and DLL CodeView GUID match. All pinned inputs
  remained unchanged during the corrected run. No gameplay test besides this
  initial attempt and its specifically reviewed fixture correction was run.

Independent SOURCE accepted both bounded fixture stages; final independent OUTCOME
and documentary closeout review accepted this narrow qualification. Full logs/TRX, both frozen fixture
versions, provenance and cleanup evidence remain outside checkout under
`/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/spiritual-pressure-recovery-qualification`.
Optimization assessment: both genuine publications are the subject of qualification;
nondefault capacity cannot reuse the six-AP template. Existing owner DATA reuse and
fresh mutation/publication checks remain. No new cache, general framework or
performance claim; the actual cost above is bounded and measured.

The [ordinary-input owner proposal](existing-pressure-ordinary-input-design.md)
has independent architecture SOURCE acceptance, not implementation acceptance.
Preferred A makes the GM's scoped complete input an ordinary proposal and derives
actual through the real owner; its authoring semantics still need separate
acceptance. The first proposed contour is before < ordinary < actual < broken.
Capped/no-increase, additional actors/wounds and new no-offer cold resume remain
outside that proposed first implementation. Exact closed wire schema, exhaustive
parser/writer/completion dispatch and an independently accepted causal pressure
oracle are still required before any production change or guard removal.

Separately, the author accepted the formerly pending participant/action declaration
and distinct-wound aggregation rules on2026-11-04, including the disclosed possible
increase in new-wound severity. The exact exchange and canonical rules are in
[strain decisions](strain-open-semantics-20261103.md#accepted-decisions-2026-11-04)
and spec.md. Independent requirements-only SOURCE review accepted that record.
This supersedes the old author-pending labels in earlier maps, but implementation/
qualification of the new semantics stays UNKNOWN. It does not approve A, weaken
no_effect or prove ordinary DATA. A5/H0/browser, CI integration, broader feature
and all unrelated existing UNKNOWN/HOLD boundaries remain unchanged. Nothing was
committed or published.


# Existing pressure option A — closed SOURCE design accepted, 2026-11-04

The technical owner selected A after the genuine birth→guard prerequisite: GM supplies a complete ordinary counterfactual pressure result; the existing owner independently checks it and derives the wound consequence. This is an architectural choice within #1536, not a third author-rule question. The two accepted author rules remain unchanged and unqualified outside their existing evidence.

The [existing design](existing-pressure-ordinary-input-design.md) now specifies exact source-preimage v3 (eight fields), genuine raw-image/pointer binding, retained ordinary calculation, shared non-committable evaluation, actual logical views, C2 cold recomputation, positive C3 owner-pair/receipt identity and historical receipt/parent/actual joins. Independent Astra High SOURCE acceptance at23:11 covered the design at SHA25605b0fa31efe00df141c524951dc8fc0c57cb7de43cf46c5a3e827d017c65303a; later wording records that acceptance and resolves exact existing guide/entrypoint paths only.

Two concrete hazards are explicitly handled, not bypassed: comparison must finish before actual allocation speculation because nested journal scopes fault; conflict effect-carrier writes can collide with the actual owner baseline, so this first contour rejects them instead of rebasing general combat. v3 actual-zero, special/guarantee/retrauma/terminal, no_effect and multiple-wound contours remain unsupported. Existing v1/v2 and source-only guards keep their semantics. Historical pressure is not assumed to need a missing v3 row merely because strain increased; known recorded comparisons receive typed exact/mismatch checks. No hash is treated as proof of discarded dynamic availability or a newly authenticated replacement's omitted history.

No production/test/catalog/selection change, build or runtime occurred in this design block. Main remainsD9FACE, integrationFFBBF5, unitADE9DD; original49 CI selection remains unchanged with its external-destination blocker. The accepted prerequisite1/1 and initial fixture-only failure remain separate evidence. Pressure stage3, the new comparator, cold/C3/C4 and performance remain UNRUN. Next proposed step is one independently reviewed causal Fact/180s, with fresh integration build/discovery/PlanOnly; recent observed setup20.93s wall and build48.61s support a25–45s test estimate, not a guarantee. All named post-design categories have at most21minutes of summed protective ceilings, not expected duration or approval for an aggregate run.

Optimization design uses the existing owner/operation context, full input/role/owner/ordinal/epoch/wound/dependency identity, detached successful DATA reuse and unchanged fresh fences. One ordinary comparison plus actual evaluation is expected; actual computation counts and timing remain unmeasured. Current-start free space was2.1GiB `/tmp` and14GiB workspace. No background build/test remained active. Local design evidence and source inventory are preserved under `local-evidence/wound-history-6007-20261104/spiritual-pressure-option-a-design` outside checkout. No commit, push or CI execution. A5/H0/browser and whole-feature acceptance remain separate UNKNOWN/open boundaries.


# Existing pressure option A — first-admission causal RED, 2026-11-04

Test-only scope after accepted closed design: new `AfterlifeResourceCutoverTests.SpiritualExistingPressurePublication.cs`; shared helper extracted from the genuine birth→guard fixture without changing its original assertions, original15/5 final dice or cleanup; one exact category `wounds-spiritual-existing-pressure-publication` (one Linux exclusive Fact,180s). Original49 selection remains byte-identical `d2f51bbdca7fbccb9902a7e407e69c3e85d2168a0ce454454fd769d7bb0a19c3`, with its existing CI-destination blocker. No production changes, CI or publication. One parent and unambiguous pressure/setback do not qualify multiple-wound aggregation or ambiguous failure evidence.

Independent Sol6.1/XHigh SOURCE accepted the fixture and its final tier-source correction. The fixture retains genuine III birth42, guard43 publication to strainI/AP3/3, signed44 actual parent/root/resources, signed4/12 dice and actual soul resilience tier0 with profile agreement. Public ordinary shape/dice/rules/cost validation passed. Ordinary proposed strainII gives trauma10/maxI; desired actualIII gives15/maxII. These distinct values are a causal target, not a successful new owner proof or manually fabricated canonical actual state.

The unchanged MainD9 owner accepted original intake and resource begin, then refused `AdvanceNextResourceExchangeAsync` with exactly one Error: `spiritual_wound_profile_integration_required`, path `activeConflict.exchangeLog[2]`, Actual `current-generation consumer for spiritual_strain_burden`. Step is null and ordinal0 capture revoked. Full physical file inventory/bytes, generation, absent checkpoint/pending/command and absent publication authority remained unchanged. Only afterward the desired admission assertion `ExistingPressureOrdinaryInput_MustAdvanceOneActualPressureExchange` failed at the new fixture line142. This is the expected causal RED, not a setup failure or a claim that removing the guard alone is safe.

Exactly1/1 case executed and failed; selection complete, zero duplicates/skips, no timeout. Category14.6968513s under180s; runner19.7037490s; TRX duration11.1014188s. Named genuine setup completed in10.575s; ordinary/precondition section0.164s and original intake/resource begin/advance section0.304s (inclusive diagnostic intervals, not added together or a speedup). Owned/runtime cleanup true, fixture cleanup emitted removed=true and actual root `/tmp/boe-resource-materialization-6377efe4ed31446aa959b3226b341dea` is absent. No runtime acceptance for actual admission, C2 cold replay, none/C3, C4, signed45 or late rollback yet. General FR-025/T081-D, A5/H0/browser and unrelated UNKNOWN/HOLD remain open.

Fresh final integration build passed108warnings/0errors in49.21s; catalog valid1433 categories/13384 methods-files in11.322s, no test bodies; PlanOnly exactly1 descriptor/1 case in5.281s. Final fixture SHA256 `ebabfeb8fb9d88caf39b66db310da7abc96553279c9ca1cb29ceaf0e1d963bbe`; helper `18f2a62ead03fa31a48c23e8abd124c4233c9e6da6bca0569b17d4aa076f6f5c`; catalog `a7698ce046a643e112d593a93a4fcd9dec3cfe4c0d1f3dbaa3b96ab4976f7be7`. Integration DLL `270011a90a3c6e89b63a9925dd463872fc47805eb4aa27ada1bfa57ee8aee81b`, PDB `acaa0618c44f17ba42fac5b0f37f25637c91f9a3519e2714eac86cf8afb6a4e5`; Main remains `d9faceef2900a699f792f6f0c4ca6fbd35bdaeb6a172fafe7e6c620baeeb101a`. Portable PDB Document rows match all931 available source files, including both fixture partials; DLL CodeView/PDB GUID agree. Ten virtual generated documents are separately listed, not claimed to be compared against missing disk files.

An initial48.46s successful build compiled the prior reviewed418c fixture while the author made the sole soul-tier correction. The first incremental retry1.33s did not recompile because corrected source mtime preceded that build output. Both facts were detected from actual PDB Document checksums; no runtime used those binaries. Preserved initial source/binary/PDB and logs remain separate. A content-preserving mtime touch forced the final compilation; current source and binary provenance was verified before discovery and the sole run. Postbuild inventory against31438 prebuild pins showed only the reviewed fixture correction and integration DLL/PDB changed; production, TestSupport and unit pins unchanged.

Evidence remains outside checkout at `/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/spiritual-pressure-causal-oracle`. Exact TRX SHA256 `5d0dab144a1c6e87140d6e0e0a5020699542b763b1c3b1ec2791ad2e51fd4521`; summary `957be6b57a3848a2a535dfcbb27092842323a079038456e66b1f81ad826afa3e`. Source freezes, manifests, PDB document evidence, all build/discovery/run logs and parsed diagnostic phases are retained. Independent Sol6.1/XHigh OUTCOME/documentary review accepted this causal baseline and closeout at23:32 UTC, with no blocking findings; no new runtime scheduled in this block.

Optimization assessment: share fixture code, not mutable worlds or proof. The two real publications are the qualified premise; exact pre/post file reads must be fresh to prove inertness, and the single additional soul read verifies the real source tier. No new cache, skipped check or speedup claim. Free space before runtime was2.1GiB `/tmp` and14GiB workspace. Next legal implementation gate is independent acceptance of this causal outcome, followed by the design's cheap typed DATA/lifetime/receipt TDD and separately reviewed affected native selection; no blanket21-minute run or guard removal is implied.

# Existing pressure option A — recovered typed SOURCE and codec RED, 2026-11-04

Bounded test-only continuation of T081-D after the accepted genuine first-admission
RED. Recovery compared all31438 prior baseline pins: no missing files and no
production/binary drift. Only the already staged design text/catalog and two new
untracked typed fixtures were pending. No causal native rerun, production edit,
commit, push or CI run occurred. The original49-entry selection remains byte-exact
`d2f51bbdca7fbccb9902a7e407e69c3e85d2168a0ce454454fd769d7bb0a19c3`.

Independent Sol6.1/XHigh SOURCE review found and resolved two fixture gaps before
compilation. The History-kind negative now constructs a parse-valid rank-III
create→complicate ordinal2 anchor and changes only its second History kind to
worsen before the exact-parent comparison. The new two-row late-actual check
requires null `out` comparison DATA and direct `ReadIntrinsic` refusal after a
resealed wrong actual margin or maximum. Both original categories remain coherent
and bounded at60s. Final reviewed source hashes: pressure preimage
`fecdf0d4216947ab48af6ff3caed5a98922bbc615cc757ce41a8a7d7e104dc16`,
receipt `3a218e59717d96ab717cd20ce2746cfee799607c29ad583bda69ed7837aa2754`,
catalog `b2225f12cc73ae125269f7a44a56ceffa55d0df40eff7cb4dc3fc56d0af8f771`.

The first toolchain attempt failed before compilation because the default CLI
home was read-only. A writable CLI home exposed that SDK10.0.402 requests8.0.32
reference packs while this restored installation contains8.0.33. Neither is a
source failure or TDD RED. Reusing the previous accepted local build environment
(SDK8.0.427/runtime8.0.33) produced a fresh Unit build with64warnings/0errors in
70.20s and Integration synchronization0warnings/0errors in1.33s. This is not an
SDK10, clean-warning or Windows qualification. Exact environment and failed logs
are retained; no download, pack renaming or project/toolchain-source change.

All712 Unit PDB Document hashes match physical sources, including both reviewed
new files; PDB ID and DLL CodeView GUID agree. Unit DLL is
`554470320762ea69ea6b8047012fe28c87532aa3c1535a14d03d6c5cf450d774`, PDB
`d0f13eb7aab86c95e448ed3eff2160c7ba5b02129ad0bf6c6ba540f678bb1611`.
Main remains `d9faceef2900a699f792f6f0c4ca6fbd35bdaeb6a172fafe7e6c620baeeb101a`
in all three output copies. Integration and TestSupport binaries are unchanged.
Catalog discovery is valid1435categories/13398methods-files in11.3014831s.
PlanOnly selected2descriptors,14methods and170cases in5.0787465s; zero test bodies.

Each exact category ran once in a separate invocation, so intentional DATA failure
did not suppress the receipt category through fail-fast:

- `wounds-spiritual-existing-pressure-data`:156/156 executed,2PASS/154FAIL;
  category4.0599727s/60s, runner8.8605818s.
- `wounds-spiritual-existing-pressure-receipt-data`:14/14 executed,0PASS/14FAIL;
  category3.4552270s/60s, runner8.5369623s.

Every one of168 failures has the same `FormatException: Integer outside its
allowed range` at the unchanged `SpiritualStrainPreimageData.ReadIntrinsic`
version1..2 check, line39. The designated
`PressureDataRetainsIndependentOrdinaryAndActualCalculations` positive reaches
that check only after valid parent, complete ordinary/actual shape/dice/rules and
independent ordinary10/maxI versus actual15/maxII premises. This is the codec RED.
The other153DATA and14receipt rows are **codec-blocked prerequisites**, not
observed semantic, C3-pair or History-join rejection. The only passing identities
are exactly version1 and version2 compatibility controls. Both runtime selections
are complete, with zero skipped/duplicate cases or timeout and both owned-tree
and runtime cleanup true. Independent Sol6.1/XHigh PREP/OUTCOME accepted this
classification and exact evidence; no runtime semantic GREEN is claimed.

All runtime source/catalog pins remain unchanged. The full postrun comparison to
31438 earlier pins found only the expected Unit DLL/PDB, staged design and catalog
changes, with no missing files or production drift. Full logs, TRXs, metadata,
source freezes, changed runtime binaries, PDB verification and review findings are
retained outside checkout at
`/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261104/spiritual-pressure-typed-tdd`.
The two initial environment failures and original causal-owner capture remain
separate; no failed or successful native scenario was repeated.

Independent Astra High read-only History seam review confirms the next atomic
boundary. Keep maneuver v1/v2 semantics; add a typed v3 absent/exact/mismatch
receipt-to-full-log join so deleted or duplicate accepted exchanges cannot vanish
through a nullable per-exchange lookup. Select signed receipt presence from the
manifest, not a nullable payload or pressure-delta heuristic, and fail declared
presence with unreadable bytes. Reuse one parsed receipt/History per frame;
summary drift remains only prior-turn top-level `summary` under the existing
`exchangeAtTurn` policy. Publication must check retained-v3 parent History even
with zero materializations. The real signed frame fixture provides missing/hash-
invalid artifact controls without reflection. These are recommendations for the
accepted design, not implemented or runtime-qualified adapters.

Optimization assessment: independent per-case detached JSON is the correct cheap
fixture; positive prerequisite reparsing prevents vacuous negatives and requires
no cache or game-world preparation. Actual category costs above are measured,
not a speedup claim. The remaining owner comparison must use the existing explicit
ValidationOperation lifetime and real computation counters; no authority/cache
relaxation follows from this codec baseline. Parser/C3/History changes must still
land atomically; the live pressure guard remains closed until the owned ordinary
comparison and actual projection are complete. Owner isolation/reuse, signed
pressure History, C2/cold/C3/C4/rollback, both broader author-rule implementations
and all previous UNKNOWN/HOLD boundaries remain unqualified/open.

Independent Sol6.1/XHigh final documentary closeout accepted the checkpoint,
staging status and task update against the retained evidence. No additional build
or runtime was needed; the next bounded work remains the atomic v3 implementation.

## Existing pressure option A — atomic v3 reader stage, 2026-11-05

This bounded stage implements the private v3 codec and its fail-closed C3/receipt/
History readers together. It does not implement the ordinary→actual owner or
change the live pressure refusal. The bounded DATA remains one existing rank-III
player-soul wound, standard Chaos Sea pressure/setback, with
`before < ordinary < actual < broken` and one added step. No multiple-wound,
ambiguous-failure, IV-pressure, capped/zero, healing or general combat support is
inferred.

The witness completes actual shape/dice/rules/actor/art/cost/math/target checks
before constructing v3 DATA. The separate helper validates closed proof/input
objects, the independent complete ordinary payload and exact sibling equality,
then calculates ordinary from the already checked actual inputs. The same actual
calculation object is retained; failed actual or ordinary checks expose no DATA.
Positive v3 now requires the authenticated C3 pair and existing-parent History
join. Signed-frame capture reads any declared-present receipt, fails unreadable
artifacts, reuses one parsed receipt/History and checks every retained v3 in the
active instance against one exact complete exchange. Only the established
prior-turn top-summary drift is permitted. The production publication gate now
checks retained-v3 parent History even when every injury decision was declined.
Existing v1/v2 maneuver/capped-guard, v2 zero/source-only and the live guard remain
unchanged.

Independent Astra High SOURCE review accepted the combined block after two
additional genuine signed semantic-mismatch cases: missing accepted log and a
valid-but-different parent History fingerprint. Those cases mutate only before
the genuine signer; artifact-corruption tests remain separate. A compile-only
missing local JSON-object helper caused five CS1955 diagnostics in the first
36.84s build. The one-line shape helper was independently accepted; this was not
semantic RED. Successful fresh builds used the previously recovered SDK8.0.427/
runtime8.0.33, not SDK10: Unit87warnings/0errors79.49s; Integration109warnings/
0errors49.85s, then fixture-only Integration rebuild109warnings/0errors48.49s.

Catalog1437categories/13410methods-files passed in11.3685112s. Exact PlanOnly
selected7descriptors/226cases in9.1676999s. The first run was explicitly partial:
204PASS/1FAIL,205of226cases,5of7descriptors,34.7019770s. The sole failure was in the
ordinary-absence fixture's unnecessary raw publication setup, before its reader
oracle (`progression_schedule.json` signed coverage); it is not codec/History
RED. The fixture now seeds ordinary exchange42 with matching AP3/3 before genuine
turn43 signing and proves receipt absence without claiming publication authority.
All original no-History/no-validation-error assertions remain. Focused SOURCE
review accepted that correction. Only the modified signed category and the two
unrun compatibility categories were then selected: PlanOnly3/33 in9.1309401s,
runtime33/33PASS in27.4386721s. The four previously passing Unit categories were
not repeated; their source and runtime binaries stayed unchanged.

Accepted runtime union, 226 distinct passing identities across seven categories:

- `wounds-spiritual-existing-pressure-data`:157PASS,4.6215589s.
- `wounds-spiritual-existing-pressure-receipt-data`:14PASS,3.6135119s.
- `wounds-spiritual-pressure-history-data`:20PASS,3.4257015s.
- `wounds-spiritual-pressure-signed-history`:12PASS,10.8640340s.
- `wounds-spiritual-existing-parent-data-positive`:2PASS,3.2584848s.
- `wounds-spiritual-strain-guard-cap-data`:12PASS,3.4371954s.
- `wounds-spiritual-strain-history-data`:9PASS,3.4148689s.

Every category remains under its60s bound. Neither attempt timed out; both owned
and runtime cleanup succeeded, with no skipped or duplicate identities. The first
attempt remains incomplete, not relabeled GREEN. The full union is complete;
the earlier167 codec-blocked cases now reach and pass their named typed assertions.
This is observed typed GREEN, not167 independent rejection proofs or live authority
coverage. Independent Astra High final OUTCOME accepted this bounded union,
source/binary provenance, failure classification, cleanup and optimization limits.

All15 reviewed source pins and18 actual assembly/PDB paths were checked after
execution. Main is `971fc6a065e99c1199537aeafba40225b112097efc9d46d71fee4c6552310441`;
Unit is `389a9aab4d502cccc38ad486bdf5ffba63c1b10a716a8a8770f11df5122f97de`;
final Integration is `54d5b85ea641bebebbe23326326b098e025a0af8d557ff598a09fd7728048561`.
All four Portable PDB/CodeView identities agree. All2965document hashes were
verified:2954disk sources and11embedded generated sources. An initial diagnostic
script wrongly compared embedded SHA-1 document hashes to SHA-256; the resulting
non-emitted classification is withdrawn. The corrected verifier checks declared
hash algorithms and preserves both digests; no rebuild was needed. Full31438-file
baseline reconciliation found no missing file and only25 expected source/docs/
binary changes. Raw evidence and failed attempts remain outside checkout in
`/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261105/spiritual-pressure-codec-history`.

Optimization assessment: one completed actual calculation is reused, ordinary is
computed once, and signed capture reads each needed receipt/History image once.
Independent read-count assertions pass. Transient indexes reuse the parsed frame
DATA; authentication remains fresh across captures. The shared helper constructs
independent mutable fixtures and grants no authority. No new persistent cache,
permission reuse or speedup claim is introduced; the costs above are actual
category measurements. This intentionally client-owned reader-only stage creates
no GM authoring capability, so no GM prompt/example/manifest change is required.
The later owner stage must synchronize its authoring guidance and worked example.

No commit, push, CI, publication or broad aggregate was performed. The existing
49-entry CI selection is unchanged and is not a qualification of this local
stage. The original actual-owner causal RED remains open. Next is the accepted
owned ordinary comparison/probe and actual projection plan, followed by its
bounded native/cold/C3/C4/rollback evidence. Broader accepted author rules and all
previous UNKNOWN/HOLD boundaries remain unqualified.

### Existing pressure option A — owned comparison preparation, 2026-11-05

Atomic v3 readers remain separately accepted as recorded above. The next bounded
block is comparison-only, with the live pressure integration guard closed. The
original genuine actual-owner causal RED remains the baseline. Proposed new APIs
have not yet compiled or executed; their source preparation is not another RED,
semantic coverage or permission to claim actual admission.

Independent Astra High SOURCE review accepted the genuine A/B/C fixture at
`AfterlifeResourceCutoverTests.SpiritualPressureOrdinaryOwner.cs`, SHA256
`98a063b52dd96d6aee1c196ebbc0067d0cf297fb4fd671a8fa7707355e4ec530` (464 lines).
Two initial gaps were corrected: exact external A inertness after disposal and
complete expected resource destinations from genuine conflict bindings. The
single genuine birth→guard signed44 baseline is captured as immutable game-session
bytes, disposed, then materialized into three independent fresh-generation roots.
Actual signed reads, per-copy ordinary drafts, prefix closure, miss/warm DATA,
foreign lease, detached mutation, disposal and exact whitespace invalidation are
source-first assertions, not runtime results yet.

The accepted implementation direction uses a shared private prospective evaluator
below ticket creation, real resource batches, exact one-component isolation and
source-owned ValidationOperation under the source gate. Never copy that operation
into a prospective session. Refusal/failure/cancellation/revoke end unfinished
reuse; physical raw inputs and current dependencies stay fresh. The supplied
active lease participates in the key without changing capture constructor
lifetime. Diagnostic snapshots are explicit/on-demand and limited to the closed-
prefix, unstaged ordinal-zero frontier, outside production comparison/Advance.
No generic private-field walker, mutating Freeze or default empty-object
serialization is acceptable.

The separate source-first common-prefix wait regression also passed independent
Astra High SOURCE review: `AfterlifeResourceCutoverTests.SpiritualPressurePrefixPreparation.cs`, SHA256
`0462110493897887f73ec96e59aa351518ea21cb966bbfb8d636ce003c3e0f25` (144 lines),
category `wounds-spiritual-pressure-prefix-preparation` (60s unknown cost). Exact
retained prefix-wait delegation is released for source implementation, with fresh
owner/input checks before return and ordinary exchange waits unchanged. Its
runtime verification remains required. The ordinary owner singleton also remains
60s with unknown cost. No build/discovery/runtime has run for this block. Actual logical projection,
C1/completion/cold/C3/C4/rollback and authoring guidance remain open, as do broader
invalidation/burden/collision controls and separately proven multi-wound or
ambiguous-outcome rules.

Local evidence is outside checkout at
`/workspace/scratch/2f2a1767475c/local-evidence/wound-history-6007-20261105/spiritual-pressure-owner-probe`:
pre-stage25-file snapshots, working-tree state, boundary and fixture SOURCE reviews,
source-preparation manifest. Production implementation is now active within the
accepted comparison-only scope. No commit, push, CI or other publication occurred.

Independent C4 source review also found a conflicting earlier design promise:
PublicationInputs retains ordinaryII for freshness, while common late-failure
compensation restores SignedRollbackImages (signed44 guard-resultI). The technical
owner explicitly accepted correcting the design to existing transaction semantics.
The future oracle uses independent signed witnesses, proves old authorities
revoked, separates pre-take draft-preserving refusal, and re-authors saved exact
ordinary/output bytes only after complete signed rollback before new intake/fresh
owners. No re-sign, stale C2 restoration or old authority reuse. No production C4
change or runtime claim results from this clarification; the design and tasks now
record it explicitly.

### Existing pressure option A — comparison-only bounded GREEN, 2026-11-05

The shared private ordinary evaluator/resource-batch, exact selected strain-row
isolation, source-owned ValidationOperation and explicit prefix/diagnostic seams
passed independent Astra High SOURCE review across16 production files. Source
findings were corrected before execution: preserve ordinary exchange-wait
precedence; read allocation DATA under both real gates without recursive locking;
derive the lower evaluator's exact basis from its owner rather than caller DATA;
and reject leaked per-exchange batches/mechanics at the ordinal-zero diagnostic
frontier. This stage does not open live actual pressure admission or add a v3
writer. Existing actual-owner causal RED remains separately preserved.

Fresh SDK8.0.427 Integration build completed99.79s with155warnings/0errors (none
on the16 changed production files). Official catalog validation passed1441
categories; exact PlanOnly selected7descriptors/8methods/11concrete cases. First
runtime stopped after1maneuverPASS and1fixtureFAIL,37.6634636s,2/11cases. The new
oracle wrongly expected normalized `player_soul` in a raw actorType field whose
genuine value is `player`. Ordinary comparison had returned DATA and expected
math; later assertions were not reached. This is a fixture assertion failure,
not a production causal RED. Its source, binaries, TRX, diagnostics and cleanup
are retained. Independent correction review accepted only the raw-form assertion
plus explicit canonical ActorKey assertion; complete resource coordinates and
all transition expectations remain intact.

Corrected Integration build completed48.99s,109warnings/0errors. Main and
TestSupport DLL/PDB remained bit-identical, so the independently executed maneuver
PASS was retained rather than repeated. Fresh remaining PlanOnly selected6
categories/10cases. Runtime completed10/10PASS in82.4258070s. Final union is
11 unique PASS across two run/build lineages, not one fresh11-case run:

- ordinary owner1PASS,15.4863241s category: genuine immutable template/fresh
  A/B/C roots, real signed reads, prefix, full ordinary DATA/resource comparison,
  detached mutation, raw whitespace revocation, foreign lease and disposal;
- other burden1PASS,14.4462493s: same legal single III birth with pressure roll
  hindrance, exact missing-hindrance diagnostics on two uncached failed probes;
- lifetime1PASS,14.8437210s: actual physical-read barriers, serialized overlap,
  in-flight/queued Dispose refusal and drained tasks before cleanup;
- prefix preparation1PASS,6.6199982s: repeated real wait identity, no repeated
  prefix advancement/evaluator, genuine receipt closure then ordinary admission;
- zero native1PASS,14.4432637s, and source-only refusal controls5PASS,11.2448856s;
- retained existing v2 maneuver1PASS,16.9008081s from the first run.

Every new singleton stayed below60s. No skips, duplicate IDs or timeout occurred.
Both official runs report owned-tree and runtime cleanup complete; emitted final
fixture roots are absent. Snapshots establish the explicitly guarded closed-
prefix/unstaged/ordinal-zero frontier, not arbitrary engine state. Raw-origin
refusal revokes before another evaluator; successful probes do not advance source,
dice, resource or allocation state. Another applicable burden remains enforced.

Optimization evidence: A's three comparison requests enter the ordinary evaluator
and signed-parent computation once, actual evaluator zero, with fresh input and
dependency checks on each request. Measured A miss151.2952ms, hit57.9510ms and
mutated-detached-result hit52.1813ms; genuine setup10242.412ms is separate. Snapshots
have separate timings and no production-call overhead. These observations prove
reuse within this fixture, not a general speedup. Fresh raw/signed sweeps remain;
the miss has separate insertion and final-return fences, and dependency snapshots
still recompute current graph identities. No safety boundary was removed to lower
counts. Semantic errors invalidate reuse without revoking otherwise current
owners; physical/signed drift and disposal remain fail-closed.

Final Main DLL SHA256:
`37835683af7c12f2a814fbd750bfa68099558e1276ea4126f3129e229e49889d`.
Final TestSupport DLL:
`c45f93f8da65b3d454a5da833568947375d7eef4fb857ea92ec6d3ae43bfe2d9`.
Final Integration DLL:
`da5463cbc3c368b87b8a9ceff4abcd7ce5ba36c31dc9ab8792cfae1b26391626`.
First build PDBs verify2259documents=2248disk+11embedded with matching CodeView;
the failed fixture uses its preserved pre-correction source for that lineage.
Final Integration PDB independently verifies946documents=936disk+10embedded,
all checksums/CodeView matching current source. Full source/build/runtime pins,
phase records,32 preimages, failed/corrected artifacts and official summaries are
outside checkout in the existing `spiritual-pressure-owner-probe` evidence root.

Independent Astra High OUTCOME accepted the exact two-lineage11-case union, source/build/runtime provenance and absence of all13 observed fixture roots. Independent documentary closeout is also accepted. Actual ordinary→actual view,
C1/completion/cold/C3/C4/rollback and authoring guidance remain open. Independent
routing/wound-read/dependency-cut mutation cannot be claimed from this one-last-
exchange/maxII-versus-parentIII contour; no synthetic mutation was added. Broader
multi-wound/ambiguous-outcome rules, full FR025 and prior UNKNOWNs remain unproven.
No commit, push, CI or other publication occurred.


# Existing pressure option A — actual-stage source preparation, 2026-11-05

Comparison-only closeout remains accepted and frozen separately. The next stage
has25 initial source/doc/catalog preimages under the local
`spiritual-pressure-actual-projection` evidence root. Native actual/C1/cold/none/
C3/C4/signed45 extension and a separate common cold-offer observer singleton are
being prepared source-first. Original180s/60s bounds are unchanged; new costs are
UNKNOWN. No actual production changes, new build or runtime result are claimed.
The current actual pressure guard remains closed.

Independent design review chose reuse of the still-current ordinary mechanics
context with full current owner/input/dependency checks and no ordinary isolation
in actual evaluation. New contexts remint contribution identities; neither
reference comparison with a reminted context nor owner-only equality is sound.
The six-field observer will read an already reconstructed offer under submit and
capture gates, without Classify/Open replay, fresh filesystem work or authority
exposure. Used sessions need a separate refusal even if a callback failed before
disposal. Source/tests review and bounded runtime acceptance are still pending.

Negative preparation separately covers admitted raw drift, cached completion,
effect conflict-carrier collision, pre-take draft preservation and late signed44
rollback/fresh retry. The signed rollback oracle is captured independently before
ordinary authoring; retry reapplies saved exact authored bytes and uses new intake
and owners without re-signing. GM guide/example/source guard synchronization is
being prepared as pending narrow A guidance. No multi-wound, ambiguous outcome,
late unsupported dependency mutation, broader FR025 or publication claim follows.

Actual-stage preparation now has independent Astra High SOURCE acceptance for the
native/observer/guidance block and all five negative Facts with their two
null-default fixture callbacks. The readonly observer implementation is separately
SOURCE-accepted in two new partials. It performs no filesystem read, parsing,
replay or evaluator work; immutable joins and pure worsening-wound DATA clones
repeat at its closing check. No speedup or deterministic concurrent-Dispose
exception type is claimed. Sequential lifetime tests remain UNRUN.

The atomic actual implementation is now underway. A required additional seam is
the resource prefix companion itself: it must join authenticated admitted actual
DATA after checking its exact raw ordinary baseline, not rely on C1 merging alone.
The raw original accessor and inputs remain unchanged. Full production SOURCE,
fresh Integration and Unit builds, discovery/PlanOnly and runtime remain pending.
The reviewed bounded selection is15categories/16methods/expected19concrete cases,
with no method duplication. It includes native, observer, five negatives, the
affected seven comparison/v2/zero categories and the changed guidance Fact.
`ListCategories` reported1447 entries; SDK8.0.427/PowerShell7.6.8 are available.
Only metadata was run. No test, build, commit, push, CI or publication occurred in
this new stage yet. Frozen source/review/selection artifacts remain local under
`spiritual-pressure-actual-projection`.


# Existing pressure option A — actual implementation and first runtime, 2026-11-05

Independent Astra High SOURCE review accepted the atomic 15-file actual path,
its two-file immutable cold observer, the native oracle extension, five negative
Facts and synchronized bounded authoring guidance. The actual projection precedes
prospective construction and changes only the selected player strain plus final
root echo. The same current ordinary mechanics context is retained, with ordinary
isolation absent from actual validation. Actual resource transitions must equal
the checked ordinary transitions; the resource companion itself joins admitted
actual DATA against its exact raw baseline. C1, completion and cold replay use the
actual view while authored input remains ordinary. No C3/C4 contract is relaxed.
The raw SourceFrontier accessor is deliberately count-only, not a missed actual
semantic consumer. Conservative conflict-carrier exclusion is retained.

Fresh Integration build succeeded in 101.204098s, 155 warnings and zero errors;
Unit build succeeded in 26.965918s, 41 warnings and zero errors. A missing optional
/usr/bin/time launcher failed before any compiler invocation and was preserved
separately; Python timing then launched the real builds. DLL/PDB source checksums
and CodeView identities match, including generated embedded sources. Official
ValidateCatalog reports 1447 categories and 13420 methods/files, discovery only.
Exact PlanOnly confirms 16 categories, 17 unique methods and 20 concrete cases,
29-minute total protective bound. The added singleton preserves the existing
same-turn C2 correction, which the pressure eligibility branch must not intercept.

The first actual runtime stopped normally after 3 cases: existing maneuver PASS,
admitted ordinary-byte drift PASS, cached-completion scenario FAIL. The failure
is before its cached assertions: Submit-none C2 saved transport confirmed the exact
successor checkpoint/pending pair, then source Prepare incorrectly repeated the
whole-capture check against its old physical control witnesses. This is a genuine
production integration failure, not a fixture failure or cached-drift coverage.
Seventeen cases remain unrun. Wall time was 56.051197s, no timeout or duplicate
IDs, and both official cleanup flags are true. Failed sources, binaries, TRX and
phase evidence remain under the local actual-projection evidence root.

Independent diagnosis approved a two-file correction: source Prepare uses the
exact ordinary conflict/request byte fence, while comparison, C1, completion and
pre-stage retain whole-capture checking plus that same raw fence. Existing C2
transport remains responsible for its authenticated exact successor controls.
No witness rewriting, arbitrary current-byte adoption, fixture change or global
private-control exception is permitted. The narrow fix is SOURCE-frozen, awaiting
final diff acceptance and fresh builds. Repeat admitted drift and failed/unrun
cases; retain the unchanged non-pressure maneuver PASS only after the reviewer
confirms its false-branch exclusion. No pressure completion/rollback acceptance
or publication is claimed yet.

Optimization assessment: eligible pressure reuses the same validated context and
ordinary comparison DATA, with live owner/input/dependency fences still fresh.
The unsupported/no-pressure fallback repeats in-memory membership/contribution
validation and some payload parsing, but routing reuses its existing mechanics
snapshot; it adds no filesystem/signed-parent reads, prospective evaluation or
resource replay merely for that fallback. No speculative cache/lifetime mechanism
or speedup claim is justified by current measurements.


# Existing pressure option A — bounded actual runtime closeout, 2026-11-05

The exact two-file transport correction has independent Astra High SOURCE
acceptance. It preserves the combined original-input helper and fresh counters,
extracts one shared exact conflict/request byte core, and uses its raw-only wrapper
solely inside the pressure Prepare branch. C2 transport still validates its exact
confirmed successor pair before and after source checking. Neither control
witnesses nor fixtures were changed. The first production failure remains evidence.

Fresh corrected Integration build: 98.942097s, 155 warnings, zero errors. Fresh
Unit build: 28.252771s, 41 warnings, zero errors. Exact remaining PlanOnly selected
15 categories, 16 unique methods and 19 concrete cases under the original leaf
bounds, with a 26-minute total limit. Runtime completed 19/19 PASS in 246.684928s:
15/15 descriptors complete, no skips, duplicate IDs or timeout. Both official
owned-tree and runtime cleanup flags are true; all 15 emitted final fixture paths
are absent, and additional owned-copy cleanup is asserted in the Facts. The two
emitted roots from the first run are also absent.

Final qualification is 20 unique PASS across two build/run lineages, not one fresh
20-case run. Only the unchanged non-pressure maneuver PASS, category16.079662s, is
retained from the first lineage. Independent source review verified that it never
enters the changed pressure branch. Admitted-input drift was rerun after the fix.
The first failed Submit-none assertions and all initially unexecuted cases remain
separately attributable; they are not silently relabeled as earlier coverage.

Observed gates and category costs:

- Native actual pressure: 19.056601s. Genuine signed III parent, authored ordinary
  II and actual III, ordinary trauma10/maxI versus actual trauma15/maxII, exact
  single-source/resource transition accounting, actual C1 full root and resource
  companion, unchanged raw ordinary input, first C2/v3 source, one explicit cold
  observation, real none/C3, common C4 and signed45 validation all execute. Normal
  protocol reconstruction after observation remains real and is not counted as
  another observation shortcut.
- Admitted-input drift15.492893s and cached-completion drift18.269928s: the latter
  now reaches Submit-none, two unchanged successful C3 reductions and the exact
  raw-byte mutation refusal. The initial failed run reached none of those later
  gates. Stale owners cannot return cached success or issue another authority.
- Genuine original conflict-carrier collision16.056371s: ordinary validation
  succeeds, actual admission is refused before evaluation/spend, owner revoked,
  external bytes/generation unchanged. This proves early exclusion only, not a
  runtime late collision. Final effect/assembler checks remain source-reviewed.
- Pre-take refusal18.664454s: zero publication writes, exact edited authored draft
  and private pair preserved, old authority unusable.
- Late cut and fresh retry22.083675s: the witness observes three genuinely
  committed targets, then all 83 independently tracked signed44 images/absences
  restore exactly, generation matches and publication journal is absent. Signed
  strain I is the rollback target, not ordinary II. Exact saved authored input is
  re-applied without re-signing; new intake/fresh owners and real C2/C3/C4 publish
  once, with no restored old authority or repeated spend.
- Cold observer9.431927s: repeated reads, foreign lease, explicit disposal, live
  non-offer and consumed-but-not-disposed callback-failure cases execute. It reads
  immutable retained DATA under existing gates, with no new replay or filesystem
  work; this is not a deterministic concurrent-Dispose exception claim.
- Comparison owner16.056443s, other burden16.058116s, lifetime16.054651s and prefix
  wait6.421348s preserve the earlier owned reuse/failure/lifetime boundaries. The
  same-turn C2 correction32.711516s, guidance4.217197s, v2 zero native14.262740s and
  five source-only refusal cases11.636527s preserve their selected consumers.

Measured native admission executes one ordinary evaluation, one actual evaluation
and one signed-parent computation, while retaining five fresh-input and eight
fresh-dependency checks. Owner advance512.9303ms, actual C1/resource read35.5682ms,
first C2 commit140.6850ms, explicit cold open480.4972ms, passive observation13.1709ms,
none/C3 1691.3970ms, C4 1225.6745ms and signed45 validation330.1350ms are separately
recorded. These are bounded fixture measurements, not a general speedup. The
fallback's repeated in-memory contribution checks/parsing remain a disclosed cost.

Final Main DLL SHA256:
`a26ef5752bd9c6ec4a792770f56ae5ef064aba7ed4da6a8a43241669e55cbd84`.
Final TestSupport DLL:
`ff6e1539d5af8dd9e136144938d57d8e02892a9e48bb9243126ec88f8f3e0299`.
Final Integration DLL:
`4af46852741fbe4be1dc7c658c25e28fad43b18f732a19645d2b6dbc81afd2da`.
Final Unit DLL:
`a01a3d5bc792e34a3fd82ca92051aaff57dbc7f8367da3801b331f378213d69e`.
Both lineages have matching CodeView/PDB identities and 2979 checked documents:
2968 disk sources plus11 embedded generated sources. The first lineage's two
changed production documents use their exact preserved preimages. Frozen binaries,
versioned reader outputs, source snapshots, full logs and runtime evidence remain
outside the checkout in `spiritual-pressure-actual-projection`. Regenerating first
reader metadata from its frozen PDBs restored overwritten derived text outputs;
it did not rerun tests or substitute current source for historical source.

GM guide, worked ordinary/actual fragment, existing manifest row and its Unit
guard are synchronized. Their durable disclaimer distinguishes intrinsic example
validation from runtime qualification; no GM-authored v3 proof or permission to
bypass a refusal is introduced. Existing daemon/launcher and rules already require
the full guide, so no broader prompt activation is added for this bounded draft.
Mortal World/Shining Abode, multiple or rank-IV wound application, ambiguous failed
actors, capped/no-increase, zero-ceiling pressure, terminal/replacement carriers,
independent late carrier/dependency mutations and broader FR025 remain unqualified.
No commit, push, CI run or publication occurred. Independent Astra High final
OUTCOME and documentary closeout accepted this bounded actual stage. All 32
closeout source/document pins and both provenance lineages were checked. The 15
phase-emitted paths plus one additional rollback-witness root give 16 observed
final roots, all absent. Broader feature acceptance remains open.


# Accepted strain rules — next direct contour reconnaissance, 2026-11-05

The previous actual-stage296-file evidence inventory was rehashed unchanged:
239138375bytes,20uniquePASS across2source/build/run lineages. New planning evidence
is separate under `spiritual-pressure-multiple-design`; the earlier accepted
sources, binaries and outcomes are not relabeled as general qualification.
No production, test body or category changed, and no build/test/publication ran.

Two independent source investigations recommend known player pressure/setback
aggregation before ambiguous failure attribution. Current isolation, actual
recognition, closed v3 and parent/History joins are genuinely singular. An arbitrary
exchange field can survive retained payloads without validation; no existing
participant-failure verifier turns it into authority. Therefore no new declaration
field or outcome table is part of the proposed next contour.

The plan first requires two real terminal C2/C4 creations in distinct conflict
instances and complete accepted-state carryforward to a third signed instance.
The position-aggregation start pattern is available but does not prove this new
pressure fixture. Proposed second birth via binding/setback needs leverage even
on failure, exact modifiers/dice/costs, unchanged control and a sufficient source
maximum. Any proposed defeat closure must describe the real outcome; a different genuine
terminal route requires prerequisite review, never the old helper's hardcoded win.
The initial proposal assumed cumulative resources across instances; the source
feasibility correction below supersedes that opposition-side assumption. A fixture
failure stops this prerequisite and is not semantic RED.

Proposed known-failure target inputs start lawfully at clear. OrdinaryII with two
parents reaches actualIV (exact boundary), while ordinaryIII plus two saturates at
IV (overflow). At margin8/tier0/resilience0/hostile capIV, ordinary trauma13/maxII
or18/maxIII becomes actual trauma23/maxIV. These are design calculations, not new
runtime proof or an automatic wound choice. Active broken is already legal in
other accepted owners; source-free/no-increase remains a separate storage problem.

The proposed private v4 retains one ordinary/input/calculation and a canonical
unique set of two complete parent/component proofs; it does not widen v3. Group
isolation must happen once for the complete set, with all other burdens retained.
Every parent must reach exact historical joins and cold reconstruction through the
existing source/resource/effect/C1/C2/C3/C4 owners. No first-parent shortcut, second
source row, persisted permission or global control exception is acceptable.

Optimization finding: current `SpiritualWoundCurrentData.ReadActive` performs full
catalog/identity/History validation per parent. A naive two-current/two-signed loop
would repeat it four times. The proposed design batches exact parent resolutions
within each existing authenticated immutable view and source operation, preserves
full graph validation and fresh identity/dependency/ownership checks, and measures
actual source work before any performance claim. No global/static cache is added.

See `existing-pressure-multiple-wounds-plan.md` for finite prerequisite, causal,
typed/receipt, owner, native and rollback/guidance gates. New costs and runtimes are
UNKNOWN; the existing180s native protective bound is not preemptively raised.
Independent concrete-design review approved the direction and required a causal
stage clarification: unchanged production cannot succeed at whole-group ordinary
isolation before that feature exists. Pre-RED proves full retained ordinary rules,
dice/calculation and genuine fixture/resource facts; GREEN must later prove the
real whole-group owner evaluation. Source tracing predicts the boundary reaches
the singular-group restriction while overflow hits the earlier old +1 rank gate;
record actual first failures separately and do not call either blocked stage clamp
coverage. Parent-array ordering is canonical, not JSON property ordering. A legal
terminal route must preserve a lawful next Chaos Sea instance; no realm/incarnation
requirement may be ignored to carry the fixture forward. Independent Astra High corrected design/documentary review accepted the six-file
fixture-first planning block. The two-parent fixture, owned group comparison,
schema implementation and runtime remain UNRUN; no production release or broader
semantics qualification follows from this planning acceptance.


# Two-parent fixture — prerequisite SOURCE correction, 2026-11-05

Before implementation, independent Astra High review confirmed a plan contradiction:
terminal C4 retires the opposition side resource owner; the reducer rejects further
transitions on that coordinate. A new distinct conflictId gets its own owner with
normal maximum initialization. There is no supported opposition9→6→3→0 continuity.
No code, category, build or runtime had been changed at this finding; it is not
a semantic RED or a change to the accepted strain rules.

The corrected fixture preserves cumulative player AP and complete ResourceState/
History. Every prior opposition owner remains retired/absent with unchanged recorded
history. Each distinct new side owner must pass genuine start composition and
publication, exact initialization/spend/retirement and canonical agreement before
signing. No manual current seed, history deletion, transfer or reactivation is
allowed. Genuine loss closure, start route and complete fixture feasibility remain
UNRUN, with a stop on any concrete contradiction.

The source also lacks a typed direct export of the capture's private mechanics
context. The prerequisite instead uses authenticated signed/current parent, root,
identity, receipt and History checks plus existing typed snapshot/contribution and
source/target composers. It proves exactly two applicable contribution DATA rows,
not group authority or ordinary isolation. No reflection or new production seam is
introduced. Reuse the snapshot's parsed DATA in one lease/read window, retain fresh
inertness/provenance checks, and measure real preparation costs. No shared mutable
fixture/cache or speedup claim.

The corrected plan retains a singleton180s protective runtime budget, cost UNKNOWN.
Independent SOURCE of the actual fixture, fresh builds, catalog discovery and exact
PlanOnly must precede runtime. Previous20uniquePASS across2lineages and all296files
of actual-stage evidence remain preserved. The source blocker and independent
feasibility review are retained under the external spiritual-pressure-multiple-fixture
evidence root. No publication or production/schema work is authorized.


# Two-parent prerequisite — first singleton preparation failure, 2026-11-05

Independent Sol6.1/XHigh SOURCE accepted the new fixture/category/selection after
a narrow cleanup correction: allocation is retained by the existing callback and
disposed even if initial bootstrap/seeding/signing throws. Production/schema and
all1210 pinned production/policy inputs remained unchanged.

Fresh Integration build succeeded in53.046381s with109 warnings/0errors; Unit
fresh build succeeded in2.003324s with0warnings/0errors. Four PE/PDB pairs matched
all2980documents (2969disk/11embedded); copied test dependencies match their source
builds. Inventory discovery found1448categories/13421methods/files and executed
no tests. Exact PlanOnly selected one Integration Fact under180s, exclusive, no
frontend.

One authorized prerequisite run completed1FAIL/0PASS, no skip/duplicate/underfill/
timeout. Category5.6631419s, runner wall10.8235235s; outer wrapper12.126876s. The
sole failure is new fixture line297: expected ActualEvaluations0, observed1, after
first source admission and closed-exchange signed dice/actors/trauma20/maxIII checks
passed. C2 transport and every later parent/start/final-observation stage were
unreached. SourceTransaction.cs476 counts every non-isolated prospective source
evaluation; it does not mean only pressure projection. Independent OUTCOME review
confirmed this is a diagnostic test-oracle error, not strain semantic RED.

The minimal proposed correction checks exact ActualEvaluations delta1 around the
real Advance, retaining OrdinaryEvaluations0 and every existing semantic oracle.
No automatic rerun, budget increase or production change follows from this failure.
The complete fixture cost remains UNKNOWN; measured failed preparation alone is
not its cost. Named initial preparation788.6006ms and cleanup0.8913ms are retained,
not a speedup claim. Owned/runtime cleanup succeeded; the emitted fixture root is
absent and no test processes remain. First frozen source/build/PDB/log/TRX and
source/OUTCOME reviews remain outside checkout under spiritual-pressure-multiple-fixture.
The previous20uniquePASS/two-lineage evidence is unchanged and not added to this run.
No commit/push/CI/publication occurred.


# Two-parent prerequisite — first birth proved, local start blocked, 2026-11-05

After independent counter-fix SOURCE acceptance and explicit parent authorization,
fresh Integration build succeeded in51.877847s (109warnings/0errors); exact singleton
PlanOnly and all four PE/PDB provenance pairs were checked again. The new run
completed1FAIL/0PASS, category12.9068206s/unchanged180s, runner18.5447673s, outer
wrapper20.032745s. No skip, duplicate, underfill or timeout; complete runtime/owned
cleanup and emitted root absence were verified. The92-file first-outcome capture
remains byte-identical; second binaries/PDB and runtime are separate.

The corrected diagnostic assertion passed. The complete first genuine pressure
III C2/terminal C4 birth also passed, including player10→7, opposition6→3 then
Retire, full History append/canonical agreement and first parent/receipt closure.
Named birth cost7074.3529ms, initial preparation1129.86ms and cleanup1.0163ms;
full two-parent cost remains UNKNOWN. No new cache or performance claim.

First failure was second-start plan.IsValid, before TryCommitAsync:
resourceHistory.coordinates[1] violates composed exact authority. Second start,
binding birth, second parent, final signed contribution observation and all strain
aggregation work remain unreached. Independent Sol6.1/XHigh OUTCOME accepted this
precise failing result; it is prerequisite failure, not semantic RED.

Targeted independent Astra High source review traced the cause to local
AfterlifeOwnerResourceStatePlanner.Build: complete retained History is checked
against only current/proposed-root authority, dropping the prior retired side.
CanonicalResourceOwnerAuthorityComposer can reconstruct that historical authority,
but local BuildCore reaches it only after the failing planner. Do not patch
production or erase History to make this fixture pass.

The existing general signed accepted-turn path merges authenticated historical
owners and has a common-start/publication test. The next safe step is a fixture-only
SOURCE revision to that route, with distinct signed start and birth turn/request
chronology, all original lineage/resource assertions and full History retained.
This route with the current terminal wound history is not yet runtime-qualified;
independent SOURCE/PREP and an explicit parent retry gate remain. No automatic
third execution, production/schema edit or publication occurred. Both old accepted
20PASS lineages and all prior evidence remain separate and unchanged.


# Signed-start fixture proof boundary — SOURCE only, 2026-11-05

The fixture-only revision uses separately signed common starts43/45, binding
birth44 and final target46. SOURCE review found no route/chronology/history API
defect, but correctly withheld a claim of direct accepted-activation transcript
observation: that transcript is not exported by the issued common plan.

Targeted independent Astra High review accepted the narrower DATA-only proof. The
completion token is privately bound to its actual effect plan and issued only after
complete transcript/plan-authority/activation/journal agreement. The fixture obtains
the genuine registered common plan, publishes that same plan, compares exact
canonical after-images, permits only the stated root trigger append and retains
strict old identity prefix, parent, receipt, History and availability checks.
It need not independently requalify the activation engine to prepare genuine DATA.

Do not claim directly observed activation authority stamps, actual nonconsumption/
use counters or transcript nonterminal results. Those are outside this fixture's
observation; source-bound consumeUses=false is a checked definition and exact
canonical output is checked, not a substituted transcript. No reflection, new
production seam or manufactured authority is added. Revised common-start runtime
and full fixture cost remain UNRUN/UNKNOWN, singleton180s unchanged. Independent
final scoped SOURCE and explicit parent retry approval remain before execution.


# Two-parent prerequisite — third failure and ownership blocker, 2026-11-05

After final scoped SOURCE accepted the honest sealed-plan observation boundary and
explicit parent authorization, fresh Integration build succeeded in52.815979s with
109warnings/0errors. Four PE/PDB pairs and all2980 source-document checksums matched;
the exact single Fact PlanOnly retained180s. Third runtime completed1FAIL/0PASS,
category12.2966725s, runner17.7715991s, outer19.123607s, without skip/duplicate/
underfill/timeout. Runtime/owned cleanup succeeded; emitted root absence and no
remaining test processes were checked. Earlier92/129-file captures remain intact.

First genuine III terminal C2/C4 again passed, with its source/receipt/resource/
retirement assertions. First failure was raw start validation line407:
pending_turn_snapshot_reader_coverage_missing for progression_schedule.json.
Registered common plan lookup and all subsequent historical-owner/start/effect/
second-parent/group assertions were unreached. Named initial preparation817.5965ms,
birth6548.2039ms and cleanup2.1817ms do not measure the uncompleted fixture.
Independent Sol6.1/XHigh OUTCOME verified this exact scope and evidence.

Read-only full prerequisite audit identified why a schedule-only patch is not a
sufficient correction. Independent Astra High review confirmed: signed closed
conflict plus genuine active same-realm parent selects natural recovery before the
ordinary resource core. Complete source coverage would next require a real prepared
safe cycle; even with that cycle, natural admission compares protected conflict
bytes against the signed original, incompatible with authored mode=start. These
are conditional SOURCE conclusions, not extra observed runtime failures. Parent
stabilization/healing tier0 does not remove it from the selected natural cohort.

The existing local start separately loses already-retired side authority; the
original source-only C3 path cannot publish PriorStartDeclaration. No sufficient
existing fixture-only typed route preserving all genuine parent/resource History
was found. Do not fake a selector, suppress recovery, weaken baseline freshness,
change signed data, drop History or add a fourth blind run. Fixture source remains
c6fa84be90e8c314b3c7b1a104741511e5698db437287e6624e7937a15415191.

Next authorized work is read-only minimal ownership design exclusively at recovery/
start: existing owners, invariants, exact causal tests and affected scenarios.
Production or a broader combat framework change requires independent review and
explicit scope agreement. Any real timing/healing rule change must be surfaced;
the already accepted distinct-wound/failure rules themselves need no new decision.
No production/schema change, strain causal RED or two-parent PASS exists. Three
failed fixture lineages remain separate from older20PASS/two-lineage acceptance;
no commit, push, CI or publication occurred. Full local evidence remains under
spiritual-pressure-multiple-fixture outside checkout.
