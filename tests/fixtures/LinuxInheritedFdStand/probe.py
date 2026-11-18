#!/usr/bin/env python3
"""Isolated Linux primitive stand. NOT a production launcher or authority API."""
import ctypes
import errno
import fcntl
import json
import os
import platform
import select
import signal
import struct
import termios
import time


def frame(fd, timeout=5):
    data = bytearray()
    deadline = time.monotonic() + timeout
    while len(data) < 1024:
        remaining = deadline - time.monotonic()
        if remaining <= 0 or not select.select([fd], [], [], remaining)[0]:
            raise TimeoutError('bounded frame')
        ch = os.read(fd, 1)
        if not ch:
            raise EOFError('frame lost')
        if ch == b'\n':
            return json.loads(data)
        data.extend(ch)
    raise ValueError('oversized frame')


def send(fd, value):
    data = json.dumps(value).encode() + b'\n'
    assert len(data) < 1024
    assert os.write(fd, data) == len(data)


def getfd(owner, locator, inject=None):
    if inject is not None:
        raise OSError(inject, 'injected transfer denial')
    # Explicit supported ABI, never guess syscall numbers on another architecture.
    if platform.machine() not in ('x86_64', 'aarch64'):
        raise OSError(errno.ENOSYS, 'stand ABI unsupported')
    libc = ctypes.CDLL(None, use_errno=True)
    result = libc.syscall(438, owner, locator, 0)
    if result < 0:
        e = ctypes.get_errno()
        raise OSError(e, os.strerror(e))
    return result


def helper(control, status, mode):
    root = None
    rootfd = None
    handles = []
    def expired(*_):
        raise TimeoutError('helper deadline')
    signal.signal(signal.SIGALRM, expired)
    signal.alarm(9)
    try:
        master, slave = os.openpty()
        gate, release = os.pipe()
        handles.extend([master, slave, gate, release])
        root = os.fork()
        if root == 0:
            try:
                os.close(master)
                os.close(release)
                os.close(control)
                os.close(status)
                signal.signal(signal.SIGALRM, signal.SIG_DFL)
                signal.alarm(7)
                if os.read(gate, 1) != b'L':
                    os._exit(125)
                os.close(gate)
                if mode == "rootfail":
                    raise RuntimeError("injected pre-setsid child failure")
                os.setsid()
                fcntl.ioctl(slave, termios.TIOCSCTTY, 0)
                for fd in range(3):
                    os.dup2(slave, fd)
                if slave > 2:
                    os.close(slave)
                rows, cols, _, _ = struct.unpack('HHHH', fcntl.ioctl(0, termios.TIOCGWINSZ, bytes(8)))
                os.write(1, f'ROOT tty={int(os.isatty(0))} rows={rows} cols={cols}\n'.encode())
                incoming = os.read(0, 80)
                os.write(1, b'REPLY:' + incoming)
                os._exit(0 if incoming == b'hello\n' else 2)
            except BaseException:
                os._exit(126)
        os.close(slave); handles.remove(slave)
        os.close(gate); handles.remove(gate)
        rootfd = os.pidfd_open(root)
        handles.append(rootfd)
        send(status, dict(phase='held', root=root, pidfd=rootfd, pty=master))
        decision = frame(control)
        if decision == {'release': True}:
            os.write(release, b'L')
        elif decision != {'release': False}:
            raise ValueError('invalid decision')
        os.close(release); handles.remove(release)
        _, result = os.waitpid(root, 0)
        root = None
        try:
            os.waitpid(-1, os.WNOHANG)
            raise AssertionError('unexpected retained child')
        except ChildProcessError:
            pass
        send(status, dict(phase='reaped', exit=os.waitstatus_to_exitcode(result), echild=True))
        return 0
    except BaseException as e:
        signal.alarm(0)
        if root is not None and root > 0:
            if rootfd is not None:
                try:
                    signal.pidfd_send_signal(rootfd, signal.SIGKILL)
                except ProcessLookupError:
                    pass
            else:
                # No wait has occurred before rootfd acquisition.
                os.kill(root, signal.SIGKILL)
            try:
                os.waitpid(root, 0)
            except ChildProcessError:
                pass
        send(status, dict(phase='failure', error=type(e).__name__, rootReaped=True))
        return 1
    finally:
        for fd in handles:
            os.close(fd)
        signal.alarm(0)


