# Read-only evidence and fresh GitHub restore verifier; no build or test execution.
import argparse,gzip,hashlib,json,os,pathlib,subprocess,sys,time
p=argparse.ArgumentParser();p.add_argument('repo');p.add_argument('expected');p.add_argument('receipt');p.add_argument('--evidence-only',action='store_true');a=p.parse_args();root=pathlib.Path(a.repo).resolve();started=time.monotonic()
def git(*args):return subprocess.check_output(['git',*args],cwd=root)
def require(ok,desc):
 if not ok:raise AssertionError(desc)
sha=git('rev-parse','HEAD').decode().strip();require(sha==a.expected,'unexpected HEAD')
receipt={'source':sha,'repo':str(root),'verification':'read-only; no build/test/game/provider/native execution','result':'IN_PROGRESS','trackedBlobs':0,'manifests':0,'artifacts':0,'commitBoundPins':0,'errors':[]}
if not a.evidence_only:
 require(git('status','--porcelain').strip()==b'','restore dirty');require(git('rev-parse','--is-shallow-repository').strip()==b'false','shallow clone')
 alternates=pathlib.Path(git('rev-parse','--git-path','objects/info/alternates').decode().strip());alternates=alternates if alternates.is_absolute() else root/alternates
 require(not alternates.exists(),'local object alternates')
 url=git('remote','get-url','origin').decode().strip();require(url=='https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git','non-GitHub clone origin');receipt['origin']=url
 refs=git('ls-remote','origin','refs/heads/1553-storage-migration-cloud-20261008','refs/heads/main').decode();require(sha+'\trefs/heads/1553-storage-migration-cloud-20261008' in refs,'remote tip mismatch');require('d0241e71e349fbe2020e4a41b6be7c627c81cbb4\trefs/heads/main' in refs,'main changed');receipt['remoteRefs']=refs.splitlines()
 receipt['tree']=git('rev-parse','HEAD^{tree}').decode().strip()
 for row in git('ls-tree','-r','-z','HEAD').split(b'\0'):
  if not row:continue
  meta,name=row.split(b'\t',1);mode,kind,oid=meta.split();require(kind==b'blob','gitlink not checked');path=root/os.fsdecode(name)
  b=os.fsencode(os.readlink(path)) if mode==b'120000' else path.read_bytes();actual=hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest();require(actual==oid.decode(),'tracked blob '+os.fsdecode(name));receipt['trackedBlobs']+=1
 fsck=subprocess.run(['git','fsck','--full','--strict'],cwd=root,capture_output=True,text=True);receipt['fsckExitCode']=fsck.returncode;receipt['fsckOutput']=fsck.stdout+fsck.stderr;require(fsck.returncode==0,'fsck failed')
cat=subprocess.Popen(['git','cat-file','--batch'],cwd=root,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL);cache={};rev_cache={}
def blob(rev,path):
 key=(rev,path)
 if key not in cache:
  cat.stdin.write((rev+':'+path+'\n').encode());cat.stdin.flush();header=cat.stdout.readline().decode().strip();parts=header.split()
  require(len(parts)==3 and parts[1]=='blob','missing pinned blob '+rev+':'+path);n=int(parts[2]);b=cat.stdout.read(n);require(cat.stdout.read(1)==b'\n','batch separator');cache[key]=(hashlib.sha256(b).hexdigest(),parts[0])
 return cache[key][0]
