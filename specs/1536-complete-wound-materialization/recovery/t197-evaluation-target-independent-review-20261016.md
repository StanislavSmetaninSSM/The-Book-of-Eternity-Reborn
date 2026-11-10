# T197 Q-L1c evaluation target catalog — independent DESIGN review

**Verdict: CHANGES REQUIRED for two source-catalog defects (ET-R1, ET-R2).** The proposal remains a partial, UNACCEPTED discussion document. It is not a complete T197 DESIGN release prerequisite, transition-qualified design, implementation authorization, or evidence of live F1 behavior.

Reviewed candidate `c31dc84a5898af0e45e7cff8825c81230decd7bf` against parent `88323cb481a1950238c48139e74309ae902418ee`. Source baseline `901435333b00ad7e92cef80429ce99ef010c7db9`. Observed HEAD when sealing this report: `fb6c1b22c9c1dee0b6fcd8dee5c017176921e27c`; the review scope is the pinned candidate regardless of later parent metadata/code activity. Five paths changed: the new author MD/JSON, canonical handoff link, and plan/tasks headers. No production/test/source change is in this diff. Pc status statements in those headers are contextual and were not independently qualified by this review.

Reviewer configuration: retained independent `gpt-6-astra`, reasoning `xhigh`, original `fork_turns:none`, as explicitly confirmed by the parent. Backend telemetry is **UNKNOWN**. The author is a separate Astra/High agent. I did not author the catalog. Parent remains sole repository writer; this review used read-only Git blobs and static text/hash inspection, with writes only to this new `/tmp` report pair. No agents were spawned.

The session records staged F1-R2/D9–D11 implementation plus parallel T197 DESIGN authorization; that does not authorize T197 implementation. Repository status text is not independent user-approval evidence. Routine policy-preserving D11 owner extraction is not a new permission request merely because an API is missing. New lifecycle authority and unaccepted target-family composition remain distinct design decisions.

## Confirmed material findings

### ET-R1 — false shared no-change predicates (medium; correction required)

The JSON repeatedly states that the current helper returns for “absent/nonobject input or equal projected serialization.” That is not the source predicate for multiple rows. N24/N25 only return early when **current is null, original is null, and the derived entry set is empty**. Their original/current faction-core inputs can produce a sidecar when its current file is absent. Related compound guards apply to N26–N28. See `CanonicalStateNormalizer.Factions.cs:138–195,198–235,238–367` at the source SHA above.

N03 explicitly handles a `JsonArray` (`CanonicalStateNormalizer.GuardiansAndProjects.cs:440–442`) and then writes the canonical projection at :491. N04/N07/N10/N11/N29 and the shared actor-journal helper likewise do not have the asserted blanket nonobject early return (`GuardiansAndProjects.cs:569–739,804–820`; `QuestsAndRivals.cs:26–41`; `Factions.cs:102–134`; `InventorySidecars.cs:89–107`). This does not mean malformed data passes the full evaluation validators. It means its actual projection/validation route cannot be replaced by an invented no-change witness. N03's additional claim that power-journal repair may happen without appended entries is also misplaced: N03 :492–493 appends only when entries exist; N06 :901–904 performs the no-entry repair.

A future detached EP implementation following those predicates could omit a required contribution or alter existing normalization semantics. Correct every affected row using the actual source branch, including absence versus original/derived inputs, supported shapes, equal projection, and validation failure. Audit all 40 rows instead of only the examples above. Add or extend future checks for absent sidecars with source entries, array projections, the true empty compound guards, and invalid required authority. Keep the original pair immutable and publish a corrected pair. This is a confirmed DESIGN source defect; no runtime failure was executed.

### ET-R2 — missing early profile-mirror health owner (medium; correction required)

Accepted health invokes `_stateManager.RefreshGameStateAsync()` at `GameEngine.ValidationAndRepair.cs:150`, **before A02**. `StateManager.cs:134–158,619–623` invokes `AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthorityForRefreshAsync` before aggregating live state. That owner is absent from the 14 surrounding rows. This finding was suggested by the parent and independently verified against the pinned source; it is not based solely on the parent's assertion.

The owner reads `game_state/meta/afterlife_entity_profiles.json`, `game_state/meta/soul_state.json`, and `game_state/meta/shining_abode_state.json`, projects the client-owned player-soul fields within the profile root, and can publish the whole profile document (`AfterlifeEntityProfileState.cs:351–407,425–465`). Its behavior includes retained backup-publication recovery before/after work, original-recorder/recovery dispatch versus trusted-local dispatch, exact-byte retention for an unchanged projection, uncertainty propagation, and cleanup before subsequent live refresh. Optional mirror decoding returns null for absent, blank, nonobject or malformed JSON (:483–504); that local no-op policy is distinct from overall target validation. `StateManager.cs:144–148` logs other repair exceptions and proceeds with aggregation, while uncertainty/session replacement escape. Those branches must be modeled, not silently strengthened into a different acceptance policy.

N16 later normalizes the same profile path, with its own originals/progression/soul/Shining inputs and C4 root-ownership exclusion. Its presence does not represent the earlier owner, source timing, publication debt, or live aggregation. A generic statement that transitive reads remain open also does not inventory this visible mutating owner.

Add its explicit ordered row before A02, named inputs/destination, actual no-change/error predicates, original/local dispatch and recovery obligations, live-state obligation, and overlap with N16/actual C4 ownership. Keep the complete aggregation read closure and evaluation-family receipt unaccepted. Add future checks for no-op/change/unusable input, dispatch variants, retained uncertainty/cleanup, overlap and live refresh gating. This correction specializes an existing owner; it does not grant generic profile publication or lifecycle authority.

## Confirmed scope and source facts

