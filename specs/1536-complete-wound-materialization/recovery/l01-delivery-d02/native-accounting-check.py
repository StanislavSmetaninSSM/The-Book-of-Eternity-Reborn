"""Pure D02-S01 regression using original fakes and the actual pinned M01 finalizer."""
from pathlib import Path
import ast
import hashlib
import json
import sys
import types

here=Path(__file__).resolve().parent;recovery=here.parent
source=(here/'phase.py').read_bytes()
original=ast.parse((recovery/'check_d02_original_source.py').read_text())
m01=recovery/'t148-m01-run-phase-20261018.py'
packet=json.loads((recovery/'l01-delivery-d01-source-packet-20261018.json').read_text())
pin=next(x for x in packet['sourcePins'] if x['path'].endswith('/t148-m01-run-phase-20261018.py'))
assert hashlib.sha256(m01.read_bytes()).hexdigest()==pin['sha256']
nodes=[n for n in original.body if isinstance(n,ast.ClassDef) and n.name in ('FakeFile','Child')]
nodes+=[n for n in ast.parse(m01.read_text()).body if isinstance(n,ast.FunctionDef) and n.name=='finalize']
ns={};exec(compile(ast.Module(body=nodes,type_ignores=[]),'<pinned-finalizer-and-original-pure-fakes>','exec'),ns)
fn=next(n for n in ast.parse(source).body if isinstance(n,ast.FunctionDef) and n.name=='run_owned')
rows=[]
for native in (0,7,124,-9):
    for fault in ('none','record-persist','output-close'):
        env=dict(time=types.SimpleNamespace(monotonic=lambda:100.0),os=types.SimpleNamespace(getpid=lambda:42),
            subprocess=types.SimpleNamespace(Popen=lambda *a,**kw:ns['Child'](native),DEVNULL=-3),REPO=ns['FakeFile']())
        exec(compile(ast.Module(body=[fn],type_ignores=[]),'<current-run-owned-only>','exec'),env)
        def write(path,value):
            if fault=='record-persist' and str(path).endswith('.record.json'):raise OSError('fake final command record persistence failure')
        lib=dict(write=write,identity=lambda pid:dict(pid=pid,parentPid=42),FINALIZE=ns['finalize'])
        row=dict(native=native,fault=fault,passed=False)
        try:
            result=env['run_owned'](['fake-command'],ns['FakeFile'](fault=fault=='output-close'),'command',{},200,lib,lambda pid:([],{}))
            final=ns['finalize'](result,lambda r:None,lambda r:None)
            row.update(returnedNative=result['nativeExit'],raw=result['rawWrapperReturnCode'],signal=result['terminationSignal'],observerErrors=result['observerErrors'],finalizerExit=final)
            row['passed']=result['nativeExit']==result['rawWrapperReturnCode']==native and final==(native if native else (2 if fault!='none' else 0)) and bool(result['observerErrors'])==(fault!='none')
        except BaseException as error:row['escaped']=repr(error)
        rows.append(row)
result=dict(kind='D02_S01_PURE_PINNED_FINALIZE_REGRESSION',controllerSha256=hashlib.sha256(source).hexdigest(),
    finalizerSourceSha256=pin['sha256'],cases=rows,passed=sum(r['passed'] for r in rows),failed=[r for r in rows if not r['passed']],nativeActions=0)
Path(sys.argv[1]).write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:result[k] for k in ('controllerSha256','passed','failed','nativeActions')},indent=2))
sys.exit(0 if not result['failed'] else 1)
