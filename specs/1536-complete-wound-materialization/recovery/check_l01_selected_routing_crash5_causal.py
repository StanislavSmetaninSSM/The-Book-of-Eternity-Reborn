#!/usr/bin/env python3
"""Read-only frozen failure review; never invokes native tools or reads retained roots."""
import base64, datetime, gzip, hashlib, json, pathlib, re, subprocess

R=pathlib.Path('/workspace/The-Book-of-Eternity-Reborn')
C=pathlib.Path('/workspace/1536-cloud-clean-restore')
OUT=pathlib.Path('/tmp/1536-cloud-tools/review')
REL=pathlib.Path('specs/1536-complete-wound-materialization/recovery')
AR=REL/'t179pd-ni-iobs-l01-cloud-selected-routing-crash5-813f1057-files-20261018/archive.json'
SHA='5479e8c29732c06bca1c2dad778f5ee18c472109'
COMPILED='c391c03012e48c4e2395bdb288f0d763d2c0cf7f'
ACTUAL='85bab6db59f15f1b7ac02ac618ab4b00461544e5'
def h(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def git(*args):return subprocess.check_output(['git','-C',str(R),*args]).strip()
def load(p):return json.loads(p.read_bytes())
checks=[]
def check(c,label):
    checks.append({'check':label,'passed':bool(c)})
    if not c:raise AssertionError(label)

archive=load(R/AR)
check(h((R/AR).read_bytes())=='8faf5d9eb531d104183d744709ac7d393ad399b1663301c1bb4405c52f734d24','archive manifest exact')
raw={}
for f in archive['files']:
    p=AR.parent/f['storedPath'];z=(R/p).read_bytes();b=gzip.decompress(z)
    check(len(z)==f['storedBytes'] and h(z)==f['storedSha256'] and len(b)==f['bytes'] and h(b)==f['sha256'] and (C/p).read_bytes()==z,'chain '+f['storedPath'])
    check(f['rawPath'] not in raw,'unique raw '+f['storedPath']);raw[f['rawPath']]=b
check(len(raw)==247,'all 247 exact stored gzip/raw chains, no live retained-root reads')
record_name='/tmp/t179pd-ni-iobs-l01-process-implementation-tools/t179pd-ni-iobs-l01-process-first-crash5-selected-routing-cloud-813f1057/record.json'
record=json.loads(raw[record_name]);check(h(raw[record_name])=='4eb04ce91fcd6335329e5dc5a4e8ef66da3b6bdecbedd32ecc9654bfdfa9c206','record exact')
check(record['actualSource']==ACTUAL and record['compiledSource']==COMPILED and record['nativeExit']==124,'actual compiled native outcome')
check(record['binaryBefore']==record['binaryAfter'] and len(record['binaryAfter'])==9,'nine managed outputs unchanged')
check(record['allOutputInventoryBefore']==record['allOutputInventory'] and len(record['allOutputInventory'])==1575,'full original 1575 inventories unchanged')
for f in record['allOutputInventory']:
    b=(R/f['path']).read_bytes();check(len(b)==f['bytes'] and h(b)==f['sha256'],'current output '+f['path'])
packet=load(R/REL/'t179pd-ni-iobs-l01-source-packet-20261018.json')
check(packet['sourcePinCount']==99 and len(packet['sourcePins'])==99 and len(packet['productDelta'])==23,'source99 product23')
check(packet['pinDigest']==record['sourcePinDigest']=='70aa551ca427bb9c28b18e205bcbe2e10d1ebb88487309bb73b4a31426e37eb5','accepted pin digest')
for f in packet['sourcePins']:
    b=(R/f['path']).read_bytes();check(len(b)==f['bytes'] and h(b)==f['sha256'] and blob(b)==f['gitBlob'] and (C/f['path']).read_bytes()==b and git('show',COMPILED+':'+f['path'])==b.rstrip(), 'pin compiled/current/clone '+f['path'])
receipt_path=pathlib.Path('/tmp/1536-cloud-tools/carrier-recovery-5479e8c2.json');receipt=load(receipt_path)
check(receipt['sha']==SHA and receipt['remoteShaVerified'] and receipt['clean'] and not receipt['localSourceOrObjectReuse'] and receipt['sourcePinsVerified']==99,'saved independent GitHub recovery attestation')
check(len(receipt['files'])==receipt['changedPathsVerified']==255,'recovered 255 paths')
for f in receipt['files']:
    b=(C/f['path']).read_bytes();check(len(b)==f['bytes'] and h(b)==f['sha256'] and blob(b)==f['gitBlob'] and (R/f['path']).read_bytes()==b,'recovered path '+f['path'])
summary=json.loads(next(b for p,b in raw.items() if p.endswith('/summary.json')))
check(summary['ExitCode']==124 and summary['TimedOut'] and not summary['Selection']['Complete'] and summary['Selection']['PlannedCases']==5 and summary['Tests']=={'Total':0,'Executed':0,'Passed':0,'Failed':0},'incomplete no-TRX summary is not executed-case truth')
check(not any(p.endswith('.trx') for p in raw),'no TRX in original archive')
check(summary['OwnedTreeCleanupSucceeded'] and summary['RuntimeCleanupSucceeded'],'native runner owned-tree/runtime cleanup flags')
logpath=next(p for p in raw if p.endswith('/dotnet-test.log'));log=raw[logpath].decode();rows=[];parse_errors=[]
for n,line in enumerate(log.splitlines(),1):
    s=line.strip()
    if not s.startswith('{'):continue
    try:j=json.loads(s)
    except Exception as e:parse_errors.append({'line':n,'error':str(e)});continue
    if isinstance(j,dict):rows.append((n,j))
check(not parse_errors,'all top-level JSON diagnostic lines parse')
failures=[]
lines=log.splitlines()
for i,line in enumerate(lines):
    if re.search(r'^\s*Failed .*ActualProcessLoss.*\(cut: [12]\)',line):
        excerpt='\n'.join(lines[i:i+12]).split('Standard Output Messages:',1)[0].rstrip();check('System.TimeoutException' in excerpt and 'line 127' in excerpt and 'line 64' in excerpt,'direct timeout stack '+line.strip());failures.append({'line':i+1,'text':excerpt})
check(len(failures)==2,'two observed xUnit timeout failures')
joins=[];cuts=[]
for n,j in rows:
    o=j.get('Observation',j)
    if j.get('Boundary')=='iobs_actual_native_join':
        args=o['Arguments'];role=args[6];cut=int(args[-2]);stdout=o['RawStdout'] or ''
        joins.append({'line':n,'cut':cut,'role':role,'pid':o['Id'],'birthTick':o['BirthTick'],'exit':o['ExitCode'],'eof':o['Eof'],'joined':o['Joined'],'failure':o['Failure'],'cleanupFailure':o['CleanupFailure'],'retainedContainer':o['RetainedOwnedContainer'],'stdoutBytes':len(stdout.encode()),'stdoutSha256':h(stdout.encode()),'stderrBytes':len((o['RawStderr'] or '').encode()),'root':args[8]})
        expected=73 if role.endswith('original-crash') else 137
        check(o['ExitCode']==expected and o['Eof'] and o['Joined'] and o['CleanupFailure'] is None and o['RetainedOwnedContainer'] is None,'joined child '+str(o['Id']))
        if role.endswith('resume'):
            check('TimeoutException' in o['Failure'] and 'line 127' in o['Failure'],'resumer timeout '+str(cut))
            if cut==1:check(stdout=='','cut1 no stage stdout')
            if cut==2:
                event=json.loads(stdout.strip());check(event['Boundary']=='iobs_independent_ordinary_policy_contrast','cut2 only auxiliary contrast stdout')
check(len(joins)==4 and len({(x['pid'],x['birthTick']) for x in joins})==4,'four observed distinct positive native identities')
cutpath=next(p for p in raw if p.endswith('/cut.json'));cut=json.loads(raw[cutpath]);root=cut['Root']
pending_path=root+'/game_session/game_state/pending_wound_resolutions.json'
if pending_path not in raw:pending_path=next(p for p in raw if p.startswith(root+'/game_session/') and p.endswith('/pending_wound_resolutions.json'))
pending=json.loads(raw[pending_path].decode('utf-8-sig'));result=pending['jointInterfacePublication']
check(cut['Cut']==0 and cut['Result']['state']=='Prepared' and result['state']=='Published','interrupted cut0 physical Prepared-to-Published progress')
stable=lambda x:{k:v for k,v in x.items() if k not in ['state','fingerprint']}
check(stable(result)==stable(cut['Result']),'cut0 immutable result association unchanged')
interface_path=root+'/game_session/output/interface_updates.json'
after_image=result['afterImage'];after_bytes=base64.b64decode(after_image['base64']) if after_image.get('exists',after_image.get('present',True)) else None
check(raw[interface_path]==after_bytes,'cut0 physical I equals stored result afterimage')
mechanical={}
for path,encoded in cut['MechanicalAtCut'].items():
    actual=raw.get(root+'/game_session/'+path);expected=None if encoded is None else base64.b64decode(encoded);mechanical[path]=actual==expected
check(all(mechanical.values()),'cut0 retained mechanical images equal cut-time images')
check(not any(p.startswith(root+'/') and p.endswith('/active.json') for p in raw),'cut0 retained no active journal image')
check(not any('resume.json' in p for p in raw),'no completed resume artifact in archive')
snapshots={}
for stage in ['before','after']:
    p=R/REL/f't179pd-ni-iobs-l01-selected-routing-crash5-{stage}-census-20261018.json';j=load(p)
    check(not j['unknown'] and not j['activeTaskGameTestProcesses'],'dated '+stage+' no active/unknown')
    snapshots[stage]={'path':str(p),'sha256':h(p.read_bytes()),'startedUtc':j['startedUtc'],'finishedUtc':j['finishedUtc'],'cleanBoundary':j['cleanBoundary'],'ownedRoots':j['ownedRoots']}
check(snapshots['before']['cleanBoundary'] and not snapshots['after']['cleanBoundary'] and len(snapshots['after']['ownedRoots'])==2,'after boundary correctly not clean: two retained roots')
report={'kind':'INDEPENDENT_FROZEN_CRASH5_CAUSAL_EVIDENCE_CHECK_NOT_RUNTIME_ACCEPTANCE','carrier':SHA,'compiled':COMPILED,'actual':ACTUAL,'archive':{'path':str(AR),'sha256':h((R/AR).read_bytes()),'chains':len(raw)},'recordSha256':h(raw[record_name]),'recovery':{'path':str(receipt_path),'sha256':h(receipt_path.read_bytes()),'verifiedChangedPaths':255,'remoteVerification':'Parent recovery receipt; independently checked restored bytes, not a new remote network query'},'sourcePins':99,'productDelta':23,'outputs':1575,'managedCopies':9,'nativeExit':124,'wallSeconds':record['wallSeconds'],'summary':summary,'directFailures':failures,'joinObservations':joins,'topLevelJsonRows':len(rows),'jsonParseErrors':parse_errors,'cut0':{'root':root,'pid':cut['Pid'],'birthTick':cut['BirthTick'],'nonce':cut['Nonce'],'cutState':cut['Result']['state'],'retainedState':result['state'],'sameImmutableResult':True,'interfaceMatchesAfter':True,'mechanicalImagesUnchanged':mechanical,'noResumeArtifact':True,'limits':'Physical post-cut progress only; no child return, own release, common completion or successful oracle inferred.'},'censuses':snapshots,'checksPassed':len(checks),'findings':[],'semanticBlockers':['Observed cut1/cut2 resumer timeouts; exact blocked await and product-versus-fixture cause unproved.','Category timeout/incomplete selection/no TRX; cut0 interrupted; cuts3/4 not evidenced complete.','Two retained disposable roots prevent clean reuse of original TMPDIR.'],'qualifiedBeforeAndAfter':52,'oldLostCrash':'UNKNOWN/unavailable, unchanged'}
p=OUT/'t179pd-ni-iobs-l01-selected-routing-crash5-causal-independent-check-5479e8c2-20261018.json';p.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({'report':str(p),'sha256':h(p.read_bytes()),'checksPassed':len(checks),'findings':[]}))
