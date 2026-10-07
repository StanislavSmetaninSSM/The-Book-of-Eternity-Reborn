# T050-CONSOLE-AUDIO-LINUX bounded handoff

Accepted base [499ca652](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/499ca652f367ec7dc5da5e85abb731793e5e47a5), runtime/tests **cf787a8f7873e83e7c02e96504ad8e7ff2d9870c**.
[Plan](console-audio-linux-plan.md), [checkpoint](console-audio-linux-checkpoint.md),
[qualification](recovery/console-audio-linux-qualification.json) contain exact source/artifact pins and commands.
Sole writer; Spec Kit existing #1553 feature/constitution, Superpowers inline TDD/debugging and bridge.

## Delivered behavior

The original [AudioService](../../BookOfEternityClient/Services/AudioService.cs) selects
Windows WaveOut or optional Linux SDL2. MP3 uses bundled managed [NLayer3.0.0](https://www.nuget.org/packages/NLayer/3.0.0);
PCM WAV uses existing NAudio managed decoding. There is no player startup compilation,
package installation, service setup or new setting. SDL2 absence and failed output
have distinct typed BackendUnavailable/DeviceUnavailable outcomes with a safe note
in existing main-menu/settings rendering. SDL2 is optional: the game remains usable silently.
No default-device capability is inferred from library presence.

Original music/cue sessions own decoder/output/run/finalization. Volume/settings drafts,
theme exclusions/nonrepeat, cue filenames/fallbacks and throttle gaps remain.
StopAll owns cues as well as music; actual GameEngine and Program finally paths clean up.
Two-second incomplete stop/disposal retains the original operation/session and CleanupUncertain,
blocks new playback, and allows retirement only after actual task/disposal completion.
Failed cleanup remains debt. No automatic failure retry storm or track replay for volume updates.

Actual [LocalWebUiHost](../../BookOfEternityClient/WebUi/LocalWebUiHost.cs) explicitly
registers BrowserManaged audio. Real BrowserAudioService config settlement still uses
the existing transaction/receipt and accepted-settings ApplySettings path, with zero
server decoder/output/device calls. HTML Audio/frontend/schema are unchanged; committed
cleanup debt and rollback remain separate. No canonical journal or game rules changed.
GM prompts/examples require no update: this is client-owned output lifecycle, not a
GM-authored capability; command/model/profile/economy/game text remain unchanged.

## Evidence and reviews

Fresh selected source cf787a8f: **27/27 PASS =20 new+7 precisely affected**,
console22/browser5, no skips/duplicates. Two narrow categories only:
`console-audio-linux`, `browser-audio-isolation`. All twenty isolated fixture
host/root cleanup receipts confirm completion, including parent timeout using only
retained original pidfd; the unchanged accepted .NET8.0.31 identity barrier is reused.
Physical/native-Windows acceptance is not implied by these controlled tests.

Coverage: actual public backend selection; all five cues/StopAll/throttle/dispose;
active-cue disable/zero volume; music context/nonrepeat/gain/mute/concurrent transitions;
original timeout and cleanup failure debt; actual engine refresh/exception-finally;
all main-menu layouts/settings note; real browser DI/config commit and exact existing
config rollback/committed cleanup warning; real ConsoleSettingsPreview unwind/restoration.
Native SDL uses only explicit dummy initialization, verifies driver==dummy before each
open, never normal initialization afterward. Synthetic WAV + valid silent MPEG frames
returned8192 decoded samples, real queue/drain and2opens/2closes. Controlled PCM gain
changed.25→.5 without reopening. Missing backend/device and queue/close failures are
synthetic controlled capability cases, not a probe of the real default output device.

History preserves2 original causal REDs (wrong Windows native binding) and4 review REDs
(three affected CTS-reflection fixtures, original-host timeout). Missing namespace
preparationFAIL0 and browser proof-path fixture failure are explicitly distinct.
Historical false cleanup receipt remains false; accepted owning runner cleanup confirms
settlement before removal of its synthetic root. Corrected final receipts are all true.
Catalog valid:383 categories/11088 methods,0 executed tests. Supplemental discovery after
context recovery is recorded separately and contributes no executed-test coverage.
SDK10.0.401/.NET8.0.31/PowerShell7.5.4; package/native bytes and SHA256 are pinned.
No native compiler/system audio package/audio service installation was used.

Independent actual Sol6.1/xhigh: designPASS0394ffae; sourcePASS cf787a8f after causal
review fixes; evidence/metadata PASS at b84ffe321c9c8a9ed879990d29c7c5cd1c702f5b.
Candidate fresh GitHub-only restore byte-compared20917 tracked files,27source and145artifact
pins, clean/fullfsck/no shallow history or alternates. [Saved proof](recovery/evidence/console-audio-linux-restoration/candidate-proof.json).
Final verdict carrier requires exact remote SHA/readback, fresh final-tip clone and
[restore verifier](recovery/console-audio-linux-restore-verifier.py) before final delivery.
The final proof is delivered separately with the final SHA, avoiding a self-referencing
commit. Restoration executes no tests, CLI, audio APIs or services.

## Remaining qualification

Actual Linux desktop/device/physical playback, native Windows WaveOut, live HTML Audio
codec/autoplay/device behavior, complete T050 and the rest of the accepted roadmap
remain unqualified. Mandatory primary systemd-user, Codex Q1/Q2, game launch/provider,
real saves and other blocks were not executed or expanded. No new product decision or
access requirement arose. Stop after verified metadata/remote/restoration closure.
