#!/usr/bin/env python3
"""Read only immutable G01 tools originals; never imports/runs project code or reads active prepare/procfs."""
from pathlib import Path
import hashlib,json,subprocess,datetime
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore')
O=Path('/tmp/1536-cloud-tools/review');L=Path('/tmp/g-a91f6d2c7b30/tools')
V='d293b3bf796289fb6662a778ca408fae76ab2736';S='86bb144d834e29e6e300c24609bc9743561f0e07';A='e520663f7918e536c2ae0675cb79a774be637655'
B='specs/1536-complete-wound-materialization/recovery';T=B+'/l01-game-capture-g01';E=B+'/l01-game-capture-g01-tools-first-20261019'
checks=[]
def h(b):return hashlib.sha256(b).hexdigest()
def j(p):return json.loads(Path(p).read_text())
def ck(n,b,detail=None):
 checks.append(dict(check=n,passed=bool(b),detail=detail));assert b,n

def git(*args):return subprocess.check_output(['git','-C',str(R),*args])
def blobs(rev,paths):
 q=''.join(rev+':'+p+'\n' for p in paths).encode();p=subprocess.run(['git','-C',str(R),'cat-file','--batch'],input=q,stdout=subprocess.PIPE,check=True)
 data=p.stdout;pos=0;out={}
 for name in paths:
  end=data.index(b'\n',pos);header=data[pos:end].split();assert header[1]==b'blob';size=int(header[2]);pos=end+1;out[name]=data[pos:pos+size];pos+=size+1
 assert pos==len(data);return out
m=j(R/E/'manifest.json');r=j(R/E/'record.json');packet=j(R/T/'source-packet.json');bindings=j(R/T/'bindings.json');control=j(R/T/'control.json')
receipt_path=Path('/tmp/1536-cloud-tools/g01-recovery-d293b3bf.json');receipt=j(receipt_path)
paths=[E+'/'+x['path'] for x in m['files']]+[E+'/manifest.json'];vb=blobs(V,paths)
rows=[]
for x in m['files']:
 p=E+'/'+x['path'];bs=(R/p).read_bytes();ok=len(bs)==x['bytes'] and h(bs)==x['sha256'] and bs==(C/p).read_bytes()==vb[p]==(L/x['path']).read_bytes()
 rows.append(dict(path=x['path'],sha256=h(bs),bytes=len(bs),allFourCopiesExact=ok))
ck('45 original files local tools / writer / Git carrier / independent restore exact',len(rows)==45 and all(x['allFourCopiesExact'] for x in rows))
ck('manifest exact writer Git restore', (R/E/'manifest.json').read_bytes()==vb[E+'/manifest.json']==(C/E/'manifest.json').read_bytes())
ck('record and source bound',m['source']==r['source']==S and m['recordSha256']==h((R/E/'record.json').read_bytes())=='410857f197e1da419f110f9f52eacbe751543c590994e81aa4498ea327e81d12')
ck('no binary publication overclaim',m['privateCompiledBinariesNotPublished'] is True)
ck('receipt exact carrier/source pins and clean independent recovery',receipt['sha']==V and receipt['remoteShaVerified'] and receipt['clean'] and receipt['localWriterObjectsCopied'] is False and receipt['changedPaths']==274 and receipt['currentPinsVerified']==163 and receipt['sourcePinDigest']==packet['sourcePinDigest'])
ck('recovery historical and privacy limits',receipt['historicalLedgersUnchanged'] and receipt['productionChanges'] is False and receipt['privateDumpOrRawReportPublished'] is False)
pinpaths=[x['path'] for x in packet['sourcePins']];ab=blobs(A,pinpaths);sb=blobs(S,pinpaths);cb=blobs(V,pinpaths)
pinok=[]
for x in packet['sourcePins']:
 p=x['path'];bs=(R/p).read_bytes();pinok.append(len(bs)==x['bytes'] and h(bs)==x['sha256'] and bs==ab[p]==sb[p]==cb[p]==(C/p).read_bytes())
