#!/usr/bin/env python3
"""Pinned current lexical census; no builds, tests, or semantic acceptance.

The immutable baseline scanner supplies rules and route symbols through literal
AST extraction, never execution. Output is separate from the original evidence.
Run from an exact-source checkout; --out may select an empty temporary directory.
"""
import argparse
import ast
import collections
import gzip
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parent
SOURCE = '96553e1e32990d1026844baa49eed69c3e60b0a1'
BASELINE = 'd0241e71e349fbe2020e4a41b6be7c627c81cbb4'
BASE = ROOT / 'specs/1553-portable-local-storage/recovery/storage-migration-20261008'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--out', type=Path, default=OUT)
args = parser.parse_args()
args.out.mkdir(parents=True, exist_ok=True)

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)

def sha(raw):
    return hashlib.sha256(raw).hexdigest()

def literal(name):
    tree = ast.parse((BASE / 'scan.py').read_text())
    for node in tree.body:
        if isinstance(node, ast.Assign) and any(
                isinstance(target, ast.Name) and target.id == name for target in node.targets):
            return ast.literal_eval(node.value)
    raise ValueError('Missing baseline literal: ' + name)

rules = literal('RULES')
extensions = literal('EXTENSIONS')
patterns = {key: re.compile(value, re.MULTILINE) for key, value in rules.items()}
groups = json.loads((BASE / 'route-symbols.json').read_text())
route_patterns = {key: re.compile(r'(?<![\w])(?:' + '|'.join(map(re.escape, symbols)) +
                                 r')(?![\w])') for key, symbols in groups.items()}
routes = {key: {'symbols': symbols, 'occurrences': []} for key, symbols in groups.items()}
changed = set(git('diff', '--name-only', BASELINE + '..' + SOURCE).decode().splitlines())
rows, corpus, omissions = [], [], []
for entry in git('ls-tree', '-r', SOURCE).decode().splitlines():
    meta, path = entry.split('\t', 1)
    blob = meta.split()[2]
    if Path(path).suffix.lower() not in extensions:
        continue
    raw = (ROOT / path).read_bytes()
    if hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() != blob:
        raise SystemExit('Pinned source differs: ' + path)
    try:
        text = raw.decode('utf-8-sig')
    except UnicodeError:
        omissions.append({'path': path, 'gitBlob': blob, 'reason': 'Non-UTF8; manual review open'})
        continue
    count = 0
    for key, pattern in route_patterns.items():
        for match in pattern.finditer(text):
            routes[key]['occurrences'].append({'path': path, 'line': text.count('\n', 0, match.start()) + 1,
                                               'symbol': match.group()})
    for family, pattern in patterns.items():
        for match in pattern.finditer(text):
            rows.append({'path': path, 'line': text.count('\n', 0, match.start()) + 1,
                         'column': match.start() - text.rfind('\n', 0, match.start()),
                         'family': family, 'symbol': match.group().strip(),
                         'reviewStatus': 'lexical-only', 'changedSinceMain': path in changed})
            count += 1
    corpus.append({'path': path, 'gitBlob': blob, 'sha256': sha(raw), 'occurrences': count,
                   'changedSinceMain': path in changed})
rows.sort(key=lambda row: (row['path'], row['line'], row['column'], row['family']))
for number, row in enumerate(rows, 1):
    row['id'] = 'C%05d' % number
files = {
    'callsites.jsonl.gz': gzip.compress(''.join(json.dumps(row, ensure_ascii=False, separators=(',', ':')) +
                                               '\n' for row in rows).encode(), mtime=0),
    'corpus.json': (json.dumps(corpus, indent=2) + '\n').encode(),
    'routes.json': (json.dumps({'sourceRevision': SOURCE, 'families': routes,
                               'limits': 'Literal definitions/calls/strings; no dynamic reachability or acceptance.'},
                              indent=2) + '\n').encode(),
}
for name, raw in files.items():
    (args.out / name).write_bytes(raw)
manifest = {'schemaVersion': 1, 'sourceRevision': SOURCE, 'comparisonMain': BASELINE,
            'trackedSourceFiles': len(corpus), 'occurrences': len(rows),
            'byFamily': dict(collections.Counter(row['family'] for row in rows)),
            'omissions': omissions, 'baselineRules': sha((BASE / 'scan.py').read_bytes()),
            'baselineRoutes': sha((BASE / 'route-symbols.json').read_bytes()),
            'generatorSha256': sha(Path(__file__).read_bytes()),
            'artifacts': {name: sha(raw) for name, raw in files.items()},
            'limits': ['Source-only lexical census; no builds/tests/runtime probes.',
                       'No candidate classification copied from stale baseline heuristics.',
                       'Comments, strings, definitions, duplicate owners and indirect callers require human review.',
                       'Same baseline extensions/exclusions; documentation/config/data/generated output excluded.',
                       'Human accepted/open/intentional/deferred map is storage-migration-inventory.md.']}
(args.out / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
print(json.dumps({key: manifest[key] for key in ['sourceRevision', 'trackedSourceFiles', 'occurrences',
                                               'byFamily', 'omissions']}))
