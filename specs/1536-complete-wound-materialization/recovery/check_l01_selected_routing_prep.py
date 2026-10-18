#!/usr/bin/env python3
"""Reusable read-only selected-routing PREP evidence checker. Run only after saved/recovered ZERO4. No native/controller/observer invocation."""
from pathlib import Path
import collections, datetime, gzip, hashlib, json, os, re, subprocess, sys

R=Path('/workspace/The-Book-of-Eternity-Reborn'); C=Path('/workspace/1536-cloud-clean-restore'); D=Path('/tmp/1536-cloud-tools/review')
REC='specs/1536-complete-wound-materialization/recovery/'
assert len(sys.argv)>=3, 'Usage: checker.py SAVED_PREP_CARRIER COMPILED_SOURCE [CONTINUOUS_RECOVERY_RECEIPTS...]'
HEAD=sys.argv[1]; assert re.fullmatch(r'[0-9a-f]{40}',HEAD)
COMPILED=sys.argv[2]; assert re.fullmatch(r'[0-9a-f]{40}',COMPILED)
assert COMPILED=='c391c03012e48c4e2395bdb288f0d763d2c0cf7f', 'Current SOURCE accepted build only'
def h(b): return hashlib.sha256(b).hexdigest()
def blob(b): return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def read(p): return json.loads(p.read_bytes())
def git(root,*a): return subprocess.check_output(['git',*a],cwd=root,env={**os.environ,'GIT_OPTIONAL_LOCKS':'0'})
def tree(root,rev):
 result={}
 for row in git(root,'ls-tree','-rz',rev).split(b'\0'):
  if row:
   meta,path=row.split(b'\t',1);result[path.decode()]=tuple(meta.decode().split())
 return result

for root in (R,C):
 assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain')
