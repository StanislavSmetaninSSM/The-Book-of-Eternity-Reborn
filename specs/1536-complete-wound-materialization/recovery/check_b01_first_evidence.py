#!/usr/bin/env python3
"""Read immutable B01 originals; no source imports, procfs reads or native tooling."""
import hashlib,json,pathlib,re,subprocess,zipfile
R=pathlib.Path('/workspace/The-Book-of-Eternity-Reborn'); C=pathlib.Path('/workspace/1536-cloud-clean-restore')
D=pathlib.Path('/tmp/1536-cloud-tools/review'); Q=pathlib.Path('specs/1536-complete-wound-materialization/recovery')
SHA='c20a9cf0bcb29c4dcea8853e77b390eeb7c50611'; SOURCE='d3a1ddcf7a6e2f06ae815877c46314ec81d9f0d7'
SCRIPT_SHA='b8182fb480ad46452010e31f395e9d2deac63c11d5e1e90d0c08c273e8387e6f'
checks=[]
def ck(name,value,detail=None):
 checks.append(dict(name=name,passed=bool(value),**({'detail':detail} if detail is not None else {})))
def h(b):return hashlib.sha256(b).hexdigest()
def read(p):return json.loads(p.read_bytes())
def git(root,*args):return subprocess.check_output(['git','-C',str(root),*args])
epath=Q/'l01-build-tooling-b01-first-outcome-20261018.json'; e=read(R/epath)
zpath=pathlib.Path(e['archive']['path']); zb=(R/zpath).read_bytes(); z=zipfile.ZipFile(R/zpath)
receiptpath=pathlib.Path('/tmp/1536-cloud-tools/d01-carrier-recovery-c20a9cf0.json'); receipt=read(receiptpath)
for root in (R,C):
 ck(str(root)+' HEAD',git(root,'rev-parse','HEAD').decode().strip()==SHA)
 ck(str(root)+' clean',not git(root,'status','--porcelain').strip())
ck('recovery provenance',receipt['sha']==SHA and receipt['remoteShaVerified'] and receipt['clean'] and not receipt['localWriterObjectsCopied'] and receipt['changedPaths']==124 and receipt['currentPinsVerified']==149)
changes=git(R,'diff','--name-only',SOURCE,SHA).decode().splitlines()
ck('actual to carrier delta only evidence plus plan',set(changes)=={str(epath),str(zpath),'specs/1536-complete-wound-materialization/plan.md'},changes)
ck('archive exact recorded bytes/hash',len(zb)==30155==e['archive']['bytes'] and h(zb)==e['archive']['sha256']=='dd65447562557b86fa116c8b3ef015ad1953cc800e4688f7afd92a5e05c1f675')
ck('four exact unique archive members',len(z.namelist())==4 and set(z.namelist())=={x['member'] for x in e['archive']['members']})
for p in (epath,zpath):
 b=(R/p).read_bytes(); row=next(x for x in receipt['files'] if x['path']==str(p))
 ck(str(p)+' saved/restored byte chain',b==(C/p).read_bytes()==git(R,'show',SHA+':'+str(p)) and len(b)==row['bytes'] and h(b)==row['sha256'])
for m in e['archive']['members']:
 b=z.read(m['member']);ck('raw '+m['member'],len(b)==m['bytes'] and h(b)==m['sha256'] and b==pathlib.Path(m['originalPath']).read_bytes())
