"""Independent corrected PREP evidence checks only; no build/discovery/tests/controller execution."""
from pathlib import Path
import collections, datetime, gzip, hashlib, json, os, re, stat, subprocess

R=Path('/workspace/The-Book-of-Eternity-Reborn'); C=Path('/workspace/1536-cloud-clean-restore'); D=Path('/tmp/1536-cloud-tools/review')
REC='specs/1536-complete-wound-materialization/recovery/'
HEAD='e5d130e38c13bdf42f6d8ba0dbb53d1fc6cbf548'; COMPILED='7dbfbddc74b9fe55fcf80bb08d85bf26d245b38c'
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
prep=cloud['settlementFixtureCorrection']['freshPreparation']; packet=read(R/(REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'))
source_bytes=git(R,'show',COMPILED+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'); compiled_packet=json.loads(source_bytes)
assert packet['sourcePins']==compiled_packet['sourcePins'] and len(packet['sourcePins'])==98
assert len(packet['productDelta'])==21 and packet['productDelta']==git(R,'diff','--name-only',packet['productBase'],HEAD,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()
for helper in ['t179pd-ni-iobs-l01-process-preparation-run.py','t179pd-ni-iobs-l01-process-first-run.py']:assert "len(binding['productDelta'])==21"in (R/(REC+helper)).read_text()
assert h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']==prep['sourcePinDigest']=='304a7e8beb3174246ac3db792c6aa63319fa8ead9f3bab62f91346d4f204248d'
for x in packet['sourcePins']:
 b=(R/x['path']).read_bytes();assert b==(C/x['path']).read_bytes()
 assert (len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob'])
 assert rt[x['path']]==ct[x['path']]==(x['mode'],'blob',x['gitBlob'])
ledger_bytes=(R/packet['ledger']['path']).read_bytes();assert h(ledger_bytes)==packet['ledger']['sha256']
for x in cloud['settlementFixtureCorrection']['sourceReview']['artifacts']:
 b=(R/x['path']).read_bytes();assert (len(b),h(b))==(x['bytes'],x['sha256'])
for path in ['AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md']:
 assert (R/path).read_bytes()==(C/path).read_bytes()==git(R,'show',COMPILED+':'+path)

ap=R/prep['archive']['path'];ab=ap.read_bytes();assert h(ab)==prep['archive']['sha256']=='8727f23c6a026d714ef916d0e5e90cc58d58389b80eec7c45b01ebff18cf4bef'
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
 assert h(path.read_bytes())==item['sha256']
 assert r['actualSource']==r['compiledSource']==r['headAfter']==COMPILED and not r['statusAfter']
 assert r['nativeExit']==r['actualTestsExecuted']==0 and r['kind']=='FIRST_NI_IOBS_L01_WHOLE_ZERO_PREPARATION_RAW'
 assert r['helperSource']==(R/(REC+'t179pd-ni-iobs-l01-process-preparation-run.py')).read_text()
 assert r['sourcePinDigest']==packet['pinDigest'] and r['sourcePinCount']==98
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
  for name,n in [('Build-integration',148),('Build-unit',37)]:
   text=(rd/(name+'.log')).read_text();assert 'Build succeeded.' in text and re.search(r'^\s+0 Error\(s\)',text,re.M)
   assert not re.search(r': error [A-Za-z0-9]+:',text)
   warnings=sorted(set(line for line in text.splitlines() if ': warning ' in line))
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
assert len(changed_nine)==9
client_hash=next(x['sha256'] for x in nine if Path(x['path']).name=='BookOfEternityClient.dll')
assert client_hash=='2da34865a700f8a2b1ec240a9038490e7f76583a65df18aa8cd0305d80c96478'

sourcecheck=read(D/'t179pd-ni-iobs-l01-settlement-fixture-source-independent-check-c79cdea9-20261018.json')
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
assert maps['corrective-plan']==sourcecheck['modes']['settlement3']['methodCases'] and len(plans['corrective-plan']['Descriptors'])==1
remaining={m:n for mode,x in sourcecheck['modes'].items() if mode!='settlement3' for m,n in x['methodCases'].items()}
assert maps['remaining-plan']==remaining and len(plans['remaining-plan']['Descriptors'])==3
assert not set(remaining)&set(maps['corrective-plan'])
retained={m:n for m,n in maps['plan'].items() if m not in remaining and m not in maps['corrective-plan']}
assert retained==sourcecheck['retained']['methodCases'] and len(retained)==28 and sum(retained.values())==44
assert sourcecheck['sourcePinDigest']==packet['pinDigest'] and sourcecheck['fixtureDiff']['exactInverseRestoresBase'] and sourcecheck['productionAssertionsCountersJournalCallbacksRootsCatalogSelectorsRunnerDocsUnchanged']
assert len(maps['plan'])==33 and sum(maps['plan'].values())==57 and len(maps['corrective-plan'])==2 and sum(maps['corrective-plan'].values())==3 and len(remaining)==3 and sum(remaining.values())==10
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
for label,rev,base,expected_count,t in [('e5d130e3',HEAD,COMPILED,59,ct)]:
 rp=Path('/tmp/1536-cloud-tools/carrier-recovery-'+label+'.json');rec=read(rp)
 assert rec['sha']==rev and rec['base']==base and rec['changedPathsVerified']==expected_count and rec['remoteShaVerified'] and rec['clean'] and not rec['localSourceOrObjectReuse']
 changed=git(C,'diff','--name-only',base,rev).decode().splitlines();assert len(changed)==expected_count and set(changed)=={x['path'] for x in rec['files']}
 for x in rec['files']:
  b=git(C,'show',rev+':'+x['path']);assert (len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob'])
  assert t[x['path']]==(x['mode'],'blob',x['gitBlob'])
  if rev==HEAD or rt[x['path']]==t[x['path']]:assert b==(R/x['path']).read_bytes()==(C/x['path']).read_bytes()
 recovery.append(dict(carrier=rev,base=base,changedPaths=expected_count,receiptSha256=h(rp.read_bytes()),bytesHashesBlobsModesVerified=True))

census=cloud['processCensusScopeCorrection'];sources=census['observerSources']
refs=prep['supplementalBoundaries']+[prep['prepQualificationBoundary']]
assert len(refs)==9
for x in sources+census['sourceReview']['artifacts']+refs+[prep['sourceAcceptanceReadback'],prep['sourceAcceptanceRecovery'],prep['sourceAcceptanceParentReadback']]:
 b=(R/x['path']).read_bytes();assert b==(C/x['path']).read_bytes() and (len(b),h(b))==(x['bytes'],x['sha256'])
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
assert census_checks[-1]['sha256']=='5bc8856dafff35087a5f6791dbce0566657dce0c75a0b27d64b889a2fac8fbcb' and census_checks[-1]['entries']==78
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
runtime=Path(corr['path']);st=runtime.lstat();assert runtime.is_dir() and not runtime.is_symlink() and str(runtime.resolve())==corr['realpath'] and stat.S_IMODE(st.st_mode)==0o700 and st.st_uid==corr['uid']==1000
assert Path('/proc/sys/kernel/random/boot_id').read_text().strip()==cloud['environment']['linuxBootId']
assert not [p for pattern in ['boe-gameengine-turnlifecycle-*','boe-joint-interface-crash-*'] for p in runtime.parent.glob(pattern)]
assert cloud['qualifiedCases']['totalDistinctTargets']==44 and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and not cloud['oldCorrectedCrash']['recoveryClaim']
assert cloud['release']['mode'] is None
accepted_cloud=json.loads(git(R,'show',COMPILED+':'+REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
for field in ['rootIsolation','processIsolation','stateIsolation','assertions']:assert cloud['crashRequalificationPlan'][field]==accepted_cloud['crashRequalificationPlan'][field]
for mode in ['legacy3','close2','close2Corrected','races2','races2Corrected','settlement3']:
 ref=cloud['actuals'][mode]['archive'];assert h((R/ref['path']).read_bytes())==ref['sha256'] and not git(R,'diff','--name-only',COMPILED,HEAD,'--',str(Path(ref['path']).parent))
assert not any('ACTUAL' in p.name and p.is_file() for p in Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools').glob(COMPILED+'-ni-iobs-l01-process-*.FIRST.json'))
assert not [p for p in Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools').glob(COMPILED+'-ni-iobs-l01-process-*.FIRST.json') if '-process-zero-' not in p.name]
for root in (R,C):assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain')

named_actual='t179pd-ni-iobs-l01-process-first-settlement3-fixture-corrected-cloud-813f1057'
assert not (Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools')/named_actual).exists()
out=dict(kind='INDEPENDENT_SETTLEMENT_FIXTURE_ZERO4_PREPARATION_CHECK',checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),carrier=HEAD,compiledSource=COMPILED,sourcePinCount=98,sourcePinDigest=packet['pinDigest'],ledgerSha256=packet['ledger']['sha256'],archive=dict(path=prep['archive']['path'],sha256=h(ab),rawChains=43,completeRawCoverage=True,allOriginalStoredRestoredBytesMatched=True),records=record_checks,catalog=summaries['catalog']['CatalogAudit'],builds=builds,plans={mode:dict(descriptors=len(plans[mode]['Descriptors']),methods=len(m),cases=sum(m.values()),executed=0) for mode,m in maps.items()},retained=dict(methods=28,cases=44,exactPriorProvenancePreserved=True),binaries=dict(copies=9,completeFiles=1575,allPhysicalPathsSizesHashesMatch=True,allPlansNoBuildExactContinuity=True,fourClientCopiesEqual=True,twoSupportCopiesEqual=True,correctedClientSha256=client_hash,selectedCopiesChangedDuringFreshCatalog=changed_nine),processLaunchRecords=process_checks,originalProcessArrayLimitation='exe-link-dependent arrays preserved; not complete process-absence evidence',nativeRunnerCleanup=True,ownedFixtureRootsEmpty=True,censuses=census_checks,observerV2=dict(sha256=v2['sha256'],bytes=7410,exactReviewedSource=True,mandatoryBeforeAfterEveryActual=True),recovery=recovery,environment=dict(envSha256=h(env),privateRuntime=str(runtime),uid=1000,mode='0700',homeUnchanged=True,sameBoot=True),boundaryGaps=boundary_gaps,metadataObservation='Current settlement fixture SOURCE accepted; all four originals ZERO with dated v2 boundaries; independent PREP pending until this decision is saved.',eligibility='After saved/recovered independent PREP acceptance and immediate authoritative clean before-census, ONLY separately named settlement3 corrective NoBuild regression label t179pd-ni-iobs-l01-process-first-settlement3-fixture-corrected-cloud-813f1057 using corrective-plan. Other modes separately gated.',oldCorrectedCrash='UNKNOWN_UNAVAILABLE',actualExecuted=0,findings=[],limits='Evidence/hash/source reads only. No builds/discovery/tests/controller/observer execution, process intervention or repo writes. No product runtime PASS or cross-build equivalence for retained44.')
dest=D/'t179pd-ni-iobs-l01-settlement-fixture-prep-independent-check-e5d130e3-20261018.json';dest.write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps(dict(path=str(dest),bytes=dest.stat().st_size,sha256=h(dest.read_bytes()),rawChains=43,plans=out['plans'],binaryChangedCopies=changed_nine,findings=[]),indent=2))
