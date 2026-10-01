# Save/load cutover: bounded engineering map

Read-only inspection, 2026-10-01, initial clean HEAD
`41d9104076d59fabacf93c3b1be3cc8d5ccc238f` on the feature branch.
Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553). Current priorities and acceptance remain in [plan.md](plan.md).
No tests, applications, providers, installs, repository writes or ref mutations.
Production references below are relative to `BookOfEternityClient/`.
During inspection the sole writer advanced HEAD to `cb4837acdaed1eb45522a25d7d86234fd67bf413`
with a staged catalog patch. `git diff --stat 41d91040 HEAD -- BookOfEternityClient`
was empty: production source references in this note retain the initial identity.

See the [admission addendum](save-load-admission.md) for exact source blobs, incoming-library and shape limits, and optional-config authority. The both-absent baseline remains a specific contract to resolve in the load block.

## Recommended bounded migration

Split **save publication plus its actual browser caller** from **load replacement**.
The first can publish one create-only ZIP member through B1. The second must
prepare the complete replacement file set and generation transition before one
B1 decision, replacing the existing physical directory-swap protocol for new
operations. Keep the original load handler intact for old evidence. A new owned
extraction/scratch directory is needed; it is not a new transaction journal.

Do not redesign normalizers or gameplay receipts. Preserve archive validation,
saved config and durable choices/history, current load profile projection,
lifecycle-before-replacement locking, generation/revision fencing, and explicit
storage outcomes. Service-only success will not establish real GM/save/load
acceptance or Windows behavior.

## 1. Save: exact current call graph

`Services/SaveLoadService.cs:129–351`:
- Public save acquires canonical snapshot lease (:133); explicit-lease overload
  (:143) uses that lease across validation, ZIP construction and final publication.
- Mandatory nonempty `game_state` and valid soul/resource owner/state/history
  authority are checked (:155–208, :1350–1478). Resource owner authority is
  validated and recomposed into the archive; it is not a gameplay replay.
- ZIP includes durable game_state, lore, mods, world_profiles, stories, entity
  images excluding scenes, output and exact config bytes (:186–275). Metadata
  and schema-1 SHA-256 inventory are appended (:278–314).
- Archive is built with managed `ZipArchive` in runtime save staging. Final
  call is `FileSystemManager.MoveRuntimeFileIntoCanonicalSessionAsync` (:318).
  That method (:1894–1946) has an explicit descriptor-only gate at :1901 and
  uses Windows opened-handle rename at :1939. Its string-source overload
  (:1949–2004) has the same gate. This is an actual remaining portability blocker.
- Publication is create-only, including same-second filename collision. Do not
  change it to overwrite. Save catches exceptions into `false`; staging cleanup
  is best effort (:328–350). Autosave saves first, then takes a separate canonical
  lease for retention (:354–368, :1680–1701). Ordinary `_fs.DeleteFile` retention
  already uses B2, outside legacy-recorder context.

**Smallest save change:** keep one snapshot lease and current ZIP/manifest code;
close/flush the owned ZIP, validate/read its exact bytes through common type/link
scope, and publish `{destination, Before=null, After=zipBytes}` through B1.
Retain mutation hooks and create-only semantics. A duplicate archive name is a
conflict, not a retry that replaces a save. The temporary ZIP is scratch, not
accepted state; clean it independently after B1 established the outcome.

`WebUi/LocalWebUiMainMenuService.cs:142–159` currently wraps manual save in the
old `ExecuteAtomicWithinTransactionAsync` with **zero rollback paths**. This
still creates a v6 manifest/recorder and prevents a portable save even if the
ZIP move changes. Migrate this caller to a prepared/guarded B1 publication using
the existing canonical-before-UI-owner order; do not nest independent B1 commits
inside the physical recorder or invent a dummy file identity.

## 2. Load: validation, staging, activation and recovery

`SaveLoadService.LoadGameAsync`, :371–600:
- :377–383 resolves a relative save under game_session or accepts the explicit
  absolute input archive; this is a read input, not a write grant outside the base.
