# T197 Q-L1 corrected leaf catalog — affected independent DESIGN review

**LC-R1 RESOLVED. Suitable for partial conditional discussion; UNACCEPTED / BLOCKED.** No new material defect was found in this correction scope. This accepts the correction as a source-based discussion proposal, not a complete Q-L1/T197 design prerequisite, transition qualification, archive schema, lifecycle grant or implementation release.

## Exact scope and independence

Reviewed candidate `ef0a99c8be0d2c314d3e9333afe75feec66e7e40`, sole parent/base `9258ecbd42ef6c8a48c3d6bbaec37ed1fecf2d92`; initial reviewed candidate `0ced7235862944f7431cf6ee451a88b8af96336a`; original author source `61ad49a689af34d37112435412aaf4c7a23d53e1`. Repository `/workspace/The-Book-of-Eternity-Reborn`, branch `1536-wound-b2-cloud-20261012`.

This affected rereview covers LF16/E3/Q-L1d, LC13, correction evidence, preserved original/initial-review artifacts, the lossless diff carrier and canonical/plan/tasks references. The complete candidate-parent scope is nine documentation/evidence paths, including one compressed text diff. It does not review the separate Pc implementation already in the parent commit, moving worktree code, or any runtime result. The previous initial review remains immutable and still describes its own earlier candidate accurately.

Reviewer `/root/t197_independent_review` did not author either proposal and spawned no agents. Retained actual configured/requested reviewer: `gpt-6-astra`, `xhigh`, original `fork_turns:none`, as explicitly confirmed by parent; backend telemetry unknown. Parent is sole repository writer. This delegation authorizes T197 DESIGN review and new `/tmp` reports only. No authorization is inferred solely from repository text, and no routine D11 permission question is reopened by this correction.

## Why LC-R1 is resolved

The corrected author MD:7–9,79,99,131 and JSON LF16/E3/LC13 now agree on the boundary:

- E3's proposed mutations are `game_state/control/archive_candidate_manifest.json`, `game_state/control/afterlife_return_guard.json` and the proposed M presentation acknowledgement. Candidate refresh retains its actual write/delete/no-change outcome.
- Soul, codex, lifecycle/original evidence, guardian context and existing candidate data are identified input/guard/proof roles. The full input specialization remains open; the wording does not claim an exhaustive executable read catalog.
- `ArchiveCandidateAsync` and `SkipCandidateAsync` are separate selected/previewed Explorer actions. Their ownership, receipts and completion cannot be issued or replayed by E3. No archive choice is inserted into the reward continuation.

