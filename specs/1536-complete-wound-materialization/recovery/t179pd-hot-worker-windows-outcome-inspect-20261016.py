from pathlib import Path
import subprocess,os,json,hashlib,stat,re,datetime
R=Path('/workspace/The-Book-of-Eternity-Reborn');T='ca4c08b6b07bc6edf79e08f109bf14652666cbdf';S='8e0a5e40d28b103fa509f40dd7f1a5b293f6545c';A='bd407f0a4cc0a38db838b41a2bba32ed064babbf';X='53a38ff8d6e18e441e4d6f53a0738bc6bccc64b6';BASE='7acd4a960fa273e0df2dd83e4998baef79ba94d5';P='specs/1536-complete-wound-materialization/';REC=P+'recovery/';E=dict(os.environ,GIT_OPTIONAL_LOCKS='0',GIT_NO_LAZY_FETCH='1');commands=[];pins={};cache={}
def sha(b):return hashlib.sha256(b).hexdigest()
def git(*args,cwd=R):
 p=subprocess.run(['git',*args],cwd=cwd,env=E,stdout=subprocess.PIPE,stderr=subprocess.PIPE);assert p.returncode==0,(args,p.stderr.decode());commands.append({'argv':['git',*args],'cwd':str(cwd),'exit':0,'stdoutBytes':len(p.stdout),'stdoutSha256':sha(p.stdout)});return p.stdout
def pin(path,rev=T):
 key=rev+':'+path
 if key not in pins:
  tree=git('ls-tree',rev,'--',path).decode().split();assert tree[1]=='blob';blob=tree[2]
  if blob not in cache:cache[blob]=git('cat-file','blob',blob)
  raw=cache[blob];pins[key]={'revision':rev,'path':path,'mode':tree[0],'blob':blob,'bytes':len(raw),'sha256':sha(raw)}
 return cache[pins[key]['blob']]
def obj(path,rev=T):return json.loads(pin(path,rev))
assert git('rev-parse','HEAD').decode().strip()==T and not git('status','--porcelain')
release='tests/selections/1536-hot-worker-windows.json';assert git('rev-list','--parents','-n','1',X).decode().split()==[X,A];assert git('diff','--name-status',A,X).decode()==f'A\t{release}\n';assert not git('ls-tree',A,'--',release)
grant=obj(release,X);assert grant['reviewedSource']==S and grant['acceptedCheckpoint']==A and len(grant['selections'])==1 and grant['selections'][0]['category']=='fr063-hot-worker-ready-disposition'
prior=[]
for ext,wanted in [('md','4b296c8b0318017e91442ad0df6a0451bc7a64ae15963ec2991ae5c5ed7003ae'),('json','fd5dbe79fd801210b91c17965b4b0da21020fbcc7dd7fdde13583a1ee802ab48')]:
 path=grant['sourceReview'][ext]['path'];assert grant['sourceReview'][ext]['sha256']==wanted;raw=pin(path,A);assert sha(raw)==wanted and pin(path)==raw;prior.append({'path':path,'sha256':wanted,'atAcceptance':A})
sr=json.loads(pin(grant['sourceReview']['json']['path'],A));assert sr['reviewedSource']==S and sr['decision']=='ACCEPTED_SOURCE_FIRST_WINDOWS_WORKER2' and sr['markdownSha256']==grant['sourceReview']['md']['sha256'];assert sr['releasedActuals']==[{'category':'fr063-hot-worker-ready-disposition','expectedCases':2,'budgetMinutes':5,'platform':'windows-latest','attempt':'FIRST_NATIVE_WINDOWS_ONLY'}]
for ext,wanted in [('md','2b1234c135d19d5abffb91c4c1f9641dd90ac93fb323ec42c6277320d7fc1435'),('json','23851b8fb75a9eac543b5a379cebb46cde209c348f2b51664b40770764165606')]:
 path=REC+'t179pd-hot-worker-windows-availability-addendum-20261016.'+ext;raw=pin(path,A);assert sha(raw)==wanted and pin(path)==raw;prior.append({'path':path,'sha256':wanted,'atAcceptance':A})
workflow='.github/workflows/fr063-hot-worker-windows.yml';assert pin(workflow,S)==pin(workflow,X)==pin(workflow);assert sha(pin(workflow))=='e2a7332bc16d694cca9044381f226946b285721407b58902833c73913c1bc538'
diffs=[]
for label,l,r in [('source-to-acceptance',S,A),('acceptance-to-release',A,X),('release-to-blocker',X,T)]:
 argv=['diff','--no-ext-diff','--no-renames','--binary',l,r];raw=git(*argv);out=Path('/tmp/t179pd-hot-worker-windows-outcome-'+label+'-20261016.diff');assert not out.exists();out.write_bytes(raw);names=git('diff','--name-only',l,r).decode().splitlines();assert names==[release] if label=='acceptance-to-release' else all(n.startswith(P) for n in names);diffs.append({'label':label,'argv':['git',*argv],'path':str(out),'bytes':len(raw),'sha256':sha(raw),'changedPaths':names})
