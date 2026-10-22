# T417 source fanout and selected boundary

Every521054 T416 path aggregate was read and its completed3447944 GetAttributes reconciled, source analysis SHA391327CDD4B1E0AB5069085E65094F1268F39B80B8815308EA57FBE1EE9E6952. Full297 named ancestor groups,275 admission caller chains and762 ordinary-reader caller chains are retained inT417-context-analysis.zip. Group totals overlap and are not additive.

| Source boundary | Completed invocations | Descendant attribute calls | API seconds |
|---|---:|---:|---:|
| MainAdmission.Validate.Guard |75420|1629915|86.275142|
| JointResources.ValidateCurrentAsync |13|1225536|58.014161|
| JointBaseline.ValidateCurrentAsync |31|1108467|52.368033|
| JointMechanical.RequireCurrentOriginalEvidence |35|465710|21.606832|
| JointMechanical.ReadAndCheckSnapshot |35|98560|4.399966|
| JointMechanical.CompareBeforeImages |35|272825|13.363019|

Source chain: WoundAcceptedTurnPlanner.MortalJointRepairResources.cs ValidateResource invokes RecomputeJointQteContributionAsync; the coordinator always calls baseline->prepare->baseline. The13 resource validations cause26 of31 baseline calls. Each baseline rechecks continuation, mechanical admission and item registry. Mechanical admission replays FrozenOriginalSource.Recheck, rebuilds a QTE original, then rereads current before-images. Each logical read again performs ordinary/physical namespace and lease admission checks; this is the measured multiplier rather than one slow individual GetAttributes.

A read-only gpt-6-astra/high architectural consultation recommended one real borrowed read reservation around one whole QTE recomputation, with phase-local immutable exact reader-family observations and an uncached touched-source/generation exit proof. Preserve both coordinator checks and every original validation algorithm; initial per-read main/worker authority remains fresh. Non-passive reachable hooks use the existing reader path; the original resumer's two known passive activity callbacks need explicit passive contracts so actual measurement remains eligible without removing diagnostic observations. AsyncLocal alone is not flow identity; targeted design review must settle inherited/overlapping/reentrant behavior before implementation.

Rejected shortcut: mechanical capture uses optional selection without signed absence, while QTE ReadSnapshot requires signed absence. Replacing the latter by the mechanical snapshot would weaken refusal. Pure captured-QTE calculation is also deferred: current reconstruction and correction run real adapter/export logic whose complete data closure has not been proved by the existing before-image map.

No product edits/build/tests or new game replay yet. Concrete design is in canonical revision45 spec/plan. Cheap causal target is48 requests over4 family observations ->8 physical requests including exit proof; actual GetAttributes reduction and runtime impact remain unmeasured. The existing600FAIL remains FAIL, full timeout acceptance OPEN. The consultant is not the independent reviewer. No raw quota or deletion.
