#!/usr/bin/env python3
"""Reusable read-only compile-corrected diagnostic1 PREP evidence checker. Run only after saved/recovered ZERO3. No native/controller/observer invocation."""
from pathlib import Path
import collections, datetime, gzip, hashlib, json, os, re, subprocess, sys

R=Path('/workspace/The-Book-of-Eternity-Reborn'); C=Path('/workspace/1536-cloud-clean-restore'); D=Path('/tmp/1536-cloud-tools/review')
REC='specs/1536-complete-wound-materialization/recovery/'
assert len(sys.argv)>=3, 'Usage: checker.py SAVED_PREP_CARRIER COMPILED_SOURCE [CONTINUOUS_RECOVERY_RECEIPTS...]'
HEAD=sys.argv[1]; assert re.fullmatch(r'[0-9a-f]{40}',HEAD)
COMPILED=sys.argv[2]; assert re.fullmatch(r'[0-9a-f]{40}',COMPILED)
assert COMPILED=='8f8f534304e3672df5d42c9ab31e7055235bfaca', 'Current SOURCE accepted build only'
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
prep=cloud['crashDiagnostic1']['freshPreparation']; packet=read(R/(REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'))
source_bytes=git(R,'show',COMPILED+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'); compiled_packet=json.loads(source_bytes)
assert compiled_packet['namedZeroRelease']['status']=='SOURCE_ACCEPTED_SAVED_RECOVERED_DIAGNOSTIC_ZERO3_ONLY'
assert compiled_packet['namedActualRelease']['mode']is None
assert packet['sourcePins']==compiled_packet['sourcePins'] and len(packet['sourcePins'])==101
assert len(packet['productDelta'])==23 and packet['productDelta']==git(R,'diff','--name-only',packet['productBase'],HEAD,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()
for helper in ['t179pd-ni-iobs-l01-process-preparation-run.py','t179pd-ni-iobs-l01-process-first-run.py']:assert "len(binding['productDelta'])==23"in (R/(REC+helper)).read_text()
assert h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']==prep['sourcePinDigest']=='2c144b7734708f8c02a3924d4d12a885384f05a1437021afd2845827c761c616'
for x in packet['sourcePins']:
 b=(R/x['path']).read_bytes();assert b==(C/x['path']).read_bytes()
 assert (len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob'])
 assert rt[x['path']]==ct[x['path']]==(x['mode'],'blob',x['gitBlob'])
ledger_bytes=(R/packet['ledger']['path']).read_bytes();assert h(ledger_bytes)==packet['ledger']['sha256']
for x in cloud['crashDiagnostic1']['sourceAcceptance']['artifacts']:
 b=(R/x['path']).read_bytes();assert (len(b),h(b))==(x['bytes'],x['sha256'])
for path in ['AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md']:
 assert (R/path).read_bytes()==(C/path).read_bytes()==git(R,'show',COMPILED+':'+path)

ap=R/prep['archive']['path'];ab=ap.read_bytes();assert h(ab)==prep['archive']['sha256']
assert 'diagnostic' in prep['archive']['path'] and prep['archive']['path'].endswith('/archive.json')
a=json.loads(ab);assert len(a['files'])==a['rawChainCount']==prep['archive']['rawChains']
assert a['actualSources']==a['compiledSources']==[COMPILED]*3 and a['nativeExits']==[0]*3
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
 assert r['sourcePinDigest']==packet['pinDigest'] and r['sourcePinCount']==101
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
  assert audit['Methods']==11801 and audit['Categories']==809 and not audit['Unmapped'] and not audit['StaleSelectors']
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
assert set(records)=={'catalog','plan','diagnostic-plan'} and expected==set(raws),(expected-set(raws),set(raws)-expected)

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
assert any(Path(x).name=='BookOfEternityClient.IntegrationTests.dll'for x in changed_nine), 'diagnostic fixture assembly must be fresh'
client_hash=next(x['sha256'] for x in nine if Path(x['path']).name=='BookOfEternityClient.dll')
# Production is unchanged: a deterministic client DLL may retain its hash despite fresh BOTH builds.

sourcecheck_path=R/(REC+'t179pd-ni-iobs-l01-selected-routing-source-independent-check-fed1f778-20261018.json')
sourcecheck=read(sourcecheck_path)
assert h(sourcecheck_path.read_bytes())=='123ec651b82761e8fd366c3ece08615521cf311dc9b982c40eca9661d456bdb7'
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
assert maps['plan']==expected_methods and len(plans['plan']['Descriptors'])==16 and len(expected_methods)==33 and sum(expected_methods.values())==57
diag_method='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss'
assert maps['diagnostic-plan']=={diag_method:1} and len(plans['diagnostic-plan']['Descriptors'])==1 and diag_method not in expected_methods
remaining=sourcecheck['modes']['crash5']['methodCases'];retained={m:n for m,n in expected_methods.items() if m not in remaining}
assert len(retained)==32 and sum(retained.values())==52 and len(remaining)==1 and sum(remaining.values())==5
source_review_path=R/(REC+'t179pd-ni-iobs-l01-diagnostic1-compile-corrected-source-independent-review-20261018.json');source_review=read(source_review_path)
assert source_review['candidate']=='6daaa3da8fb279909ef6318cdfa9892a2f06362c' and source_review['pinDigest']==packet['pinDigest'] and not source_review['findings']
assert source_review['eligibility']['namedZeroRelease']['labels']==compiled_packet['namedZeroRelease']['labels']
selection=read(R/'tests/selection.json');diag_selection=read(R/'tests/selections/1536-joint-interface-crash-diagnostic1.json')
catalog={x['id']:x for x in read(R/'tests/categories.json')['categories']}
for mode,pl in plans.items():
 categories={d['Category'] for d in pl['Descriptors']};r=records[mode]
 selection_path=Path(r['argv'][r['argv'].index('-SelectionFile')+1]);selection_path=selection_path if selection_path.is_absolute() else R/selection_path
 expected_selection=selection if mode=='plan' else diag_selection
 assert read(selection_path)==expected_selection and h(selection_path.read_bytes())==r['selectionSha256']
 assert h((R/'tests/selection.json').read_bytes())==r['wholeSelectionSha256']
 assert len(maps[mode])==r['expectedMethods'] and sum(maps[mode].values())==r['expectedCases'] and len(pl['Descriptors'])==r['expectedDescriptors']
 assert categories=={x['category']for x in expected_selection['selections']}
assert catalog['fr063-joint-interface-crash-diagnostic']['timeoutMinutes']==15 and catalog['fr063-joint-interface-crash-diagnostic']['requirements']['exclusive']
assert diag_method not in maps['plan']

recovery=[]
receipt_paths=[Path(x)for x in sys.argv[3:]] or [Path('/tmp/1536-cloud-tools/carrier-recovery-'+HEAD[:8]+'.json')]
previous=COMPILED;covered=set()
for rp in receipt_paths:
 rec=read(rp);rev=rec['sha'];base=rec['base'];t=tree(C,rev)
 assert rec['sourcePinsVerified']==101 and rec['sourcePinDigest']==packet['pinDigest']
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
assert len(refs)==7
orchestration=prep['orchestrationEvidence'];assert orchestration
# All saved nested artifact references are independently hash/clone checked.
all_refs={}
def collect_refs(value):
 if isinstance(value,dict):
  if all(k in value for k in ['path','bytes','sha256']) and str(value['path']).startswith(REC):
   old=all_refs.setdefault(value['path'],value);assert (old['bytes'],old['sha256'])==(value['bytes'],value['sha256'])
  for child in value.values():collect_refs(child)
 elif isinstance(value,list):
  for child in value:collect_refs(child)
collect_refs(prep)
for x in sources+census['sourceReview']['artifacts']+list(all_refs.values()):
 b=(R/x['path']).read_bytes();assert b==(C/x['path']).read_bytes() and (len(b),h(b))==(x['bytes'],x['sha256'])
wrapper=packet['diagnosticOrchestration'];wrapper_bytes=(R/wrapper['path']).read_bytes()
assert (len(wrapper_bytes),h(wrapper_bytes))==(wrapper['bytes'],wrapper['sha256']) and wrapper_bytes==git(R,'show',COMPILED+':'+wrapper['path'])
assert any(x['sha256']==h(wrapper_bytes) for x in all_refs.values())
envref=wrapper['environmentShell'];env=(R/envref['path']).read_bytes();assert (len(env),h(env))==(envref['bytes'],envref['sha256']) and env==git(R,'show',COMPILED+':'+envref['path'])
assert any(x['sha256']==h(env)for x in all_refs.values()) and not re.search(rb'^\s*export\s+HOME=',env,re.M)
namespace=cloud['crashDiagnostic1']['runNamespace'];newroot=namespace['tmpdir'];assert namespace['samePhysicalCloud']
exports={line[7:].split('=',1)[0]:line[7:].split('=',1)[1]for line in env.decode().splitlines()if line.startswith('export ')}
assert exports['TMPDIR']==newroot and exports['XDG_RUNTIME_DIR']==newroot+'/runtime' and all(exports[k].startswith(newroot+'/tooling/')for k in ['DOTNET_CLI_HOME','XDG_CACHE_HOME','XDG_CONFIG_HOME','XDG_DATA_HOME'])
assert len(namespace['frozenRootsOutsideMutableNamespace'])==2 and all(not Path(x).is_relative_to(newroot)for x in namespace['frozenRootsOutsideMutableNamespace'])
saved_acceptance=read(R/prep['sourceAcceptanceRecovery']['path'])
assert saved_acceptance['sha']==COMPILED and saved_acceptance['sourcePinsVerified']==101 and saved_acceptance['sourcePinDigest']==packet['pinDigest']
v2=sources[1];assert v2['bytes']==7410 and v2['sha256']==census['sourceReview']['sourceSha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f'
assert (R/v2['path']).read_bytes()==git(R,'show',COMPILED+':'+v2['path'])
census_checks=[]
for ref in refs:
 c=read(R/ref['path']);assert c['fixtureTemp']==newroot and c['fixtureTempUid']==1000 and c['observerVersion']==2 and c['observerSourceSha256']==v2['sha256']
 assert c['cleanBoundary'] and not any(c[k]for k in ['unknown','activeTaskGameTestProcesses','ownedRoots'])
 # A confirmed disappearance is legitimate dated evidence, not UNKNOWN.
 assert set(c['pidsEnumerated']).issubset({x['pid']for x in c['entries']}|{x['pid']for x in c['confirmedGone']}) and {x['pid']for x in c['entries']}.issubset(set(c['pidsEnumerated']))
 assert all(x['confirmedAbsentOnReprobe']for x in c['confirmedGone'])
 for x in c['entries']:
  def ids(raw):
   f=raw.rsplit(')',1)[1].split();return int(raw.split(' ',1)[0]),int(f[19]),int(f[1]),f[0]
  assert ids(x['statBefore'])[:2]==ids(x['statAfter'])[:2]==(x['pid'],x['startTick'])
  assert ids(x['statAfter'])[2:]==(x['ppid'],x['state'])
  if 'argv'in x:assert h(('\0'.join(x['argv'])+'\0').encode())==x['argvSha256']
  if x['classification']=='exited-nonexecuting':assert x['state']in {'Z','X','x'}and x['argvCount']==0
 assert all(x['status']in {'MATCH','GONE'}for x in c['knownVisibility'])
 assert {x['pid']for x in c['buildInfrastructure']}=={x['pid']for x in c['entries']if x['classification']=='build-infrastructure'}
 for x in c['buildInfrastructure']:assert any('MSBuild.dll'in arg or 'VBCSCompiler'in arg for arg in x['argv'])
 census_checks.append(dict(path=ref['path'],sha256=ref['sha256'],version=2,phase=c['phase'],startedUtc=c['startedUtc'],finishedUtc=c['finishedUtc'],entries=len(c['entries']),confirmedGone=c['confirmedGone'],classes=dict(collections.Counter(x['classification']for x in c['entries'])),clean=True,knownVisibility=c['knownVisibility']))
boundary_gaps={}
for index,mode in enumerate(['catalog','plan','diagnostic-plan']):
 before,after=census_checks[index*2:index*2+2];r=records[mode]
 assert before['phase']=='crash-diagnostic1-'+mode+'-before' and after['phase']=='crash-diagnostic1-'+mode+'-after'
 delta_before=(datetime.datetime.fromisoformat(r['startedUtc'])-datetime.datetime.fromisoformat(before['finishedUtc'])).total_seconds()
 delta_after=(datetime.datetime.fromisoformat(after['startedUtc'])-datetime.datetime.fromisoformat(r['finishedUtc'])).total_seconds()
 assert delta_before>=0 and delta_after>=0 and delta_after==refs[index*2+1]['datedBoundaryGapSeconds'] and delta_before==refs[index*2]['datedBoundaryGapSeconds']
 boundary_gaps[mode]=dict(beforeFinishToStartSeconds=delta_before,finishToAfterStartSeconds=delta_after)
assert datetime.datetime.fromisoformat(census_checks[-1]['startedUtc'])>datetime.datetime.fromisoformat(records['diagnostic-plan']['finishedUtc'])
frozen_archive=read(R/cloud['actuals']['crash5SelectedRouting']['archive']['path'])
frozen_files=[x for x in frozen_archive['files']if any(Path(x['rawPath']).is_relative_to(z)for z in map(Path,namespace['frozenRootsOutsideMutableNamespace']))]
frozen_checks=[]
for ref in all_refs.values():
 if not ref['path'].endswith('.json'):continue
 j=read(R/ref['path'])
 if not isinstance(j,dict)or j.get('kind')!='READ_ONLY_FROZEN_CURRENT_CLOUD_ROOTS_SEPARATION':continue
 assert j['actualSource']==COMPILED and j['physicalCloudUnchanged']and j['unchanged']and j['newMutableRoot']==newroot and j['frozenRoots']==namespace['frozenRootsOutsideMutableNamespace']and j['verifiedFiles']==len(frozen_files)
 assert j['mode']in records and datetime.datetime.fromisoformat(j['checkedUtc'])<datetime.datetime.fromisoformat(records[j['mode']]['startedUtc'])
 frozen_checks.append(dict(mode=j['mode'],path=ref['path'],sha256=ref['sha256'],verifiedFiles=j['verifiedFiles'],checkedUtc=j['checkedUtc']))
assert len(frozen_checks)==3 and {x['mode']for x in frozen_checks}==set(records), 'Save all three original frozen separation receipts among freshPreparation artifact references'
# Compare warnings with the immediately preceding accepted source/build phase.
prior_prep=cloud['selectedResumeRoutingCorrection']['freshPreparation'];prior_record=next(x for x in prior_prep['records']if x['mode']=='catalog');prior_raw=Path(prior_record['path']).read_bytes();assert h(prior_raw)==prior_record['sha256'];prior_cat=json.loads(prior_raw);prior_dir=Path(prior_cat['resultDirectories'][0]);current_dir=Path(cat['resultDirectories'][0]);warning_comparison={}
def warning_lines(path):
 lines=set(line.strip()for line in path.read_text().splitlines()if ': warning 'in line)
 return {re.sub(r'\(\d+,\d+\): warning ', ': warning ',line)for line in lines}
for name in ['Build-integration','Build-unit']:
 before=warning_lines(prior_dir/(name+'.log'));after=warning_lines(current_dir/(name+'.log'))
 warning_comparison[name]=dict(added=sorted(after-before),removed=sorted(before-after),priorDistinct=len(before),currentDistinct=len(after))
 assert not after-before, ('New warning needs independent review',name,sorted(after-before))
# Saved evidence only: no live /proc, retained-root or runtime-directory inspection.
assert cloud['qualifiedCases']['totalDistinctTargets']==52 and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and not cloud['oldCorrectedCrash']['recoveryClaim']
assert cloud['release']['mode'] is None
accepted_cloud=json.loads(git(R,'show',COMPILED+':'+REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
assert cloud['crashDiagnostic1']['runNamespace']==accepted_cloud['crashDiagnostic1']['runNamespace']
assert cloud['actuals']==accepted_cloud['actuals'] and cloud['qualifiedCases']==accepted_cloud['qualifiedCases'] and cloud['oldCorrectedCrash']==accepted_cloud['oldCorrectedCrash']
for mode,value in cloud['actuals'].items():
 if 'archive'not in value:continue
 ref=value['archive'];assert h((R/ref['path']).read_bytes())==ref['sha256'] and not git(R,'diff','--name-only',COMPILED,HEAD,'--',str(Path(ref['path']).parent))
assert not any('ACTUAL' in p.name and p.is_file() for p in Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools').glob(COMPILED+'-ni-iobs-l01-process-*.FIRST.json'))
assert not [p for p in Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools').glob(COMPILED+'-ni-iobs-l01-process-*.FIRST.json') if '-process-zero-' not in p.name]
for root in (R,C):assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain')

out=dict(kind='INDEPENDENT_COMPILE_CORRECTED_DIAGNOSTIC1_ZERO3_PREPARATION_CHECK',checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),carrier=HEAD,compiledSource=COMPILED,sourcePinCount=101,sourcePinDigest=packet['pinDigest'],productDelta=23,ledgerSha256=packet['ledger']['sha256'],archive=dict(path=prep['archive']['path'],sha256=h(ab),rawChains=len(raws),completeRawCoverage=True,allOriginalStoredRestoredBytesMatched=True),records=record_checks,catalog=summaries['catalog']['CatalogAudit'],builds=builds,warningComparison=warning_comparison,plans={mode:dict(descriptors=len(plans[mode]['Descriptors']),methods=len(m),cases=sum(m.values()),executed=0,methodCases=m)for mode,m in maps.items()},retained=dict(methods=32,cases=52,exactPriorProvenancePreserved=True),diagnosticCountsToward57=False,binaries=dict(copies=9,completeFiles=len(inventory),allPhysicalPathsSizesHashesMatch=True,allPlansNoBuildExactContinuity=True,fourClientCopiesEqual=True,twoSupportCopiesEqual=True,clientSha256=client_hash,selectedCopiesChangedDuringFreshCatalog=changed_nine),processLaunchRecords=process_checks,originalProcessArrayLimitation='exe-link-dependent arrays preserved; not complete process-absence evidence',nativeRunnerCleanup=True,ownedFixtureRootsEmpty=True,censuses=census_checks,observerV2=dict(sha256=v2['sha256'],bytes=7410,exactReviewedSource=True),recovery=recovery,environment=dict(envSha256=h(env),namespace=namespace,effectiveExports=exports,homeUnchanged=True,scope='Saved environment/census/separation evidence only; no live root/process observation'),frozenRootBeforeEachPhase=frozen_checks,boundaryGaps=boundary_gaps,orchestrationEvidence=orchestration,eligibility='CHECKER_ONLY_NO_AUTOMATIC_PREP_ACCEPTANCE_OR_RUNTIME_RELEASE. Independent review still required. Only a separately named diagnostic1 gate could follow accepted PREP; original crash5/57 qualification remains frozen.',oldCorrectedCrash='UNKNOWN_UNAVAILABLE',newCrash5='ORIGINAL_NATIVE124_NO_TRX_FROZEN',actualExecuted=0,findings=[],limits='No build/discovery/test/controller/observer invocation, process intervention or repo writes. No product runtime PASS or cross-build equivalence for retained52. Dated observations do not establish continuous absence.')
dest=D/('t179pd-ni-iobs-l01-diagnostic1-compile-corrected-prep-independent-check-'+HEAD[:8]+'-20261018.json');dest.write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps(dict(path=str(dest),bytes=dest.stat().st_size,sha256=h(dest.read_bytes()),rawChains=len(raws),plans=out['plans'],findings=[]),indent=2))
