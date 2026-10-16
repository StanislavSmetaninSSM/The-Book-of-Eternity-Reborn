# T197 Q-L1c corrected evaluation catalog — affected independent DESIGN rereview

**Verdict: ET-R1 and ET-R2 RESOLVED. No new material finding in the affected scope.** The corrected catalog is suitable for partial conditional discussion. It remains **UNACCEPTED / BLOCKED**, not a complete T197 DESIGN release prerequisite, transition-qualified contract, implementation authorization, or live F1 evidence.

Reviewed `9c7f3a0da1b1752c6acfc547d24d441b9782d302` against parent `fb6c1b22c9c1dee0b6fcd8dee5c017176921e27c`; historical source `901435333b00ad7e92cef80429ce99ef010c7db9`. Observed HEAD at report sealing: `f6e64ae954b36624ccd18a93dd9f524ea2a5717f`. Review uses those pinned Git blobs, not a moving worktree. The actual retained reviewer configuration is `gpt-6-astra`, reasoning `xhigh`, original `fork_turns:none`, confirmed by the parent; backend telemetry is **UNKNOWN**. Author configuration is separate Astra/High. I did not author the proposal. Parent is sole repository writer; no subagents, repository mutations, builds, tests, application runtime or discovery were used. Only this new `/tmp` pair is written.

The eleven-path carrier includes corrected T197 author MD/JSON, lossless pair-diff gzip/index, byte-exact initial review copies, canonical/plan/tasks metadata, and a separate T192/D9 consultation pair. The latter is future-UNRUN context: its implementation proposals and Pc status metadata are **not independently qualified by this rereview** and do not qualify T197. The original author pair and initial independent findings remain immutable.

## Resolution of confirmed findings

**ET-R1 — resolved.** All forty rows now distinguish local early returns, equality of a particular write, derived inputs and independent effects. Their reader semantics explicitly separate null returned for blank/missing/JSON-null/parse failure from authenticated physical absence; `ReadObjectAsync` adds the nonobject cast. The shared write predicate compares ordinal reserialized node strings, while validated item IP13 compares `JsonNode.DeepEquals`. Neither is presented as a complete accepted-family no-change receipt.

The important corrected branches match source:

- N24/N25/N27 require the actual conjunction of null current, null original and empty derived entries. N26 also requires no explicit project surface, even when lists are empty; N28 tests both original/current core-derived chronicle arrays. Missing sidecars can therefore still require writes. Source: `CanonicalStateNormalizer.Factions.cs:138–367`.
- N03/N04/N07/N10/N11/N29 and actor journals retain their actual supported array/nonobject processing rather than a blanket object-only skip. N03 appends a power journal only with entries; the no-entry power-journal repair belongs to N06. Equal primary roots do not suppress those independent effects. Source: `CanonicalStateNormalizer.GuardiansAndProjects.cs:403–494,569–739,804–960`; `CanonicalStateNormalizer.QuestsAndRivals.cs:26–41`; `CanonicalStateNormalizer.InventorySidecars.cs:89–107`.
- N02 retains validated per-IP13 publication and the separate ordinary transfer/identity behavior; zero transfers is not a whole-method no-op. N13 retains independent clue-driven writes after its initial equality check. N22 distinguishes a return after a possible canonical-wrapper write from a skip. N30/N31 and selected item/quest tails match their actual helper result/disposition behavior, including cloned-current equality rather than invented early success. The two supplemental helper pins support these refinements.
- N15/N16's actual C4 root-owned skip does not become whole-root no-change; actual domain/realm validation remains necessary. The NoPlan guard is not a target acceptance or empty-result capability. Recorded normalizer overlaps match intersections of their declared destination sets; surrounding/common owners remain separately named dependencies.

T17–T19 add future coverage for these distinctions. Their status remains UNRUN. This resolves the DESIGN source error; it does not certify an implemented detached planner.

**ET-R2 — resolved.** A15 now explicitly records `EnsureClientOwnedSystemFilesHealthyCoreAsync` → `StateManager.RefreshGameStateAsync` → `ApplyPlayerSoulProfileClientAuthorityForRefreshAsync`, after orphan cleanup and before the control-preservation calculation and A02. It also identifies possible repeated profile publication after canonical refresh and later accepted validation. Sources: `GameEngine.ValidationAndRepair.cs:145–153,1176–1179`; `GameEngine.SessionAndSnapshots.cs:183–202,252–283`; `StateManager.cs:134–174,619–623`.

A15 names profile/soul/Shining inputs and the profile-root-only output; player-soul projection selection; null/unusable and DeepEquals branches; exact-byte no-op preservation; original receipt-returning versus trusted-local publication; real recovery before/after work; committed/uncertain handling; and subsequent live aggregation. It preserves the public StateManager overload's logging of non-uncertainty/non-replacement repair errors and the internal overload's propagation, without calling a caught failure accepted no-change. Source: `AfterlifeEntityProfileState.cs:351–465,483–504` and `StateManager.cs:134–174`.

Q-E2/Q-E3/Q-E6 and N16 now explicitly retain A15/N16/C4 overlap, exact source roles, real publication/recovery debt and live obligations. Post-E1 recovery must consume/validate the accepted result and discharge real obligations rather than invoke A15 as an untracked writer. Full aggregation inputs and the complete target receipt remain OPEN. T20 covers the new owner branches, dispatch/debt and live refresh; it is UNRUN. This is a correct owner inventory and an unresolved composition requirement, not an invented present-day issuer.

