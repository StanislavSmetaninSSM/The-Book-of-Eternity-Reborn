"""Native terminal/pipe I/O only; no queue, model or process ownership authority."""
import codecs
import os
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
        self.decoder = codecs.getincrementaldecoder('utf-16-le')('strict')
        try:
            # VT input, extended flags, Ctrl+C signal; no line/echo/QuickEdit.
            self._set(self.input, (self.initial[0] | 0x0200 | 0x0080 | 0x0001) & ~(0x0002 | 0x0004 | 0x0040))
            self._set(self.output, self.initial[1] | 0x0001 | 0x0004)
        except BaseException:
            self.close()
            raise

    @staticmethod
    def _set(handle, mode):
        if not _kernel.SetConsoleMode(handle, mode):
            raise ctypes.WinError(ctypes.get_last_error())

    def read(self, timeout):
        deadline = time.monotonic() + timeout
        while not msvcrt.kbhit():
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                return None
            time.sleep(min(.005, remaining))
        units = bytearray()
        while len(units) < 8192 and msvcrt.kbhit():
            units.extend(msvcrt.getwch().encode('utf-16-le', errors='surrogatepass'))
        # A UTF-16 pair may cross polls. Do not replace malformed input silently.
        text = self.decoder.decode(bytes(units), final=False)
        return text.encode('utf8') if text else None

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
