# FR063 completed-v2 authenticated output binding — final narrow design A

Issue: #1536 / T070 / FR-012 / FR-063. Source baseline: 85caae3c388aaeb615c89593e1ad3e7f3e48adc8.
Status: written design for independent DESIGN review; production implementation UNRUN.
Authority: coordinator's 2026-10-15 instruction records user approval of option A from
the proposal published at 62bd24834723b46c284a874d508df0d191433c19. Option B is not selected.

## Contract and closed durable shape

Keep the current joint marker at its existing StatePath and schemaVersion=2. Add
one required `outputBinding` object with EXACT fields:

```
version: 1
localWoundRef: non-empty accepted response-local reference
woundId: exact allocated acquisition CREATE wound identity
transitionId: exact acquisition CREATE transition identity
narrativeBase64: exact complete accepted narrative_response.json bytes
narrativeHash: uppercase 64-character SHA256 of those decoded bytes
```

Every field is required, with exact types, no duplicate/unknown property or alternate
representation. Decode strict UTF-8 with the established optional single-BOM rule;
the decoded JSON must be an object with the ordinary valid response/timestamp shape.
The narrative must already be a fixed point of AcceptedTurnOutputProjector.ProjectNarrative.
The full byte image includes timestamp, formatting and BOM. The existing marker's
whole-root joint fingerprint includes this object. A domain-separated immutable
basis fingerprint is also included in the private common authority/input seal.
The pure parsed value/fingerprint is never a publication or continuation capability.

Old joint-v2 artifacts missing this required binding fail closed. No compatibility
fallback, packet inference, recreated draft or WoundId-as-localRef alias. The ordinary
v1 schema, decision resubmission and exception/rollback behavior stay unchanged.

## Issuance and normalization before seal/Take

Use the real existing joint continuation, its corrected QTE adapter acquisition
command and already guarded narrative bytes. JointRepairContinuationAuthority already
seals both AcquisitionCommandRoot and `_before[output/narrative_response.json]` and
checks them physically. The new basis is derived from those detached sealed inputs
and the same complete prepared/final bundle's exact acquisition CREATE/receipt.
Do not reread mutable output to invent a later basis, reallocate IDs, re-evaluate a
treatment result or create a second mechanics plan/claim/Take.

Run the existing ordinary narrative validation/normalization BEFORE constructing the
joint continuation/common basis. Its permitted correction timestamp exception remains:
MortalJointRepairCheckpoint.ValidateCorrectedResponse may accept a fresh timestamp
before sealing. Capture only the final normalized, validated current image. The basis
factory independently requires the projector's fixed point; unnormalized input cannot
be sealed. The existing continuation/current-source guards bracket all boundary awaits.

The common authority's private issuer owns basis construction. It includes the immutable
basis fingerprint in ComputeFingerprint/HasValidSeal and validates current narrative
bytes through the already existing guard path. The taken publisher may serialize ONLY
that exact sealed basis after its existing full Take/plan/owner checks and verifies its
IDs against the actual intended receipt and CREATE. Existing full marker readback must
agree with the taken basis. Keep all existing P5/P6, source, cache, bound, reservation,
inventory, prepared member, compensation and settlement guards.

No new path, write/delete/rollback member, publication, general storage mechanism or
privileged writer is added. In particular narrative remains an existing guarded public
output, not a newly added common transaction write/rollback participant.

## Cold proof, applicability and output validation

Extend only the joint completed proof. First authenticate the actual original and G,
headers/input, complete receipt/occurrence partition, typed TREAT/finalization, exact
CREATE/TREAT suffix, canonical carriers/identity/effects/resources, complete footprint
and frozen progression source/phase. Then validate the closed output basis:

* Basis woundId/transitionId equal the fully validated materialized receipt and its
  exact CREATE history row; canonical wound/event/owner/severity agree.
* Prove localWoundRef independently by recomputing the EXISTING decision fingerprint
  and operationKey from that validated original-derived opportunity/source, selected
  canonical CREATE severity and candidate local ref. Share the existing pure hash
  formula with the ordinary decision evaluator; return only equality/classification,
  never a new decision/command/accepted authority. A public packet/hash is not an issuer.
