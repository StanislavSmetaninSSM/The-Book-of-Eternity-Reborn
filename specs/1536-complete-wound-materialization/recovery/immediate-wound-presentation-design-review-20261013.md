# Immediate wound presentation — proposed-design review

Reviewed immutable checkpoint `a966bd277d58bbdd71972a2f213f31fac28990f2`, especially Appendix A of `specs/1536-complete-wound-materialization/recovery/immediate-wound-recommendations-proposed-source-plan-20261013.md`. Repository: `/workspace/1553-storage-migration-cloud-20261008-recovery-61d3a7f`. All source locations below refer to that checkpoint.

**Disposition: the proposed direction is viable, but Appendix A needs one Important lifecycle correction before it is a complete implementation contract.** No other confirmed Important/Critical design defect found in this bounded review. This is proposed-design advice only: no implementation, UI, source gate, or runtime qualification is granted. FR063 and its mutable writer were not reviewed or changed. No repository edits, Git mutations, builds, discovery, tests, providers, or agents were used.

## Important: presentation must exist before a hidden route is revealed

Appendix A item 5 (line 138) makes presentation mandatory for currently known own-Mortal routes but optional for hidden additional routes. It then requires a later lawful reveal to yield a complete known state without adding reveal mechanics. Those rules conflict with the existing transition contract.

`BookOfEternityClient/Services/MortalWoundTreatmentContract.cs:217–235` requires every public/known-to-player route to be in `KnownRouteIds`. Its discovery check at `:258–316` computes reachability from known facts and rejects every hidden route outside that reachable graph. Thus a valid hidden own-Mortal route is already a candidate for lawful later disclosure.

`BookOfEternityClient/Services/WoundTransitionReducer.cs:1440–1480` permits diagnosis to append exactly the declared route IDs to `KnownRouteIds` and adjust only the declared complication visibility. It normalizes those permitted changes, then requires the remaining wound to be canonically equal to the before-state. The route itself cannot receive new presentation during this transition.

Concrete failure: a valid initial wound contains a complete known baseline and a hidden reachable route with no presentation, as Appendix A currently permits. Existing successful diagnosis reveals that hidden route. The resulting known route fails the proposed completeness rule; attaching text during diagnosis instead fails canonical transition immutability. This would break an existing lawful transition or encourage an inappropriate diagnosis exception.

**Minimum amendment:** require complete, sealed presentation on **every route of an own-Mortal wound at authoring/full-contract admission**, including hidden routes. Keep display restricted to current known membership, own ownership, and current realm. Under the current discovery contract all valid own-Mortal routes are either known or reachable, so an additional reachability exemption would add complexity without a sound smaller accepted set. Keep presentation structurally optional for genuinely out-of-scope NPC/non-Mortal wounds; any present value remains strictly validated. Detached route parsing still cannot decide owner scope and must defer mandatory presence to full-wound validation. No diagnosis-policy change, new reveal action, diagnosis fee, or reveal-time text mutation is needed.

## Seal clarification

The proposed separation between full-source identity and the mechanical route hash is supported by the current code:

* `WoundIdentityState.cs:190–195` hashes full canonical wound serialization. `MortalWoundTreatmentMemberFingerprint` hashes the whole canonical route member. Presentation must participate through the ordinary writers and detached copies.
* `MortalWoundTreatmentRouteFingerprint.cs:32–46` explicitly hashes mechanical route fields and already excludes `DisplayName`. Keep that algorithm/domain unchanged; do not add presentation to it.
* `MortalWoundTreatmentPlanner.Requests.cs:92–121` checks both the detached full wound fingerprint and the mechanical route fingerprint. Text equality is protected by the former, without granting capability to text or public hashes.

Clarify line 161's phrase “mechanical route/result hashes without presentation.” Preserve the existing algorithms, **not a promise that every result hash remains equal after text changes**. `MortalWoundTreatmentPlanner.Requests.cs:136–157` binds the full `routeSourceWoundFingerprint` into the request. `MortalWoundTreatmentResolver.Semantics.cs:221–238,544–596` binds that request fingerprint into resolution authority and then into the result fingerprint. A newly valid request over changed full source can therefore change result hashes indirectly. Do not remove that dependency to obtain presentation-independent result hashes. The requested negative oracle should assert unchanged mechanical route hash and unchanged costs/operations, changed full source, and rejection of stale source/request authority.

