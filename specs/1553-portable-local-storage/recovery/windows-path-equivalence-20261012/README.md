# Windows path equivalence — actual operand RED preserved

Tracked #1553/T068. HOME-PC reboot recovery boot01:47:25.5UTC verified:
localremote00764cf90bdc96c93c1c4c922a3bc25a918ee4d0 clean; all historical
phases exited before boot,12archives/1626unchangedsources/157oldDLLPDB preserved.
No new build/actual had started before recovery summary; no interrupted runtime
was labelled complete. Fresh observation SOURCE PASS actual Astra XHigh00764cf.
Build81.5986464s/PlanOnly95.9694994s/2planned0executed;1629source157DLLPDB.
One focused authentic6 NoBuild2cases=1ordinaryPASS1extendedcausalFAIL,
23.2297020s,3min/8min unchanged,cleanupcomplete.42ZIPentries preserve originals.

Actual NativeRollbackPathOperands trace: raw GetFinalPath equals intent
DestinationPath byte-for-byte in extended drive form, while compared path is
exactly raw[4..] (ordinary drive form). Source and failed-source operands remain
extended. Original unknown-path decision/primary error unchanged. This confirms
one-sided spelling normalization in the same native failure, before any fix.
Full operands are in observe/manifest.json and original TRX; file IDs/fullintent
image are not separately claimed. Product equality fix/GREEN remain UNRUN.

Existing TrustedLocalFileScope.NormalizeWindowsPathSpelling is the bounded
candidate:drive/UNC spelling, device/ambiguous/dot components refused. It does
not grant authority; ID/hash/containment/reparse/race/unknown-path checks stay.
UNC tests will be string-only; no UNC native/network filesystem qualification.
No fresh-process cold restart; prior3browserFAILcarriers/F18PASS preserved.