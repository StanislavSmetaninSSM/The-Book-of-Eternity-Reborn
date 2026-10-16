# T179-Pd-AUDIT-SBM — affected SBM-D01 verification

**Verdict: ACCEPTED_AFFECTED_DOCUMENTARY_CORRECTION.** SBM-D01 is resolved at `80b6212e7057bb5b49ec1eac92ad2d2d669c3437`, relative to reviewed `96bd6acbfa0a403c3c6bf53c9cdcadddedc69899`. No remaining material finding was identified in this affected change. The bounded documentary audit may proceed to final persistence; this decision releases no implementation or runtime work.

Independent reviewer: `/root/t179pc_independent_review`, separate from the author; root remains sole repository writer. Retained configured model/effort: **gpt-6-astra / xhigh**, from the coordinator-confirmed actual configuration receipt. Backend telemetry is unavailable.

## Correction verified

Implementation-plan line 414 now requires capture/freeze S while Capturing, irrevocable CloseCapture before preparation, and the future genuine prepare/seal split binding retained S/C/raw B and the source-guard union before seals. It retains the prohibitions on after-seal attachment and v2 promotion.

This agrees with unchanged SBM02 (line 395), V05 (line 427), and the actual owner: `GameEngine.MortalJointPublicationAttempt.cs:83–95` closes capture irreversibly and forbids retaining a stage while capturing; its actual capture method closes at line 177. `GameEngine.MortalJointRepairCanonicalPublication.cs:43–53` awaits capture closure before creating/retaining preparation and reserving the existing publication. No production stages were moved, and no future issuer/schema was accepted.

All other substantive audit lines 385–438 are byte-identical. The other document changes accurately record preservation of the initial blocked review and the correction awaiting this verification. The current authority notices remain intact.

## Evidence checked

- Exact eight-file documentary delta: three status/design documents plus five preserved review/recovery documents. Product, fixtures, tests/catalog/selection, scripts/workflows, release and governance paths are unchanged.
- Fourteen exact Git/blob/file pins, ten exact line projections, eight individual diff hashes and the complete diff checked. Complete full-index/binary diff: 326,168 bytes, SHA256 `f1669f157a425a9cb3eb9738eabcc1042f1c0e90a2a50bc2e4c574e307d28b38`.
- Initial report MD `42877fcae6a823a2de36971fa271652203d96b05b7befcdd39624c852a4783a6` and JSON `0eae1cd4cea602bfbc02a2e4487e0cbeafb099d169a6b43f64eead486f0276e2` match both committed bytes and unchanged original files. Their historical BLOCKED verdict is preserved.
- Corrected recovery: **227/227 exact files**, covering **all 224 changed paths**, no deletions. Every recovered byte/mode/length/hash matches exact candidate Git blobs and writer bytes. Receipt SHA256: `ea1644f4a5abe49bf09fc3bde5507c0cdd0b52b656a3a5ae8d1cdd23f87bb5c1`.
- The retained independently verified full7acd base has its receipt hash, HEAD, clean status, and absence of alternates/sparse checkout rechecked. This remains full base plus complete delta, not a new full checkout, full-byte rescan, build, runtime, or independent network retrieval.
- All twelve future checks remain FUTURE_UNRUN; the main T178–T191 tasks remain uniquely unchecked. Current candidate HEAD is clean.

The paired JSON includes exact pins/projections/diffs, local read-only argv/results, recovery verification and checker text. This affected verification does not repeat the unchanged historical audit.

## Unchanged limits

Windows user permission is **UNANSWERED**. Failed hold delivery and technical review are not permission. Hosted JOBsuccess remains separate from unavailable raw Windows worker evidence; **WIN-E01 remains BLOCKED**. Prior 13 PASS and first two Linux FAIL retain their original f9bc provenance and were neither rerun nor requalified.

No schema/issuer/implementation, full S/B/M3/F1/T197/new-QTE, D4, guided US5, actual DOM/visual, Windows or D8 gate is closed. No runtime/build/discovery/tests/application/network/CI/transfer/settings/catalog/release action, additional agent or repository edit occurred. Only this new immutable report pair was written.

**FINALSTOP.**
