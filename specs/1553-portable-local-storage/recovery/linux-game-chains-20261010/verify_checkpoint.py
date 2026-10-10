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

if not __debug__:
    raise RuntimeError("Checkpoint verification requires Python assertions; do not use -O or PYTHONOPTIMIZE")

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
source_annotations = []
plain_artifacts = 0
for manifest in sorted(packet.glob("*/manifest.json")):
    document = json.loads(manifest.read_text())
    source = document.get("SourceSHA", document.get("RuntimeSource"))
    if source is not None:
        assert re.fullmatch(r"[0-9a-f]{40}", source), manifest
        git("cat-file", "-e", source + "^{commit}")
        sources.add(source)
    else:
        # The early browser ownership preflight explicitly records an
        # uncommitted correction. Check its bytes, never invent a source SHA.
        assert set(document) == {"Scope", "Source", "Files"}, manifest
        assert isinstance(document["Source"], str) and document["Source"], manifest
        source_annotations.append({"Packet": manifest.parent.name, "Source": document["Source"]})
    assert isinstance(document["Files"], list) and document["Files"], manifest
    seen = set()
    for entry in document["Files"]:
        path = (manifest.parent / entry["Path"]).resolve()
        assert path.is_relative_to(manifest.parent.resolve()) and path not in seen
        seen.add(path)
        stored = path.read_bytes()
        if path.suffix == ".gz":
            raw = gzip.decompress(stored)
            length, digest = entry["RawBytes"], entry["RawSHA256"]
        else:
            raw = stored
            length, digest = entry["Bytes"], entry["SHA256"]
            plain_artifacts += 1
        assert len(raw) == length, path
        assert hashlib.sha256(raw).hexdigest() == digest, path
        artifacts += 1
    manifests += 1
print(json.dumps({"Status": "PASS", "Scope": "GitHub-only clean restoration and raw artifact integrity; not runtime acceptance",
    "HEAD": expected, "Origin": origin, "Tree": git("rev-parse", "HEAD^{tree}").decode().strip(),
    "TrackedBlobs": blobs, "Manifests": manifests, "RawArtifacts": artifacts,
    "SourceCommits": sorted(sources), "SourceAnnotationsWithoutCommitBinding": source_annotations,
    "PlainArtifacts": plain_artifacts, "FsckExitCode": fsck.returncode, "RemoteRefs": refs.splitlines()}, indent=2))
