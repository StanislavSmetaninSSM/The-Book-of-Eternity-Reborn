"""AST-exact controller fault checks; no main, process, compiler or game execution."""
from pathlib import Path
import ast
import hashlib
import json
import tempfile
from types import SimpleNamespace

here=Path(__file__).resolve().parent
phase=here/'phase.py'
source=phase.read_bytes()
tree=ast.parse(source)
selected=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ('emit_summary','verify_installed_inputs')]
assert len(selected)==2
scope=dict(json=json,Path=Path,sha=lambda b:hashlib.sha256(b).hexdigest())
exec(compile(ast.Module(body=selected,type_ignores=[]),str(phase),'exec'),scope)
finalizer=here.parent/'t148-m01-run-phase-20261018.py'
node=next(n for n in ast.parse(finalizer.read_bytes()).body if isinstance(n,ast.FunctionDef) and n.name=='finalize')
exec(compile(ast.Module(body=[node],type_ignores=[]),str(finalizer),'exec'),scope)
checks=[]
for native in (0,7):
    for fault in ('none','short','error'):
        writes=[];persisted=[]
        def write(fd,payload):
            assert fd==1;writes.append(payload)
            if fault=='error':raise BrokenPipeError('Source-bound observer injection')
            return len(payload)-(1 if fault=='short' else 0)
        scope['os']=SimpleNamespace(write=write)
        record=dict(status='FAKE_ONLY',nativeExit=native,observerErrors=[],durationSeconds=0)
        result=scope['finalize'](record,lambda r:persisted.append(json.loads(json.dumps(r))),scope['emit_summary'])
        assert record['nativeExit']==native and result==(native if native else (0 if fault=='none' else 2))
        assert len(writes)==1 and writes[0].endswith(b'\n') and len(record['observerErrors'])==(fault!='none')
        assert persisted[-1]==record
        checks.append(dict(check='unbuffered_native_priority',native=native,fault=fault,returned=result,passed=True))
with tempfile.TemporaryDirectory(prefix='d01-controller-fake-') as directory:
    image=Path(directory)/'fake-runtime';image.write_bytes(b'owned fake only')
    row=dict(path=str(image),bytes=image.stat().st_size,sha256=hashlib.sha256(image.read_bytes()).hexdigest())
    inventory=dict(availableFiles=[row],net8ReferenceInputs=[])
    assert scope['verify_installed_inputs'](inventory)['count']==1
    checks.append(dict(check='inventory_exact_bytes',passed=True))
    image.write_bytes(b'changed bytes!!')
    try:scope['verify_installed_inputs'](inventory)
    except AssertionError:checks.append(dict(check='inventory_drift_rejected',passed=True))
    else:raise AssertionError('Drift accepted')
main=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='main')
guard=next(n for n in main.body if isinstance(n,ast.Try))
assert isinstance(guard.body[1],ast.Assign) and isinstance(guard.body[1].value,ast.Call) and guard.body[1].value.func.id=='verify_installed_inputs'
assert isinstance(guard.body[2],ast.If)
checks.append(dict(check='inventory_verification_before_all_phase_branches',passed=True))
assert not any(isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=='print' for n in ast.walk(main))
result=dict(kind='D01_CORRECTED_CONTROLLER_SOURCE_FAULT_CHECKS',phaseSha256=hashlib.sha256(source).hexdigest(),
    scope='AST-exact functions with fake writes and one private temporary file only; no processes/compiler/helper/game or current phase executed.',
    checks=checks,passed=len(checks),fixtureSubprocessTargets=0,allPassed=True)
output=here/'controller-fault-checks.json'
output.write_text(json.dumps(result,indent=2)+'\n')
print('SOURCE fake checks',len(checks),'PASS; no processes')
