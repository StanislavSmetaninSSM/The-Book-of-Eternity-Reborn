# FR063 joint progression owner — proposed bounded amendment

**PROPOSED / UNACCEPTED / NOT IMPLEMENTED.** Based on checkpoint `017c3a2d`, accepted DESIGNv2 otherwise unchanged, and retained Astra High consultation e3588cd5. Architectural design only; independent DESIGN and explicit implementation release precede code. No new gameplay question: genuine ordinary originals always carry progression, and accepted completion must apply it exactly once.

## Selected integration

Keep ONE joint mechanics reduction/publication. Extend its existing private v2 marker at the SAME path with an immutable progression basis and `unapplied` phase, captured before common sealing/take. After successful physical canonical/narrative validation, one fixed three-member trusted batch writes the exact existing schedule/report mutation and changes that marker to `applied`. This is an owned bookkeeping phase, not another mechanics plan, effect schedule, resource reservation or hold settlement.

Putting progression into the initial mechanics batch would require advancing it before the existing post-common checks or moving those checks; it broadens the transaction change. A separate acknowledgement path creates another authority surface. Writing a flag after standalone Apply leaves the proven crash window. The selected same-marker, paired phase preserves existing validation order.

## Closed surface and source authority

Private schemaVersion 2 gains exactly one closed `progression` object: `version`, `phase` (`unapplied|applied`), `controlFingerprint`, `preparedAtUtc`, `sourceGuards`, `scheduleBefore`, `reportBefore`, `scheduleAfter`, `reportAfter`, `mutationFingerprint`. Each image has exactly existence/base64-bytes/digest, with null bytes/digest only for absence. Fixed source guards cover the actual original request/control, realm and world-time inputs. No caller paths, optional defaults or older-v2 fallback. Old v1 fields/domain/bytes remain unchanged.

The actual engine issuer uses its validated original, official signed present/absence coverage, current generation and participating lease. It freezes the actual submitted report only after existing progression validation. Schedule-before must agree with authenticated original coverage; report/source evidence must satisfy the current original's exact report/context rules. Missing coverage blocks admission; any necessary producer registration happens BEFORE minting future originals, never by rewriting an existing original.

The genuine existing Prepare calculation supplies the pair. Its immutable basis fingerprint and exact guards enter the private joint input/cache seal BEFORE take, with marker construction in the common batch. This prevents after-seal attachment. Root tuple/original hashes and the complete existing mechanical footprint/history/resource finalization remain mandatory; no field weakens them.

A parsed acknowledgement/digest is evidence, not authority. A proposed owner-local reader must independently validate the full physical joint proof, signed source/control and strict report semantics, then recompute the exact frozen pair from that basis. Only that owner may issue a fresh lease/root/G-bound phase capability. No old transaction receipt, live flag, diagnostic witness, story row, zero-cycle inference or report absence substitutes.

## Existing calculation and proposed APIs

Extract the narrow existing Mortal calculation from `PrepareAcceptedTurnOutcomeMutationAsync` into an internal pure calculation with frozen typed inputs and captured UTC. Old callers retain identical calculations, encoding, fingerprints and timestamp-read placement. Cold validation uses the captured time and frozen inputs; it never prepares a new post-consumption mutation with today's clock. World-time/realm/source guards must be revalidated; stored after-images alone cannot issue a mutation.

Proposed, unimplemented methods on the existing progression owner: `PrepareJointProgressionBasisAsync` (actual engine source proof), `ReadCurrentJointProgressionPhaseAsync` (full original/G/physical proof), and `CommitJointProgressionPhaseAsync` (fresh phase capability). These are private fixed-contour APIs, not a generic runtime-context/storage facility. Existing service capability and one-use mutation checks remain; an old lease-bound object is never transferred into a fresh process.

## Publication, recovery and acceptance

Under one current canonical lease, classify only:

