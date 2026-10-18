from pathlib import Path
import json,hashlib,gzip,subprocess,re,datetime,os,stat,collections
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
carrier='a35ec692e79538f255f850842f4ade1afdbf298f';compiled='6ab43f6add261d90628fc07d6aacf42dfbae0561'
def h(b):return hashlib.sha256(b).hexdigest()
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root)
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def read(p):return json.loads(p.read_bytes())
assert git(R,'rev-parse','HEAD').decode().strip()==git(C,'rev-parse','HEAD').decode().strip()==carrier
assert not git(R,'status','--porcelain') and not git(C,'status','--porcelain')
assert git(R,'ls-remote','origin','refs/heads/1536-wound-b2-cloud-20261012').decode().split()[0]==carrier
assert not (C/'.git/objects/info/alternates').exists()
origin=git(C,'remote','get-url','origin').decode().strip();assert 'github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn' in origin
assert not git(R,'diff','--name-only',compiled,carrier,'--','.',':!specs/1536-complete-wound-materialization')
for name in ['AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md']:
 assert (R/name).read_bytes()==(C/name).read_bytes()==git(R,'show',compiled+':'+name)
cloud=read(R/(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));prep=cloud['freshPreparation'];packet=read(R/(REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'))
assert len(packet['sourcePins'])==98 and h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']==prep['sourcePinDigest']
for pin in packet['sourcePins']:
 b=(R/pin['path']).read_bytes();assert b==(C/pin['path']).read_bytes() and len(b)==pin['bytes'] and h(b)==pin['sha256'] and blob(b)==pin['gitBlob']
 mode,typ,gb=git(C,'ls-tree',carrier,'--',pin['path']).split(b'\t')[0].decode().split();assert (mode,gb)==(pin['mode'],pin['gitBlob'])
assert h((R/packet['ledger']['path']).read_bytes())==packet['ledger']['sha256']
for sr in cloud['sourceReview']['artifacts']:assert h((R/sr['path']).read_bytes())==sr['sha256']
archive_path=R/prep['archive']['path'];assert h(archive_path.read_bytes())==prep['archive']['sha256']=='06abc6c48256844aafd5e4b9761c505f82aa3ae6b906c20378c7c9075b80081d'
a=read(archive_path);assert len(a['files'])==a['rawChainCount']==32
assert a['nativeExits']==[0,0,0] and a['actualSources']==a['compiledSources']==[compiled]*3
assert a['helperText']==(R/(REC+'t179pd-ni-iobs-l01-archive-whole.py')).read_text()
archive_paths=set();raws={}
for f in a['files']:
 assert f['rawPath'] not in archive_paths;archive_paths.add(f['rawPath'])
 stored=archive_path.parent/f['storedPath'];sb=stored.read_bytes();assert sb==(C/stored.relative_to(R)).read_bytes()
 assert len(sb)==f['storedBytes'] and h(sb)==f['storedSha256'];raw=gzip.decompress(sb)
 assert len(raw)==f['bytes'] and h(raw)==f['sha256'] and raw==Path(f['rawPath']).read_bytes();raws[f['rawPath']]=raw
expected_paths=set();records={};summaries={};plans={};checks=[];warn={};processes=[]
source_bytes=(R/(REC+'t179pd-ni-iobs-l01-source-packet-20261018.json')).read_bytes();ledger_bytes=(R/packet['ledger']['path']).read_bytes()
for x in prep['records']:
 p=Path(x['path']);r=read(p);mode=r['mode'];records[mode]=r
 assert h(p.read_bytes())==x['sha256'] and r['actualSource']==r['compiledSource']==r['headAfter']==compiled
 assert r['kind']=='FIRST_NI_IOBS_L01_WHOLE_ZERO_PREPARATION_RAW' and r['nativeExit']==r['actualTestsExecuted']==0 and r['statusAfter']==''
 assert r['helperSource']==(R/(REC+'t179pd-ni-iobs-l01-process-preparation-run.py')).read_text()
 assert r['sourcePinDigest']==packet['pinDigest'] and r['sourcePinCount']==98 and r['sourceBindingSha256']==h(source_bytes) and r['ledgerContextSha256']==h(ledger_bytes)
 assert (p.parent/'source-binding.json').read_bytes()==source_bytes and (p.parent/'ledger-context.json').read_bytes()==ledger_bytes
 assert r['namespaceBefore']==r['namespaceAfter']=={'ownedRoots':[]} and r['processesBefore']==r['processesAfter']==[]
 assert h(Path(r['stdoutPath']).read_bytes())==r['stdoutSha256']
 process=read(Path(r['processRecord']));assert process['argv']==r['argv'] and process['startedUtc']==r['startedUtc']
 ident=process['identity'];pid=ident['pid'];assert int(ident['stat'].split(' ',1)[0])==pid and int(ident['stat'].rsplit(')',1)[1].split()[19])>0
 assert '(pwsh)' in ident['stat']
 if ident['cmdline']:assert 'pwsh' in ident['cmdline'] and '-File scripts/test-csharp.ps1' in ident['cmdline']
 assert any('-'+str(pid)+'-' in d for d in r['resultDirectories'])
 processes.append({'mode':mode,'pid':pid,'kernelBirthTick':int(ident['stat'].rsplit(')',1)[1].split()[19]),'cmdlineCaptured':bool(ident['cmdline']),'launchArgvAndRunnerPidBound':True})
 sentinel=read(Path(r['firstSentinel']));assert sentinel['mode']==mode and sentinel['actualSource']==compiled and sentinel['label']==r['label'] and sentinel['startedUtc']==r['startedUtc']
 for q in [p,Path(r['stdoutPath']),Path(r['processRecord']),Path(r['firstSentinel']),p.parent/'source-binding.json',p.parent/'ledger-context.json']:expected_paths.add(str(q))
 for name in ['observed-plan.json','exact-selected-scope.json']:
  if (p.parent/name).is_file():expected_paths.add(str(p.parent/name))
 assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);expected_paths.update(str(q) for q in rd.rglob('*') if q.is_file());assert not list(rd.rglob('*.trx'))
 s=read(rd/'summary.json');summaries[mode]=s
 assert s['ExitCode']==0 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded']
 assert s['Tests']=={'Total':0,'Executed':0,'Passed':0,'Failed':0} and not s['CategoryResults'] and not s['AdapterResults'] and not s['DuplicateTests']
 assert s['Revision']['Head']==compiled and s['Revision']['ChangedFiles']==0
 assert s['Selection']['CompletedDescriptors']==s['Selection']['CompletedCases']==0 and s['Selection']['Complete']==False
 log=(rd/'dotnet-test.log').read_text();phase_exits=re.findall(r'^===== ([^\n]+): exit (-?\d+) =====$',log,re.M);assert phase_exits and all(exit_=='0' for _,exit_ in phase_exits)
 if mode=='catalog':
  assert r['argv'][-1]=='-ValidateCatalog' and s['DiscoveryOnly'] and not s['PlanOnly'];audit=read(rd/'catalog-audit.json');assert audit==s['CatalogAudit'];assert audit['Valid'] and audit['Methods']==11800 and audit['Categories']==808 and not audit['Unmapped'] and not audit['StaleSelectors']
  for build,n in [('Build-integration',148),('Build-unit',37)]:
   text=(rd/(build+'.log')).read_text();assert 'Build succeeded.' in text and re.search(r'^\s+0 Error\(s\)',text,re.M) and not re.search(r': error [A-Za-z0-9]+:',text)
   assert re.search(r'^\s+'+str(n)+r' Warning\(s\)',text,re.M)
   warnings=sorted(set(line for line in text.splitlines() if ': warning ' in line));assert len(warnings)==n
   warn[build]={'warnings':n,'errors':0,'codes':dict(collections.Counter(re.search(r'warning (\w+)',line).group(1) for line in warnings))}
  assert all((b,'0') in phase_exits for b in ['Build-integration','Build-unit','integration-discovery','unit-discovery'])
 else:
  assert '-PlanOnly' in r['argv'] and '-NoBuild' in r['argv'] and s['PlanOnly'] and not s['DiscoveryOnly']
  assert not any(name.startswith('Build-') for name,_ in phase_exits)
  plan=read(rd/'plan.json');plans[mode]=plan;assert plan==read(p.parent/'observed-plan.json');assert plan['Revision']['Head']==compiled
  assert r['binaryBefore']==r['binaryAfter'] and r['allOutputInventoryBefore']==r['allOutputInventory']
 checks.append({'mode':mode,'record':str(p),'sha256':h(p.read_bytes()),'nativeExit':0,'seconds':r['wallSeconds'],'executed':0,'phaseExits':phase_exits})
assert expected_paths==archive_paths,(expected_paths-archive_paths,archive_paths-expected_paths)
cat=records['catalog'];whole=records['plan'];remaining=records['remaining-plan'];nine=cat['binaryAfter'];inv=cat['allOutputInventory'];assert len(nine)==9 and len(inv)==1575
assert nine==whole['binaryBefore']==whole['binaryAfter']==remaining['binaryBefore']==remaining['binaryAfter']
assert inv==whole['allOutputInventoryBefore']==whole['allOutputInventory']==remaining['allOutputInventoryBefore']==remaining['allOutputInventory']
roots=[R/x/'bin/Debug/net8.0' for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
physical=[str(q.relative_to(R)) for q in sorted(q for root in roots for q in root.rglob('*') if q.is_file())];assert physical==[x['path'] for x in inv]
for row in inv:
 b=(R/row['path']).read_bytes();assert len(b)==row['bytes'] and h(b)==row['sha256'],row['path']
lookup={x['path']:x for x in inv}
for row in nine:assert lookup[row['path']]==row
for name,n in [('BookOfEternityClient.dll',4),('BookOfEternityClient.TestSupport.dll',2)]:
 hashes=[x['sha256'] for x in nine if Path(x['path']).name==name];assert len(hashes)==n and len(set(hashes))==1
before={x['path']:x for x in cat['binaryBefore']};assert len(before)==7
assert all(before.get(x['path'])!=x for x in nine)
selection=read(R/'tests/selection.json');ledger=read(R/packet['ledger']['path']);sourcecheck=read(D/'t179pd-ni-iobs-l01-cloud-source-check-20261018.json')
def methodmap(plan):
 m={}
 for d in plan['Descriptors']:
  assert d['Adapter'] in ['unit','integration'] and not d['FrontendBuild']
  assert d['EstimatedCases']==sum(row['Cases'] for row in d['ExpectedMethodCounts'])
  assert set(d['Filter'].split('|'))=={'FullyQualifiedName='+row['Name'] for row in d['ExpectedMethodCounts']}
  for row in d['ExpectedMethodCounts']:assert row['Name'] not in m;m[row['Name']]=row['Cases']
 return m
wm=methodmap(plans['plan']);pm=methodmap(plans['remaining-plan']);assert len(plans['plan']['Descriptors'])==16 and len(wm)==33 and sum(wm.values())==57
assert len(plans['remaining-plan']['Descriptors'])==7 and len(pm)==12 and sum(pm.values())==20
assert {d['Category'] for d in plans['plan']['Descriptors']}=={x['category'] for x in selection['selections']}
pendingcats={x['category'] for x in cloud['pending']};assert {d['Category'] for d in plans['remaining-plan']['Descriptors']}==pendingcats
for row in ledger['rows']:assert wm[row['method']]==row['staticRows']
assert all(wm[m]==n for m,n in pm.items())
retained={m:n for m,n in wm.items() if m not in pm};assert retained==sourcecheck['retained']['methodCases']
for scope in cloud['pending']:
 selected=next(d for d in plans['remaining-plan']['Descriptors'] if d['Category']==scope['category']);assert {r['Name']:r['Cases'] for r in selected['ExpectedMethodCounts']}=={r['method']:r['cases'] for r in scope['methods']}
selection_path=Path(remaining['argv'][remaining['argv'].index('-SelectionFile')+1]);actual_selection=read(selection_path);assert actual_selection==dict(schemaVersion=1,selections=[x for x in selection['selections'] if x['category'] in pendingcats])
assert h(selection_path.read_bytes())==remaining['selectionSha256'];assert h((R/'tests/selection.json').read_bytes())==whole['selectionSha256']==whole['wholeSelectionSha256']==remaining['wholeSelectionSha256']
# Exact carrier recovery, not an inferred source restore from the working checkout.
receipt=read(Path('/tmp/1536-cloud-tools/prep-carrier-recovery-a35ec.json'));changed=git(R,'diff','--name-only',compiled,carrier).decode().splitlines();assert len(changed)==40 and set(changed)=={x['path'] for x in receipt['files']}
for f in receipt['files']:
 b=(C/f['path']).read_bytes();assert b==(R/f['path']).read_bytes() and len(b)==f['bytes'] and h(b)==f['sha256'] and blob(b)==f['gitBlob']
 gb=git(C,'ls-tree',carrier,'--',f['path']).split(b'\t')[0].decode().split()[-1];assert gb==f['gitBlob']
# Preserve earlier failed attempt and independently verify both old and corrected environment evidence.
failure=cloud['preparationAttempts'][0];fa=R/failure['archive'];assert h(fa.read_bytes())==failure['archiveSha256'];farc=read(fa);assert farc['nativeExits']==[1] and len(farc['files'])==8
for f in farc['files']:
 b=(fa.parent/f['storedPath']).read_bytes();assert len(b)==f['storedBytes'] and h(b)==f['storedSha256'];raw=gzip.decompress(b);assert len(raw)==f['bytes'] and h(raw)==f['sha256']
 if f['rawPath'].endswith('/record.json'):assert read(Path(f['rawPath']))['actualTestsExecuted']==0 and Path(f['rawPath']).read_bytes()==raw
corr=cloud['environmentCorrection'];env=Path('/tmp/1536-cloud-tools/env.sh').read_bytes();assert env.decode()==corr['envShell'] and h(env)==corr['envShellSha256']
addition=('export XDG_RUNTIME_DIR='+corr['path']+'\n').encode();assert env.count(addition)==1 and h(env.replace(addition,b''))==corr['envShellBeforeSha256']
assert not re.search(rb'^\s*export\s+HOME=',env,re.M)
runtime=Path(corr['path']);st=runtime.lstat();assert runtime.is_dir() and not runtime.is_symlink() and str(runtime.resolve())==corr['realpath'] and st.st_uid==corr['uid']==os.getuid() and stat.S_IMODE(st.st_mode)==0o700
assert str(runtime.parent)==cloud['environment']['root'] and (runtime/'.dotnet').is_dir()
assert Path('/proc/sys/kernel/random/boot_id').read_text().strip()==cloud['environment']['linuxBootId']
assert not [q for pattern in ['boe-gameengine-turnlifecycle-*','boe-joint-interface-crash-*'] for q in runtime.parent.glob(pattern)]
assert h(gzip.decompress((R/corr['diagnosticTrace']['path']).read_bytes()))==corr['diagnosticTrace']['rawSha256']
# No process intervention: current new-environment read-only census of only game/test command names.
game=[]
for d in Path('/proc').iterdir():
 if not d.name.isdigit():continue
 try:
  exe=(d/'exe').resolve();cmd=(d/'cmdline').read_bytes().decode(errors='replace').replace('\0',' ')
  if exe.name in {'dotnet','testhost'} and re.search(r'testhost\.dll|BookOfEternityClient(?:\.TestSupport|\.IntegrationTests|\.Tests)?\.dll',cmd):game.append(int(d.name))
 except (OSError,ValueError):pass
assert not game
out={'kind':'INDEPENDENT_NEW_CLOUD_ZERO3_PREPARATION_AND_RECOVERED_CARRIER_CHECK','checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'carrier':carrier,'compiledSource':compiled,'sourcePinCount':98,'sourcePinDigest':packet['pinDigest'],'ledgerSha256':packet['ledger']['sha256'],'archive':{'path':str(archive_path.relative_to(R)),'sha256':h(archive_path.read_bytes()),'originalGzipRawChainsVerified':32,'completeRawCoverage':True,'allLocalOriginalBytesMatchedSavedAndRestored':True},'records':checks,'catalog':summaries['catalog']['CatalogAudit'],'builds':warn,'whole':{'descriptors':16,'methods':33,'cases':57,'executed':0},'pending':{'descriptors':7,'methods':12,'cases':20,'executed':0},'retainedDifference':{'methods':len(retained),'cases':sum(retained.values()),'equalsPreviouslyQualifiedOriginalProvenance':True},'binaries':{'copies':9,'allFourClientCopiesEqual':True,'bothSupportCopiesEqual':True,'allNineFreshlyChangedOrAddedDuringCatalog':True,'all1575PathsSizesHashesMatchPhysicalOutputAndBothPlans':True},'processRecords':processes,'cleanup':{'allZeroRecordsAndRunnerClean':True,'currentGameProcesses':game,'currentOwnedFixtureRoots':[],'runtimeScratch':'Ordinary compiler/runtime scratch remains under GUID TMPDIR; no claim entire TMPDIR is empty, no deletion or process intervention.'},'recovery':{'independentClone':str(C),'head':carrier,'clean':True,'originGitHub':True,'noObjectAlternates':True,'remoteBranchShaVerified':carrier,'changedPaths':40,'bytesSha256GitBlobsMatch':True},'environment':{'xdgRuntimePath':str(runtime),'realpath':str(runtime.resolve()),'uid':st.st_uid,'mode':'0700','envShellSha256':h(env),'exactOnlyXdgRuntimeAddition':True,'homeNotReassigned':True,'sameNewCloudBoot':True,'originalFailedCatalog':{'nativeExit':1,'tests':0,'separateArchiveChainsVerified':8}},'legacy3Eligibility':'After saved independent PREP acceptance and Root named activation, exact legacy3 NoBuild is eligible first; other modes require their own named release; crash5 remains separate under accepted new isolation plan. No automatic advance/retry.','limits':'ZERO preparation only. No new product semantic result, old37 replay/cross-build equality, recovered old corrected crash result, Windows/CI/fullNI/HOT/F1/M3/T197/A3 qualification.','findings':[]}
op=D/'t179pd-ni-iobs-l01-cloud-prep-independent-check-a35ec-20261018.json';op.write_text(json.dumps(out,indent=2)+'\n');print(str(op));print(h(op.read_bytes()));print(json.dumps({k:out[k] for k in ['sourcePinCount','whole','pending','retainedDifference','binaries']}))