s=read(pathlib.Path(e['archive']['members'][0]['originalPath'])); r=json.loads(z.read('record.json'));out=z.read('stdout.txt').decode();err=z.read('stderr.txt').decode()
sp=Q/'l01-build-tooling-unit-once-20261018.py'; sb=(R/sp).read_bytes();source_review=read(R/Q/'l01-build-tooling-b01-independent-review-20261018.json')
ck('exact admitted runner source',h(sb)==SCRIPT_SHA==e['scriptSha256']==source_review['buildScriptSha256'] and sb==git(R,'show',SOURCE+':'+str(sp))==(C/sp).read_bytes())
ck('admitted single build only',source_review['buildOnlyAdmission']=='ACCEPT_ONE_ISOLATED_UNIT_BUILD_ONLY')
p=read(R/Q/'l01-delivery-d01-source-packet-20261018.json')
ck('149 pin closure unchanged by evidence carrier',len(p['sourcePins'])==149 and not ({x['path'] for x in p['sourcePins']} & set(changes)) and p['sourcePinDigest']==receipt['sourcePinDigest']==r['sourcePinDigest']==e['sourcePinDigest'])
ck('packet unchanged from actual',git(R,'show',SOURCE+':'+str(Q/'l01-delivery-d01-source-packet-20261018.json'))==(R/Q/'l01-delivery-d01-source-packet-20261018.json').read_bytes())
ck('historical ledgers/production unchanged receipt',receipt['historicalLedgersUnchanged'] and not receipt['productionChanges'] and receipt['S01PinsVerified']==11)
ck('actual source consistency',r['source']==s['source']==e['source']==SOURCE)
ck('initial incomplete state retained',s['nativeExit'] is None and s['samples']==[] and 'wrapperPid' not in s)
ck('initial/final admitted command identical',s['command']==r['command'] and s['buildCommand']==r['buildCommand'])
cmd=r['command']; root=pathlib.Path(r['root'])
ck('unchanged 600 plus conditional10 GNU wrapper',cmd[:5]==['/usr/bin/timeout','--verbose','--signal=TERM','--kill-after=10s','600s'])
ck('one unit build command/private outputs/server/feed/doc flags',cmd[5:]==r['buildCommand'] and cmd[6:8]==['build','BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj'] and cmd[cmd.index('--artifacts-path')+1]==str(root/'artifacts') and all(x in cmd for x in ['--disable-build-servers','-p:GenerateDocumentationFile=true','-p:NoWarn=1591','-p:NuGetAudit=false','-p:RestoreSources=/tmp/1536-cloud-tools/nuget']))
ck('original native0 and observer separation',r['nativeExit']==r['rawWrapperReturnCode']==e['rawWrapperReturnCode']==e['controllerExit']==0 and r['wrapperTerminationSignal'] is None and r['observerErrors']==e['observerErrors']==[] and r['joined'] is True)
ck('elapsed under unchanged wrapper budget',0<r['durationSeconds']==e['durationSeconds']<600)
ck('no tests/game/fixtures/synthetic/diagnostic targets',all(r[k]==0 for k in ['testsExecuted','gameProcesses','fixtureTargets','syntheticRuns','diagnosticRequests']))
ck('old outcome preserved and permit consumed',r['originalCatalogNativeExit']==124 and not r['originalOutcomeReplaced'] and e['originalCatalog']==dict(nativeExit=124,unitNativeExit=137,outcomeReplaced=False) and e['permit']=='CONSUMED_FIRST_BUILD_OUTCOME_NO_RETRY')
ck('all following runtime releases NONE',all(e[k]=='NONE' for k in ['catalogPrepRelease','syntheticRelease','gameplayRelease']))
ck('dated original pre/post tool census unchanged zombies',r['beforeTools']==r['afterTools']==[dict(comm='dotnet',state='Z',count=35)] and r['afterLiveTools']==r['remainingSessionProcesses']==[])
ck('visibility limitation explicit',r['beforeVisibility']==r['afterVisibility']==dict(executableFallbacks=5,positivelyVanished=0))
a=r['samples'];ck('131 original samples',len(a)==e['observations']['samples']==131)
ck('sample monotonic order',all(x['elapsed']<y['elapsed'] and x['resources']['monotonic']<y['resources']['monotonic'] for x,y in zip(a,a[1:])) and 0<=a[0]['elapsed']<a[-1]['elapsed']<r['durationSeconds'])
rows=[q for v in a for q in v['processes']];ident=r['wrapperIdentity'];pid=r['wrapperPid']
ck('recorded wrapper identity',ident==e['terminal']['identity'] and ident['pid']==pid==102017 and ident['birthTick']==4494247 and ident['uid']==1000)
ck('every sampled row in owned session/group',all(q['session']==q['group']==pid and q['birthTick']>0 and q['identityBasis']=='same owned session' for q in rows))
ck('first sample binds direct timeout wrapper',a[0]['processes'][0]['pid']==pid and a[0]['processes'][0]['birthTick']==ident['birthTick'] and a[0]['processes'][0]['parentPid']==ident['parentPid'] and a[0]['processes'][0]['comm']=='timeout')
byid={}
for v in a:
 for q in v['processes']:byid.setdefault((q['pid'],q['birthTick']),[]).append(q)
