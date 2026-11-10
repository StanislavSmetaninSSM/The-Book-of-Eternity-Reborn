from pathlib import Path
import subprocess,os,json,hashlib,gzip,re,stat
R=Path('/workspace/The-Book-of-Eternity-Reborn'); S='8e0a5e40d28b103fa509f40dd7f1a5b293f6545c'; C='a09272e91b379adb792450cd9e9d4f2a928d8a9b'; B='f9cf1a16b2d186d8ecfc00333f9b04a2e950efe5'; F='f9bc5cc824cbc374225e64e8fe6c2da4cd8f09c9'; BASE='7acd4a960fa273e0df2dd83e4998baef79ba94d5'
P='specs/1536-complete-wound-materialization/'; REC=P+'recovery/'; E=dict(os.environ,GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0'); cmds=[]; pins={}; projections=[]
def sha(b):return hashlib.sha256(b).hexdigest()
def git(*a,cwd=R):
 q=subprocess.run(['git',*a],cwd=cwd,env=E,stdout=subprocess.PIPE,stderr=subprocess.PIPE);assert q.returncode==0,(a,q.stderr.decode());cmds.append({'argv':['git',*a],'cwd':str(cwd),'exit':0,'stdoutBytes':len(q.stdout),'stdoutSha256':sha(q.stdout)});return q.stdout
def pin(p,rev=S):
 key=rev+':'+p
 if key not in pins:
  b=git('show',key); ent=git('ls-tree',rev,'--',p).decode().strip().split();assert ent[1]=='blob';pins[key]={'revision':rev,'path':p,'blob':ent[2],'mode':ent[0],'bytes':len(b),'sha256':sha(b)}
  if rev==S:assert b==(R/p).read_bytes(),p
 return git('show',key)
def projection(p,a,b):
 raw=pin(p);v=b''.join(raw.splitlines(keepends=True)[a-1:b]);projections.append({'path':p,'revision':S,'startLine':a,'endLine':b,'sha256':sha(v),'text':v.decode()})
def load(p):return json.loads(pin(p))
assert git('rev-parse','HEAD').decode().strip()==S
assert git('status','--porcelain')==b''
release='tests/selections/1536-hot-worker-windows.json';assert git('ls-tree',S,'--',release)==b'' and not (R/release).exists()
diffs=[]
for label,l,r,paths in [('bounded',B,S,[]),('compiled-continuity',C,S,[]),('production-continuity',F,S,['BookOfEternityClient','scripts','TestSupport','BookOfEternityClient.Tests']),('fixture',B,S,['BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.HotWorkerDisposition.cs'])]:
 argv=['diff','--no-ext-diff','--no-renames','--binary',l,r,'--',*paths];b=git(*argv);out=Path('/tmp/t179pd-hot-worker-windows-source-'+label+'-20261016.diff');assert not out.exists() or out.read_bytes()==b;
 if not out.exists():out.write_bytes(b)
 diffs.append({'label':label,'argv':['git',*argv],'path':str(out),'bytes':len(b),'sha256':sha(b)})
 if label=='production-continuity':assert not b
changed=git('diff','--name-only',B,S).decode().splitlines(); expected={'.github/workflows/fr063-hot-worker-windows.yml','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.HotWorkerDisposition.cs','tests/categories.json','tests/selection.json'}
assert {p for p in changed if not p.startswith(P)}==expected
cc=git('diff','--name-only',C,S).decode().splitlines();assert all(p=='.github/workflows/fr063-hot-worker-windows.yml' or p.startswith(P) for p in cc)
chains=[]; summaries={}; natives={}
for phase in ['catalog','plan']:
 stem=REC+'t179pd-hot-custody-worker-windows-prep-'+phase+'-20261016-';idx=load(stem+'artifact-index.json');assert idx['source_sha']==C and idx['native_exit_code']==0
 for row in idx['artifacts']:
  p=REC+row['path'];stored=pin(p);raw=gzip.decompress(stored) if p.endswith('.gz') else stored;assert sha(stored)==row['stored_sha256'];assert sha(raw)==row['raw_sha256'] and len(raw)==row['raw_bytes'];assert raw==Path(row['raw_path']).read_bytes();chains.append({'phase':phase,**row,'committedPath':p,'gitBlob':pins[S+':'+p]['blob'],'verifiedRawLocalStoredGit':True})
 summary=load(stem+'summary.json');native=load(stem+'native-exit.json');summaries[phase]=summary;natives[phase]=native
 assert summary==idx['summary'];assert summary['Revision']['Head']==C and summary['Revision']['ChangedFiles']==0 and summary['ExitCode']==0 and not summary['TimedOut'] and summary['Tests']=={'Total':0,'Executed':0,'Passed':0,'Failed':0};assert summary['OwnedTreeCleanupSucceeded'] and summary['RuntimeCleanupSucceeded'];assert native['actual_head']==C and native['compiled_source']==C and native['native_exit']==0
assert len(chains)==12
ca=summaries['catalog'];assert ca['DiscoveryOnly'] and ca['CatalogAudit']=={'Valid':True,'Methods':11725,'Categories':777,'Unmapped':[],'StaleSelectors':[]}
assert {x['Name'] for x in ca['PhaseResults'] if x['Completed']}>={'Build-unit','Build-integration'}
pl=load(REC+'t179pd-hot-custody-worker-windows-prep-plan-20261016-plan.json');assert len(pl['Descriptors'])==1;d=pl['Descriptors'][0];assert d['Category']=='fr063-hot-worker-ready-disposition' and d['EstimatedCases']==2 and d['BudgetMinutes']==5 and d['Exclusive'] and not d['FrontendBuild'];assert d['ExpectedMethodCounts']==[{'Name':'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.HotSubmission_ActualWorkerApplyCannotIssueOrdinaryReadyCustody','Cases':2}]
manifest=load(REC+'t179pd-hot-custody-worker-windows-prep-binaries-20261016.json');assert manifest['source_sha']==C and len(manifest['binaries'])==4
for row in manifest['binaries']:assert sha((R/row['path']).read_bytes())==row['sha256']
rcpath=Path('/tmp/t179pd-hot-custody-worker-windows-prepared-delta-recovery-20261016.json');rcraw=rcpath.read_bytes();rc=json.loads(rcraw);assert rc['sha']==S and rc['base_full_tree']==BASE and rc['count']==172 and rc['changed_count']==169 and not rc['deleted_paths']
assert git('diff','--name-status','--no-renames',BASE,S).decode()==rc['name_status']
actualchanged=set(git('diff','--name-only','--no-renames',BASE,S).decode().splitlines());assert len(actualchanged)==169
rows=rc['files'];assert len(rows)==len({x['path'] for x in rows})==172 and actualchanged<={x['path'] for x in rows}
for row in rows:
 p=row['path'];raw=pin(p);assert raw==(Path(rc['directory'])/p).read_bytes();v=pins[S+':'+p];assert len(raw)==row['bytes'] and sha(raw)==row['sha256'] and v['blob']==row['git_blob'] and v['mode']==row['mode'];assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==row['git_blob'];mode=(Path(rc['directory'])/p).stat().st_mode;assert bool(mode&stat.S_IXUSR)==(row['mode']=='100755')
base=Path(rc['base_full_tree_directory']);assert git('rev-parse','HEAD',cwd=base).decode().strip()==BASE;assert git('status','--porcelain','--untracked-files=all','--ignored',cwd=base)==b'';assert not (base/'.git/objects/info/alternates').exists();assert not (base/'.git/info/sparse-checkout').exists()
baseReceipt=Path(rc['base_full_tree_receipt']);assert sha(baseReceipt.read_bytes())=='0a51a61038e5c03b2a639e2e011999b254ee40c8539e1a0ba3170f25cb8251b0'
cat=load('tests/categories.json');sel=load('tests/selection.json');owner=[x for x in cat['categories'] if x['id']==d['Category']];assert len(owner)==1 and owner[0]['requirements']=={'frontendBuild':False,'exclusive':True,'ciRunner':'windows-latest'} and owner[0]['timeoutMinutes']==5
matrix=load(REC+'t179pd-hot-worker-windows-ci-matrix-20261016.json');win=[x for x in matrix['include'] if x['runner']=='windows-latest'];assert len(win)==1 and [x['category'] for x in win[0]['selection']['selections']]==[d['Category']]
support=load(REC+'t179pd-hot-custody-worker-windows-preparation-supports-20261016.json')
for x in support['files']:assert sha(pin(REC+x['path']))==x['sha256'] and (R/(REC+x['path'])).read_bytes()==Path(x['raw_path']).read_bytes()
for p in ['AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md',P+'spec.md',P+'plan.md',P+'tasks.md',P+'design/lawful-active-joint-tail-b-implementation-plan.md','scripts/test-csharp.ps1','scripts/testing/TestCategoryCatalog.psm1','scripts/check-dotnet-warning-budget.ps1','.github/workflows/portable-load-linux.yml','.github/workflows/dotnet-ci.yml','BookOfEternityClient/Services/GmWorkers/GmWorkerBackendSelector.cs','BookOfEternityClient/Services/GmWorkers/GmWorkerBridgePool.cs','BookOfEternityClient/Services/GmWorkers/GmWorkerWindowsOwnedLaunch.cs','BookOfEternityClient/Services/GmWorkers/GmWorkerProcessTree.cs','BookOfEternityClient/Services/GmWorkers/GmWorkerProcessHost.cs','BookOfEternityClient/Services/GmWorkers/GmWorkerValidationRepairDelegator.cs','BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SpiritualContinuation.cs','BookOfEternityClient.IntegrationTests/CleanupPublicationCut.cs']:
 pin(p)
for p,a,b in [('.github/workflows/fr063-hot-worker-windows.yml',1,48),('.github/workflows/fr063-hot-worker-windows.yml',49,88),('.github/workflows/fr063-hot-worker-windows.yml',89,125),('.github/workflows/fr063-hot-worker-windows.yml',126,151),('.github/workflows/fr063-hot-worker-windows.yml',152,206),('BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.HotWorkerDisposition.cs',86,100),('BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.HotWorkerDisposition.cs',109,198),('BookOfEternityClient/Services/GmWorkers/GmWorkerBackendSelector.cs',1,70),('BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SpiritualContinuation.cs',480,510),(P+'plan.md',16,32)]:projection(p,a,b)
# Filename feasibility only: does not execute checkout on Windows.
tree=git('ls-tree','-rz',S); names=[];invalid=[];symlinks=[];seen={};collisions=[];components={}
for entry in tree.split(b'\0'):
 if not entry:continue
 meta,raw=entry.split(b'\t',1);name=raw.decode();names.append(name)
 if meta.split()[0]==b'120000':symlinks.append(name)
 for comp in name.split('/'):
  if re.search(r'[<>:"\\|?*\x00-\x1f]',comp) or comp.endswith(('.', ' ')) or len(comp.encode('utf-16-le'))//2>255 or re.fullmatch(r'(?i)(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?',comp):invalid.append(name)
 for i in range(1,len(name.split('/'))+1):
  part='/'.join(name.split('/')[:i]);key=part.casefold()
  if key in components and components[key]!=part:collisions.append([components[key],part])
  components[key]=part
assert not invalid and not symlinks and not collisions
assert git('status','--porcelain')==b'' and git('rev-parse','HEAD').decode().strip()==S
result={'reviewedSource':S,'compiledSource':C,'scopeBase':B,'priorRuntimeSource':F,'allChecksPassed':True,'gitPins':list(pins.values()),'projections':projections,'diffs':diffs,'argvResults':cmds,'preparation':{'summaries':summaries,'native':natives,'plan':pl,'chains':chains,'manifest':manifest},'recovery':{'receiptPath':str(rcpath),'receiptSha256':sha(rcraw),'receipt':rc,'currentAllDeltaBytesVerified':True,'fullBaseReceiptSha256':sha(baseReceipt.read_bytes()),'fullBaseClean':True,'priorFullBaseByteScanRetainedNotRepeated':True},'selection':{'owner':owner[0],'wholeSelection':sel,'matrix':matrix},'windowsTreeStatic':{'files':len(names),'longestRelativeCharacters':max(map(len,names)),'over180':sum(len(n)>180 for n in names),'invalid':invalid,'symlinks':symlinks,'caseCollisions':collisions},'releaseFileAbsent':True,'reviewerRuntimeCommands':[]}
out=Path('/tmp/t179pd-hot-worker-windows-source-inspection-20261016.json');assert not out.exists();out.write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n');print(json.dumps({'path':str(out),'sha256':sha(out.read_bytes()),'pins':len(pins),'projections':len(projections),'chains':len(chains),'delta':len(rows),'tree':result['windowsTreeStatic']}))
