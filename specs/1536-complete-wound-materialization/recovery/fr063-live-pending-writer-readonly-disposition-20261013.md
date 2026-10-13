# FR063 live pending writer — read-only architectural disposition

2026-10-13. **CONFIRMED LIVE-PRODUCER GAP; full FR063 remains OPEN.** The genuine pending2 component can establish complete private representation, exact-copy coalescing, divergence refusal and owned recovery at its selected component boundary. It cannot establish that normal gameplay emits this private pending authority. A connected production repair-persistence change and evidence are still required for full command/pending/history acceptance. This is architecture consultation, not final SOURCE acceptance, production-authoring permission or a runtime finding.

ROOT reports current metadata `e8bbbf39`, production reader accepted at `6f1eb1bf`; no Git or remote operation was performed. Source facts below use actual read-time byte pins. Sole Sol's active test-only component work was not modified, qualified or audited. The 13 previously accepted cases remain accepted in their stated scopes; no original 15-case GREEN or full FR063 completion is inferred.

## Normative boundary

- `spec.md:312` (FR-012): command/pending authority and accepted history must retain the complete sealed request and detached route source; nested semantics are rederived, exact repeats do not reapply, and changed semantics conflict.
- `spec.md:537` (FR-063): complete request in command, pending and history; exact command/pending copies coalesce; divergent copies conflict; actual durable cold reconstruction, full reducer/publication, release/rollback/compensation requirements remain mandatory.
- `contracts/mortal-wound-treatment.md:1004–1018` narrows *when pending is present*: only a real nonempty wound-repair wave may add `submittedTreatmentRequests[]`, copied from an already strictly parsed/recomposed command. It is absent without a real wave or treatment request, and is never in a GM-facing packet. A treatment failure is not a reason to fabricate an acquisition repair candidate.

Thus a normal treatment-only success may correctly have no pending file. That conditional absence is not a waiver of the required real-wave representation and lifecycle. Full acceptance cannot be obtained by replacing the real-wave case with the old pending-null fixture.

## Actual production caller map

Search scope was production `BookOfEternityClient`, with repository test hits used only to distinguish component callers. No compiled/runtime discovery was used.

| Surface | Actual source/callers and consequence |
| --- | --- |
| Pending constructor | `MortalWoundTreatmentPersistence.cs:2701–2785`, `WoundRepairPacketBuilder.ComposePendingRoot(binding, packets, parsedCommand)`. Exactly one production definition, **no production invocation**. Existing textual invocations/reflection-name lookups are tests. It validates recomposition and bound packet/receipt wave, then copies all full `TreatmentCommands` requests into private `submittedTreatmentRequests` when the wave is nonempty. This is a pure constructor, not a writer. |
| Durable private path | `WoundAcceptedTurnSnapshotContract.cs:5–6`: `game_state/control/pending_wound_resolutions.json`. It belongs to the original/publication inventory; that inventory alone does not produce a repair wave. |
| Actual command producer | `IO/StateDistributor.cs:163–181` writes accepted command and output, then confirms persisted treatment resources. On known failure it restores file mutations before release (`:191–226`). There is no pending-wave constructor/write in this producer. |
| Live wound repair capture | `GameEngine.ValidationAndRepair.cs:699–724` and corresponding post-seal path `:866+` build actual packets and capture retry obligations. `CaptureWoundRepairRetryObligationsAsync:1905–1941` stores packet + full expected command root in memory, or uses genuine retained QTE rejected input. `GameEngine.PrivateImplementation.cs:135–139` is that in-memory record; it is not pending serialization. |
| Real repair dispatch | `WaitForContractRepairAsync`, `ValidationAndRepair.cs:3150–3244`, validates fail-closed conditions, restores baseline, then dispatches. `WriteValidationRepairRequestForSessionAsync:3931–4076` writes the public validation-repair request. Its packet projection is `ToJsonObject()` (`:4415–4416`); it never appends the private complete treatment collection. |
| Repair baseline restoration | `GameEngine.SessionAndSnapshots.cs:2091–2115` restores the actual pre-turn backup. Its sole optional checkpoint is **ResourcePendingResolutionState.PendingPath**, not the wound pending path. No private wound-request checkpoint is restored at this boundary. |
| Existing pending consumers | `MortalWoundTreatmentAcceptedStateAuthority.cs:880–922` reads command + pending + history, strictly parses, then restores claims through existing registry authority. `GameEngine.MortalGuaranteedTreatment.cs:41–50` likewise parses the complete set. They consume a supplied pending root; neither creates it. |
| Existing pending mutations | `MortalWoundTreatmentResourcePublicationTransaction.cs:121–145,1393–1448` removes exact submitted rows during quarantine and restores captured before-images on compensation (`:1280–1300`). `CanonicalStateNormalizer.MortalItems.cs:1571–1602` reads exact command/pending before-images for terminal settlement. `GameEngine.ValidationAndRepair.cs:1237–1264,1345–1395` parses/removes finalized duplicate authority and commits cleanup. These are cleanup/restoration writers for already-present bytes, not producers of a new real pending wave. |

