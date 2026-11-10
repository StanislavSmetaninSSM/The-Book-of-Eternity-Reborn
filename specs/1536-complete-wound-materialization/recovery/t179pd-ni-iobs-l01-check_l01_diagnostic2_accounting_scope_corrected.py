#!/usr/bin/env python3
"""Read-only arithmetic/identity assessment of already archived diagnostic2 profile.
Requires completed independent native-outcome reader. No live process/fixture access.
No automatic causal verdict, runtime release, target qualification or budget change.
"""
from pathlib import Path
import argparse,collections,datetime as dt,gzip,hashlib,json,math,posixpath,re,sys
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=Path('/tmp/1536-cloud-tools/review');issues=[]
def h(b):return hashlib.sha256(b).hexdigest()
def ref(p):
 p=Path(p);b=p.read_bytes();return dict(path=str(p),bytes=len(b),sha256=h(b))
def stamp(s):return dt.datetime.fromisoformat(s.replace('Z','+00:00')).timestamp()
def issue(label,detail=None):issues.append(dict(issue=label,detail=detail))
def require(v,label):
 if not v:raise AssertionError(label)
def counters(s):
 out={}
 for line in s.splitlines():
  f=line.split();require(len(f)==2 and f[0].rstrip(':')not in out,'counter shape/duplicate');out[f[0].rstrip(':')]=int(f[1]);require(out[f[0].rstrip(':')]>=0,'nonnegative counter')
 return out
def parse_stat(text,pid):
 f=text.rsplit(')',1)[1].split();require(int(text.split(' ',1)[0])==pid and len(f)>=22,'stat shape/PID')
 return dict(pid=pid,startTick=int(f[19]),utime=int(f[11]),stime=int(f[12]),state=f[0])
def membership(raw):
 values=[line.split(':',2)[2]for line in raw.splitlines()if line.startswith('0::')]
 return values[0]if len(values)==1 else None
def unescape(s):return re.sub(r'\\([0-7]{3})',lambda m:chr(int(m.group(1),8)),s)
def governance(env,member):
 try:
  node=env['values']['sampledCgroupNode']['path'];own=membership(env['values']['samplerCgroup']);target=membership(member)
  require(target is not None and own is not None,'unified memberships')
  require(target.startswith('/')and own.startswith('/')and '..'not in target.split('/')and '..'not in own.split('/'),'canonical memberships')
  mappings=[]
  for line in env['values']['cgroupMountMapping'].splitlines():
   left,right=line.split(' - ',1);f=left.split();g=right.split();require(g[0]=='cgroup2','cgroup2 mount')
   mountroot,mountpoint=unescape(f[3]),unescape(f[4]);require(mountroot.startswith('/') and mountpoint.startswith('/') and '..'not in mountroot.split('/') and '..'not in mountpoint.split('/'),'namespace-relative ancestor mount root cannot be normalized into a proven mapping');rel=posixpath.relpath(node,mountpoint)
   if rel=='..'or rel.startswith('../'):continue
   represented=posixpath.normpath(posixpath.join(mountroot,rel));under=lambda x:x==represented or x.startswith(represented.rstrip('/')+'/')
   mappings.append(dict(mountRoot=mountroot,mountPoint=mountpoint,sampledNode=node,representedMembershipPath=represented,samplerMembership=own,targetMembership=target,governsBoth=under(own)and under(target)))
  good=[x for x in mappings if x['governsBoth']]
  return dict(established=len(good)==1,mappings=mappings,limit='Raw same-namespace lexical mount/membership relation only. Shared node includes other processes; not exclusive attribution or proof of a particular quota ancestor.')
 except Exception as e:return dict(established=False,error=str(e),limit='Mapping incomplete/ambiguous; no target pressure attribution.')
