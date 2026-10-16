Independent T192a SOURCE/FIXTURE/SELECTION/EVIDENCE review — 2026-10-16

**NO MATERIAL FINDINGS within the detached calculation milestone.** This does not approve full T192/F1, live ordinary resolution, or new joint QTE activation. No material source defect or evidence misstatement was established in the reviewed scope.

Reviewed `62c404507142ea692e81a4425faf584ecf8678d5` against `d43d6f3a9ee151e9e7742c5328e6281dd8af3528` on `1536-wound-b2-cloud-20261012`, using immutable Git blobs throughout. Requested and actually configured reviewer: `gpt-6-astra` / `xhigh`, confirmed from the parent's spawn arguments; backend execution telemetry is unknown. The reviewer did not implement the patch.

The exact 30-path binary diff is SHA256 `45e750c321ac58c1f8c2702ad8abef6d15cb1c719ff6ae3b154532fb8ae6c325` (1,769,033 bytes). Reproduction: the JSON report contains the complete `git diff --binary --full-index --no-ext-diff --no-renames` argv and path manifest. Whole shared spec/plan/tasks carrier bytes are hashed, but T197 substantive contents have no verdict here; standalone T197 design/evidence/reviews are excluded.

The current spec/ledger records user approval of staged F1-R2/D9–D11 work and states that this supersedes historical DESIGN-only authorization. That records the repository position; this reviewer has not independently verified the original conversation approval. Before final handoff the parent reported a discrepancy between a visible DESIGN-only delegation and the later approval recorded in its summary/plan, and is reconciling it. This review grants no authorization for further implementation or runtime activity. The bounded milestone is accurately described as client-only detached calculation. T192 remains unchecked; genuine issuer/M3/common/cold and accept/decline integration remain future work. The existing canonical implementation plan and R2 source contract were read, including T192 and action/time sections. Its historical prohibitions and the recorded supersession are treated as authorization provenance to reconcile, never as current runtime evidence.

Source assessment:

- `MortalJointActionBoundary.cs` creates only internal immutable value descriptions; deterministic action IDs are domain-separated, length-prefixed correlation hashes. No physical original/G/M/phase/accepted-result proof or publication capability is created, no filesystem/publication call exists, and no live production caller is wired.
- Action IDs bind generation, session, original request, snapshot token, turn, realm and incarnation. A continuation uses a distinct request/token while retaining action and turn n, ordinal 1. Event calculation recomputes the full expected request record, rejects foreign turns and duplicate event refs before suppression, and leaves strict scheduler replay rejection intact.
- Consumed pure `owner_turn_end` events are suppressed. Any mixed independent time/trigger/source/scene/condition/target/realm/causal payload is rejected for owner decomposition; separately represented genuine events pass through. Existing composer events actually contain CurrentRealm and often CurrentTime, so this helper cannot yet consume those live events unchanged. Target/roster provenance, structural operation identity and durable action+ack are explicit remaining integration work, not implied by these values.
- None requires exact unchanged byte images/absence and carries the post-prefix result without reapplying stale timeChange. Incremental retains the real delta and nullable canonical/effect coordinates. Numeric absolute jumps/rewinds never become guessed elapsed time. Calendar-only input retains the unchanged owner's unresolved fallback/null behavior. The test documenting different progression/effect reader precedence is correct against their actual code.
- `ResolveFrozenWorldTime` changes only visibility, private→internal; the body/formulas and existing callers are unchanged. The projection is correctly named ProgressionResolution and does not claim schedule/report/control composition, source admission, or due-cycle execution.
- Source/coordinates/event records contain immutable scalars, output lists have independent read-only backing, and time bytes are copied at construction and retrieval. Null absence and strict optional-BOM UTF8 behavior are preserved through existing readers. The discriminator/post-prefix baseline are supplied calculation inputs, not validation of arbitrary GM replies.

Fixture and selection assessment:

