# T034 bounded full-route construction proposal (not implementation authorization)

## Reuse and exact scope
One owned inert TestSupport child, same qualified PTY100x25/noecho fixture and raw
capture. Reuse the construction approach in ConsoleSettingsSessionTests.cs:504,
not ProductionLoadGameEngine's scripted FixtureInput/TestExplorerConsole. Real
console input and real Explorer console are required. No provider, relay, daemon,
accepted turn or hosted replacement is constructed. No new engine/UI framework.

Minimal access-only seam: GameEngine.InGameOptionsMenu private→internal, unchanged
body. ExplorerMode.TryProcessCommand is already public. Do not clone Options into
SelectionPrompt: its actual ShowSingleChoiceMenu custom ReadKey/render loop must
run. Unsupported position query stops the probe; never fabricate DSR replies.

## Required real services and fixture DATA
Exclusive temporary root with authored isolated canonical DATA, never a live-world
copy. FileSystemManager, StateManager, actual GameSettings/local config, Russian
LocalizationManager, real ExplorerMode console, actual StorageTransportMoveService
and its current local-scope/identity dependencies, real SaveLoadService, GameLoop
for explicit fixture turn43, inert-configured AudioService and ConsoleAppearance.
Use existing initialization/admission APIs as their contracts require; no forged
runtime owner records. Enumerate/instantiate every dependency reached by the chosen
branches before execution. Null services are allowed only for provably unreachable
GM/game branches and must not be a successful path through missing behavior.

Use exact reviewed names/IDs/weights from authored T034 scenario inputs. Inventory
ShowInventory reads totalWeight/maxWeight/isOverloaded directly (lines221–232);
never substitute a calculated display estimate. Actual transfer services determine
post-transfer inventory DATA and display. Record baseline and per-stage canonical
hashes, exact selected item/storage IDs, and unchanged story/request absence.

## Sequence and proof boundaries
1. Actual Explorer.TryProcessCommand("/storage_move") with locked fixture storage:
   select locked row, observe fresh unchanged inventory, select Back. No mutation.
2. Separately declared fixture DATA transition grants access solely to exercise
   rendering; this is NOT a GM decision, live permission or acceptance witness.
3. Actual retrieve through storage panel/picker, exact chalk identity and complete
   redraw, Back through both levels. Verify actual local transfer bytes.
4. Actual InGameOptionsMenu: numeric1 plus Enter; real PromptTextInput save name s,
   blank description, real CreateSaveAsync, complete success/ACK/no followup warning.
   Require clean committed result via archive/typed evidence, exact one archive and
   actual metadata/config/canonical ZIP bytes. No manually simulated save success.
5. Actual Explorer deposit via picker past sword to chalk, both Back levels, exact
   reverse transfer and source-provided post-transfer weight display.
6. Actual Options numeric2 plus Enter enters actual LoadGameFlow; observe actual
   sole archive row using returned metadata timestamp/name/location/size, navigate
   archive→Back and Enter Back. Re-enter actual Options/Load, navigate Back→archive
   to prove focus, then Back and Enter to leave WITHOUT committing Load.

Selecting the archive with Enter invokes LoadSelectedSaveWithMainLifecycleAsync,
then potentially EnterGameLoop; this is NOT an inert menu-only path. Do not fake
that success or supply null lifecycle services. T033 already separately qualifies
in-process canonical continuation; actual hosted replacement and live local route
remain T016. A demand for archive Enter in this inert stage requires an explicit
separate genuine lifecycle fixture design/review, not silent scope expansion.

## Evidence, controls, and cost
Retain all stdout/stderr raw, original/configured/pre-key/restored termios, ioctl
and Console/Spectre measurements, exact current menu descriptors/selection cuts,
input bytes (SS3 from observed mode, not hardcoded CSI), returned branch/choice,
real archive metadata/hash and per-stage DATA deltas. Reuse held immutable fixture
inputs within this one child; reread actual mutable state after each real transfer.
No cache authority. One bounded child/guardian; no game startup or broad suites.
Normal child exit/EOF/configured restoration and explicit original restoration
remain separate from any negative owned kill/drain. Geometry mismatch/unknown UI,
query, extra archive or missing dependency refuses; no retry/fuzzy matching.

