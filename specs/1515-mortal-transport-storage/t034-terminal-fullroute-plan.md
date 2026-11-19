# T034 bounded fullroute-only terminal integration plan

Status: independent design accepted; bounded Python integration SOURCE authorized, execution still gated.
Baseline qualification outcome: terminal-qualification-first/outcome.json,
SHA256 7823992885fd2abb9674e76dbd2eec228cf7b5b21947ab7360463df3193f8bb9.
Both independent oracle and core outcome reviews accepted bounded qualification.

## Scope and exact files

Only existing `SelectionPrompt/smoke.py` fullroute branch, `fullroute_inputs.py`,
its cheap DATA controls, and if necessary narrow terminal transport accessor tests.
Reuse `terminal/OwnedTerminal` and `terminal/pty_probe.PtyWriter`. No game/C# UI,
production live driver, default PIPE or other Screen mode changes. No second emulator,
no query stripping, no manually generated coordinates. Existing actual fullroute
receipt/state assertions and Back-only Load boundary remain unchanged.

## One output owner and one input owner

Before the actual fullroute child starts, initialize the pinned sidecar with the
same original `start+45` work deadline. Its initialization and disposal consume that
budget. Set owned master nonblocking and instantiate PtyWriter using master for
both data writes and termios/size queries (master remains valid after slave close).
Original/configured baseline capture and child-before-owner restore remain in smoke.
No second writer: both upstream replies and user keys traverse this exact writer.
Its audit records kind/seq/raw hex; route metadata additionally records raw cut and
stage. Original raw PTY capture retains every byte unchanged, including DSR.

`pump_fullroute` writes raw first, appends to captured, then calls `terminal.feed`.
Only after WRITTEN callback and validated response batch can replies be forwarded.
Keep acknowledged byte-prefix length, seq, modes, pending flag and cells snapshot.
Partial UTF8/control means no snapshot/key or fresh UI acceptance. Failure poisons
transport; no further input/replay. Sidecar stdout/raw/audit remains separate from
client raw and is retained on success/failure.

Before each user key, drain currently readable PTY data through that same pump,
then revalidate the exact intended UI predicate against the NEW acknowledged prefix.
If data arrived, previous selection/cut alone cannot authorize a key. Repeated known
selection is allowed only under existing exact transition rules; unknown/partial
state refuses/waits within the unchanged deadline. There is no atomic claim that
another child write cannot follow a snapshot. Any subsequent unexpected transition
still fails closed. No key is sent solely because select temporarily reports empty.

Derive Down/Up from the latest acknowledged applicationCursorKeysMode, yielding
SS3 vs CSI as appropriate; no hardcoded CSI. Save text/Enter remain literal existing
inputs. Replies bypass user-key pending checks only as upstream responses already
validated by their own admitted query and write callback, still using fresh physical
size/noecho/deadline check. No terminal-reply is counted as a route action.

## Exact observations, not a replacement generic UI engine

Spectre inventory/storage/picker/Load continues through public maintained raw-frame
decoder and exact descriptor/selected predecessor/profile. It sees original raw,
not sanitized text. Existing cuts between actual menus stay explicit. An unexpected
DSR inside a raw menu interval is refused, not removed to make its grammar pass.
Initial keyboard-mode pair is checked using acknowledged modes plus existing exact
raw prefix. No Screen.feed in fullroute; other modes retain it unchanged.

Custom Options uses a separate small source-specific cells predicate: latest fully
acknowledged 100x25 snapshot, exact ordered five labels, exact four descriptions,
one selected marker, title, Esc line and exact footer. Match padded cell lines,
not arbitrary substring membership. Initial entry requires a newer acknowledged
prefix than entry cut and complete title/grid/footer. Numeric selection requires the maintained options_frame original-raw predicate to prove a complete NEW grid/footer after key cut, together with the same acknowledged exact cells. Cursor/SGR-only newer output with old Save-selected cells cannot pass, including numeric1. A later complete snapshot with missing/reordered/extra grid
content refuses. It never infers cells by stripping CSI or replacing a DSR answer.
The static initial title plus custom grid may scroll; source-defined required rows
must actually be visible, otherwise report unsupported geometry rather than widen.

Save name/description/success prompts likewise use current acknowledged visible
cells plus a fresh prefix after the preceding action, never old Screen.plain.
Track the original raw Save interval separately. A source-specific outcome grammar
requires exactly one standalone success line, permitting only admitted edge decorations
and line separators, through the independently recognized returned inventory. Unknown
outcome text, internal controls (including inside warning phrases), duplicate success,
partial output or any follow-up warning refuse. No ANSI sanitizing or substring-absence
clean proof. Source OptionsAndSettings1222–1226 emits success/optional warning then
SILENT ReadKey; there is no printed ACK prompt. The one existing Enter acknowledges
that silent boundary. Join split raw reads unchanged, and recheck through complete
returned deposit inventory before any next move. No second Save/Load call or fake prompt.

## Cleanup and normal outcome

After managed success and original PTY EOF: verify child exit0/configured termios
BEFORE owner restoration, all existing managed receipt checks, then close sidecar
within original deadline and require normal CLOSE/join/drain. Only afterward can
fullroute success be true. Failure preserves primary error; abort exact sidecar and
actual child using existing bounded cleanup and record all failures. Original owner
termios restoration is separate and cannot repair child normal-baseline failure.
Sidecar abort or guardian emergency never becomes normal success. Existing guardian
55s / wrapper60s remain; work stays45s including sidecar. No budget reset at query,
menu, Save or cleanup. No new package acquisition during qualification.

## Source and bounded test gate

Before execution: complete source save and focused independent review. Add cheap
DATA using authored acknowledged-snapshot records: exact initial/selected Options
positive; missing/reordered/footer/extra selection/old seq/pending prefix negatives;
mode-based SS3 vs CSI; newly drained partial/changed frame prevents key; upstream
reply vs route-key audit; split Save warning through returned inventory. These are
DATA controls, not actual UI qualification. Keep existing raw-frame controls for
untouched grammar, selecting only relevant helpers if caller changes affect them.
Then reuse unchanged owning assemblies iff exact source/output join permits
(Python-only integration), PlanOnly exactly existing fullroute1, one bounded real
fullroute under original guards. Stop first unsupported control/query/render or
cleanup failure. No blind retry. Real fullroute success does NOT establish live
transport, GM grant, accepted world, archive Load commit or hosted replacement.

