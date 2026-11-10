from pathlib import Path
import datetime
import hashlib
import json
import os
import subprocess
import sys

root = Path.cwd()
sha, destination, receipt = sys.argv[1:]
target, output = Path(destination), Path(receipt)
assert target.is_absolute() and target.parent == Path('/workspace') and not target.exists()
assert target != root and not output.exists()
assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip() == sha
assert not subprocess.check_output(['git', 'status', '--porcelain'], text=True)
branch = '1536-wound-b2-cloud-20261012'
origin = subprocess.check_output(['git', 'remote', 'get-url', 'origin'], text=True).strip()
assert origin == 'https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git'
refs = ['git', 'ls-remote', 'origin', 'refs/heads/' + branch, 'refs/heads/main']
remote = subprocess.check_output(refs, text=True)
assert sha + '\trefs/heads/' + branch in remote
assert 'd0241e71e349fbe2020e4a41b6be7c627c81cbb4\trefs/heads/main' in remote
tree = subprocess.check_output(['git', 'ls-tree', '-rlz', sha])
entries = []
for row in tree.split(b'\0'):
    if not row:
        continue
    metadata, path = row.split(b'\t', 1)
    mode, kind, blob, size = metadata.split()
    assert kind == b'blob' and mode in (b'100644', b'100755', b'120000')
    entries.append((mode.decode(), blob.decode(), int(size), os.fsdecode(path)))
assert len(entries) == len(set(item[3] for item in entries))
subprocess.run(['git', 'clone', '--quiet', '--depth', '1', '--single-branch',
                '--branch', branch, origin, str(target)], check=True)
assert subprocess.check_output(['git', '-C', str(target), 'rev-parse', 'HEAD'], text=True).strip() == sha
assert subprocess.check_output(['git', '-C', str(target), 'ls-tree', '-rlz', sha]) == tree
assert not (target / '.git/objects/info/alternates').exists()
assert not (target / '.git/info/sparse-checkout').exists()

def physical_bytes(directory, name, mode):
    path = directory / name
    if mode == '120000':
        assert path.is_symlink()
        return os.fsencode(os.readlink(path))
    assert path.is_file() and not path.is_symlink()
    assert bool(path.stat().st_mode & 0o111) == (mode == '100755')
    return path.read_bytes()

files, seen = [], {}
batch = subprocess.Popen(['git', 'cat-file', '--batch'], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
try:
    for mode, blob, size, name in entries:
        if blob not in seen:
            batch.stdin.write((blob + '\n').encode())
            batch.stdin.flush()
            assert batch.stdout.readline().split() == [blob.encode(), b'blob', str(size).encode()]
            body = batch.stdout.read(size)
            assert len(body) == size and batch.stdout.read(1) == b'\n'
            seen[blob] = hashlib.sha256(body).hexdigest()
        original = physical_bytes(root, name, mode)
        restored = physical_bytes(target, name, mode)
        assert original == restored and len(restored) == size
        assert hashlib.sha256(restored).hexdigest() == seen[blob]
        files.append(dict(path=name, mode=mode, git_blob=blob, bytes=size, sha256=seen[blob]))
finally:
    batch.stdin.close()
    batch.stdout.close()
    try:
        batch_exit = batch.wait(timeout=30)
    except subprocess.TimeoutExpired:
        batch.kill()
        batch.wait()
        raise
assert batch_exit == 0
assert not subprocess.check_output(['git', '-C', str(target), 'status', '--porcelain', '--ignored'], text=True)
assert not subprocess.check_output(['git', 'status', '--porcelain'], text=True)
assert subprocess.check_output(refs, text=True) == remote
result = dict(sha=sha, directory=str(target), utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              files=files, count=len(files), tracked_bytes=sum(row['bytes'] for row in files),
              new_directory=True, clean=True, noAlternates=True, noSparseCheckout=True,
              source='NEW GitHub-only full current working-tree shallow clone; no local object/reference/cache transport',
              remoteReadback=remote, restoredBuildOrRuntime='NOT_RUN',
              scope='ALL tracked current-tree files, modes and raw bytes verified against writer and exact Git blobs; shallow history only, no build/runtime or external dependency qualification')
output.write_text(json.dumps(result, indent=2) + '\n')
print('FRESH_GITHUB_FULL_CURRENT_TREE_RESTORE', sha, len(files), 'exact tracked files;',
      result['tracked_bytes'], 'bytes; NEW/clean/noAlternates/noSparse/main unchanged; no restored runtime')