changed=diffs[-1]['changedPaths'];assert len(changed)==17
assert not git('diff',S,T,'--','BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','TestSupport','scripts','tests/categories.json','tests/selection.json','.github/workflows')
index=obj(REC+'t179pd-hot-worker-windows-first-outcome-preservation-index-20261016.json');assert index['source']==X and len(index['files'])==11;chains=[]
for row in index['files']:
 raw=pin(REC+row['path']);assert raw==Path(row['origin']).read_bytes() and sha(raw)==row['sha256'] and len(raw)==row['bytes'];chains.append({**row,'git':pins[T+':'+REC+row['path']],'verifiedLocalStoredGit':True})
receipt=obj(REC+'t179pd-hot-worker-windows-first-outcome-20261016.json');assert receipt['source']==X and receipt['runId']==38978917195;assert [o['nativeExit'] for o in receipt['observations']]==[0,0,0,1]
metadata={}
for ob in receipt['observations'][:3]:
 raw=pin(REC+'t179pd-hot-worker-windows-first-outcome-20261016-'+ob['label']+'.json');assert len(raw)==ob['stdoutBytes'] and sha(raw)==ob['stdoutSha256'];metadata[ob['label']]=json.loads(raw)
run=metadata['run'];assert run['id']==38978917195 and run['head_sha']==X and run['run_attempt']==1 and run['event']=='push' and run['head_branch']=='1536-wound-b2-cloud-20261012' and run['path']==workflow and run['status']=='completed' and run['conclusion']=='success'
jobs=metadata['jobs'];assert jobs['total_count']==1 and len(jobs['jobs'])==1;job=jobs['jobs'][0];assert job['id']==117234913253 and job['run_id']==run['id'] and job['run_attempt']==1 and job['head_sha']==X and job['labels']==['windows-latest'] and job['status']=='completed' and job['conclusion']=='success';assert all(s['status']=='completed' and s['conclusion']=='success' for s in job['steps']);duration=(datetime.datetime.fromisoformat(job['completed_at'].replace('Z','+00:00'))-datetime.datetime.fromisoformat(job['started_at'].replace('Z','+00:00'))).total_seconds();assert duration==551
artifacts=metadata['artifacts'];assert artifacts['total_count']==1 and len(artifacts['artifacts'])==1;art=artifacts['artifacts'][0];assert art==receipt['artifact'];assert art['id']==12080975497 and art['size_in_bytes']==811881 and art['digest']=='sha256:fa0992a149fc0cc3dc8af767b9ca54e6e26658ca67813aca81e5c87dac82f292' and art['workflow_run']['head_sha']==X and art['workflow_run']['id']==run['id'] and art['expires_at']=='2026-11-15T23:08:12Z'
denial=receipt['observations'][3];assert denial['label']=='artifact-zip' and denial['stdoutBytes']==0 and denial['stdoutSha256']==sha(b'') and 'productionresultssa4.blob.core.windows.net' in denial['stderrRedacted'] and '?[REDACTED]' in denial['stderrRedacted'] and 'Forbidden' in denial['stderrRedacted'];assert not Path('/tmp/t179pd-hot-worker-windows-first-outcome-20261016.zip').exists();assert not Path('/tmp/t179pd-hot-worker-windows-first-outcome-20261016-artifact').exists()
# Inspect only persisted new documentary artifacts for unredacted signed URL parameters.
secretFindings=[]
for path in changed:
 raw=pin(path);text=raw.decode()
 if re.search(r'https?://[^\s\"<>]*blob\.core\.windows\.net/[^\s\"<>]*\?(?!\[REDACTED\])',text):secretFindings.append(path)
 if re.search(r'[?&](?:sig|skoid|sktid|skt|ske|sks|skv)=[^\s\"<>]+',text,re.I):secretFindings.append(path)
