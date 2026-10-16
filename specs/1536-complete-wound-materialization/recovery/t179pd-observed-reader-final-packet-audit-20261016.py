from pathlib import Path
import sys,json,gzip,hashlib,subprocess,collections,xml.etree.ElementTree as ET
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery';H=lambda b:hashlib.sha256(b).hexdigest();G=lambda *a:subprocess.check_output(['git',*a],cwd=R)
late,default,ordinary=sys.argv[1:];current='8c098487398830c20bd3705261c2a2c507bce5d8';e522='e522102735317eedc04e42ea1e0b892f73983871';d50='d50d1a9a98e087c8fc5a8908745dfd1e03aaac48'
packets=[('fr063-joint-late-completion','t179pd-observed-green-default-fixture-first-late-20261016',late,current),('fr063-observed-original-reader','t179pd-observed-green-orf02-reader-20261016','05a077e1cf20ecaee92640677eaa5f0e08d0d921',e522),('fr063-observed-original-barrier','t179pd-observed-green-orf02-barrier-20261016','0bbc8524713ea6ce70231fc8185a91d6708d2d0e',e522),('fr063-observed-original-conformance','t179pd-observed-green-admission-fixture-conformance-20261016','54e07796d5fca6bac5940b447ef65379727f2247',d50),('fr063-observed-original-browser','t179pd-observed-green-admission-fixture-browser-20261016','da3b8085845fd8cdebba7be7adce9e5416f998ac',d50),('fr063-observed-original-default','t179pd-observed-green-default-fixture-default-20261016',default,current),('portable-ordinary-readers','t179pd-observed-green-default-fixture-ordinary-20261016',ordinary,current)]
prep=json.loads((D/'t179pd-observed-green-default-fixture-prep-plan-20261016-plan.json').read_text(encoding='utf-8-sig'));wanted=collections.Counter({m['Name']:m['Cases'] for x in prep['Descriptors'] for m in x['ExpectedMethodCounts']});assert len(wanted)==59 and sum(wanted.values())==131
allmethods=collections.Counter();ids=set();roots=[];case_rows=[];reports=[];totalchains=0
for category,prefix,carrier,compiled in packets:
 def raw(name):
  path=D/name;b=path.read_bytes();assert G('show',carrier+':'+str(path.relative_to(R)))==b;return b
 def doc(suffix):return json.loads(raw(prefix+'-'+suffix).decode('utf-8-sig'))
 idx=doc('artifact-index.json');chains=[]
 for a in idx['artifacts']:
  b=raw(a['path']);v=gzip.decompress(b) if a['path'].endswith('.gz') else b;assert H(b)==a['stored_sha256'] and H(v)==a['raw_sha256'] and len(v)==a['raw_bytes'] and Path(a['raw_path']).read_bytes()==v;chains.append(a)
 totalchains+=len(chains);plan,summary,native=doc('plan.json'),doc('summary.json'),doc('native-exit.json');actual=native['actual_head'];assert native['compiled_source']==compiled and idx['source_sha']==actual and idx['native_exit_code']==native['native_exit']==0
 assert native['argv']==['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category',category,'-NoBuild'] and native['category']==category
 assert all(native[k] is True for k in ['binaries_unchanged','post_head_unchanged','post_git_clean']) and not any(native['new_scoped_temp_residue'].values())
 assert summary['Categories']==plan['Categories']==[category] and summary['Revision']['Head']==plan['Revision']['Head']==actual and summary['Revision']['ChangedFiles']==plan['Revision']['ChangedFiles']==0
 assert summary['ExitCode']==0 and not summary['TimedOut'] and not summary['DuplicateTests'] and summary['OwnedTreeCleanupSucceeded'] and summary['RuntimeCleanupSucceeded'] and summary['Selection']['Complete'] and not summary['Selection']['UnderfilledDescriptors']
 expected=[p for p in prep['Descriptors']if p['Category']==category];assert len(expected)==len(plan['Descriptors']);packet_cases=[];packet_roots=[]
 for descriptor in plan['Descriptors']:
  peer=[p for p in expected if p['Project']==descriptor['Project']];assert len(peer)==1
  for k in ['Project','Category','Categories','Filter','Adapter','Exclusive','FrontendBuild','BudgetMinutes','EstimatedCases','ExpectedMethodCounts']:assert descriptor[k]==peer[0][k]
  x=ET.fromstring(gzip.decompress(raw(prefix+'-'+descriptor['Name']+'.trx.gz')));ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};defs={}
  for u in x.findall('t:TestDefinitions/t:UnitTest',ns):
   m=u.find('t:TestMethod',ns);defs[u.attrib['id']]=m.attrib['className'].split(',')[0]+'.'+m.attrib['name']
  methods=collections.Counter();results=x.findall('t:Results/t:UnitTestResult',ns);c=x.find('t:ResultSummary/t:Counters',ns).attrib
  assert all(int(v)==(len(results)if k in ['total','executed','passed']else 0)for k,v in c.items())
  for result in results:
   testid=result.attrib['testId'];assert testid not in ids and result.attrib['outcome']=='Passed';ids.add(testid);method=defs[testid];methods[method]+=1;clean=[]
   for line in (result.findtext('t:Output/t:StdOut',namespaces=ns)or'').splitlines():
    try:v=json.loads(line)
    except ValueError:continue
    if isinstance(v,dict)and'CleanupOwnedRoot'in v:
     assert v['OwnedFixtureRemoved'] and v['CleanupFailure'] is None and not Path(v['CleanupOwnedRoot']).exists();clean.append(v);packet_roots.append(v['CleanupOwnedRoot'])
   strict=category not in ['fr063-observed-original-default','portable-ordinary-readers']or method.startswith('BookOfEternityClient.Tests.PortableOrdinaryReadTests.')
   if strict:assert len(clean)==1
   row={'category':category,'method':method,'name':result.attrib['testName'],'testId':testid,'outcome':'Passed','carrier':carrier,'actual_head':actual,'compiled_source':compiled,'strict_cleanup':len(clean)};packet_cases.append(row);case_rows.append(row)
  assert methods==collections.Counter({m['Name']:m['Cases']for m in descriptor['ExpectedMethodCounts']}) and len(results)==descriptor['EstimatedCases'];allmethods.update(methods)
 n=len(packet_cases);assert summary['Tests']==dict(Total=n,Executed=n,Passed=n,Failed=0) and summary['Selection']['PlannedCases']==summary['Selection']['CompletedCases']==n and summary['Selection']['PlannedDescriptors']==summary['Selection']['CompletedDescriptors']==len(plan['Descriptors'])
 assert all(c['Completed']and c['Seconds']<=c['BudgetMinutes']*60 for c in summary['CategoryResults']);roots+=packet_roots
 reports.append({'category':category,'prefix':prefix,'carrier':carrier,'actual_head':actual,'compiled_source':compiled,'cases':n,'chains':len(chains),'strict_roots':len(packet_roots),'wall':summary['WallTime'],'index_sha256':H(raw(prefix+'-artifact-index.json'))})
assert allmethods==wanted and len(ids)==131 and len(roots)==len(set(roots))==63 and totalchains==43
out={'scope':'Bounded unactivated observed-original reader/storage integration ONLY; independent final review still required','all_checks_passed':True,'distinct_methods':59,'distinct_cases':131,'passed':131,'artifact_chains':43,'strict_absent_roots':63,'narrow_cleanup_cases':68,'historical_failed_packets':'immutable failed history; excluded from selected successful union','packets':reports,'cases':case_rows,'all_roots':roots,'limits':'Not full Pd/source admission/S/C/B/M3/P0/F1/T197, natural alias, legacy dispatch, actual DOM visual or Windows qualification. No new joint QTE/public activation. D4/guidedUS5/D8 remain.'}
p=Path('/tmp/t179pd-observed-reader-final-packet-audit-20261016.json');assert not p.exists();p.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:out[k]for k in ['all_checks_passed','distinct_methods','distinct_cases','artifact_chains','strict_absent_roots','narrow_cleanup_cases']}));print(H(p.read_bytes()))