def cpu(e):return e['statAfter']['utime']+e['statAfter']['stime']
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('outcome_check');args=p.parse_args();n=json.loads(Path(args.outcome_check).read_bytes());require(n['kind']=='INDEPENDENT_DIAGNOSTIC2_ORIGINAL_OUTCOME_READER_NOT_CAUSAL_VERDICT','bound neutral reader');require(n['actualSource']=='52dc426673a14ce718ab85481234109063be0317'and n['compiledSource']=='5aa8673ec128d808b3fdc3d3fd244946e2ae7df6','one allowed original source')
 ea=n['externalAccounting']['archive'];raw=(R/ea['path']).read_bytes();require(h(raw)==ea['sha256'],'paired archive SHA');archive=json.loads(raw);paths={}
 for x in archive['files']:
  z=(R/Path(ea['path']).parent/x['storedPath']).read_bytes();b=gzip.decompress(z);require((len(z),h(z),len(b),h(b))==(x['storedBytes'],x['storedSha256'],x['bytes'],x['sha256']),'paired raw chain');paths[x['rawPath']]=b
 profile_key='/tmp/1536-cloud-tools/diagnostic2-diagnostic2-accounting-de0f3e20.jsonl';rows=[];parse_errors=[]
 for i,line in enumerate(paths.get(profile_key,b'').splitlines(),1):
  try:rows.append(json.loads(line))
  except Exception as e:parse_errors.append(dict(line=i,error=str(e)))
 if profile_key not in paths or parse_errors:issue('missing or malformed profile',parse_errors)
 starts=[x for x in rows if x.get('kind')=='PROFILE_START'];envs=[x for x in rows if x.get('kind')=='PROFILE_ENVIRONMENT'];finishes=[x for x in rows if x.get('kind')=='PROFILE_FINISH'];scans=[x for x in rows if x.get('kind')=='PROFILE_SCAN'];require(len(starts)<=1 and len(envs)<=1 and len(finishes)<=1,'no duplicate profile envelope')
 if len(starts)!=1 or len(envs)!=1 or len(finishes)!=1:issue('incomplete profile envelope',dict(start=len(starts),environment=len(envs),finish=len(finishes)))
 hz=starts[0]['clockTickHz']if starts else None;require(hz is None or isinstance(hz,int)and hz>0,'clock frequency');environment=envs[0]if envs else {'values':{}};groups=collections.defaultdict(list);joins={(x['pid'],x['birthTick']):x for x in n['joinObservations']};last_scan=0;last_mono=None
 for s in scans:
  require(s['scan']>last_scan,'scan sequence strictly increasing')
  if s['scan']!=last_scan+1:issue('scan sequence gap',dict(previous=last_scan,current=s['scan']))
  require(s['monotonicEnd']>=s['monotonicStart']and s['durationSeconds']>=0,'nonnegative scan interval')
  if last_mono is not None:require(s['monotonicStart']>=last_mono,'nonoverlapping scans')
  last_scan=s['scan'];last_mono=s['monotonicEnd'];seen=set()
  for e in s['entries']:
   ident=(e['identity']['pid'],e['identity']['startTick']);require(ident not in seen,'one identity per scan');seen.add(ident);b=e['binding']
   for k in ['statBefore','statAfter']:
    st=parse_stat(e[k]['raw'],ident[0]);require(all(st[x]==e[k][x]for x in st),'raw process stat fields');require(st['startTick']==ident[1],'stable process birth')
   require(h(('\0'.join(b['argv'])+'\0').encode())==e['argvSha256'],'argv exact hash');require(e['statAfter']['utime']>=e['statBefore']['utime']and e['statAfter']['stime']>=e['statBefore']['stime'],'nondecreasing in-scan CPU')
   j=joins.get(ident);attributed=bool(j and b['role']==j['role']and b['root']==j['root']and b['own']==j['ownedContainer']and n['cut']and b['nonce']==n['cut']['Nonce'])
   if not attributed:issue('profile identity not independently bound to native join/cut',dict(identity=ident,scan=s['scan']))
   for t in e['tasks']:
    if 'statAfter'not in t:continue
    for k in ['statBefore','statAfter']:
     st=parse_stat(t[k]['raw'],t['tid']);require(st['startTick']==t['startTick']and st['utime']==t[k]['utime']and st['stime']==t[k]['stime'],'task exact stat birth/counters')
   groups[ident].append((s,e,attributed))
 summaries=[]
 for ident,values in groups.items():
  first_binding=values[0][1]['binding'];require(all(x[1]['binding']==first_binding for x in values),'stable root/nonce/argv manifest binding');intervals=[]
  for (a,x,ax),(b,y,by)in zip(values,values[1:]):
   wall=b['monotonicEnd']-a['monotonicEnd'];ticks=cpu(y)-cpu(x);require(wall>0 and ticks>=0,'positive same-identity wall/nonnegative CPU delta')
   gaps=[dict(scan=z['scan'],**g)for z in [a,b]for g in z['gaps']if g.get('pid')in {None,ident[0]}];consecutive=b['scan']==a['scan']+1;spacing=wall<=3;io=None
   try:
    ix,iy=counters(x['io']),counters(y['io']);require(ix.keys()==iy.keys(),'IO counter set stable');io={k:iy[k]-ix[k]for k in ix};require(all(z>=0 for z in io.values()),'nondecreasing IO counters')
   except Exception as error:gaps.append(dict(stage='derived-io-gap',error=str(error)))
   seconds=ticks/hz if hz else None;lower=max(0,b['monotonicStart']-a['monotonicEnd']);upper=b['monotonicEnd']-a['monotonicStart']
   intervals.append(dict(firstScan=a['scan'],lastScan=b['scan'],startUtc=a['utc'],endUtc=b['utc'],wallSeconds=wall,cpuSeconds=seconds,averageCores=None if seconds is None else seconds/wall,cpuCoreRangeDueToReadInterval=None if seconds is None or lower<=0 else [seconds/upper,seconds/lower],ioDeltas=io,consecutiveScans=consecutive,maxSpacingGuard=spacing,attributed=ax and by,gaps=gaps,classificationEligible=bool(ax and by and consecutive and spacing and not gaps and hz),membershipStable=x.get('cgroupMembership')==y.get('cgroupMembership')))
  windows=[]
  for i in range(len(values)):
   j=i+1
   while j<len(values)and(values[j][0]['monotonicEnd']-values[i][0]['monotonicEnd']<30 or j-i+1<10):j+=1
   if j>=len(values):break
   selected=intervals[i:j];wall=sum(v['wallSeconds']for v in selected);used=sum(v['cpuSeconds']or 0 for v in selected);eligible=all(v['classificationEligible']for v in selected)
   windows.append(dict(startUtc=values[i][0]['utc'],endUtc=values[j][0]['utc'],wallSeconds=wall,cpuSeconds=used,averageCores=used/wall,samples=j-i+1,maxEndpointGap=max(v['wallSeconds']for v in selected),classificationEligible=eligible,meetsActiveCpuHeuristic=eligible and used/wall>=.5,meetsLowCpuHeuristic=eligible and used/wall<=.05,limit='Descriptive only. LowCPU additionally needs worker/wait/I/O evidence and never proves managed lock ownership. Ineligible windows are not used for causal classification.'))
  stages=[];stream=next((v for v in n['streamedDiagnostics']if(v['identity']['pid'],v['identity']['birthTick'])==ident),None)
  if stream:
   for a,b in zip(stream['events'],stream['events'][1:]):
    start,end=stamp(a['Utc']),stamp(b['Utc']);elapsed=(b['ElapsedMilliseconds']-a['ElapsedMilliseconds'])/1000
    if elapsed<5:continue
    selected=[v for v in intervals if stamp(v['startUtc'])>=start and stamp(v['endUtc'])<=end];valid=[v for v in selected if v['classificationEligible']]
    stages.append(dict(fromStage=a['Stage'],fromDetail=a['Detail'],toStage=b['Stage'],toDetail=b['Detail'],startUtc=a['Utc'],endUtc=b['Utc'],stageMonotonicSeconds=elapsed,stageUtcSeconds=end-start,whollyContainedEndpointIntervals=len(selected),eligibleIntervals=len(valid),eligibleEndpointWallSeconds=sum(v['wallSeconds']for v in valid),eligibleCpuSeconds=sum(v['cpuSeconds']for v in valid),maxEndpointGap=max((v['wallSeconds']for v in selected),default=None),edgeAndGapTimeNotExtrapolated=True))
  memberships=sorted({x[1].get('cgroupMembership','<MISSING>')for x in values});maps=[dict(rawMember=m,assessment=governance(environment,m))for m in memberships];tasks=collections.Counter(t.get('statAfter',{}).get('state','<GAP>')for _,e,_ in values for t in e['tasks']);waits=collections.Counter(t.get('wchan','<GAP>').strip()for _,e,_ in values for t in e['tasks'])
  totalwall=values[-1][0]['monotonicEnd']-values[0][0]['monotonicEnd'];totalcpu=(cpu(values[-1][1])-cpu(values[0][1]))/hz if hz else None
  summaries.append(dict(pid=ident[0],startTick=ident[1],binding=first_binding,samples=len(values),firstUtc=values[0][0]['utc'],lastUtc=values[-1][0]['utc'],endpointTotals=dict(wallSeconds=totalwall,cpuSeconds=totalcpu,averageCores=totalcpu/totalwall if totalcpu is not None and totalwall>0 else None,mayBridgeMissingObservation=True,notContinuousOrExactLaunchToExit=True),intervals=intervals,windows=windows,stageWindows=stages,taskStateSnapshots=dict(tasks),taskWchanSnapshots=dict(waits),membershipMappings=maps))
 cgroup=[]
 for a,b in zip(scans,scans[1:]):
  row=dict(firstScan=a['scan'],lastScan=b['scan'],startUtc=a['utc'],endUtc=b['utc'],wallSeconds=b['monotonicEnd']-a['monotonicEnd'],deltas={},gaps=[],quotaUnchanged=a['cgroup'].get('cpu.max')==b['cgroup'].get('cpu.max'),memoryLimitUnchanged=a['cgroup'].get('memory.max')==b['cgroup'].get('memory.max'),cpuMax=b['cgroup'].get('cpu.max'),memoryMax=b['cgroup'].get('memory.max'))
  for key in ['cpu.stat','memory.events']:
   try:
    x,y=counters(a['cgroup'][key]),counters(b['cgroup'][key]);require(x.keys()==y.keys(),'cgroup keys stable');delta={k:y[k]-x[k]for k in x};require(all(v>=0 for v in delta.values()),'cgroup monotonic counters');row['deltas'][key]=delta
   except Exception as e:row['gaps'].append(dict(field=key,error=str(e)))
  try:row['memoryCurrent']=[int(a['cgroup']['memory.current']),int(b['cgroup']['memory.current'])]
  except Exception as e:row['gaps'].append(dict(field='memory.current',error=str(e)))
  row['classificationEligible']=b['scan']==a['scan']+1 and row['wallSeconds']<=3 and not row['gaps'] and row['quotaUnchanged']and row['memoryLimitUnchanged'];row['scope']='Shared node only; require matching actual per-identity mount/membership before target quota/pressure interpretation.';cgroup.append(row)
 out=dict(kind='INDEPENDENT_DIAGNOSTIC2_ACCOUNTING_DESCRIPTIVE_CHECK_NOT_JOINT_VERDICT',checkedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),carrier=n['carrier'],actualSource=n['actualSource'],compiledSource=n['compiledSource'],reader=ref(__file__),nativeOutcomeCheck=ref(args.outcome_check),nativeExit=n['nativeExit'],profileArchive=ea,profileSha256=h(paths[profile_key])if profile_key in paths else None,rows=len(rows),scans=len(scans),clockTickHz=hz,environment=environment,profileFinish=finishes[0]if finishes else None,identities=summaries,cgroupIntervals=cgroup,scanOverhead=dict(sumMeasuredCpuSeconds=sum(s['samplerCpuSeconds']for s in scans),maxDurationSeconds=max((s['durationSeconds']for s in scans),default=None),maxLateSeconds=max((s['lateBySeconds']for s in scans),default=None),finishTotalCpuSeconds=finishes[0].get('samplerTotalCpuSeconds')if finishes else None),allRawScanGaps=[dict(scan=s['scan'],**g)for s in scans for g in s['gaps']],headerGaps=environment.get('gaps',[]),findings=issues,countsToward57=False,qualifiedRemains=52,limits=['Profile/scalars are observed data only; sampling perturbs timing. No old timeout cause inferred.','Same-identity endpoint CPU totals may bridge unobserved periods and exclude launch/exit edges. Classification windows require consecutive scans, spacing<=3s and no target/global observed gap; this is a conservative reader guard, not a test assertion or performance PASS threshold.','Process totals include threads; never sum task CPU into process CPU. State/wchan are dated snapshots, not lock ownership.','Cgroup mapping must be established for a child; shared totals never exclusively attributed. Pressure or apparent waiting alone does not prove a product defect.','One authorized diagnostic consumed; final joint verdict requires manual evidence/source review and must be narrow defect/remedy, justified environment budget with separate performance hold, or bounded blocker. No automatic further diagnostic/retry.'])
 dest=D/('t179pd-ni-iobs-l01-diagnostic2-accounting-scope-corrected-independent-check-'+n['carrier'][:8]+'-20261018.json');require(not dest.exists(),'preserve prior output');dest.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps({'report':ref(dest),'identities':len(summaries),'scans':len(scans),'findings':issues,'noAutomaticVerdict':True}))
if __name__=='__main__':main()
