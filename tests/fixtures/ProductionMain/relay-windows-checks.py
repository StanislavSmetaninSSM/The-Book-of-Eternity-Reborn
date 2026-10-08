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
    def modes():
        result = []
        for number in (-10, -11):
            value = wintypes.DWORD()
            assert kernel.GetConsoleMode(kernel.GetStdHandle(number & 0xffffffff), ctypes.byref(value))
            result.append(value.value)
        return result
    before = modes()
    finished = threading.Event()
    def stop_requested():
        while not finished.wait(.02):
            if (own / 'stop-relay').exists():
                signal.raise_signal(signal.SIGTERM)
                return
    watcher = threading.Thread(target=stop_requested, daemon=True)
    import relay_cli
    sys.argv = [str(tools / 'relay_cli.py'), '--session', str(own / 'session'),
                '--queue', str(own / 'queue'), '--model', 'inert-native-windows']
    watcher.start()
    try:
        result = relay_cli.main()
    finally:
        finished.set(); watcher.join(1)
        (own / 'console-modes.json').write_text(json.dumps({'Before': before, 'After': modes()}))
    sys.exit(result)
else:
    raise AssertionError(mode)
print(json.dumps({'Case': mode, 'Passed': True, 'ModelRequests': 0}))
