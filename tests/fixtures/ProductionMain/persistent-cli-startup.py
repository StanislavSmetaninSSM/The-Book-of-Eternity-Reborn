"""One zero-input persistent OpenCode startup via original M1; guardian required.

Arguments: fresh owned output directory, already-published shipped package.
No provider prompt, terminal-query response, confirmation or readiness override.
"""
import errno
import base64
import gzip
import re
import fcntl
import hashlib
import json
import os
from pathlib import Path
import pty
import select
import socket
import struct
import subprocess
import sys
import termios
import time
import uuid

out, ship = (Path(v).resolve() for v in sys.argv[1:3])
draft_mode = len(sys.argv) == 4 and sys.argv[3] == '--observe-draft'
assert len(sys.argv) == 3 or draft_mode
install = Path('/workspace/qualification-1553-opencode-install')
binary = install / 'package/node_modules/opencode-linux-x64-baseline/bin/opencode'
assert hashlib.sha256(binary.read_bytes()).hexdigest() == '77b2cfe4b97df6f15c3673b22100b9f79c711f25ecb9bf513bb82526b15d24fa'
assert os.environ.get('TERM') == 'dumb'
scratch, root = out / 'empty-cli-scratch', out / 'fixture-root'
assert not scratch.exists() and not root.exists()
scratch.mkdir(); session = root / 'game_session'; session.mkdir(parents=True)
pipe = 'oq1-' + uuid.uuid4().hex[:12]
quote = lambda v: "'" + str(v).replace("'", "''") + "'"
command = '& ' + quote(binary) + ' --mini --pure --no-replay -m opencode/ling-3.1-flash-free'
(session / 'config.json').write_text(json.dumps({
    'GmBridgeEnabled': True, 'GmBridgeBackend': 'OwnedTerminal', 'GmMainOwnerBackend': 'NativeLineage',
    'GmBridgeAutoStart': False, 'GmBridgePipeNameOverride': pipe, 'GmCliLaunchCommand': command,
    'GmBridgeShellWorkingDirectory': str(scratch), 'GmWorkerBridgeProfiles': [],
    'MusicEnabled': False, 'SoundEnabled': False,
    'GmCliInputProfile': {'IdleMarker': '', 'PromptPrefix': '', 'WorkingMarker': ''}
}, indent=2) + '\n')
env = {k: v for k, v in os.environ.items() if not k.upper().startswith('OPENCODE_')}
for name, child in [('XDG_CONFIG_HOME', 'config'), ('XDG_DATA_HOME', 'data'),
                    ('XDG_STATE_HOME', 'state'), ('XDG_CACHE_HOME', 'cache'), ('TMPDIR', 'tmp')]:
    directory = out / child; directory.mkdir(); env[name] = str(directory)
for name in ['OPENCODE_DISABLE_PROJECT_CONFIG', 'OPENCODE_DISABLE_AUTOUPDATE',
             'OPENCODE_DISABLE_EXTERNAL_SKILLS', 'OPENCODE_DISABLE_CLAUDE_CODE']:
    env[name] = '1'
env.update(NPM_CONFIG_USERCONFIG=str(install / 'empty-user.npmrc'),
           NPM_CONFIG_GLOBALCONFIG=str(install / 'empty-global.npmrc'),
           NPM_CONFIG_CACHE=str(out / 'npm-cache'), NPM_CONFIG_IGNORE_SCRIPTS='true')
draft = '\n'.join(f'Synthetic draft line {n:02}: Кириллица café; UpdateGuardians is draft text.' for n in range(60))
if draft_mode:
    observer_source = Path(__file__).with_name('read-only-draft-observer.py')
    observer = out / 'draft-observer'
    assert ' ' not in str(observer)
    observer.write_bytes(observer_source.read_bytes()); observer.chmod(0o700)
    env.update(VISUAL=str(observer), EDITOR=str(observer), BOE_DRAFT_PROBE_ROOT=str(out))
record_path = root / '.boe_runtime/gm-runs/main.json'
socket_path = str(out / 'tmp' / ('CoreFxPipe_' + pipe))
master, slave = pty.openpty()
fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack('HHHH', 25, 100, 0, 0))
initial = termios.tcgetattr(slave)
started = time.monotonic(); captured = bytearray(); eof = False; process = None
identity = None; running_identity = None; shutdown_attempts = 0; journal = []
result = {'ModelPromptsSent': 0, 'KeyboardBytes': 0, 'TerminalQueryAnswers': 0,
          'Confirmations': 0, 'ProfileIsSupported': False, 'ReadinessQualified': False,
          'ConfiguredCommand': command, 'ConfiguredModel': 'opencode/ling-3.1-flash-free',
          'InheritedTERM': os.environ['TERM'], 'NoNeutralPackage': True,
          'Scope': 'ordinary-same-namespace-lineage', 'LogicalLifecycleVerified': False}
