Confirmed: G01 raw timestamp-order defect; minimal fix plan accepted.

Saved/recovered `345f550644d443a09022c0afb88db1d9e3ec7775`, original source `17684f613fea24d9236105e43be0709001cc7744`. 20 read-only checks pass. Five original files match local/Git/independent clone; compiler0, pure probe1 with22PASS/1FAIL, solely `future_timestamp_cancels`; observers empty, no game/API operation.

The test passes receiptT and nowT−1. Current Step checks the sign only after GetElapsedTime→TimeSpan→TotalSeconds. The original assertion demonstrates non-cancellation. Quantization of a sub-TimeSpan-tick negative interval to zero explains this; original Frequency/residence/returned-state measurements were not logged and are not reconstructed.

Minimal fix: reject `item.ReceivedTimestamp > now` before conversion, using the existing cancellation reason. Keep all23 assertions and other branches unchanged. Optional nonpositive validation is a separate contract choice. Preserve initial RED and firstGREEN failure. Use a new unused `green-corrected` phase with correct success classification; save/recover the correction before its one directcompile/pure23-check run, keeping60s/10s bounds and native accounting. Preserve that original result for whole SOURCE.

The preliminary writer-clean metadata check encountered an unrelated untracked MapFrames.cs; this report binds immutable saved345 and clean independent clone, excluding that file. No native retry occurred.

This is not whole SOURCE/PREP or game/capture admission. Phase-admission clarification remains separate; gameplay/collector/crash5 NONE and L0152/57/oldUNKNOWN unchanged.
