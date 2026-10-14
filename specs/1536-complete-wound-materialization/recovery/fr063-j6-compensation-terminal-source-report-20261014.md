# FR063 J6 compensation guard and terminal owner source WIP — 2026-10-14

FINAL STOPPED for remote preservation. Owning base: `16f3c0bb9f06980e3e8849a07a8bcff0986fac7e` (metadata-only; code a92b3962). This candidate is six code paths plus active plan/tasks, including two new narrow partials. The prior raw report SHA256 `5049a34370034231cc3da2ff49a49b7b5d5a5add339bb9e35038956916302fa4`, historical 214-check static and ROOT receipts are separately preserved and unchanged. No whole SOURCE or runtime qualification is claimed.

## Verified source cause and authored correction

The actual existing `ProbeTakenJointRepairPublication` checks the open receipt, private state token, generation/revision, preparation seal, accepted owner, reservation, both cache take fences and `ProbeConfirmed(request, finalization)`. The latter validates the same request/reservation/resource authority, original finalization fingerprint and ConfirmedHeld state. These existing owner APIs provide the required proof; matching canonical bytes alone do not.

`CompleteTaken` non-Committed results now return PublicationUncertain directly, without compensation. Known publication/readback failure compensation probes the actual current receipt and confirmed agreement before byte inspection, after its awaited reads and in the existing trusted publisher's pre-commit callback. Refusal preserves current evidence. The shared publisher also had an earlier v1 compensation catch: joint failures now bypass that catch and do not populate its v1 stored cohort. They reach only the private joint transaction. Default/null v1 source reverses byte-exactly to the base. No claim, cache or resource API was weakened.

## Narrow terminal owning source

The private token is minted only from a freshly reconstructed `RejectedCapture` under the participating lease. It verifies exact actual homogeneous command, complete private wave, packet-authorized rejected/corrected draft, unchanged scene and original manifest/authority bytes. The existing confirmed probe and `PrepareActiveRelease(cancelled|validation_failed)` run under the state gate; unsupported reasons refuse. Token identity binds the existing state token/G revision, issuing filesystem/lease and the actual prepared agreement. No common receipt, reservation or publication authority is manufactured.

A nonmutating locked cache predicate excludes raw/validated common plans and spiritual publication/take. `TryPeekValidated` was unsuitable because it may invalidate a malformed slot and ignores raw candidates; the new exclusion performs no validation or mutation. Physical generation and accepted-state lease IO stay outside registry monitors. Old cache method bodies reverse byte-exactly after removing the new property.

The owning Engine terminal method acquires a lease and reconstructs the genuine owner under it. It validates the existing complete rollback backup inventory, captures current before-images, and combines that baseline with exact whole command/pending/draft removal. Only declared trusted local members enter the single existing publication. The publisher pre-commit callback rechecks ownership, original bytes and full inventory. Committed disposition, all-member and closed-inventory readback precede the private proof and `ReleasePreparedUnchecked` for that same agreement. TerminalReleased is returned only after actual release and one-use token consumption.

Known removal failure compensation requires the same still-current confirmed agreement, known before/after bytes, no undeclared rollback member, one complete compensation batch and exact before-image readback. Lost token/claim or unknown publication remains blocked; no released claim is recreated to conceal failure. No IO/await occurs under state monitors. This is owning source, not an invoked live repair path: the real caller is still unconnected, including post-lease runtime refresh and typed outer handling.

## Static evidence, inventory and remaining limits

New author readback `fr063-j6-compensation-terminal-wip-static-readback.json` records 194/194 text/byte checks and 116 protected pins. SHA256 `8717b30471177f1b4d7e0d0b10817e441f608c957dde111a8c750f1bb68401f4`. Checks include exact eight-path scope, old shared-publisher/cache inverses, uncertain-completion no-restore control flow, prewrite receipt/claim checks, real prepared-release ordering, immutable member capture, full readback, closed rollback inventory, monitor discipline, historical tails/acceptance checkboxes and identical active MD bodies. These are source observations; they do not prove compilation or runtime safety.

Catalog and selections are unchanged: static catalog730, existing checkpoint component1 method/5 cases/1 descriptor, budget5/default10. No tests, cases or runtime outcomes were added. Qualified fixtures/ce955, old marker/resource schemas and APIs, source vectors, historical results and global CI remain unchanged. The isolated GM tree was not edited.

Actual normal admission/checkpoint capture before baseline restore, restore/install/repair dispatch, bounded correction/retry, typed outer settlement, v2 exact/changed late-idle and same-root/new-root cold continuation remain OPEN. Terminal runtime refresh and lifecycle interception must be connected without a second outer rollback. Genuine R2-R7/C1 controls/selections, normative FR063/FR012/private lifecycle and J8 GM example/manifest/source guards remain required before whole SOURCE. Compilation, positive/claim-loss/removal/compensation/lease-lifetime checks, discovery, preparation, catalog execution and all new runtime checks are UNRUN/UNCHECKED. The frozen J1 causal RED remains the actual test-first basis; no new behavior PASS is inferred. Full FR063/T070/UI/parent remain OPEN.

No Git mutation, project execution, provider call, new agent or alternate environment was used. The prior transport diagnostic/probe/history remain immutable. Same retained sole writer; stop is a bounded preservation handoff, not a tool refusal.

## Exact authored paths and content pins

| Path | SHA256 |
|---|---|
| `BookOfEternityClient/Services/AcceptedMechanicsPlanCache.MortalJointRepair.cs` | `24a33d0a26aa294e18f4d95cf5941c72bb0731df53b6243857d7fb3b23a2b4f9` |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs` | `9d6a6b4c9fa688def2b1452050cf139cffac81a3e78ab2bd3717dd4c924e432d` |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalJointRepair.cs` | `0f5b4bdb666d43bc4c4a6d78bf9db7195ddb1cff83b70064e57057d0fc60bc32` |
| `BookOfEternityClient/Services/MortalJointRepairPublicationTransaction.cs` | `3f3f486c2c39550034609a96f0467a6f892ff4ff0acdb214a64f2253837fdbed` |
| `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.MortalJointRepairRelease.cs` | `d4362b38c7380071af8df0371d469b9fc03245605513408ea5f7bbe26d75791c` |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairTerminalRelease.cs` | `ce622ac731e2d9aa8b532928b105ebc19df982603cea2eda1d7e856782b27e99` |
| `specs/1536-complete-wound-materialization/plan.md` | `f6bbd20ecbadb4f86c6291429fbeeae466536d248e4d6f09da6afa036f7e9027` |
| `specs/1536-complete-wound-materialization/tasks.md` | `efeff1baa35d4c8ba7eee946e26295deacc487b750550a01aedb4e0f7332ae3c` |
