"""Verify full child-output artifacts without embedding them in a JSON scalar.

Historical inline-output readers are unchanged. Rebased files are immutable DATA;
this adapter validates bytes and framing, never process ownership or native proof.
"""
from pathlib import Path, PureWindowsPath
import argparse
import hashlib
import json

REPRESENTATION = "decoded-text-reencoded-utf8-no-bom"


def validate(manifest_path, *, directory=None, allow_incomplete=False):
    """Return compact verified file references; read every byte with fixed memory."""
    manifest_path = Path(manifest_path)
    document = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    if document.get("Kind") != "joint_child_output_v1":
        raise ValueError("Unknown child-output manifest kind")
    root = Path(directory).resolve() if directory is not None else manifest_path.parent.resolve()
    streams = {}
    for key, filename in (("Stdout", "stdout.utf8"), ("Stderr", "stderr.utf8")):
        reference = document[key]
        if reference.get("Representation") != REPRESENTATION:
            raise ValueError(f"Unsupported decoded-text representation: {key}")
        if type(reference.get("Eof")) is not bool or type(reference.get("PersistenceComplete")) is not bool:
            raise ValueError(f"Missing explicit completion observations: {key}")
        original = reference["Path"]
        if PureWindowsPath(original).name != filename:
            raise ValueError(f"Unexpected artifact basename: {key}")
        candidate = root / filename
        path = candidate.resolve()
        if path.parent != root or candidate.is_symlink():
            raise ValueError(f"Artifact escapes its exclusive directory: {key}")
        if directory is None and Path(original).resolve() != path:
            raise ValueError(f"Manifest path disagrees with its capture directory: {key}")
        before = path.stat()
        digest = hashlib.sha256()
        length = 0
        with path.open("rb") as stream:
            while block := stream.read(65536):
                length += len(block)
                digest.update(block)
        after = path.stat()
        if (before.st_size, before.st_mtime_ns, before.st_ino) != (after.st_size, after.st_mtime_ns, after.st_ino):
            raise ValueError(f"Artifact changed during verification: {key}")
        actual_hash = digest.hexdigest().upper()
        if length != after.st_size:
            raise ValueError(f"Artifact length changed during verification: {key}")
        if reference.get("StoredBytes") is not None and reference["StoredBytes"] != length:
            raise ValueError(f"Stored byte length mismatch: {key}")
        if reference.get("Sha256") is not None and reference["Sha256"] != actual_hash:
            raise ValueError(f"Stored SHA256 mismatch: {key}")
        complete = reference["PersistenceComplete"]
        if complete and (document.get("FinalizationAttempted") is not True or not reference["Eof"] or reference.get("ReadFailure") is not None or
                         reference.get("Failures") or reference.get("StoredBytes") is None or
                         reference.get("Sha256") is None):
            raise ValueError(f"Inconsistent successful persistence: {key}")
        if not complete and not allow_incomplete:
            raise ValueError(f"Explicitly incomplete capture: {key}")
        streams[key] = {"Path": str(path), "Bytes": length, "Sha256": actual_hash,
                        "Eof": reference["Eof"], "PersistenceComplete": complete,
                        "ReadFailure": reference.get("ReadFailure"), "Failures": reference.get("Failures", [])}
    if document.get("Errors") and not allow_incomplete:
        raise ValueError("Capture includes diagnostic/manifest/report errors")
    return {"Kind": document["Kind"], "Manifest": str(manifest_path), "Rebased": directory is not None,
            "DataOnly": True, "NativeIdentityVerified": False, "Context": document.get("Context"),
            "Identity": document.get("Identity"), "Outcome": document.get("Outcome"),
            "FinalizationAttempted": document.get("FinalizationAttempted"),
            "Errors": document.get("Errors", []), "Streams": streams}


def main():
    """Validate the explicitly selected manifest without launching project code."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--directory", type=Path, help="Rebase known artifact filenames after copying immutable DATA")
    parser.add_argument("--allow-incomplete", action="store_true", help="Verify retained partial files, preserving incomplete status")
    args = parser.parse_args()
    print(json.dumps(validate(args.manifest, directory=args.directory, allow_incomplete=args.allow_incomplete),
                     ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
