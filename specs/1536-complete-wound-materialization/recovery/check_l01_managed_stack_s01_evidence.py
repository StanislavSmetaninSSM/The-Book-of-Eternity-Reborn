#!/usr/bin/env python3
"""Read-only original S01 evidence verification; never loads/runs helper or .NET code."""
from pathlib import Path
import datetime,hashlib,json,stat,struct,subprocess,uuid,zipfile
R=Path('/workspace/The-Book-of-Eternity-Reborn'); C=Path('/workspace/1536-cloud-clean-restore')
P='specs/1536-complete-wound-materialization/'; D=P+'recovery/'
OUT=Path('/tmp/1536-cloud-tools/review/l01-managed-stack-s01-evidence-focused-checks-20261018.json')
SHA='32cfa9ce156a8e5bc5e41c60ad4b1b0e605bfd94'; ACT='4d476b388b3319ed6fb3602f80d6bccb9035e11e'; SRC='d2c716af0946484e1440783d2ad0425b8e469b07'; PREP='0422f6c7148f2f6eda4caa8dbc1043c557ae90a3'
checks=[];facts={}
def h(b):return hashlib.sha256(b).hexdigest()
def ck(name,ok,detail=None):
 row={'check':name,'passed':bool(ok)}
 if detail is not None:row['detail']=detail
 checks.append(row)
 if not ok:raise AssertionError(name)
def git(*args,root=R):return subprocess.check_output(['git','-C',str(root),*args])
def read(p):return json.loads(Path(p).read_text())
def img(p):
 b=Path(p).read_bytes();return {'path':str(p),'bytes':len(b),'sha256':h(b)}
def pinned(b,row):return len(b)==row['bytes'] and h(b)==row['sha256']
def PE(b):
 # Bounded ECMA metadata reader for this exact small PE, no assembly execution.
 u16=lambda o:struct.unpack_from('<H',b,o)[0];u32=lambda o:struct.unpack_from('<I',b,o)[0]
 pe=u32(0x3c); assert b[pe:pe+4]==b'PE\0\0'
 opt=pe+24; sect=opt+u16(pe+20);sections=[]
 for i in range(u16(pe+6)):
  s=sect+i*40;sections.append((u32(s+12),max(u32(s+8),u32(s+16)),u32(s+20)))
 def off(rva):
  for va,sz,raw in sections:
   if va<=rva<va+sz:return raw+rva-va
  raise ValueError('unmapped RVA')
 cli=off(u32(opt+(112 if u16(opt)==0x20b else 96)+14*8));md=off(u32(cli+8))
 assert b[md:md+4]==b'BSJB';p=md+16+u32(md+12);p=(p+3)&~3;n=u16(p+2);p+=4;streams={}
 for _ in range(n):
  start,size=u32(p),u32(p+4);end=b.index(0,p+8);name=b[p+8:end].decode();streams[name]=(md+start,size);p=(end+4)&~3
 t=streams['#~'][0];flags=b[t+6];valid=struct.unpack_from('<Q',b,t+8)[0];p=t+24;rows={}
 for i in range(64):
  if valid>>i&1:rows[i]=u32(p);p+=4
 assert flags&7==0 and all(v<16384 for v in rows.values())
 widths={0:10,1:6,2:14,3:2,4:6,5:2,6:14};offsets={}
 for i in range(7):
  if i in rows:offsets[i]=p;p+=rows[i]*widths[i]
 mod=offsets[0];gid=u16(mod+4);gs=streams['#GUID'][0]
 mvid=str(uuid.UUID(bytes_le=b[gs+(gid-1)*16:gs+gid*16]));ss=streams['#Strings'][0];methods={}
 for rid in range(1,rows[6]+1):
  o=offsets[6]+(rid-1)*14;ni=u16(o+8);ne=b.index(0,ss+ni);name=b[ss+ni:ne].decode();rva=u32(o)
  size=None
  if rva:
   bo=off(rva);size=(b[bo]>>2) if b[bo]&3==2 else u32(bo+4)
  methods[0x06000000+rid]={'name':name,'codeBytes':size,'implFlags':u16(o+4)}
 return mvid,methods