- :390–394 creates `.boe_runtime/load-transactions/<id>/stage/game_session` and
  a `LoadStagingAuthoritySet` before lifecycle/canonical admission.
- :400 raw bounded central-directory preflight precedes `new ZipArchive`;
  :407 validates structure, required soul/resource roots, and manifested content.
  :411–450 extracts only regular file payloads, skipping manifest and defined
  ephemeral paths. Extraction checks actual length, then captures/retains physical
  authority (`FileSystemManager.cs:4210–4311`). `CaptureFileIdentity` itself throws
  on Linux (`PhysicalFileAuthority.cs:890–897`).
- :468 verifies complete staging, :471 takes lifecycle lease, :472 captures runtime,
  :473–474 takes replacement canonical lease. :476 starts the old schema-2 load
  journal. :479–488 moves old live tree to backup and stage tree to live.
- Physical staging verifies exact registered file/directory inventory plus
  identities/lengths/hashes (`FileSystemManager.cs:2138–2971`), adds protective
  hardlinks (:2305), and retains/rebinds handles across directory rename
  (:4340–4409). These identity/anti-owner mechanisms must not become the new backend.
- :494 activates replacement generation and worker cleanup; :495 ensures
  directories; :496–530 repairs the canonical player-soul profile using an
  identity-conditioned publication and rebind; :531–535 refreshes runtime,
  loads settings and validates the sealed set; :536 commits.
- :538–558 releases physical staging, runs legacy rollback and restores runtime;
  any ultimate exception becomes `false` (:565–568). This bool does not distinguish
  confirmed restoration, committed state with failed runtime refresh, or uncertainty.

Legacy methods in `Core/FileSystemManager.cs`:
- `BeginLoadTransaction` :4002–4017 records previous/replacement generation;
  `ActivateLoadTransactionSession` :4020–4033 writes generation then deletes worker
  artifacts. `CommitLoadTransaction` :4036–4060 deliberately uses the original
  generation reader, persists `Committed=true`, then retries cleanup later on failure.
- `RecoverInterruptedLoadTransaction` :4063–4087 restores the backup and previous
  generation for uncommitted/absent-live cases; otherwise retains replacement.
  `RestoreLoadTransactionBackup` :4419–4437 uses whole-directory moves;
  `CleanupCommittedLoadTransaction` :4440–4449 removes transaction data before active
  journal; malformed evidence prevents inactive cleanup (:4499–4515).
- Keep these original schemas/handlers, including the B3a original-reader gate,
  separate from the new route. Do not bump a schema number and reinterpret old trees.

**Important admission collision, source-traced on both OSes:** new load staging
already populates the legacy load-transactions root before lease acquisition.
At :3612 Linux rejects old evidence before recovery. Windows runs the original
handler, but :4066 returns when active.json is absent, leaving this staging tree;
the unconditional :3624 old-evidence guard then rejects it too. Linux may first
fail earlier at retained identity capture; the lease collision remains after
that is migrated. No runtime load probe was run, and the five earlier Windows
menu/settings checks do not test this path. Use a distinct owned extraction
scratch namespace for new loads; retain recover-or-block for every legacy entry.

## 3. Smallest coherent B1 load decision

1. Read/validate the selected archive and extract to the new owned scratch scope,
   without live writes or generation changes. Reuse all archive budgets and
   hashes. Keep a complete logical extraction inventory (relative path, exact
   bytes/length/hash), rejecting missing/extra payloads, links and wrong types.
2. Prepare the existing pure canonical profile projection on staged data via
   `AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority`
   (:350–368). Include its resulting profile bytes in the final set. The current
   loaded profile is `game_state/meta/afterlife_entity_profiles.json`, not Daren.
   Do not apply a second identity-returning repair after publication.
