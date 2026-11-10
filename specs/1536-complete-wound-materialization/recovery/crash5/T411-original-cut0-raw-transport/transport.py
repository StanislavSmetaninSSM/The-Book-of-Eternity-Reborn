"""Lossless, streaming transport of the complete T411 original cut0 raw ZIP.

No command deletes, overwrites, changes Git configuration, or excludes a file.
Failures leave created files available for inspection. Transport qualification
establishes byte equality only, not diagnostic or original-case acceptance.
"""
from pathlib import Path
import argparse
import datetime
import hashlib
import json
import re
import subprocess
import sys

RAW_NAME = 'T411-original-cut0-raw.zip'
EXPECTED_BYTES = 665274231
EXPECTED_SHA256 = '6D79BFA61E73E0D82FE3F5D91905C4ACB1C69C315E5AAF31C7979349D315BC03'
DEFAULT_SOURCE = Path('D:/Code/The-Book-of-Eternity-Reborn-crash5-windows-20261019/specs/1536-complete-wound-materialization/recovery/crash5') / RAW_NAME
LIMIT = 100 * 1024 * 1024
CHUNK = 96 * 1024 * 1024
BUFFER = 4 * 1024 * 1024


def require(condition, message):
    if not condition:
        raise ValueError(message)


def ordinary(path):
    state = path.stat(follow_symlinks=False)
    require(path.is_file() and not path.is_symlink() and not getattr(state, 'st_file_attributes', 0) & 1024,
            'Expected a regular non-reparse file: ' + str(path))
    return state


def consume(stream, combined=None, output=None):
    digest = hashlib.sha256()
    count = 0
    while True:
        data = stream.read(BUFFER)
        if not data:
            break
        digest.update(data)
        if combined is not None:
            combined.update(data)
        if output is not None:
            output.write(data)
        count += len(data)
    return count, digest.hexdigest().upper()


def read_manifest(path):
    ordinary(path)
    raw = path.read_bytes()
    manifest = json.loads(raw)
    require(manifest['SchemaVersion'] == 1 and manifest['OriginalName'] == RAW_NAME,
            'Unexpected transport identity or schema')
    require(manifest['ChunkBytes'] == CHUNK and manifest['GitHubLimitBytes'] == LIMIT,
            'Unexpected transport sizes')
    require(manifest['OriginalBytes'] == EXPECTED_BYTES and manifest['OriginalSHA256'] == EXPECTED_SHA256,
            'Transport identity differs from the closed T411 raw ZIP')
    size = manifest['OriginalBytes']
    require(type(size) is int and size > 0, 'Invalid original byte count')
    split = size > LIMIT
    require(manifest['Mode'] == ('split' if split else 'single'), 'Incorrect transport mode')
    parts = manifest['Parts']
    require(isinstance(parts, list) and parts, 'Missing transport parts')
    offset = 0
    for index, part in enumerate(parts, 1):
        expected_name = RAW_NAME + '.part' + str(index).zfill(6) if split else RAW_NAME
        require(part['Name'] == expected_name and part['Index'] == index and part['Offset'] == offset,
                'Noncontiguous, misnamed or reordered part')
        expected_bytes = min(CHUNK, size - offset) if split else size
        require(type(part['Bytes']) is int and part['Bytes'] == expected_bytes and 0 < part['Bytes'] <= LIMIT,
                'Unexpected part byte count')
        require(re.fullmatch('[0-9A-F]{64}', part['SHA256']) is not None, 'Invalid part SHA256')
        offset += part['Bytes']
    require(offset == size and (split or len(parts) == 1), 'Incomplete or extra parts')
    require(re.fullmatch('[0-9A-F]{64}', manifest['OriginalSHA256']) is not None, 'Invalid original SHA256')
    return manifest, raw


def verify(path, output=None, repo=None, git_ref=None):
    manifest, raw = read_manifest(path)
    if git_ref is not None:
        require(repo is not None, '--repo is required with --git-ref')
        relative = path.resolve().relative_to(repo.resolve()).as_posix()
        saved = subprocess.check_output(['git', '-C', str(repo), 'cat-file', 'blob', git_ref + ':' + relative])
        require(saved == raw, 'Manifest bytes differ from the selected Git blob')
    combined = hashlib.sha256()
    total = 0
    for part in manifest['Parts']:
        file = path.parent / part['Name']
        if git_ref is None:
            require(ordinary(file).st_size == part['Bytes'], 'Part size differs: ' + str(file))
            with file.open('rb') as stream:
                size, digest = consume(stream, combined, output)
        else:
            relative = file.resolve().relative_to(repo.resolve()).as_posix()
            child = subprocess.Popen(['git', '-C', str(repo), 'cat-file', 'blob', git_ref + ':' + relative], stdout=subprocess.PIPE)
            try:
                with child.stdout as stream:
                    size, digest = consume(stream, combined, output)
            finally:
                code = child.wait()
            require(code == 0, 'Cannot read selected Git part blob: ' + relative)
        require(size == part['Bytes'] and digest == part['SHA256'], 'Part hash/size differs: ' + part['Name'])
        total += size
    require(total == manifest['OriginalBytes'] and combined.hexdigest().upper() == manifest['OriginalSHA256'],
            'Concatenation differs from the original ZIP identity')
    return manifest


