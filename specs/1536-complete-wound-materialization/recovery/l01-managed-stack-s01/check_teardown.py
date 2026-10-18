"""Original AST close fragments and fake callbacks only; no live diagnostic target."""
from pathlib import Path
from types import SimpleNamespace
import ast
import copy
import hashlib
import json
import runpy
import subprocess

here = Path(__file__).resolve().parent
repo = here.parents[3]
source = '33cb9a07180dd90046d17bb23043cd401cf38695'
old = subprocess.check_output(['git', 'show', source + ':' + str((here / 'smoke.py').relative_to(repo))], cwd=repo).decode()
tree = ast.parse(old)
forms = {'stdin': 'target.stdin.close()', 'target_fd': 'os.close(target_handle)',
         'drain': 'stream.close()', 'subprocess_fd': 'os.close(handle)'}
current = runpy.run_path(str(here / 'smoke.py'))
baseline = []
corrected = []
for site, form in forms.items():
    fragments = [node for node in ast.walk(tree) if isinstance(node, ast.Expr) and ast.unparse(node) == form]
    fragment = max(fragments, key=lambda node: node.lineno)
    for native in [0, 7]:
        def fail():
            raise OSError('fake ' + site + ' close failure')
        scope = dict(target=SimpleNamespace(stdin=SimpleNamespace(close=fail)), stream=SimpleNamespace(close=fail),
                     target_handle=42, handle=43, os=SimpleNamespace(close=lambda value: fail()))
        try:
            exec(compile(ast.fix_missing_locations(ast.Module(body=[fragment], type_ignores=[])), '<original-close>', 'exec'), scope)
        except OSError:
            baseline.append(dict(site=site, nativeExit=native, originalUnhandled=True, originalLine=fragment.lineno))
        else:
            raise AssertionError('Original close no longer reproduces failure.')
        for close_fails in [False, True]:
            record = dict(nativeExit=native, observerErrors=[])
            saved = []
            calls = []
            def close():
                calls.append('close')
                if close_fails: fail()
            current['close_observed'](record, site, close)
            code = current['FINALIZE'](record, lambda value: saved.append(copy.deepcopy(value)), lambda value: None)
            assert code == (7 if native == 7 else 2 if close_fails else 0)
            assert record['nativeExit'] == native and bool(record['observerErrors']) == close_fails
            assert calls == ['close'] and saved[-1] == record
            corrected.append(dict(site=site, nativeExit=native, closeFails=close_fails, controllerExit=code, recordPersisted=True))
report = dict(kind='L01_S01_FAKE_TEARDOWN_PROOF', baselineSource=source,
              baselineSourceSha256=hashlib.sha256(old.encode()).hexdigest(),
              currentSourceSha256=hashlib.sha256((here / 'smoke.py').read_bytes()).hexdigest(),
              baseline=baseline, corrected=corrected, outcome='PASS',
              limits='8 original AST reproductions,16 corrected callback cases; zero synthetic/diagnostic API calls. No original native result is changed.')
destination = here.parent / 'l01-managed-stack-teardown-proof-20261018.json'
destination.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(dict(outcome='PASS', original=len(baseline), corrected=len(corrected))))
