#!/usr/bin/env python3
"""Actual native helper terminal-pipe bootstrap qualification; no game/model."""
import argparse
import fcntl
import json
import os
import secrets
import select
import signal
import struct
import subprocess
import termios
import time
from probe import frame, getfd


def run(binary, mode):
    run_id = 'fd-' + secrets.token_hex(12)
    # Inert terminal payload; the actual native helper owns all execution/cleanup.
    payload = 'printf "READY:"; stty size; read answer; printf "REPLY:%s\\n" "$answer"'
    p = subprocess.Popen([binary, run_id, '100', '2500', '--terminal-pipe-v1',
                          '101', '31', '/bin/sh', '-c', payload],
                         stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    held = []
    statuses = []
    try:
        candidate = os.pidfd_open(p.pid)
        held.append(candidate)
        # Generated strictly AFTER candidate capture, on original launch channels.
        nonce = secrets.token_hex(32)
        p.stdin.write(('H' + nonce + '\n').encode()); p.stdin.flush()
        auth = frame(p.stdout.fileno())
        assert auth == dict(bootstrap='authenticated', version=1, runId=run_id, nonce=nonce), auth
        assert p.poll() is None
        signal.pidfd_send_signal(candidate, 0)
        ready = frame(p.stdout.fileno()); statuses.append(ready)
        assert ready['state'] == 'Ready' and ready['runId'] == run_id
        p.stdin.write(b'L'); p.stdin.flush()
        offer = frame(p.stdout.fileno())
        assert set(offer) == {'bootstrap', 'version', 'runId', 'rootPidfd', 'terminalMaster'}
        assert offer['bootstrap'] == 'held' and offer['version'] == 1 and offer['runId'] == run_id
        rootfd = getfd(candidate, offer['rootPidfd']); held.append(rootfd)
        pty = getfd(candidate, offer['terminalMaster']); held.append(pty)
        signal.pidfd_send_signal(rootfd, 0)
        assert not select.select([rootfd], [], [], 0)[0]
        assert os.isatty(pty)
        fcntl.ioctl(pty, termios.TIOCSWINSZ, struct.pack('HHHH', 37, 113, 0, 0))
        if mode == 'success':
            p.stdin.write(b'A'); p.stdin.flush()
            started = frame(p.stdout.fileno()); statuses.append(started)
            assert started['state'] == 'Started', started
            text = b''; deadline = time.monotonic() + 3
            while b'READY:37 113' not in text:
                remaining = deadline - time.monotonic()
                assert remaining > 0 and select.select([pty], [], [], remaining)[0]
                text += os.read(pty, 1024)
                assert len(text) < 4096
            os.write(pty, b'hello\n')
            deadline = time.monotonic() + 3
            while b'REPLY:hello' not in text:
                remaining = deadline - time.monotonic()
                assert remaining > 0 and select.select([pty], [], [], remaining)[0]
                text += os.read(pty, 1024)
                assert len(text) < 4096
        elif mode == 'cancel-held':
            p.stdin.write(b'C'); p.stdin.flush()
        else:
            raise ValueError(mode)
        while not statuses or not statuses[-1].get('cleanupComplete'):
            statuses.append(frame(p.stdout.fileno()))
        terminal = statuses[-1]
        assert terminal['state'] == 'StoppedWithinScope', terminal
        assert terminal['authorityRetained'] is False
        assert p.wait(timeout=5) == 0
        assert select.select([rootfd], [], [], 0)[0]
        assert p.stdout.read() == b''
        return dict(mode=mode, statuses=statuses, helperExit=p.returncode)
    finally:
        if p.poll() is None:
            p.stdin.close()
            # Original helper must seal on owner EOF and reap its descendants.
            p.wait(timeout=10)
        for fd in held:
            os.close(fd)
        p.stdin.close(); p.stdout.close(); p.stderr.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('binary')
    args = parser.parse_args()
    print(json.dumps([run(os.path.abspath(args.binary), mode) for mode in ('success', 'cancel-held')], indent=2))
