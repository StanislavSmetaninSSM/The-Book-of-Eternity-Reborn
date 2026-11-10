# Independent review erratum: exact Late2 selection path

ACCEPTED artifact-only correction. My original review JSON contained an incorrect path in `actualRelease[0]`. Replace only that literal for gate construction:

- Incorrect, nonexistent: `tests/selections/1536-fr063-joint-late-completion.json`.
- Correct, tracked: `tests/selections/1536-fr063-joint-connected-late-completion.json`.

The correct file selects exactly `fr063-joint-late-completion`, integration project, two Facts / two cases / one descriptor:

- `BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_LiveSeverityRepairCompletesJointOnceWithZeroCycleProgression` — `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs:16`.
- `BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_LiveSeverityRepairConsumesMatchingNonzeroReportExactlyOnce` — `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs:20`.

Independently read the tracked selection and category selectors at reviewed source `7375c24a41430b9d578e817d07d847c1ef68cbe4`; selection bytes are identical to prior tested source `85caae3c388aaeb615c89593e1ad3e7f3e48adc8` and current metadata carrier `4ab8576acffeec123fe0a8ece61c3083a0be9f7b`. No selection expansion or code change. SOURCE/FIXTURE/SELECTION remain ACCEPTED, with actualRelease exactly the correct path above, after fresh BOTH/PlanOnly/catalog prerequisites. No build, discovery or runtime was executed by this erratum review.

The carrier was independently verified clean and remote-exact; its entire diff from reviewed source is the two byte-identical original review copies. All 44 changed-file pins and 12 additional dependency pins match. Original external reports remain unchanged; their hashes and the correct selection pin are in the companion JSON.

Byte-identical copies of this erratum MD and JSON may be added by another metadata-only carrier: ROOT must verify the incremental diff contains exactly those two added recovery paths, hashes match these external files, all prior source/config/contract and original report bytes remain unchanged, and remote carrier SHA is exact. Keep reviewedSource `7375c24a41430b9d578e817d07d847c1ef68cbe4` distinct from metadata/compiled/tested carrier SHA. No runtime acceptance follows from either carrier. The original erroneous literal is superseded only by this explicit erratum; original artifacts must not be edited.

Requested model/effort: gpt-6-astra / xhigh; actual backend telemetry unavailable. Only external erratum files were written. ROOT must observe actual FINALSTOP before setting reviewerStopped=true.