3. Acquire lifecycle then replacement canonical lease; let admission settle old
   evidence/B1 recovery. Read exact B3a generation snapshot, validate the complete
   current and staged namespace/required directories, then freeze the member set.
4. Declare all replacement files, all old selected files absent from the replacement
   as deletions, and the exact old/new runtime generation image in **one B1 set**.
   Reuse the B3b replacement integration after it is accepted. Current public
   `PublishLocalFilesAsync` accepts canonical relative members, not arbitrary
   runtime generation paths; do not evade its fence with a crafted relative path.
5. Publish once. Advance generation revision only for established transition/recovery.
   Confirmed rollback restores exact old bytes/absence and old runtime. Committed
   outcome binds runtime/settings to the new generation; runtime-refresh or cleanup
   failure is committed follow-up, not permission to restore the old runtime as
   authoritative. Uncertain outcome blocks continuation and preserves evidence.
6. Derive post-load runtime from committed canonical state, never save-list metadata.
   No gameplay normalization/replay should mint new histories while loading them.

**Material save-library finding, source-traced rather than executed:** load's
whole-tree replacement includes the library. `FileSystemManager.ResolvePath`
(:598–633) always anchors under `BasePath/game_session`; required directories
(:505–507) put manual/autosave/checkpoint archives under its `saves/`. Save's
complete inclusion list (:186–275) excludes `saves/`. Load moves that whole live
tree into backup (:479–488), recreates empty required directories (:495), and
committed cleanup deletes the transaction tree containing the backup
(`FileSystemManager.cs:4440–4449`). The selected ZIP is closed at :460, so it is
not retained as an open source when the old tree is removed.

All production `LoadGameAsync` callers found were the console and web methods
mapped below. Their rebind/settings/UI-normalization/menu continuations do not
copy archives back; `NormalizeRuntimeUiArtifactsAsync` (:1385–1448) is control-file
maintenance. Browser lists only the same manual/autosave paths (:419–436);
console lists all three scopes (:3300–3303). Existing positive tests
`LocalWebUiHostTests.SaveLoadEndpoint_LoadsOnlyMenuIssuedSaveIds` (:486–525) and
`GameEngineTurnLifecycleTests.LoadSelectedSaveAndRebindRuntime_UsesLoadedCanonicalStateInsteadOfSaveListMetadata`
(:1033–1091) assert loaded state, not retained slots. No post-success library
preservation/copy-back test was found in the searched save/load and callers.

Consequently, **if the legacy load reaches successful commit/cleanup**, source
implies its old in-session archives are lost. Current earlier admission failures
can prevent that path; this note does not claim a reproducing runtime result.
This is a source-traced accidental-loss risk, not required compatibility. The
tracked implementation direction is now settled: preserve the existing save
library. Synthetic multiple manual/autosave/checkpoint slots must remain
byte-identical after successful load and after cold recovery. Do not reproduce
whole-tree library loss in the new member set. Current producers never include
`saves/`, but legacy validation accepts sandboxed incoming `saves/` entries. The
load block must explicitly prevent such entries from overwriting/removing the
retained library and cover their admission/conflict rule before publication.

Other old files absent from the archive include input/ready, workers and scene
images. Do not substitute B3b clear's exclusions for the declared load set.

Two genuine edge choices require bounded resolution:
- A file-only B1 journal cannot by itself turn an existing file into an ancestor
  directory, or a retained directory into a file. Preflight these structural
  collisions before any mutation; determine supported current archive shapes.
  Retaining harmless empty directories is otherwise sufficient, with B3b's pending
  consumers. Do not add an unjournaled destructive directory-pruning phase.
- Config is genuinely optional in the current producer and existing positive
  load tests. Do not make it mandatory to simplify portability. Validate present
  config before commit and preserve its choices; an absent config remains a
  supported input. `StateManager.LoadSettingsJson` (:75–91) retains runtime defaults
  on absent/malformed input, so the bounded migration must prove runtime/restart
  alignment for absence and reject invalid present config before live mutation.
  No hypothetical historical compatibility fallback or silent defaults overwrite
  is authorized by the optional-input contract.

