from pathlib import Path
import subprocess,json,hashlib,zipfile,io,ast,re
R=Path('/workspace/The-Book-of-Eternity-Reborn'); CL=Path('/workspace/1536-cloud-clean-restore'); OUT=Path('/tmp/1536-cloud-tools/review')
P='specs/1536-complete-wound-materialization/recovery/'; OLD='0db1f4186fd190c4940b018ec186cc9667727cfd'; NEW='4810d2ad2ac5121aacabb21bf49cfb0b56a0da09'; BASE='1817ae5814f06a45cd6df975a9211c380a256df8'
h=lambda b:hashlib.sha256(b).hexdigest()
def git(*args,repo=R):return subprocess.check_output(['git','-C',str(repo),*args])
def b(commit,path,repo=R):return git('show',commit+':'+path,repo=repo)
def j(commit,path):return json.loads(b(commit,path))
checks=[]
def check(name,value,detail=None):
 row={'check':name,'passed':bool(value)}
 if detail is not None:row['detail']=detail
 checks.append(row)
 if not value:raise AssertionError(name)
def ref(path):
 p=Path(path);d=p.read_bytes();return {'path':str(p),'bytes':len(d),'sha256':h(d)}
for C,N in [(OLD,146),(NEW,149)]:
 packet=j(C,P+'l01-delivery-d01-source-packet-20261018.json');pins=packet['sourcePins']
 check(C[:8]+' pin count/digest',len(pins)==N and h(json.dumps(pins,sort_keys=True,separators=(',',':')).encode())==packet['sourcePinDigest'])
 for row in pins:
  data=b(C,row['path']);check(C[:8]+' pin '+row['path'],len(data)==row['bytes'] and h(data)==row['sha256'])
 receiptpath=Path('/tmp/1536-cloud-tools/d01-carrier-recovery-'+C[:8]+'.json');receipt=json.loads(receiptpath.read_text())
 check(C[:8]+' receipt scope',receipt['sha']==C and receipt['remoteShaVerified'] and receipt['clean'] and receipt['localWriterObjectsCopied']==False and receipt['currentPinsVerified']==N and receipt['sourcePinDigest']==packet['sourcePinDigest'] and not receipt['productionChanges'] and receipt['historicalLedgersUnchanged'] and receipt['S01PinsVerified']==11)
 for row in receipt['files']:
  data=b(C,row['path'],CL);mode,kind,blob=git('ls-tree',C,'--',row['path'],repo=CL).decode().split('\t')[0].split()
  check(C[:8]+' recovered '+row['path'],len(data)==row['bytes'] and h(data)==row['sha256'] and mode==row['mode'] and blob==row['gitBlob'])
 check(C[:8]+' historical provenance exceptions',[(x['originalPins'],x['unchangedPins'],len(x['explicitCurrentDeltas'])) for x in packet['historicalPinSets']]==[(101,98,3),(93,91,2)])
 for group in packet['historicalPinSets']:
  oldpins=j(C,group['path'])['sourcePins'];delta=[x['path'] for x in oldpins if h(b(C,x['path']))!=x['sha256']]
  check(C[:8]+' historical actual deltas '+group['path'],delta==[x['path'] for x in group['explicitCurrentDeltas']])
 check(C[:8]+' closed admission',j(C,P+'l01-delivery-d01-control-20261018.json')['source']['status']=='SOURCE_REVIEW_PENDING' and packet['gameplayRelease']=='NONE' and packet['nativeFixtureRelease']=='NONE')
# Exact original evidence; bytes remain identical in corrected candidate.
archives=[]
for family in ['baseline','nonlive']:
 path=P+'l01-delivery-d01-'+family+'-evidence-20261018.json';index=j(OLD,path);a=index['archive'];raw=b(OLD,a['path']);z=zipfile.ZipFile(io.BytesIO(raw))
 check(family+' archive hash',h(raw)==a['sha256'] and len(raw)==a['bytes'])
 check(family+' exact entry set',sorted(z.namelist())==sorted(x['entry'] for x in a['files']) and len(z.namelist())==12)
 for row in a['files']:
  data=z.read(row['entry']);check(family+' raw '+row['entry'],len(data)==row['bytes'] and h(data)==row['sha256'])
 check(family+' immutable index/archive',b(OLD,path)==b(NEW,path) and raw==b(NEW,a['path']))
 check(family+' compiler original',index['compiler']['nativeExit']==0 and index['compiler']['joined'] and not index['compiler']['observerErrors'])
 actor=index['witness'] if family=='baseline' else index['host'];expected=7 if family=='baseline' else 0
 check(family+' original host outcome',actor['nativeExit']==expected and actor['joined'] and not actor['observerErrors'])
 if family=='baseline':
  payload=json.loads(z.read('baseline-witness/host.stdout'));check('baseline raw payload exact',payload==index['baseline'])
  check('baseline expected bounded gap',payload['ExpectedMissingLiveDelivery'] and payload['PendingBeforeEof'] and payload['RawDecodedTextExact'] and payload['EofReleasedAndJoined'] and payload['Failure'] is None and payload['FixtureSubprocessTargets']==0)
 else:
  payload=json.loads(z.read('checks01-pure/host.stdout'));check('nonlive raw twenty PASS',len(index['checks'])==20 and all(x['Passed'] for x in index['checks']) and payload['Checks']==index['checks'])
  helper='BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs';check('helper unchanged from original20',h(b(OLD,helper))==index['helperSource']['sha256'] and b(OLD,helper)==b(index['source'],helper)==b(NEW,helper))
 archives.append({'family':family,'indexPath':path,'indexSha256':h(b(OLD,path)),**a})