Source searches for the literal pending path, `submittedTreatmentRequests`, the path constant, and `ComposePendingRoot` agree: there is no connected live creation of the proposed private treatment-copy root. No dynamic/reflection production invocation by that name was found. This is a bounded static caller fact, not an exhaustive claim about arbitrary external plugins.

## Why a one-line caller insertion is insufficient

The lower-level genuine component can legitimately combine independently owned inputs, but the normal full-turn owner does not currently admit that combination:

1. `MortalWoundTreatmentPersistedRequestCatalog.Parse`, `MortalWoundTreatmentPersistence.cs:141–153`, rejects diagnosis/alternative capture and mixed opportunity+treatment command roots. Cross-root exact set/binding checks remain at `:185–200`; no parser relaxation is justified.
2. `StateDistributor.ResolveAcceptedWoundCommand`, `IO/StateDistributor.cs:310–334`, rejects unowned accepted transitions and a treatment command combined with raw wound decisions/opportunity commands. It is not the place to add a synthetic repair packet.
3. `GameEngine.MortalGuaranteedTreatment.cs:27–29` admits one homogeneous guaranteed held treatment. `:130–149` excludes concurrent raw resource/effect/QTE draft work and altered mechanical sources.
4. Most decisively, `ValidationService.ResourceMaterialization.cs:503–537` handles treatment before QTE staging and rejects unconsumed occurrences at `:533–536`. It also preserves full-party/resource/effect/pending/owner exclusions. On successful homogeneous treatment it returns through its owner at `:562–574`; unsupported treatment returns at `:576–581`. Only afterward is `stageMortalQteDecision` invoked at `:584–587`.
5. The supported repair kinds are only `construct_wound`, `repair_wound`, `narrate_acquisition` (`WoundRepairPacketBuilder.cs:621–622`). A treatment-only command supplies none of the `parsed.Commands` that ordinary `CaptureWoundRepairRetryObligationsAsync:1920–1934` matches. QTE raw semantic refusal has a genuine separate retained source (`GameEngine.MortalQteWoundDecision.cs:99–106`), but the homogeneous treatment branch prevents this mixed scenario from reaching that producer in normal validation.

Therefore the current combined live contour is **intentionally unsupported**, not silently accepted with a proved missing field. There is no observed runtime loss asserted here. The static requirement gap is that the mandatory conditional persistence behavior lacks a reachable owning producer. The component's success, if later obtained, cannot change this conclusion.

## One minimal existing seam for the next engineering design

Use the **existing wound-repair capture / baseline-restore / dispatch boundary**, not a new endpoint or a change to the treatment publisher:

`CaptureWoundRepairRetryObligationsAsync` → `WaitForContractRepairAsync` → `RestorePreTurnBaselineForRepairSessionAsync` → `WriteValidationRepairRequestForSessionAsync`.

A future narrowly owned repair-persistence extension should capture the exact current command, full held request set, actual generation/original binding and genuinely issued nonempty packet wave **before** baseline restoration. It should construct private bytes through existing `ComposePendingRoot`, then preserve/install them with the exact owned command checkpoint across baseline restoration, under the participating lease and current generation. Dispatch only the existing sanitized packet projection after durable readback. Reuse the current strict catalog, registry claim recovery and transaction settlement/compensation; do not allocate another reservation or treat pending bytes as publication authority.

This identifies the right integration seam, but is **not yet an implementation-ready one-line patch**. The smallest unresolved prerequisite is a concrete lawful production entrance for a real acquisition repair wave while a complete held treatment request is dormant. The current ordinary mixed guards forbid that entrance. A dedicated, bounded repair-only capture owner could be designed at the same existing boundary, reusing the actual adapter's rejected source/packet and already-issued homogeneous treatment command, while leaving mixed publication rejected. Its exact lifetime and successful correction handoff must first be specified and source-reviewed; merely persisting an unreachable constructor or obtaining a packet by calling the adapter directly from a test is insufficient. No such live owner is asserted to exist today.

Keep these concrete constraints in that design:

- Capture fresh actual source/frame/G and only already parsed/recomposed homogeneous treatment commands; never infer a packet from an error string, manufacture a request or use an unrelated packet/root.
- Keep unsupported diagnosis/alternative, mixed command families, raw resource/effect/QTE publication, foreign command and changed source refusals. Do not remove the occurrence guard or route dormant treatment through the ordinary publication callback to make a fixture pass.
- Baseline restoration is destructive by design. A pending write made only before it will disappear. Carry a typed private checkpoint through the existing exact restore boundary and verify command/pending/hold as one lifecycle; on failure restore bytes and retain/release the matching hold according to the existing terminal versus retryable contract. A post-restore naked file write without compensation is not sufficient.
- No public packets, worker proposal targets or `RequiredResubmissionPaths` may acquire the private root/full request. The current client-owned path classifier in `GameEngine.SessionAndSnapshots.cs:2165–2213` does not list this wound pending path or wound command path. When this producer becomes reachable, explicitly keep those paths client-owned rather than relying on today's absence. This is a future integration condition, not evidence of current disclosure.
- `IsValidPersistedRepairWave` (`WoundRepairPacketBuilder.PersistenceValidation.cs:44–48`) validates exact public packet/receipt/root shape and agreement; it explicitly does **not** rederive hidden source authority. At production capture, genuine adapter/owner provenance remains necessary.
- Normal homogeneous treatment with no actual wound-repair wave must still emit no submitted collection. Component pending recovery does not grant a mixed ordinary publisher; any corrected-wave resumption must have a separately proven owner, or remain explicitly fail-closed without dropping the held request.