For optional archive config, keep admitted live persisted config bytes when the
archive omits them, then derive post-load runtime from the committed canonical
authority. Do not retain arbitrary mutable runtime previews. Normal B2 clients
bootstrap config before menus. The low-level both-absent case requires an explicit
fresh GameSettings baseline, not an inference from the current Settings instance;
cover it separately with restart alignment. This is the bounded correction
candidate to verify in the load block, not executed behavior from this map.

B1 currently materializes exact images and journal JSON in memory. Existing ZIP
limits are 8,192 entries, 64 MiB/entry and 512 MiB durable expansion, with separate
name/manifest/soul/ratio bounds (:28–53, :791–882). Old live before-images can be
larger. Preserve these limits and fail safely on allocation/I/O failure; do not
introduce an arbitrary smaller save limit. Assess representative memory use before
claiming the complete supported envelope; any streaming refinement belongs to the
same B1 protocol, not a second journal.

## 4. Already common code and required narrow dependencies

- ZIP parsing/compression, hashes, raw central-directory preflight and schema/resource
  validation are managed code. Preserve ordering/budget/duplicate/path/UTF-8 checks,
  not the physical-ID implementation. Archive case-collision rejection is deliberate
  cross-platform validation; current live member enumeration must still not collapse
  distinct Linux names. Validate scope before open, including FIFO/dangling links.
- Archive/read staging APIs named `Physical` are not all Windows-only: ordinary
  `OpenReadFile` and directory wrappers have Unix paths. Their old type/single-link
  policy is still unsuitable for the new trusted-local input contract. Use common
  scope plus regular-file validation, accept ordinary hardlinks and replace names
  without altering outside hardlink bytes. No adversarial same-byte identity promise.
- `StateManager.RefreshGameStateAsync(lease)` (:131–135) invokes profile repair.
  Its default helper currently calls `WriteFileAtomicWithPublicationAsync`
  (`AfterlifeEntityProfileState.cs:403–406`), still physical. Preparing a correct
  staged mirror avoids that write on normal load; narrowly migrating ordinary
  Task-returning mirror repair is also needed wherever post-load refresh can change it.
- No save/load call to external Daren profile was found. Its actual path is
  `BasePath/client_profile/qte_showcase_rewards.json`
  (`DarenRewardProfileFileStore.cs:14–23`, `DarenQteRewardProfileService.cs:13`),
  external to game_session but within the configured base. Preserve it outside the
  load set. B1's `[BasePath]` scope can cover it; canonical integration API restrictions
  are a different issue. No home/global-documents grant or new user path is needed.
- SaveLoadService and these menu callers do not stop provider processes. Browser
  pending-turn/UI-owner gates and lifecycle/canonical leases serialize participating
  operations, not arbitrary surviving provider writers. Quiescent storage tests do
  not require provider integration. Live replacement when a writer may remain active
  requires B4's confirmed-stop/uncertain-outcome admission; do not add a supervisor
  inside save/load or infer a stop from the lease alone.

## 5. Actual outcomes and selected verification

Console save: `GameEngine.OptionsAndSettings.cs:1223` consumes bool. Console load:
`GameEngine.MainMenu.cs:3282–3288` loads then rebinds; :3338–3354 reports success,
syncs settings, validates and enters game. Rebind in `GameEngine.SessionAndSnapshots.cs`
:184–227 clears transient state and reads the new canonical session/turn.

Browser load: `LocalWebUiMainMenuService.cs:53–95` accepts only menu-issued save IDs,
checks pending/owner status, then uses replacement guard. Browser save :98–174 uses
the old zero-member recorder noted above. Host :104–118 maps bool to success/400;
DTOs :579–591 expose only bool/error/menu. Carry explicit committed/rolled-back/
uncertain plus follow-up through these actual callers. Menu rebuilding may itself
fail during unresolved recovery; it must not erase the established storage outcome.
Frontend consumers are `SettingsView.tsx:81–143` and `GameLauncher.tsx:68–88`.
SettingsView already invalidates drafts and reloads after failed load; preserve that
generation ownership and add honest uncertain/committed-follow-up notices.

