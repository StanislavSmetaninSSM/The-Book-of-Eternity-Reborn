"""Read-only GitHub restoration/artifact integrity, not gameplay qualification.

python3 <this script> /path/to/fresh/github/clone <expected-full-SHA>
Run after ordinary clone of the task branch. No build, test, game or model call.
"""
import gzip
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

root = Path(sys.argv[1]).resolve()
expected = sys.argv[2]
assert re.fullmatch(r"[0-9a-f]{40}", expected)


def git(*args):
    return subprocess.check_output(["git", *args], cwd=root)


assert git("rev-parse", "HEAD").decode().strip() == expected
assert not git("status", "--porcelain").strip(), "Fresh checkout must be clean"
assert git("rev-parse", "--is-shallow-repository").strip() == b"false"
alternates = Path(git("rev-parse", "--git-path", "objects/info/alternates").decode().strip())
assert not (alternates if alternates.is_absolute() else root / alternates).exists()
origin = "https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git"
assert git("remote", "get-url", "origin").decode().strip() == origin
refs = git("ls-remote", "origin", "refs/heads/1553-storage-migration-cloud-20261008", "refs/heads/main").decode()
assert expected + "\trefs/heads/1553-storage-migration-cloud-20261008" in refs
assert "d0241e71e349fbe2020e4a41b6be7c627c81cbb4\trefs/heads/main" in refs
blobs = 0
for row in git("ls-tree", "-r", "-z", "HEAD").split(b"\0"):
    if not row:
        continue
    meta, name = row.split(b"\t", 1)
    mode, kind, oid = meta.split()
    assert kind == b"blob", "Unexpected gitlink"
    path = root / os.fsdecode(name)
    data = os.fsencode(os.readlink(path)) if mode == b"120000" else path.read_bytes()
    assert hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest() == oid.decode(), path
    blobs += 1
fsck = subprocess.run(["git", "fsck", "--full", "--strict"], cwd=root, capture_output=True, text=True)
assert fsck.returncode == 0, fsck.stdout + fsck.stderr
packet = root / "specs/1553-portable-local-storage/recovery/linux-game-chains-20261010"
manifests = artifacts = 0
sources = set()
for manifest in sorted(packet.glob("*/manifest.json")):
    document = json.loads(manifest.read_text())
    source = document["SourceSHA"]
    assert re.fullmatch(r"[0-9a-f]{40}", source), manifest
    git("cat-file", "-e", source + "^{commit}")
    sources.add(source)
    seen = set()
    for entry in document["Files"]:
        path = (manifest.parent / entry["Path"]).resolve()
        assert path.is_relative_to(manifest.parent.resolve()) and path not in seen
        seen.add(path)
        raw = gzip.decompress(path.read_bytes())
        assert len(raw) == entry["RawBytes"], path
        assert hashlib.sha256(raw).hexdigest() == entry["RawSHA256"], path
        artifacts += 1
    manifests += 1
print(json.dumps({"Status": "PASS", "Scope": "GitHub-only clean restoration and raw artifact integrity; not runtime acceptance",
    "HEAD": expected, "Origin": origin, "Tree": git("rev-parse", "HEAD^{tree}").decode().strip(),
    "TrackedBlobs": blobs, "Manifests": manifests, "RawArtifacts": artifacts,
    "SourceCommits": sorted(sources), "FsckExitCode": fsck.returncode, "RemoteRefs": refs.splitlines()}, indent=2))