Before implementation: independent approve exact service construction and admission
needs, access-only seam, stage receipt mechanism, and clarification that Load focus
is geometry proof rather than hosted Load acceptance. No source changes made here.

## Concrete constructor/admission map (read-only source follow-up)
- `new FileSystemManager(exclusiveRoot, NullLogger<FileSystemManager>.Instance)`;
  `new GameSettings { Language="ru", MusicEnabled=false, SoundEnabled=false,
  GenerateSceneImages=false, ShowImagesInConsole=false }`; one real
  `StateManager(files, settings, logger)` and `BootstrapLocalStorageAsync()`
  (`StateManager.cs:51`) establish local config and genuine fresh generation.
  No durable main/daemon records are authored. This is a cold isolated local
  session, not an imitation of a Running remote owner.
- Execute each UI stage through
  `SessionOperationContext.RunParticipatingCurrentSessionAsync(files, callback,
  observeOriginalClose: ...)` (`SessionOperationContext.cs:110`). This establishes
  the real existing participating MainAdmission, binds current generation and
  completes/closes on return/error. Do not keep a canonical write lease over UI.
  Bootstrap and fixture-data installation precede stage input; record generation
  before/after every stage. No generation replacement is expected in Back-only Load.
- Explorer: `new ExplorerMode(state, files, loc, clipboardService: clipboard,
  inputSource: actualConsoleInput)` uses its default real SpectreExplorerConsole,
  actual LocalUiSessionLockService and default console lock owner. Its public
  TryProcessCommand enters the existing command lock/SafeExecute path. Storage
  panel calls ReadStorageMoveContextAsync and MoveStorageItemAsync directly
  (`ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:3090–3178`). The move service
  acquires/releases its own canonical write lease; actual inventory refresh calls
  StateManager.RefreshGameStateAsync. CompleteCommandResultAsync discards a pending
  local rollback snapshot only when no GM action is returned. Assert returned empty
  action and absence of request/repair/original artifacts, not invented GM success.
- GameEngine composition follows ConsoleSettingsSessionTests:504. Supply the SAME
  files/state/settings/loc/explorer, a real GameLoop.SetSession(fixtureSession,43),
  real SaveLoadService(files,state,logger), AudioService(files,settings,logger),
  ConsoleAppearanceService(settings,logger), SystemModService(files,settings,logger),
  real console input and clipboard implementation. Default text-composer console
  must remain real: PromptTextInput (`MainMenu.cs:838`) calls TextComposer.Read with
  `_textComposerConsole` and `_clipboardService`, not a scripted string substitute.
  Unselected settings/exit and Load-commit branches are forbidden by exact input
  script; no null reachable dependency may be accepted. Provider/image/validation/
  health services are not called by Save or Back-only Load and are not started.
- Existing assembly friend entry explicitly includes TestSupport
  (`BookOfEternityClient.csproj:66–67`), so the sole proposed production edit is
  private→internal InGameOptionsMenu without body changes. No reflection.
- Actual Save branch calls CreateSaveAsync (`Creation.cs:248`) which owns its lease,
  catches/preserves typed failure and closes before returning. Options itself returns
  bool rather than the typed Save result; therefore do NOT claim direct receipt
  access. Observe actual UI success plus no follow-up warning, exactly one archive
  returned by real GetAvailableSavesAsync, its metadata/ZIP byte content, and exact
  before/after archive inventory. Do not invoke CreateSaveAsync again for a receipt.
- Back-only Load runs the actual three-directory GetAvailableSavesAsync aggregation
  (`MainMenu.cs:3400+`), real metadata row and SelectionPrompt. Enter Back returns
  before LoadSelectedSaveWithMainLifecycleAsync. Selecting archive Enter is excluded
  and cannot be qualified by this minimal composition.