The actual `RefreshRuntimeStateCoreAsync` also has a staged-browser read-only branch (`SessionAndSnapshots.cs:190–198`); the source permits repeated mirror writes on the ordinary branch. A15's statement that it can run repeatedly does not grant permission to replace that read-only branch or other real dispatch predicates. Platform and browser qualification remain outstanding.

## Affected dependencies and limits

The minor callsite citations are corrected: normalizer span starts at :416 and A04 identifies QTE health at :174. The removal of ready/repair from the direct current-turn reader lists is supported by `TryReadCurrentTurnNumberAsync` (`CanonicalStateNormalizer.GuardianProjectHelpers.cs:1383–1407`), which reads only `input/turn_request.json`. Separate original/terminal/lifecycle validation and the unresolved transitive closure remain; this is not a reduced authority requirement.

A02 still identifies config/mods/manifest as logical paths under `game_session`, adds no `game_settings.json` output to the accepted-health route, and leaves cross-owner atomic receipt and live settings application unaccepted. Existing preparation helpers justify policy-preserving extraction work, not a new general publication capability. Missing routine D11 helpers do not themselves reopen permission; genuinely new lifecycle/current-G authority remains a separate design decision.

The canonical handoff link now points to the corrected pair and initial findings, accurately states affected rereview pending at the pinned candidate, and retains all blockers. Plan/tasks current headers agree. Older chronological entries remain evidence, not current release claims. E3 still excludes separate Explorer Archive/Skip actions and their soul writes. No N82/FileMapping/catalog membership grant, generic namespace admission, source replay, forged absence receipt, copied capability or old-G rebind is introduced.

Six interfaces remain OPEN: complete validated raw inventory; health/output and live obligations; ordered overlap/compensation; guardian/cleanup receipts; transitive mechanics/item/spiritual allowed inventories; and durable complete EP/E1 receipt/crash ownership. DEP-C/I/L, full target/intake/bootstrap, H2/H3 and new joint QTE remain blocked. D4, guided, visual/DOM, Windows/platform and D8 limits remain. No automatic F2, suppression of the genuine evaluation turn, or T197 implementation is authorized. These declared incomplete decisions are not additional newly discovered defects.

## Static evidence and artifact lineage

All 135 original source pin records are unchanged, and both supplemental pins verify: **137/137** exact commit/blob/SHA-256/byte records. All **61** numbered source excerpts match their pinned Git slices. At the candidate, only the historical canonical handoff context pin differs; the other 136 source/context pins are unchanged. Three previously reviewed settings/path-resolution source pins were also rechecked and remain unchanged.

All 16 catalog arrays, 73 constant records, 181 literal mapping rows, 50-destination normalizer union and mechanics families remain identical to the initial author pair. The source still has the same forty ordered calls. This affected check reused the initial review's bounded catalog/source conclusions; it did not run or claim runtime discovery or full transitive target closure. The added four future cases bring the total to **20, all UNRUN**.

Full eleven-path carrier diff (cwd `/workspace/The-Book-of-Eternity-Reborn`):

```text
git diff --binary --full-index --no-ext-diff fb6c1b22c9c1dee0b6fcd8dee5c017176921e27c 9c7f3a0da1b1752c6acfc547d24d441b9782d302 --
```

SHA-256 `3cf0bf3d725d651c769598e36162a37bae962cb89335380840a9453e0f4289de`, **905109 bytes**. `git diff --check` returned exit 0 with no output. Exact per-document blob/SHA/length pins are in JSON.

Corrected author MD: `6ccd0ae16751c68a0e5276fb35f1bc8b41d45a7ec07e358e73eee8d826d40fbd`; corrected JSON: `6962060bc352ba0f119864af453f5aa92f7f749a38a802cb2d31492b50f9c27a`.

The separate old→corrected author-pair diff was independently regenerated using Python `difflib.unified_diff`, UTF-8 line-preserving inputs, n=3, MD then JSON, exact `/tmp` old/new filenames. Result: **501093 bytes**, SHA-256 `261ae6f38412be9451b6f13064c2e692ed5963ce90b098129f4103601c5919c4`. MD component: 72067 bytes / `246ca938883ffd5cad305aca2859cc1a5f5fa2d07f7a001edecf46157244afb9`; JSON component: 429026 bytes / `361c93581da4f57fc3263c2d15d65d111d311a44f9c0aa8b69c8020ee7323a4a`. Stored gzip: **65480 bytes**, SHA-256 `28bb083bc83dafe8c8d7631a4174743a23faa19827019437be60ab7dbf7c7af3`, mtime 0. Decompression is byte-exact and matches the index; raw unified context whitespace is preserved. It is not a runtime artifact or source patch.

The copied initial independent pair is byte-identical to the original `/tmp` reports (`387f12a8…` MD / `679da2a3…` JSON); the original author pair remains byte-identical as well. All exact pins are retained in the accompanying JSON.

**FINALSTOP — affected independent rereview complete; ET-R1/ET-R2 resolved; no new material finding. No pending writes, calls or repository/runtime actions.**
