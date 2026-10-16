from pathlib import Path
import json,gzip,hashlib,subprocess,xml.etree.ElementTree as E,collections,base64
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery'
S='e522102735317eedc04e42ea1e0b892f73983871';T='BookOfEternityClient.Tests/PendingTurnObservedOriginalReaderConformanceTests.cs'
h=lambda b:hashlib.sha256(b).hexdigest()
def git(*a):return subprocess.check_output(['git',*a],cwd=R)
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
prep=json.loads((D/'t179pd-observed-green-orf02-prep-plan-20261016-plan.json').read_text())
out={'compiled_source':S,'packets':[],'evidence_pins':[]}
for label,carrier,actual,count,failed in [('reader','05a077e1cf20ecaee92640677eaa5f0e08d0d921','109671cb955885e1bda13e467f97572cdc6fd919',1,0),('barrier','0bbc8524713ea6ce70231fc8185a91d6708d2d0e','05a077e1cf20ecaee92640677eaa5f0e08d0d921',2,0),('conformance','865be2d2a9e74473180faf3c20c2e281c7b76a71','0bbc8524713ea6ce70231fc8185a91d6708d2d0e',35,1)]:
 prefix='t179pd-observed-green-orf02-'+label+'-20261016'; category='fr063-observed-original-'+label
 def pin(p):
  b=p.read_bytes();rel=str(p.relative_to(R));assert b==git('show',carrier+':'+rel)
  x={'path':rel,'revision':carrier,'blob':git('rev-parse',carrier+':'+rel).decode().strip(),'bytes':len(b),'sha256':h(b)};out['evidence_pins'].append(x);return x
 def doc(suffix):
  p=D/(prefix+'-'+suffix);pin(p);return json.loads(p.read_text(encoding='utf-8-sig'))
 idx=doc('artifact-index.json');assert idx['source_sha']==actual and idx['native_exit_code']==failed
 chains=[]
 for a in idx['artifacts']:
  p=D/a['path'];ip=pin(p);st=p.read_bytes();raw=gzip.decompress(st) if p.suffix=='.gz' else st
  assert h(st)==a['stored_sha256'] and h(raw)==a['raw_sha256'] and len(raw)==a['raw_bytes'] and Path(a['raw_path']).read_bytes()==raw
  chains.append({**a,'blob':ip['blob'],'raw_local_stored_committed_equal':True})
 assert len(chains)==6
 plan,summary,native=[doc(s) for s in ['plan.json','summary.json','native-exit.json']]
 assert native['native_exit']==failed and native['actual_head']==actual and native['compiled_source']==S
 assert native['argv']==['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category',category,'-NoBuild']
 assert all(native[k] for k in ['binaries_unchanged','post_head_unchanged','post_git_clean']) and not any(native['new_scoped_temp_residue'].values())
 assert summary['Revision']['Head']==actual and summary['Revision']['ChangedFiles']==0 and summary['ExitCode']==failed
 assert summary['Tests']==dict(Total=count,Executed=count,Passed=count-failed,Failed=failed)
 assert summary['Selection']['Complete'] and summary['Selection']['PlannedCases']==summary['Selection']['CompletedCases']==count
 assert summary['Selection']['PlannedDescriptors']==summary['Selection']['CompletedDescriptors']==1 and not summary['Selection']['UnderfilledDescriptors']
 assert not summary['DuplicateTests'] and not summary['TimedOut'] and summary['OwnedTreeCleanupSucceeded'] and summary['RuntimeCleanupSucceeded']
 assert summary['CategoryResults'][0]['Seconds']<summary['CategoryResults'][0]['BudgetMinutes']*60
 assert len(plan['Descriptors'])==1
 prior=next(a for a in prep['Descriptors'] if a['Category']==category)
 assert {k:v for k,v in plan['Descriptors'][0].items() if k!='Name'}=={k:v for k,v in prior.items() if k!='Name'}
 assert plan['Descriptors'][0]['Name']==category+'-unit-001'
 assert not git('diff','--name-only',S,actual,'--','BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts')
 assert not git('diff','--name-only',actual,carrier,'--','BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts')
 d=plan['Descriptors'][0];trx=E.fromstring(gzip.decompress((D/(prefix+'-'+d['Name']+'.trx.gz')).read_bytes()))
 defs={u.attrib['id']:u.find('t:TestMethod',ns).attrib['className'].split(',')[0]+'.'+u.find('t:TestMethod',ns).attrib['name'] for u in trx.findall('t:TestDefinitions/t:UnitTest',ns)}
 counters=trx.find('t:ResultSummary/t:Counters',ns).attrib
 for k,v in counters.items():assert int(v)==({'total':count,'executed':count,'passed':count-failed,'failed':failed}.get(k,0)),(k,v)
 rows=trx.findall('t:Results/t:UnitTestResult',ns);assert len(rows)==count and len({r.attrib['testId'] for r in rows})==count and len({r.attrib['testName'] for r in rows})==count
 methods=collections.Counter(defs[r.attrib['testId']] for r in rows);assert dict(methods)=={m['Name']:m['Cases'] for m in d['ExpectedMethodCounts']}
 cases=[];roots=[]
 for r in rows:
  diagnostics=[]
  for line in (r.findtext('t:Output/t:StdOut','',ns)).splitlines():
   try:q=json.loads(line)
   except ValueError:continue
   if isinstance(q,dict):diagnostics.append(q)
  clean=[q for q in diagnostics if 'CleanupOwnedRoot' in q];assert len(clean)==1 and clean[0]['OwnedFixtureRemoved'] and clean[0]['CleanupFailure'] is None
  assert not Path(clean[0]['CleanupOwnedRoot']).exists();roots.append(clean[0]['CleanupOwnedRoot'])
  error=r.findtext('t:Output/t:ErrorInfo/t:Message',None,ns);stack=r.findtext('t:Output/t:ErrorInfo/t:StackTrace',None,ns)
  if r.attrib['outcome']=='Failed':
   assert label=='conformance' and r.attrib['testName'].endswith('(failure: "manifest")')
   assert 'JsonReaderException' in error and 'PreflightBrowserOriginalAdmission' in stack and 'MainAdmission.AcquireAsync' in stack and 'cs:line 273' in stack
   assert 'PendingTurnSnapshotReader.ReadCurrent' not in stack and 'PendingTurnSnapshotReader.ReadObservedCurrent' not in stack
  else:assert r.attrib['outcome']=='Passed' and error is None
  if label=='reader':
   q=next(q for q in diagnostics if q.get('GenuineOriginalControl'));assert q['SessionId']=='observed-original-session-71' and q['RequestId']=='observed-original-request-71' and q['TurnNumber']==71 and q['Generation'] and q['SnapshotToken'] and q['ReadOnlyTree']
   assert base64.b64decode(q['OriginalBytes'])!=base64.b64decode(q['LiveBytes'])
  if label=='barrier':
   q=next(q for q in diagnostics if q.get('GenuineBarrierControl'));assert q['InjectionCount']==1 and q['ReadOrder'][:2]==['A','B'] and q['OnlyFirstSnapshotChanged'] and q['SameGeneration'] and q['Generation'] and q['ManifestPayloadHash']
   assert base64.b64decode(q['ChangedFirst'])==base64.b64decode(q['OriginalFirst'])+b' '
   if 'ReadObservedCurrent_' in defs[r.attrib['testId']]:assert q['ApiPresent'] and q['InvokedApi']=='ReadObservedCurrent' and not q['Success'] and not q['SnapshotReturned'] and not q['EvidenceReturned']
   else:assert q['InvokedApi']=='ReadCurrent' and q['Success'] and q['SnapshotReturned'] and not q['EvidenceReturned']
  cases.append(dict(id=r.attrib['testId'],name=r.attrib['testName'],method=defs[r.attrib['testId']],outcome=r.attrib['outcome'],diagnostics=diagnostics,error=error,stack=stack))
 assert len(set(roots))==count
 out['packets'].append(dict(category=category,carrier=carrier,actual=actual,summary=summary,native=native,chains=chains,cases=cases,strict_absent_roots=roots,exact_preparation_descriptor=d))
assert len({r for p in out['packets'] for r in p['strict_absent_roots']})==38
out['notes']=['Historical e522 DLL identity is preserved in its immutable manifest/native receipts; current fresh binaries are checked separately.','Conformance packet is failed/STOP, not accepted GREEN. Reader/barrier passing evidence is not relabeled as d50 execution.']
p=Path('/tmp/t179pd-observed-admission-fixture-history-independent.json');assert not p.exists();p.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'output':str(p),'sha256':h(p.read_bytes()),'chains':18,'cases':38,'passed':37,'failed':1,'strict_roots':38}))
