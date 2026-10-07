# T050-CONSOLE-AUDIO-LINUX implementation plan

> Sole writer executes inline with Superpowers executing-plans/TDD/debugging;
> independent actual Sol6.1/xhigh design/source/evidence/metadata reviews are read-only.

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553),
US5/FR-009/011/012/SC-005, owner authorization from accepted clipboard
`499ca652f367ec7dc5da5e85abb731793e5e47a5`, branch `codex/1553-load-filesystem`.
Goal: actual console AudioService retains settings/volume/music/cues with owned
cancel/stop/disposal and clear capability outcomes; browser HTML Audio remains
client-side. This is bounded synthetic lifecycle/backend qualification, no physical
playback/device/session/Windows/game acceptance.

## Source and backend choice

`Services/AudioService.cs` directly constructs NAudio AudioFileReader/WaveOutEvent
for both cues and music. Cue tasks are untracked by StopAll; music cancellation can
skip PlayTrackAsync cleanup, while caller disposes the same output separately.
`AudioService.SettingsPreview.cs` supplies the scoped settings draft.
`GameEngine.MainMenu`/TurnLifecycle/OptionsAndSettings are actual callers;
RefreshAudioPlaybackContextAsync chooses playlist from existing context.
`WebUi/BrowserAudioService.UpdatePreparedSettingsAsync` accepts settings only after
existing transaction commit and calls AudioService.ApplySettingsAsync.
`LocalWebUiHost` currently registers the same full output service. Browser assets,
DTOs, HTML Audio, autoplay gesture/volume and existing storage/lease semantics stay.

Selected candidate: existing optional SDL2 Linux library for queued float PCM;
NLayer core3.0.0 (MIT/net8, bundled managed application dependency) decodes MP3,
existing NAudio managed WaveFileReader decodes PCM WAV. Do not add
NLayer.NAudioSupport3 or upgrade NAudio2.2.1. Windows retains AudioFileReader+
WaveOut adapter. No player install/compiler/service/configuration required: absent
SDL2/symbols returns BackendUnavailable; absent/failed output returns DeviceUnavailable.
The game remains usable silently. SDL2 is optional capability, not a new mandatory
player dependency; NLayer is included by normal application build/publish, not
fetched by player startup. This choice and justification are announced before
implementation; if delivery requires a new install/product choice, stop that stage.

