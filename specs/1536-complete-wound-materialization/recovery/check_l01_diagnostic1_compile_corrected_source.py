#!/usr/bin/env python3
"""Affected one-line SOURCE check only; no native work or live fixture/process inspection."""
import pathlib,subprocess,json,hashlib,re,datetime
R=pathlib.Path('/workspace/The-Book-of-Eternity-Reborn');C=pathlib.Path('/workspace/1536-cloud-clean-restore');D=pathlib.Path('/tmp/1536-cloud-tools/review');REL='specs/1536-complete-wound-materialization/recovery/'
BASE='c66d33592622c59261e08c2cb739a61fbf1955a9';SHA='6daaa3da8fb279909ef6318cdfa9892a2f06362c';checks=[]
def h(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def git(*a,root=R):return subprocess.check_output(['git','-C',str(root),*a])
def get(path,rev=SHA,root=R):return git('show',rev+':'+path,root=root)
def check(ok,name):checks.append(name);assert ok,name
def ref(path):
 p=pathlib.Path(path);b=p.read_bytes();return {'path':str(p),'bytes':len(b),'sha256':h(b)}
packetpath=REL+'t179pd-ni-iobs-l01-source-packet-20261018.json';cloudpath=REL+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'
p=json.loads(get(packetpath));prior=json.loads(get(packetpath,BASE));c=json.loads(get(cloudpath));old=json.loads(get(cloudpath,BASE))
check(p['sourcePinCount']==len(p['sourcePins'])==101,'101 pins')
check(h(json.dumps(p['sourcePins'],sort_keys=True,separators=(',',':')).encode())==p['pinDigest']=='2c144b7734708f8c02a3924d4d12a885384f05a1437021afd2845827c761c616','regenerated exact digest')
tree={}
for line in git('ls-tree','-r',SHA).decode().splitlines():
 meta,path=line.split('\t');mode,kind,oid=meta.split();tree[path]=(mode,oid)
for x in p['sourcePins']:
 b=get(x['path']);check(b==get(x['path'],root=C)==(R/x['path']).read_bytes()==(C/x['path']).read_bytes()and(len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob'])and tree[x['path']]==(x['mode'],x['gitBlob']),'pin '+x['path'])
oldpins={x['path']:x for x in prior['sourcePins']};newpins={x['path']:x for x in p['sourcePins']};changed=[x for x in newpins if newpins[x]['sha256']!=oldpins[x]['sha256']]
crash='BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrash.cs'
check(set(oldpins)==set(newpins)and changed==[crash],'only Crash.cs pinned content changes')
before=get(crash,BASE);after=get(crash);addition=b'using Microsoft.Extensions.Logging;\n'
check(after.count(addition)==1 and after.replace(addition,b'',1)==before,'one import inverse exact')
check(b'ILogger<GameEngine> engineLogger ='in before and b'using Microsoft.Extensions.Logging.Abstractions;'in before,'known failed type reference/import scope')
check(not git('diff','--name-only',BASE,SHA,'--','BookOfEternityClient','BookOfEternityGMBridge').strip(),'no production delta')
check(p['productDelta']==prior['productDelta']==git('diff','--name-only',p['productBase'],SHA,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()and len(p['productDelta'])==23,'product23 exact unchanged')
check(re.findall(rb'\bAssert\.\w+\(',before)==re.findall(rb'\bAssert\.\w+\(',after),'all assertions untouched')
check(b'WaitAsync(TimeSpan.FromSeconds(220))'in after,'child220 unchanged')
cat=json.loads(get('tests/categories.json'));check(next(x for x in cat['categories']if x['id']=='fr063-joint-interface-crash-diagnostic')['timeoutMinutes']==15,'category900 unchanged')
for path in ['tests/selection.json','tests/selections/1536-joint-interface-lifecycle-stage.json','tests/selections/1536-joint-interface-crash-diagnostic1.json',p['ledger']['path'],'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrashDiagnostic.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SpiritualContinuation.cs',REL+'t179pd-ni-iobs-l01-process-first-run.py',REL+'t179pd-ni-iobs-l01-process-preparation-run.py',p['diagnosticOrchestration']['path']]:check(get(path)==get(path,BASE),'unchanged dependency '+path)
envref=p['diagnosticOrchestration']['environmentShell'];oldenvref=prior['diagnosticOrchestration']['environmentShell'];env=get(envref['path']);oldenv=get(oldenvref['path'],BASE)
oldguid='f57baaa823e7465cb91cd7fcd9613dea';newguid='b51375bae9144c92842838e1210df2a9'
check(env==oldenv.replace(oldguid.encode(),newguid.encode())and h(env)==envref['sha256']and len(env)==envref['bytes'],'environment exact GUID-only substitution')
check(env==(R/envref['path']).read_bytes()==(C/envref['path']).read_bytes()and not re.search(rb'^\s*export\s+HOME=',env,re.M),'versioned environment exact; HOME untouched')
ns=c['crashDiagnostic1']['runNamespace'];oldns=old['crashDiagnostic1']['runNamespace'];hist=c['crashDiagnostic1']['historicalRunNamespaces'][-1]
check({k:v for k,v in hist.items()if k!='scope'}==oldns,'old f57 namespace preserved exactly with scope label')
check(ns['samePhysicalCloud']and ns['bootId']==oldns['bootId']and ns['tmpdir']==oldns['tmpdir'].replace(oldguid,newguid),'same cloud/boot and new GUID namespace')
exports={line[7:].split('=',1)[0]:line[7:].split('=',1)[1]for line in env.decode().splitlines()if line.startswith('export ')}
check(exports['TMPDIR']==ns['tmpdir']and exports['XDG_RUNTIME_DIR']==ns['tmpdir']+'/runtime'and all(exports[k].startswith(ns['tmpdir']+'/tooling/')for k in ['DOTNET_CLI_HOME','XDG_CACHE_HOME','XDG_CONFIG_HOME','XDG_DATA_HOME']),'effective private mutable paths')
check(ns['frozenRootsOutsideMutableNamespace']==oldns['frozenRootsOutsideMutableNamespace']and all(not pathlib.Path(x).is_relative_to(ns['tmpdir'])for x in ns['frozenRootsOutsideMutableNamespace']),'original frozen roots remain outside new mutable namespace')
check(c['crashDiagnostic1']['preparationAttempts']==old['crashDiagnostic1']['preparationAttempts'],'original failed catalog preserved')
check(c['crashDiagnostic1']['historicalSourceAcceptanceBeforeCompileFix']==old['crashDiagnostic1']['sourceAcceptance'],'prior SOURCE acceptance preserved as historical')
for field in ['actuals','qualifiedCases','oldCorrectedCrash']:check(c[field]==old[field],'immutable '+field)
check(c['qualifiedCases']['totalDistinctTargets']==52 and c['oldCorrectedCrash']['outcome']=='UNKNOWN'and not c['oldCorrectedCrash']['recoveryClaim'],'52 retained and old UNKNOWN')
check(not p['namedZeroRelease']['labels']and p['namedActualRelease']['mode']is None and c['release']['mode']is None,'no premature ZERO or actual release')
for x in c['crashDiagnostic1']['buildCausalReview']['artifacts']:
 b=get(x['path']);check(b==get(x['path'],root=C)and len(b)==x['bytes']and h(b)==x['sha256'],'saved causal artifact '+x['path'])
rp=pathlib.Path('/tmp/1536-cloud-tools/carrier-recovery-6daaa3da.json');receipt=json.loads(rp.read_bytes());check(receipt['sha']==SHA and receipt['base']==BASE and receipt['sourcePinsVerified']==101 and receipt['remoteShaVerified']and receipt['clean']and not receipt['localSourceOrObjectReuse'],'exact remote/full clean GitHub receipt')
delta=git('diff','--name-only',BASE,SHA).decode().splitlines();check(len(delta)==receipt['changedPathsVerified']==10 and set(delta)=={x['path']for x in receipt['files']},'all10 changed paths recovered')
for x in receipt['files']:
 b=get(x['path']);check(b==(C/x['path']).read_bytes()==(R/x['path']).read_bytes()and(len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob'])and tree[x['path']]==(x['mode'],x['gitBlob']),'recovered '+x['path'])
out={'kind':'AFFECTED_ONE_LINE_IMPORT_SOURCE_CHECK','candidate':SHA,'base':BASE,'sourcePins':101,'pinDigest':p['pinDigest'],'productDelta':23,'changedPinnedContents':changed,'exactInverse':True,'assertInvocationsUnchanged':len(re.findall(rb'\bAssert\.\w+\(',after)),'whole':[16,33,57],'diagnostic':[1,1,1],'diagnosticCountsToward57':False,'childSeconds':220,'categorySeconds':900,'newNamespace':ns,'environment':envref,'environmentGUIDOnlyChange':True,'oldAttemptImmutable':True,'qualifiedTargets':52,'recovery':ref(rp),'verifiedRecoveryPaths':10,'remoteScope':'Parent saved exact remote/full independent GitHub receipt; reviewer independently checked restored bytes/modes/Git blobs.','checksPassed':len(checks),'findings':[],'buildExecuted':False,'prepAccepted':False,'runtimeReleased':False,'limits':'SOURCE only; no compile success inferred. Read-only Git/source/artifacts; no native/observer/process or live-root action.'}
path=D/'t179pd-ni-iobs-l01-diagnostic1-compile-corrected-source-independent-check-6daaa3da-20261018.json';path.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(ref(path)))
