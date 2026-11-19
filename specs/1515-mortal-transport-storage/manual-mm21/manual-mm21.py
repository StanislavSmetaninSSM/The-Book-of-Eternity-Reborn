import pathlib,json,hashlib,shutil,os,sys,importlib.util,time,subprocess
def require_current_producer_contract(b,src):
 # Finite reviewed Python/document preparation changes, never compiler/resource paths.
 allowed={
  'tests/fixtures/SelectionPrompt/test_fullroute_inputs.py',
  'tests/fixtures/SelectionPrompt/smoke.py','tests/fixtures/SelectionPrompt/fullroute_inputs.py',
  'tests/fixtures/ProductionMain/live-mortal-phase.py','tests/fixtures/ProductionMain/test-live-terminal-fences.py',
  'tests/fixtures/ProductionMain/relay_phase_evidence.py','tests/fixtures/ProductionMain/test-mortal-phase-evidence.py',
  'specs/1515-mortal-transport-storage/t034-physical-lease-observation-plan.md',
  'specs/1515-mortal-transport-storage/manual-mm21/test-submit-mm21-data.py',
  'specs/1515-mortal-transport-storage/manual-mm21/prepare-mm21-profile.py',
  'specs/1515-mortal-transport-storage/manual-mm21/manual-mm21.py'}
 additions_allowed={'specs/1515-mortal-transport-storage/manual-mm21/publish-current.py','specs/1515-mortal-transport-storage/manual-mm21/publish-current.sh'}
 owning=b['owningBuildSourcePins']
 if set(owning)-set(src):raise ValueError('Owning source removed')
 changes={p:src[p] for p in owning if src[p]!=owning[p]}
 additions={p:src[p] for p in set(src)-set(owning)}
 if changes!=b['allowedNonCompilerSourceChanges'] or not set(changes)<=allowed:
  raise ValueError('Unreviewed owning-source changes')
 if additions!=b['allowedNonCompilerSourceAdditions'] or not set(additions)<=additions_allowed:
  raise ValueError('Unreviewed owning-source additions')
 if b['outputPins']!=b['producerOutputPins']:raise ValueError('Current producer must equal current owning outputs')
 if b['contentOverlays']!=[]:raise ValueError('Current producer accepts zero overlays only')

def checked_tools():
 expected={'dotnet','pwsh','python3','cc','readelf','git'}
 if set(b['toolPins'])!=expected:raise ValueError('Exact preparation tool set required')
 for name,row in b['toolPins'].items():
  path=shutil.which(name)
  if path is None or str(pathlib.Path(path).resolve(strict=True))!=row['path'] or h(pathlib.Path(row['path']))!=row['sha256']:
   raise ValueError('Preparation tool path/hash drift: '+name)

def require_published_rows(rows,expected):
 actual={row['path']:{'sha256':row['sha256'],'mode':row['mode']} for row in rows}
 if len(actual)!=len(rows) or actual!=expected:raise ValueError('Published hash/mode membership drift')

os.environ.update(POWERSHELL_TELEMETRY_OPTOUT='1',POWERSHELL_UPDATECHECK='Off',DOTNET_CLI_TELEMETRY_OPTOUT='1')
R=pathlib.Path('/workspace/shared/mortal-isolated-20261119');h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
if len(sys.argv)!=3:raise ValueError('Reviewed input manifest and new destination required')
input_path=pathlib.Path(sys.argv[1]);D=pathlib.Path(sys.argv[2])
for path in (input_path,D):
 if not path.is_absolute() or path!=path.resolve() or any(x.is_symlink() for x in [path,*path.parents]):raise ValueError('Canonical unlinked input/destination required')
import re
if D.parent!=pathlib.Path('/workspace/shared') or re.fullmatch(r'mm[0-9]{2}-[0-9]{8}',D.name) is None:raise ValueError('Bounded direct workspace destination required')
input_bytes=input_path.read_bytes()
if hashlib.sha256(input_bytes).hexdigest()!=os.environ['MM_REVIEWED_INPUT_SHA256']:raise ValueError('Reviewed input hash differs')
b=json.loads(input_bytes);assert b['destination']==str(D)
E=input_path.parent;serial=E;producer=pathlib.Path(b['immutableProducerRoot']);old=pathlib.Path(b['nativeDirectory'])
for path in (producer,old):
 assert path.is_absolute() and path==path.resolve() and not any(x.is_symlink() for x in [path,*path.parents]) and path.is_dir()