result['DraftProbeMode'] = draft_mode
result['ManualRpcInputBytes'] = 0
draft_pasted = editor_requested = False

def elapsed():
    return time.monotonic() - started

def receive(delay=.02):
    global eof
    if not eof and select.select([master], [], [], delay)[0]:
        try:
            data = os.read(master, 65536)
        except OSError as ex:
            if ex.errno != errno.EIO: raise
            data = b''
        if not data: eof = True
        captured.extend(data)
        if len(captured) > 524288: raise RuntimeError('Bounded startup output exceeded512KiB')

def rpc(payload, seconds, expected_frame=None):
    assert payload['command'] in ['status', 'diagnostics', 'shutdown'] or (draft_mode and payload['command'] == 'addtext')
    entry = {'AtSeconds': round(elapsed(), 3), 'Request': payload, 'Sent': False}; journal.append(entry)
    with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as peer:
        peer.settimeout(.5); peer.connect(socket_path)
        if payload['command'] == 'addtext':
            assert expected_frame in ['startup', 'draft']
            check_manual_frame(expected_frame == 'draft')
            assert elapsed() < 12 and process.poll() is None, 'Never write after observation budget/lifetime'
        # Mark the one attempt before send; ambiguous partial send is never retried.
        entry['SendAttempted'] = True; peer.sendall(json.dumps(payload).encode() + b'\n'); entry['Sent'] = True
        peer.setblocking(False); reply = bytearray(); deadline = min(started + 25, time.monotonic() + seconds)
        while time.monotonic() < deadline:
            readable, _, _ = select.select([peer] + ([] if eof else [master]), [], [], .02)
            if master in readable: receive(0)
            if peer in readable:
                data = peer.recv(8192)
                if not data: raise EOFError('Original response lost; do not retry')
                reply.extend(data)
                if len(reply) > 524288: raise RuntimeError('RPC response exceeded bound')
                if reply.endswith(b'\n'):
                    response = json.loads(reply); entry['Response'] = response; return response
        raise TimeoutError('Original RPC outcome unknown; do not retry')

def own_foreground():
    os.setsid(); fcntl.ioctl(slave, termios.TIOCSCTTY, 0); os.tcsetpgrp(slave, os.getpid())

def current_diagnostic_frame():
    # A strict throwaway byte/frame gate; this never supplies TerminalScreen or Ready.
    # Drain bounded pending foreground output before examining the most recent commit.
    for _ in range(32):
        if not select.select([master], [], [], 0)[0]: break
        receive(0)
    else: raise RuntimeError('Foreground did not settle within the drain bound')
    raw = bytes(captured)
    begin = raw.rfind(b'\x1b[?2026h'); end = raw.rfind(b'\x1b[?2026l')
    assert begin >= 0 and end > begin and not raw[end + 8:], 'Current complete synchronized frame unavailable'
    return raw[begin + 8:end]

