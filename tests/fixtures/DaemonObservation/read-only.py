"""Test-only raw observer: no advisory lock request, ownership operation, or write to input."""
import os
import stat
import sys

path = sys.argv[1]
if not os.path.isabs(path):
    raise ValueError('Absolute owned fixture input required')
fd = os.open(path, os.O_RDONLY | os.O_CLOEXEC | os.O_NOFOLLOW | os.O_NONBLOCK)
try:
    if not stat.S_ISREG(os.fstat(fd).st_mode):
        raise ValueError('Observation requires an owned regular file')
    while True:
        chunk = os.read(fd, 65536)
        if not chunk:
            break
        sys.stdout.buffer.write(chunk)
    sys.stdout.buffer.flush()
finally:
    os.close(fd)