- All **135** author source pins match exact historical Git blob, SHA-256 and byte length. At the candidate, the 134 runtime/context pins other than canonical handoff remain byte-identical; the canonical design changed as declared. Three additional source pins cover path resolution and detached/local settings preparation. The full pins are in the JSON.
- The actual normalizer call order is **40 stages** (`CanonicalStateNormalizer.cs:414–471`, with N01 at :416), with guardian-project inputs read first. The declared row destination union contains **50** distinct paths. Its difference from N82 is exactly `afterlife_chronicles.json` and `afterlife_global_flags.json`. These counts do not prove valid witnesses or complete target coverage.
- The **16** static catalog arrays and **73** constant-resolution records match the previously independently reviewed corrected Q-L1 pair. The prior runtime source files are unchanged at this source baseline; only old design/plan context differs. The 73 declarations resolve correctly, including six multiline declarations. This turn reused that bounded prior registry expansion, not a runtime discovery pass. The **181 literal** FileMapping field/path rows and line records match the pinned mapping source; they remain routing data, not an accepted target inventory or DTO/ordinary-admission proof.
- The actual LifeEvaluation path waits on a genuine target, validates/repairs it, increments its real Chaos turn, appends story, presents rewards/return guard, checks an eligible incarnation trigger and then cleans up (`GameEngine.TurnLifecycle.cs:2747–2808`). This proposal does not turn that sequence into an existing atomic receipt. Required reward validation includes a new meaningful `lore/chaos_sea/player_chronicle.json` summary, at least ten feathers and a new relic (`ValidationService.BootstrapAndProtocol.cs:329–398`), beyond N82/normalizer membership.
- Candidate health/output computation before E1 and receipt-based recovery after E1 are sound stated boundaries. The source still interleaves health, publication, validation/repair, progression and cleanup. No existing complete EP/E1 issuer was established. Guardian completion, repair/audit, E2P/E2S, E3 and E4 obligations remain separate. E3 does not gain separate Explorer Archive/Skip actions or their soul writes.

## A02 settings boundary and remaining decisions

No second settings-root defect was found. `FileSystemManager.cs:574,662–685` resolves the logical `config.json`, `mods/*.*` roster and manifest under `game_session`; config is not a file at the application BasePath. The roster is top-level, extension-filtered and trusted-path checked (`SystemModService.Prepared.cs:30–58`), with content and last-write metadata (`SystemModService.cs:208–222`). Dynamic filenames must remain exact validated inputs, not a namespace write grant.

Actual accepted health publishes the manifest, then mutates the injected live `EnabledSystemMods`, then conditionally saves config (`SystemModService.cs:69–97`; `GameEngine.ValidationAndRepair.cs:159–165`; `StateManager.cs:116–125`). `PrepareManifestForGmAsync` and `LocalSettingsPreparation` demonstrate detached operations, not an accepted complete evaluation issuer. The latter also covers its own `game_settings.json` projection; that does not silently add that output to A02's accepted route.

Q-E2/Q-E6 explicitly leave the combined receipt and live obligations unaccepted. Their D11 classification is defensible **only for policy-preserving extraction of actual existing owners**. Shared filesystem roots and a possible helper do not grant a new cross-owner atomic capability. The eventual exact contract must identify genuine target/G/original bindings, manifest/config before and after bytes and absence, trusted mod roster/content/metadata, frozen settings/UTC, the actual publication result, live settings application and crash/recovery debt. That decision stays open; no new grant or completed receipt is inferred here.

The six declared interfaces remain unresolved: complete validated raw inventory; health/output candidate plans and live obligations; ordered overlapping projections; guardian and cleanup receipts; transitive mechanics/item/spiritual allowed inventories; and the durable complete EP/E1 receipt with crash ownership. ET-R2 adds the missing named owner within those questions. DEP-C, DEP-I, DEP-L, full target/intake/bootstrap closure, H2/H3 and new joint QTE activation remain blocked. D4, guided, visual/DOM, Windows/platform and D8 limits remain. There is no automatic F2, no suppression of the real evaluation turn, and no discard/reoffer substitute for accepted lawful work.

Minor citation cleanup, separate from the two material findings: the common :418–471 callsite span omits N01 at :416, and A04's :177–179 citation does not identify the QTE-health call at :174. Correct these in the new pair if revising it.

## Evidence and final state

All **16** listed future checks are explicitly **UNRUN**. No build, test, runtime, browser, discovery, restore, platform or gameplay check was run for T197. The unrelated Pc results in status headers were not used as T197/live-F1 evidence. Static Git/text/hash checks are the only verification performed here.

Full reviewed diff command (cwd `/workspace/The-Book-of-Eternity-Reborn`):

```text
git diff --binary --full-index --no-ext-diff 88323cb481a1950238c48139e74309ae902418ee c31dc84a5898af0e45e7cff8825c81230decd7bf --
```

Diff SHA-256 `8d14569ecbaf24bea8547f6c0086413fe0fc3f0839eede414512a75e37711f14`, **314169 bytes**. `git diff --check` returned exit 0 with no output. Author MD SHA-256 `859b2f2e3088cf241a5a0577d5b4428ee57b8fb2349e7cb9740b796370289b15`; author JSON `433594bd787f343fb532898893209bc39c0b9f7c42e05ed0fd3e24216bfc16b5`. Exact five document pins, historical source pins, extra context, prior immutable pair pins and structured findings are in the accompanying JSON. Prior author/reviewer pairs remain unchanged.

Required next step is a new corrected author pair addressing ET-R1/ET-R2 followed by affected independent rereview. Existing incompleteness does not itself become an additional material finding, and correction of these defects will not close the remaining target/lifecycle design gates.

**FINALSTOP — report pair complete; no pending writes, calls, runtime actions or repository mutations.**