Use focused category owners rather than all of `canonical-storage` (which currently
owns all `SaveLoadServiceTests`). Suggested separate portable-save and portable-load
owners can reuse exact existing methods:
- Save: `SaveGameAsync_FailureBeforeCommitLeavesNoPartialSaveOrTemporaryFile`,
  `SaveGameAsync_WaitsForCanonicalWriteLeaseBeforeReadingSessionSnapshot`,
  `SaveGameAsync_WritesIntegrityManifestCoveringEveryPayloadEntry`, mandatory-root
  negatives, `AutosaveAsync_CleanupWaitsForCanonicalWriteLeaseAfterSavePublication`.
- Archive: raw preflight/budget exact/over-one, zip-slip, corrupt ZIP, missing soul/
  resource roots, unknown owners, tampered/truncated manifested payload,
  case-colliding payload, metadata-only/optional-only rejection before lifecycle.
- Load: canonical/lifecycle wait tests; profile repair without reacquiring lease;
  disk/runtime rollback, recovery evidence and writer fencing; preserve unified
  resource roots/live pending contracts while excluding defined ephemeral controls.
  Physical hardlink/identity-swap tests should be replaced only for migrated routes
  with content/type/link/unknown-content coverage; original-handler tests stay explicit.
- Exact cross-consumers: `SessionOperationContextTests.BoundWriter_LoadRotatesGeneration_ThrowsBeforeReplacementMutation`
  (`canonical-storage`); `GmWorkerApplyGateTests.ApplyAsync_LoadWaitsForDecisionWithoutCreatingReplaceableSessionLock`
  (`gm-worker-repair`); `GameEngineTurnLifecycleTests.LoadSelectedSaveAndRebindRuntime_UsesLoadedCanonicalStateInsteadOfSaveListMetadata`
  (`turn-lifecycle-snapshots`); `LocalWebUiHostTests.SaveLoadEndpoint_LoadsOnlyMenuIssuedSaveIds`,
  `SaveCreateEndpoint_CreatesManualSaveVisibleInMenu`, and blocked-load test
  (`browser-api-host`). Extract these selectors instead of running broad owners.
- Source guards: `ValidationSourceGuardTests.ResourceAuthorityBoundaries_MustPreflightZipAndRetainLoadLeavesThroughPublication`
  currently demands old retained hardlinks; retain raw ZIP preflight assertions and
  replace migrated identity assertions with logical inventory/B1 requirements.
  `GmWorkerBridgeDocumentationTests.DurableFileAuthoritySourceGuard_RejectsPathBasedRuntimeMutations`
  and generation/ephemeral-worker guard assertions need route-specific review.
  Keep `PortableLegacyGenerationTests.CommitLoadTransaction_UsesOriginalGenerationReadBoundaryBeforeCommittingJournal`
  while the old handler exists. Select common B1/generation categories only when changed.

New REDs: source-traced old-staging admission collision on each intended route;
empty/non-UTF8/BOM before-images and absence; complete staged inventory; structural
collision rejected before generation change; hardlink outside bytes unchanged;
all meaningful member/generation/commit/cleanup cuts; later-member unknown data
leaves every earlier member untouched; abrupt child exit before/after commit with
fresh-process recovery; saved config/mod choices, immutable receipt/history and
pending authority byte preservation; exactly one rebind/accepted result; failed
runtime refresh after commit and uncertain outcome never presented as safe retry.

No GM payload schema change is required for storage-only replacement. Update
operational recovery/portability docs and affected source guards; if saved scope,
pending preservation, receipt ownership or acceptance timing changes, update the
relevant GM contract/examples in the same bounded change. This map establishes
source dependencies only, not successful save/load on Linux or Windows.