def run(mode):
    started = time.monotonic()
    cr, cw = os.pipe()
    sr, sw = os.pipe()
    child = os.fork()
    if child == 0:
        os.close(cw); os.close(sr)
        os._exit(helper(cr, sw, mode))
    os.close(cr); os.close(sw)
    handles = [cw, sr]
    reaped = False
    try:
        if mode == 'ownerfail':
            raise OSError(errno.EPERM, 'injected original helper pidfd denial')
        owner = os.pidfd_open(child)  # This caller alone reaps its own child.
        handles.append(owner)
        held = frame(sr)
        assert held['phase'] == 'held'
        error = None
        try:
            inject = {'eperm': errno.EPERM, 'enosys': errno.ENOSYS}.get(mode)
            locator = -1 if mode == 'badfd' else held['pty'] if mode == 'wrongtype' else held['pidfd']
            actual = getfd(owner, locator, inject)
            handles.append(actual)
            pty = getfd(owner, held['pty'])
            handles.append(pty)
            assert fcntl.fcntl(actual, fcntl.F_GETFD) & fcntl.FD_CLOEXEC
            with open(f'/proc/self/fdinfo/{actual}') as file:
                info = file.read()
            if f'Pid:\t{held["root"]}\n' not in info:
                raise ValueError('transferred descriptor is not the held root pidfd')
            signal.pidfd_send_signal(actual, 0)
            assert not select.select([actual], [], [], 0)[0]
            assert os.isatty(pty)
            fcntl.ioctl(pty, termios.TIOCSWINSZ, struct.pack('HHHH', 31, 101, 0, 0))
        except OSError as e:
            error = e.errno
        except ValueError:
            error = 'invalid-identity'
        send(cw, dict(release=error is None))
        text = b''
        if error is None and mode != 'rootfail':
            assert mode == 'success'
            deadline = time.monotonic() + 3
            while b'\n' not in text:
                remaining = deadline - time.monotonic()
                if remaining <= 0 or not select.select([pty], [], [], remaining)[0]:
                    raise TimeoutError('PTY line')
                text += os.read(pty, 1024)
                assert len(text) <= 1024
            assert b'ROOT tty=1 rows=31 cols=101' in text
            os.write(pty, b'hello\n')
        outcome = frame(sr)
        if error is None and mode == 'rootfail':
            assert outcome == dict(phase='reaped', exit=126, echild=True)
        elif error is None:
            while select.select([pty], [], [], 0)[0]:
                try:
                    chunk = os.read(pty, 1024)
                except OSError as e:
                    if e.errno == errno.EIO:
                        break
                    raise
                if not chunk:
                    break
                text += chunk
            assert b'REPLY:hello' in text
            assert outcome == dict(phase='reaped', exit=0, echild=True)
            assert select.select([actual], [], [], 0)[0]
        else:
            assert outcome == dict(phase='reaped', exit=125, echild=True)
            expected = {'eperm': errno.EPERM, 'enosys': errno.ENOSYS, 'badfd': errno.EBADF, 'wrongtype': 'invalid-identity'}.get(mode)
            if expected is not None:
                assert error == expected
        _, status = os.waitpid(child, 0)
        reaped = True
        assert os.waitstatus_to_exitcode(status) == 0
        return dict(mode=mode, transfer=error is None, errno=error,
                    refusalInjected=mode in ('eperm', 'enosys'), root=outcome,
                    helperReaped=True, seconds=round(time.monotonic()-started, 4))
    except OSError as e:
        if mode != 'ownerfail' or e.errno != errno.EPERM:
            raise
        os.close(cw); handles.remove(cw)
        _, status = os.waitpid(child, 0)
        reaped = True
        assert os.waitstatus_to_exitcode(status) == 1
        assert frame(sr)['phase'] == 'held'
        assert frame(sr) == dict(phase='failure', error='EOFError', rootReaped=True)
        return dict(mode=mode, refusalInjected=True, errno=e.errno, helperReaped=True, rootReaped=True)
    finally:
        if not reaped:
            os.close(cw); handles.remove(cw)
            # Helper's own deadline and cleanup remain active; do not orphan its root.
            os.waitpid(child, 0)
        for fd in handles:
            os.close(fd)


if __name__ == '__main__':
    result = [run(mode) for mode in ('success', 'badfd', 'wrongtype', 'eperm', 'enosys', 'ownerfail', 'rootfail')]
    print(json.dumps(dict(kernel=platform.release(), machine=platform.machine(), cases=result), indent=2))
