"""Pure exact-role regression; AST extraction executes no watchdog/native/child code."""
from pathlib import Path
import ast
import json

source=Path(__file__).resolve().parent.parent/'l01-game-capture-g01/watchdog.py'
tree=ast.parse(source.read_text()); nodes=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='request_role']
cases=[]
if nodes:
    ns={'Path':Path};exec(compile(ast.Module(body=nodes,type_ignores=[]),str(source),'exec'),ns)
    fn=ns['request_role']
    cfg=dict(Dotnet='/dotnet',Runtimeconfig='/r',Depsfile='/d',SupportAssembly='/s',IntegrationAssembly='/i',OracleAssembly='/oracle.dll',OracleRuntimeconfig='/oracle.runtimeconfig.json',OracleContract='S01_G01_PREAPI_ZERO_REQUESTS')
    m=dict(Own='/own',Root='/own/tmp/root',Cut=1,Nonce='a'*32)
    real=['/dotnet','exec','--runtimeconfig','/r','--depsfile','/d','/s','engine-joint-interface-resume','/i',m['Root'],'/own/resume.json','1','/own']
    oracle=['/dotnet','exec','--runtimeconfig','/oracle.runtimeconfig.json','/oracle.dll','a'*32]
    for name,flag,command,contract,accept in [('real',False,real,cfg['OracleContract'],True),('synthetic',True,oracle,cfg['OracleContract'],True),('missing',None,real,cfg['OracleContract'],False),('string','false',real,cfg['OracleContract'],False),('wrong-contract',True,oracle,'WRONG',False),('synthetic-real-argv',True,real,cfg['OracleContract'],False),('real-synthetic-argv',False,oracle,cfg['OracleContract'],False)]:
        c=dict(cfg,OracleContract=contract)
        if flag is not None:c['SyntheticPreApiOracle']=flag
        try:fn(c,dict(m,TargetCommand=command));accepted=True
        except (AssertionError,KeyError):accepted=False
        cases.append(dict(name=name,passed=accepted==accept))
else:cases=[dict(name='request_role-required',passed=False)]
result=dict(kind='G02_PURE_ROLE',passed=sum(c['passed'] for c in cases),cases=cases,processes=0,requests=0)
print(json.dumps(result,indent=2));raise SystemExit(0 if all(c['passed'] for c in cases) else 1)