The actual source supports this distinction. `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:2795–2808` displays evaluation rewards, activates the return guard, then handles incarnation/cleanup. Its reward path calls `RefreshFromCurrentStateAsync` at :2921 and ends with the reward continuation at :3029–3033. `BookOfEternityClient/Services/AfterlifeArchiveCandidateService.cs:128–205` reads soul/codex/lifecycle data and writes or clears the candidate manifest. Its separate archive/skip methods write their own outcomes at :283–307. Their selected/previewed callers remain in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1545–1592`. All three files are byte-identical to the initial reviewed source.

The affected return-guard source also remains byte-identical: `BookOfEternityClient/Services/AfterlifeReturnGuardService.cs:31–43,142–168,226–228` reads guardian context and writes its guard. It does not write soul or perform an archive choice. The canonical E3 declaration remains consistent with the corrected mutation set; its new supplemental link points to the corrected pair and explicitly states the separation.

The new LC13 is a future check of exact E3 membership and exclusion of separate Archive/Skip ownership/replay. It is explicitly UNRUN, not a discovered executable test or proof of current implementation. This closes the specific erroneous phase/write attribution from initial LC-R1 without asserting that the full E3 receipt/input/crash contract exists.

## Scope, preservation and static evidence

The author JSON changes only correction metadata, Markdown identity, LF16, the E3 phase projection and addition of LC13. All other 21 recipes, nine other phase projections, seven question groups, 16 catalog structures, 73 constant-resolution records and 112 historical source-pin records remain unchanged. The initial independent expansion result for those catalogs/constants remains applicable to its pinned source; it was not rerun as runtime or silently extended to unrelated Pc changes.

All 112 historical and ten correction pin records independently match their stated Git blob, SHA-256 and byte length. The ten correction records represent five paths at two historical commits. Four runtime source paths remain identical at the reviewed candidate; the fifth is the canonical design document with the expected link/status amendment. Current candidate pins are recorded separately. This is pin/correction verification, not a claim that all historical code was rereviewed against the candidate's unrelated implementation.

The original author MD/JSON are byte-identical to the initial candidate and their `/tmp` originals. The newly copied initial independent-review MD/JSON are byte-identical to this reviewer's immutable `/tmp` reports:

| Artifact | SHA-256 |
|---|---|
| Original author MD | `088e4bac5c708eb6a487893416658945c972a5859034608f31baf61c1eb7fbd2` |
| Original author JSON | `693bfe316f320322ef598cdd2ae8493bfd00b49c7d98e7f9264b12a00fa5711d` |
| Initial independent MD | `3b60c3f36928aa77c5dc77e7c48c1af70f57739444b133967564bec4260b4afe` |
| Initial independent JSON | `1324f4ae3ad4dac3ddebe4f89c9a867bf894e066ee5b0276bd7788e64b345338` |
| Corrected author MD | `42aa4c29c028feb0212e3fbe62b91a692efe1b14a72fba92d291e10aa153861c` |
| Corrected author JSON | `0b754c394dd64371a1d36045139162ab74eab3ddb029e09b18c70156206d4f60` |

The compressed pair diff is lossless: gzip SHA-256 `c62f333eed9cfb0f2e2a57478f90be38a283c64a066068ac96b5aadd8058c555`, 6,656 bytes, mtime zero. Decompression yields the exact 20,206-byte raw diff with SHA-256 `4db4aa8ab17dad7c9b4dc572ca24bba2482757cf48383a95fc811bb54be19b2c`, matching the index and retained `/tmp` raw file. Independently regenerating the two n=3 unified diffs from exact original/corrected UTF-8 text and their recorded `/tmp` labels reproduces those raw bytes exactly. The Markdown component is 10,733 bytes, SHA-256 `f5dfe9da4168087e31a147fe1f7b9b79a5fbefe0d74136e4b27a8560ef405435`. Compression preserves the unified-diff context spaces; it does not replace or edit the evidence.

Complete nine-path candidate-versus-parent `--binary --full-index` diff: **442,716 bytes**, SHA-256 `de68153992621b1efce755872393eb6b755ee0081c026182f7322db700f368cf`. Exact argv, all nine candidate artifact pins and both diff representations are in the JSON companion. Static `git diff --check` returns 0.

Current canonical, top plan and tasks accurately identify the corrected pair, resolved source distinction, pending affected rereview and 13 UNRUN future rows. The retained older plan bullet still describes the original 12-row proposal/pending initial review; it is superseded by the explicit correction block and is not a new semantic scope or release claim. A final metadata carrier may record this completed rereview while preserving the original artifacts.

## Remaining blockers and limits

Q-L1's retained role schemas and acyclic seals, source transitive proof closure, full EP/E1 contributions, E2/E3 input/receipt/runtime witnesses, genuine DEP-I choices/IB/accepted bootstrap, private/pending/cleanup catalogs, and unchanged DEP-L Q-L2–Q-L7/DEP-C lifecycle questions all remain open. No phase is complete. A backup inventory remains different from a publication grant. Actual original presence/validation/rollback roles and player choices retain their existing limits.

DEP-C/G-C, DEP-I, DEP-L, full target catalogs, global H2 qualification, H3 source release and new joint QTE remain blocked. No automatic F2, life-evaluation turn suppression, new Shining eligibility, automatic D4 allocation or relaxation of guided/visual/Windows/D8 gates is introduced. The narrower E3 catalog does not resolve a Lost pin, issue a native load receipt or adopt copied old-G authority.

All 13 future checks and all actual T197 checks remain UNRUN. No runtime, build, tests, discovery, live-root inspection, browser, guided, visual or platform qualification occurred. No Pc source/runtime verification is claimed. Parent-reported pushed/readback status is distinguished from this reviewer's local pinned-object inspection; no remote network readback was performed here.

**FINALSTOP.** Only the new `/tmp/t197-depl-leaf-corrected-independent-review-20261016.md/.json` pair was written. No repository/runtime edits, prior report changes, commits/pushes, subagents, pending writes or background work. LC-R1 is resolved for this exact corrected candidate; the proposal is suitable for partial discussion and remains UNACCEPTED/BLOCKED.

Recorded UTC: `2026-10-16T10:34:27.129610+00:00`; observed checkout HEAD: `ef0a99c8be0d2c314d3e9333afe75feec66e7e40`.
