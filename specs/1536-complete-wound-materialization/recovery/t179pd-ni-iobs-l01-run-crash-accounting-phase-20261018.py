"""One diagnostic2 accounting phase, unchanged FIRST/101pins. No signal, retry or automatic advance."""
from pathlib import Path
import sys,json,subprocess,os,hashlib,datetime,time,importlib.util
sys.dont_write_bytecode=True
r=Path('/workspace/The-Book-of-Eternity-Reborn');d=r/'specs/1536-complete-wound-materialization/recovery';t=Path('/tmp/1536-cloud-tools');stem='t179pd-ni-iobs-l01-'
kind,mode,actual=sys.argv[1:4];p=json.loads((d/f'{stem}source-packet-20261018.json').read_bytes());c=json.loads((d/f'{stem}cloud-qualification-20261018.json').read_bytes());ns=c['crashDiagnostic2']['runNamespace'];root=ns['tmpdir'];suffix=ns['identity'].split('-')[-1][:8]
assert c['qualifiedCases']['totalDistinctTargets']==52 and os.environ['TMPDIR']==root and Path(root).is_dir()
assert Path(root)==Path(root).resolve() and Path(root).stat().st_uid==os.getuid()
for name,suffix_path in {'XDG_RUNTIME_DIR':'/runtime','DOTNET_CLI_HOME':'/tooling/cli','XDG_CACHE_HOME':'/tooling/cache','XDG_CONFIG_HOME':'/tooling/config','XDG_DATA_HOME':'/tooling/data'}.items():assert os.environ[name]==root+suffix_path
assert os.environ['DOTNET_ROOT']=='/tmp/1536-cloud-tools/dotnet' and os.environ['NUGET_PACKAGES']=='/tmp/1536-cloud-tools/nuget'
assert Path('/proc/sys/kernel/random/boot_id').read_text().strip()==ns['bootId']
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip()==actual and not subprocess.check_output(['git','status','--porcelain'],cwd=r)
if kind=='prep':
 release=p['namedZeroRelease'];assert release['status']=='SOURCE_ACCEPTED_SAVED_RECOVERED_DIAGNOSTIC2_ZERO3_ONLY' and mode in {'catalog','plan','diagnostic-plan'}
 label=release['labels'][mode];controller=d/f'{stem}process-preparation-run.py';args=[mode,label,actual]
 if mode!='catalog':
  catalog=Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools')/release['labels']['catalog']/'record.json';assert json.loads(catalog.read_bytes())['nativeExit']==0;args.append(str(catalog))
else:
 assert kind=='actual' and mode=='diagnostic2';release=p['namedActualRelease'];assert release['status']=='SEPARATELY_NAMED_ONE_DIAGNOSTIC2_ACCOUNTING_AFTER_SAVED_ACCEPTANCE_RECOVERY_AND_CLEAN_PREFLIGHT' and release['mode']==mode
 label=release['label'];controller=d/f'{stem}process-first-run.py';args=['diagnostic1',label,actual,release['prepRecord']]
assert not (Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools')/label).exists()
# Only read current-cloud frozen roots. This is not access to old failed66529 environment.
frozen=ns['frozenRootsOutsideMutableNamespace'];assert len(frozen)==2
archive=r/c['actuals']['crash5SelectedRouting']['archive']['path'];a=json.loads(archive.read_bytes());files={x['rawPath']:x for x in a['files'] if any(Path(x['rawPath']).is_relative_to(Path(z)) for z in frozen)}
actual_files={str(f) for z in frozen for f in Path(z).rglob('*') if f.is_file()};assert actual_files==set(files),'Frozen root file set changed'
for path,entry in files.items():
 b=Path(path).read_bytes();assert len(b)==entry['bytes'] and hashlib.sha256(b).hexdigest()==entry['sha256'],'Frozen root bytes changed'
rec=dict(kind='READ_ONLY_FROZEN_CURRENT_CLOUD_ROOTS_SEPARATION',checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),actualSource=actual,mode=mode,newMutableRoot=root,physicalCloudUnchanged=True,frozenRoots=frozen,verifiedFiles=len(files),unchanged=True,limits='Read-only current-cloud frozen fixtures only; no root/process intervention, no old66529 access, no continuous absence claim.')
pre=t/f'diagnostic2-{mode}-frozen-separation-{suffix}.json';assert not pre.exists();pre.write_text(json.dumps(rec,indent=2)+'\n')
observer=d/f'{stem}process-census-v2-20261018.py'
def census(side):
 out=t/f'census-crash-diagnostic2-{mode}-{side}-{suffix}.json';assert not out.exists();return subprocess.run([sys.executable,str(observer),'--phase',f'crash-diagnostic2-{mode}-{side}','--fixture-temp',root,'--known','10433:477854','--out',str(out)],cwd=r).returncode
assert census('before')==0,'No native on active/UNKNOWN/new mutable roots'
tools=p['diagnostic2Orchestration']
for name in ['sampler','wrapper','environmentShell','discriminators','fakeProcChecker','fakeProcProof']:
 entry=tools[name];data=(r/entry['path']).read_bytes();assert len(data)==entry['bytes'] and hashlib.sha256(data).hexdigest()==entry['sha256']
assert (r/tools['wrapper']['path']).resolve()==Path(__file__).resolve()
spec=importlib.util.spec_from_file_location('l01_readonly_accounting',r/tools['sampler']['path']);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
sampler=None
if kind=='actual':
 profile_out=t/f'diagnostic2-{mode}-accounting-{suffix}.jsonl';assert not profile_out.exists()
 # A sampler startup failure precedes and blocks native creation.
 sampler=module.Sampler(root,os.environ['DOTNET_ROOT'],r,profile_out)
def save_status(native,after,summary,errors):
 status_out=t/f'diagnostic2-{mode}-accounting-status-{suffix}.json';assert not status_out.exists()
 data=profile_out.read_bytes()
 status=dict(kind='ONE_DIAGNOSTIC2_EXTERNAL_READONLY_ACCOUNTING_STATUS_NOT_VERDICT',actualSource=actual,compiledSource=release['compiledSource'],label=label,mode=mode,controllerMode='diagnostic1',controllerExit=native,afterCensusExit=after,profile=dict(path=str(profile_out),bytes=len(data),sha256=hashlib.sha256(data).hexdigest()),summary=summary,observationErrors=errors,tools=tools,countsToward57=False,limits='One original only; preserve every gap/timeout/failure, independent causal review before any action. Never sum process plus thread CPU. No old timeout cause reconstructed.')
 with status_out.open('x')as f:json.dump(status,f,indent=2);f.write('\n')
result=module.run_one_controller(lambda:subprocess.Popen([sys.executable,str(controller),*args],cwd=r),lambda:census('after'),sampler,save_status if kind=='actual'else None)
sys.exit(result)