* Require current narrative bytes to equal the complete sealed byte image. Parse and
  compose the ordinary WoundPlayerNotification with the authenticated local reference,
  canonical wound/acquisition narration and exact accepted full response. A matching
  acquisition substring alone is insufficient. Revalidate the full physical proof and
  current byte image after awaited reads; retain all existing phase/story brackets.

An absent consumed public decision is legal only for this verified joint-v2 family.
If a draft is present, preserve its exact accepted full decision-payload fingerprint
requirement; changed, malformed or foreign drafts refuse. InspectCurrent's MayResubmit
must distinguish this completed-family continuation from ordinary v1 resubmission.
No routing bypass precedes the real full proof, and a remembered family flag grants
nothing. The exact binder reads the retained basis, not a missing live draft.

All post-common joint narrative validation uses an explicitly READ-ONLY mode of the
ordinary validator. It must report a normalization-required error rather than write
new bytes. The ordinary v1/default normalization API remains unchanged. Validate exact
basis before any output normalizer could repair tampering; every joint validation and
story entry brackets reads with fresh full proof. Missing/stale/changed output retains
Committed and its original/Ready/backups, cannot enter destructive generic repair, and
causes no second mechanics/progression/story mutation. Existing terminal cleanup removes
the same cohort; stale markers after cleanup/new original cannot authorize continuation.

## Test-first proof and bounded execution

The authentic scope2 Late2 outcome is the initial consumed-draft/story regression;
keep its complete native evidence. Before production implementation, author focused
behavioral controls against real existing owner/caller APIs and demonstrate expected
missing-binding behavior, with no fake marker granting a positive continuation.

Required positive coverage: genuine absent consumed draft in both existing Late rows;
pre-seal narrative normalization and allowed correction timestamp; present exact draft;
unapplied/applied real cold continuation, nonzero consumed report, single story/turn,
inert repeat and supported new-root proof. Existing lawful severity I–II authority
remains unchanged; no II-only admission rule may be added.

Required negative coverage: changed full scene retaining its acquisition substring;
timestamp-only and formatting-only changes; missing/stale scene; escaped line breaks
that would otherwise trigger a post-common normalization write; changed/malformed/
foreign present draft; missing binding/old-v2, unknown version, extra/missing fields,
invalid encoding/base64/digest, foreign localRef or receipt/CREATE IDs; current original,
G, marker, footprint and phase/source drift; stale post-cleanup evidence. Include a
coherent local-ref/marker rehash attempt so pure candidate/hash equality cannot replace
receipt/source binding. Cold refusal cases must damage the quiescent durable namespace
BEFORE launching the fresh PID and exercise its first actual owner admission without a
prior successful output-proof preflight in that child. Retain raw outcomes before oracles,
exact after-tamper files, strict no-extra-mutation checks and real cleanup receipts.

Use small coherent category groups for parser/closed-basis, exact-scene, draft/reference,
and normalization/cold consumers; every new test gets an owner and explicit selection.
Keep per-case mutable fixtures and existing native child/runner bounds. Do not hide a
slow group by increasing its budget or silently dropping cases. New exact inventory
must be independently reviewed and freshly discovered; 32/58/18 and len14 were the old
candidate, not authority to omit new controls. Update the restored metadata adapter's
strict expected inventory/allowlist to the reviewed finite candidate, preserving its
zero-execution preparation gate and explicit actual release. No arbitrary <= counts.

Sequence: (1) finish scope2 fresh Late2/freeze/EVIDENCE; (2) independent DESIGN verdict
on this artifact; (3) tests-first and narrow same-writer implementation; (4) independent
corrected SOURCE/FIXTURE/SELECTION and adapter/inventory checks; (5) fresh BOTH, exact
PlanOnly/catalog and pinned first Late2 outcome/freeze/EVIDENCE; (6) only then the
remaining accepted finite stages and new focused negatives/cold controls, sequentially,
freezing any first failure before correction. Checkpoint5 is repeated only if a changed
prerequisite requires it, with fresh result clearly separate from its retained PASS.

Update the existing FR063/private treatment lifecycle normative documentation and a
worked GM correction explanation to say the same accepted complete scene is retained
after draft consumption; no new GM-authored field or diagnosis reveal gate. Update
associated source guards and relevant documentation selections. Preserve existing
plan/tasks history and checkboxes; do not infer full FR063/T070/#1536 from Committed,
static acceptance, preparation, or a partial runtime subset.
