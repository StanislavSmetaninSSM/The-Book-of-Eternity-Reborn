from pathlib import Path
import json,gzip,hashlib,re,subprocess,collections,xml.etree.ElementTree as E
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery';T=Path('/tmp');C='ad5859121253d7972dca51ced6f77927e9e953b4';S='d50d1a9a98e087c8fc5a8908745dfd1e03aaac48';OLD='e522102735317eedc04e42ea1e0b892f73983871'
h=lambda b:hashlib.sha256(b).hexdigest()
def git(*a):return subprocess.check_output(['git',*a],cwd=R)
def pin(path,rev=C):
 b=git('show',rev+':'+path);assert b==(R/path).read_bytes()
 return {'path':path,'revision':rev,'blob':git('rev-parse',rev+':'+path).decode().strip(),'bytes':len(b),'sha256':h(b)}
planpath='specs/1536-complete-wound-materialization/recovery/t179pd-observed-green-admission-fixture-prep-plan-20261016-plan.json';plan=json.loads((R/planpath).read_text());assert len(plan['Descriptors'])==8
notes={int(a[0]):a[1:] for a in (line.split('\t') for line in (T/'t179pd-selected-negative-fixture-admission-audit-rows-20261016.tsv').read_text().splitlines())};assert set(notes)==set(range(1,60))
audit=[];files=set();i=0;cache={}
for d in plan['Descriptors']:
 root=str(Path(d['Project']).parent)
 if root not in cache:cache[root]=[(p,(R/p).read_text()) for p in git('ls-files',root+'/*.cs').decode().splitlines()]
 for m in d['ExpectedMethodCounts']:
  i+=1;short=m['Name'].split('.')[-1];matches=[]
  for p,s in cache[root]:
   match=re.search(r'\bpublic\s+(?:(?:async|static)\s+)*(?:Task(?:<[^\n]+>)?|void|ValueTask)\s+'+re.escape(short)+r'\s*\(',s)
   if match:matches.append((p,s[:match.start()].count('\n')+1))
  assert len(matches)==1,(short,matches);p,line=matches[0];files.add(p)
  mutation,ordering,oracle,decision=notes[i]
  audit.append(dict(row=i,method=m['Name'],category=d['Category'],project=d['Project'],descriptor=d['Name'],cases=m['Cases'],file=p,line=line,mutations_or_controls=mutation,admission_order=ordering,intended_oracle=oracle,assessment=decision,executed_cases=[]))
