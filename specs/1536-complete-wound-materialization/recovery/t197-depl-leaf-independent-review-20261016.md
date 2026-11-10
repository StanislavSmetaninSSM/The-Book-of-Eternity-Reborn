# T197 Q-L1 leaf refinement — independent initial DESIGN review

**CHANGES REQUIRED: LC-R1, a narrow E3 phase/write-attribution error.** The 16 expanded registries and 73 constant-resolution entries match the pinned source; that successful static extraction does not validate the incorrect LF16 phase projection. Correct LF16/E3 before accepting this refinement for discussion. The proposal remains PARTIAL / UNACCEPTED and all broader T197 gates remain blocked. No actual gameplay grant, runtime change or activation is inferred.

## Exact reviewed scope

Candidate `0ced7235862944f7431cf6ee451a88b8af96336a`, sole parent/base `c6cc3263a3afdc759f82052298a3cefca5ef330b`, author source `61ad49a689af34d37112435412aaf4c7a23d53e1`, repository `/workspace/The-Book-of-Eternity-Reborn`, branch `1536-wound-b2-cloud-20261012`.

The complete five-document diff comprises the new `recovery/t197-depl-leaf-catalog-20261016.md/.json`, its canonical death-handoff link, and plan/tasks metadata. Plan/tasks also update unrelated T179-Pc status in this commit. Those status bytes are included in the diff pin, but their runtime assertions are not independently reviewed or used as T197 evidence. The inspected sources are immutable candidate Git blobs. Moving parent implementation/worktree content is outside scope.

Independent reviewer `/root/t197_independent_review`: retained actual configured/requested `gpt-6-astra`, reasoning `xhigh`, original `fork_turns:none`, explicitly confirmed by parent; backend execution telemetry unknown. The reviewer did not author this proposal and used no subagents. Parent is sole repository writer. The current delegation permits only T197 DESIGN/source review and new `/tmp` reports. Parent-reported staged F1-R2/D9–D11 implementation authorization does not authorize T197 transition implementation before complete accepted design. Missing routine D11 APIs do not by themselves require a new principled grant. Prior review reports remain unchanged.

`Proposal` below means `specs/1536-complete-wound-materialization/recovery/t197-depl-leaf-catalog-20261016.md`; `Catalog JSON` is its matching JSON. All line citations refer to the pinned candidate.

## Confirmed material finding

### LC-R1 — medium: LF16 assigns a separate Explorer archive action to E3

**Location:** Proposal:75,95,127; Catalog JSON:4161–4166 and 4300–4307. LF16 is named `target.E3`, adds `game_state/meta/soul_state.json when real archive action commits`, calls this `KNOWN_WRITES_COMPLETE_FULL_BRANCH_INPUTS_OPEN`, and is then selected by the E3 phase. Its prose also includes archive choice outcomes among E3's outstanding branches.