def split(source, destination):
    require(source.name == RAW_NAME, 'This helper transports only the exact T411 original raw ZIP')
    before = ordinary(source)
    require(before.st_size == EXPECTED_BYTES, 'Original ZIP size differs from the closed T411 raw ZIP')
    raw_report_path = source.with_suffix('.json')
    ordinary(raw_report_path)
    raw_report_bytes = raw_report_path.read_bytes()
    raw_report = json.loads(raw_report_bytes)
    require(raw_report['Archive'] == RAW_NAME and raw_report['Stem'] == 'T411-original-cut0' and raw_report['ArchiveSHA256'] == EXPECTED_SHA256,
            'Matching completed T411 raw archive report is required')
    destination.mkdir(parents=False, exist_ok=False)
    parts = []
    combined = hashlib.sha256()
    offset = 0
    split_mode = before.st_size > LIMIT
    with source.open('rb') as original:
        while offset < before.st_size:
            index = len(parts) + 1
            name = RAW_NAME + '.part' + str(index).zfill(6) if split_mode else RAW_NAME
            remaining = min(CHUNK, before.st_size - offset) if split_mode else before.st_size
            digest = hashlib.sha256()
            size = 0
            with (destination / name).open('xb') as output:
                while size < remaining:
                    data = original.read(min(BUFFER, remaining - size))
                    require(bool(data), 'Original ZIP became shorter while splitting')
                    output.write(data)
                    digest.update(data)
                    combined.update(data)
                    size += len(data)
            parts.append({'Index': index, 'Name': name, 'Offset': offset, 'Bytes': size, 'SHA256': digest.hexdigest().upper()})
            offset += size
        require(original.read(1) == b'', 'Original ZIP became longer while splitting')
    after = ordinary(source)
    require((before.st_size, before.st_mtime_ns) == (after.st_size, after.st_mtime_ns), 'Original ZIP changed while splitting')
    require(combined.hexdigest().upper() == raw_report['ArchiveSHA256'],
            'Original ZIP differs from the completed raw archive report')
    manifest = {'SchemaVersion': 1, 'Task': 'T411', 'CreatedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                'OriginalName': RAW_NAME, 'OriginalBytes': offset, 'OriginalSHA256': combined.hexdigest().upper(),
                'RawReportName': raw_report_path.name, 'RawReportSHA256': hashlib.sha256(raw_report_bytes).hexdigest().upper(),
                'Mode': 'split' if split_mode else 'single', 'ChunkBytes': CHUNK, 'GitHubLimitBytes': LIMIT,
                'Parts': parts, 'Meaning': 'Complete byte transport only; no diagnostic rows omitted or altered.'}
    path = destination / 'transport.json'
    with path.open('x', encoding='utf-8', newline='\n') as output:
        output.write(json.dumps(manifest, indent=2) + '\n')
    verify(path)
    with source.open('rb') as stream:
        size, digest = consume(stream)
    require(size == offset and digest == manifest['OriginalSHA256'], 'Final original ZIP differs from verified concatenation')
    return path, manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    command = commands.add_parser('split')
    command.add_argument('--source', type=Path, default=DEFAULT_SOURCE)
    command.add_argument('--output-dir', type=Path)
    for name in ['verify', 'reassemble']:
        command = commands.add_parser(name)
        command.add_argument('--manifest', type=Path, required=True)
        if name == 'verify':
            command.add_argument('--repo', type=Path)
            command.add_argument('--git-ref')
        else:
            command.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.command == 'split':
        destination = args.output_dir or args.source.with_name('T411-original-cut0-raw-transport')
        path, manifest = split(args.source, destination)
    elif args.command == 'verify':
        path = args.manifest
        manifest = verify(path, repo=args.repo, git_ref=args.git_ref)
    else:
        path = args.manifest
        verify(path)
        with args.output.open('xb') as output:
            manifest = verify(path, output=output)
        with args.output.open('rb') as stream:
            size, digest = consume(stream)
        require(size == manifest['OriginalBytes'] and digest == manifest['OriginalSHA256'], 'Reassembled file differs')
    print(json.dumps({'Command': args.command, 'Manifest': str(path), 'Parts': len(manifest['Parts']),
                      'OriginalBytes': manifest['OriginalBytes'], 'OriginalSHA256': manifest['OriginalSHA256'],
                      'ByteTransportVerified': True, 'OriginalCaseQualified': False}))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(type(error).__name__ + ': ' + str(error), file=sys.stderr)
        raise SystemExit(1)