## Stage receipts (fixture-owned observations, not authority)
After each awaited public Explorer/internal Options call returns and its participating
scope closes, write external JSON: stage name, exact returned value, captured raw
byte cut range, generation before/after, selected canonical file hashes before/after,
actual storage/item identity positions, story unchanged, pending/request absence,
and observed close callback result. Save adds actual sole archive path/hash, metadata
and selected ZIP member hashes; Load-Back adds archive unchanged and no replacement.
Capture failures separately before cleanup; do not emit a successful stage receipt
from menu text alone. Keep the original child handle/guardian/EOF/termios evidence.
The local UI lock is the real service's own fixture record; do not manually delete
or impersonate it, and do not label that record a hosted main-owner closure proof.

Remaining pre-implementation checks are narrowly source verification of actual
console input/clipboard constructor defaults and the exact authored fixture DATA
installation via existing typed writes. If either reaches an unlisted service or
requires host authority, stop design review rather than broaden the fixture.

## Closed source gaps and exact fixture installation
Input/clipboard are now concrete: pass `SystemConsoleInputSource.Instance` to both
Explorer and GameEngine. GameEngine.cs159–161 constructs the real
`StandardTextComposerConsole(_inputSource)`; its ReadLine/KeyAvailable/ReadKey directly
use that input, while SpectreExplorerConsole has the same default. Supply a real
`SystemClipboardService(NullLogger<SystemClipboardService>.Instance)` (constructor
ClipboardService.cs:37), not TestClipboardService or FixtureInput. The fixed route
sends ordinary save-name characters and Enter, never paste/clipboard shortcuts;
clipboard must not be invoked. No ConsoleE2E input script or synthetic returned text.

Fixture installation will reuse existing authored test DATA builders, NOT a live
seed export or accepted-world copy. Existing exact precedent is
BrowserStorageTransportParityTests.WriteAcceptedLocationStateAsync (962–994):
`MortalLocationTestFixture.CreateCanonicalLocationWithIdentity`,
`CreateStorageMetadata`, set storageMetadata disposition populated,
`ResealCanonicalLocation`, `CreateCurrentProjection`, `CreateWorldMap`, and
`CreateIdentityIndex`. This builds valid local location identity DATA; it is not a
GM publication or runtime owner witness. Use the reviewed exact location/storage
names and fixture IDs in one source fixture. Current projection receives contents
while world-map storage metadata remains metadata-only.

For sword/chalk use existing `MortalItemTestFixture.CreateCanonicalRootAtTurn` /
`CreateCanonicalRoot` as appropriate to authored fixture turn, then configure exact
name/type/count/weight/display fields before corresponding fixture receipt/index
construction. Reuse `CreateIndexForCarriers` with sword player_inventory/player and
chalk location_storage/<locationId>/<storageId>; this supplies the history required
by StorageTransportMoveService.ExecuteTransferAsync (439–490). Do not write only
itemId/name and pretend it is accepted, and do not copy receipt authority from a
live world. These authored receipts are fixture DATA, explicitly not acceptance.

Install the fixed map/current/index, item inventory/index, player status/money,
soul-state Mortal realm, fixture story/chat session, and necessary empty companions
through `FileSystemManager.WriteFileAtomicAsync(CanonicalWriteLease,path,content)`
under one short owned lease within an existing participating session after local
bootstrap. Use existing `MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles`
for empty companions and `MortalStorageAccessProjection.Build(map,null)` for the
storage access projection, as MortalLiveSeedBuilder.cs60–68 does; do not invoke its
whole export/copy/validator flow. Assert builder issues empty. Keep explicitly
reviewed `totalWeight`, `maxWeight`, `isOverloaded` source values, never recompute a
presentation estimate. Close installation lease before `RefreshGameStateAsync()`
and before any terminal input. That refresh is the actual StateManager aggregation
needed by Explorer realm dispatch, not GameEngine health/turn preparation.