def expected_draft_frame(frame):
    # Require this fresh complete known synthetic composer shape, not historical labels.
    # Partial/unknown/question/permission frames refuse the diagnostic gesture.
    value = frame.decode('utf-8', errors='strict')
    cells = {}; row = column = 0; visible = False; i = 0
    while i < len(value):
        if value[i] == '\x1b':
            match = re.match(r'\x1b\[([0-9;? ]*)([A-Za-z])', value[i:])
            if match:
                params, code = match.groups(); i += len(match[0])
                if code == 'H':
                    numbers = [int(n or '1') for n in params.split(';')]
                    assert len(numbers) == 2 and 1 <= numbers[0] <= 25 and 1 <= numbers[1] <= 100
                    row, column = numbers[0] - 1, numbers[1] - 1
                elif code == 'm':
                    assert params in ['0', '1', '39', '49'] or re.fullmatch(r'(38|48);2;[0-9]{1,3};[0-9]{1,3};[0-9]{1,3}', params)
                    assert all(int(n) <= 255 for n in params.split(';'))
                elif code == 'K' and params in ['', '0', '2']:
                    for c in range(0 if params == '2' else column, 100): cells[row, c] = ' '
                elif code in ['h', 'l'] and params == '?25': visible = code == 'h'
                elif code == 'q' and params == '1 ': pass
                else: raise RuntimeError('Unknown/partial diagnostic frame; never send editor gesture')
                continue
            match = re.match(r'\x1b\]12;#[0-9a-fA-F]{6}\x07', value[i:])
            assert match, 'Unknown diagnostic control payload'
            i += len(match[0]); continue
        c = value[i]; i += 1
        if c == '\r': column = 0; continue
        if c == '\n': row += 1; assert row < 25; continue
        assert c >= ' ' and c != '\x7f' and column < 100
        # All fixture draft scalars have width one; no general Unicode/TUI claim.
        assert ord(c) < 0x300 or 0x400 <= ord(c) <= 0x4ff
        cells[row, column] = c; column += 1
    line = lambda r: ''.join(cells.get((r, c), '\0') for c in range(100)).rstrip(' ')
    assert all(line(r) == expected for r, expected in zip(range(5, 11), draft.split('\n')[-6:]))
    assert line(4) == '' and line(11) == ''
    assert line(12) == ' BUILD ' + ' ' * 82 + 'ctrl+p cmd'
    assert all(c == ' ' or r in [5, 6, 7, 8, 9, 10, 12] for (r, _), c in cells.items()), 'Unexpected pane text outside composer/footer'
    assert '\0' not in line(12) and visible
    assert row == 10 and column == len(draft.split('\n')[-1]), 'Actual composer focus absent'

def check_manual_frame(expect_draft):
    frame = current_diagnostic_frame()
    if expect_draft: expected_draft_frame(frame)
    else:
        known = gzip.decompress((Path(__file__).parents[3] / 'specs/1553-portable-local-storage/recovery/evidence/opencode-q1/startup/startup.raw.gz').read_bytes())
        expected = re.findall(rb'\x1b\[\?2026h(.*?)\x1b\[\?2026l', known, re.S)[-1]
        assert frame == expected, 'Current mini startup frame changed; never paste'
    assert elapsed() < 12 and process.poll() is None, 'Original lifetime or observation budget ended'

def before_manual_write(expect_draft):
    status = rpc({'command': 'status'}, 1)['status']
    assert status['terminalRunId'] == identity['runId'] and status['terminalOwnerRetained']
    assert not status['terminalUncertain'] and not status['ready']
    assert status['inputBindingId'] == result['OriginalRunningStatus']['status']['inputBindingId']
    check_manual_frame(expect_draft)

try:
    args = ['pwsh', '-NoLogo', '-NoProfile', '-File',
            str(ship / 'BookOfEternityClient/Launcher/bookofeternity.ps1'),
            'start-bridge', 'visible', '-SessionPath', str(session)]
    process = subprocess.Popen(args, cwd=ship, env=env, stdin=slave, stdout=slave, stderr=slave, preexec_fn=own_foreground)
    result['OriginalLauncherPid'] = process.pid
    # Latch original identity while its real retained owner acknowledges Running,
    # before observation can fail. Never reconstruct authority from later metadata.
    while elapsed() < 12:
        receive()
        if identity is None and record_path.exists() and Path(socket_path).exists():
            record = json.loads(record_path.read_bytes())
            if record['Disposition'] == 'Running':
                status = rpc({'command': 'status'}, 2)
                assert status['ok'] and status['status']['terminalOwnerRetained']
                assert status['status']['terminalRunId'] == record['Identity']['RunId']
                assert status['status']['cliLaunchCommand'] == command
                assert status['status']['shellWorkingDirectory'] == str(scratch)
                running_identity = record['Identity']; identity = {k[0].lower() + k[1:]: v for k, v in running_identity.items()}
                identity['backend'] = 2; result['OriginalRunningRecord'] = record; result['OriginalRunningStatus'] = status
        if process.poll() is not None: break
        if draft_mode and identity is not None and elapsed() >= 6:
            if not draft_pasted:
                before_manual_write(False)
                body = '\x1b[200~' + draft + '\x1b[201~'
                draft_pasted = True # mark before send: no ambiguous replay
                response = rpc({'command': 'addtext', 'text': body}, 1, 'startup')
                assert response['ok'] and not response['status']['ready']
                result['ManualRpcInputBytes'] += len(body.encode())
                result['PasteAcknowledgedAtSeconds'] = round(elapsed(), 3)
            elif not editor_requested and elapsed() >= result['PasteAcknowledgedAtSeconds'] + .8:
                before_manual_write(True)
                editor_requested = True # one standard editor gesture, no Enter
                response = rpc({'command': 'addtext', 'text': '\x18e'}, 1, 'draft')
                assert response['ok'] and not response['status']['ready']
                result['ManualRpcInputBytes'] += 2
                result['EditorGestureAcknowledgedAtSeconds'] = round(elapsed(), 3)
    assert identity is not None, 'No original Running acknowledgement; do not mint a replacement owner'
    result['StartupStatus'] = rpc({'command': 'status'}, 2)
    result['StartupDiagnostics'] = rpc({'command': 'diagnostics'}, 2)
    if draft_mode:
        receipt = json.loads((out / 'draft-observer.json').read_bytes())
        actual = base64.b64decode(receipt['ActualDraftBase64'], validate=True)
        assert actual == draft.encode(), 'Actual CLI-created draft differs; never submit'
        assert not Path(receipt['ActualArgvFile']).exists(), 'CLI has not completed its editor-file removal'
        assert captured.count(b'BOE_READ_ONLY_DRAFT_OBSERVED') == 1
        result['ActualDraftMatched'] = True
        result['ActualDraftBytes'] = len(actual)
        result['ActualDraftSHA256'] = hashlib.sha256(actual).hexdigest()
        result['CliRemovedEditorFile'] = True
        result['EditorObserverInvocations'] = 1
    result['ObservationEndedAtSeconds'] = round(elapsed(), 3)
