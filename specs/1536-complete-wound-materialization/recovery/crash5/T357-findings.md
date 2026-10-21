# T357 — current-source full-owner bounded timeout

Compiled `44320b27e77ff01315eec8685f12344f5c12386c`, accepted release `91f61f6423d1317a6fe69cdf605dea1e72367d44`. One guarded changed-source Windows attempt, one planned descriptor/case, ZERO completed cases and no TRX test verdict. Command `00:02:00.2556262`, timeout exit124 under unchanged2minute total/category bounds. No automatic retry or limit increase. [Actual outcome](T357-profile-outcome.json), [all intervals/open scopes](T357-timings.json.gz), [every storage/footprint ancestry](T357-evidence-ancestry.json.gz), [all ancestry/named probe groups](T357-evidence-groups.json), [independent DATA/closing observations](T357-data-closing.json), [summary](T357-analysis-summary.json), [seven corrected offline tools](T357-analysis-tools-v2.zip).

Full124,436,075JSONLbytes retained, SHA256 `BF6D679CBB17D04D3966E419198D55556AAEA989901A5C3759E9F7EFA4D96C3C`;666,040continuous timing events,333,014closed intervals,12open scopes,0parse errors. Last timing timestamp100.3769093s is measured from the profile capture, not the120.2556262s command clock. Complete timing capture/owner completion are NOT established. Raw archive19indexedfiles SHA256 `2A07635F1B39596553B9C96353E0DCECBEA03795E260ED127B2350EA04ED42CB`; sevenindexedcorrectedtools SHA256 `24E22F51B23079076E95C4B00878E8411672CC2DC863DA7C60E441535CF9E3BC`.

| Completed scope | Calls | Inclusive seconds |
| --- | ---: | ---: |
|LateProfile.ConstructRealServices|1|0.0360649|
|LateProfile.RefreshReadOnlyState|1|0.5399991|
|LateProfile.AdoptRuntime|1|0.3018399|
|JointStory.InitialInspectionLease|1|10.3673608|
|JointStory.ValidateAcceptedTurnOutcomeWithRepairLoopAsync|1|45.9794241|
|JointCompletion.TryReadCompletedContinuationAsync|1|45.0102250|
|JointCompletion.ValidatePostPublicationAsync|1|42.6784255|
|JointCompletion.ValidateGameStateAsync|1|22.1813156|
|JointStory.AppendStoryAsync|1|7.8964770|
|JointCleanup.ReadStoryAsync|1|7.9024026|
|JointCleanup.VerifyBeforeImagesAsync|1|1.1516748|
|JointCleanup.BeforePublish.ValidateAppliedAsync|1|1.0322723|
|JointCleanup.BeforePublish.ValidateStoryAsync|1|3.9774160|

Nested inclusive scopes overlap; do not add them. Service construction is36.3548ms by its separately reported process-cost wall clock (36.0649ms operation scope), CPU46.875ms. No completed actual-owner/outer CPU/wall report exists. This fixture already avoids world/source42/Stage43 preparation; runtime service construction is not the observed large cost. The durable prefix contains11,390completed async reads and31,111sync reads. All8,145completed storage probes occupy6.5100534s;84footprint stages completed and another remains open. These are stage counts, not successful complete-proof/owner issuance claims.

The open chain reaches cleanup PublishLocalFiles→PreparedNamespace→ValidateApplied→current completed proof→footprint→ordinary async ValidateAndOpen. Unlike historicalT345's before-image read prefix, this run completed before-image validation and reached the preparation callback preceding physical publication. This difference is an observed reached boundary, not controlled speedup or historical cause. Cleanup/response install/inert repeat/actual outer disposal/independent closing remain UNCONFIRMED.

Read-only observation after stop: all220gamefiles/60directories, including empty directories, and exact generation bytes still match immutable T333input; expected cleanup cohort has not been removed. This proves the observed DATA, not game closing. The NEW owned root `D:\Code\The-Book-of-Eternity-Reborn-crash5-windows-20261019\TestResults\joint-late-owned\a0e2395332bb422faae662dee3e9a652` is retained and must not be manually deleted. Older T345 and rejected-cleanup ACL roots remain untouched (metadata-only existence check). Current4DLL+4copies and input are unchanged. Runner process-tree cleanup succeeded; it does not establish local game-owner closing.

Owner profile does not export per-operation serialization/write ledger; no T355 writer percentages are transferred. Largest completed residual groups include async Total9.3066170s, async ValidateAndOpen8.3133460s, sync ValidateAndOpen6.8431701s, sync Complete5.9099784s, sync Admission5.8947855s and sync PrepareScope5.0438518s; residuals include diagnostics/scheduling/unmeasured work and are not native or CPU attribution. All groups are retained losslessly, no diagnostic quotas. A next bounded source investigation should inspect per-read physical work/immutable derivation and meaningful independent fixtures while preserving fresh checks, rather than rerunning this graph unchanged.

Independent actual-result review pending. Historical original600second recovery and originalfive/Linux remain unqualified; no source42seed rebind, native/NativeLineage change or historical hang-cause claim.

Independent review found absent InstalledResponse incorrectly encoded false in derived DATA; corrected to null/unknown with every other DATA field exact. [Correction](T357-response-unknown-correction.json), original tool archive remains retained; no raw/source/test/root change or runtime replay.
