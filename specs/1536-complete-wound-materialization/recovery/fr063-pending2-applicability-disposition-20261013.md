**Applicability disposition: ACCEPTED for separating the two misassigned fixtures from the narrow ordinary-owner cohort. FR063 persistence qualification remains OPEN.** Reviewed exact `b3bdd4af7547f5dc28ed03c3580e76f2eef14767`; no source changes or project execution.

The approved requirements distinguish two responsibilities:

- **Mandatory persistence semantics:** [FR-063, line537](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/b3bdd4af7547f5dc28ed03c3580e76f2eef14767/specs/1536-complete-wound-materialization/spec.md#L537) requires complete requests in command/pending/history, exact-copy coalescing and divergent-copy conflict. FR-012 at line312 reinforces complete request persistence and replay safety. Neither requirement is N/A.
- **Conditional pending storage:** [treatment contract, lines1004–1018](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/b3bdd4af7547f5dc28ed03c3580e76f2eef14767/specs/1536-complete-wound-materialization/contracts/mortal-wound-treatment.md#L1004) explicitly prohibits a synthetic treatment-repair candidate. `submittedTreatmentRequests[]` is permitted only with a real, non-empty repair wave and must otherwise be absent.

The two existing fixtures violate that second precondition. [OrdinaryOwner.cs:24](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/b3bdd4af7547f5dc28ed03c3580e76f2eef14767/BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.OrdinaryOwner.cs#L24) assumes the held guaranteed-request producer already created a pending root. It did not emit a repair wave, so the observed null is lawful. Neither row reached duplicate parsing or ordinary-owner invocation. Their historical results remain **fixture prerequisite FAILs**, not production duplicate failures.

The strict implementation agrees with the contract:

- `MortalWoundTreatmentPersistence.cs:287–301` validates a non-empty bound wave whenever submitted requests exist.
- `WoundRepairPacketBuilder.PersistenceValidation.cs:50–65` requires at least one packet and matching receipt.
- `ComposePendingRoot`, at `MortalWoundTreatmentPersistence.cs:2701–2786`, copies submitted requests only when packets are non-empty and the command has been parsed/recomposed.
- The current ordinary guaranteed contour deliberately refuses competing resource/owner work and unconsumed wound occurrences: `ValidationService.ResourceMaterialization.cs:503–537`. Its mechanical intake also refuses a concurrent QTE decision draft: `GameEngine.MortalGuaranteedTreatment.cs:128–143`.

Consequently, inserting an empty pending root, fabricating a packet, borrowing another original’s packet, or weakening an admission guard would not repair these tests. No genuine drop-in wave under the unchanged original42 fixture has been established.

**Responsibility and remaining blocker**

Exact-copy and divergent-copy coverage belongs first to the **repair-persistence/catalog/claim-restoration boundary**, using a genuine applicable wave and the same complete request binding. Existing source contains relevant lower-level tests:

- `CommandAndRepairPending_ByteDifferentSemanticCopiesCoalesceToOneCompleteRequest`
- `PersistedCatalog_RejectsEveryCommandPendingDivergence`
- `RepairPending_DoesNotCopySubmittedTreatmentWithoutANonEmptyRepairWave`
- `PersistedPending_RejectsSubmittedTreatmentWithoutARepairWave`

These show the intended responsibility; they are not newly qualified runtime evidence. Their repair-opportunity helper constructs fixture event authority (`MortalWoundTreatmentResolverTests.Persistence.cs:2216–2267`), so they cannot silently replace the requested genuine-wave proof.

A corresponding future fixture must establish, before duplicate assertions:

1. One genuine original and compatible held treatment request.
2. A real actionable rejection producing a non-empty packet/receipt wave under that same binding.
3. Production pending composition and complete-request parsing.
4. Exact-copy restoration yielding one request/claim, followed separately by divergent-copy refusal without mutation.

The lawful combination and its owning production publication path remain unresolved. Preserve that as an FR063 blocker; no replacement SOURCE or runtime release is granted here.

**Finite accounting**

The absence-only ordinary-owner cohort can be recorded separately as **8 methods / 13 cases**: native1 + post-seal1 + capture7 + reflection3 + warm1. The two pending-wave obligations remain separately **OPEN/reassigned**, with their original FAILs preserved.

The dated plan explicitly promised both controls at lines298–301 and349. Record a reasoned applicability amendment; do not erase that commitment or label the original **15 cases GREEN**. Before the interrupted preparation’s outcome is reconciled, only native1 and post-seal1 have accepted positive evidence in this proposed13-case cohort. No result is inferred for the eleven selected cases.

The external disposition file was not written during the transport hold. Remote exact-SHA reads completed; subsequently authorized local read only located an existing source file. Preparation completion remains unknown from this review, and no security cause is inferred from the transport failure.

**FINAL STOPPED.**