assert len(audit)==len({a['method'] for a in audit})==59 and sum(a['cases'] for a in audit)==131
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};byname={a['method']:a for a in audit};ep=[];packets=[];chains=[]
configs=[('t179pd-observed-green-admission-fixture-first-late-20261016','fr063-joint-late-completion',2,0,False),('t179pd-observed-green-orf02-reader-20261016','fr063-observed-original-reader',1,0,False),('t179pd-observed-green-orf02-barrier-20261016','fr063-observed-original-barrier',2,0,False),('t179pd-observed-green-admission-fixture-conformance-20261016','fr063-observed-original-conformance',35,0,False),('t179pd-observed-green-admission-fixture-browser-20261016','fr063-observed-original-browser',8,0,False),('t179pd-observed-green-admission-fixture-default-20261016','fr063-observed-original-default',67,1,False),('t179pd-observed-green-orf02-conformance-20261016','fr063-observed-original-conformance',35,1,True)]
for prefix,cat,count,fail,historical in configs:
 def doc(suf):
  p=D/(prefix+'-'+suf);ep.append(pin(str(p.relative_to(R))));return json.loads(p.read_text(encoding='utf-8-sig'))
 index=doc('artifact-index.json');actual=index['source_sha'];assert index['native_exit_code']==fail
 for a in index['artifacts']:
  p=D/a['path'];b=p.read_bytes();raw=gzip.decompress(b) if p.suffix=='.gz' else b
  assert h(b)==a['stored_sha256'] and h(raw)==a['raw_sha256'] and len(raw)==a['raw_bytes'] and raw==Path(a['raw_path']).read_bytes();ip=pin(str(p.relative_to(R)));ep.append(ip);chains.append(dict(prefix=prefix,**a,blob=ip['blob']))
 p,s,n=[doc(a) for a in ['plan.json','summary.json','native-exit.json']]
 assert n['actual_head']==actual and n['native_exit']==fail and n['compiled_source'] in [S,OLD] and n['binaries_unchanged'] and n['post_head_unchanged'] and n['post_git_clean']
 assert n['argv']==['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category',cat,'-NoBuild']
 assert not any(n['new_scoped_temp_residue'].values()) and s['Revision']['Head']==actual and s['Revision']['ChangedFiles']==0
 assert s['Tests']==dict(Total=count,Executed=count,Passed=count-fail,Failed=fail) and not s['DuplicateTests'] and not s['TimedOut'] and s['Selection']['Complete'] and not s['Selection']['UnderfilledDescriptors']
 assert s['RuntimeCleanupSucceeded'] and s['OwnedTreeCleanupSucceeded'] and len(p['Descriptors'])==1
 desc=p['Descriptors'][0];expected=next(d for d in plan['Descriptors'] if d['Category']==cat)
 assert {k:v for k,v in desc.items() if k!='Name'}=={k:v for k,v in expected.items() if k!='Name'}
 rawtrx=gzip.decompress((D/(prefix+'-'+desc['Name']+'.trx.gz')).read_bytes());xml=E.fromstring(rawtrx)
 defs={u.attrib['id']:u.find('t:TestMethod',ns).attrib['className'].split(',')[0]+'.'+u.find('t:TestMethod',ns).attrib['name'] for u in xml.findall('t:TestDefinitions/t:UnitTest',ns)}
 rs=xml.findall('t:Results/t:UnitTestResult',ns);assert len(rs)==count and len({r.attrib['testId'] for r in rs})==count
 counters=xml.find('t:ResultSummary/t:Counters',ns).attrib
 for k,v in counters.items():assert int(v)==dict(total=count,executed=count,passed=count-fail,failed=fail).get(k,0)
 cnt=collections.Counter();failures=[];roots=[];cases=[]
 for r in rs:
  name=defs[r.attrib['testId']];cnt[name]+=1;msg=r.findtext('t:Output/t:ErrorInfo/t:Message',None,ns);stack=r.findtext('t:Output/t:ErrorInfo/t:StackTrace',None,ns)
  diagnostics=[]
  for line in r.findtext('t:Output/t:StdOut','',ns).splitlines():
   try:q=json.loads(line)
   except ValueError:continue
   if isinstance(q,dict) and 'CleanupOwnedRoot' in q:
    assert q['OwnedFixtureRemoved'] and q['CleanupFailure'] is None and not Path(q['CleanupOwnedRoot']).exists();roots.append(q['CleanupOwnedRoot']);diagnostics.append(q)
  if cat!='fr063-observed-original-default':assert len(diagnostics)==1
  if r.attrib['outcome']=='Failed':
   assert fail==1 and 'PreflightBrowserOriginalAdmission' in stack and 'MainAdmission.AcquireAsync' in stack
   assert ('duplicate JSON property' in msg and "'$.files'" in msg and 'cs:line 339' in stack) if not historical else ('JsonReaderException' in msg and 'cs:line 273' in stack)
   failures.append(dict(name=r.attrib['testName'],message=msg,stack=stack))
  else:assert r.attrib['outcome']=='Passed'
  row=dict(id=r.attrib['testId'],test_name=r.attrib['testName'],method=name,outcome=r.attrib['outcome'],packet=prefix,actual=actual,compiled_source=n['compiled_source'],cleanup=diagnostics)
  cases.append(row)
  if not historical:byname[name]['executed_cases'].append(row)
 assert dict(cnt)=={m['Name']:m['Cases'] for m in desc['ExpectedMethodCounts']};assert len(failures)==fail and len(roots)==len(set(roots))
 packets.append(dict(prefix=prefix,actual=actual,compiled_source=n['compiled_source'],historical_failed_packet=historical,tests=s['Tests'],wall=s['WallTime'],native=n,summary=s,failures=failures,strict_root_count=len(roots),case_rows=cases))