# Bounded source preservation and original bookkeeping.
crash='BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrash.cs';old=b(BASE,crash).decode();cur=b(OLD,crash).decode()
check('all original Assert statement lines same order',re.findall(r'^.*\bAssert\..*$',old,re.M)==re.findall(r'^.*\bAssert\..*$',cur,re.M))
check('original budget literals retained',old.count('TimeSpan.FromSeconds(220)')==cur.count('TimeSpan.FromSeconds(220)')==1)
check('child diagnostic emitter unchanged',b(BASE,'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrashDiagnostic.cs')==b(NEW,'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrashDiagnostic.cs'))
changed=git('diff','--name-only',BASE,NEW).decode().splitlines();check('no production delta after accepted S01',not any(x.startswith(('BookOfEternityClient/','BookOfEternity.Core/')) for x in changed))
g=j(OLD,P+'l01-delivery-d01-generator-initial-path-error-20261018.json');check('generator initial exact source preserved',h(g['sourceSnapshot'].encode())==g['sourceSha256']);check('generator path correction source only','parents[3]' in g['sourceSnapshot'] and 'parents[4]' in b(OLD,P+'l01-delivery-d01/make_packet.py').decode() and g['fixtureTargetsStarted']==g['gameProcesses']==0)
check('original phase snapshot exact',Path(OUT/'d01-phase-original-source-0db1f418.py').read_bytes()==b(OLD,P+'l01-delivery-d01/phase.py'))
# Focused correction only.
expected={P+'l01-delivery-d01-control-20261018.json',P+'l01-delivery-d01-source-packet-20261018.json',P+'l01-delivery-d01/controller_fault_checks.py',P+'l01-delivery-d01/controller-fault-checks.json',P+'l01-delivery-d01/make_packet.py',P+'l01-delivery-d01/phase.py'}
check('corrected candidate exact six-path delta',set(git('diff','--name-only',OLD,NEW).decode().splitlines())==expected)
phase=b(NEW,P+'l01-delivery-d01/phase.py');tree=ast.parse(phase);main=next(x for x in tree.body if isinstance(x,ast.FunctionDef) and x.name=='main');guard=next(x for x in main.body if isinstance(x,ast.Try));check('installed-input guard precedes every phase branch',isinstance(guard.body[1],ast.Assign) and guard.body[1].value.func.id=='verify_installed_inputs' and isinstance(guard.body[2],ast.If));check('main no buffered print',not any(isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=='print' for n in ast.walk(main)))
proof=j(NEW,P+'l01-delivery-d01/controller-fault-checks.json');check('nine exact-source fake proofs',proof['phaseSha256']==h(phase) and proof['passed']==9 and proof['allPassed'] and all(x['passed'] for x in proof['checks']) and proof['fixtureSubprocessTargets']==0)
invpath=P+'l01-managed-stack-capability-inventory-20261018.json';inv=j(NEW,invpath);check('inventory newly pinned and unchanged original',invpath in [x['path'] for x in j(NEW,P+'l01-delivery-d01-source-packet-20261018.json')['sourcePins']] and invpath not in [x['path'] for x in j(OLD,P+'l01-delivery-d01-source-packet-20261018.json')['sourcePins']] and b(OLD,invpath)==b(NEW,invpath))
rows=inv['availableFiles']+inv['net8ReferenceInputs'];check('installed input closure9+163',len(inv['availableFiles'])==9 and len(inv['net8ReferenceInputs'])==163)
for row in rows:
 data=Path(row['path']).read_bytes();check('installed bytes '+row['path'],len(data)==row['bytes'] and h(data)==row['sha256'])
check('writer and independent restore exact corrected clean',git('rev-parse','HEAD').decode().strip()==NEW and git('rev-parse','HEAD',repo=CL).decode().strip()==NEW and not git('status','--porcelain') and not git('status','--porcelain',repo=CL))
result={'kind':'D01_INDEPENDENT_SOURCE_STATIC_AND_ORIGINAL_ARCHIVE_CHECKS','originalCandidate':OLD,'correctedCandidate':NEW,'scope':'Read-only Git/ZIP/hash/AST and installed-file reads. No compiler, .NET, helper, fixture, process-control, diagnostics or game execution. Prior fake emitter proof separate.','checks':checks,'passed':len(checks),'findings':[],'archives':archives,'utilityHistory':[{'operation':'initial read of proposed fault_checks.py filename','result':'FileNotFound; actual file controller_fault_checks.py read afterward','nativeGameOrSourceEffect':False}],'originalFindingIds':['D01-S01','D01-S02'],'findingInterpretation':'Both original findings require correction; passing integrity checks are not original candidate admission. Corrected candidate acceptance is in separately written review.'}
p=OUT/'l01-live-delivery-source-focused-checks-0db1f418-20261018.json';p.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({'path':str(p),'passed':len(checks),'sha256':h(p.read_bytes())}))
