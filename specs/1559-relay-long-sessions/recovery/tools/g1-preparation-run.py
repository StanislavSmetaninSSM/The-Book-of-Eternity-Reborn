from pathlib import Path
import sys,subprocess,hashlib,json,datetime,time,tempfile,re
root=Path('/workspace/The-Book-of-Eternity-Reborn-1559')
mode,label,expected=sys.argv[1:4]
assert mode in {'catalog','plan'},'G1 wrapper only permits zero-execution preparation; actual/capture requires separate release'
assert label.startswith('gm1559-g1-') and '/' not in label and re.fullmatch(r'[0-9a-f]{40}',expected)
def git(*args):return subprocess.check_output(['git',*args],cwd=root)
assert git('rev-parse','HEAD').decode().strip()==expected and not git('status','--porcelain')
selection=json.loads((root/'tests/selection.json').read_text());categories=[x['category'] for x in selection['selections']]
assert len(categories)==8 and len(set(categories))==8
out=Path('/tmp/gm1559-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
paths=['BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.Tests.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.IntegrationTests.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityGMBridge.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityClient.dll']
def binaries():return [dict(path=p,sha256=hashlib.sha256((root/p).read_bytes()).hexdigest(),bytes=(root/p).stat().st_size) for p in paths if (root/p).is_file()]
binding_path=root/'specs/1559-relay-long-sessions/g1-source-packet.json';binding_raw=binding_path.read_bytes();binding=json.loads(binding_raw)
assert binding['sourcePinCount']==119
for pin in binding['sourcePins']:
 file=root/pin['path'];data=file.read_bytes() if file.is_file() else git('show','HEAD:'+pin['path'])
 assert len(data)==pin['bytes'] and hashlib.sha256(data).hexdigest()==pin['sha256'],pin['path']
before=binaries();args=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1'];compiled=expected
if mode=='catalog':
 assert len(sys.argv)==4;args+=['-ValidateCatalog']
else:
 assert len(sys.argv)==5
 prep=json.loads(Path(sys.argv[4]).read_text());assert prep['mode']=='catalog' and prep['nativeExit']==0 and prep['binaryAfter']==before and len(before)==9
 compiled=prep['sourceHead'];delta=git('diff','--name-only',compiled,expected).decode().splitlines();assert all(p.startswith('specs/1559-relay-long-sessions/') for p in delta)
 args+=['-SelectionFile','tests/selection.json','-PlanOnly','-NoBuild']
result_root=root/'TestResults/test-categories';old=set(result_root.iterdir()) if result_root.exists() else set()
prefixes=['boe-relay1559-*','boe-daemon-relay1559-*','boe-daemon-restart1559-*','boe-relay-publication-*','boe-prompt-*','boe-gameengine-turnlifecycle-*','boe-live-turn-prep-*','boe-save-load-tests-*']
def roots():return sorted(str(p) for pattern in prefixes for p in Path(tempfile.gettempdir()).glob(pattern))
prefix_before=roots();start=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic()
print('LOCAL_GM1559_G1_START',mode,expected,'log',out/'stdout.log',flush=True)
sentinel=out.parent/(expected+'-g1-'+mode+'.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_ZERO_EXECUTION_G1_PREPARATION',source=expected,phase=mode,label=label,startedUtc=start),f,indent=2)
with (out/'stdout.log').open('wb') as stream:
 process=subprocess.Popen(args,cwd=root,stdout=stream,stderr=subprocess.STDOUT)
 (out/'process.json').write_text(json.dumps(dict(pid=process.pid,argv=args,startedUtc=start),indent=2)+'\n')
 code=process.wait()
elapsed=time.monotonic()-clock;new=sorted(str(p) for p in set(result_root.iterdir())-old) if result_root.exists() else []
after=binaries();head_after=git('rev-parse','HEAD').decode().strip();status=git('status','--porcelain').decode()
j=dict(mode=mode,label=label,argv=args,cwd=str(root),sourceHead=expected,compiledSource=compiled,productSource='a82b3d8439f76f83baade6df8516e78eea86e546',startedUtc=start,finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),wallSeconds=elapsed,nativeExit=code,stdoutPath=str(out/'stdout.log'),stdoutSha256=hashlib.sha256((out/'stdout.log').read_bytes()).hexdigest(),resultDirectories=new,binaryBefore=before,binaryAfter=after,headAfter=head_after,statusAfter=status,ownedPrefixesBefore=prefix_before,ownedPrefixesAfter=roots(),helperText=Path(__file__).read_text(),runtimeScope='FRESH BOTH/catalog or exact reviewed selection PlanOnly; ZERO test/capture/host/browser execution',authority='Existing Root G1 preparation authorization; no actual permission, source/evidence verdict, retry mechanism or production release inferred.')
j.update(sourceBindingPath=str(binding_path),sourceBindingSha256=hashlib.sha256(binding_raw).hexdigest(),firstSentinel=str(sentinel),processRecord=str(out/'process.json'),actualTestsExecuted=0)
(out/'record.json').write_text(json.dumps(j,indent=2)+'\n')
print('LOCAL_GM1559_G1_EXIT',code,'wall',round(elapsed,3),'results',new,'record',out/'record.json',flush=True)
assert head_after==expected and not status
if mode=='plan':assert before==after
if code==0:assert len(after)==9 and prefix_before==j['ownedPrefixesAfter']
sys.exit(code)
