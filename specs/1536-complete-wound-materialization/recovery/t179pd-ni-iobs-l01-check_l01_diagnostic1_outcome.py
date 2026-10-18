#!/usr/bin/env python3
"""Read-only diagnostic1 original outcome reader. Run ONLY after saved archive/recovery.
No native/test/build/controller/observer/process actions and no live fixture-root reads.
Outcome is neutral: evidence findings, observed runtime failures and causal limits are separate.
"""
from pathlib import Path
import argparse, base64, collections, datetime as dt, gzip, hashlib, json, os, re, subprocess, sys
import xml.etree.ElementTree as ET
R=Path('/workspace/The-Book-of-Eternity-Reborn'); C=Path('/workspace/1536-cloud-clean-restore'); D=Path('/tmp/1536-cloud-tools/review')
REC='specs/1536-complete-wound-materialization/recovery/'
ACTUAL='801229a1d301cc3526b451c95cc71953f89f1f62'
COMPILED='8f8f534304e3672df5d42c9ab31e7055235bfaca'
DIGEST='2c144b7734708f8c02a3924d4d12a885384f05a1437021afd2845827c761c616'
LABEL='t179pd-ni-iobs-l01-process-first-diagnostic1-compile-corrected-cloud-b51375ba'
RECORD='/tmp/t179pd-ni-iobs-l01-process-implementation-tools/'+LABEL+'/record.json'
METHOD='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss'
CATEGORY='fr063-joint-interface-crash-diagnostic'
NAMESPACE='/tmp/ni-iobs-l01-crash-diagnostic1-20261018-b51375bae9144c92842838e1210df2a9'
OBSERVER='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f'
checks=[]; findings=[]; parse_errors=[]
def h(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def js(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root,env={**os.environ,'GIT_OPTIONAL_LOCKS':'0'})
def check(ok,name):
 checks.append({'check':name,'passed':bool(ok)})
 if not ok:raise AssertionError(name)
def observe(ok,name,detail=None):
 if not ok:findings.append({'finding':name,'detail':detail,'scope':'evidence integrity or diagnostic attribution; not automatic product classification'})
def ref(path):
 path=Path(path);b=path.read_bytes();return {'path':str(path),'bytes':len(b),'sha256':h(b)}
def saved_ref(path):
 path=Path(path);check(not path.is_absolute() and path.parts[:3]==('specs','1536-complete-wound-materialization','recovery'),'repository evidence path')
 b=(R/path).read_bytes();check(b==(C/path).read_bytes()==git(C,'show',ARGS.carrier+':'+str(path)),'saved/clone/blob '+str(path));return b

def json_lines(text,location):
 rows=[]
 for n,line in enumerate((text or '').splitlines(),1):
  s=line.strip()
  if not s.startswith('{'):continue
  try:value=json.loads(s)
  except Exception as exc:parse_errors.append({'source':location,'line':n,'error':str(exc)});continue
  if isinstance(value,dict):rows.append({'source':location,'line':n,'value':value})
 return rows

def plain_images(value):
 """Decode image dictionaries only; no physical file access or implicit absent fallback."""
 if value is None:return None
 check(isinstance(value,dict),'image dictionary type')
 result={}
 for path,encoded in value.items():
  check(isinstance(path,str) and (encoded is None or isinstance(encoded,str)),'image entry shape')
  data=None if encoded is None else base64.b64decode(encoded,validate=True)
  result[path]={'present':data is not None,'bytes':None if data is None else len(data),'sha256':None if data is None else h(data)}
 return result

def direct_errors(log):
 lines=log.splitlines();out=[]
 for i,line in enumerate(lines):
  if not (re.search(r'^\s*Failed .*DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss',line) or ('[FAIL]'in line and 'DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss'in line)):continue
  excerpt=[line]
  for nextline in lines[i+1:i+45]:
   if 'Standard Output Messages:'in nextline or nextline.strip().startswith('{') or re.match(r'^\s*(Failed|Passed) ',nextline):break
   excerpt.append(nextline)
  out.append({'source':'dotnet-test.log','line':i+1,'text':'\n'.join(excerpt).rstrip()})
 return out

def validate_snapshot(path,record,side):
 raw=saved_ref(path);c=json.loads(raw)
 check(c['observerVersion']==2 and c['observerSourceSha256']==OBSERVER and c['fixtureTemp']==NAMESPACE and c['fixtureTempUid']==1000,'dated '+side+' accepted observer/namespace')
 for e in c['entries']:
  def identity(stat):
   f=stat.rsplit(')',1)[1].split();return int(stat.split(' ',1)[0]),int(f[19]),int(f[1]),f[0]
  check(identity(e['statBefore'])[:2]==identity(e['statAfter'])[:2]==(e['pid'],e['startTick']),'dated identity '+side+' '+str(e['pid']))
  if 'argv'in e:check(h(('\0'.join(e['argv'])+'\0').encode())==e['argvSha256'],'dated argv '+side+' '+str(e['pid']))
 check(all(x['confirmedAbsentOnReprobe']for x in c['confirmedGone']),'confirmed disappearances '+side)
 unknown=c['unknown'];active=c['activeTaskGameTestProcesses'];roots=c['ownedRoots']
 if side=='before':check(c['cleanBoundary'] and not unknown and not active and not roots,'before gate clean')
 else:observe(c['cleanBoundary'] and not unknown and not active and not roots,'after boundary not clean',{'unknown':unknown,'active':active,'roots':roots})
 delta=(dt.datetime.fromisoformat(record['startedUtc'])-dt.datetime.fromisoformat(c['finishedUtc'])).total_seconds() if side=='before' else (dt.datetime.fromisoformat(c['startedUtc'])-dt.datetime.fromisoformat(record['finishedUtc'])).total_seconds()
 check(delta>=0,'dated '+side+' chronological bound')
 return {'path':str(path),'sha256':h(raw),'entries':len(c['entries']),'startedUtc':c['startedUtc'],'finishedUtc':c['finishedUtc'],'cleanBoundary':c['cleanBoundary'],'active':active,'unknown':unknown,'ownedRoots':roots,'knownVisibility':c['knownVisibility'],'gapSeconds':delta,'scope':'Dated observation only, no continuous absence or reconstruction.'}

def main():
 global ARGS
 p=argparse.ArgumentParser(description=__doc__)
 p.add_argument('carrier');p.add_argument('--archive',required=True);p.add_argument('--archive-sha256',required=True)
 p.add_argument('--before',required=True);p.add_argument('--after',required=True);p.add_argument('--frozen-separation',required=True)
 p.add_argument('--recovery',action='append',required=True,help='Continuous receipt(s) from actual801229a1 to saved outcome carrier')
 ARGS=p.parse_args();check(bool(re.fullmatch('[0-9a-f]{40}',ARGS.carrier)),'carrier format')
 for root in (R,C):check(git(root,'rev-parse','HEAD').decode().strip()==ARGS.carrier and not git(root,'status','--porcelain'),'clean saved HEAD '+str(root))
 check(git(C,'ls-remote','origin','refs/heads/1536-wound-b2-cloud-20261012').decode().split()[0]==ARGS.carrier,'exact remote SHA')
 check(not (C/'.git/objects/info/alternates').exists(),'no clone object alternates')
 check('github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn'in git(C,'remote','get-url','origin').decode(),'independent clone GitHub origin')
 check(git(R,'ls-tree','-rz',ARGS.carrier)==git(C,'ls-tree','-rz',ARGS.carrier),'whole saved Git trees match')
 check(not git(R,'diff','--name-only',COMPILED,ARGS.carrier,'--','.',':!specs/1536-complete-wound-materialization'),'only recovery metadata since compiled source')
 packet=js(R/(REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'));cloud=js(R/(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
 actual_packet_bytes=git(R,'show',ACTUAL+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json');actual_packet=json.loads(actual_packet_bytes)
 actual_cloud=json.loads(git(R,'show',ACTUAL+':'+REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
 check(len(packet['sourcePins'])==101 and len(packet['productDelta'])==23 and packet['sourcePins']==actual_packet['sourcePins'],'accepted101 product23')
 check(packet['pinDigest']==h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==DIGEST,'pin digest')
 check(packet['productDelta']==git(R,'diff','--name-only',packet['productBase'],ARGS.carrier,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines(),'actual product23 delta')
 tree_entries={}
 for row in git(R,'ls-tree','-rz',ARGS.carrier).split(b'\0'):
  if row:
   meta,path=row.split(b'\t',1);tree_entries[path.decode()]=tuple(meta.decode().split())
 for pin in packet['sourcePins']:
  check(tree_entries[pin['path']]==(pin['mode'],'blob',pin['gitBlob']),'pin Git mode/blob '+pin['path'])
  b=(R/pin['path']).read_bytes();check((len(b),h(b),blob(b))==(pin['bytes'],pin['sha256'],pin['gitBlob']) and b==(C/pin['path']).read_bytes()==git(R,'show',COMPILED+':'+pin['path']),'pin '+pin['path'])
 release=actual_packet['namedActualRelease'];check(release['status']=='SEPARATELY_NAMED_ONE_DIAGNOSTIC_AFTER_SAVED_ACCEPTANCE_RECOVERY_AND_CLEAN_PREFLIGHT'and release['mode']=='diagnostic1'and release['label']==LABEL and release['compiledSource']==COMPILED and release['method']==METHOD and release['category']==CATEGORY and release['expectedCases']==1 and not release['countsToward57'],'explicit actual release at801')
 check(cloud['qualifiedCases']==actual_cloud['qualifiedCases'] and cloud['qualifiedCases']['totalDistinctTargets']==52,'qualified52 provenance unchanged')
 check(cloud['oldCorrectedCrash']==actual_cloud['oldCorrectedCrash'] and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN'and not cloud['oldCorrectedCrash']['recoveryClaim'],'old66529 UNKNOWN unchanged')
 for key,old in actual_cloud['actuals'].items():
  check(cloud['actuals'][key]==old,'historical actual unchanged '+key)
  if 'archive'in old:
   ar=R/old['archive']['path'];check(h(ar.read_bytes())==old['archive']['sha256'] and not git(R,'diff','--name-only',ACTUAL,ARGS.carrier,'--',str(ar.parent.relative_to(R))),'historical archive unchanged '+key)
 check(cloud['crashDiagnostic1']['preparationAttempts']==actual_cloud['crashDiagnostic1']['preparationAttempts'],'old compiler failure preserved')
 archive_bytes=saved_ref(ARGS.archive);check(h(archive_bytes)==ARGS.archive_sha256,'archive supplied SHA256');archive=json.loads(archive_bytes)
 check(archive['actualSources']==[ACTUAL]and archive['compiledSources']==[COMPILED],'archive actual/compiled')
 raw={};raw_refs={};ap=Path(ARGS.archive)
 for item in archive['files']:
  z=saved_ref(ap.parent/item['storedPath']);b=gzip.decompress(z)
  check((len(z),h(z),len(b),h(b))==(item['storedBytes'],item['storedSha256'],item['bytes'],item['sha256']),'archive chain '+item['storedPath'])
  check(item['rawPath']not in raw,'unique raw path');raw[item['rawPath']]=b;raw_refs[item['rawPath']]=item
 check(len(raw)==archive['rawChainCount'],'full manifest count')
 r=json.loads(raw[RECORD]);check(r['actualSource']==r['headAfter']==ACTUAL and r['compiledSource']==COMPILED and not r['statusAfter'],'record source/status')
 check(r['mode']=='diagnostic1'and r['label']==LABEL and r['expectedCases']==1 and r['categories']==[CATEGORY],'one diagnostic record')
 check(r['sourcePinDigest']==DIGEST and r['sourcePinCount']==101 and r['sourceBindingSha256']==h(actual_packet_bytes),'record source binding')
 check(r['helperSource']==(R/(REC+'t179pd-ni-iobs-l01-process-first-run.py')).read_text(),'actual controller exact')
 check(archive['nativeExits']==[r['nativeExit']],'original native result agrees')
 rp=Path(RECORD);check(raw[str(rp.parent/'source-binding.json')]==actual_packet_bytes,'original packet exact')
 ledger=(R/packet['ledger']['path']).read_bytes();check(h(ledger)==packet['ledger']['sha256']and raw[str(rp.parent/'ledger-context.json')]==ledger,'fixed ledger exact')
 prep_path=Path(release['prepRecord']);prep_bytes=prep_path.read_bytes();prep=json.loads(prep_bytes)
 check(r['prepRecord']==str(prep_path)and r['prepRecordSha256']==h(prep_bytes)==release['prepRecordSha256'],'exact accepted diagnostic prep')
 check(prep['nativeExit']==prep['actualTestsExecuted']==0 and prep['mode']=='diagnostic-plan'and prep['compiledSource']==COMPILED,'accepted zero prep')
 check(len(r['binaryBefore'])==9 and r['binaryBefore']==r['binaryAfter']==prep['binaryAfter'],'nine stable NoBuild copies')
 check(len(r['allOutputInventory'])==1575 and r['allOutputInventoryBefore']==r['allOutputInventory']==prep['allOutputInventory'],'full1575 inventory continuity')
 output_roots=[R/x/'bin/Debug/net8.0'for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
 current_paths=[str(p.relative_to(R))for p in sorted(p for root in output_roots for p in root.rglob('*')if p.is_file())]
 check(current_paths==[x['path']for x in r['allOutputInventory']],'complete current output path set')
 for item in r['allOutputInventory']:
  b=(R/item['path']).read_bytes();check((len(b),h(b))==(item['bytes'],item['sha256']),'output '+item['path'])
 check('-NoBuild'in r['argv']and '-PlanOnly'not in r['argv']and '-ValidateCatalog'not in r['argv'],'actual NoBuild command')
 check(h(raw[r['stdoutPath']])==r['stdoutSha256'],'raw stdout hash')
 launch=json.loads(raw[r['processRecord']]);check(launch['argv']==r['argv']and launch['startedUtc']==r['startedUtc'],'launch bound')
 stat=launch['identity']['stat'];launch_tick=int(stat.rsplit(')',1)[1].split()[19]);check(int(stat.split(' ',1)[0])==launch['identity']['pid']and launch_tick>0,'original launcher PID/kernel birth')
 sentinel=json.loads(raw[r['firstSentinel']]);check(sentinel['actualSource']==ACTUAL and sentinel['compiledSource']==COMPILED and sentinel['label']==LABEL and sentinel['mode']=='diagnostic1'and sentinel['startedUtc']==r['startedUtc'],'one first sentinel')
 check(len(r['resultDirectories'])==1,'one result directory');rd=Path(r['resultDirectories'][0]);check(rd.is_relative_to(R/'TestResults/test-categories'),'result directory inside immutable test artifacts')
 expected={RECORD,r['stdoutPath'],r['processRecord'],r['firstSentinel'],str(rp.parent/'source-binding.json'),str(rp.parent/'ledger-context.json'),str(rp.parent/'exact-selected-scope.json')}
 expected.update(str(p)for p in rd.rglob('*')if p.is_file())
 retained=r['namespaceAfter']['ownedRoots'];root_files={path for path in raw if any(Path(path).is_relative_to(z)for z in map(Path,retained))}
 check(set(raw)==expected|root_files,'complete original nonfixture archive coverage; retained-root bytes read from archive only')
 for path in expected:check(Path(path).read_bytes()==raw[path],'original raw equality '+path)
 check(r['namespaceBefore']=={'ownedRoots':[]},'original mutable namespace empty')
 observe(not retained,'original retained owned roots after actual',retained)
 summary=json.loads(raw[str(rd/'summary.json')]);plan=json.loads(raw[str(rd/'plan.json')])
 check(summary['ExitCode']==r['nativeExit']and summary['Revision']['Head']==ACTUAL and summary['Revision']['ChangedFiles']==0,'native summary identity')
 check(len(plan['Descriptors'])==1,'one actual descriptor');descriptor=plan['Descriptors'][0]
 check(descriptor['Category']==CATEGORY and descriptor['Adapter']=='integration'and descriptor['EstimatedCases']==1 and descriptor['ExpectedMethodCounts']==[{'Name':METHOD,'Cases':1}],'exact actual diagnostic case')
 for key in ['OwnedTreeCleanupSucceeded','RuntimeCleanupSucceeded','TimedOut']:check(type(summary[key])is bool,'typed summary '+key)
 observe(summary['OwnedTreeCleanupSucceeded']and summary['RuntimeCleanupSucceeded'],'native cleanup unsuccessful')
 log=raw[str(rd/'dotnet-test.log')].decode();transport=[('dotnet-test.log',log)];trx_results=[];trx_errors=[]
 for path,b in raw.items():
  if not path.endswith('.trx'):continue
  tree=ET.fromstring(b);ns={'t':tree.tag.split('}')[0][1:]}
  for u in tree.findall('.//t:UnitTestResult',ns):
   check(u.attrib['testName'].split('(',1)[0]==METHOD,'TRX exact diagnostic method')
   trx_results.append({'path':path,**u.attrib})
   text=u.find('t:Output/t:StdOut',ns)
   if text is not None and text.text:transport.append((path+':case:'+u.attrib.get('executionId',''),text.text))
   for e in u.findall('t:Output/t:ErrorInfo',ns):trx_errors.append({'path':path,'message':e.findtext('t:Message',default='',namespaces=ns),'stack':e.findtext('t:StackTrace',default='',namespaces=ns)})
  text=tree.find('t:ResultSummary/t:Output/t:StdOut',ns)
  if text is not None and text.text:transport.append((path+':run',text.text))
 check(len(trx_results)<=1,'no duplicate diagnostic TRX case')
 if r['nativeExit']==0:check(len(trx_results)==1 and trx_results[0]['outcome']=='Passed'and summary['Selection']['Complete']and summary['Tests']['Executed']==summary['Tests']['Passed']==1 and summary['Tests']['Failed']==0,'native0 complete single PASS')
 unique={}
 for location,text in transport:
  for row in json_lines(text,location):
   key=json.dumps(row['value'],sort_keys=True,separators=(',',':'))
   x=unique.setdefault(key,{'value':row['value'],'sources':[]});x['sources'].append({'source':location,'line':row['line']})
 rows=list(unique.values());boundaries=collections.defaultdict(list);cleanups=[]
 for row in rows:
  j=row['value'];o=j.get('Observation',j)
  if 'Boundary'in j:boundaries[j['Boundary']].append({'observation':o,'sources':row['sources']})
  if 'CleanupOwnedRoot'in j:cleanups.append({'observation':j,'sources':row['sources']})
 cuts=[]
 for row in boundaries['iobs_actual_abrupt_cut_raw_before_identity']:
  o=row['observation'];b=base64.b64decode(o['Base64'],validate=True);check(len(b)==o['Bytes']and h(b).upper()==o['Sha256'].upper(),'cut raw image hash');cuts.append(json.loads(b))
 for row in boundaries['iobs_actual_current_resume_exit']:
  cut=row['observation']['Cut']
  if cut not in cuts:cuts.append(cut)
 check(len(cuts)<=1,'one diagnostic cut identity')
 cut=cuts[0]if cuts else None
 if cut:check(cut['Cut']==1 and cut['CommonCallbacks']==0 and cut['Turn']==42 and cut['FirstMarker']is None,'exact abrupt cut1 no common')
 inventory={str(R/x['path']):x for x in r['allOutputInventory']}
 def deps(value,loaded=None):
  check(set(value['paths'])==set(value['sha256'])=={'integration','support','runtimeconfig','depsfile','immutable'},'exact child dependency closure')
  for key,path in value['paths'].items():check(path in inventory and value['sha256'][key]==inventory[path]['sha256'].upper(),'child dependency inventory '+key)
  check(value['immutableAssemblyIdentity']=='System.Collections.Immutable, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a','immutable assembly identity')
  if loaded is not None:
   check(len(loaded)==3 and {x['Path']for x in loaded}=={value['paths'][k]for k in ['integration','support','immutable']},'self loaded dependency paths')
   for item in loaded:check(item['IsDefaultContext']and item['LoadContext']=='Default'and item['Sha256']==inventory[item['Path']]['sha256'].upper(),'self loaded default assembly bytes')
 if cut:deps(cut['Dependencies'],cut['Loaded'])
 joins=[];streams=[];identities=set();child_rows=[]
 for row in boundaries['iobs_actual_native_join']:
  o=row['observation'];argv=o['Arguments'];check(len(argv)==12 and argv[6]in ['engine-joint-interface-original-crash','engine-joint-interface-resume']and argv[-2]=='1','genuine diagnostic child role/cut command')
  deps(o['Dependencies']);check(o['Executable']=='/tmp/1536-cloud-tools/dotnet/dotnet'and argv[2]==o['Dependencies']['paths']['runtimeconfig']and argv[4]==o['Dependencies']['paths']['depsfile']and argv[5]==o['Dependencies']['paths']['support']and argv[7]==o['Dependencies']['paths']['integration'],'child exact command dependency binding')
  role=argv[6];root=argv[8];own=argv[-1];check(Path(own).is_relative_to(NAMESPACE)and Path(own).name.startswith('boe-joint-interface-crash-'),'child container in fresh namespace')
  check(Path(argv[9]).is_relative_to(own),'child output contained')
  if role.endswith('resume'):check(Path(root).is_relative_to(Path(own)/'tmp'),'resume root contained')
  else:check(root==own,'producer receives own container')
  identity=(o['Id'],o['BirthTick']);check(o['Id']>0 and o['BirthTick']>0 and identity not in identities,'distinct positive child PID/kernel birth');identities.add(identity)
  for key in ['Eof','Joined']:check(type(o[key])is bool,'typed native child '+key)
  stdout=o.get('RawStdout');stderr=o.get('RawStderr');stdout_source='join.RawStdout'
  if stdout is None:
   key='Resume'if role.endswith('resume')else 'Original'
   alternatives=[x['observation'][key]for x in boundaries['iobs_actual_current_resume_exit']if (x['observation'][key]['Pid'],x['observation'][key]['BirthTick'])==identity]
   if alternatives:
    check(len(alternatives)==1 and alternatives[0]['Arguments']==argv and alternatives[0]['Executable']==o['Executable'],'fallback outcome has exact independently observed child identity/command')
    stdout=alternatives[0]['Stdout'];stdout_source='current_resume_exit.'+key+'.Stdout; join.RawStdout absent'
  parsed=json_lines(stdout or '',role+':'+str(identity));child_rows.extend(parsed)
  entry={'role':role,'pid':o['Id'],'birthTick':o['BirthTick'],'birthUtc':o['BirthUtc'],'root':root,'ownedContainer':own,'exit':o['ExitCode'],'eof':o['Eof'],'joined':o['Joined'],'primaryFailure':o['Failure'],'cleanupFailure':o['CleanupFailure'],'retainedContainer':o['RetainedOwnedContainer'],'stdoutSource':stdout_source,'stdoutBytes':None if stdout is None else len(stdout.encode()),'stdoutSha256':None if stdout is None else h(stdout.encode()),'stderr':stderr,'sources':row['sources']}
  joins.append(entry)
  if role.endswith('original-crash')and cut:check((cut['Pid'],cut['BirthTick'])==identity and Path(cut['Root']).is_relative_to(Path(own)/'tmp'),'producer independent cut identity/containment')
  if role.endswith('resume'):
   events=[z for z in parsed if z['value'].get('Boundary')=='iobs_current_resume_diagnostic'];previous_elapsed=-1;previous_utc=None;attributed=[];open_stages=[]
   for ev in events:
    v=ev['value'];check((v['Pid'],v['BirthTick'])==identity and v['Cut']==1 and v['Root']==root,'every scalar exact resumer attribution')
    if cut:check(v['Nonce']==cut['Nonce']and v['Root']==cut['Root'],'every scalar exact cut nonce/root')
    elapsed=v['ElapsedMilliseconds'];stamp=dt.datetime.fromisoformat(v['Utc'].replace('Z','+00:00'));check(type(elapsed)is int and elapsed>=0,'nonnegative monotonic timestamp type')
    observe(elapsed>=previous_elapsed,'diagnostic elapsed order reversal',{'child':identity,'stage':v['Stage'],'previous':previous_elapsed,'current':elapsed});observe(previous_utc is None or stamp>=previous_utc,'diagnostic UTC order reversal',{'child':identity,'stage':v['Stage']})
    previous_elapsed=elapsed;previous_utc=stamp;check(v['Detail']is None or isinstance(v['Detail'],str)and len(v['Detail'])<=2000,'bounded scalar detail')
    stage=v['Stage'];key=(stage[:-6],v.get('Detail')if stage.startswith('finalization-')else None)if stage.endswith('-enter')else None
    if key:open_stages.append({'key':key,'event':v})
    if stage.endswith('-return'):
     return_key=(stage[:-7],v.get('Detail')if stage.startswith('finalization-')else None)
     for index in range(len(open_stages)-1,-1,-1):
      if open_stages[index]['key']==return_key:open_stages.pop(index);break
    attributed.append({'line':ev['line'],**v})
   source=(R/'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrash.cs').read_text();source_stages=sorted(set(re.findall(r'diagnostic\.Observe\("([^"]+)"',source)))
   streams.append({'identity':{'pid':identity[0],'birthTick':identity[1]},'root':root,'nonce':cut['Nonce']if cut else None,'cutIdentityAvailable':cut is not None,'stdoutSource':stdout_source,'otherChildBoundaries':[{'line':z['line'],'value':z['value']}for z in parsed if z['value'].get('Boundary')and z['value']['Boundary']!='iobs_current_resume_diagnostic'],'events':attributed,'observedStages':list(dict.fromkeys(v['Stage']for v in attributed)),'sourceStagesNotObserved':sorted(set(source_stages)-{v['Stage']for v in attributed}),'lastEvent':attributed[-1]if attributed else None,'enteredWithoutObservedReturn':[x['event']for x in open_stages],'limit':'An unreturned observed enter is a trace boundary, not proof of a unique blocked await, deadlock or product cause. Stages not observed may be conditional or omitted by interruption.'})
 observe(len(joins)==2,'incomplete two-child join evidence',{'observed':len(joins)})
 results=[]
 for row in boundaries['iobs_actual_current_resume_exit']:
  o=row['observation'];check(cut is not None and o['Cut']==cut,'resume observation exact cut')
  for child_name,role in [('Original','engine-joint-interface-original-crash'),('Resume','engine-joint-interface-resume')]:
   child=o[child_name];matching=[x for x in joins if x['role']==role and x['pid']==child['Pid']and x['birthTick']==child['BirthTick']];check(len(matching)==1,'outcome corresponding genuine join '+child_name);join=matching[0]
   check(child['ExitCode']==join['exit']and child['Eof']==join['eof']and child['Joined']==join['joined']and h(child['Stdout'].encode())==join['stdoutSha256'],'outcome/join byte identity '+child_name)
  result=None if o['ResultRaw']is None else json.loads(o['ResultRaw'])
  if result is not None:
   deps(result['Dependencies'],result['Loaded']);resume=o['Resume'];check((result['Pid'],result['BirthTick'],result['Nonce'],result['Root'])==(resume['Pid'],resume['BirthTick'],cut['Nonce'],cut['Root']),'result self identity and nonce/root')
   images={key:plain_images(result[key])for key in ['Before','After','Repeated']}
   positive={'completed':result['Completed'],'held':result['Held'],'actualCallerEntered':result['ActualCallerEntered'],'failure':result['Failure'],'diagnosticOutputFailure':result['DiagnosticOutputFailure'],'changingI':result['NewChangingICommits'],'inertRepeat':result['InertRepeatBytesEqual'],'freshAuthorityAbsent':result['FreshAuthorityFieldsAbsentBeforeEntry'],'handled':result['Handled'],'second':result['Second'],'images':images,'logs':result['Logs'],'result':result['Result']}
   if r['nativeExit']==0:check(result['Completed']and not result['Held']and result['Failure']is None and result['DiagnosticOutputFailure']is None and result['ActualCallerEntered']and result['NewChangingICommits']==1 and result['InertRepeatBytesEqual']and result['FreshAuthorityFieldsAbsentBeforeEntry']and images['After']==images['Repeated'],'successful original strong result flags and exact repeat images')
   results.append(positive)
 observe(not parse_errors,'JSON diagnostic parse errors',parse_errors)
 before=validate_snapshot(ARGS.before,r,'before');after=validate_snapshot(ARGS.after,r,'after')
 separation_raw=saved_ref(ARGS.frozen_separation);separation=json.loads(separation_raw)
 check(separation['kind']=='READ_ONLY_FROZEN_CURRENT_CLOUD_ROOTS_SEPARATION'and separation['actualSource']==ACTUAL and separation['mode']=='diagnostic1'and separation['newMutableRoot']==NAMESPACE and separation['physicalCloudUnchanged']and separation['unchanged']and separation['verifiedFiles']==237,'saved frozen237 isolation check')
 check(separation['frozenRoots']==actual_cloud['crashDiagnostic1']['runNamespace']['frozenRootsOutsideMutableNamespace']and dt.datetime.fromisoformat(separation['checkedUtc'])<dt.datetime.fromisoformat(r['startedUtc']),'frozen roots/dated check bound')
 recovery=[];previous=ACTUAL;covered=set()
 for path in ARGS.recovery:
  rc=js(path);check(rc['base']==previous and rc['remoteShaVerified']and rc['clean']and not rc['localSourceOrObjectReuse']and rc['sourcePinsVerified']==101 and rc['sourcePinDigest']==DIGEST,'continuous recovery receipt')
  changed=git(C,'diff','--name-only',rc['base'],rc['sha']).decode().splitlines();check(set(changed)=={x['path']for x in rc['files']}and len(changed)==rc['changedPathsVerified'],'complete recovered delta')
  for item in rc['files']:
   b=git(C,'show',rc['sha']+':'+item['path']);check(b==git(R,'show',rc['sha']+':'+item['path'])and(len(b),h(b),blob(b))==(item['bytes'],item['sha256'],item['gitBlob']),'recovered path '+item['path'])
  covered.update(changed);previous=rc['sha'];recovery.append({'receipt':ref(path),'base':rc['base'],'sha':rc['sha'],'paths':len(changed)})
 check(previous==ARGS.carrier and covered==set(git(R,'diff','--name-only',ACTUAL,ARGS.carrier).decode().splitlines()),'recovery covers actual to final carrier')
 for root in (R,C):check(git(root,'rev-parse','HEAD').decode().strip()==ARGS.carrier and not git(root,'status','--porcelain'),'stable final read-only HEAD '+str(root))
 report={'kind':'INDEPENDENT_DIAGNOSTIC1_ORIGINAL_OUTCOME_READER_NOT_CAUSAL_VERDICT','checkedUtc':dt.datetime.now(dt.timezone.utc).isoformat(),'carrier':ARGS.carrier,'actualSource':ACTUAL,'compiledSource':COMPILED,'reader':ref(__file__),'recordSha256':h(raw[RECORD]),'archive':{'path':ARGS.archive,'sha256':h(archive_bytes),'chains':len(raw),'allGzipRawOriginalNonfixtureAndCloneBytesMatch':True,'retainedRootBytesFromArchiveOnly':True},'sourcePins':101,'productPaths':23,'pinDigest':DIGEST,'outputInventory':{'copies':9,'completeFiles':1575,'allCurrentBytesExact':True},'nativeExit':r['nativeExit'],'wallSeconds':r['wallSeconds'],'summary':summary,'descriptor':descriptor,'trxResults':trx_results,'directErrors':direct_errors(log)+trx_errors,'joinObservations':joins,'streamedDiagnostics':streams,'resultObservations':results,'cleanupRows':cleanups,'jsonParseErrors':parse_errors,'transportUniqueRows':len(rows),'cut':None if cut is None else {k:cut[k]for k in ['Pid','BirthTick','Nonce','Root','Cut','Phase','Index','CommonCallbacks','Turn','FirstMarker']},'censuses':{'before':before,'after':after},'frozenSeparation':{'path':ARGS.frozen_separation,'sha256':h(separation_raw),'record':separation},'recovery':recovery,'checksPassed':len(checks),'findings':findings,'qualifiedBeforeAndAfter':52,'countsToward57':False,'runtimeReleaseOrRemedy':False,'limits':['No native/test/build/discovery/controller/observer/process action or live fixture-root read; report/check files only.','Original failure/timeout is preserved independently of reader findings. No TRX or zero summary counts never proves that no case ran.','Per-child stdout attribution only; identical transport copies deduplicated with locations retained. No pooling across roots/nonces/cases.','Dated snapshots do not prove continuous absence. No old missing diagnostics reconstructed, and no automatic product-versus-fixture diagnosis or retry.','This diagnostic Fact is outside57 and cannot qualify any of the five original crash targets.']}
 dest=D/('t179pd-ni-iobs-l01-diagnostic1-outcome-independent-check-'+ARGS.carrier[:8]+'-20261018.json');check(not dest.exists(),'do not overwrite existing outcome check');dest.write_text(json.dumps(report,indent=2)+'\n')
 print(json.dumps({'report':ref(dest),'nativeExit':r['nativeExit'],'trxResults':len(trx_results),'streamedEventCounts':[len(x['events'])for x in streams],'findings':findings,'qualified':52,'verdict':'READER_ONLY_REQUIRES_INDEPENDENT_REVIEW'},indent=2))
 return 0 if not findings else 2

if __name__=='__main__':
 try:sys.exit(main())
 except Exception as exc:
  failure={'kind':'DIAGNOSTIC1_READER_FAILURE_NOT_NATIVE_RESULT','utc':dt.datetime.now(dt.timezone.utc).isoformat(),'reader':ref(__file__),'arguments':sys.argv[1:],'exception':type(exc).__name__,'message':str(exc),'completedChecks':checks,'evidenceFindings':findings,'parseErrors':parse_errors,'scope':'Preserve this utility failure before any reader correction; no runtime result or verdict inferred.'}
  stamp=dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ');dest=D/('t179pd-ni-iobs-l01-diagnostic1-outcome-reader-failure-'+stamp+'.json');dest.write_text(json.dumps(failure,indent=2)+'\n');print(json.dumps({'utilityFailure':ref(dest),'exception':failure['exception'],'message':failure['message']}),file=sys.stderr);raise
