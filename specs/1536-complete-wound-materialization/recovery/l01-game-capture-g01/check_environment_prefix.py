"""Execute only the actual caller filesystem prefix and pinned environment() AST; no processes/API."""
from pathlib import Path
import ast
import hashlib
import json
import os
import stat
import sys

HERE = Path(__file__).resolve().parent
H = lambda b: hashlib.sha256(b).hexdigest()
source = HERE/'watchdog.py'
helper = HERE.parent/'l01-managed-stack-s01/smoke.py'
tree = ast.parse(source.read_text())
main = next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name == 'main')
outer = next(n for n in main.body if isinstance(n,ast.Try))
index = next(i for i,n in enumerate(outer.body) if isinstance(n,ast.Assign) and isinstance(n.value,ast.Call)
    and isinstance(n.value.func,ast.Subscript) and isinstance(n.value.func.value,ast.Name)
    and n.value.func.value.id == 'lib' and n.value.func.slice.value == 'environment')
start = index
if isinstance(outer.body[index].value.args[0],ast.Name):
    name = outer.body[index].value.args[0].id
    start = next(i for i,n in enumerate(outer.body[:index]) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id == name for t in n.targets))
end = next(i for i,n in enumerate(outer.body[index:],index) if isinstance(n,ast.FunctionDef))
prefix = ast.Module(body=outer.body[start:end],type_ignores=[])
for n in ast.walk(prefix):
    if isinstance(n,ast.Call):
        assert isinstance(n.func,ast.Name) and n.func.id == 'str' or isinstance(n.func,ast.Attribute) and n.func.attr == 'mkdir' or isinstance(n.func,ast.Subscript) and n.func.slice.value == 'environment'
function = next(n for n in ast.parse(helper.read_text()).body if isinstance(n,ast.FunctionDef) and n.name == 'environment')
lib = {'Path':Path}
exec(compile(ast.fix_missing_locations(ast.Module(body=[function],type_ignores=[])),str(helper),'exec'),lib)
os.umask(0o077)
proof = Path(sys.argv[1]); proof.mkdir(mode=0o700)
own = proof/'own'; own.mkdir(mode=0o700); (own/'tmp').mkdir(mode=0o700)
root = own/'capture'; root.mkdir(mode=0o700); phase = root/'collector-env'
scope = dict(root=root,own=own,lib=lib)
error = None
try: exec(compile(ast.fix_missing_locations(prefix),str(source),'exec'),scope)
except BaseException as e: error = dict(type=type(e).__name__,message=str(e))
children = ['tmp','cache','config','data','runtime','cli']
keys = {'PATH','DOTNET_ROOT','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_SKIP_FIRST_TIME_EXPERIENCE','LANG',
    'TMPDIR','XDG_CACHE_HOME','XDG_CONFIG_HOME','XDG_DATA_HOME','XDG_RUNTIME_DIR','DOTNET_CLI_HOME'}
env = scope.get('env',{})
checks = [dict(name='actual prefix completes without missing parent',passed=error is None),
    dict(name='caller parent and six children private0700',passed=all(p.is_dir() and stat.S_IMODE(p.stat().st_mode)==0o700 for p in [phase]+[phase/c for c in children])),
    dict(name='same allowlist and target own TMPDIR',passed=set(env)==keys and env.get('TMPDIR')==str(own/'tmp'))]
refused = False
try: exec(compile(ast.fix_missing_locations(prefix),str(source),'exec'),scope)
except FileExistsError: refused = True
except BaseException: pass
checks.append(dict(name='strict caller prefix refuses existing phase',passed=refused))
result = dict(kind='G01_ACTUAL_CALLER_PREFIX_FILESYSTEM_REGRESSION',watchdogSha256=H(source.read_bytes()),
    helperSha256=H(helper.read_bytes()),proofSourceSha256=H(Path(__file__).read_bytes()),
    prefixAstSha256=H(ast.dump(prefix,include_attributes=False).encode()),checks=checks,passed=sum(c['passed'] for c in checks),
    firstError=error,nativeChildren=0,gameCases=0,apiRequests=0,originalGameRootsTouched=False,retainedProofRoot=str(proof))
print(json.dumps(result,indent=2))
raise SystemExit(0 if all(c['passed'] for c in checks) else 1)
