import os,json,hashlib,subprocess,stat
from pathlib import Path
from collections import Counter
R=Path('/workspace/The-Book-of-Eternity-Reborn');T=Path('/tmp');rec='specs/1536-complete-wound-materialization/recovery/';source='d474cdd8546f8157b6ffd71840f268062ee05ed0';base='f186abee075654859d536073d8a582f213af0529';head='bc719d3f6002965167c5915d841e56c5dc45449a';prep='22e31d09999ff9cd8616116ff0e19f517a192426';stem='t179pd-hot-custody-full-recovery-independent'
env=dict(os.environ,GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
def git(*a,where=R):return subprocess.check_output(['git',*a],cwd=where,env=env)
def sha(b):return hashlib.sha256(b).hexdigest()
def bid(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def file_ref(p):
 p=Path(p);b=p.read_bytes();return dict(path=str(p),bytes=len(b),sha256=sha(b))
def save(p,b):
 with p.open('xb') as f:f.write(b)
 return dict(path=str(p),bytes=len(b),sha256=sha(b))
def pin(p,c=head):
 b=git('show',c+':'+p);return dict(path=p,commit=c,git_blob=bid(b),bytes=len(b),sha256=sha(b),writer_equal=(R/p).read_bytes()==b)
receiptp=T/'t179pd-hot-custody-red2-full-source-recovery-20261016.json';receipt=json.loads(receiptp.read_text());clone=Path(receipt['directory'])
assert receipt['sha']==head and receipt['new_directory'] and receipt['clean'] and receipt['noAlternates'] and receipt['noSparseCheckout'] and receipt['restoredBuildOrRuntime']=='NOT_RUN'
assert git('rev-parse','HEAD').decode().strip()==head and not git('status','--porcelain')
assert git('rev-parse','HEAD',where=clone).decode().strip()==head
assert not git('status','--porcelain','--ignored',where=clone)
assert not (clone/'.git/objects/info/alternates').exists() and not (clone/'.git/info/sparse-checkout').exists()
assert (clone/'.git/shallow').is_file()
assert git('remote','get-url','origin',where=clone).decode().strip()=='https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git'
tree=git('ls-tree','-rlz',head);assert tree==git('ls-tree','-rlz',head,where=clone)
entries=[]
for item in tree.split(b'\0'):
 if item:
  meta,path=item.split(b'\t',1);mode,kind,blob,size=meta.split();assert kind==b'blob' and mode in [b'100644',b'100755',b'120000'];entries.append((mode.decode(),blob.decode(),int(size),os.fsdecode(path)))
assert len(entries)==len({r[3] for r in entries})==receipt['count']
rows={r['path']:r for r in receipt['files']};assert len(rows)==len(entries) and set(rows)=={r[3] for r in entries}
assert sum(r[2] for r in entries)==receipt['tracked_bytes']
def open_physical(root,name,mode):
 p=root/name;st=p.lstat()
 if mode=='120000':
  assert stat.S_ISLNK(st.st_mode);return os.fsencode(os.readlink(p))
 assert stat.S_ISREG(st.st_mode) and bool(st.st_mode&0o111)==(mode=='100755');return p.open('rb')
CHUNK=1024*1024;cache={};inventory=hashlib.sha256();mode_counts=Counter();batch=subprocess.Popen(['git','cat-file','--batch'],cwd=R,env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE)
try:
 for index,(mode,blob,size,name) in enumerate(entries,1):
  expected=rows[name];assert expected['mode']==mode and expected['git_blob']==blob and expected['bytes']==size
  original=open_physical(R,name,mode);restored=open_physical(clone,name,mode);physical_hash=hashlib.sha256()
  fresh=blob not in cache
  if fresh:
   batch.stdin.write((blob+'\n').encode());batch.stdin.flush();assert batch.stdout.readline().split()==[blob.encode(),b'blob',str(size).encode()]
   object_hash=hashlib.sha1(b'blob '+str(size).encode()+b'\0')
  if mode=='120000':
   assert original==restored and len(restored)==size
   if fresh:
    body=batch.stdout.read(size);assert body==restored;object_hash.update(body)
   physical_hash.update(restored)
  else:
   try:
    remaining=size
    while remaining:
     n=min(remaining,CHUNK);a=original.read(n);b=restored.read(n);assert len(a)==len(b)==n and a==b
     if fresh:
      body=batch.stdout.read(n);assert body==b;object_hash.update(body)
     physical_hash.update(b);remaining-=n
    assert not original.read(1) and not restored.read(1)
   finally:original.close();restored.close()
  value=physical_hash.hexdigest();assert value==expected['sha256']
  if fresh:
   assert batch.stdout.read(1)==b'\n' and object_hash.hexdigest()==blob;cache[blob]=value
  else:assert cache[blob]==value
  inventory.update((json.dumps(dict(path=name,mode=mode,git_blob=blob,bytes=size,sha256=value),sort_keys=True,separators=(',',':'))+'\n').encode());mode_counts[mode]+=1
  if index%10000==0:print('READ_ONLY_FULL_BYTE_VERIFICATION',index,'of',len(entries),flush=True)
finally:
 batch.stdin.close();batch.stdout.close();assert batch.wait(timeout=30)==0
assert not git('status','--porcelain','--ignored',where=clone) and not git('status','--porcelain')
print('READ_ONLY_FULL_BYTE_VERIFICATION_COMPLETE',len(entries),receipt['tracked_bytes'],flush=True)
# Re-derive the eight finite reports independently; their semantic content is not re-reviewed here.
reports=[('t179pd-automatic-player-time-policy-engineering-independent-review-20261016.json',['source_evidence.candidate_pins']),('t179pd-hot-submitted-admission-engineering-design-20261016.json',['source_pins']),('t179pd-hot-custody-engineering-independent-review-20261016.json',['source_pins','current_governance_and_extra_pins']),('t179pd-narrative-interface-policy-engineering-design-20261016.json',['source_pins']),('t179pd-narrative-interface-policy-engineering-independent-review-20261016.json',['source_evidence.author_historical_pins','source_evidence.candidate_pins']),('t179pd-hot-custody-finite-causal-fixture-design-20261016.json',['source_pins']),('t179pd-hot-custody-red2-source-independent-review-20261016.json',['pins']),('t179pd-hot-custody-red2-owned-source-independent-review-20261016.json',['pins'])]
required=set();scopes=[]
for name,keys in reports:
 data=json.loads(git('show',head+':'+rec+name));paths=set()
 for key in keys:
  value=data
  for part in key.split('.'):value=value[part]
  paths.update(r['path'] for r in value)
 assert paths<=set(rows);required.update(paths);scopes.append(dict(report=pin(rec+name),count=len(paths),paths=sorted(paths),all_present=True))
assert len(required)==197
old=json.loads(git('show',head+':'+rec+'t179pd-hot-custody-red2-owner-scope-recovery-20261016.json'));oldpaths={r['path'] for r in old['files']};missing=sorted(required-oldpaths);assert len(missing)==17
previous=json.loads(git('show',head+':'+rec+'t179pd-hot-custody-owner-scope-recovery-independent-review-20261016.json'));assert missing==previous['omitted_concrete_paths']
auditpath=rec+'t179pd-recovery-scope-root-cause-audit-20261016.json';audit=json.loads(git('show',head+':'+auditpath));assert audit['complete_eight_report_required_union']==197 and audit['omitted_concrete_paths']==missing
assert audit['initial_readonly_audit_assertion']['assumed_union']==162 and audit['initial_readonly_audit_assertion']['measured_union']==197
scopeargs=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts'];assert not git('diff','--binary',source,head,'--',*scopeargs)
assert not git('diff','--check',base,head)
helper=rec+'t179pd-fresh-github-full-tree-restore-20261016.py';diffs=[]
for name,args in [('source-continuity',['diff','--binary',source,head,'--',*scopeargs]),('helper-audit',['diff','--binary',base,head,'--',helper,auditpath,auditpath.replace('.json','.py')]),('status',['diff','--binary',base,head,'--','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md','specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md'])]:diffs.append(dict(**save(T/(stem+'-'+name+'-20261016.diff'),git(*args)),argv=['git',*args]))
manifestpath=rec+'t179pd-hot-custody-red2-owned-prep-binaries-20261016.json';b=git('show',head+':'+manifestpath);assert b==git('show',prep+':'+manifestpath);manifest=json.loads(b);assert manifest['source_sha']==source
for r in manifest['binaries']:assert sha((R/r['path']).read_bytes())==r['sha256']
assert len(manifest['binaries'])==4
artifacts=[]
for phase in ['catalog','plan']:
 ip=rec+'t179pd-hot-custody-red2-owned-prep-'+phase+'-20261016-artifact-index.json';data=git('show',head+':'+ip);assert data==git('show',prep+':'+ip)
 for r in json.loads(data)['artifacts']:
  path=rec+r['path'];data=git('show',head+':'+path);assert data==git('show',prep+':'+path) and sha(data)==r['stored_sha256'];artifacts.append(pin(path))
assert len(artifacts)==12
prior=[]
for name,h in [('t179pd-hot-custody-owner-scope-recovery-independent-review-20261016.md','7707bb4965319c65cf3c11b0cbdb055f2607a965663e0a7b33b7512d1081a65a'),('t179pd-hot-custody-owner-scope-recovery-independent-review-20261016.json','cf703927d252014b189611b687762de212a24897f808d9c70fdcda922b8bc29e'),('t179pd-hot-custody-red2-owned-source-independent-review-20261016.md','6fb681405761e82fa7fd44971a17fa7b064342d94a9b81ced94cd8a3a54bc4e2'),('t179pd-hot-custody-red2-owned-source-independent-review-20261016.json','17f28e83864ab8796265942e0a401a1d54965f517391cf7400d745e58cd109ed')]:
 data=git('show',head+':'+rec+name);assert sha(data)==h and data==(T/name).read_bytes();prior.append(pin(rec+name))
np=T/'t179pd-hot-custody-red2-full-source-20261016.exit.json';native=json.loads(np.read_text());assert native['native_exit']==0 and native['actual_head']==head
assert native['argv']==['python',helper,head,str(clone),str(receiptp)]
logp=T/'t179pd-hot-custody-red2-full-source-20261016.log';log=logp.read_text();assert 'FRESH_GITHUB_FULL_CURRENT_TREE_RESTORE '+head+' '+str(len(entries))+' exact tracked files;' in log
wrapperp=T/'t179pd-run-full-source-recovery-20261016.py'
projections=[]
for first,last in [(11,38),(40,75),(76,86)]:
 text=''.join(git('show',head+':'+helper).decode().splitlines(keepends=True)[first-1:last]);projections.append(dict(path=helper,commit=head,start=first,end=last,text=text,sha256=sha(text.encode())))
proj=save(T/(stem+'-projections-20261016.json'),(json.dumps(projections,indent=2)+'\n').encode())
pinpaths=[helper,auditpath,auditpath.replace('.json','.py'),manifestpath,'AGENTS.md','docs/development-workflow.md','tests/categories.json','tests/selection.json','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md','specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md']+[rec+n for n,k in reports]+missing
out=dict(base=base,compiled_source=source,candidate=head,source_scope_unchanged=True,full_recovery=dict(receipt=file_ref(receiptp),directory=str(clone),head=head,files=len(entries),tracked_bytes=receipt['tracked_bytes'],unique_blobs=len(cache),mode_counts=dict(mode_counts),tree_inventory_sha256=sha(tree),verification_inventory_sha256=inventory.hexdigest(),inventory_digest_format='Git ls-tree order; compact sorted-key JSON per line with path/mode/git_blob/bytes/sha256; UTF-8 LF',all_file_modes_writer_clone_git_verified=True,clean_including_ignored=True,no_alternates=True,no_sparse=True,shallow=True,source_transport_and_remote='Coordinator native receipt/argv; reviewer no network',receipt_remote_readback=receipt['remoteReadback']),eight_report_scopes=scopes,required_paths=sorted(required),required_projection=[rows[p] for p in sorted(required)],required_count=197,prior_omissions=missing,all17_covered=True,root_cause_audit=pin(auditpath),pins=[pin(p) for p in dict.fromkeys(pinpaths)],diffs=diffs,projections=dict(**proj,count=3),binaries=manifest,unchanged_preparation=artifacts,prior_pairs=prior,native=native,external_supports=[file_ref(wrapperp),file_ref(np),file_ref(logp)],reviewer_runtime=0)
f=save(T/(stem+'-inspection-20261016.json'),(json.dumps(out,indent=2)+'\n').encode());print(json.dumps(dict(inspection=f,files=len(entries),tracked_bytes=receipt['tracked_bytes'],required=197,closed_omissions=17,unique_blobs=len(cache),mode_counts=dict(mode_counts),pins=len(out['pins']),diffs=3,DLLs=4,unchanged_prep=12),indent=2),flush=True)