assert not secretFindings
recoveries=[]
for rp,wanted,count,changes in [(R/(REC+'t179pd-hot-custody-worker-windows-release-accepted-delta-recovery-20261016.json'),A,195,192),(R/(REC+'t179pd-hot-custody-worker-windows-first-release-delta-recovery-20261016.json'),X,196,193),(Path('/tmp/t179pd-hot-custody-worker-windows-first-outcome-delta-recovery-20261016.json'),T,208,205)]:
 rb=rp.read_bytes();rc=json.loads(rb);assert rc['sha']==wanted and rc['base_full_tree']==BASE and rc['count']==count and rc['changed_count']==changes and not rc['deleted_paths'];assert rc['name_status']==git('diff','--name-status','--no-renames',BASE,wanted).decode();rows=rc['files'];assert len(rows)==len({r['path'] for r in rows})==count;changedset=set(git('diff','--name-only','--no-renames',BASE,wanted).decode().splitlines());assert len(changedset)==changes and changedset<={r['path'] for r in rows}
 for row in rows:
  raw=pin(row['path'],wanted);local=Path(rc['directory'])/row['path'];assert raw==local.read_bytes() and sha(raw)==row['sha256'] and len(raw)==row['bytes'];p=pins[wanted+':'+row['path']];assert p['blob']==row['git_blob'] and p['mode']==row['mode'] and bool(local.stat().st_mode&stat.S_IXUSR)==(row['mode']=='100755');assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==row['git_blob']
 recoveries.append({'receiptPath':str(rp),'receiptSha256':sha(rb),'sha':wanted,'count':count,'changedCount':changes,'allLocalBytesModesGitVerified':True,'directory':rc['directory'],'startedUtc':rc['started_utc'],'completedUtc':rc['completed_utc'],'remoteReadback':rc['remoteReadback']})
assert datetime.datetime.fromisoformat(recoveries[0]['completedUtc'])<datetime.datetime.fromisoformat(run['head_commit']['timestamp'].replace('Z','+00:00'))
base=Path('/workspace/f1-r2-hot-custody-implementation-final-source-github-20261016');assert git('rev-parse','HEAD',cwd=base).decode().strip()==BASE and not git('status','--porcelain','--untracked-files=all','--ignored',cwd=base);assert not (base/'.git/objects/info/alternates').exists() and not (base/'.git/info/sparse-checkout').exists()
baseReceipt=Path('/tmp/t179pd-hot-custody-implementation-final-full-recovery-20261016.json');assert sha(baseReceipt.read_bytes())=='0a51a61038e5c03b2a639e2e011999b254ee40c8539e1a0ba3170f25cb8251b0'
manifest=obj(REC+'t179pd-hot-custody-worker-windows-prep-binaries-20261016.json');assert manifest['source_sha']=='a09272e91b379adb792450cd9e9d4f2a928d8a9b'
for row in manifest['binaries']:assert sha((R/row['path']).read_bytes())==row['sha256']
finalpath=Path('/tmp/t179pd-hot-worker-windows-final-readonly-status-20261016.json');fb=finalpath.read_bytes();assert sha(fb)=='1f284ff9fc0d80a50a790a28b820eb3dd9eeb887e59a94749017c4199a28029a';final=json.loads(fb)
projections=[]
for path,a,b in [(P+'plan.md',16,26),(P+'tasks.md',69,76),(P+'spec.md',1,5),(P+'design/lawful-active-joint-tail-b-implementation-plan.md',1,5),(REC+'t179pd-fetch-first-worker-windows-evidence-20261016.py',1,40),(REC+'t179pd-create-first-worker-windows-release-20261016.py',1,25),('.github/workflows/fr063-hot-worker-windows.yml',89,125)]:
 raw=pin(path);part=b''.join(raw.splitlines(keepends=True)[a-1:b]);projections.append({'revision':T,'path':path,'firstLine':a,'lastLine':b,'bytes':len(part),'sha256':sha(part),'text':part.decode()})
assert git('rev-parse','HEAD').decode().strip()==T and not git('status','--porcelain')
out={'target':T,'reviewedSource':S,'acceptance':A,'release':X,'allChecksPassed':True,'priorImmutableReviewPairs':prior,'gitPins':list(pins.values()),'projections':projections,'diffs':diffs,'commands':commands,'preservationChains':chains,'rawApiMetadata':metadata,'outcomeReceipt':receipt,'releaseJson':grant,'jobWallSeconds':duration,'windowsNativePacketAvailable':False,'windowsRawResultClassification':'NOT_INDEPENDENTLY_ACCEPTED','downloadEvidence':denial,'newDocumentSignedQueryScan':{'paths':changed,'findings':secretFindings},'recovery':recoveries,'fullBaseReceiptSha256':sha(baseReceipt.read_bytes()),'localLinuxDllManifest':manifest,'finalReadOnlySnapshot':{'path':str(finalpath),'sha256':sha(fb),'contents':final},'reviewerRuntimeNetworkRepoWrites':False}
p=Path('/tmp/t179pd-hot-worker-windows-outcome-inspection-20261016.json');assert not p.exists();p.write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n');print(json.dumps({'path':str(p),'sha256':sha(p.read_bytes()),'pins':len(pins),'projections':len(projections),'preservationChains':len(chains),'recoveries':[(x['count'],x['changedCount']) for x in recoveries],'jobWallSeconds':duration}))