- New fixtures allocate their own records, bytes, JsonObjects and processed-ref sets; no shared mutable cache/owner or test-order dependency was introduced. The inherited progression neighbor uses a fresh GUID root and service per test.
- Tests invoke the real EffectLifecycleScheduler for turn lifetime, replay refusal, expiry, scene transition and calendar-only null-time behavior. Assertions use literal expected values and outcomes. Source-loss is checked as preserved event data; this is not a complete source-authority/effect-composition test. No fixture claims physical G/common/cold proof.
- `fr063-action-boundary-calculation` owns 19 methods/24 cases: 18 new calculation cases and six existing real scheduler/progression neighbors. Its responsibility, exclusions, Linux CI environment and five-minute budget are narrow and coherent; no broad suite is needed for this detached patch. Category ownership discovery is valid. This selection must be revisited when production consumers change or before the full intended diff integrates.
- The explicit client-only no-GM-update rationale is justified here: no GM-authored schema, pending surface or player behavior changed. Genuine source/accepted gameplay integration still requires prompts/examples/manifests/source guards in the same change.

Evidence readback (existing runs; this reviewer ran no tests):

| Evidence | Source SHA | Executed / planned | Result |
|---|---|---:|---|
| Missing-API preflight | `27c2f89392e1be241c803cb663f0d13eddbf1fee` | 0 | Build failure exit 1; not runtime RED |
| Runtime RED | `a3c96dfb990c28f6cae2e76ae419ce4ca3dd2de0` | 22/22 | 16 NotImplemented failures, six neighbors pass |
| First GREEN | `08140227fb6b4bc53f3912609dc8818b4627eb1c` | 22/22 | All pass |
| Time-drift RED | `70c2b9626646b4de32e32afaf83d2a743b3e5828` | 24/24 | 23 pass, expected InvalidDataException absent |
| Corrected GREEN | `8a4cb6ae0a4c1b3e0c358b9afc111d4e6159a995` | 24/24 | All pass, exit 0 |
| Catalog discovery | `8a4cb6ae0a4c1b3e0c358b9afc111d4e6159a995` | 0 | Valid=true; 11,668 methods, 759 categories, zero unmapped/stale |

The bounded command was `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -Category fr063-action-boundary-calculation`; preflight adds `-PlanOnly`, and inventory uses `-ValidateCatalog`. Environment entrypoint: `source /tmp/f1-r2-tools/env.sh`. The recorded environment is Linux with SDK 10.0.401, .NET 8.0.32 and PowerShell 7.6.6; this review did not reinstall or independently rerun that toolchain.

I parsed all four full runtime TRX files and their plans/summaries. Every runtime method/case count matches its plan; the final run has 19 methods and 24 passed cases with no missing/replaced method. Corrected GREEN source/test/catalog/selection bytes exactly match the reviewed SHA. Summaries show clean checkout revision fingerprints, no duplicate tests/timeouts and successful owned-tree/runtime cleanup. The catalog log/summary confirms fresh unit and integration builds but zero executed tests. RuntimeCleanup=null is not a native child/lease/debt cleanup receipt and is not claimed as such.

All six log archives decompress to the indexed raw sizes/SHA256 and match the raw blobs in the immediately preceding Git commit, with zero gzip mtime. Thus the gzip packaging preserves the original failures and terminal whitespace losslessly. The JSON report pins every reviewed changed file, evidence artifact and supporting source to blob/SHA256. `git diff --check` over this exact scope exited 0.

Nonblocking observations:

- O1: Focused tests do not directly exercise mixed consumed-closure rejection, tampered request records, BOM/invalid-UTF8 or the two null/present None drift directions. The inspected guards are correct; no bad outcome was established. Add narrow regression rows when touching these seams, especially before live integration.
- O2: `plan.md:21` and `tasks.md:11` retain outdated next-step text requesting runs already recorded as complete. Reconcile those lines when adding the stage review so recovery does not unnecessarily repeat passing tests.

Remaining blockers are the explicitly unimplemented full-T192/F1 owners and gates plus the parent’s pending authorization-provenance reconciliation; the calculations do not implement F1. No GM/client/visual/Windows/native F1 qualification, T197 acceptance, remote persistence verification or clean-clone recovery is issued by this review. Parent remains responsible for final publication/readback and integration decisions. No repository edits, commits or pushes were made; only the two requested `/tmp` reports were written.

FINALSTOP — no pending writes.