**Source proof:** `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:2795–2808` calls the evaluation reward display, activates the post-life return guard, then checks incarnation/terminal cleanup. `ShowLifeEvaluationRewards` calls only `RefreshFromCurrentStateAsync` at :2921 and ends with the reward continuation at :3029–3033. It does not call archive or skip selection. `BookOfEternityClient/Services/AfterlifeArchiveCandidateService.cs:128–205` reads soul/codex/lifecycle evidence and writes or clears the candidate manifest; its separate `ArchiveCandidateAsync` writes soul plus the manifest at :283–290, and `SkipCandidateAsync` writes the manifest at :294–307. The actual callers are separate player-selected Explorer actions in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1545–1592`, including selection and preview before the calls at :1575 and :1592. The canonical handoff's E3 contract likewise names the candidate manifest, return guard and proposed M presentation acknowledgement; soul is an input, not this E3 write branch.

**Why this is material:** the artifact is intended to derive exact leaf use and phase ownership. A real method elsewhere in the same service does not establish reachability from the evaluation phase. Retaining its soul mutation as an E3 “known write” can incorrectly enlarge the future E3 cohort, require an unrelated player choice for E3 completion, or make a later separately accepted archive action appear replayable/owned by E3. The existing warnings against automatic actions and the phase's incomplete status do not correct this erroneous attribution. This is a proposed design boundary error, not evidence that a permission was already granted or that current runtime has changed.

**Required correction:** in both MD and JSON, restrict the E3 mutation recipe to the actual candidate-manifest refresh/clear, post-life return-guard result and proposed M acknowledgement. Classify soul/codex/lifecycle/original inputs as the actual read/guard/proof roles; leave full branch input closure explicitly open. If archive-selection operations remain in the source map, label them as separate independently admitted Explorer player actions, excluded from E3 completion, receipt and replay. Keep their real owner/phase/receipt contract separate; do not manufacture archive/skip choices in the life-evaluation reward continuation. Correct the E3 phase guard and Q-L1d wording accordingly. Preserve original author artifacts and provide a new corrected pair for affected independent readback.

No other confirmed material defect was found in this bounded pass. Declared missing full target, intake, cold and lifecycle contracts remain unresolved prerequisites, not additional findings merely because they are incomplete.

## Source checks that succeeded

**Registry expansion:** independently resolved each of the 73 cited constants from its actual `const string` declaration and checked the recorded declaration line. Expanded the source initializer/Concat/union expressions without executing C# and compared exact literal membership to the JSON. All 16 sets match, with no missing/extra/duplicate paths; each JSON leaf row matches its catalog path and has `writable_by_presence_in_catalog: false`.

| Registry | Verified count | Narrow source meaning |
|---|---:|---|
| P22 | 22 | Current closed original-presence domain |
| W40 / O4 | 40 / 4 | Wound required snapshot set / output companions |
| ES15 | 15 | Effect source authority dependencies |
| N82 | 82 | Declared normalizer rollback registry |
| A5 / Q4 / R18 | 5 / 4 / 18 | Realm owners / resource quartet / composition sources |
| M18 / JM42 | 18 / 42 | Existing schema-1/schema-2 marker footprints |
| GR13 | 13 | Named snapshot producer request array |
| B25 | 25 | Pure fresh-bootstrap dictionary output keys |
| GC6 / GC10 | 6 / 10 | Required / allowed correction receipt paths |
| IP13 / IT7 | 13 / 7 | Item projection roots / first-contour refusal guards |

These results establish only the declared source sets. N82 includes W40 and ES15 by source composition; JM42 unions the existing footprint, item, resource and inactive-tail sets. Neither is a complete accepted-target or write-ownership catalog. GR13 is a named array, not all snapshot additions: `GameEngine.SessionAndSnapshots.cs:472–489` also treats capstone/Saref/default roots separately. B25's dictionary does not cover the incarnation call graph. The proposal states these limits rather than collapsing all sets into permission.

**Original image and absence roles:** `GameEngine.SessionAndSnapshots.cs:417–568` captures a manifest and detached authority after the real initialization/overlay steps; `:1018–1045` selects rollback bytes unless the validation overlay requests current state. `:589–682` proves initialized backups contain post-initialization bytes and cannot substitute for historical before-absence. `:1145–1170,2030–2048` distinguishes technical markers, validated rollback mappings and explorer replacement backups. Proposal:40–52 and LF02–06 correctly require separately retained authenticated maps/bytes and no suffix-based ownership inference.

`PendingTurnSnapshotAuthority.cs:10–16,116–125,775–800` supports current format-4 exact snapshot/rollback hashes and exact spelling/alias admission. Its portable hash is local lifecycle integrity evidence, not a standalone capability or strong same-user security boundary (:981–984). `PendingTurnSnapshotReader.cs:66–102,501–550,654–669` supports the current P22 coverage and authenticated optional absence; older supported closed sets do not prove absence for newly added roots. The proposal's current-domain recipe cannot be read as an automatic legacy expansion. Missing keys outside admitted proof domains remain unknown.

`GameEngine.SessionAndSnapshots.cs:685–717` hashes client continuity through text reads and selects the actual stories roster, whereas format-4 original artifact hashing is byte-based. `:1851–1953` checks physical spelling before case-folding, then performs a finite instance selection across game_state/lore plus named outputs. Proposal:47–48 correctly avoids directory walking as new authority, fabricated concrete instance inventories and conversion of text digests to byte digests. The additional inspected `StoryService.cs:113–160` confirms marker turn −1 and actual Mortal/Chaos filenames; accepted append-prefix receipts and durable evaluation runtime-turn acknowledgement still need their separate contracts.

**Resource and HP projection:** `AfterlifeOwnerResourceStateService.cs:180–212` writes selected owner after-images and guards unused owner roots plus definitions. `CanonicalResourceQuartetTransaction.cs:410–448` adds exact global before-image guards and writes or guards authority. The catalog does not turn A5/R18 membership into an instruction to write all members. `ProgressionScheduleService.cs:132–257,1314–1403,1551–1567,1683–1712` confirms the narrow HP schedule/soul/world-time/Shining roots and schedule-only canonical mutation. LF12 adds the proposed M receipt; it does not claim an implemented pure extractor or solve D9-covered time issuance. Its HP after-image may serve the target validation/repair baseline while historical source evidence remains distinct.

**Bootstrap, pending and overlap limits:** `MortalBootstrapStateBuilder.cs:53–145` has the verified B25 keys. `GameEngine.TurnLifecycle.cs:3408–3614` instead uses existing sealed resource state/history, characteristic calculation, projected owner documents, quartet guards, codex merging and registered validation overlays; resource defaults are not blindly installed. Later :3341–3351 waits for the real bootstrap response and separately materializes directives/clears an eligible prepared Shining package. LF18–20 and Proposal:106–108 correctly leave the full caller composition, memory/blessing overlap, D4/player choice and accepted bootstrap graph unresolved. No runtime loss claim follows merely from overlapping source paths.

`SpiritualWoundCaptureCheckpointState.cs:16–21` has the exact header-pair/request physical witnesses; `SpiritualOriginalDraftInputs.cs:77–97,109–145` validates a supplied exact inventory rather than issuing one from its broad path predicate. LF21 and Q-L1f preserve that distinction. No backup map, raw DTO or broad candidate filter grants the missing private pending/cleanup publications.

**Archive and authority boundaries:** Proposal:112–116 retains the actual save exclusions, the difference between canonical `qte_runtime.json` and root `.boe_runtime`, immutable historical G, and the need to admit any path/profile transformation separately. This refinement supplies no new import issuer, native transaction identity, source debt closure or old-G rebind. Routine pure source extraction remains a D11 specialization candidate. Any genuinely new durable lifecycle authority remains the separate unaccepted DEP-C/G-C question. No F2, life-evaluation turn suppression, Shining eligibility from a Chaos source, automatic D4 allocation, or relaxation of guided/visual/Windows/D8 gates follows.

## Completion blockers and evidence limits

Even after LC-R1 is corrected, this remains a partial source catalog. Q-L1a retained role paths/discriminators and acyclic seals; Q-L1b full source proof graph; Q-L1c EP/E1 exact owner contributions; Q-L1d E2/E3 read guards and durable runtime witness; Q-L1e genuine DEP-I intake/choices/IB/accepted bootstrap; Q-L1f private/pending/cleanup catalogs; and the unchanged DEP-L Q-L2–Q-L7/DEP-C source closure, same-transaction receipt, imported-origin/repeated-load proof, cold custody and platform transforms all remain open. All ten phase projections remain incomplete. H2 qualification, H3 release and new joint QTE remain blocked.

LC01–LC12 remain future responsibilities, all **UNRUN**. The source text comparisons performed here do not execute LC01 as a gameplay/runtime check, discover executable IDs, validate a real original inventory or qualify any phase. No build/test/discovery/runtime/browser/visual/guided/platform work was performed. Parent's Pc unit-10/actual-1 semantic RED and other stage results remain separate evidence and are not T197 or live-F1 proof.

All 112 historical author pins match their Git blob IDs, SHA-256 and byte lengths. At the candidate, 110 are byte-identical; only canonical link/status and plan differ. Two additional affected sources (Explorer archive callers and StoryService) are pinned in the JSON companion, for 114 source/context records. Pin verification is not a claim of complete semantic review of every line in all 112 files. Five candidate document pins and the exact diff argv are included separately.

Complete five-path binary/full-index diff: **272,855 bytes**, SHA-256 `a7fba838e043ada6864e573d96c287c7ea82c3a2f49057213759f9ab38c13cdd`. Static `git diff --check` returned 0. Author MD SHA-256 `088e4bac5c708eb6a487893416658945c972a5859034608f31baf61c1eb7fbd2`; JSON `693bfe316f320322ef598cdd2ae8493bfd00b49c7d98e7f9264b12a00fa5711d`. This reviewer did not perform remote network readback; publication/readback provenance is parent-reported.

**FINALSTOP.** Only this new `/tmp/t197-depl-leaf-independent-review-20261016.md/.json` pair was written. No repository/runtime edits, no prior report rewrite, no commits/pushes, no subagents, no pending writes or background work. Initial pinned verdict remains CHANGES REQUIRED for LC-R1; no corrected proposal has been reviewed in this report.

Recorded UTC: `2026-10-16T10:25:55.427304+00:00`; observed checkout HEAD: `0ced7235862944f7431cf6ee451a88b8af96336a`. Review scope remains the exact candidate above regardless of subsequent parent worktree edits.
