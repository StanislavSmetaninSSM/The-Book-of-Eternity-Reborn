"""Controlled ordinary idle daemon PTY, beneath the independent fixture guardian."""
import errno, fcntl, hashlib, json, os, pty, select, struct, subprocess, sys, termios, time
from pathlib import Path

folder = Path(sys.argv[1]); ship = folder / 'ship'; session = folder / 'root/game_session'
master, slave = pty.openpty()
fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack('HHHH', 25, 100, 0, 0))
initial = termios.tcgetattr(slave); started = time.monotonic(); marker_tail = b''
raw = (folder / 'idle-daemon-terminal.log').open('wb', buffering=0)
startup_deadline = started + 30; operation_deadline = None
result = {'SignalCount': 0, 'EOF': False}; process = None
def own():
    os.setsid(); fcntl.ioctl(slave, termios.TIOCSCTTY, 0); os.tcsetpgrp(slave, os.getpid())
try:
    prologue = ship / 'daemon-close-diagnostic.ps1'
    if prologue.exists():
        daemon = ship / 'BookOfEternityClient/game_master_daemon.ps1'
        original = daemon.read_bytes(); prefix = prologue.read_bytes()
        anchor = b'$ErrorActionPreference = "Stop"'
        assert original.count(anchor) == 1
        instrumented = original.replace(anchor, prefix + b'\n' + anchor, 1)
        daemon.write_bytes(instrumented)
        (folder / 'daemon-close-instrumentation.json').write_text(json.dumps({
            'DiagnosticOnly': True, 'OriginalSHA256': hashlib.sha256(original).hexdigest(),
            'PrologueSHA256': hashlib.sha256(prefix).hexdigest(),
            'InstrumentedSHA256': hashlib.sha256(instrumented).hexdigest(),
            'HelperUnmodifiedSHA256': hashlib.sha256((ship / 'BookOfEternityClient/Launcher/gm_main_operation.ps1').read_bytes()).hexdigest()
        }))
    argv = ['pwsh', '-NoLogo', '-NoProfile', '-File', str(ship / 'BookOfEternityClient/Launcher/bookofeternity.ps1'),
            'start-daemon', 'visible', '--timeout', '30', '--log', str(folder / 'idle-daemon.log'), '-SessionPath', str(session)]
    process = subprocess.Popen(argv, cwd=ship, stdin=slave, stdout=slave, stderr=slave, preexec_fn=own)
    result['OriginalPid'] = process.pid; result['Argv'] = argv
    result['SpawnAtSeconds'] = time.monotonic() - started
    result['StartupBudgetSeconds'] = 30; result['OperationBudgetSeconds'] = 20
    waiting = False; slave_closed = False
    while time.monotonic() < (operation_deadline if waiting else startup_deadline):
        if select.select([master], [], [], .001)[0]:
            try: chunk = os.read(master, 65536)
            except OSError as failure:
                if failure.errno != errno.EIO: raise
                chunk = b''
            if not chunk:
                result['EOF'] = True; result['EOFAtSeconds'] = time.monotonic() - started; break
            raw.write(chunk)
            # Only marker detection retains a rolling window; all raw bytes go to disk.
            marker_tail = marker_tail + chunk
            if 'WatchingAtSeconds' not in result and b'Watching:' in marker_tail:
                result['WatchingAtSeconds'] = time.monotonic() - started
            saw_waiting = b'Waiting for turns...' in marker_tail
            marker_tail = marker_tail[-64:]
        else:
            saw_waiting = False
        if not waiting and saw_waiting:
            waiting = True
            result['WaitingAtSeconds'] = time.monotonic() - started
            operation_deadline = time.monotonic() + 20
            (folder / 'daemon-waiting.json').write_text(json.dumps({'OriginalPid': process.pid}))
        if waiting and result['SignalCount'] == 0 and (folder / 'daemon-stop-request.json').exists():
            result['SignalCount'] = 1; result['SignalAtSeconds'] = time.monotonic() - started
            assert process.poll() is None
            assert os.write(master, b'\x03') == 1
        if process.poll() is not None and not slave_closed:
            os.close(slave); slave = -1; slave_closed = True
    if not result['EOF']: raise TimeoutError('Owned idle daemon operation/EOF bound' if waiting else 'Owned idle daemon startup/Waiting bound')
    result['ExitCode'] = process.wait(timeout=1)
    result['TermiosRestored'] = termios.tcgetattr(master) == initial
except Exception as failure:
    result['Failure'] = type(failure).__name__ + ': ' + str(failure)
finally:
    result['ElapsedSeconds'] = time.monotonic() - started
    raw.close()
    (folder / 'idle-daemon-terminal.json').write_text(json.dumps(result, indent=2))
    for fd in [master, slave]:
        if fd >= 0: os.close(fd)
sys.exit(0 if 'Failure' not in result and result['SignalCount'] == 1 and result['ExitCode'] == 0 and result['TermiosRestored'] else 1)
