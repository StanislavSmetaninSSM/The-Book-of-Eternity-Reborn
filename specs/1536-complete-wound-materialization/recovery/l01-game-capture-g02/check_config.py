"""Inert regression of exact current G02 config guard, including frozen first invalid nonce."""
from pathlib import Path
import ast
import json
import zipfile

HERE=Path(__file__).resolve().parent
phase=HERE.parent/'l01-game-capture-g01/phase.py'
nodes=[n for n in ast.parse(phase.read_text()).body if isinstance(n,ast.FunctionDef) and n.name=='validate_config']
cases=[]
if nodes:
    ns={};exec(compile(ast.Module(body=nodes,type_ignores=[]),str(phase),'exec'),ns);validate=ns['validate_config']
    valid=dict(syntheticNonce='a'*32,syntheticRunId='b'*32,runId='c'*32)
    variants=[('valid-N32',valid,True),('N33',dict(valid,syntheticNonce='a'*33),False),('bad-hex',dict(valid,syntheticNonce='z'*32),False),('nonstring',dict(valid,syntheticNonce=32),False),('missing',dict(syntheticRunId='b'*32,runId='c'*32),False),('reused-identity',dict(valid,runId='b'*32),False)]
    with zipfile.ZipFile(HERE.parent/'l01-game-capture-g02-first-synthetic-pretarget-failure-20261019.zip') as archive:
        variants.append(('frozen-original-N33-rejected',json.loads(archive.read('frozen-current/bindings.json')),False))
    variants.append(('current-authored-config',json.loads((HERE/'bindings.json').read_text()),True))
    for name,cfg,expected in variants:
        try:validate(cfg);accepted=True
        except (AssertionError,KeyError):accepted=False
        cases.append(dict(name=name,passed=accepted==expected))
else:cases=[dict(name='validate_config-required',passed=False)]
result=dict(kind='G02_PURE_CONFIG_SHAPE',passed=sum(c['passed'] for c in cases),cases=cases,targets=0,requests=0)
print(json.dumps(result,indent=2));raise SystemExit(0 if all(c['passed'] for c in cases) else 1)