- `unapplied`: exact marker basis and BOTH current before-images; revalidate all sources and full mechanical union, then publish schedule/report/applied-marker together.
- `applied`: exact independently validated frozen pair and BOTH current after-images; skip Prepare/Apply and all mechanics.
- Anything missing, foreign, mixed/intermediate or ownership-uncertain: hold original/ready/backups/output and committed mechanics without generic rollback.

Reuse the existing trusted local publisher and existing exact paired-write encoding/guard rules. An unchanged report stays guard-only. Do not call old TryCommit followed by a marker write, or copy its separate per-file restore tail. Known compensation may restore ONLY this exact three-member cohort to its unapplied before-images after current ownership/known-byte proof; it never restores mechanics or rearms/releases the finalized treatment claim. Uncertain journal/publication/readback preserves evidence and blocks until existing recovery establishes the namespace.

After complete three-member readback, fresh applied-phase/full-union/source validation and ordinary narrative checks, the actual owner may return Accepted into existing story completion. Post-phase/pre-story interruption resumes applied without another mutation. Preserve original/request/backups until successful story and terminal cleanup; stale markers alone grant no continuation afterward. Session replacement with a different generation refuses. Fresh PID, or a supported complete durable-namespace restoration into a new root, must authenticate the real original/current generation before minting a local phase capability; raw copy, Load's new generation or parsed hashes cannot rebind authority.

## Required proof and change scope

Finite later causal oracles: genuine non-null zero-cycle original; nonzero matching report consumed once; unapplied and applied fresh-process continuation; pause after the atomic phase before story then one story completion with byte-inert phase replay; foreign/tampered original/control/basis and mixed schedule/report bytes held without mechanical loss; known three-member failure compensation versus unknown evidence retention. New-root success requires actual reader authentication, with wrong-generation refusal. No counts, budgets, PASS claims or runtime release are invented.

Target existing progression service (one private partial may contain the new owner), joint common input/composer/cache guard, preparation/publisher/marker proof, engine canonical/completion/lifecycle, focused controls and normative FR063/FR012/private-lifecycle documentation. Old six-argument/singleton/public-mixed paths remain exact. GM fields/costs/mechanics stay unchanged; eventual J8 synchronization describes client-owned continuation. Main risk: faithfully extracting asynchronous source reads/sanitization, report semantics and captured-clock calculation into cold-verifiable inputs. Signed coverage and this extraction require implementation proof; no current API already supplies durable idempotence.

## Read-time source pins

- `BookOfEternityClient/Services/ProgressionScheduleService.cs` — `cae67d35831c5cd67df85fc049657fb51ed400ba1749b984f0e4c6331afbeaf9`
- `BookOfEternityClient/Services/MortalWoundTreatmentResourcePublicationTransaction.cs` — `5508ec06e2baecbcab25ae3e3fffc3f88a83d57ed3cfd18e70258c26990e2177`
- `BookOfEternityClient/Services/CoordinatedStateWriteHelper.cs` — `5b79e2b7d42839ff6dadc0a67040e40ac4b2bf0a18fd589d2b66ffd22cff97b4`
- `BookOfEternityClient/Services/MortalJointRepairPublicationPreparation.cs` — `4b63871bb085508cfb27e68ab1cc46097c84d03f43926cec8e11b6ae676f28b7`
- `BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointRepair.cs` — `f88f79f92a42a78735b0addf0a1a036f1562d5c10feed5b1fd1ff8b22d772133`
- `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs` — `e5a5bd4432522a8699882dea1c6b91868646f4faa2770eeb3967a9a0e902d085`
- `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCompletion.cs` — `f7de271ee654d2facfd5e80e7cabc33bba94beb14de2183dc6ac18de3524c7f1`
- `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` — `85ad957b7fa801930a7d4f13d6abd24e96b2e877d00544c5e74aad4440be6b1e`

Consultation SHA256: `e3588cd511fe62d8873d30dbf878d410a13ded20d76aabbcf534df6ff0b0b6fa`.

FINAL STOPPED. Proposal only; no repository or project activity.
