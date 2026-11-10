# ROOT-HS01-F02 — independent source addendum

**Verdict: BLOCKED. No actual run is released.** This addendum confirms an earlier fixture precondition failure at source `e182b6745817bd34ff5247c34f8d469bb71754f1`, frozen preparation carrier `e0ed69e2b96f41437e8465ae41182b111dfc1150`. It supplements the immutable F01 report; it does not review the subsequent correction.

The first failing boundary is more precise than the initial coordinator diagnosis. In the pinned `MortalWoundTreatmentResourcePublicationLifecycleTests.HotSubmissionCustody.cs` blob, lines 38–41 call `CreateWithObservedOptionalPaths`; line 42 then requires success. `PendingTurnSnapshotReader.cs:452–467` rejects each of the five optional public paths because it is outside the closed `originalPathPresenceV1` set. `ReadCurrentCore` returns failure at lines 162–164, **before reading the manifest**, with `pending_turn_snapshot_reader_required_paths_invalid`. The issue text identifies the observed optional path outside the presence set. The later `absence_unproven` branch at lines 676–693 is not reached for this selection.

The five rejected optional paths are `status_changes.json`, `experience.json`, `core/player_status.json`, `misc/characteristics.json`, and `stat_points.json` at their exact paths recorded in the JSON report. Four have signed file/hash coverage in the historical originals; that does not make them eligible for this stricter observed-optional selection. Soul is the required path. The pinned assertion lines are **42 and 55**, not the earlier shorthand 36 and 48.

I independently decoded both actual prior Late2 TRX cases and their six embedded manifest observations. Each case has three identical manifest images, 22 presence-map keys, and no `game_state/player/status_changes.json` entry. That path is absent from signed file/hash coverage and the rollback inventories. The separate structural-inventory observations agree. This establishes **uncovered inventory**, not authenticated absence. The historical tests passed; HS01 itself has not run.

The committed gzip SHA-256 is `1455812f2ab3bc30f3c9b43c4a04b77028629e0a8a15d30054a9f287c1700dbb`; decompressed TRX SHA-256 is `1a2bb8054bb9122bc5448fef13a8f48c199f7a23952ec5cca1ffb37a9ca503b2`. I checked the artifact-index pins, committed compressed bytes, decompressed bytes, local raw TRX, and equality with historical carrier `eac4e7e0dd695770d86f776d89f90ae9a2d7f822`. Exact case IDs, manifest hashes, source blobs, and ten source projections are in the JSON/support files.

After changing to ordinary optional selection, line 55 would still be wrong: an uncovered ordinary optional root is not added to `AbsentLogicalPaths`. The current dictionary/diagnostic helper also turns null bytes into `present=false`, which would misrepresent unknown original presence as absence. Both must be corrected together.

Required correction:

- Use the existing ordinary optional policy, without modifying production guards, the closed presence set, original bytes/authority, or schema.
- Record this original row as `sourceStatus=UncoveredInSignedInventory`, `present=null`, and `base64=null`. Keep the actual current absence observed under the post-original Stage lease separate as fixture provenance. Covered originals retain their genuine bytes; missing declared or unreadable artifacts remain failures.
- Any signed-absence row must have real signed-absence evidence. Ordinary optional selection alone grants none.
- After the public write, require F01's exact `Uncovered joint mechanical input is present: game_state/player/status_changes.json` refusal. Do not accept alternate refusal messages.

This correction fits the accepted bounded engineering design: line 113 specifies the existing optional policy, line 161 distinguishes source states, and line 174 preserves the mechanical consumer's existing selection. The affected contract should explicitly clarify unknown original presence before implementation. That clarification creates no new custody issuer or semantic authority and needs no original/schema change. It does not establish pristine absence, full S, output acceptance, B, or M3.

The first F01 review missed this earlier precondition. Its finding remains valid downstream, and its blocked verdict remains unchanged; this addendum corrects the ordering and the meaning of original “absence.” Both old report hashes were rechecked unchanged: MD `18886ecd532776f9658c3f77c0c05863ebbc3606161265bc3d785d3398155c31`, JSON `8389138cb51f6b644ab7e120f4704c93d3beff5ad0ea12cfbcfbdd324735fc37`.

The later root correction is outside this pinned review. It needs fresh preparation and an affected SOURCE/FIXTURE/SELECTION decision. Protected Late2 and HS01 causal RED remain unreleased by this addendum; no production success logic or whole-stage acceptance is granted. No build, discovery, test, application, network, or repository mutation was performed.

Configured reviewer: `gpt-6-astra`, `xhigh`, independent of the author, according to the coordinator spawn receipt. Backend telemetry is unavailable. Inspection support SHA-256: `b8730a47c7890768f256a9a0f2ed1bedf502c236f94cb1df0a5d6e9577ab8f5a`. The initial new finalizer hit a full `/tmp`; after root freed task-owned scratch space, a new finalizer wrote this pair. Existing artifacts were unchanged.

**FINALSTOP.**