try:
 receipt_path=Path('/tmp/1536-cloud-tools/m01-carrier-recovery-32cfa9ce.json');receipt=read(receipt_path)
 ck('carrier receipt identity and clean remote independent recovery',receipt['sha']==SHA and receipt['clean'] and receipt['remoteShaVerified'] and not receipt['localSourceOrObjectReuse'] and receipt['changedPaths']==61)
 for root in [R,C]:
  ck('checkout SHA '+str(root),git('rev-parse','HEAD',root=root).decode().strip()==SHA)
  ck('clean checkout '+str(root),git('status','--porcelain',root=root)==b'')
 for row in receipt['files']:
  p=row['path'];b=(R/p).read_bytes();clone=(C/p).read_bytes();ls=git('ls-tree',SHA,'--',p).decode().split()
  ck('recovered file '+p,pinned(b,row) and clone==b and git('show',SHA+':'+p)==b and ls[0]==row['mode'] and ls[2]==row['gitBlob'])
 facts['recoveryReceipt']=img(receipt_path)
 idxpath=R/(D+'l01-managed-stack-s01-original-evidence-20261018.json');idx=read(idxpath);control=read(R/(D+'l01-managed-stack-capability-control-20261018.json'))
 rel=json.loads(git('show',ACT+':'+D+'l01-managed-stack-capability-control-20261018.json'))
 ck('actual release admitted reviewed source and prepared source',rel['smokeAdmission']['status']=='SOURCE_PREP_ACCEPTED_S01_ONCE' and rel['smokeAdmission']['reviewedSource']==SRC and rel['smokeAdmission']['preparedSource']==PREP)
 ck('current permit consumed no next or gameplay release',control['smokeAdmission']['status']=='CONSUMED_FIRST_S01_EVIDENCE_PENDING' and control['smokeAdmission']['nextRelease']=='NONE' and control['gameplayRelease']=='NONE')
 ck('eleven exact source pins',len(control['sourcePins'])==11)
 for row in control['sourcePins']:
  p=row['path'];b=(R/p).read_bytes()
  ck('source pin and actual/review continuity '+p,pinned(b,row) and b==git('show',ACT+':'+p)==git('show',SRC+':'+p))
 zip_path=R/idx['archive']['path'];ck('original ZIP hash/size',pinned(zip_path.read_bytes(),idx['archive']))
 z=zipfile.ZipFile(zip_path);expected=[x['entry'] for x in idx['archive']['files']]
 ck('exact eight original archive members no core',len(z.namelist())==8 and set(z.namelist())==set(expected) and all(not x.endswith('.core') for x in z.namelist()))
 for row in idx['archive']['files']:
  b=z.read(row['entry']);local=Path('/tmp/l01ms-e3edce403bfe41ee')/row['entry']
  ck('raw original chain '+row['entry'],pinned(b,row) and local.read_bytes()==b and oct(stat.S_IMODE(local.stat().st_mode))==row['originalMode'])
 rec=json.loads(z.read('s01/record.json'));start=json.loads(z.read('s01/started.json'));man=json.loads(z.read('s01/collector-manifest.json'));stack=json.loads(z.read('s01/synthetic-normal.core.crashreport.json'))
 ck('original record hash',h(z.read('s01/record.json'))==idx['originalRecordSha256']==control['smokeAdmission']['originalRecordSha256'])
 ck('first original source/prep/nonce/start',rec['source']==ACT and rec['preparedSource']==PREP and all(start[k]==rec[k] for k in ['source','preparedSource','nonce','startedUtc']) and start['collectorLaunches']==0 and start['nativeExit'] is None)
 ck('original native result and observer outcome',rec['status']=='PASS_OWNED_SYNTHETIC_MANAGED_STACK_ONLY' and rec['nativeExit']==rec['collector']['nativeExit']==rec['target']['nativeExit']==0 and rec['observerErrors']==rec['collector']['observerErrors']==[])
 ck('owned joins EOF no unresolved child',rec['collector']['joined'] and rec['target']['joinedEof'] and rec['childrenAfterRequest']==[] and not any(k in rec['target'] for k in ['forcedCleanupExit','cleanupExit']) and 'deadlineExceeded' not in rec['collector'] and 'terminatedOwnedCreatedump' not in rec)
 tid=rec['target']['identity'];cid=rec['collector']['identity'];ready=rec['ready']
 ck('cross-bound target/collector/supervisor identity',tid==idx['runIdentity']['target'] and cid==idx['runIdentity']['collector'] and tid['parentPid']==cid['parentPid']==man['SupervisorPid'] and tid['uid']==cid['uid']==man['Uid']==1000 and man['SupervisorBirthTick']<tid['birthTick']<cid['birthTick'])
 ck('target reported nonce PID starttick UID runtime',ready['Nonce']==rec['nonce']==man['Nonce'] and ready['Pid']==tid['pid']==man['TargetPid'] and ready['BirthTick']==tid['birthTick']==man['TargetBirthTick'] and ready['Uid']==tid['uid'] and ready['Runtime']=='.NET 8.0.32')
 ck('exact private command/socket binding',rec['target']['command']==man['TargetCommand'] and man['TargetCommand'][-1]==rec['nonce'] and man['Socket']==f"/tmp/l01ms-e3edce403bfe41ee/s01/tmp/dotnet-diagnostic-{tid['pid']}-{tid['birthTick']}-socket" and len(man['Socket'].encode())<=107 and man['DumpPath']==rec['privateCore']['path'])
 tev=[json.loads(x[len('L01_SYNTHETIC '):]) for x in z.read('s01/target.stdout').decode().splitlines() if x.startswith('L01_SYNTHETIC ')]
 cev=[json.loads(x[len('L01_COLLECTOR '):]) for x in z.read('s01/collector.stdout').decode().splitlines() if x.startswith('L01_COLLECTOR ')]
 ck('target raw events exactly ready and matching cooperative done',len(tev)==2 and tev[0]==ready and tev[1:]==rec['targetEventsAfterReady'] and tev[1]['Stage']=='done' and tev[1]['Nonce']==rec['nonce'] and tev[1]['ValidStop'] and tev[1]['Iterations']>ready['Iterations']>=1000)
 ck('collector raw exactly one entry returned0 exit0',cev==rec['collectorEvents'] and [x['Stage'] for x in cev]==['request_started','api_outcome','collector_exit'] and cev[1]['ApiOutcome']=='RETURNED' and cev[1]['ApiStatus']==0 and cev[1]['Failure'] is None and cev[2]['ObserverErrors']==[] and cev[2]['PreflightStatus']==0)
 ck('public API reach count precise',rec['collectorLaunches']==rec['diagnosticRequests']==rec['admittedRequestStages']==1 and rec['apiOutcome']=='RETURNED' and rec['apiNativeStatus']==0 and 'not an independently counted IPC' in rec['requestCountScope'])
 ck('entry ties request to exact socket/nonce/identity',all(cev[0][k]==man[k] for k in ['TargetPid','TargetBirthTick','Nonce','Socket']))
 for row in man['LoadedFiles']:
  event=next(x for x in cev[0]['Loaded'] if x['Path']==row['path']);ck('loaded ABI actual hash '+row['path'],pinned(Path(row['path']).read_bytes(),row) and event['Sha256']==row['sha256'])
 prepidx=read(R/(D+'l01-managed-stack-helper-preparation-20261018.json'));prep=prepidx['record'];pz=zipfile.ZipFile(R/prepidx['archive']['path'])
 ck('original preparation byte binding',pinned((R/prepidx['archive']['path']).read_bytes(),prepidx['archive']) and h(pz.read('prepare/record.json'))==prepidx['recordSha256']==rel['smokeAdmission']['prepRecordSha256'] and json.loads(pz.read('prepare/record.json'))==prep)
 ck('reviewed affected deltas retained exactly',rec['affectedPreparation']==rel['smokeAdmission']['acceptedAffectedPreparation']['sourcePinExceptions']==control['smokeAdmission']['acceptedAffectedPreparation']['sourcePinExceptions'])
 for row in prep['outputs']:
  b=Path(row['path']).read_bytes();entry='prepare/'+Path(row['path']).name
  ck('prepared output unchanged '+entry,pinned(b,row) and b==pz.read(entry))
 cfg=json.loads(pz.read('prepare/L01Synthetic.runtimeconfig.json'))['runtimeOptions'];ck('pinned runtime no roll-forward',cfg['framework']['version']=='8.0.32' and cfg['rollForward']=='Disable')
 mvid,methods=PE(pz.read('prepare/L01Synthetic.dll'));ck('independent static PE MVID',mvid==ready['Mvid']==prep['abi']['TargetMvid'])
 frames=[]
 for native_tid,leaf,outer in [(ready['WaitTid'],'KnownWaitLeaf','KnownWaitOuter'),(ready['CpuTid'],'KnownCpuLeaf','KnownCpuOuter')]:
  thread=next(t for t in stack['payload']['threads'] if int(t['native_thread_id'],0)==native_tid)
  ck('raw managed native TID '+str(native_tid),thread['is_managed']=='true' and thread['crashed']=='false')
  positions=[]
  for name in [leaf,outer]:
   matches=[(i,f) for i,f in enumerate(thread['stack_frames']) if name+'(' in f.get('method_name','')];ck('unique raw '+name,len(matches)==1);i,f=matches[0];positions.append(i)
   token=int(f['token'],0);il=int(f['il_offset'],0);method=methods[token]
   ck('exact frame metadata '+name,f['is_managed']=='true' and f['filename']=='L01Synthetic.dll' and f['guid']==mvid.replace('-','') and method['name']==name and 0<=il<method['codeBytes'] and method['implFlags']&0x48==0x48)
   frames.append({'nativeTid':native_tid,'rawFrameIndex':i,'frame':f,'staticMethodDef':method})
  ck('leaf precedes caller '+leaf,positions[0]<positions[1])
 ck('four indexed frame copies equal raw',idx['rawMatchedFrames']==[{k:v for k,v in row.items() if k!='staticMethodDef'} for row in frames])
 wait=next(t for t in stack['payload']['threads'] if int(t['native_thread_id'],0)==ready['WaitTid'])['stack_frames'];cpu=next(t for t in stack['payload']['threads'] if int(t['native_thread_id'],0)==ready['CpuTid'])['stack_frames']
 ck('independent known wait/work evidence',any('System.Console.ReadLine' in f.get('method_name','') for f in wait[:12]) and 'Interlocked.Increment' in cpu[0]['method_name'] and ready['WaitTid']!=ready['CpuTid'])
 core=idx['privateCore'];cp=Path(core['path']);ck('private core hash/size/mode only no upload',pinned(cp.read_bytes(),core) and stat.S_IMODE(cp.stat().st_mode)==0o600 and not core['uploaded'] and not core['recoverableFromGit'])
 ck('private root permissions',all(stat.S_IMODE(Path(p).stat().st_mode)==0o700 for p in ['/tmp/l01ms-e3edce403bfe41ee','/tmp/l01ms-e3edce403bfe41ee/s01']))
 ck('empty stderr',z.read('s01/target.stderr')==z.read('s01/collector.stderr')==b'')
 ck('createdump raw duration retained',b'Dump successfully written in 136ms' in z.read('s01/target.stdout') and idx['outcome']['createdumpReportedDurationMilliseconds']==136)
 ck('derived durations equal original, scope separately reviewed',idx['outcome']['requestWallSeconds']==rec['requestSeconds'] and idx['outcome']['phaseWallSeconds']==rec['durationSeconds'] and 0<rec['requestSeconds']<rec['durationSeconds']<20)
 base='39cafe9e9595730929efb7b7450b4cc260b5bafc';delta=git('diff','--name-only',base,SHA).decode().splitlines()
 ck('only scoped external docs/helpers changed since M01 acceptance',all(p.startswith(P) for p in delta))
 for name in ['t148-m01-source-packet-20261018.json','t148-m01-actual-evidence-20261018.json','t179pd-ni-iobs-l01-source-packet-20261018.json','t179pd-ni-iobs-l01-cloud-qualification-20261018.json','t179pd-ni-iobs-l01-method-row-ledger-20261018.json']:
  ck('historical immutable '+name,git('show',base+':'+D+name)==(R/(D+name)).read_bytes())
 ck('current holds explicit',idx['holds']['gameplayRelease']=='NONE' and idx['holds']['syntheticPermit']=='CONSUMED_NO_RETRY' and '52/57' in idx['holds']['L01'] and 'UNKNOWN' in idx['holds']['old66529'] and idx['holds']['cloudLinuxGameplay']=='HOLD' and idx['holds']['WindowsArtifact403']=='BLOCKED_NO_RETRY')
 facts.update(reviewedSource=SHA,actualSource=ACT,preparedSource=PREP,reviewedHelperSource=SRC,evidenceIndex=img(idxpath),archive=img(zip_path),rawRecordSha256=idx['originalRecordSha256'],rawStackReport=idx['rawStackReport'],frames=frames,ownTarget=tid,collector=cid,iterationsBefore=ready['Iterations'],iterationsAfter=tev[1]['Iterations'],collectorEnvelopeWallSeconds=rec['requestSeconds'],phaseWallSeconds=rec['durationSeconds'],createdumpReportedDurationMilliseconds=136,apiOnlyElapsed='UNMEASURED',actualSuspensionDuration='UNMEASURED',verifiedRawMembers=8,helperPins=11,privateCoreHashVerifiedNoContentInspection=True,scope='read-only files/Git/ZIP/static PE metadata only; no runtime/attach/process reads or mutation')
except BaseException as e:
 facts['readerFailure']={'type':type(e).__name__,'message':str(e)}
finally:
 result={'kind':'INDEPENDENT_S01_ORIGINAL_EVIDENCE_CHECKS','reviewedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'facts':facts,'checks':checks,'passed':all(c['passed'] for c in checks) and 'readerFailure' not in facts,'findings':[c for c in checks if not c['passed']]}
 OUT.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'output':str(OUT),'checks':len(checks),'passed':result['passed'],'readerFailure':facts.get('readerFailure'),'sha256':h(OUT.read_bytes())}))
 if not result['passed']:raise SystemExit(1)
