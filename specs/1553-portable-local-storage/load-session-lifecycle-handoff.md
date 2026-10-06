# T041-LOAD-SESSION-LIFECYCLE — bounded handoff candidate

Issue: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553). Accepted T031 base `dc62a88a`. Runtime `d4cfb195b4df67af090e0b54357cdb0d6419ef65`; independent actual Sol6.1/xhigh source PASS. Final evidence review/restoration pending.

Existing real console and both browser handlers now connect original owned stop → actual typed Load → mandatory full refresh → one fresh configured CLI at installed generation. The retained original pipe/owner references grant authority; JSON/status/PID do not. Filesystem admission is acquired after stop and released before finish IPC; original quiescent/UI guard spans Load, release and bundle. Independent worker/storage conditions stay mandatory.

No active GM: existing quiescent Load/full refresh succeeds without creating a GM. NotLoaded/RolledBack/Uncertain, failed stop/refresh, cleanup debt, stale owner/generation or ambiguous reply cannot authorize fresh. A confirmed storage Committed survives later failure. No old input or unknown operation is replayed. A cancellation before the fresh decision prevents it; an already accepted decision can settle, and old callbacks cannot revoke a later epoch.

Fresh launch consumes the **installed archive profile** verbatim through M1: command/model/args/cwd/input profile. The actual child reported distinct pre-Load active and fresh archived sentinels. Executor Sol is not a GM setting. Legacy Windows keeps original ConPTY/Job source; StartedNotReady is not schema1 Running or CLI readiness. Native execution is unqualified.

## Evidence

132distinct successful scoped checks =130executed cases +2typecheck receipts;41new+91affected, zero qualification skips. Results are source-pinned bounded sets, not one run at the final metadata tip. Console positive/debt were additionally rechecked2PASS at8aa for truthful Committed finish; these are not counted twice.

| Retained set | Count | Source | Qualified scope |
| --- | ---: | --- | --- |
| [reviewed-positive-green](recovery/evidence/load-ux-reviewed-positive-green/manifest.json) | 10 | `ff500224` | 5protocol +3actual selected Load +early ACK +console cleanup debt |
| [frontend-matrix-green](recovery/evidence/load-ux-frontend-matrix-green/manifest.json) | 18 | `ff500224` | Actual launcher/settings handlers; refresh effect is controlled, not a live renderer |
| [stop-faults-green](recovery/evidence/load-ux-stop-faults-green/manifest.json) | 4 | `ecd14793` | stop receipt loss, logical Uncertain, failed refresh, manual cancel |
| [restart-faults-first](recovery/evidence/load-ux-restart-faults-first/manifest.json) | 3 | `8aa96165` | Only the three passing restart fault rows; failed lost Load row superseded |
| [profile-green-lost-load-timeout](recovery/evidence/load-ux-profile-green-lost-load-timeout/manifest.json) | 1 | `5671d19c` | Only profile PASS; actual active/fresh child argv/cwd |
| [storage-admission-first](recovery/evidence/load-ux-storage-admission-first/manifest.json) | 2 | `1ffda2e8` | Only no-active/worker-debt PASS rows; preparation failures excluded |
| [lost-load-green-rollback-transient](recovery/evidence/load-ux-lost-load-green-rollback-transient/manifest.json) | 1 | `555711c1` | Only lost Load PASS; real server abort and eventual retained Committed/no fresh |
| [schema-and-storage-green](recovery/evidence/load-ux-schema-and-storage-green/manifest.json) | 2 | `d4cfb195` | Actual nontransient RolledBack/Uncertain cut rows |
| [ordinary-schema-causal-red](recovery/evidence/load-ux-ordinary-schema-causal-red/manifest.json) | 8 | `55ac46ac` | Only eight passing refresh integration rows; seven RED superseded |
| [schema-and-storage-green](recovery/evidence/load-ux-schema-and-storage-green/manifest.json) | 19 | `d4cfb195` | Twelve portable unit consumers and seven actual schema GREEN |
| [affected-frontend-green](recovery/evidence/load-ux-affected-frontend-green/manifest.json) | 64 | `ecd14793` | 62Vitest cases +2typecheck receipts |

The accepted source/selection reviews justify retaining unchanged successes. New interruption groups were split to rerun only failed/preparation cases. Actual frontend handler refresh is controlled; complete backend bundle and existing frontend reconciliation are separately proven. Real menu and HTTP current-schema consumers supplied7causalRED→7GREEN; shared strict generation interpretation is reused, with actual post-stop canonical revalidation unchanged.

The complete [execution ledger](load-session-lifecycle-plan.md) retains causal failures separately from fixture namespace/serialization/validation/injection/short-wait failures. Lost HTTP response passed after a proven7.727s healthy bundle, with the existing20s HTTP bound and30s independent guardian unchanged. Nontransient storage cuts are used to qualify terminal RolledBack; transient known-rollback retry policy stays unchanged. No runtime performance/storage retry change was introduced.

Writer integrity audit:36manifests/4619sourcepins/680artifacts/352gzip/42guardianreceipts all verified against exact Git blobs, raw/gzip hashes and ECHILD/emergency0/failures0/deadlinefalse. Own guarded fixture roots were removed after capture; logical Uncertain is not cleared by this physical proof.

Catalog372/11018 has no unmapped/stale selectors, discovery-only0tests. Selection17descriptors/57discovery estimates executes0tests; frontend file estimates expand to actual execution cases. [Qualification](recovery/load-session-lifecycle-qualification.json) names every source and remaining boundary.

## Remaining boundaries and stop

This qualifies configured persistent **neutral** CLI through explicit packaged NativeLineage/M1 in isolated roots. No neutralPackage bypass, provider/model request, real saves, public rollout or whole-game claim. The accepted bounded VT subset does not qualify arbitrary TUI. Queue128 and input-profile schema are controlled implementation choices, not final live-client UX. No filesystem scope spans stop/restart IPC; refusal helper detects registered ambient/bound/main scopes, not arbitrary separately held explicit leases.

Primary existing systemd-user remains mandatory and unqualified here; no manager/setup/downgrade. Native Windows, CodexQ1/Q2/TERM and live-GM acceptance remain separate. Gacha **T031-BROWSER-DIRECT-GACHA-LINUX** and standalone **T031-DAREN-STANDALONE-LINUX** remain real open consumers; this task does not erase them. Four historical F2 Windows-only IDs, exact original reasons and0newexecutions remain in qualification. Cold/nonterminal refusal, no old-session mint/replay, no cold exactly-once/reboot/power-loss salvage remain unchanged.

Final independent evidence verdict, ordinary checkpoint/readback and fresh GitHub-only restore precede writer closure. Stop before another implementation stage.