def walk(v,manifest,source=None,group=''):
 if isinstance(v,dict):
  proposed=v.get('source')
  if isinstance(proposed,str):
   try:
    if proposed not in rev_cache:rev_cache[proposed]=git('rev-parse','--verify',proposed+'^{commit}').decode().strip()
    source=rev_cache[proposed]
   except subprocess.CalledProcessError:source=None
  artifact_rows=v.get('artifacts',[])
  if isinstance(artifact_rows,dict):artifact_rows=[{'path':k,'sha256':x} for k,x in artifact_rows.items()]
  for r in artifact_rows:
   try:
    f=manifest.parent/r['path'];b=f.read_bytes();require(hashlib.sha256(b).hexdigest().lower()==r['sha256'].lower(),'stored hash');expanded=gzip.decompress(b) if f.suffix=='.gz' else b
    if r.get('originalSha256'):require(hashlib.sha256(expanded).hexdigest().lower()==r['originalSha256'].lower(),'expanded hash')
    receipt['artifacts']+=1
   except Exception as e:receipt['errors'].append({'manifest':str(manifest.relative_to(root)),'group':group,'artifact':r.get('path'),'error':str(e)})
  for r in v.get('sourcePins',[]):
   try:
    rev=r.get('source',r.get('revision',source));require(bool(rev),'source pin has no commit binding');require(blob(rev,r['path']).lower()==r['sha256'].lower(),'source hash mismatch '+rev+':'+r['path']);receipt['commitBoundPins']+=1
   except Exception as e:receipt['errors'].append({'manifest':str(manifest.relative_to(root)),'group':group,'sourcePin':r.get('path'),'source':source,'error':str(e)})
  for k,x in v.items():
   if k not in ('artifacts','sourcePins'):walk(x,manifest,source,group+'/'+k)
 elif isinstance(v,list):
  for i,x in enumerate(v):walk(x,manifest,source,group+'/'+str(i))
for m in sorted((root/'specs/1553-portable-local-storage/recovery').glob('storage-migration-*/manifest.json')):
 receipt['manifests']+=1;doc=json.loads(m.read_text());walk(doc,m)
 if doc.get('sourceRevision') and (m.parent/'corpus.json').exists():
  for r in json.loads((m.parent/'corpus.json').read_text()):
   rev=doc['sourceRevision'];require(blob(rev,r['path'])==r['sha256'],'historical corpus hash '+r['path']);require(cache[(rev,r['path'])][1]==r['gitBlob'],'historical corpus git object '+r['path']);receipt['commitBoundPins']+=1
cat.stdin.close();cat.wait();receipt['uniquePinnedBlobs']=len(cache)
cpath=root/'specs/1553-portable-local-storage/recovery/storage-migration-final-owner-census-20261009/owning-candidates.json';c=json.loads(cpath.read_text());sem=json.loads(cpath.with_name('semantic-classification.json').read_text());require(c['InvocationCount']==170 and len(sem['records'])==170,'170 records missing');require(len({(r['Path'],r['MethodSpanStart']) for r in c['Invocations']})==164,'164 full declarations missing')
require(sem['source']==c['Source'],'semantic census source differs')
syntax_fields=['Path','Line','Method','InvocationSpanStart','InvocationSpanLength','MethodSpanLength','MethodBodySha256','MethodSpanStart','MethodStartLine','MethodEndLine','MethodSignature','Classes','Invocation','NestedLambda','SourceSha256']
for index,(syntax,record) in enumerate(zip(c['Invocations'],sem['records']),1):
 require(record['acquisition']==index,'semantic acquisition order')
 for key in syntax_fields:require(record[key]==syntax[key],'semantic/source mismatch '+str(index)+' '+key)
 require(record['sourceLink']=='https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/'+c['Source']+'/'+record['Path']+'#L'+str(record['Line']),'source link mismatch')
 require(bool(record.get('role')) and bool(record.get('reason')) and bool(record.get('scopeLimit')),'semantic rationale missing')
counts={}
for record in sem['records']:counts[record['qualification']]=counts.get(record['qualification'],0)+1
require(counts==sem['counts'],'semantic classification totals differ')
for x in c['SourceBlobs']:require(blob(c['Source'],x['Path'])==x['Sha256'],'current census source hash '+x['Path'])
receipt['censusSourceFiles']=len(c['SourceBlobs']);receipt['censusAcquisitions']=170;receipt['censusDeclarations']=164;receipt['wallSeconds']=round(time.monotonic()-started,3);receipt['result']='PASS' if not receipt['errors'] else 'FAIL';pathlib.Path(a.receipt).write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:v for k,v in receipt.items() if k not in ['fsckOutput','remoteRefs','errors']},ensure_ascii=False));print('errors',len(receipt['errors']));sys.exit(0 if not receipt['errors'] else 1)