except Exception as ex:
    result['ObservationFailure'] = type(ex).__name__ + ': ' + str(ex)
finally:
    # Cleanup precedes assertions and consumes only the latched original identity.
    # One attempted original shutdown even after early exit/observation failure.
    try:
        if identity is None: raise RuntimeError('Original stop identity unobserved; guardian cleanup cannot certify it')
        shutdown_attempts += 1
        stop = rpc({'command': 'shutdown', 'rootKey': identity['rootKey'], 'expectedMainIdentity': identity}, 12)
        result['ShutdownReceipt'] = stop
        assert stop['ok'], 'Original stop unconfirmed'
        proof = stop['status']['terminalStop']
        assert proof['identity']['runId'] == identity['runId'] and proof['state'] == 'stopped-within-scope'
        assert proof['cleanupComplete'] and not proof['authorityRetained']
        assert not stop['status']['terminalOwnerRetained'] and not stop['status']['terminalUncertain']
        while process.poll() is None and elapsed() < 25: receive()
        assert process.poll() == 0, 'Original launcher did not exit successfully'
        os.close(slave); slave = -1
        while not eof and elapsed() < 25: receive()
        record = json.loads(record_path.read_bytes()); result['StoppedRecord'] = record
        assert record['Disposition'] == 'Stopped' and record['Identity'] == running_identity
        assert eof, 'Foreground PTY not actually drained'
        result['LogicalLifecycleVerified'] = True
    except Exception as ex:
        result['CleanupFailure'] = type(ex).__name__ + ': ' + str(ex)
    result['ShutdownAttempts'] = shutdown_attempts
    result['LauncherExitCode'] = process.poll() if process is not None else None
    result['ForegroundPtyEOF'] = eof
    try: result['ForegroundTermiosRestored'] = termios.tcgetattr(master) == initial
    except OSError: result['ForegroundTermiosRestored'] = False
    for fd in [slave, master]:
        if fd >= 0: os.close(fd)
    result['ForegroundDescriptorsClosed'] = True
    result['Success'] = (result['LogicalLifecycleVerified'] and result['ForegroundTermiosRestored']
                         and 'ObservationFailure' not in result and 'CleanupFailure' not in result)
    result['ElapsedSeconds'] = round(elapsed(), 3); result['CapturedBytes'] = len(captured)
    (out / 'startup.raw').write_bytes(captured)
    (out / 'startup-readable.txt').write_text(captured.decode('utf-8', errors='replace').replace('\x1b', '<ESC>'))
    (out / 'rpc-journal.json').write_text(json.dumps(journal, indent=2) + '\n')
    (out / 'probe-result.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({k: result.get(k) for k in ['Success', 'ObservationFailure', 'CleanupFailure', 'CapturedBytes', 'ElapsedSeconds']}))
sys.exit(0 if result['Success'] else 1)
