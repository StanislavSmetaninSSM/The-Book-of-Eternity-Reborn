"""Controlled ordinary idle daemon PTY, beneath the independent fixture guardian."""
import errno, fcntl, json, os, pty, select, struct, subprocess, sys, termios, time
from pathlib import Path

folder = Path(sys.argv[1]); ship = folder / 'ship'; session = folder / 'root/game_session'
master, slave = pty.openpty()
fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack('HHHH', 25, 100, 0, 0))
initial = termios.tcgetattr(slave); started = time.monotonic(); capture = bytearray()
result = {'SignalCount': 0, 'EOF': False}; process = None
def own():
    os.setsid(); fcntl.ioctl(slave, termios.TIOCSCTTY, 0); os.tcsetpgrp(slave, os.getpid())
try:
    argv = ['pwsh', '-NoLogo', '-NoProfile', '-File', str(ship / 'BookOfEternityClient/Launcher/bookofeternity.ps1'),
            'start-daemon', 'visible', '--timeout', '30', '--log', str(folder / 'idle-daemon.log'), '-SessionPath', str(session)]
    process = subprocess.Popen(argv, cwd=ship, stdin=slave, stdout=slave, stderr=slave, preexec_fn=own)
    result['OriginalPid'] = process.pid; result['Argv'] = argv
    waiting = False; slave_closed = False
    while time.monotonic() - started < 16:
        if select.select([master], [], [], .001)[0]:
            try: chunk = os.read(master, 65536)
            except OSError as failure:
                if failure.errno != errno.EIO: raise
                chunk = b''
            if not chunk:
                result['EOF'] = True; break
            capture.extend(chunk)
            if len(capture) > 262144: raise RuntimeError('Owned daemon output bound')
        if not waiting and b'Waiting for turns...' in capture:
            waiting = True
            (folder / 'daemon-waiting.json').write_text(json.dumps({'OriginalPid': process.pid}))
        if waiting and result['SignalCount'] == 0 and (folder / 'daemon-stop-request.json').exists():
            result['SignalCount'] = 1; result['SignalAtSeconds'] = time.monotonic() - started
            assert process.poll() is None
            assert os.write(master, b'\x03') == 1
        if process.poll() is not None and not slave_closed:
            os.close(slave); slave = -1; slave_closed = True
    if not result['EOF']: raise TimeoutError('Owned idle daemon exit/EOF bound')
    result['ExitCode'] = process.wait(timeout=1)
    result['TermiosRestored'] = termios.tcgetattr(master) == initial
except Exception as failure:
    result['Failure'] = type(failure).__name__ + ': ' + str(failure)
finally:
    result['ElapsedSeconds'] = time.monotonic() - started
    (folder / 'idle-daemon-terminal.log').write_bytes(capture)
    (folder / 'idle-daemon-terminal.json').write_text(json.dumps(result, indent=2))
    for fd in [master, slave]:
        if fd >= 0: os.close(fd)
sys.exit(0 if 'Failure' not in result and result['SignalCount'] == 1 and result['ExitCode'] == 0 and result['TermiosRestored'] else 1)
