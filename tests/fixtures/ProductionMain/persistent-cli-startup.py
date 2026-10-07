"""One zero-input persistent OpenCode startup via original M1; guardian required.

Arguments: fresh owned output directory, already-published shipped package.
No provider prompt, terminal-query response, confirmation or readiness override.
"""
import errno
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

def rpc(payload, seconds):
    assert payload['command'] in ['status', 'diagnostics', 'shutdown']
    entry = {'AtSeconds': round(elapsed(), 3), 'Request': payload, 'Sent': False}; journal.append(entry)
    with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as peer:
        peer.settimeout(.5); peer.connect(socket_path)
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
    assert identity is not None, 'No original Running acknowledgement; do not mint a replacement owner'
    result['StartupStatus'] = rpc({'command': 'status'}, 2)
    result['StartupDiagnostics'] = rpc({'command': 'diagnostics'}, 2)
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
    result['Success'] = result['LogicalLifecycleVerified'] and result['ForegroundTermiosRestored']
    result['ElapsedSeconds'] = round(elapsed(), 3); result['CapturedBytes'] = len(captured)
    (out / 'startup.raw').write_bytes(captured)
    (out / 'startup-readable.txt').write_text(captured.decode('utf-8', errors='replace').replace('\x1b', '<ESC>'))
    (out / 'rpc-journal.json').write_text(json.dumps(journal, indent=2) + '\n')
    (out / 'probe-result.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({k: result.get(k) for k in ['Success', 'ObservationFailure', 'CleanupFailure', 'CapturedBytes', 'ElapsedSeconds']}))
sys.exit(0 if result['Success'] else 1)