No new gameplay-policy question is required to state this gap: the full persistence obligation already exists. No production authoring is released by this report. Sol's current component-only task remains unchanged.

## Bounded acceptance needed beyond pending2

For a later connected-owner proposal, require an actual existing normal repair-loop entry—not a direct test call to the composer—to emit a genuine nonempty wave and private complete treatment copy. Observe command/pending equal request graphs and one claim; changed nested/outer coordinates or foreign wave refuse before dispatch/publication; private graphs never reach GM surfaces. Exercise a write/readback failure at that producer boundary with exact byte/hold compensation. Then prove terminal cleanup and exact fresh-root/process recovery through the actual owner needed by FR063. These are functional acceptance obligations, not invented method/case counts or permission to rerun previously qualified suites.

The immediate next engineering step after the scoped component is a concrete source design for that one repair boundary and its lawful producer/handoff, retaining the above exclusions. If no lawful entrance can be demonstrated without broadening the owning contour, report that as a specific owner-integration dependency and keep full FR063 OPEN; do not label the constructor/consumer pair end-to-end complete.

## Read-time byte pins

Repository: `/workspace/1553-storage-migration-cloud-20261008-recovery-61d3a7f`. No Git commands used; no active test files or metadata were changed. Prior component design read for scope only: `fr063-pending2-corresponding-contour-readonly-design.md`, SHA256 `19459d2b1296e355737c52f5a19524fa4f0b746e8ba2c14a7f1af0411728bfeb`.

| Repository-relative source | SHA256 |
| --- | --- |
| `specs/1536-complete-wound-materialization/spec.md` | `394c1c0996f21ac9e4bc98828e240e8bbffd47fc037799b0e6207ce6851c0b69` |
| `specs/1536-complete-wound-materialization/contracts/mortal-wound-treatment.md` | `96e03eea7c813eff8af47b6b5c57373bd6c426dda901ec3c4ee9dd38d80a1e8d` |
| `BookOfEternityClient/Services/MortalWoundTreatmentPersistence.cs` | `ca7b36dd5904e02d42de25b365d0fea597e9c7eee3651960d0e32184682070ef` |
| `BookOfEternityClient/Services/WoundRepairPacketBuilder.cs` | `4d4b0eb287207011edf9596ae6af2afd4ff7e8f7ee3d7a2889b95f6315c77fdd` |
| `BookOfEternityClient/Services/WoundRepairPacketBuilder.PersistenceValidation.cs` | `358fcaa3bf9dbdbaffae7cd8d1d4cf9d39e579efdb0a9d5cbb444df5960ad68c` |
| `BookOfEternityClient/Services/WoundAcceptedTurnSnapshotContract.cs` | `288e2429bbdb3c79c540f5dc8539cc2cc9b3bfdb4551a02b94c2b443422af3a1` |
| `BookOfEternityClient/Services/MortalWoundTreatmentAcceptedStateAuthority.cs` | `2c02b391f1cef80e115bc66f2d0c2ada92bc3d694403912bee47b9beb556f535` |
| `BookOfEternityClient/Services/MortalWoundTreatmentResourcePublicationTransaction.cs` | `5508ec06e2baecbcab25ae3e3fffc3f88a83d57ed3cfd18e70258c26990e2177` |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs` | `6b2de14c772aa509fb04d3b00192ddee491bdc3110f85aafd140ab0e63972c4d` |
| `BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs` | `560f146d8210f8347f53f53196054c42ba9406f92cd6ef073b30aa17fb2c7b83` |
| `BookOfEternityClient/IO/StateDistributor.cs` | `0ceecfb1f5d517673504e413f138bad5593065791f1f85a222238c7f19a3893d` |
| `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs` | `c967865961aa37163c6460617f6d268c3ff6ed824ae759c9900cd1f3095dde81` |
| `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` | `244d8c232fc29a44e5bf3d86c350282f72ceb849ccbe89699881686e26487ea6` |
| `BookOfEternityClient/Core/GameEngine/GameEngine.PrivateImplementation.cs` | `d3238dd98bd901ac5ac0c1a7e38a1bd4931ef83344c00d7064eb032fdae83b09` |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalGuaranteedTreatment.cs` | `fdacf8c145a10b9be5f09f1fde28415969d740e6526c84ab4097d4e9093b4642` |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalQteWoundDecision.cs` | `7a581df7c052ecee9c4f6943089941a9a5ac957810065890f116b356a743992b` |

**FINAL STOPPED.**
