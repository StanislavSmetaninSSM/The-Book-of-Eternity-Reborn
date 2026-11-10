#!/usr/bin/env python3
"""Read-only SOURCE carrier checks for diagnostic2. Never imports/runs sampler, wrapper or fake-proc tests.
Requires a saved/recovered exact carrier. Static checks are not a semantic SOURCE verdict.
"""
from pathlib import Path
import argparse,ast,datetime,hashlib,json,os,re,subprocess
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review')
REC='specs/1536-complete-wound-materialization/recovery/'
BASELINE='9793e320bef881462ef7dbac847f748e645ab324';COMPILED_BASE='8f8f534304e3672df5d42c9ab31e7055235bfaca'
DIGEST='2c144b7734708f8c02a3924d4d12a885384f05a1437021afd2845827c761c616'
NS='/tmp/ni-iobs-l01-crash-diagnostic2-20261018-de0f3e200f884d3a8f3cecbc9c9f4892'
def h(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def git(root,*args):return subprocess.check_output(['git',*args],cwd=root,env={**os.environ,'GIT_OPTIONAL_LOCKS':'0'})
def read(p):return json.loads(Path(p).read_bytes())
checks=[]
def ck(ok,label):
 checks.append(label)
 if not ok:raise AssertionError(label)
def ref(p):
 p=Path(p);b=p.read_bytes();return dict(path=str(p),bytes=len(b),sha256=h(b))
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('carrier');p.add_argument('--base',default=BASELINE);p.add_argument('--sampler',required=True);p.add_argument('--wrapper',required=True);p.add_argument('--environment',required=True);p.add_argument('--fake-proof',required=True);p.add_argument('--discriminators',required=True);p.add_argument('--recovery',action='append',required=True)
 a=p.parse_args();ck(all(re.fullmatch('[0-9a-f]{40}',v)for v in [a.carrier,a.base]),'SHA format')
 for root in (R,C):ck(git(root,'rev-parse','HEAD').decode().strip()==a.carrier and not git(root,'status','--porcelain'),'clean exact carrier '+str(root))
 ck(git(C,'ls-remote','origin','refs/heads/1536-wound-b2-cloud-20261012').decode().split()[0]==a.carrier,'exact remote SHA');ck(not(C/'.git/objects/info/alternates').exists(),'no clone object alternates');ck('github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn'in git(C,'remote','get-url','origin').decode(),'independent GitHub clone origin');ck(git(R,'ls-tree','-rz',a.carrier)==git(C,'ls-tree','-rz',a.carrier),'full tree equality')
 ck(not git(R,'diff','--name-only',BASELINE,a.carrier,'--','.',':!specs/1536-complete-wound-materialization'),'no product/fixture/controller/catalog/selection edits outside recovery metadata')
 packet=read(R/(REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'));cloud=read(R/(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));oldpacket=json.loads(git(R,'show',BASELINE+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'));oldcloud=json.loads(git(R,'show',BASELINE+':'+REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
 ck(packet['sourcePins']==oldpacket['sourcePins']and len(packet['sourcePins'])==101 and packet['pinDigest']==h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==DIGEST,'unchanged101 pins/digest');ck(len(packet['productDelta'])==23 and packet['productDelta']==oldpacket['productDelta']==git(R,'diff','--name-only',packet['productBase'],a.carrier,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines(),'product23 exact')
 tree={}
 for row in git(R,'ls-tree','-rz',a.carrier).split(b'\0'):
  if row:meta,path=row.split(b'\t',1);tree[path.decode()]=tuple(meta.decode().split())
 for x in packet['sourcePins']:
  b=(R/x['path']).read_bytes();ck(b==(C/x['path']).read_bytes()==git(R,'show',COMPILED_BASE+':'+x['path'])and(len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob'])and tree[x['path']]==(x['mode'],'blob',x['gitBlob']),'pin '+x['path'])
 ck(cloud['qualifiedCases']==oldcloud['qualifiedCases']and cloud['qualifiedCases']['totalDistinctTargets']==52,'qualified52 provenance unchanged');ck(cloud['oldCorrectedCrash']==oldcloud['oldCorrectedCrash']and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN'and not cloud['oldCorrectedCrash']['recoveryClaim'],'old66529 remains UNKNOWN')
 for key,old in oldcloud['actuals'].items():
  new=cloud['actuals'][key]
  for field in ['recordPath','recordSha256','archive','actualSource','compiledSource','nativeExit']:
   if field in old:ck(new[field]==old[field],'old original field '+key+'.'+field)
  if 'archive'in old:ck(h((R/old['archive']['path']).read_bytes())==old['archive']['sha256']and not git(R,'diff','--name-only',BASELINE,a.carrier,'--',str(Path(old['archive']['path']).parent)),'immutable old archive '+key)
 ck(packet['namedActualRelease']['mode']is None,'no actual release before SOURCE/PREP')
 refs={}
 def collect(value):
  if isinstance(value,dict):
   if all(k in value for k in ['path','sha256','bytes'])and str(value['path']).startswith(REC):refs.setdefault(value['path'],[]).append(value)
   for child in value.values():collect(child)
  elif isinstance(value,list):
   for child in value:collect(child)
 collect(packet);collect(cloud)
 def saved(path,require_binding=True):
  ck(str(path).startswith(REC),'versioned evidence path '+str(path));b=(R/path).read_bytes();ck(b==(C/path).read_bytes()==git(R,'show',a.carrier+':'+path),'saved bytes '+path)
  if require_binding:ck(any(x['bytes']==len(b)and x['sha256']==h(b)for x in refs.get(path,[])),'explicit packet/cloud executable/evidence hash '+path)
  return b
 sampler=saved(a.sampler);wrapper=saved(a.wrapper);env=saved(a.environment);proof_bytes=saved(a.fake_proof);disc=saved(a.discriminators)
 static={};deny={'kill','killpg','terminate','send_signal','ptrace','sched_setaffinity','setpriority','setrlimit','prlimit','system','execv','execve','execvp'}
 for name,b in [('sampler',sampler),('wrapper',wrapper)]:
  node=ast.parse(b);compile(node,name,'exec');imports=[];calls=[]
  for n in ast.walk(node):
   if isinstance(n,(ast.Import,ast.ImportFrom)):imports.extend([n.module]if isinstance(n,ast.ImportFrom)else[x.name for x in n.names])
   if isinstance(n,ast.Call):
    call=n.func.attr if isinstance(n.func,ast.Attribute)else n.func.id if isinstance(n.func,ast.Name)else '<dynamic>';calls.append(call);ck(call not in deny,'no process intervention call '+name+':'+call)
  if name=='sampler':
   ck(not any(x in {'ctypes','signal'}for x in imports),'sampler no signal/ctypes imports')
   subprocess_names=[n for n in ast.walk(node)if isinstance(n,ast.Name)and n.id=='subprocess']
   timeout_attrs=[n for n in ast.walk(node)if isinstance(n,ast.Attribute)and isinstance(n.value,ast.Name)and n.value.id=='subprocess'and n.attr=='TimeoutExpired']
   ck(len(subprocess_names)==len(timeout_attrs)==1 and not any(isinstance(n,ast.ImportFrom)and n.module=='subprocess'for n in ast.walk(node)),'subprocess used only as TimeoutExpired exception type, never to execute')
  static[name]={'sha256':h(b),'bytes':len(b),'imports':imports,'calledNames':sorted(set(calls)),'parsedWithoutExecution':True}
 exports={line[7:].split('=',1)[0]:line[7:].split('=',1)[1]for line in env.decode().splitlines()if line.startswith('export ')}
 ck('HOME'not in exports and exports['TMPDIR']==NS and exports['XDG_RUNTIME_DIR']==NS+'/runtime','new namespace/HOME unchanged')
 for key in ['DOTNET_CLI_HOME','XDG_CACHE_HOME','XDG_CONFIG_HOME','XDG_DATA_HOME']:ck(exports[key].startswith(NS+'/tooling/'),'fresh mutable tooling '+key)
 ck(exports['DOTNET_ROOT']=='/tmp/1536-cloud-tools/dotnet'and exports['NUGET_PACKAGES']=='/tmp/1536-cloud-tools/nuget','same declared immutable toolchain inputs')
 proof=json.loads(proof_bytes);ck(h(sampler)in json.dumps(proof),'fake-proc proof explicitly binds exact sampler SHA');criteria=read(D/'t179pd-ni-iobs-l01-diagnostic2-source-review-criteria-20261018.json')
 recovery=[];previous=a.base;covered=set()
 for rp in a.recovery:
  rc=read(rp);ck(rc['base']==previous and rc['sourcePinsVerified']==101 and rc['sourcePinDigest']==DIGEST and rc['remoteShaVerified']and rc['clean']and not rc['localSourceOrObjectReuse'],'continuous exact recovery receipt')
  changed=git(C,'diff','--name-only',rc['base'],rc['sha']).decode().splitlines();ck(set(changed)=={x['path']for x in rc['files']}and len(changed)==rc['changedPathsVerified'],'recovery full delta')
  for x in rc['files']:
   b=git(C,'show',rc['sha']+':'+x['path']);ck(b==git(R,'show',rc['sha']+':'+x['path'])and(len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob']),'recovered path '+x['path'])
  covered.update(changed);previous=rc['sha'];recovery.append({'receipt':ref(rp),'sha':rc['sha'],'base':rc['base'],'paths':len(changed)})
 ck(previous==a.carrier and covered==set(git(R,'diff','--name-only',a.base,a.carrier).decode().splitlines()),'full receipt chain to carrier')
 for root in(R,C):ck(git(root,'rev-parse','HEAD').decode().strip()==a.carrier and not git(root,'status','--porcelain'),'stable final clean HEAD')
 out={'kind':'DIAGNOSTIC2_SOURCE_STATIC_BINDING_CHECK_ONLY_NOT_SOURCE_VERDICT','checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'carrier':a.carrier,'base':a.base,'sourcePins':101,'productPaths':23,'pinDigest':DIGEST,'qualified':52,'static':static,'environment':{'path':a.environment,'sha256':h(env),'exports':exports},'fakeProof':{'path':a.fake_proof,'sha256':h(proof_bytes),'topLevelKeys':list(proof),'requiresManualCaseAssertionsAndFailureReview':True},'discriminators':{'path':a.discriminators,'sha256':h(disc),'requiresManualPredeclaredOneRunDecisionReview':True},'criteria':ref(D/'t179pd-ni-iobs-l01-diagnostic2-source-review-criteria-20261018.json'),'recovery':recovery,'checksPassed':len(checks),'findings':[],'manualReviewRequired':criteria['sourceCriteria'],'eligibility':'No SOURCE/PREP/runtime admission from checker. Independent semantic review and saved acceptance required. Never imports or runs reviewed programs.'}
 dest=D/('t179pd-ni-iobs-l01-diagnostic2-source-independent-check-'+a.carrier[:8]+'-20261018.json');ck(not dest.exists(),'preserve prior check artifacts');dest.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps({'check':ref(dest),'checksPassed':len(checks),'findings':[],'verdict':'STATIC_BINDINGS_ONLY_MANUAL_SOURCE_REVIEW_REQUIRED'}))
if __name__=='__main__':main()
