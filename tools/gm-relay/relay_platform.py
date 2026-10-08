"""Native terminal/pipe I/O only; no queue, model or process ownership authority."""
import codecs
import os
import queue
import threading
import time


class _PosixTerminal:
    def __init__(self, input_fd, output_fd):
        import termios
        import tty
        self.input_fd, self.output_fd = input_fd, output_fd
        self.initial = termios.tcgetattr(input_fd)
        try:
            tty.setraw(input_fd)
        except BaseException:
            self.close()
            raise

    def read(self, timeout):
        import select
        if select.select([self.input_fd], [], [], timeout)[0]:
            return os.read(self.input_fd, 16384)
        return None

    def write(self, data):
        while data:
            count = os.write(self.output_fd, data)
            if not count:
                raise OSError('Terminal output made no progress')
            data = data[count:]

    def close(self):
        import termios
        termios.tcsetattr(self.input_fd, termios.TCSANOW, self.initial)


if os.name == 'nt':
    import ctypes
    from ctypes import wintypes
    import msvcrt

    _kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    _kernel.GetConsoleMode.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    _kernel.GetConsoleMode.restype = wintypes.BOOL
    _kernel.SetConsoleMode.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    _kernel.SetConsoleMode.restype = wintypes.BOOL
    _kernel.WriteConsoleW.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                                     ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
    _kernel.WriteConsoleW.restype = wintypes.BOOL
    _kernel.PeekNamedPipe.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                                    ctypes.c_void_p, ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
    _kernel.PeekNamedPipe.restype = wintypes.BOOL
    _kernel.ReadConsoleW.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                                    ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
    _kernel.ReadConsoleW.restype = wintypes.BOOL
    _kernel.OpenThread.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    _kernel.OpenThread.restype = wintypes.HANDLE
    _kernel.CancelSynchronousIo.argtypes = [wintypes.HANDLE]
    _kernel.CancelSynchronousIo.restype = wintypes.BOOL
    _kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    _kernel.CloseHandle.restype = wintypes.BOOL


class _WindowsTerminal:
    def __init__(self, input_fd, output_fd):
        self.input = msvcrt.get_osfhandle(input_fd)
        self.output = msvcrt.get_osfhandle(output_fd)
        self.initial = []
        # Read both original modes before any change; redirected handles refuse.
        for handle in (self.input, self.output):
            mode = wintypes.DWORD()
            if not _kernel.GetConsoleMode(handle, ctypes.byref(mode)):
                raise ctypes.WinError(ctypes.get_last_error())
            self.initial.append(mode.value)
        self.events = queue.Queue(maxsize=2)
        self.stopping = threading.Event()
        self.reader_start = threading.Event()
        self.reader = None
        self.reader_handle = None
        try:
            # VT input, extended flags, Ctrl+C signal; no line/echo/QuickEdit.
            self._set(self.input, (self.initial[0] | 0x0200 | 0x0080 | 0x0001) & ~(0x0002 | 0x0004 | 0x0040))
            self._set(self.output, self.initial[1] | 0x0001 | 0x0004)
            self.reader = threading.Thread(target=self._read_console, name='relay-console-input')
            self.reader.start()
            # Retain the actual thread handle, never reopen by a later thread ID.
            self.reader_handle = _kernel.OpenThread(0x0001, False, self.reader.native_id)
            if not self.reader_handle:
                raise ctypes.WinError(ctypes.get_last_error())
            self.reader_start.set()
        except BaseException:
            self.close()
            raise

    @staticmethod
    def _set(handle, mode):
        if not _kernel.SetConsoleMode(handle, mode):
            raise ctypes.WinError(ctypes.get_last_error())

    def _publish(self, value):
        while not self.stopping.is_set():
            try:
                self.events.put(value, timeout=.02)
                return
            except queue.Full:
                pass

    def _read_console(self):
        self.reader_start.wait()
        decoder = codecs.getincrementaldecoder('utf-16-le')('strict')
        try:
            while not self.stopping.is_set():
                buffer = ctypes.create_string_buffer(8192)
                count = wintypes.DWORD()
                if not _kernel.ReadConsoleW(self.input, buffer, 4096, ctypes.byref(count), None):
                    if self.stopping.is_set():
                        return
                    raise ctypes.WinError(ctypes.get_last_error())
                text = decoder.decode(buffer.raw[:count.value * 2], final=count.value == 0)
                if text or not count.value:
                    self._publish(text.encode('utf8'))
                if not count.value:
                    return
        except Exception as ex:
            self._publish(ex)

    def read(self, timeout):
        try:
            value = self.events.get(timeout=timeout)
        except queue.Empty:
            return None
        if isinstance(value, Exception):
            raise value
        return value

    def write(self, data):
        text = data.decode('utf8')
        for start in range(0, len(text), 4096):
            raw = text[start:start + 4096].encode('utf-16-le')
            while raw:
                buffer = ctypes.create_string_buffer(raw)
                written = wintypes.DWORD()
                if not _kernel.WriteConsoleW(self.output, buffer, len(raw) // 2, ctypes.byref(written), None):
                    raise ctypes.WinError(ctypes.get_last_error())
                if not written.value:
                    raise OSError('Terminal output made no progress')
                raw = raw[written.value * 2:]

    def close(self):
        # Attempt both even if one restoration fails; never report success then.
        failure = None
        self.stopping.set()
        self.reader_start.set()
        if self.reader is not None:
            deadline = time.monotonic() + 2
            while self.reader.is_alive() and time.monotonic() < deadline:
                if self.reader_handle:
                    # A read can start just after cancellation reports NOT_FOUND;
                    # retry against the retained handle until actual thread exit.
                    if not _kernel.CancelSynchronousIo(self.reader_handle):
                        error = ctypes.get_last_error()
                        if error != 1168:
                            failure = ctypes.WinError(error)
                self.reader.join(.02)
            if self.reader.is_alive():
                failure = OSError('Console input reader shutdown unconfirmed')
            elif self.reader_handle:
                if not _kernel.CloseHandle(self.reader_handle):
                    failure = ctypes.WinError(ctypes.get_last_error())
                self.reader_handle = None
        for handle, mode in zip((self.input, self.output), self.initial):
            try:
                self._set(handle, mode)
            except OSError as ex:
                failure = ex
        if failure:
            raise failure


def Terminal(input_fd, output_fd):
    return (_WindowsTerminal if os.name == 'nt' else _PosixTerminal)(input_fd, output_fd)


def poll_child_output(stream):
    """None = no data; b'' = observed EOF; bytes = available bounded output."""
    if os.name != 'nt':
        import select
        return os.read(stream.fileno(), 65536) if select.select([stream], [], [], 0)[0] else None
    available = wintypes.DWORD()
    handle = msvcrt.get_osfhandle(stream.fileno())
    if not _kernel.PeekNamedPipe(handle, None, 0, None, ctypes.byref(available), None):
        error = ctypes.get_last_error()
        if error == 109:  # ERROR_BROKEN_PIPE, actual end of this retained pipe.
            return b''
        raise ctypes.WinError(error)
    if not available.value:
        return None
    return os.read(stream.fileno(), min(available.value, 65536))
