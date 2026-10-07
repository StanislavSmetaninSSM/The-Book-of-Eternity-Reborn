# T050-DESKTOP-HELPERS-PORTABLE implementation plan

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), owner's bounded request; accepted auxiliary base `65674ee83ddeac9c63e368334ea170cf9b700c54`.
Sole writer, existing isolated `codex/1553-load-filesystem`; actual independent Sol6.1/xhigh reviews. No new product decision is required.

## Source map and selected mechanism

- `Services/ImageService.cs:642–685`: actual viewer and gallery-folder methods; viewer missing/suppressed early returns; association errors only logged; gallery directory creation is outside catch. Existing image suffixes are png/jpg/jpeg/gif/bmp/webp.
- `Core/GameEngine/GameEngine.OptionsAndSettings.cs:740–825`: original settings mods-folder menu, original ConsoleSettingsSession reads/leases, existing key acknowledgement.
- `UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs:13–103`: original `/mods` folder branch, original guard/console pipeline. PrivateImplementation also has its existing folder helper used by guardian-library UI; retain this related helper through the same narrow opener, without touching profile/map launches.
- `Core/GameEngine/GameEngine.MainMenu.cs:2174–2195`: main-menu guardian-library folder helper; directory creation outside catch, failed association emits path and waits for input.
- `ConsoleAppearanceService.cs` plus options font preview: already declines non-classic host and displays font_size_apply_note; no direct defect in this fallback, leave unchanged.

Managed `Process.Start` with `UseShellExecute=true` is already portable and suitable. Keep it (including Windows associations) rather than adding shell commands, xdg-open wrappers, packages or desktop detection guesses. A nonthrowing request is not proof that a viewer displayed anything; even a null Process can be an accepted shell request. Do not wait/kill external viewer or claim positive desktop qualification.

## Minimal connected change

One small `DesktopPathOpener` with injectable `Action<ProcessStartInfo>` defaults to existing Process.Start; single literal target, UseShellExecute=true, no arguments, no shell construction/retry. Typed result: Requested, Missing, Unsupported, Unavailable, Failed, Cancelled, Suppressed. Requested means only association request returned; always supply manual path and conditional fallback text. Existing image settings/force flag preserved. Known image suffixes only; unknown files are not automatically launched. Folder creation belongs inside handled operation, errors retain manual target and never escape UI.

Inject the same narrow opener through optional constructor parameters in ImageService/GameEngine/ExplorerMode, preserving production/default construction and DI. Original consumers render escaped path and concise Russian outcome. Existing input acknowledgements stay on their original failure paths; no success pause or automatic retry. Cancellation stays explicit; no attempt after cancelled result. Do not change game rules, profile/model/CLI, state/leases, GM prompts or browser API. This is client-owned display only; no GM-authored contract changes.

## Causal sequence and verification

1. Publish plan and get independent design review before code/tests. Preserve original managed association mechanism with a controlled injection seam only; this baseline does not fix fallback semantics.
2. Tests reach actual ImageService methods, complete `/mods` Explorer command, original settings mods menu and actual main-menu folder method with isolated Unicode/spaced/markup-looking paths. Adapter records exact ProcessStartInfo and throws controlled association/cancel/error outcomes, never launches a process. Capture causal RED visible missing message/path and folder-creation error; do not call compile/preparation failure a RED.
3. Implement typed handled result and render it through those consumers. GREEN and incremental cases: requested vs displayed, association unavailable, generic error, cancellation, suppression/force, unsupported/missing image, folder blocker, Unicode/spaces, exact one request/manual retry only. Main/Explorer related folder helper covered. Source guard proves existing menu branches call tested helper and no shell/window fallback was introduced.
4. Narrow categories: `desktop-image-consumers`, `desktop-folder-consumers`, `desktop-launch-contracts`. No old audio/clipboard/M1/fence/storage cohorts: their contracts do not change; constructor compatibility is compiled. Catalog validation and PlanOnly; no frontend changes/tests. If a direct impacted previous regression is identified, add exact reason/selector, never whole cohorts.
5. Ordinary checkpoint push/remote SHA/byte readback before lengthy tests/reviews. Preserve exact runner/TRX/source pins/commands/counts and isolated own-root cleanup. Independent source/evidence/metadata review, final fresh GitHub-only source/byte restoration and bounded handoff. Mark only this task after evidence; full T050 and physical desktop qualification remain open.

## Process decisions and limits

Spec Kit constitution2.1, existing feature/spec/plan/tasks maintained rather than resetting setup-plan templates; consistency pass before implementation. Optional git hooks fulfilled by explicit ordinary checkpoints. User authorizes bounded implementation after independent review, so no repeated design approval question. Native Linux/Windows desktop positive launch is not executed: controlled adapter consumer wiring only. No live browser/GM/Codex/systemd/worker/real saves/devices, installs/settings/network/auth/privileges, merge/force push or next block.