producer_bytes=(producer/'published.json').read_bytes()
assert hashlib.sha256(producer_bytes).hexdigest()==b['currentProducerPublishedSHA256']
current_producer=json.loads(producer_bytes)
assert h(producer/'outcome.json')==b['currentProducerOutcomeSHA256']
assert h(producer/'logs/publish-BookOfEternityClient.TestSupport.outcome.json')==b['currentProducerHelperPublishOutcomeSHA256']
assert not os.path.lexists(D)
# Enumerate the same owning-build source universe BEFORE importing checkout code.
def source_inventory():
 import stat
 excluded={'.git','bin','obj','TestResults','node_modules','__pycache__'}
 if any(p.is_symlink() for p in [R,*R.parents]):raise ValueError('Linked checkout root')
 rows={};pending=[R]
 while pending:
  for path in pending.pop().iterdir():
   if path.name in excluded:continue
   mode=path.lstat().st_mode
   if stat.S_ISDIR(mode):pending.append(path)
   elif stat.S_ISREG(mode):rows[path.relative_to(R).as_posix()]=h(path)
   else:raise ValueError('Linked/special source refused: '+str(path))
 return rows
for name in b['sourcePins']:
 member=pathlib.PurePosixPath(name)
 assert name and not member.is_absolute() and str(member)==name and '..' not in member.parts and '\\' not in name
src=source_inventory();assert src==b['sourcePins'],'Source membership/hash drift'
require_current_producer_contract(b,src)
checked_tools()
assert src['tests/fixtures/ProductionMain/live-mortal-phase.py']==os.environ['MM21_REVIEWED_DRIVER_SHA256']
def checked_output(name,digest):
 member=pathlib.PurePosixPath(name)
 assert name and not member.is_absolute() and str(member)==name and '..' not in member.parts and '\\' not in name
 path=R
 for part in member.parts:
  path=path/part
  assert not path.is_symlink(),'Linked output'
 assert path.is_file() and h(path)==digest
def output_inventory():
 import stat
 rows={}
 for project in ('BookOfEternityClient','BookOfEternityGMBridge','BookOfEternityClient.TestSupport'):
  root=R/project/'bin/Debug/net8.0';pending=[root]
  assert not any(x.is_symlink() for x in [root,*root.parents])
  while pending:
   for p in pending.pop().iterdir():
    mode=p.lstat().st_mode
    if stat.S_ISDIR(mode):pending.append(p)
    elif stat.S_ISREG(mode):rows[p.relative_to(R).as_posix()]=h(p)
    else:raise ValueError('Linked/special owning output')
 return rows
assert output_inventory()==b['outputPins'],'Owning output membership/hash drift'
assert len(b['unitOutputsCorroboration'])==8
for name,digest in b['outputPins'].items():checked_output(name,digest)
for name,digest in b['unitOutputsCorroboration'].items():checked_output(name,digest)
sys.path.insert(0,str(R/'tests/fixtures/ProductionMain'));s=importlib.util.spec_from_file_location('prep',R/'tests/fixtures/ProductionMain/prepare-mortal-live.py');m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
for tool in ('dotnet','pwsh','cc','readelf','git'):assert shutil.which(tool),tool
assert pathlib.Path('/usr/bin/python3').is_file() and pathlib.Path(os.environ['DOTNET_ROOT'],'dotnet').is_file()
native_all=m.native_join(R,old,b['nativeBuiltSourceCommit']);native=[row for row in native_all if row['path'] in b['nativeRows']]
assert len(native)==4 and all(row['sha256']==b['nativeRows'][row['path']]['sha256'] and row['mode']==b['nativeRows'][row['path']]['mode'] for row in native)
guardian=m.guardian_tool(R,b['guardianTool'])
extra='runtimes/win/lib/net8.0/System.Diagnostics.EventLog.Messages.dll';nuget=pathlib.Path('/workspace/shared/isolated-restore-20261119/packages/system.diagnostics.eventlog/8.0.0')/extra;assert h(nuget)=='68f73ab17fb7f4aff3d35ef6db0e9d5b0fa0151111cb3d03992e23bc29d6c40a'
assets=R/'BookOfEternityClient.TestSupport/obj/project.assets.json';a=json.loads(assets.read_text());targets=[v['System.Diagnostics.EventLog/8.0.0']['runtimeTargets'][extra] for v in a['targets'].values() if 'System.Diagnostics.EventLog/8.0.0' in v];assert targets and all(x=={'assetType':'runtime','rid':'win'} for x in targets)
packages={project:producer/'ship'/project for project in ('BookOfEternityClient','BookOfEternityGMBridge','BookOfEternityClient.TestSupport')};verified={}
for project,path in packages.items():
 prefix=project+'/bin/Debug/net8.0/';expected={n[len(prefix):]:v for n,v in b['producerOutputPins'].items() if n.startswith(prefix)}
 for n in ('boe-lineage-supervisor','package-manifest.json'):expected['runtimes/linux-x64/native/'+n]=h(old/n)
 assert extra not in expected;expected[extra]=h(nuget)
 assetdata=json.loads((R/project/'obj/project.assets.json').read_text());matches=[v['System.Diagnostics.EventLog/8.0.0']['runtimeTargets'][extra] for v in assetdata['targets'].values() if 'System.Diagnostics.EventLog/8.0.0' in v];assert matches and all(x=={'assetType':'runtime','rid':'win'} for x in matches)
 rows=m.inventory(path);actual={x['path']:x['sha256'] for x in rows};assert actual==expected,(project,sorted(set(actual)-set(expected)),sorted(set(expected)-set(actual)))
 require_published_rows(rows,current_producer['packages'][project]['files'])
 verified[project]=rows