The locked→accessible transition occurs only between fully returned/closed UI
stages. Use the same authored fixture builder to change access consistently in
map/current/storage-access projection under a fresh short typed write lease;
retain items/identity/history/story unchanged. It is labelled a conditional fixture
arrangement, not a live grant. All subsequent transfer mutations go exclusively
through the actual StorageTransportMoveService invoked by Explorer.

Correction to earlier generic lock wording: `/storage_move` is NOT in
ExplorerMode.PrivateImplementation.cs282–313 LocalUiSessionMutatingCommands, so
TryAcquireLocalUiSessionMutationLockAsync returns true without creating that UI
lock for this route. The real participating MainAdmission/session generation and
actual per-transfer canonical leases are the relevant custody. Do not manufacture
or claim an extra UI lock. Stage close callback plus original process evidence is
recorded, never upgraded into hosted/remote owner proof.

The only new production access seam remains unchanged-body InGameOptionsMenu.
Before running, source review checks the completed authored fixture constructor
against these current builders, exact canonical join prerequisites and all reachable
menu services. No unresolved runtime service is to be patched opportunistically
on the live path. Unexpected setup failure stops; no automatic retries.

## Final admission/fixture specifics (supersede generic CurrentSession wording)
Capture the generation string returned by BootstrapLocalStorageAsync once. All
installation/refresh/options stages use existing
RunParticipatingExpectedSessionAsync(files, capturedGeneration, callback), never a
handwritten generation JSON or permissive creation on missing generation. Explorer
uses existing GameEngine.DispatchExplorerForCurrentSessionAsync(files,explorer,cmd)
(ExplorerDispatch.cs13–25), which explicitly checks ReadExistingSessionGeneration
before the CurrentSession helper could bootstrap. Compare captured generation before
and after each dispatch; no nested redundant participation wrapper. A missing or
replaced generation fails, not recreated/adopted. Its normal return follows actual
participation close; no separate invented Close receipt if the helper exposes none.

Install resource companions explicitly using existing
ResourceBootstrapStateBuilder.BuildMortalPlayer(2,41,20,20,20,20,20); require IsValid,
then canonical DefinitionsPath/StatePath/HistoryPath. Compose
CanonicalResourceOwnerAuthorityComposer.ComposeAsync with actual installed input
reads and FinalAfterImage purpose, require IsValid, write exact AuthorityPath.
Use lease-aware reads for this composition while installation lease is held, not
nested reads acquiring a second lease. Empty vehicles/npc_skills/item_bonds/
item_text_updates come from BuildFreshMortalBootstrapFiles(2,41,...), as existing
MortalLiveSeedBuilder. For item fixture overrides call ResealCanonical after edits
and before CreateIndexForCarriers; never leave an old receipt seal after changing
materialization DATA. Supply exact current map/index/current projection agreement
and storage access projection before any actual read/transfer.

Actual SystemClipboardService constructor takes the non-null typed NullLogger;
SystemConsoleInputSource.Instance is explicit, not null. Immediately before Save,
await real StateManager.RefreshGameStateAsync inside the captured-generation stage,
then require CurrentState.CurrentLocation equals the exact authored long name and
GameLoop.SessionId/TurnNumber equal fixture session/43. ZIP metadata must match that
state and archive bytes. This refresh is not whole-engine health/validation.

Load focus sequence uses observed key mode and explicit finite two-row transitions:
archive initially selected→one Down→Back; one Up→archive; one Down→Back→Enter.
The second visit repeats archive focus/Back exit if needed. Up is a deliberately
bounded two-row fixture check, not a change to forward-only live finite_select and
not a key sent without fresh exact current selection. No archive Enter occurs.
After each public call returns, emit an external fixture stage receipt, not a fake
"Ваш ход" production prompt. The real player's prompt and hosted Load/continuation
remain live/T016 proof. No stage boundary is advertised as game-loop readiness.

This final proposal is ready for source-design review. No implementation, build,
fixture seed installation or runtime process has been performed for fullroute.
