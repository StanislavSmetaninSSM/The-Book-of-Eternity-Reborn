# T179: retained common calculation before basis sealing

Preserved technical consultation, 2026-10-16. Inspected source HEAD: `144825517852fa6db623adb108a36bcfd8496ecc`. Source pins and scoped diff metadata: `t179-preseal-common-kernel-20261016.json`. This records the previously delivered narrow ruling; it is not an implementation or expanded investigation. Parent remains sole repository writer.

## Ruling

Use two shared calculation kernels around the existing pending-resolution orchestration—not a fingerprint-free copy of `ReduceAcceptedPlan`. There is a genuine additional dependency: `ResolvePendingBoundary` computes fingerprints from final `Input.InternalInputs` and authority seals (`BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs:4734`, `:5636`). That operation cannot precede B for arbitrary ordinary plans.

The narrow joint path can precede B because its accepted construction contour excludes unresolved resource work. Preserve ordinary pending/replay behavior separately.

### 1. Calculation inputs without AcceptedMechanicsInput

Introduce an internal detached computational view containing:

- Turn/realm.
- Definitions, state, history, owners, mutation-source catalog.
- Parsed resource commands.
- Effect plan and fallback effect identity root.
- Capacity intents, owner-capacity drafts, terminal owners.
- Registered outcomes, initial companion images/transitions.
- Execution offset and actual resource/effect identity factories.
- Wound bundle and anchors for final typed composition.

Ordinary code derives this view from its genuine input/context. Joint code derives it privately from `JointResourcePublicationAuthority` and its real baseline/bundle/anchors. No fake context, dummy fingerprint or CommonAuthority is necessary. The computational view itself grants nothing; a private joint wrapper binds its owner, original, lease, reservation and source fingerprints.

### 2. Discovery calculation

Extract the body from initial wound/effect agreement through discovery resource-result validation, currently approximately planner lines 4199–4335: definition/capacity preparation, periodic expansion, registered mutations, actual `BuildResources`, and replay completeness. Change helper dependencies from whole `input` to the coordinates they actually need—for example, capacity composition uses turn/realm; ordinary mutation composition uses realm.

Return a retained calculation object with final definitions, discovery resources, initial effect plan, prepared periodic/mutation inputs and source identity. Keep the ordinary resolved-pending second-pass behavior in its current orchestration.

### 3. Completion after boundary selection

Share effect transcript finalization, owner agreement and registered-outcome projection, currently lines 4459–4485. This returns detached resource/effect results and companion images/transitions, with no input fingerprint.

Joint admission must additionally prove no active pending requests, no submitted effect-resolution receipts, no same-frame terminal receipt obligation, and no newly discovered bounded candidates. Share an explicit no-pending boundary check with the corresponding ordinary branch; do not silently bypass terminal-history checks. A discovered bounded candidate refuses C. It must not create a pending request using provisional hashes.

### 4. Complete C before B, including typed artifact assembly

Factor the fingerprint-independent typed carrier assembly in `AssembleCompletedAcceptedReduction`, around planner line 4590: quartet images, completed effects, wound/item companions, identity/history images and conflict checks. Retain those exact artifacts in private `JointActiveCommonInputs`, together with resource binding, event/boundary transcript and source identity. Exclude M and all final input/prepared fingerprints.

This final typed artifact assembly is a necessary part of the extraction, not optional follow-up: pre-finalized effects or an incomplete companion map cannot stand for planned common after-images. Preserve exact item array/object restoration at the existing encoding boundary.

The resulting order is:

```text
real resource owner
  -> one retained discovery/completion calculation
  -> exact typed common artifacts C
  -> B
  -> CommonAuthority(S,B,C)
  -> genuine composed input + BindInput
  -> final AcceptedMechanicsPlan assembly from retained C
  -> same cache candidate/fence -> existing admission/seal
```

`CompletedOrdinaryMechanicsReduction` remains the post-binding object. Construct it—or an equivalent narrow final-assembly wrapper—only once the real input/fingerprint exists. Do not retrofit it with mutable or nullable fingerprints.

For existing v2, use the same joint calculation and retained artifacts, then construct its existing progression-only authority/input and assemble the candidate. Preserve v2 fingerprint grammar and output bytes; holding C internally need not add new serialized v2 fields. Ordinary non-joint `ReduceAcceptedPlan` uses the shared kernels around its unchanged pending orchestration.

### 5. Cache ownership

Retain the calculation once on the actual joint resource owner; validate it before reuse and reject source drift rather than recomputing under the same owner. `CreateJointCandidate` should select a named joint assembly path from that authority's retained result instead of invoking `_planner` for another reduction. Keep its existing `_inputFingerprint`, `_planningResult`, exact candidate reference and fence checks. Do not introduce a caller-supplied candidate factory/map.

Assembly may copy results; it must not rerun resource reduction or `CompleteAcceptedBoundaryTranscript`, which can allocate identities. The ordinary pending flow's existing justified second pass is not removed by this rule; the rule prohibits a second joint calculation merely to obtain a differently sealed candidate.

## Smallest meaningful real-entrypoint RED and retained coverage

Extend `RunJointCompletionControlAsync` with a narrow observer around C retention and candidate creation:

- Drive genuine Stage43/repair/common entry; observe one resource/effect calculation and the same retained C used by the real sealed candidate.
- Assert exact resource state/history, completed effect identities, companions and existing CREATE/TREAT/charge outcomes. Counter-only proof is insufficient.
- Between C and sealing, change a genuine captured source; require refusal/no common publication, then establish the unchanged positive baseline.
- Attempt reuse with another genuine resource owner/reservation; require refusal despite equivalent detached values.
- Add bounded-candidate/no-pending negative coverage without minting private source authority.

Retain `fr063-joint-late-completion` as first actual, then independent EVIDENCE release before the new seam cases. Preserve `fr063-joint-active-tail` and `fr063-joint-c1-common-mixed-guard`; select affected `fr063-joint-c1-qte-default-publication` cases for the shared ordinary kernel. Add a finite existing-test subset from `accepted-mechanics-planning` covering registered outcomes, periodic/reaction execution and pending replay; do not automatically run the broad owner. Header/generation/story categories remain impact-dependent. Final selection must follow the actual diff and independent review.

## Explicit limits and next execution ownership

- The fixed-joint no-pending/refuse-C rule is a construction contour for this bounded prerequisite and held staging. It must never substitute for the future complete F1 ordinary-resolution owner/DAG or be presented as support for all lawful ordinary outcomes. Future F1 must support its full accepted contract through genuine owners, including applicable pending/bounded semantics.
- Generic pending fingerprint orchestration remains unchanged. No provisional full-turn fingerprint, weakened pending authority, altered replay key or generic admission bypass is authorized by this extraction.
- Source S, complete C, B and the same final candidate must retain whole common source/owner guarantees. DTO readability, detached hashes, reflection-issued authority and arbitrary path maps are not proof.
- Active publication remains explicitly held until complete M3 preparation/take/writer/readback/dispatch exists. Existing v2 must not publish an active candidate. No P2–P4, new-QTE, T197 or full F1 activation is implied.
- Parent will copy these files byte-for-byte into the existing recovery record and reference its journal when starting the next bounded C stage. Parent owns actual test-first implementation, source/fixture/selection review, required preparation/Late2 ordering, EVIDENCE review and remote checkpointing. This consultation replaces none of those gates.
- No repository modification, build, discovery, test, application runtime, commit or push was performed for this consultation or preservation. Only `/tmp` report/metadata were written; no new permission or principled grant is requested.

FINALSTOP.
