# T324: exact T323 failure and next bounded question

Actual one original seeded cut0 failed at the unchanged600s child wait; body00:12:33.2382874. Producer73/resumer-1, EOF/join both true, both roots removed and independently absent, four PREP DLLs and current seed unchanged. T323-profile-cut0-outcome/raw and complete child streams remain immutable. Code d41f80f347; actual metadatafd5d599796. Original cold-five0/5/LinuxUNRUN.

Canonical offline reader reconstructs all3197records/1596completed intervals/5unfinished, bound to actual resumer24512 and exact source locations. Current live binding and four historical golden-report checks all pass; no runtime repeated. End boundaries include exception unwinding and never certify operation success. No diagnostic quota.

Actual append completed with3source reads in37.120776s (old T31549.419955s); explicit fresh story proof then completed in12.540799s. Final unfinished stack is first-current-owner → joint story reconciliation → final materialization lease → ValidateAppliedProgressionAsync → JointPhase.ReadCompletedProofAsync. Last begin598068.3332ms on collector clock; no completed duration exists for that call. Parent600s wait is a different clock. No cleanup entry or export exists, so actual inner cleanup cost remains unknown.

| Completed operation | Same count | T315 seconds | T323 seconds |
| --- | ---: | ---: | ---: |
| `JointCompletion.ValidatePostPublicationAsync` | 2 | 169.005510 | 168.501024 |
| `GameEngine.PublishCurrentMortalJointRepairAsync` | 1 | 314.355618 | 316.912996 |
| `JointBaseline.ValidateCurrentAsync` | 31 | 134.548738 | 138.292832 |
| `JointResources.ValidateCurrentAsync` | 13 | 145.952444 | 149.661807 |
| `JointStory.ValidateAcceptedTurnOutcomeWithRepairLoopAsync` | 1 | 102.020997 | 101.811239 |
| `JointStory.AppendStoryAsync` | 1 | 49.419955 | 37.120776 |
| `JointStory.VerifyProjectionBeforeStoryAsync` | 1 | 10.821978 | 10.822766 |

Observed inclusive values, not a controlled benchmark; nested rows are not additive. Early unchanged31baseline/13resource/2postpublication checks still consume most of the finite validation graph. The local correction is exercised and reduces append reads; it does not complete original recovery.

The newly exposed source costs localize the next question: five complete story-source reads include27.727s in BindContinuationAsync and15.707/15.891s in applied-before/after. Row calculation is0.028s acrossfive; actual append0.164s. Source inspection shows each source read has full applied-phase fences before/after and a nested continuation binder with its own full completed-publication reads before/after presentation. These checks have different code boundaries; none is declared removable merely by timing.

Bounded next design question: can continuation presentation use real freshly validated completion DATA inside one call bracketed by both retained physical phase checks, without nested duplicate G rereads? Ordinary continuation callers must keep their full contract; exact outputs/carriers/presentation semantics remain, callback/async/failure/lifetime behavior must be explicit, no cross-call cache or parsed-private-proof authority. A minimal production-used orchestration seam and real DATA component should prove the contract if justified. Architecture consultation and concrete written spec precede code; unresolved and unimplemented here. No direct filesystem-mutation equivalence or OS/scanner cause is claimed.

Continue after independent evidence review; no unchanged original retry or timeout increase. All old DATA/raw failures and current correction remain preserved.
