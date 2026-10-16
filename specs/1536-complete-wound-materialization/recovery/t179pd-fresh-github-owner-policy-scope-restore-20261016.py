from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys

root = Path.cwd()
sha, destination, receipt = sys.argv[1:]
target = Path(destination)
assert target.is_absolute() and target.parent == Path('/tmp') and not target.exists()
assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip() == sha
assert not subprocess.check_output(['git', 'status', '--porcelain'], text=True)
branch = '1536-wound-b2-cloud-20261012'
origin = subprocess.check_output(['git', 'remote', 'get-url', 'origin'], text=True).strip()
assert origin == 'https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git'
refs = ['git', 'ls-remote', 'origin', 'refs/heads/' + branch, 'refs/heads/main']
remote = subprocess.check_output(refs, text=True)
assert sha + '\trefs/heads/' + branch in remote
assert 'd0241e71e349fbe2020e4a41b6be7c627c81cbb4\trefs/heads/main' in remote

recovery = root / 'specs/1536-complete-wound-materialization/recovery'
profile = json.loads((recovery / 't179pd-observed-reader-final-fresh-source-recovery-20261016.json').read_text())
paths = {row['path'] for row in profile['files']}
paths.update(subprocess.check_output([
    'git', 'diff', '--name-only', 'd13aab44fd3487d95bf6005bb8296dbda54846bb', sha
], text=True).splitlines())
tracked = set(subprocess.check_output(['git', 'ls-files'], text=True).splitlines())
scopes = []
required = set()

def include_report(name, keys):
    raw = (recovery / name).read_bytes()
    data = json.loads(raw)
    selected = set()
    for key in keys:
        value = data
        for part in key.split('.'):
            value = value[part]
        selected.update(row['path'] for row in value)
    assert selected <= tracked, (name, sorted(selected - tracked))
    paths.update(selected)
    required.update(selected)
    scopes.append(dict(report=name, sha256=hashlib.sha256(raw).hexdigest(),
                       count=len(selected), paths=sorted(selected)))

include_report('t179pd-automatic-player-time-policy-engineering-independent-review-20261016.json',
               ['source_evidence.candidate_pins'])
include_report('t179pd-hot-submitted-admission-engineering-design-20261016.json', ['source_pins'])
include_report('t179pd-hot-custody-engineering-independent-review-20261016.json',
               ['source_pins', 'current_governance_and_extra_pins'])
include_report('t179pd-narrative-interface-policy-engineering-design-20261016.json', ['source_pins'])
include_report('t179pd-narrative-interface-policy-engineering-independent-review-20261016.json',
               ['source_evidence.author_historical_pins', 'source_evidence.candidate_pins'])
assert paths <= tracked
subprocess.run(['git', 'clone', '--quiet', '--depth', '1', '--filter=blob:none',
                '--single-branch', '--branch', branch, '--no-checkout', origin, str(target)], check=True)
subprocess.run(['git', '-C', str(target), 'sparse-checkout', 'set', '--no-cone', '--stdin'],
               input='\n'.join('/' + path for path in sorted(paths)) + '\n', text=True, check=True)
subprocess.run(['git', '-C', str(target), 'checkout', '--quiet', '--detach', sha], check=True)
assert subprocess.check_output(['git', '-C', str(target), 'rev-parse', 'HEAD'], text=True).strip() == sha
assert not (target / '.git/objects/info/alternates').exists()
files = []
for path in target.rglob('*'):
    if '.git' in path.parts or not path.is_file():
        continue
    relative = str(path.relative_to(target))
    raw = subprocess.check_output(['git', 'show', sha + ':' + relative])
    assert path.read_bytes() == raw == (root / relative).read_bytes()
    files.append(dict(path=relative, sha256=hashlib.sha256(raw).hexdigest(), bytes=len(raw)))
assert paths <= {row['path'] for row in files}
assert required <= {row['path'] for row in files}
assert not subprocess.check_output(['git', '-C', str(target), 'status', '--porcelain'], text=True)
assert subprocess.check_output(refs, text=True) == remote
result = dict(sha=sha, directory=str(target), utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              files=files, count=len(files), new_directory=True, clean=True, noAlternates=True,
              source='NEW GitHub-only shallow filtered sparse clone; no local object/reference/cache transport',
              remoteReadback=remote, restoredBuildOrRuntime='NOT_RUN', dependencyScopes=scopes,
              concreteRequiredPaths=sorted(required),
              scope='Selected reader/delta plus all concrete reviewed 3b owner, 3c automatic and 3d narrative/interface dependencies; not a full checkout or restored build/runtime')
output = Path(receipt)
assert not output.exists()
output.write_text(json.dumps(result, indent=2) + '\n')
print('FRESH_GITHUB_OWNER_POLICY_SELECTED_RESTORE', sha, len(files), 'exact files;',
      len(required), 'required dependency paths; NEW/clean/noAlternates/main unchanged; no restored runtime')
