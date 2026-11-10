# Genuine pending2 one-leaf recovery-mode correction

FINAL STOPPED. Base `907f64577873d78e7a27c9e701c63dd637e4ffb3`, primary `/workspace/1553-storage-migration-cloud-20261008-recovery-61d3a7f`. Exact3-path authoring scope; GM checkout untouched. Retained Sol6.1 XHigh is the parent-selected configuration; shell tools do not expose independent model/effort telemetry.

## Source delta and pins

Only executable addition at PendingWave.cs:282:

```csharp
        wound["recovery"]!["mode"] = "no_natural_recovery";
```

It is inside ConfigureGenuinePendingTreatmentWorldAsync after the existing effect clearing and before SeedHeldTreatmentAuthorityAsync/genuine original creation. Removing exactly this line restores old test SHA256 `ce554f54c3dc526760771374be69c4a763fc3da9fd3505f1f15e61c1e6b70090` byte-for-byte. No added preassert or shared/default helper change.

- `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.PendingWave.cs`: SHA256 `0aea03657d175b4af389fa49532af361e5b7c549359050ba2dc57359a7bcc42a`
- `specs/1536-complete-wound-materialization/plan.md`: SHA256 `5fc3e47c030cc57bd24f1c006123cb6a23111593107a874bf55a4d5e1221c30f`
- `specs/1536-complete-wound-materialization/tasks.md`: SHA256 `61cd48ba760c00877a83afb3a656501805a328493a6023dd7cd530a439cc07df`

## Known before/after legality

The accepted first actual2 failed at PrepareGuaranteedRequest (outer guaranteed_result_inapplicable), before full held request/confirmedclaim/repairwave/pendingcatalog/restore. Independent9c12 EVIDENCE accepts that prerequisite classification and source diagnosis; all35 chains stay unchanged.

WoundMaterializationContract.cs:1713–1738 requires active physical zero-slot wounds to have a recognized lifecycle impact. The existing untreated state initially supplies impact, but the declared stabilization result removes that care impact. `no_natural_recovery` is an explicitly permitted recovery mode and independently supplies the required impact. WoundMaterializationContractTests.cs:993–1005 already defines stabilized empty graph + this mode as valid. These are source-backed legality expectations, not newly executed test results. The fixture retains untreated care and declared stabilization/route/requirements; it does not pre-stabilize or weaken the validator.

## Static preservation

- One-line removal exactly reverses executable delta; every other test byte, all two-row attributes and assertions/hooks/cleanup preserved.
- Active plan/tasks checkpoint body is identical; removing only that new body restores both complete old files, historical tails and all checkbox lines byte-exact.
- Catalog, production contract, existing contract tests and original ce955 source remain byte-exact. No selector/globalCI/runner/grammar changes. Catalog728/11568 unchanged; ROOT retains valid audit without repeat.
- Exact dirty footprint is3 files. `git diff --check` is checked separately below; no Git mutation, project/build/test/discovery/catalog/runtime/fault/provider/new-agent operation was performed.

## Gate and remaining scope

Corrected SOURCE/build/runtime are UNRUN/PENDING. ROOT publication/GitHub recovery and focused SOURCE must precede fresh BOTH/exactpair preparation and only failed2 retry. No component GREEN/live pending producer/cold/new-root/UI/fullFR063/T070/#1536 closure claim. Qualified cohorts and immutable raw outcomes are retained; no passing repeat.
