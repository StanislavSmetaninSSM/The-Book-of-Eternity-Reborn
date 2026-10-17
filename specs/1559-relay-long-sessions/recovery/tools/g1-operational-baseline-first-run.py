"""UNRUN future FIRST exact owner recorder. Separate named Root activation required."""
from pathlib import Path
import sys,subprocess,json,hashlib,re,datetime,time,tempfile
R=Path('/workspace/The-Book-of-Eternity-Reborn-1559')
label,actual,category,plan_record=sys.argv[1:]
assert re.fullmatch(r'gm1559-g1-operational-baseline-first-[a-z0-9-]+',label)
assert re.fullmatch(r'[0-9a-f]{40}',actual)
compiled='d473ed332fe7443f93759e44973ba963a790a13b'
allowed={
 'gm-relay-operational-live-preparation':('unit',[
  dict(Name='BookOfEternityClient.Tests.GmRelayPublicationFoundationTests.OperationalDraftExclusion_IsExactAndPreservesOrdinarySuffixNeighbors',Cases=4),
  dict(Name='BookOfEternityClient.Tests.LiveTurnPreparationServiceTests.RelayOperationalRecords_ActualLivePreparationExcludesOnlyOperationalSnapshotMembers',Cases=1)]),
 'gm-relay-operational-save-load':('integration',[
  dict(Name='BookOfEternityClient.Tests.SaveLoadServiceTests.RelayOperationalRecords_ActualSaveLoadExcludesOperationalAuthorityAndReplacesGeneration',Cases=1)])
}
assert category in allowed
adapter,expected_methods=allowed[category]
def git(*a):return subprocess.check_output(['git',*a],cwd=R)
def h(b):return hashlib.sha256(b).hexdigest()
assert git('rev-parse','HEAD').decode().strip()==actual and not git('status','--porcelain')
assert all(p.startswith('specs/1559-relay-long-sessions/') for p in git('diff','--name-only',compiled,actual).decode().splitlines())
prep=json.loads(Path(plan_record).read_text());assert prep['mode']=='plan' and prep['nativeExit']==0 and prep['compiledSource']==compiled and prep['actualTestsExecuted']==0
plan=json.loads((Path(prep['resultDirectories'][0])/'plan.json').read_text());owners=[d for d in plan['Descriptors'] if d['Category']==category]
assert len(owners)==1 and owners[0]['Adapter']==adapter and sorted(owners[0]['ExpectedMethodCounts'],key=lambda x:x['Name'])==sorted(expected_methods,key=lambda x:x['Name'])
binding_path=R/'specs/1559-relay-long-sessions/g1-source-packet.json';binding_bytes=binding_path.read_bytes();binding=json.loads(binding_bytes);assert binding['compiledSource']==compiled and binding['sourcePinCount']==119
for pin in binding['sourcePins']:
 path=R/pin['path'];data=path.read_bytes() if path.is_file() else git('show','HEAD:'+pin['path'])
 assert len(data)==pin['bytes'] and h(data)==pin['sha256'],pin['path']
pins=prep['binaryAfter'];assert len(pins)==9 and pins==prep['binaryBefore']
def binaries():return [dict(path=p['path'],bytes=(R/p['path']).stat().st_size,sha256=h((R/p['path']).read_bytes())) for p in pins]
assert binaries()==pins
out=Path('/tmp/gm1559-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category',category,'-NoBuild']
result_root=R/'TestResults/test-categories';old=set(result_root.iterdir()) if result_root.exists() else set()
patterns=['boe-gameengine-turnlifecycle-*','boe-relay1559-*','boe-live-turn-prep-*','boe-save-load-tests-*','boe-prompt-*']
def roots():return sorted(str(p) for pattern in patterns for p in Path(tempfile.gettempdir()).glob(pattern))
namespace_before=roots();start=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic();sentinel=out.parent/(compiled+'-'+category+'.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_ACTUAL_OWNER_SENTINEL',actualSource=actual,compiledSource=compiled,category=category,label=label,startedUtc=start),f,indent=2)
print('FIRST_OWNER_START',category,actual,str(out/'stdout.log'),flush=True)
with (out/'stdout.log').open('wb') as stream:
 process=subprocess.Popen(argv,cwd=R,stdout=stream,stderr=subprocess.STDOUT)
 proc=Path('/proc')/str(process.pid)
 try:
  stat=(proc/'stat').read_text();tail=stat[stat.rfind(')')+2:].split();identity=dict(pid=process.pid,birthTick=int(tail[19]),stat=stat,cmdline=(proc/'cmdline').read_bytes().decode().replace('\0',' '))
 except FileNotFoundError:identity=dict(pid=process.pid,alreadyExitedBeforeCensus=True)
 (out/'process.json').write_text(json.dumps(dict(argv=argv,identity=identity,startedUtc=start),indent=2)+'\n')
 native=process.wait()
record=dict(kind='FIRST_G1_REMAINING_OPERATIONAL_BASELINE_RAW_NOT_CAUSAL_VERDICT',label=label,category=category,argv=argv,cwd=str(R),actualSource=actual,compiledSource=compiled,productSource='a82b3d8439f76f83baade6df8516e78eea86e546',sourceBindingSha256=h(binding_bytes),sourcePinDigest=binding['sourcePinDigest'],sourcePinCount=119,nativeExit=native,startedUtc=start,finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),wallSeconds=time.monotonic()-clock,stdoutPath=str(out/'stdout.log'),stdoutSha256=h((out/'stdout.log').read_bytes()),processRecord=str(out/'process.json'),firstLaunchSentinel=str(sentinel),planRecord=str(plan_record),planSha256=h((Path(prep['resultDirectories'][0])/'plan.json').read_bytes()),expectedDescriptor=owners[0],resultDirectories=sorted(str(p) for p in set(result_root.iterdir())-old),binaryBefore=pins,binaryAfter=binaries(),ownedPrefixesBefore=namespace_before,ownedPrefixesAfter=roots(),headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),helperSource=Path(__file__).read_text(),scope='ONLY separately activated current named live/draft5 OR save/load1 original baseline. Native1 alone is not causal RED; require actual producer/save-load/generation/ordinary/suffix prerequisites and original exact final oracles, strict cleanup/source119/DLL9. No automatic retry, implementation, other owner or full49 selection.')
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n')
print('FIRST_OWNER_NATIVE',native,'record',str(out/'record.json'),flush=True)
sys.exit(native)