assert git(C,'ls-remote','origin','refs/heads/1536-wound-b2-cloud-20261012').decode().split()[0]==HEAD
assert not (C/'.git/objects/info/alternates').exists()
assert 'github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn' in git(C,'remote','get-url','origin').decode()
assert not git(R,'diff','--name-only',COMPILED,HEAD,'--','.',':!specs/1536-complete-wound-materialization')
assert not git(R,'diff','--check',COMPILED,HEAD)
rt,ct=tree(R,HEAD),tree(C,HEAD);assert rt==ct
cloud=read(R/(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
prep=cloud['selectedResumeRoutingCorrection']['freshPreparation']; packet=read(R/(REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'))
source_bytes=git(R,'show',COMPILED+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'); compiled_packet=json.loads(source_bytes)
assert compiled_packet['namedZeroRelease']['status']=='SOURCE_ACCEPTED_SAVED_RECOVERED_ZERO4_ONLY' and not compiled_packet['namedZeroRelease']['actualRelease']
assert compiled_packet['namedActualRelease']['mode']is None
assert packet['sourcePins']==compiled_packet['sourcePins'] and len(packet['sourcePins'])==99
assert len(packet['productDelta'])==23 and packet['productDelta']==git(R,'diff','--name-only',packet['productBase'],HEAD,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()
for helper in ['t179pd-ni-iobs-l01-process-preparation-run.py','t179pd-ni-iobs-l01-process-first-run.py']:assert "len(binding['productDelta'])==23"in (R/(REC+helper)).read_text()
assert h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']==prep['sourcePinDigest']=='70aa551ca427bb9c28b18e205bcbe2e10d1ebb88487309bb73b4a31426e37eb5'
for x in packet['sourcePins']:
 b=(R/x['path']).read_bytes();assert b==(C/x['path']).read_bytes()
 assert (len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob'])
 assert rt[x['path']]==ct[x['path']]==(x['mode'],'blob',x['gitBlob'])
ledger_bytes=(R/packet['ledger']['path']).read_bytes();assert h(ledger_bytes)==packet['ledger']['sha256']
for x in cloud['selectedResumeRoutingCorrection']['sourceAcceptance']['artifacts']:
 b=(R/x['path']).read_bytes();assert (len(b),h(b))==(x['bytes'],x['sha256'])
for path in ['AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md']:
 assert (R/path).read_bytes()==(C/path).read_bytes()==git(R,'show',COMPILED+':'+path)

ap=R/prep['archive']['path'];ab=ap.read_bytes();assert h(ab)==prep['archive']['sha256']
assert prep['archive']['path']==REC+'t179pd-ni-iobs-l01-cloud-selected-routing-preparation-813f1057-files-20261018/archive.json'
a=json.loads(ab);assert len(a['files'])==a['rawChainCount']==43
assert a['actualSources']==a['compiledSources']==[COMPILED]*4 and a['nativeExits']==[0]*4
assert a['helperText']==(R/(REC+'t179pd-ni-iobs-l01-archive-whole.py')).read_text()
raws={}
for x in a['files']:
 assert x['rawPath'] not in raws
 stored=ap.parent/x['storedPath'];sb=stored.read_bytes();assert sb==(C/stored.relative_to(R)).read_bytes()
 assert (len(sb),h(sb))==(x['storedBytes'],x['storedSha256'])
 raw=gzip.decompress(sb);assert (len(raw),h(raw))==(x['bytes'],x['sha256'])
 assert raw==Path(x['rawPath']).read_bytes();raws[x['rawPath']]=raw

expected=set();records={};summaries={};plans={};record_checks=[];process_checks=[];builds={}
for item in prep['records']:
 path=Path(item['path']);r=read(path);mode=r['mode'];assert mode not in records;records[mode]=r
 assert r['label']==compiled_packet['namedZeroRelease']['labels'][mode]
 assert h(path.read_bytes())==item['sha256']
 assert r['actualSource']==r['compiledSource']==r['headAfter']==COMPILED and not r['statusAfter']
 assert r['nativeExit']==r['actualTestsExecuted']==0 and r['kind']=='FIRST_NI_IOBS_L01_WHOLE_ZERO_PREPARATION_RAW'
 assert r['helperSource']==(R/(REC+'t179pd-ni-iobs-l01-process-preparation-run.py')).read_text()
 assert r['sourcePinDigest']==packet['pinDigest'] and r['sourcePinCount']==99
 assert r['sourceBindingSha256']==h(source_bytes) and (path.parent/'source-binding.json').read_bytes()==source_bytes
 assert r['ledgerContextSha256']==h(ledger_bytes) and (path.parent/'ledger-context.json').read_bytes()==ledger_bytes
 assert r['namespaceBefore']==r['namespaceAfter']=={'ownedRoots':[]}
 # Preserve historical fields; do NOT infer complete process absence from these arrays.
 assert r['processesBefore']==r['processesAfter']==[]
 assert h(Path(r['stdoutPath']).read_bytes())==r['stdoutSha256']
 proc=read(Path(r['processRecord']));assert proc['argv']==r['argv'] and proc['startedUtc']==r['startedUtc']
 pid=proc['identity']['pid'];rawstat=proc['identity']['stat'];tick=int(rawstat.rsplit(')',1)[1].split()[19])
 assert int(rawstat.split(' ',1)[0])==pid and '(pwsh)' in rawstat and tick>0
 cmdline=proc['identity'].get('cmdline','')
 if cmdline:assert 'pwsh' in cmdline and '-File scripts/test-csharp.ps1' in cmdline
 assert any('-'+str(pid)+'-' in p for p in r['resultDirectories'])
 process_checks.append(dict(mode=mode,pid=pid,startTick=tick,cmdlineCaptured=bool(cmdline),launchAndResultDirectoryBound=True))
 sentinel=read(Path(r['firstSentinel']));assert sentinel['mode']==mode and sentinel['actualSource']==COMPILED and sentinel['label']==r['label'] and sentinel['startedUtc']==r['startedUtc']
 for p in [path,Path(r['stdoutPath']),Path(r['processRecord']),Path(r['firstSentinel']),path.parent/'source-binding.json',path.parent/'ledger-context.json']:expected.add(str(p))
 for name in ['observed-plan.json','exact-selected-scope.json']:
  if (path.parent/name).is_file():expected.add(str(path.parent/name))
 assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0])
 expected.update(str(p) for p in rd.rglob('*') if p.is_file());assert not list(rd.rglob('*.trx'))
 s=read(rd/'summary.json');summaries[mode]=s
 assert s['ExitCode']==0 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded']
 assert s['Tests']=={'Total':0,'Executed':0,'Passed':0,'Failed':0}
 assert not s['CategoryResults'] and not s['AdapterResults'] and not s['DuplicateTests']
 assert s['Revision']['Head']==COMPILED and s['Revision']['ChangedFiles']==0
 assert s['Selection']['CompletedDescriptors']==s['Selection']['CompletedCases']==0 and not s['Selection']['Complete']
 log=(rd/'dotnet-test.log').read_text();phases=re.findall(r'^===== ([^\n]+): exit (-?\d+) =====$',log,re.M)
 assert phases and all(code=='0' for name,code in phases)
 if mode=='catalog':
  assert r['argv'][-1]=='-ValidateCatalog' and s['DiscoveryOnly'] and not s['PlanOnly']
  audit=read(rd/'catalog-audit.json');assert audit==s['CatalogAudit'] and audit['Valid']
  assert audit['Methods']==11800 and audit['Categories']==808 and not audit['Unmapped'] and not audit['StaleSelectors']
  for name in ['Build-integration','Build-unit']:
   text=(rd/(name+'.log')).read_text();assert 'Build succeeded.' in text and re.search(r'^\s+0 Error\(s\)',text,re.M)
   assert not re.search(r': error [A-Za-z0-9]+:',text)
   warnings=sorted(set(line for line in text.splitlines() if ': warning ' in line))
   totals=re.findall(r'^\s+(\d+) Warning\(s\)',text,re.M);assert totals; n=int(totals[-1])
   assert len(warnings)==n and re.search(r'^\s+'+str(n)+r' Warning\(s\)',text,re.M)
   builds[name]=dict(warnings=n,errors=0,codes=dict(collections.Counter(re.search(r'warning (\w+)',line).group(1) for line in warnings)))
  assert all((name,'0') in phases for name in ['Build-integration','Build-unit','integration-discovery','unit-discovery'])
 else:
  assert '-PlanOnly' in r['argv'] and '-NoBuild' in r['argv'] and s['PlanOnly'] and not s['DiscoveryOnly']
  assert not any(name.startswith('Build-') for name,code in phases)
  pl=read(rd/'plan.json');assert pl==read(path.parent/'observed-plan.json') and pl['Revision']['Head']==COMPILED;plans[mode]=pl
  assert r['binaryBefore']==r['binaryAfter'] and r['allOutputInventoryBefore']==r['allOutputInventory']
 native_seconds=sum(float(v)*factor for v,factor in zip(s['WallTime'].split(':'),[3600,60,1]));assert native_seconds < s['TimeoutMinutes']*60
 record_checks.append(dict(mode=mode,path=str(path),sha256=h(path.read_bytes()),nativeExit=0,executed=0,seconds=r['wallSeconds'],nativeWallSeconds=native_seconds,timeoutMinutes=s['TimeoutMinutes'],phaseExits=phases))
assert set(records)=={'catalog','plan','corrective-plan','remaining-plan'} and expected==set(raws),(expected-set(raws),set(raws)-expected)

cat=records['catalog'];nine=cat['binaryAfter'];inventory=cat['allOutputInventory'];assert len(nine)==9 and len(inventory)==1575
for mode,r in records.items():
 assert r['binaryAfter']==nine and r['allOutputInventory']==inventory
 if mode!='catalog':assert r['binaryBefore']==nine and r['allOutputInventoryBefore']==inventory
outroots=[R/x/'bin/Debug/net8.0' for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
paths=[str(p.relative_to(R)) for p in sorted(p for root in outroots for p in root.rglob('*') if p.is_file())]
assert paths==[x['path'] for x in inventory] and len(set(paths))==1575
for x in inventory:
 b=(R/x['path']).read_bytes();assert (len(b),h(b))==(x['bytes'],x['sha256']),x['path']
lookup={x['path']:x for x in inventory};assert all(lookup[x['path']]==x for x in nine)
for name,n in [('BookOfEternityClient.dll',4),('BookOfEternityClient.TestSupport.dll',2)]:
 hashes=[x['sha256'] for x in nine if Path(x['path']).name==name];assert len(hashes)==n and len(set(hashes))==1
oldnine={x['path']:x for x in cat['binaryBefore']}
changed_nine=[x['path'] for x in nine if oldnine.get(x['path'])!=x]
assert any(Path(x).name=='BookOfEternityClient.dll'for x in changed_nine)
client_hash=next(x['sha256'] for x in nine if Path(x['path']).name=='BookOfEternityClient.dll')
assert client_hash not in {x['sha256']for x in cat['binaryBefore']if Path(x['path']).name=='BookOfEternityClient.dll'}

sourcecheck=read(D/'t179pd-ni-iobs-l01-selected-routing-source-independent-check-fed1f778-20261018.json')
assert h((D/'t179pd-ni-iobs-l01-selected-routing-source-independent-check-fed1f778-20261018.json').read_bytes())=='123ec651b82761e8fd366c3ece08615521cf311dc9b982c40eca9661d456bdb7'
expected_methods=dict(sourcecheck['retained']['methodCases'])
for mode,info in sourcecheck['modes'].items():
 assert not set(expected_methods)&set(info['methodCases']);expected_methods.update(info['methodCases'])
def methods(plan):
 result={}
 for d in plan['Descriptors']:
  assert d['Adapter'] in {'unit','integration'} and not d['FrontendBuild']
  assert d['EstimatedCases']==sum(m['Cases'] for m in d['ExpectedMethodCounts'])
  assert set(d['Filter'].split('|'))=={'FullyQualifiedName='+m['Name'] for m in d['ExpectedMethodCounts']}
  for m in d['ExpectedMethodCounts']:
   assert m['Name'] not in result;result[m['Name']]=m['Cases']
 return result
maps={mode:methods(pl) for mode,pl in plans.items()}
assert maps['plan']==expected_methods and len(plans['plan']['Descriptors'])==16
corrective={m:n for mode,x in sourcecheck['modes'].items()if mode!='crash5'for m,n in x['methodCases'].items()}
assert maps['corrective-plan']==corrective and len(plans['corrective-plan']['Descriptors'])==1
remaining=sourcecheck['modes']['crash5']['methodCases']
assert maps['remaining-plan']==remaining and len(plans['remaining-plan']['Descriptors'])==1
assert not set(remaining)&set(maps['corrective-plan'])
retained={m:n for m,n in maps['plan'].items() if m not in remaining and m not in maps['corrective-plan']}
assert retained==sourcecheck['retained']['methodCases'] and len(retained)==31 and sum(retained.values())==51
assert sourcecheck['sourcePinDigest']==packet['pinDigest'] and sourcecheck['sourceInverse']['exactInverse'] and sourcecheck['sourceInverse']['fullPostvalidationCatchCompletionAndHoldBodyExact']
assert len(maps['plan'])==33 and sum(maps['plan'].values())==57 and len(maps['corrective-plan'])==1 and sum(maps['corrective-plan'].values())==1 and len(remaining)==1 and sum(remaining.values())==5
selection=read(R/'tests/selection.json');catalog={x['id']:x for x in read(R/'tests/categories.json')['categories']}
for mode,pl in plans.items():
 categories={d['Category'] for d in pl['Descriptors']};r=records[mode]
 selection_path=Path(r['argv'][r['argv'].index('-SelectionFile')+1]);selection_path=selection_path if selection_path.is_absolute() else R/selection_path
 expected_selection=selection if mode=='plan' else dict(schemaVersion=1,selections=[x for x in selection['selections'] if x['category'] in categories])
 assert read(selection_path)==expected_selection and h(selection_path.read_bytes())==r['selectionSha256']
 assert h((R/'tests/selection.json').read_bytes())==r['wholeSelectionSha256']
 assert len(maps[mode])==r['expectedMethods'] and sum(maps[mode].values())==r['expectedCases'] and len(pl['Descriptors'])==r['expectedDescriptors']
 for d in pl['Descriptors']:
  assert d['Category'] in catalog
  if mode!='plan':assert catalog[d['Category']]['requirements']['exclusive']

recovery=[]
receipt_paths=[Path(x)for x in sys.argv[3:]] or [Path('/tmp/1536-cloud-tools/carrier-recovery-'+HEAD[:8]+'.json')]
previous=COMPILED;covered=set()
for rp in receipt_paths:
 rec=read(rp);rev=rec['sha'];base=rec['base'];t=tree(C,rev)
 assert rec['sourcePinsVerified']==99 and rec['sourcePinDigest']==packet['pinDigest']
 assert base==previous and rec['remoteShaVerified'] and rec['clean'] and not rec['localSourceOrObjectReuse']
 changed=git(C,'diff','--name-only',base,rev).decode().splitlines();assert len(changed)==rec['changedPathsVerified']and set(changed)=={x['path']for x in rec['files']}
 covered.update(changed)
 for x in rec['files']:
  b=git(C,'show',rev+':'+x['path']);assert b==git(R,'show',rev+':'+x['path'])and(len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob'])and t[x['path']]==(x['mode'],'blob',x['gitBlob'])
  if rt.get(x['path'])==t[x['path']]:assert b==(R/x['path']).read_bytes()==(C/x['path']).read_bytes()
 recovery.append(dict(carrier=rev,base=base,changedPaths=len(changed),receiptSha256=h(rp.read_bytes()),bytesHashesBlobsModesVerified=True));previous=rev
assert previous==HEAD and covered==set(git(R,'diff','--name-only',COMPILED,HEAD).decode().splitlines())

census=cloud['processCensusScopeCorrection'];sources=census['observerSources']
refs=prep['supplementalBoundaries']+[prep['prepQualificationBoundary']]
assert len(refs)==9
orchestration=prep['orchestrationEvidence'];assert orchestration
for x in sources+census['sourceReview']['artifacts']+refs+orchestration+[prep['sourceAcceptanceRecovery']]:
 b=(R/x['path']).read_bytes();assert b==(C/x['path']).read_bytes() and (len(b),h(b))==(x['bytes'],x['sha256'])
wrapper_path=REC+'t179pd-ni-iobs-l01-run-selected-routing-phase-20261018.py'
wrapper_bytes=(R/wrapper_path).read_bytes();assert h(wrapper_bytes)=='da65d134a2c41f471c34d4983773239d4641df7058eb632694db90e61e8cc5b7'
assert wrapper_bytes==git(R,'show',COMPILED+':'+wrapper_path)
# Current wrapper was captured and versioned before catalog launch. Historical
# borrowed wrapper reconstruction remains historical, not a new provenance claim.
assert any(x['sha256']==h(wrapper_bytes)for x in orchestration)
acceptance_readback=D/'t179pd-ni-iobs-l01-selected-routing-source-acceptance-readback-c391c030-20261018.json'
assert any(x['sha256']==h(acceptance_readback.read_bytes())for x in orchestration)
saved_acceptance=read(R/prep['sourceAcceptanceRecovery']['path'])
assert saved_acceptance['sha']==COMPILED and saved_acceptance['sourcePinsVerified']==99 and saved_acceptance['sourcePinDigest']==packet['pinDigest']
v2=sources[1];assert v2['bytes']==7410 and v2['sha256']==census['sourceReview']['sourceSha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f'
assert (R/v2['path']).read_bytes()==Path('/tmp/1536-cloud-tools/l01-process-census.py').read_bytes()==(D/'l01-process-census-v2-reviewed.py').read_bytes()
census_checks=[]
for ref in refs:
 c=read(R/ref['path']);assert c['fixtureTemp']==cloud['environment']['root'] and c['fixtureTempUid']==1000 and c['observerVersion']==2 and c['observerSourceSha256']==v2['sha256']
 assert c['cleanBoundary'] and not any(c[k] for k in ['unknown','confirmedGone','activeTaskGameTestProcesses','ownedRoots'])
 assert len(c['pidsEnumerated'])==len(c['entries']) and set(c['pidsEnumerated'])=={x['pid'] for x in c['entries']}
 for x in c['entries']:
  def ids(raw):
   f=raw.rsplit(')',1)[1].split();return int(raw.split(' ',1)[0]),int(f[19]),int(f[1]),f[0]
  assert ids(x['statBefore'])[:2]==ids(x['statAfter'])[:2]==(x['pid'],x['startTick'])
  assert ids(x['statAfter'])[2:]==(x['ppid'],x['state'])
  if 'argv' in x:assert h(('\0'.join(x['argv'])+'\0').encode())==x['argvSha256']
  if x['classification']=='exited-nonexecuting':assert x['state'] in {'Z','X','x'} and x['argvCount']==0
 assert all(x['status']=='MATCH' for x in c['knownVisibility'])
 known=next(x for x in c['entries'] if x['pid']==10433);assert known['startTick']==477854 and known['classification']=='exited-nonexecuting'
 assert {x['pid']for x in c['buildInfrastructure']}=={x['pid']for x in c['entries']if x['classification']=='build-infrastructure'}
 for x in c['buildInfrastructure']:
  assert any('MSBuild.dll'in arg or 'VBCSCompiler'in arg for arg in x['argv'])
 census_checks.append(dict(path=ref['path'],sha256=ref['sha256'],version=2,phase=c['phase'],startedUtc=c['startedUtc'],finishedUtc=c['finishedUtc'],entries=len(c['entries']),classes=dict(collections.Counter(x['classification'] for x in c['entries'])),clean=True,knownVisibility=c['knownVisibility']))
assert census_checks[-1]['sha256']==prep['prepQualificationBoundary']['sha256']
assert 'immediately before/after each named actual' in census['mandatoryGate']
boundary_gaps={}
for index,mode in enumerate(['catalog','plan','corrective-plan','remaining-plan']):
 before,after=census_checks[index*2:index*2+2];r=records[mode]
 delta_before=(datetime.datetime.fromisoformat(r['startedUtc'])-datetime.datetime.fromisoformat(before['finishedUtc'])).total_seconds()
 delta_after=(datetime.datetime.fromisoformat(after['startedUtc'])-datetime.datetime.fromisoformat(r['finishedUtc'])).total_seconds()
 assert delta_before>=0 and delta_after==refs[index*2+1]['datedBoundaryGapSeconds'] and delta_before==refs[index*2]['datedBoundaryGapSeconds']
 boundary_gaps[mode]=dict(beforeFinishToStartSeconds=delta_before,finishToAfterStartSeconds=delta_after)
assert datetime.datetime.fromisoformat(census_checks[-1]['startedUtc'])>datetime.datetime.fromisoformat(records['remaining-plan']['finishedUtc'])

corr=cloud['environmentCorrection'];env=Path('/tmp/1536-cloud-tools/env.sh').read_bytes()
assert env.decode()==corr['envShell'] and h(env)==corr['envShellSha256']=='5d61c19c1341cf9a26b9d361c81da8c89d09924381791c4108d968555f899c53'
assert not re.search(rb'^\s*export\s+HOME=',env,re.M)
# No live /proc, fixture-root or runtime-directory inspection; saved original censuses supply dated evidence.
assert cloud['qualifiedCases']['totalDistinctTargets']==51 and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and not cloud['oldCorrectedCrash']['recoveryClaim']
assert cloud['release']['mode'] is None
accepted_cloud=json.loads(git(R,'show',COMPILED+':'+REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
for field in ['rootIsolation','processIsolation','stateIsolation','assertions']:assert cloud['crashRequalificationPlan'][field]==accepted_cloud['crashRequalificationPlan'][field]
assert cloud['actuals']==accepted_cloud['actuals'] and cloud['qualifiedCases']==accepted_cloud['qualifiedCases'] and cloud['oldCorrectedCrash']==accepted_cloud['oldCorrectedCrash']
for mode,value in cloud['actuals'].items():
 if 'archive'not in value:continue
 ref=value['archive'];assert h((R/ref['path']).read_bytes())==ref['sha256'] and not git(R,'diff','--name-only',COMPILED,HEAD,'--',str(Path(ref['path']).parent))
assert not any('ACTUAL' in p.name and p.is_file() for p in Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools').glob(COMPILED+'-ni-iobs-l01-process-*.FIRST.json'))
assert not [p for p in Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools').glob(COMPILED+'-ni-iobs-l01-process-*.FIRST.json') if '-process-zero-' not in p.name]
for root in (R,C):assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain')

out=dict(kind='INDEPENDENT_SELECTED_ROUTING_ZERO4_PREPARATION_CHECK',checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),carrier=HEAD,compiledSource=COMPILED,sourcePinCount=99,sourcePinDigest=packet['pinDigest'],ledgerSha256=packet['ledger']['sha256'],archive=dict(path=prep['archive']['path'],sha256=h(ab),rawChains=43,completeRawCoverage=True,allOriginalStoredRestoredBytesMatched=True),records=record_checks,catalog=summaries['catalog']['CatalogAudit'],builds=builds,plans={mode:dict(descriptors=len(plans[mode]['Descriptors']),methods=len(m),cases=sum(m.values()),executed=0) for mode,m in maps.items()},retained=dict(methods=31,cases=51,exactPriorProvenancePreserved=True),binaries=dict(copies=9,completeFiles=1575,allPhysicalPathsSizesHashesMatch=True,allPlansNoBuildExactContinuity=True,fourClientCopiesEqual=True,twoSupportCopiesEqual=True,correctedClientSha256=client_hash,selectedCopiesChangedDuringFreshCatalog=changed_nine),processLaunchRecords=process_checks,originalProcessArrayLimitation='exe-link-dependent arrays preserved; not complete process-absence evidence',nativeRunnerCleanup=True,ownedFixtureRootsEmpty=True,censuses=census_checks,observerV2=dict(sha256=v2['sha256'],bytes=7410,exactReviewedSource=True,mandatoryBeforeAfterEveryActual=True),recovery=recovery,environment=dict(envSha256=h(env),privateRuntime=corr['path'],homeUnchanged=True,scope='Saved correction/env evidence only; no new live boot/root/process observation'),boundaryGaps=boundary_gaps,orchestrationEvidence=orchestration,wrapperProvenanceLimit='Current selected-routing wrapper was saved before launch and equals the accepted source version; prior borrowed wrapper reconstruction remains historical. Actual controller/process/record argv and original ZERO evidence bind each phase. Same-invocation census is dated observation, not continuous absence.',metadataObservation='Current selected-routing SOURCE accepted; original ZERO4 records and dated v2 boundaries checked; final independent PREP verdict must be separately authored.',eligibility='CHECKER_RESULT_ONLY_NO_AUTOMATIC_PREP_ACCEPTANCE_OR_RUNTIME_RELEASE. Independent review of complete recovered ZERO4 required; each later actual separately gated.',oldCorrectedCrash='UNKNOWN_UNAVAILABLE',actualExecuted=0,findings=[],limits='Evidence/hash/source reads only. No builds/discovery/tests/controller/observer execution, process intervention or repo writes. No product runtime PASS or cross-build equivalence for retained51.')
dest=D/('t179pd-ni-iobs-l01-selected-routing-prep-independent-check-'+HEAD[:8]+'-20261018.json');dest.write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps(dict(path=str(dest),bytes=dest.stat().st_size,sha256=h(dest.read_bytes()),rawChains=43,plans=out['plans'],binaryChangedCopies=changed_nine,findings=[]),indent=2))
