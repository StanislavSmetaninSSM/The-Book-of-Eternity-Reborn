#!/usr/bin/env python3
"""Actual native helper terminal-pipe bootstrap qualification; no game/model."""
import argparse
import contextlib
import fcntl
import json
import os
import secrets
import select
import signal
import struct
import subprocess
import termios
import tempfile
import time
from probe import frame, getfd


def run(binary, mode):
    run_id = 'fd-' + secrets.token_hex(12)
    # Inert terminal payload; the actual native helper owns all execution/cleanup.
    temporary = tempfile.TemporaryDirectory(prefix='boe-native-fd-')
    marker = os.path.join(temporary.name, 'executed')
    payload = 'printf yes > "$1"; printf "READY:"; stty size; read answer; printf "REPLY:%s\\n" "$answer"'
    p = subprocess.Popen([binary, run_id, '100', '2500', '--terminal-pipe-v1',
                          '101', '31', '/bin/sh', '-c', payload, '--', marker],
                         stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    held = []
    statuses = []
    try:
        candidate = os.pidfd_open(p.pid)
        held.append(candidate)
        # Generated strictly AFTER candidate capture, on original launch channels.
        nonce = secrets.token_hex(32)
        challenge = ('H' + nonce + '\n').encode()
        if mode == 'fragmented':
            for chunk in (challenge[:5], challenge[5:19], challenge[19:]):
                p.stdin.write(chunk); p.stdin.flush(); time.sleep(.01)
        else:
            p.stdin.write(challenge); p.stdin.flush()
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
        assert not os.path.exists(marker)
        if mode in ('success', 'fragmented'):
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
        elif mode == 'kill-held':
            signal.pidfd_send_signal(rootfd, signal.SIGKILL)
        elif mode == 'duplicate-launch':
            p.stdin.write(b'L'); p.stdin.flush()
        elif mode == 'duplicate-release':
            p.stdin.write(b'AA'); p.stdin.flush()
        elif mode == 'owner-eof-held':
            p.stdin.close()
        elif mode == 'late-release':
            time.sleep(5.15)
            with contextlib.suppress(BrokenPipeError):
                p.stdin.write(b'A'); p.stdin.flush()
        elif mode == 'held-timeout':
            pass  # Native five-second held deadline must expire without release.
        elif mode == 'cancel-held':
            p.stdin.write(b'C'); p.stdin.flush()
        else:
            raise ValueError(mode)
        while not statuses or not statuses[-1].get('cleanupComplete'):
            statuses.append(frame(p.stdout.fileno(), timeout=7))
        terminal = statuses[-1]
        expected_uncertain = mode in ('duplicate-launch', 'held-timeout', 'late-release', 'owner-eof-held', 'duplicate-release')
        assert terminal['state'] == ('Uncertain' if expected_uncertain else 'StoppedWithinScope'), terminal
        assert terminal['authorityRetained'] is False
        assert p.wait(timeout=5) == (2 if expected_uncertain else 0)
        if mode != 'duplicate-release':
            assert os.path.exists(marker) == (mode in ('success', 'fragmented'))
        assert select.select([rootfd], [], [], 0)[0]
        assert p.stdout.read() == b''
        return dict(mode=mode, statuses=statuses, helperExit=p.returncode)
    finally:
        try:
            with contextlib.suppress(BrokenPipeError):
                p.stdin.close()
            if p.poll() is None:
                try:
                    p.wait(timeout=10)
                except subprocess.TimeoutExpired as e:
                    raise RuntimeError('owned cleanup unresolved; no emergency helper kill') from e
        finally:
            for fd in held:
                os.close(fd)
            p.stdout.close(); p.stderr.close()
            temporary.cleanup()


def reject_before_root(binary, mode):
    run_id = 'reject-' + secrets.token_hex(12)
    with tempfile.TemporaryDirectory(prefix='boe-native-fd-reject-') as directory:
        marker = os.path.join(directory, 'executed')
        p = subprocess.Popen([binary, run_id, '100', '2500', '--terminal-pipe-v1',
                              '80', '25', '/bin/sh', '-c', 'printf yes > "$1"', '--', marker],
                             stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        records = []
        try:
            commands = {'premature-launch': b'L', 'premature-release': b'A', 'malformed': b'H!',
                        'oversized': b'H' + b'a' * 65 + b'\n',
                        'cancel-before': b'C', 'cancel-partial': b'HabcC',
                        'duplicate-challenge': b'H' + b'a' * 64 + b'\nH',
                        'auth-timeout': b'Habc', 'owner-eof': b''}
            command = commands[mode]
            if mode == 'owner-eof':
                p.stdin.close()
            else:
                p.stdin.write(command); p.stdin.flush()
            while not records or not records[-1].get('cleanupComplete'):
                records.append(frame(p.stdout.fileno(), timeout=7))
            expected = 0 if mode.startswith('cancel-') else 2
            assert p.wait(timeout=5) == expected
            assert not os.path.exists(marker)
            assert not any(r.get('bootstrap') == 'held' or r.get('state') == 'Started' for r in records)
            assert records[-1]['state'] == ('StoppedWithinScope' if expected == 0 else 'Uncertain')
            assert records[-1]['rootExitCode'] == -1
            assert records[-1]['authorityRetained'] is False
            return dict(mode=mode, statuses=records, helperExit=p.returncode, payloadExecuted=False)
        finally:
            try:
                with contextlib.suppress(BrokenPipeError):
                    p.stdin.close()
                if p.poll() is None:
                    try:
                        p.wait(timeout=10)
                    except subprocess.TimeoutExpired as e:
                        raise RuntimeError('owned cleanup unresolved; no emergency helper kill') from e
            finally:
                p.stdout.close(); p.stderr.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('binary')
    args = parser.parse_args()
    binary = os.path.abspath(args.binary)
    results = [run(binary, mode) for mode in ('success', 'fragmented', 'cancel-held', 'kill-held', 'duplicate-launch', 'held-timeout', 'late-release', 'owner-eof-held', 'duplicate-release')]
    results += [reject_before_root(binary, mode) for mode in ('premature-launch', 'premature-release', 'malformed', 'oversized', 'cancel-before', 'cancel-partial', 'duplicate-challenge', 'auth-timeout', 'owner-eof')]
    print(json.dumps(results, indent=2))