overlays=b['contentOverlays'] # Exactly empty, checked before checkout imports.
import relay_phase_evidence as pe
terminal_rows=[]
for name in pe.TERMINAL_SOURCE_FILES:
 p=m.safe_member(R/'tests/fixtures',name);terminal_rows.append({'path':name,'sha256':h(p),'mode':p.stat().st_mode&0o777})
deps=pathlib.Path('/workspace/shared/relay-alternative/mortal-t034/terminal-deps');provenance=json.loads((R/'tests/fixtures/SelectionPrompt/terminal/provenance.json').read_text());expected_deps={n:v for a in provenance.values() for n,v in a['files'].items()}
dep_rows=[dict(row,path='node_modules/'+row['path']) for row in m.inventory(deps/'node_modules')];assert {r['path']:r['sha256'] for r in dep_rows}==expected_deps and len(dep_rows)==17
node=pathlib.Path(os.environ['BOE_TERMINAL_NODE']);assert str(node)==os.environ['BOE_TERMINAL_NODE'] and node.is_absolute() and h(node)==pe.TERMINAL_NODE_SHA256
relay=m.inventory(R/'tools/gm-relay');exportinput=m.inventory(R/'FileSystemExample/game_session');os.umask(0o077);D.mkdir(mode=0o700);(D/'logs').mkdir();(D/'p01').mkdir();start=time.monotonic()
def copy_exact(source,target,rows):
 target.mkdir(parents=True)
 for row in rows:
  p=m.safe_member(source,row['path']);assert h(p)==row['sha256'];q=target/row['path'];q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());q.chmod(0o700 if row['mode']&0o111 else 0o600);assert h(q)==row['sha256']
copy_exact(old,D/'native',native)
for project,rows in verified.items():copy_exact(packages[project],D/'receipt-helper' if project.endswith('TestSupport') else D/'package'/project,rows)
for project,rows in verified.items():
 target=D/'receipt-helper' if project=='BookOfEternityClient.TestSupport' else D/'package'/project
 expected={x['path']:(x['sha256'],0o700 if x['mode']&0o111 else 0o600) for x in rows}
 assert {x['path']:(x['sha256'],x['mode']) for x in m.inventory(target)}==expected,'Final package inventory differs from exact current producer'
