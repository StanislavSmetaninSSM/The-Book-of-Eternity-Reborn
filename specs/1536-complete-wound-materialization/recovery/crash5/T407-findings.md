# T407: full original Windows cut0, exact measured boundaries and remaining gaps

Compiled source4617800fa084d5a5490d2fee8004c1a76ed76ba7; original Fact JointInterfaceFixtureBackedWindows_Cut0_Prepared. Actual1PASS, test606.4904931s / command629.4431323s; unchanged child600/shared820/category15minute bounds, all original assertions and owned closing passed. Current helper uses D:/Code; historical actual600FAIL T365 used C:/Temp. Source/input/observer volume differ; this is not a controlled retrospective timing or hardware comparison.

Full raw:40 indexed files, resumer12693832283bytes,34724298 contiguous timing events /17362149 closed /0 open. Both independent passes agree all501named counts, inclusive and structural-leaf sums; all634074 invocation paths retained. OriginalZIP857617577bytes SHA5BE1E5B63FD87BF6A59B03F9AEF110BC7DB9F4B17283D25EF50AFD1F86B11D7F is transported losslessly in9 parts with offsets/full and part hashes. Complete expanded analysis is in T407-original-cut0-analysis.zip; no diagnostic quota or discarded records.

| Measured boundary | Calls | Own reported duration |
|---|---:|---:|
| File.GetAttributes | 6182797 | 69.2687541s |
| DirectoryInfo.Refresh plus original cached reads | 1262724 | 14.1322747s |
| FileInfo.Refresh plus original cached reads | 304901 | 3.1783226s |
| FileStream open | 71342 | 9.1137420s |
| Canonical JSON writer block | 177426 | 14.2710213s |
| SHA256.HashData fingerprint call | 643198 | 3.5784116s |

42.7911984s /3608871 GetAttributes calls are under MainAdmission.Validate.Guard. The original full scenario completed56279 synchronous ordinary reads and22037 async ordinary totals. Counts demonstrate repeated metadata admission; they are managed call counts, not inferred native syscall counts. The largest GetAttributes occurrence is92.5631ms: accumulated repetition, not a single600s GetAttributes stall in this run.

Last recorded observer counters:95.3665985s building/serializing +121.0195963s writing =216.3861948s. Gate1.3117884s is separate and can overlap; never add it as disjoint wall time. Current/last callback and bookkeeping are not fully captured. These current detailed-observer costs do not explain historical T365's much smaller stream. Observer-adjusted parent residuals are estimates.

Exactly one begin-observer capture is outside its apparent parent's stopwatch. Raw and uncorrected aggregates remain intact; the correction excludes375.4264ms capture and0.0333ms capture observer work from the parent. Corrected summary and exact four raw boundaries are retained; no other negative residual remains.

Remaining measured opaque sections: HasStorageEvidence15,334 calls/14.326759s combines PhysicalFileAuthority.ProbeStorageEvidenceNamespaceEntryFromRoot and enumeration. MainAdmission.Validate.GuardCheck has8.588199s observer-adjusted residual after timed scope children, including untimed handle-path/link checks. They are not automatically OS-call costs. Continue direct native timing and original C:/Temp reproduction; no production optimization, historical cause or whole-goal completion claimed here. T407/T408 exact-cause acceptance remains open.
