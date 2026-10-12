# Windows path equivalence — native GREEN, evidence review pending

Tracked #1553/T068 on `1553-storage-migration-windows-f18-20261011T235650Z`.
Compiled source `13e17a3eb446b02e3d136b0214b52b00e730e25c`. Native HOME-PC Windows 11/NTFS,
.NET SDK10.0.401. Fresh build 76.2472392s; PlanOnly36 cases/13methods,
two exclusive integration descriptors, zero execution, 00:01:28.1546530.
One NoBuild GREEN: native30 30/30 and affected F18native6 6/6, total36/36 PASS,
00:01:51.1851158, category3minutes each/runner8minutes explicit unchanged.
Zero skips/duplicates/timeouts; complete selection and both runtime/tree cleanup.
1629 source pins and157 DLL/PDB equal before/after; Client/Integration four outputs fresh.
GREEN46 original ZIP entries and causal RED42 entries retain raw CRLF/localized logs.

Actual pre-fix causal RED compiled00764cf: one ordinaryPASS/one extendedFAIL,
23.2297020s. The same failing native opened-handle path equals extended intent
DestinationPath exactly; the compared path equals raw[4..]. Source and failed-source
paths remain extended. Full operands are preserved in observe/manifest.json/TRX.
File IDs/full intent image are not separately exported or claimed.

Product delta versus93ffa98 is four lines: both Windows PathsEqual operands use
existing TrustedLocalFileScope.NormalizeWindowsPathSpelling before existing
GetFullPath/OrdinalIgnoreCase. Linux unchanged. Temporary observer removed.
No new normalizer/trim/device-prefix stripping/namespace grant/journal format;
identity/hash/authority/containment/reparse/race/unknown-path guards retained.

Two existing authentic6 rows each exercise20 actual private-comparer comparisons
and18 refused spellings on both operand sides. Drive/extended and UNC aliases,
case/slash and unequal name/drive/server/share/outside paths are checked. Device,
GLOBALROOT/Volume/pipe/ADS/reserved/trailing dot-space/dot-segment forms refuse.
Directory trailing separators remain significant. UNC controls are strings only,
no filesystem/network grant; case-sensitive Windows folders remain unqualified.
Both rows pass original schema6/physicalAfter/receipt/identity/neutral backup,
fresh same-process recovery/restored bytes/absence assertions. Physical BOM/noBOM,
strict malformedUTF8 and doubleBOM controls emit two successful witnesses.

Independent actual gpt-6-astra/xhigh SOURCE + CAUSAL RED EVIDENCE PASS at13e17a3;
GREEN evidence/final metadata pending. No extra pure-controls RED was required:
the original product defect was reproduced natively before the patch.

Boot01:47:25.5UTC recovery verified previous007 head clean/localremote equal;
all historical phases had exited before boot,12 archives/1626 unchanged sources/
157 old DLL/PDB intact. No new build/actual had begun; no interrupted run labelled
complete. All three prior browser failure carriers and original F18 PASS preserved.
No fresh-process cold restart, UNC native/filesystem, case-sensitive directory,
full game/UI/provider, other five Windows recipes or performance qualification.
Parent #1553/T067/T068/T061–T065 OPEN; ordinary40FAIL/performanceUNQUALIFIED and
B2–B5 paused remain unchanged. No main/merge/PR/issues/installation/security change.
