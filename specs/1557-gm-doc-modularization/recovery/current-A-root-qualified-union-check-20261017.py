from pathlib import Path
import json,hashlib,xml.etree.ElementTree as E,collections,datetime,re
repo=Path('/workspace/The-Book-of-Eternity-Reborn-1557')
plan_records=[]
for rp in Path('/tmp/gm1557-implementation-tools').glob('*/record.json'):
 rd=json.loads(rp.read_text())
 if rd['mode']=='plan' and rd['nativeExit']==0:plan_records.append(rd)
latest_plan_record=max(plan_records,key=lambda rd:rd['startedUtc'])
original_plan=Path(latest_plan_record['resultDirectories'][0])/'plan.json'
expected=collections.Counter()
for row in json.loads(original_plan.read_text())['Descriptors']:
 for m in row['ExpectedMethodCounts']:expected[m['Name']]+=m['Cases']
records=[]
for p in Path('/tmp/gm1557-implementation-tools').glob('*/record.json'):
 d=json.loads(p.read_text())
 if d['mode']=='actual':records.append((d['startedUtc'],p,d))
history=collections.defaultdict(list);rows=[];failures=[]
for _,p,record in sorted(records):
 category=record['argv'][-2];assert record['argv'][-3]=='-Category' and record['argv'][-1]=='-NoBuild'
 assert record['binaryBefore']==record['binaryAfter'] and len(record['binaryAfter'])==7
 assert not record['statusAfter'] and record['headAfter']==record['sourceHead']
 assert len(record['resultDirectories'])==1;directory=Path(record['resultDirectories'][0]);summary=json.loads((directory/'summary.json').read_text())
 assert not summary['PlanOnly'] and not summary['DiscoveryOnly'];assert not summary['TimedOut'];assert summary['OwnedTreeCleanupSucceeded'] and summary['RuntimeCleanupSucceeded'];assert summary['Selection']['Complete'] or (record['label']=='gm1557-A-body-first-5d2c9e61' and summary['Selection']['CompletedDescriptors']==2 and summary['Selection']['PlannedDescriptors']==3 and summary['Selection']['CompletedCases']==120 and summary['Selection']['PlannedCases']==122);assert not summary['DuplicateTests'];counts=collections.Counter();results=[];trx_pins=[]
 for trx in sorted(directory.glob('*.trx')):
  b=trx.read_bytes();root=E.fromstring(b);ns={'t':root.tag[1:].split('}')[0]};methods={}
  for unit in root.findall('t:TestDefinitions/t:UnitTest',ns):
   m=unit.find('t:TestMethod',ns);methods[unit.attrib['id']]=m.attrib['className']+'.'+m.attrib['name']
  local=root.findall('t:Results/t:UnitTestResult',ns)
  for result in local:
   method=methods[result.attrib['testId']];assert method in expected
   display=result.attrib['testName'];kind_match=re.search(r'kind: "([^"]+)"',display);topics_match=re.search(r'topics: "([^"]+)"',display)
   # Explicit three-row method split preserves original/topics/kind lineage; normalize only these repair rows.
   repair_row='GameEngineTurnLifecycleTests.GmDocumentationRepair' in method and kind_match is not None and topics_match is not None
   key=(method.rsplit('.',1)[0]+'.RepairSemanticRow',kind_match[1]+'|'+topics_match[1]) if repair_row else (method,display)
   outcome=result.attrib['outcome'];assert outcome in {'Passed','Failed'}
   item={'method':method,'outcome':outcome,'source':record['sourceHead'],'compiledSource':record['compiledSource'],'category':category,'record':str(p)}
   history[key].append(item);counts[outcome]+=1;results.append(key)
   if outcome=='Failed':
    message=result.findtext('t:Output/t:ErrorInfo/t:Message',namespaces=ns)
    source_failure=record['label']=='gm1557-current-A-source-guards-first-1a48d0c2' and method.endswith('.FinalDaemonSourceKeepsScopedDirectionsAtRealDispatch') and message=='Assert.Empty() Failure: Collection was not empty\nCollection: ["source.bootstrap_source_exception"]'
    body_failure=record['label']=='gm1557-A-body-first-5d2c9e61' and method.rsplit('.',1)[1] in {'PrimaryAfterlifeAcceptedExamplesUseFullActorBrainAndOwnedMemorySurfaces','SpiritualSourceOnly_DocumentsNoDecisionAndOnePublisher','SpiritualContinuationFileHelper_DocumentsExplicitCommandsAndPublicationWitnesses','SarefMainStoryE2EAuditMatrixCoversIssue692StagesAndEvidence','AfterlifeActorResourceMaterialization_UsesTheActorEnvelopeWithoutLegacyRefs'} and message.startswith('Assert.Contains() Failure: Sub-string not found')
    repair_failure=record['label']=='gm1557-A-repair-first-59ac4bd7' and method.endswith('.GmDocumentationRepairPacketsResolveActualBoundedBodies') and kind_match is not None and kind_match[1] in {'faction_materialization_repair','actor_materialization_repair','actor_memory_persistence_repair'} and message.startswith(('Assert.Equal() Failure: Collections differ','Assert.All() Failure:'))
    lifetime_failure=record['label']=='gm1557-A-lifetime-first-b821b685' and method.endswith('.ControlledOwnedOrphanTimeoutKeepsConcurrentPipesAndStrictCleanup') and message=='Assert.Empty() Failure: Collection was not empty\nCollection: ["delivery.cleanup_failed"]'
    assert source_failure or body_failure or repair_failure or lifetime_failure,(record['label'],method,message);failures.append({'method':method,'message':message,**item})
  counters=root.find('t:ResultSummary/t:Counters',ns).attrib;assert int(counters['total'])==int(counters['executed'])==len(local)
  trx_pins.append({'path':str(trx),'sha256':hashlib.sha256(b).hexdigest(),'cases':len(local)})
 assert len(results)==len(set(results));assert summary['Tests']=={'Total':len(results),'Executed':len(results),'Passed':counts['Passed'],'Failed':counts['Failed']};assert summary['Selection']['CompletedCases']==len(results)
 if summary['Selection']['Complete']:assert summary['Selection']['PlannedCases']==len(results)
 assert record['nativeExit']==summary['ExitCode']==(1 if counts['Failed'] else 0)
 rows.append({'category':category,'sourceHead':record['sourceHead'],'compiledSource':record['compiledSource'],'native':record['nativeExit'],'passed':counts['Passed'],'failed':counts['Failed'],'cases':len(results),'descriptors':len(trx_pins),'wallSeconds':record['wallSeconds'],'recordSha256':hashlib.sha256(p.read_bytes()).hexdigest(),'trx':trx_pins})