copy_exact(R/'tools/gm-relay',D/'p01/relay',relay)
copy_exact(R/'tests/fixtures',D/'p01/terminal-fixture',terminal_rows)
copy_exact(deps,D/'p01/terminal-deps',dep_rows)
inputrecord={'kind':'manual-standard-preparation-not-T028-helper','sourceCommit':b['sourceCommit'],'publishedCheckpoint':os.environ['MM21_REVIEWED_CHECKPOINT'],'sourcePins':src,'outputPins':b['outputPins'],'unitOutputsCorroboration':b['unitOutputsCorroboration'],'owningBuildManifestSHA256':hashlib.sha256(input_bytes).hexdigest(),'nativeSourceRows':native,'publishedSourceRows':verified,'contentOverlays':overlays,'destination':str(D),'exportInput':exportinput,'publishOnlyDependency':{'path':extra,'source':str(nuget),'sha256':h(nuget),'projectAssetsSHA256':h(assets),'runtimeTarget':targets,'actualPublishOutcomeSHA256':h(producer/'logs/publish-BookOfEternityClient.TestSupport.outcome.json'),'currentProducerPublishedSHA256':b['currentProducerPublishedSHA256'],'depsPins':{p.name:h(p) for p in packages['BookOfEternityClient.TestSupport'].glob('*.deps.json')},'depsNote':'Exact deps files retained; EventLog.Messages is a win runtimeTarget in restore assets, not separately named in these deps.'},'bootstrap':b['guardianTool']}
(D/'preparation-inputs.json').write_text(json.dumps(inputrecord,indent=2)+'\n')
argv=[shutil.which('dotnet'),str(D/'receipt-helper/BookOfEternityClient.TestSupport.dll'),'mortal-live-seed-export',str(D/'play'),str(R/'FileSystemExample/game_session'),str(D/'seed-manifest.json')]
result=m.run_owned(argv,R,D/'logs','seed-export',guardian,180);assert result['exitCode']==0,result
seed=json.loads((D/'seed-manifest.json').read_text());assert seed['acceptedGmTurn'] is False and seed['setupTurn']==41 and seed['validationIssues']==0,seed
assert m.inventory(R/'FileSystemExample/game_session')==exportinput
session=D/'play/game_session';assert not(session/'_runtime').exists();copy_exact(D/'package',session/'_runtime',m.inventory(D/'package'))
for n in ('empty-user.npmrc','empty-global.npmrc'):(D/'p01'/n).write_bytes(b'')
m.private_tree(D);driver=R/'tests/fixtures/ProductionMain/live-mortal-phase.py';pins={'RuntimePins':m.relay_inventory(session/'_runtime'),'RelayPins':m.relay_inventory(D/'p01/relay'),'ReceiptHelperPins':m.relay_inventory(D/'receipt-helper'),'DriverSHA256':h(driver),'EvidenceModuleSHA256':h(driver.with_name('relay_phase_evidence.py')),'ConsoleRouteModuleSHA256':h(driver.with_name('mortal_console_routes.py'))};pins.update(TerminalFixturePins=m.relay_inventory(D/'p01/terminal-fixture'),TerminalFixtureModes={r['path']:r['mode'] for r in m.inventory(D/'p01/terminal-fixture')},TerminalDependencyPins=m.relay_inventory(D/'p01/terminal-deps'),TerminalDependencyModes={r['path']:r['mode'] for r in m.inventory(D/'p01/terminal-deps')},TerminalNodePath=str(node),TerminalNodeSHA256=h(node));pe.terminal_package_preflight(D/'p01',pins);(D/'p01/relay-phase-package.json').write_text(json.dumps(pins,indent=2)+'\n');m.relay_package_preflight(D/'p01',session/'_runtime',driver);m.require_native_bootstrap_path_budget(D/'p01/tmp')
assert not(D/'play/.boe_runtime/gm-runs').exists() and not(D/'p01/queue').exists()
for n in ('input','output','ready'):assert not any((session/n).iterdir()) if (session/n).exists() else True
for n in ('pending_turn_snapshot','pending_turn_snapshot.json','pending_turn_snapshot.authority.json','gm_bridge_status.json','validation_repair_request.json'):assert not(session/'game_state/control'/n).exists()
assert input_path.read_bytes()==input_bytes
assert h(producer/'published.json')==b['currentProducerPublishedSHA256']
assert h(producer/'outcome.json')==b['currentProducerOutcomeSHA256']
assert h(producer/'logs/publish-BookOfEternityClient.TestSupport.outcome.json')==b['currentProducerHelperPublishOutcomeSHA256']
assert output_inventory()==b['outputPins']
assert source_inventory()==src
checked_tools()
for project,path in packages.items():require_published_rows(m.inventory(path),current_producer['packages'][project]['files'])
for name,digest in b['outputPins'].items():checked_output(name,digest)
for name,digest in b['unitOutputsCorroboration'].items():checked_output(name,digest)
manifest={'kind':'manual-standard-preparation','prepared':True,'acceptedGmTurn':False,'elapsedSeconds':time.monotonic()-start,'inputSHA256':h(D/'preparation-inputs.json'),'seedManifestSHA256':h(D/'seed-manifest.json'),'packageSHA256':h(D/'p01/relay-phase-package.json'),'seed':seed,'inventories':{n:m.inventory(p) for n,p in {'native':D/'native','runtime':D/'package','deployed':session/'_runtime','helper':D/'receipt-helper','relay':D/'p01/relay','world':session,'terminal':D/'p01/terminal-fixture','terminalDeps':D/'p01/terminal-deps'}.items()},'exportOutcome':result,'sourceOutputUnchanged':True,'OldWorldReadOrCopied':False,'NoGameOrRelayLaunched':True}
(D/'preparation-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');print(json.dumps({'manifest':str(D/'preparation-manifest.json'),'sha256':h(D/'preparation-manifest.json'),'counts':{n:len(v) for n,v in manifest['inventories'].items()},'seconds':manifest['elapsedSeconds']},indent=2))