Alternatives: ffplay is present but command control does not establish original
live volume/track continuity; optional native mpg123 decoder adds another native
capability/interop surface. SDL2 + bundled managed decoding keeps the current
tracks/formats and permits volume on future queued blocks without restarting a track.
Read-only inventory found SDL2 library and ffplay path; neither was initialized/run.
NuGet core3.0.0 read-only HEAD returned200. No device availability inferred.
Primary [NLayer](https://github.com/naudio/NLayer), [package](https://www.nuget.org/packages/NLayer/3.0.0),
[SDL open](https://wiki.libsdl.org/SDL2/SDL_OpenAudioDevice),
[queue](https://wiki.libsdl.org/SDL2/SDL_QueueAudio), [close](https://wiki.libsdl.org/SDL2/SDL_CloseAudioDevice).

## Minimal connected design

1. `AudioService` remains real consumer API and playlist/cue path/throttle owner.
   Add typed immutable status/outcome (NotRequested/Muted/NoAssets/BackendUnavailable/
   DeviceUnavailable/Playing/Stopped/Canceled/Error/CleanupUncertain/Disposed/
   BrowserManaged), backend kind and concise safe Russian note. Existing public
   async method callers remain valid; add result-returning API or snapshot without
   exposing native exceptions/asset contents. Show capability note through existing
   console settings/main menu rendering, not background writes into player input.
2. Small `Services/Audio/` contracts: backend opens an owned session, session owns
   decoder/output, dynamic volume, run completion, stop and disposal. Thin Windows
   WaveOut and Linux SDL adapters; instance-only internal test seam, no global config.
   Default Linux native library loads/exports are lazy; no device init in constructor.
   Actual console Program finally awaits AudioService.DisposeAsync; GameEngine.RunAsync
   finally stops original operations even on initialization/scripted-input errors, with
   an inert real-engine exception-exit test. Browser factory is explicitly suppressed in real LocalWebUiHost DI: every playback
   request yields BrowserManaged, ApplySettings keeps existing committed settings
   settlement, zero backend/decoder/device calls. No frontend/schema changes needed.
3. Serialize music transitions/settings/disposal. Publish an original operation before
   asynchronous work, track every cue task/session, and stop admission during closing.
   Replace music only after previous operation actually finishes/disposes. StopAll
   cancels and awaits cues and music; DisposeAsync permanently closes admission.
   No native blocking cleanup/cancellation callback on caller/input thread; native
   work/finally stays with its original task. Wait bounded2s; timeout/error retains
   original task/session/CTS and reports CleanupUncertain, blocks replacement/new
   playback until actual settlement. Never claim Stopped from cancellation alone.
4. PCM is streamed in small blocks; bounded SDL queue about100ms plus one block,
   volume clamps0..1 and changes future queued samples without reopen/rewind.
   Session finalizer owns pause/clear/close/decoder disposal exactly once, with
   shared SDL initialization owned/refcounted across this backend's active sessions.
   No native callback into managed code. Natural completion continues original
   playlist selection/nonrepeat policy; unavailable/error exits instead of500ms
   failure retry storm. Explicit later context/settings request may try again after
   settled failure. Muting music stops it; sound disable/zero cancels owned cues;
   current preview accepted/disposed settings source remains unchanged.
5. Preserve cue filenames/fallbacks, MP3 music catalog/theme exclusions, throttle
   gaps120ms/80ms, profile values and all game/GM text/rules/model. Audio adds no
   mutation journal, canonical write or lease. Shared browser ApplySettings remains
   after Committed; rollback/cleanup debt cannot be relabelled by audio refresh.

## Causal bounded sequence and verification

- [x] Publish plan/task checkpoint; independent Sol6.1/xhigh design PASS at
  0394ffaeb79486bdc7c2eec41dcfcdd3ab2133e3 (blob c5d2d5b99c82909820002e280dc7735b65a4a3cb).
  Actual console finally and driver-asserted dummy seam are required; no device/physical claims.
- [ ] New narrow categories `console-audio-linux` and `browser-audio-isolation`;
  initial child fixture reaches real AudioService.PlayCue on synthetic WAV and
  real browser registered service. Child-only guard prevents any native output
  device call (legacy Windows and SDL bindings are denied/recorded before init by child-only
  DllImport resolvers; the same public constructor/factory is exercised on both sides). RED must observe the
  wrong Windows backend attempt/unreported capability and browser server-output
  attempt, not a missing constructor/compilation failure. Keep preparation separate.
- [x] Implement minimal contracts/service/adapters/DI, fresh same cases GREEN (2/2 at 756e2683),
  ordinary WIP push/readback before expanding matrix.
- [ ] Per-case owned test host/root and independent hard lifetime, no shared mutable
  SDL state across tests. Controlled backend/API adapters prove settings/volume,
  menu/game context, all cues/throttle, natural end, failure, concurrent replacement,
  cancellation, StopAll/Dispose and retained timeout debt/eventual settlement.
  Real BrowserAudioService.UpdateSettings + original coordinator/config receipt
  proves accepted values and no server output; original guards/held leases unchanged.
- [ ] Linux native SDL dummy-only child replaces the initialization seam itself with
  SDL_AudioInit("dummy"), asserts SDL_GetCurrentAudioDriver()=="dummy" before
  every open, and never subsequently calls normal SDL_InitSubSystem. This avoids
  SDL2 default reinitialization replacing the dummy driver. Native source: [SDL.c](https://github.com/libsdl-org/SDL/blob/SDL2/src/SDL.c),
  [SDL_audio.c](https://github.com/libsdl-org/SDL/blob/SDL2/src/audio/SDL_audio.c).
  never production default driver/device, no env/system setting override. Production
  queued backend and real synthetic PCM WAV/valid silent MPEG frames (ReadSamples must return >0) exercise decoding,
  queue/drain/volume/scoped close; negative controlled open0/absent library exercises
  explicit capability. Dummy is synthetic output, not physical device qualification.
  If dummy unavailable, record exact limit rather than touch a user device.
- [ ] Select only existing affected AudioPreviewUsesDraftThenRestoresAcceptedSettingsWhenDisposed,
  PortableBrowserSettings audio config/rollback/cleanup outcomes and precise changed
  console renderer/source guards. Adapt the old reflection-only CTS preview fixture
  if needed into actual controlled lifecycle. No unrelated clipboard/settings/full
  cohorts or frontend tests when frontend unchanged. Structural catalog update and
  ValidateCatalog discovery only. All test execution via scripts/test-csharp.ps1.
- [ ] Independent source/evidence/metadata PASS, exact source/command/count/cleanup/
  tool/package/native-library provenance, ordinary push/remote byte readback,
  fresh GitHub-only source restore, verified handoff and stop.

## Boundaries and decisions

No hardware/user device open or capture, audio server/package installation, system/
network/auth/security/config changes, live GM/CLI/browser/provider/real saves or
other block. Source library paths alone prove presence, not device or physical
sound. Native Windows and actual Linux desktop audio remain owner-run environments.
No new player-facing mode/setting/model/UX decision is proposed; SDL2 absence is
an explicit optional capability outcome. No full T050/systemd/live qualification.
Spec Kit uses existing feature/constitution and manual bounded artifact consistency;
installed project skills read, global specify not installed or initialized again.
