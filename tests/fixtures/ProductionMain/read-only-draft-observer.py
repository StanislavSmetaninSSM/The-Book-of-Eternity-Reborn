#!/usr/bin/python3
"""Zero-submit fixture: observe only OpenCode's actual owned temporary draft.

Receives no expected draft; leaves the CLI file unchanged. This is not a runtime
operation witness, ready profile, model controller or player dependency.
"""
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import sys

assert len(sys.argv) == 2
root = Path(os.environ['BOE_DRAFT_PROBE_ROOT'])
temporary = root / 'tmp'
path = Path(sys.argv[1])
assert root.is_absolute() and temporary == path.parent
assert re.fullmatch(r'[0-9]+\.md', path.name)
assert not root.is_symlink() and not temporary.is_symlink()
directory = os.open(temporary, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
try:
    fd = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=directory)
    try:
        before = os.fstat(fd)
        assert stat.S_ISREG(before.st_mode) and before.st_nlink == 1 and before.st_size <= 65536
        data = bytearray()
        while chunk := os.read(fd, 4096):
            data.extend(chunk)
            assert len(data) <= 65536
        bytes(data).decode('utf-8', errors='strict')
        os.lseek(fd, 0, os.SEEK_SET)
        second = bytearray()
        while chunk := os.read(fd, 4096):
            second.extend(chunk)
            assert len(second) <= 65536
        after = os.fstat(fd)
        assert bytes(data) == bytes(second)
        assert (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) == (
            after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)
        receipt = {'ActualArgvFile': str(path), 'ActualDraftBytes': len(data),
                   'ActualDraftSHA256': hashlib.sha256(data).hexdigest(),
                   'ActualDraftBase64': base64.b64encode(data).decode(),
                   'ReadOnlyNoFollowRegularSingleLink': True, 'UnchangedSecondRead': True,
                   'ExpectedPromptSuppliedToObserver': False, 'ModelOrSubmitInput': False}
        with (root / 'draft-observer.json').open('x') as output:
            json.dump(receipt, output, indent=2); output.write('\n')
    finally:
        os.close(fd)
finally:
    os.close(directory)
print('BOE_READ_ONLY_DRAFT_OBSERVED', flush=True)
