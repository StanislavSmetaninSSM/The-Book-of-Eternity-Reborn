"""#1589 inert foreground diagnostic. Missing Ctrl-C finally markers are observations, not success criteria."""
import errno, fcntl, json, os, pathlib, pty, select, struct, subprocess, sys, termios, time
fixture, root, shape, stop = pathlib.Path(__file__).parent, pathlib.Path(sys.argv[1]), sys.argv[2], sys.argv[3]
assert shape in ('direct', 'nested') and stop in ('cooperative', 'ctrl-c')
root.mkdir(parents=True, exist_ok=False)
master, slave = pty.openpty()
fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack('HHHH', 25, 100, 0, 0))
initial = termios.tcgetattr(slave)
started = time.monotonic(); process = None; tail = b''
result = {'Shape': shape, 'Stop': stop, 'InertOnly': True, 'Ready': False, 'StopSent': False, 'EOF': False}
def own():
    os.setsid(); fcntl.ioctl(slave, termios.TIOCSCTTY, 0); os.tcsetpgrp(slave, os.getpid())
try:
    args = ['pwsh', '-NoLogo', '-NoProfile', '-File', str(fixture / ('child.ps1' if shape == 'direct' else 'launcher.ps1')), '-Evidence', str(root)]
    result['Argv'] = args
    process = subprocess.Popen(args, stdin=slave, stdout=slave, stderr=slave, preexec_fn=own)
    result['OriginalPid'] = process.pid
    with (root / 'terminal.raw').open('wb', buffering=0) as raw:
        while time.monotonic() - started < 16:
            if select.select([master], [], [], .01)[0]:
                try: data = os.read(master, 65536)
                except OSError as error:
                    if error.errno != errno.EIO: raise
                    data = b''
                if not data:
                    result['EOF'] = True; break
                raw.write(data); tail += data
                if not result['Ready'] and b'FINALLY_CHILD_READY' in tail:
                    result['Ready'] = True
                    result['ReadySeconds'] = time.monotonic() - started
                tail = tail[-64:]
            if result['Ready'] and not result['StopSent']:
                assert process.poll() is None
                if stop == 'ctrl-c':
                    assert os.write(master, b'\x03') == 1
                else:
                    (root / 'cooperative-exit').write_text('owned fixture request')
                result['StopSent'] = True
                result['StopSeconds'] = time.monotonic() - started
            if process.poll() is not None and slave >= 0:
                os.close(slave); slave = -1
        if not result['EOF']: raise TimeoutError('Owned foreground diagnostic EOF bound')
    result['ExitCode'] = process.wait(timeout=1)
    result['TermiosRestored'] = termios.tcgetattr(master) == initial
    assert result['Ready'] and result['StopSent'] and result['TermiosRestored']
    if stop == 'cooperative':
        markers = (root / 'markers.log').read_text().splitlines()
        assert result['ExitCode'] == 0 and all('child.' + marker in markers for marker in ('try', 'cooperative-seen', 'body.exit', 'finally.enter', 'finally.after-write', 'finally.exit'))
        if shape == 'nested':
            assert all('launcher.' + marker in markers for marker in ('before', 'after', 'finally.enter', 'finally.exit'))
    result['Success'] = True
except Exception as error:
    result['Failure'] = type(error).__name__ + ': ' + str(error)
finally:
    # Independent guardian owns remaining descendants on any failed deadline.
    # No PID discovery, foreign signal or interpretation of missing markers as closure.
    result['ElapsedSeconds'] = time.monotonic() - started
    result['Markers'] = (root / 'markers.log').read_text().splitlines() if (root / 'markers.log').exists() else []
    (root / 'result.json').write_text(json.dumps(result, indent=2))
    for fd in (master, slave):
        if fd >= 0: os.close(fd)
sys.exit(0 if result.get('Success') else 1)
