# US5-RO1 reader preparation correction: independent bounded gate

**Disposition: ACCEPT exact four-call fixture correction; bounded reader-only fresh preparation/intentional RED retry is sound.** This is not final RO1 implementation, selection sufficiency, GREEN or public-consumer acceptance.

Independent reviewer, requested Astra XHigh; actual backend telemetry unavailable. Reviewed source `36f696c99cd3432af8159bb77e4612e4fcd67c2b`, failure carrier `c7200fa99ba583ec9bb252b3a0714db044b42e62`, correction `793136eb15d99c36f05e8434f17f47ab94ec3f83`. Local correction HEAD was clean during review. No new remote verification by this reviewer; primary owns publication verification. Read-only source/recorded evidence inspection only; no build/test/discovery/Plan/runner/controller/native owner calls.

## Failure and immutable evidence

Independently verified all three compressed/raw/original chains and all2085 source pins against immutable tested Git. Frozen packet `specs/1536-complete-wound-materialization/recovery/us5-ro1-reader-build-failure-36f696c99-20261015T200222Z-freeze.json` SHA256 `cf778134110e02460458a973c088bc15bfe4f9e94577f9a5c00319c55667c537`.

Actual log contains four unique CS1061 call sites at `WoundPlayerReadServiceTests.cs:89,90,91,125`; each diagnostic is repeated in the build summary, not eight distinct defects. All call the nonexistent FileSystemManager.WriteFileAsync. Summary/stdout agree: native1, Build-integration failed,77.7850366s, no timeout, zero descriptors/cases/executed/PASS/FAIL, selection complete=false. No tests or reader behavior ran. Zero fixture receipts is not cleanup-test PASS; orchestration cleanup success does not change that classification.

## Exact correction and API binding

Byte comparison proves the corrected test file equals the previous file with exactly four `.WriteFileAsync(` → `.WriteFileAtomicAsync(` replacements. Arguments, lease acquisition, fixture inputs/damage, assertions, snapshots and disposal are byte-identical. The only other correction-commit changes are the two plan/tasks front notes. Reader stub, category/selection, project helper link and shared helper are unchanged.

Existing `BookOfEternityClient/Core/FileSystemManager.cs:769` declares internal async Task WriteFileAtomicAsync(CanonicalWriteLease,string,string). Each call supplies the already-acquired fixture write lease, a path string and serialized string. The API delegates to lease-bound bytes writing; it validates lease/mutation authority at858–863 and uses the existing atomic write machinery. Integration assembly access already exists through InternalsVisibleTo in the production project at69–71. No new API, authority, storage path or mutation membership was introduced.

Source pins:

- Failed fixture: `5baf85eb6b1e7f68cf4060cccf7634113868f712286263d1237c8e80ead0b64f`.
- Corrected fixture: `9924fa65a89d20d9b658ce65c4e2ae11a88fad7b05d884bc687f8c5b1162b9a9`.
- Unchanged FileSystemManager: `14dcb900d73951bf5228d474255c197324ddb32df74a075ce22f4dec64600ec9`.
- Unchanged reader stub: `e853eb886ed1141858fd34af6b91e36dd8c7f69eb72a8719ab73a4b56006812a`.

The linked WoundPlayerProjectionTestData is helper-only with no Fact/Theory/InlineData declarations; the owning category selects only WoundPlayerReadServiceTests in integration. This excludes accidental projection-test duplication from this bounded retry.

## Retry boundary and required interpretation

After ordinary correction publication is verified, use fresh official preparation and the single `wounds-player-read` category. No stale NoBuild assumption, old A7/Late2 gate, aggregate or unrelated historical rerun is justified. Source inspection predicts three methods/eleven cases: two positive cases and nine malformed/missing cases. Fresh official discovery/Plan must establish actual selection; the failed preparation established zero.

The reader still unconditionally returns Unavailable. If fixtures reach the reader, the expected intentional RED is two availability failures plus nine unavailable passes. Those nine passes are vacuous for reader correctness. Positive read-only assertions follow the expected availability failure and therefore will not be reached in that RED. Preserve actual native exit/counts/complete status and eleven distinct genuine per-case cleanup roots/receipts if all eleven fixtures run; verify each root absent, lease inactive, removal true and failure explicitly null. Fixture creation exceptions also dispose their root. Do not infer successful filesystem verification from predictions or an aggregate cleanup line.

Freeze the resulting behavioral RED separately; retain both original projection RED and failed-build evidence unchanged. Any new preparation, fixture, count or cleanup failure requires its own classification before proceeding.

Final pure projection/current implementation review and its exact realm/history/effect/privacy/read-only coverage obligations remain pending. This narrow correction gate neither accepts that code nor releases GREEN/full US5/FR063/lawful ACTIVE. No further material blocker found for this exact reader-only intentional RED retry.

**FINALSTOP. Only this external MD/JSON pair written; no repository changes and no pending reviewer writes.**