ck('163 source pins accepted source / tools source / carrier / current / restore exact',len(pinok)==163 and all(pinok))
ck('source digest and controller accepted',r['sourcePinDigest']==packet['sourcePinDigest']=='a6c2675674dd6e3218e16917fd9abfcffb44d638195c367c3efcb2d98780b026' and h((R/T/'phase.py').read_bytes())=='63969901c241f7007030af78b757bba0618c72dae457ae75c11804de45bc9980' and h((R/T/'bindings.json').read_bytes())=='50e5bffdc10799c05b8757b8606642c0a5e6329fc4a0b91a764851eca4a679b2')
source_control=json.loads(git('show',S+':'+T+'/control.json'));gate=source_control['sourceAdmission'];review=(R/gate['reviewPath']).read_bytes();sr=json.loads(review)
ck('tools launch had accepted saved SOURCE gate',gate['status']=='SOURCE_ACCEPTED_G01_ZERO_ONLY' and h(review)==gate['reviewSha256'] and sr['verdict']=='ACCEPT_G01_WHOLE_SOURCE_ZERO_ONLY' and sr['sourcePinDigest']==r['sourcePinDigest'])
ck('current actual remains HOLD_PREP',control['actualAdmission']['status']=='HOLD_PREP' and control['gameplayRelease']==control['snapshotRelease']==control['crash5Release']=='NONE')
ck('tools native0 zero game/request/dump',r['nativeExit']==0 and not r['observerErrors'] and r['status']=='PREPARED_G01_ZERO_ONLY' and r['gameCasesPlanned']==r['diagnosticRequestUpperBound']==m['gameCases']==m['apiRequests']==m['dumpArtifacts']==0)
names=['compile-ParentProbe','compile-MapFrames','parent-proof','collector-health-proof','map-valid','map-wrong-mvid','map-zero-token','map-body-end','map-outside-body']
ck('nine original commands all native0 joined no residual session/errors',len(r['commands'])==9 and all(x['nativeExit']==x['rawWrapperReturnCode']==0 and x['joined'] and not x['observerErrors'] and not x['remainingSessionProcesses'] and x['terminationSignal'] is None for x in r['commands']))
ck('nine original command records match aggregate',all(j(R/E/(name+'.record.json'))==row for name,row in zip(names,r['commands'])))
ck('owned command identities distinct positive PID and kernel birth',len({(x['identity']['pid'],x['identity']['birthTick']) for x in r['commands']})==9 and all(x['identity']['pid']>0 and x['identity']['birthTick']>0 and x['identity']['uid']==1000 for x in r['commands']))
ck('original command timeouts within 60 compile / 30 host ceilings',all(0<float(x['command'][4][:-1])<=(60 if i<2 else 30) for i,x in enumerate(r['commands'])))
ck('compiler banners only; no warnings or errors',all('error ' not in (R/E/(n+'.stdout')).read_text().lower() and 'warning ' not in (R/E/(n+'.stdout')).read_text().lower() and not (R/E/(n+'.stderr')).read_bytes() for n in names[:2]))
ck('all command stderr empty',all(not (R/E/(n+'.stderr')).read_bytes() for n in names))
outs=[]
for x in r['toolOutputs']:
 bs=Path(x['path']).read_bytes();outs.append(dict(**x,currentBytesExact=len(bs)==x['bytes'] and h(bs)==x['sha256']))
ck('eight completed local tool outputs exact bound bytes',len(outs)==8 and all(x['currentBytesExact'] for x in outs))
inv=j(R/B/'l01-managed-stack-capability-inventory-20261018.json');refs=inv['net8ReferenceInputs']
ck('163 net8 references match installed source-pinned inventory',len(refs)==163 and all(len(Path(x['path']).read_bytes())==x['bytes'] and h(Path(x['path']).read_bytes())==x['sha256'] for x in refs))
for n in ['ParentProbe','MapFrames']:
 lines=(L/(n+'.rsp')).read_text().splitlines();refargs=[x[len('-reference:'):] for x in lines if x.startswith('-reference:')]
 expected_sources=[str(L/'GlobalUsings.cs'),str(R/'BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs'),str(R/'BookOfEternityClient.IntegrationTests/JointInterfaceManagedCapturePolicy.cs'),str(R/'BookOfEternityClient.IntegrationTests/JointInterfaceManagedCapture.cs'),str(R/T/'ParentProbe.cs')] if n=='ParentProbe' else [str(R/T/'MapFrames.cs')]
 ck(n+' exact compile sources and all reference paths',refargs==[x['path'] for x in refs] and [x for x in lines if not x.startswith('-')]==expected_sources and lines[:5]==['-nostdlib+','-target:exe','-debug:portable','-optimize-','-out:'+str(L/(n+'.dll'))])
 ck(n+' exact net8 runtime Disable binding',j(L/(n+'.runtimeconfig.json'))=={'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.32'},'rollForward':'Disable'}})