latest=collections.Counter();unresolved=[]
for key,items in history.items():
 if items[-1]['outcome']=='Passed':latest[items[-1]['method']]+=1
 else:unresolved.append({'method':items[-1]['method'],'case':key[1],**items[-1]})
assert not (latest-expected)
complete=latest==expected and not unresolved
out={'kind':'ROOT_QUALIFIED_UNION_RAW_OBSERVATION_NOT_INDEPENDENT_EVIDENCE_VERDICT','checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'actualCommands':len(rows),'actualExecuted':sum(r['cases'] for r in rows),'historicalFailed':failures,'latestUniquePassedCases':sum(latest.values()),'latestUniqueMethods':len(latest),'plannedUniqueCases':sum(expected.values()),'completeUniqueSelection':complete,'supersededExecutions':sum(len(v)-1 for v in history.values()),'repairRowIdentityRule':'Normalize only explicit GameEngine GmDocumentationRepair split by unchanged kind+topics; source ledger/reviewer binds original method tuple; original raw method/display retained in records','unresolved':unresolved,'rows':rows}
p=Path('/tmp/gm1557-root-qualified-union-raw-observation-20261017.json');p.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps({k:v for k,v in out.items() if k not in {'rows','historicalFailed','unresolved'}}));print('ROWS',[(r['category'],r['passed'],r['failed'],round(r['wallSeconds'],3)) for r in rows])