## Required source connections and privacy boundaries

The proposed single closed `{instructions, requirementLabels}` object is a suitable bounded representation. Existing text validation (`WoundMaterializationContract.cs:2578–2618`) supplies trimmed, nonempty, bounded text. Common requirement indices and actual course milestone ordinals have distinct roles; the existing course validator requires ordered contiguous ordinals. Complete ordered coordinate coverage preserves repeated requirements across different stages. Empty requirements legitimately use an empty label list and still require instructions.

The implementation must carry the field through the generic route record/closed parser/writer, all three typed and GM route forms, shared route header writers, detached clones and transition bounds. Existing explicit constructors do not carry a newly added property automatically. The proposal already identifies these paths. Full canonical and member fingerprints should acquire the field through serialization; no independent presentation seal, UI index rewrite, or fallback label inference is justified.

The renderer must state requirement AND, alternative-route OR, and course-stage sequencing explicitly. Labels name means/conditions; typed requirements supply role, quantity, tier, timing and consumption. Instructions must not convert “stabilize” into “heal.” Zero inventory, unavailable provider or unproved availability must not suppress known guidance. Availability is separate and may remain unknown. Presentation supplies neither an action nor eligibility/cost authority.

Do not assume the accepted-notification path already has the proposed privacy filter. `UI/WoundPlayerNotification.cs:96–213` validates all bundle transitions and requires notification/transition cardinality equality; it does not provide the own/current-realm/known recommendation filter. Preserve that mechanical validation and add the scoped recommendation projection separately, before copying any instructions/labels into a public DTO. Valid NPC or Spiritual transitions must neither leak their labels nor fail merely because they are outside this first UI slice. Unknown additional routes remain absent even though their presentation is now sealed in canonical state; a formerly hidden route becomes displayable only through actual known membership.

Persistent console/browser reads need one coherent own/current-realm view of validated root/index/history state, using the existing publication-quiescent read pattern. Corruption must not become an empty-wounds result. Accepted notifications should use their already validated bundle, not reopen files inside publication. Escape once for the actual renderer and expose no raw requirement refs, private provider identities/reasons, or internal paths. The plan correctly acknowledges that a structural parser cannot prove the truth or privacy of arbitrary natural-language prose; public authoring constraints and focused fixtures are necessary, rather than a claimed NLP privacy guarantee.

## Finite controls for the later source patch

Use a bounded inventory owned by the changed contracts; exact method/case counts remain to be authored and reviewed. These are distinct required behaviors, not a request for broad suite reruns:

1. Procedure, course and guaranteed-route round trips through GM draft, typed form, canonical form and immutable detached clone; include repeated references at separate course coordinates, empty requirements, and actual typed quantities/roles.
2. Closed-shape/coordinate negatives: null, missing/unknown/duplicate fields, text limits, wrong scope, fractional/out-of-range index, ordinal-versus-array-index confusion, missing/extra/duplicate/reordered coverage and attempted mechanical overrides. Assert the intended diagnostic boundary.
3. Own-Mortal completeness: known and hidden route missing presentation both refuse. A complete hidden reachable route remains absent from public output; the **existing diagnosis transition** reveals it without changing any route bytes, and it then displays its already sealed presentation. Out-of-scope missing-presentation behavior remains explicit.
4. Full-source text tamper and stale authority rejection with unchanged mechanical route hash/costs; alternative-route member immutability; detached-list mutation isolation. Do not require unchanged request/result fingerprints across changed full source.
5. Public projection: known guidance survives zero inventory/unavailable or unknown provider; requirement AND/route OR/course grouping remains clear; hidden routes, NPC/private/provider data and other-realm data do not enter console/browser DTOs. Include valid out-of-scope accepted transitions without breaking bundle validation.
6. Actual accepted CREATE notification plus persistent console and browser scene consumption, with readable nonempty means and renderer escaping. Read-only operations preserve canonical roots, identity/history, resources, attempts, turn/story and randomness; no diagnosis, reservation or treatment is started merely by opening guidance.

The policy of immediate how/means/conditions disclosure is already approved. The amendment above makes its proposed representation compatible with existing lifecycle invariants; it does not require policy reconfirmation. Correct the durable proposed appendix, then review the future connected implementation and its finite selection separately. Full Spiritual presentation and broader task completion remain outside this first own-Mortal slice.

FINAL STOPPED.