ck('per-identity CPU ticks nondecreasing',all(all(x['cpuTicks']<=y['cpuTicks'] for x,y in zip(v,v[1:])) for v in byid.values()))
ck('no sampled same PID changes birth',all(len({birth for pp,birth in byid if pp==pid1})==1 for pid1,b in byid))
resources=[r['beforeResources']]+[v['resources'] for v in a]+[r['afterResources']]
ck('resource scope identity stable',all((v['cgroupMembership'],v['cgroupMount'],v['mountNode'])==(resources[0]['cgroupMembership'],resources[0]['cgroupMount'],resources[0]['mountNode']) for v in resources))
ck('scope retains /.. mapping caveat',resources[0]['cgroupMembership']=='0::/' and ' /.. /sys/fs/cgroup ' in resources[0]['cgroupMount'] and all('unproved' in v['counterScope'] for v in resources))
def counters(text):return {k:int(v) for k,v in map(str.split,text.splitlines())}
deltas={k:{n:v-counters(r['beforeResources']['cgroup'][k])[n] for n,v in counters(r['afterResources']['cgroup'][k]).items()} for k in ('cpu.stat','memory.events')}
metrics=dict(samples=len(a),maximumGapSeconds=max(y['elapsed']-x['elapsed'] for x,y in zip(a,a[1:])),maximumObservedSingleProcessRssBytes=max(q['rssBytes'] for q in rows),maximumObservedSumOfSessionRssBytes=max(sum(q['rssBytes'] for q in v['processes']) for v in a),maximumObservedSharedNodeMemoryCurrentBytes=max(int(v['resources']['cgroup']['memory.current']) for v in a),childrenRusage=r['childrenRusage'],sharedNodeCounterDeltas=deltas)
for k,v in metrics.items():ck('recomputed '+k,v==e['observations'][k],v)
ck('no observed mounted-node memory event increments',all(v==0 for v in deltas['memory.events'].values()))
ck('stdout succeeds exactly once',out.count('Build succeeded.')==1 and re.search(r'^\s*94 Warning\(s\)\s*$',out,re.M) is not None and re.search(r'^\s*0 Error\(s\)\s*$',out,re.M) is not None)
ck('stderr no build error or timeout termination', 'Welcome to .NET 10.0!' in err and 'sending signal' not in err and not re.search(r'\berror\b',err,re.I))
projects=out.split('Project Performance Summary:')[1].split('Target Performance Summary:')[0]
for v in e['msbuildPerformance']['projectSummary']:
 ck('project inclusive statistic '+pathlib.Path(v['project']).stem,re.search(r'\b'+str(v['milliseconds'])+r' ms\s+'+re.escape(v['project'])+r'\s+'+str(v['calls'])+r' calls',projects) is not None)
targets=out.split('Target Performance Summary:')[1].split('Task Performance Summary:')[0];tasks=out.split('Task Performance Summary:')[1]
for v in e['msbuildPerformance']['taskSummary']:
 ck(('target ' if v['task']=='CoreCompile' else 'task ')+v['task'],re.search(r'\b'+str(v['milliseconds'])+r' ms\s+'+re.escape(v['task'])+r'\s+'+str(v['calls'])+r' calls',targets if v['task']=='CoreCompile' else tasks) is not None)
ck('build reported elapsed retained separately','Time Elapsed 00:04:41.67' in out)
ck('exact five expected primary outputs',len(e['outputs'])==5 and {x['project'] for x in e['outputs']}=={'BookOfEternityClient','BookOfEternityGMBridge','BookOfEternityClient.TestSupport','PortableStorageCrashHost','BookOfEternityClient.Tests'})
for x in e['outputs']:
 path=pathlib.Path(x['path']);b=path.read_bytes();ck('private physical DLL '+x['project'],path.is_relative_to(root/'artifacts/bin') and len(b)==x['bytes'] and h(b)==x['sha256'] and str(path) in out)
oldz=zipfile.ZipFile(R/Q/'l01-delivery-d01-first-prep-failure-originals-20261018.zip');old=json.loads(oldz.read('phase/record.json'))['binaryInventory']
current=[]
for project in ['BookOfEternityClient.IntegrationTests','BookOfEternityClient.Tests']:
 for file in sorted((R/project/'bin/Debug/net8.0').rglob('*')):
  if file.is_file():
   b=file.read_bytes();current.append(dict(path=str(file.relative_to(R)),bytes=len(b),sha256=h(b)))
ck('all899 normal-bin file paths and bytes unchanged',len(current)==len(old)==899 and sorted(current,key=lambda x:x['path'])==sorted(old,key=lambda x:x['path']))
ck('normal binaries not claimed new PREP',e['oldNormalInventory']['count']==899 and 'not catalog' in e['conclusion'])
result=dict(kind='B01_READ_ONLY_FIRST_EVIDENCE_CHECKS',candidate=SHA,actualSource=SOURCE,reviewerActions='Static files/ZIP/Git and private binary hash reads only; no procfs, SDK, build, test, game or signals.',checks=checks,passed=sum(x['passed'] for x in checks),failed=[x for x in checks if not x['passed']],metrics=metrics,rawArchiveSha256=h(zb),evidenceIndexSha256=h((R/epath).read_bytes()),recoveryReceiptSha256=h(receiptpath.read_bytes()),checkerSha256=h(pathlib.Path(__file__).read_bytes()),normalInventoryCount=len(current),sampledIdentityCount=len(byid))
p=D/'l01-build-tooling-b01-evidence-checks-20261018.json';p.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['passed','failed','normalInventoryCount','sampledIdentityCount']}));print('checks sha256',h(p.read_bytes()))