assert len(chains)==42
for a in audit:
 if a['category']=='portable-ordinary-readers':assert not a['executed_cases'];a['runtime_support']='UNRUN; static boundary audit only'
 else:
  assert len(a['executed_cases'])==a['cases'];a['runtime_support']='ACTUAL '+str(sum(c['outcome']=='Passed' for c in a['executed_cases']))+' PASS / '+str(sum(c['outcome']=='Failed' for c in a['executed_cases']))+' FAIL; source reasoning remains independent'
assert sum(len(a['executed_cases']) for a in audit)==115
assert sum(c['outcome']=='Failed' for a in audit for c in a['executed_cases'])==1
sourcepaths=sorted(files|{'BookOfEternityClient/Core/FileSystemManager.cs','BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs','BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs','BookOfEternityClient/Core/StrictJsonAuthority.cs','BookOfEternityClient/Core/FileSystemManager.OrdinaryReads.cs','BookOfEternityClient/Services/PendingTurnSnapshotReader.cs','BookOfEternityClient/Services/PendingTurnSnapshotReader.Observed.cs','BookOfEternityClient/Services/PendingTurnSnapshotAuthority.cs','BookOfEternityClient/Services/LiveTurnPreparationService.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.BrowserInputRecovery.cs','BookOfEternityClient.TestSupport/TrustedLocalPublicationCrashFixture.cs','tests/fixtures/PortableStorageCrashHost/Program.cs','tests/categories.json','tests/selection.json','AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md'})
sp=[pin(p) for p in sourcepaths];ep.append(pin(planpath))
manifest='specs/1536-complete-wound-materialization/recovery/t179pd-observed-green-admission-fixture-prep-binaries-20261016.json';bi=json.loads((R/manifest).read_text());assert bi['source_sha']==S
for a in bi['binaries']:assert h((R/a['path']).read_bytes())==a['sha256']
ep.append(pin(manifest));scope=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityClient.TestSupport','tests','scripts']
diffs=[]
for label,a,b,paths in [('source-to-current',S,C,scope),('resolved-conformance-fixture',OLD,S,['BookOfEternityClient.Tests/PendingTurnObservedOriginalReaderConformanceTests.cs']),('default-fixture-unchanged',OLD,S,['BookOfEternityClient.Tests/PendingTurnSnapshotReaderTests.cs'])]:
 argv=['git','diff','--no-ext-diff','--binary','--full-index',a,b,'--',*paths];raw=subprocess.check_output(argv,cwd=R);p=T/('t179pd-selected-negative-fixture-admission-audit-'+label+'-20261016.diff');assert not p.exists();p.write_bytes(raw);diffs.append(dict(argv=argv,path=str(p),bytes=len(raw),sha256=h(raw)))
assert diffs[0]['bytes']==diffs[2]['bytes']==0
out=dict(candidate=C,compiled_source=S,previous_source=OLD,plan_pin=pin(planpath),selected_categories=plan['Categories'],descriptors=plan['Descriptors'],selected_methods=59,selected_cases=131,executed_current_case_coverage=115,passed_current_cases=114,failed_current_cases=1,unrun_ordinary_cases=16,audit_rows=audit,source_pins=sp,evidence_pins=list({a['path']:a for a in ep}.values()),raw_artifact_chains=chains,packets=packets,binaries=bi,diffs=diffs,scope_notes=['59 methods only; no blanket inference from PASS','Prior e522 failed conformance35 preserved separately and excluded from current115 count','No runtime, build, discovery, network, or repository mutation by reviewer'])
p=T/'t179pd-selected-negative-fixture-admission-audit-inspection-20261016.json';assert not p.exists();p.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(dict(path=str(p),sha256=h(p.read_bytes()),source_pins=len(sp),evidence_pins=len(out['evidence_pins']),chains=len(chains),methods=len(audit),cases=131,reviewed_test_files=len(files),diffs=diffs),indent=2))