parent=j(R/E/'parent-proof.stdout');health=j(R/E/'collector-health-proof.stdout')
ck('Parent6 original complete, source-bound, no fixture children/request',parent==r['parentProof'] and len(parent['cases'])==parent['passed']==6 and all(x['passed'] for x in parent['cases']) and parent['gameProcesses']==parent['fixtureChildren']==parent['requests']==0)
ck('PS26 original complete no game/request',health==r['collectorHealthProof'] and len(health['Cases'])==health['Passed']==26 and all(x['Passed'] for x in health['Cases']) and health['GameProcesses']==health['Requests']==0)
ck('new cancellation marker refusal triad reached', [x['Name'] for x in health['Cases']][-3:]==['only missing marker means absent','private existing marker means cancelled','private marker I/O uncertainty refuses'])
ck('both observed BCL clock stamps inside Python brackets same frequency',len(r['clockProof'])==2 and parent['clockFrequency']==health['ClockFrequency']==1000000000 and all(x['beforeMonotonicNs']<=x['stamp']*10**9//x['frequency']<=x['afterMonotonicNs'] and x['insideObservedBracket'] for x in r['clockProof']) and r['clockProof'][0]['stamp']==parent['clockStamp'] and r['clockProof'][1]['stamp']==health['ClockStamp'])
maprows=[]
for case in ['valid','wrong-mvid','zero-token','body-end','outside-body']:
 x=j(R/E/('map-'+case+'.json'));inp=j(R/E/('map-'+case+'.input.json'))['payload']['threads'][0];fr=inp['stack_frames'][0]
 ck(case+' input provenance and offline output',inp['native_thread_id']=='0xb' and fr['filename']=='ParentProbe.dll' and fr['is_managed'] and x['offlineOnly'] and x['requests']==0 and x['capturePhase']==x['suspensionDuration']=='UNKNOWN')
 if case=='valid':
  f=x['mappedFrames'][0];ck('valid actual module/method/IL/source anchor',len(x['mappedFrames'])==1 and not x['limitations'] and f['nativeTid']==11 and f['moduleMvid']==fr['guid']==parent['moduleMvid'] and int(f['methodDef'],16)==int(fr['token'],16)==parent['methodToken'] and f['ilOffset']=='0x0' and f['method']=='SourceAnchor' and f['sourceFile']==T+'/ParentProbe.cs' and f['sourceLine']==9)
 else:
  expected={'wrong-mvid':'FRAME_MODULE_MVID_MISMATCH','zero-token':'UNMAPPABLE_METHODDEF_OR_IL','body-end':'IL_OFFSET_OUTSIDE_METHOD_BODY','outside-body':'IL_OFFSET_OUTSIDE_METHOD_BODY'}[case]
  ck(case+' refusal not mapped',not x['mappedFrames'] and expected in x['limitations'])
 if case=='body-end':ck('body-end uses actual compiled method length',int(fr['il_offset'],16)==parent['methodIlBytes']==4)
 if case=='outside-body':ck('outside-body actual large offset',int(fr['il_offset'],16)==0x7fffffff)
 maprows.append(dict(case=case,mapped=len(x['mappedFrames']),limitations=x['limitations']))
ck('five original map results all reached',len(r['mappingProof'])==5 and all(x['passed'] for x in r['mappingProof']))
modules=j(R/E/'map-proof-modules.json')['MapModules'];ck('mapper DLL/PDB binding exact compiled outputs',len(modules)==1 and modules[0]['DllSha256']==outs[0]['sha256'] and modules[0]['PdbSha256']==outs[1]['sha256'])
ck('dated visibility no active tool; normal899 original before/after stable',r['normal899UnchangedBefore'] and r['normal899UnchangedAfter'] and r['afterLiveTools']==[] and r['beforeTools']==r['afterTools']==[{'comm':'dotnet','state':'Z','count':35}])
# No current process inspection; this reads only immutable git trees.
def tree(rev):
 lines=git('ls-tree','-r','--full-tree',rev).splitlines();lines=[x for x in lines if not x.split(b'\t',1)[1].startswith((b'specs/1536-complete-wound-materialization/',b'BookOfEternityClient.IntegrationTests/')) and x.split(b'\t',1)[1] not in (b'tests/categories.json',b'tests/selection.json')];return dict(count=len(lines),sha256=h(b'\n'.join(lines)+b'\n'))
ck('retained unit source tree equivalence exact scoped exclusion',tree(S)==tree(V)==tree(bindings['unit']['compiledSource'])==bindings['unchangedUnitTree'])
out=dict(kind='G01_PARTIAL_TOOLS_PREP_READONLY_CHECKS',reviewedCarrier=V,toolsSource=S,checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),checks=checks,checkCount=len(checks),findings=[],originalChains=rows,localToolOutputs=outs,mappingResults=maprows,receiptSha256=h(receipt_path.read_bytes()),manifestSha256=h((R/E/'manifest.json').read_bytes()),limits=['TOOLS_ONLY: no final PREP/actual admission','No active prepare/procfs or SDK/native execution by reviewer','Tool binaries local/hash-bound; no binary Git recovery claim','Clock brackets and process visibility dated; no continuous guarantee'])
(O/'l01-game-capture-g01-tools-partial-prep-checks-20261019.json').write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps(dict(checks=len(checks),findings=[],recordSha256=h((R/E/'record.json').read_bytes()))))
