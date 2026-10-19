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
    cfg=dict(Dotnet='/dotnet',Runtimeconfig='/r',Depsfile='/d',SupportAssembly='/s',IntegrationAssembly='/i',OracleAssembly='/oracle.dll',OracleRuntimeconfig='/oracle.runtimeconfig.json',OracleContract='S01_G01_ONE_CAPTURE')
    m=dict(Own='/own',Root='/own/tmp/root',Cut=1,Nonce='a'*32)
    real=['/dotnet','exec','--runtimeconfig','/r','--depsfile','/d','/s','engine-joint-interface-resume','/i',m['Root'],'/own/resume.json','1','/own']
    oracle=['/dotnet','exec','--runtimeconfig','/oracle.runtimeconfig.json','/oracle.dll','a'*32]
    for name,flag,command,contract,accept in [('real',False,real,cfg['OracleContract'],True),('synthetic',True,oracle,cfg['OracleContract'],True),('missing',None,real,cfg['OracleContract'],False),('string','false',real,cfg['OracleContract'],False),('wrong-contract',True,oracle,'WRONG',False),('synthetic-real-argv',True,real,cfg['OracleContract'],False),('real-synthetic-argv',False,oracle,cfg['OracleContract'],False)]:
        c=dict(cfg,OracleContract=contract)
        if flag is not None:c['SyntheticOracle']=flag
        try:fn(c,dict(m,TargetCommand=command));accepted=True
        except (AssertionError,KeyError):accepted=False
        cases.append(dict(name=name,passed=accepted==accept))
else:cases=[dict(name='request_role-required',passed=False)]
# Inert admission negatives execute the actual validator function extracted from source.
import copy
phase=source.parent/'phase.py';pt=ast.parse(phase.read_text())
validators=[n for n in pt.body if isinstance(n,ast.FunctionDef) and n.name=='validate_synthetic']
if validators:
    ns={};exec(compile(ast.Module(body=validators,type_ignores=[]),str(phase),'exec'),ns);validator=ns['validate_synthetic']
    valid=dict(sourcePinDigest='digest',status='SYNTHETIC_CAPTURE_PASSED_API1',nativeExit=0,observerErrors=[],afterLiveTools=[],decodedOracleFrames=[1,2,3,4],syntheticOutcome=dict(runId='run',status='SYNTHETIC_CAPTURE_PASSED_API1',observerErrors=[],apiCalls=1,watchdog=dict(apiOutcome='RETURNED',diagnosticRequests=1),targetNativeExit=0,watchdogNativeExit=0,targetJoined=True,watchdogJoined=True,stdoutEof=True,stderrEof=True,done=dict(ValidStop=True)))
    variants=[('exact-success',(),None,True),('wrong-source',('sourcePinDigest',),'other',False),('wrong-run',('syntheticOutcome','runId'),'other',False),('native-fail',('nativeExit',),1,False),('api0',('syntheticOutcome','apiCalls'),0,False),('missing-frame',('decodedOracleFrames',),[1,2,3],False),('no-eof',('syntheticOutcome','stdoutEof'),False,False),('missing-outcome',('syntheticOutcome',),None,False)]
    for name,keys,value,expected in variants:
        record=copy.deepcopy(valid)
        if keys:
            parent=record
            for key in keys[:-1]:parent=parent[key]
            if value is None:del parent[keys[-1]]
            else:parent[keys[-1]]=value
        try:validator(record,dict(syntheticRunId='run'),'digest');accepted=True
        except (AssertionError,KeyError):accepted=False
        cases.append(dict(name=name,passed=accepted==expected))
else:cases.append(dict(name='validate_synthetic-required',passed=False))
result=dict(kind='G02_PURE_ROLE_AND_ADMISSION',passed=sum(c['passed'] for c in cases),cases=cases,processes=0,requests=0)
print(json.dumps(result,indent=2));raise SystemExit(0 if all(c['passed'] for c in cases) else 1)
