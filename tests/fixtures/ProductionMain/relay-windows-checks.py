"""Native Windows relay tests only. No model/network calls or user data."""
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import threading
import time

assert os.name == 'nt', 'This fixture requires native Windows, not a no-op'
mode, own = sys.argv[1], Path(sys.argv[2])
if mode == 'console':
    import traceback
    (own / 'console-started').write_text('started')
    def report_exception(kind, value, trace):
        (own / 'console-failure.txt').write_text(''.join(traceback.format_exception(kind, value, trace)), encoding='utf8')
        sys.__excepthook__(kind, value, trace)
    sys.excepthook = report_exception
tools = Path(__file__).resolve().parents[3] / 'tools/gm-relay'
sys.path.insert(0, str(tools))


def command(case):
    return [sys.executable, '-B', __file__, case, str(own)]


def run(case):
    return subprocess.run(command(case), capture_output=True, timeout=5)


if mode == 'help':
    result = subprocess.run([sys.executable, '-B', str(tools / 'relay_cli.py'), '--help'],
                            capture_output=True, timeout=5)
    assert result.returncode == 0, result.stderr.decode()
    assert b'--session' in result.stdout and b'--queue' in result.stdout
elif mode == 'lock-holder':
    import relay_contract as c
    with c.execution_gate(own / 'queue'):
        print('LOCKED', flush=True)
        assert sys.stdin.readline() == 'release\n'
elif mode == 'close-attempt':
    import relay_contract as c
    try:
        c.request_close(own / 'queue')
    except c.RelayGateUnavailable:
        sys.exit(23)
elif mode == 'gate':
    queue = own / 'queue'
    queue.mkdir()
    holder = subprocess.Popen(command('lock-holder'), stdin=subprocess.PIPE,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    try:
        # communicate with a separate actual process, not a mocked/thread lock.
        lines = []
        ready = threading.Thread(target=lambda: lines.append(holder.stdout.readline()), daemon=True)
        ready.start(); ready.join(3)
        assert not ready.is_alive() and lines == [b'LOCKED\r\n'], (lines, holder.poll())
        before = time.monotonic()
        attempt = run('close-attempt')
        assert attempt.returncode == 23, (attempt.returncode, attempt.stderr)
        assert .8 <= time.monotonic() - before < 4
        assert not (queue / 'close-request.json').exists()
        assert not (queue / 'closed.json').exists()
        holder.communicate(b'release\n', timeout=3)
        assert holder.returncode == 0
        assert run('close-attempt').returncode == 0
        assert (queue / 'close-request.json').exists()
        assert not (queue / 'closed.json').exists()
        assert (queue / '.execution.lock').exists()
    finally:
        if holder.poll() is None:
            holder.kill(); holder.wait(timeout=3)
        holder.stdin.close(); holder.stdout.close(); holder.stderr.close()
elif mode == 'pipe':
    from relay_platform import poll_child_output
    payload = 'child Ж🙂\n'.encode() * 10000
    script = "import sys; sys.stdin.buffer.read(1); sys.stdout.buffer.write(('child Ж🙂\\n'.encode())*10000); sys.stdout.buffer.flush()"
    child = subprocess.Popen([sys.executable, '-c', script], stdin=subprocess.PIPE,
                             stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    try:
        assert poll_child_output(child.stdout) is None, 'Quiet live pipe is not EOF'
        child.stdin.write(b'x'); child.stdin.flush(); child.stdin.close()
        output = bytearray(); eof = False; deadline = time.monotonic() + 5
        while not eof and time.monotonic() < deadline:
            part = poll_child_output(child.stdout)
            if part is None:
                time.sleep(.01)
            elif part:
                output.extend(part)
            else:
                eof = True
        assert eof and bytes(output) == payload, (eof, len(output), len(payload))
        assert child.wait(timeout=2) == 0
    finally:
        if child.poll() is None:
            child.kill(); child.wait(timeout=3)
        child.stdout.close()
elif mode == 'console':
    # Keep observation outside product code. A local test barrier requests a
    # normal signal-handler exit even after the relay enters its closed state.
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.GetStdHandle.argtypes = [wintypes.DWORD]
    kernel.GetStdHandle.restype = wintypes.HANDLE
    kernel.GetConsoleMode.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    kernel.GetConsoleMode.restype = wintypes.BOOL
    kernel.SetStdHandle.argtypes = [wintypes.DWORD, wintypes.HANDLE]
    kernel.SetStdHandle.restype = wintypes.BOOL
    import msvcrt
    original_streams = (sys.stdin, sys.stdout)
    original_handles = [kernel.GetStdHandle(n & 0xffffffff) for n in (-10, -11)]
    prepared = []
    # Explicit component-fixture setup: these are the already-attached original
    # ConPTY console devices, never AllocConsole or a substitute terminal. The
    # untouched ordinary Bridge/outer-PTY route is verified separately.
    for device, access in [('CONIN$', 'r'), ('CONOUT$', 'w')]:
        prepared.append(open(device, access, encoding='utf8'))
    sys.stdin, sys.stdout = prepared
    for number, stream in zip((-10, -11), prepared):
        assert kernel.SetStdHandle(number & 0xffffffff, msvcrt.get_osfhandle(stream.fileno()))
    def modes():
        result = []
        observations = []
        for number in (-10, -11):
            value = wintypes.DWORD()
            handle = kernel.GetStdHandle(number & 0xffffffff)
            ok = kernel.GetConsoleMode(handle, ctypes.byref(value))
            observations.append(dict(Which=number, Handle=handle, Ok=bool(ok), Error=ctypes.get_last_error(), Mode=value.value))
            result.append(value.value)
        for stream in prepared:
            value = wintypes.DWORD()
            assert kernel.GetConsoleMode(msvcrt.get_osfhandle(stream.fileno()), ctypes.byref(value))
            result.append(value.value)
        (own / 'console-handles.json').write_text(json.dumps(observations))
        assert all(item['Ok'] for item in observations), observations
        return result
    finished = threading.Event()
    def stop_requested():
        while not finished.wait(.02):
            if (own / 'stop-relay').exists():
                signal.raise_signal(signal.SIGTERM)
                return
    watcher = threading.Thread(target=stop_requested, daemon=True)
    try:
        before = modes()
        import relay_cli
        sys.argv = [str(tools / 'relay_cli.py'), '--session', str(own / 'session'),
                    '--queue', str(own / 'queue'), '--model', 'inert-native-windows']
        watcher.start()
        try:
            result = relay_cli.main()
        finally:
            finished.set(); watcher.join(1)
            (own / 'console-modes.json').write_text(json.dumps({'Setup': 'ExplicitAttachedConsoleStreams', 'Before': before, 'After': modes()}))
    finally:
        sys.stdin, sys.stdout = original_streams
        for number, handle in zip((-10, -11), original_handles):
            assert kernel.SetStdHandle(number & 0xffffffff, handle)
        for stream in prepared:
            stream.close()
    sys.exit(result)
else:
    raise AssertionError(mode)
print(json.dumps({'Case': mode, 'Passed': True, 'ModelRequests': 0}))
