# Windows browser protocol30 — first complete FAIL result frozen

Source #1553 / F04-F05. Checkout5d9f822195a2d4788dadcf483eee742e3ae7e766;
unchanged compiled source8c2ecdfaa1003e49bf15ba83693788e9b19dd10f, same Windows branch.

NoBuild PlanOnly:1descriptor/7Theory/30planned/0executed,13.4988788s/exit0/cleanupcomplete.
Fresh F18 integration/Client/TestSupport binaries reused after exact1629 source/data/
recipe and157 DLL/PDB equality before preparation, before actual and after actual.
Only two previously updated journal documents are explicitly excluded from source
pins; product/tests/runner/catalog unchanged. No extra build or common C5 selection.

Exactly ONE NoBuild actual:30planned/30executed,28PASS/2FAIL, runner97.6990411s,
category84.4173681s, exit1. Existing3min/8min budgets unchanged; zero skips/duplicates/
timeouts. Selection complete=true does NOT mean category success; original category
Completed=false and all first raw failures are preserved. All30 cleanup outputs
OwnedFixtureRemoved=True; no browser roots/worker runtime/owned operations remain.

Both failures are WindowsAuthenticOriginalSix_RecoversReceiptAndRetainsNeutralBackup,
ordinary and extended. Actual System.IO.IOException sharing violation comes from
File.ReadAllBytes(ProfilePath) at Windows.cs190 inside the still-owned write lease.
Pinned source shows authentic6/context/profile+member publication and zero current
common-member check reached first. The manifest receipt assertions, neutral backup
creation and subsequent authentic6 Fresh() recovery are UNREACHED/UNQUALIFIED.

Read-only diagnosis/proposal is separate and pending independent review. Product
retains its source stream with write/delete access; a new default File.ReadAllBytes
reader shares only Read. This sufficient source mechanism explains self-sharing
incompatibility without claiming a new raw handle trace. Proposed correction is
test-only compatible physical reading with FileShare.ReadWrite|Delete under the
same held lease, retaining the original After equality and all later oracles.
No cached _publishedBytes shortcut, product share weakening, disposal relocation,
retry/sleep/timer change or fixture correction has been applied. Further runtime HOLD.

Three original ZIPs preserve complete runner dirs, separate stdout/stderr, commands,
PID/exit/env states and reused-build provenance. All34 entries byte/hash read back.
28 passing rows cover current schema7/members8, aliases2, external pending4,
cleanup protocols4, pending admission order8, neutral-only pending backup2. The
neutral rows each output actual cut=1/recoveryEvents=3/exactSnapshot=True.
All seven bodies require Windows; source assertions are not mock/platform returns.
Other native hook/byte/absence expectations are execution assertions, not new
separately serialized native values/file images.

Fresh() is a new FileSystemManager in the SAME process; no fresh-process cold
restart is qualified here or by F18. No browser DOM/UI, full turn/whole migration,
other Windows recipes, B2–B5, paid game-provider calls, new product/architecture/
security/CI change. F18 prior6/6 and historical ordinary40 FAIL remain unchanged.
Independent first-evidence/diagnosis review pending; parent tasks stay open.
