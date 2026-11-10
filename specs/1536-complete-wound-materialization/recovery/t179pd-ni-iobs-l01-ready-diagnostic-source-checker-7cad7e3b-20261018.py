"""Independent static SOURCE and evidence readback; never executes reviewed controllers or product."""
from pathlib import Path
import ast,datetime,fnmatch,hashlib,json,os,stat,subprocess,sys
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
HEAD=sys.argv[1];BASE='40920dab235f48abc07988b231a8bf0481c646bd';FIRST_CANDIDATE='8e1fedfecf2b4799502034f539330c8c3eba43f9'
PACKET=REC+'t179pd-ni-iobs-l01-source-packet-20261018.json';PRODUCT='BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs';PREP=REC+'t179pd-ni-iobs-l01-process-preparation-run.py';FIRST=REC+'t179pd-ni-iobs-l01-process-first-run.py'
def h(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root,env={**os.environ,'GIT_OPTIONAL_LOCKS':'0'})
def frozen(p,rev=HEAD):return git(R,'show',rev+':'+p)
def tree(root,rev):
 out={}
 for line in git(root,'ls-tree','-rz',rev).split(b'\0'):
  if line:
   m,p=line.split(b'\t',1);out[p.decode()]=tuple(m.decode().split())
 return out
def assignment(module,name):return next(n.value for n in ast.walk(module) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id==name for t in n.targets))
for root in (R,C):
 assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain')
 assert not (root/'.git/objects/info/alternates').exists()
remote=git(C,'ls-remote','origin','refs/heads/1536-wound-b2-cloud-20261012').decode().strip();assert remote==HEAD+'\trefs/heads/1536-wound-b2-cloud-20261012'
rt=tree(R,HEAD);ct=tree(C,HEAD);assert rt==ct
p=json.loads(frozen(PACKET));oldraw=frozen(PACKET,BASE);old=json.loads(oldraw);assert p['sourceBase']==BASE and len(p['sourcePins'])==p['sourcePinCount']==98
assert len({x['path']for x in p['sourcePins']})==98 and h(json.dumps(p['sourcePins'],sort_keys=True,separators=(',',':')).encode())==p['pinDigest']
oldpins={x['path']:x for x in old['sourcePins']};assert set(oldpins)=={x['path']for x in p['sourcePins']};changedpins=[]
for pin in p['sourcePins']:
 b=frozen(pin['path']);assert b==(R/pin['path']).read_bytes()==(C/pin['path']).read_bytes() and (len(b),h(b),blob(b))==(pin['bytes'],pin['sha256'],pin['gitBlob']) and rt[pin['path']]==ct[pin['path']]==(pin['mode'],'blob',pin['gitBlob'])
 if any(pin[k]!=oldpins[pin['path']][k] for k in ('bytes','sha256','gitBlob','mode')):changedpins.append(pin['path'])
assert set(changedpins)=={PRODUCT,PREP,FIRST}
hist=p['historicalPacket'];assert frozen(hist['path'])==oldraw and h(oldraw)==hist['sha256'] and len(oldraw)==hist['bytes']
assert h(frozen(p['ledger']['path']))==p['ledger']['sha256']==old['ledger']['sha256']
productdiff=git(R,'diff','--name-only',p['productBase'],HEAD,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines();assert productdiff==p['productDelta'] and len(productdiff)==21 and PRODUCT in productdiff
changed=git(R,'diff','--name-only',BASE,HEAD).decode().splitlines();assert [x for x in changed if not x.startswith('specs/1536-complete-wound-materialization/')]==[PRODUCT]
assert not git(R,'diff','--check',BASE,HEAD)
assert not git(R,'diff','--name-only',BASE,HEAD,'--','BookOfEternityClient.IntegrationTests','BookOfEternityClient.TestSupport','BookOfEternityClient.Tests','tests','scripts','docs','AGENTS.md')
before=frozen(PRODUCT,BASE);after=frozen(PRODUCT);added=b'                _logger.LogError(error,\n                    "Joint canonical publication retained {Disposition} after an exception.", result.Disposition);\n'
assert after.count(added)==1 and after.count(b'using Microsoft.Extensions.Logging;\n')==1
assert after.replace(added,b'',1).replace(b'using Microsoft.Extensions.Logging;\n',b'',1)==before
assert after==frozen(PRODUCT,FIRST_CANDIDATE) and git(R,'diff','--numstat',BASE,HEAD,'--',PRODUCT).decode().strip()=='3\t0\t'+PRODUCT
assert b'existing?.Plan, JointRepairAdmissionIssue(error.Message), existing?.Completed));\n'+added in after
for x in p['verificationTools']+p['causalReview']:
 b=frozen(x['path']);assert (len(b),h(b))==(x['bytes'],x['sha256'])
preptext=frozen(PREP).decode();firsttext=frozen(FIRST).decode();pa=ast.parse(preptext);fa=ast.parse(firsttext)
groups=ast.literal_eval(assignment(fa,'groups'));expectedgroups={'crash5':(['fr063-joint-interface-crash-current'],5),'integrity4':(['fr063-joint-interface-current-integrity'],4),'races2':(['fr063-joint-interface-source-races'],2),'settlement3':(['fr063-joint-interface-settlement'],3),'uncertain1':(['fr063-joint-interface-uncertain-current'],1)};assert groups==expectedgroups
allowed=next(n for n in ast.walk(pa) if isinstance(n,ast.Compare) and isinstance(n.left,ast.Name) and n.left.id=='mode' and isinstance(n.comparators[0],ast.Set) and 'catalog'in ast.literal_eval(n.comparators[0]));assert ast.literal_eval(allowed.comparators[0])=={'catalog','plan','corrective-plan','remaining-plan'}
pending=assignment(pa,'pending_categories');assert ast.literal_eval(pending.body)==set(groups['races2'][0]) and ast.literal_eval(pending.orelse)=={v[0][0] for k,v in groups.items() if k!='races2'}
mapping=next(n for n in ast.walk(fa) if isinstance(n,ast.IfExp) and isinstance(n.body,ast.Constant) and n.body.value=='corrective-plan');assert ast.dump(mapping.test)==ast.dump(ast.parse("mode=='races2'",mode='eval').body) and mapping.orelse.value=='remaining-plan'
count_expr=next(n.value for n in ast.walk(pa) if isinstance(n,ast.Assign) and isinstance(n.value,ast.IfExp) and isinstance(n.value.body,ast.Tuple));assert ast.literal_eval(count_expr.body)==(1,2,2) and ast.literal_eval(count_expr.orelse)==(4,5,13)
for module,text in [(pa,preptext),(fa,firsttext)]:
 assert len([n for n in ast.walk(module) if isinstance(n,ast.Call) and isinstance(n.func,ast.Attribute) and n.func.attr=='Popen'])==1 and not any(isinstance(n,ast.While)for n in ast.walk(module))
 assert "len(binding['productDelta'])==21"in text and "sentinel.open('x')"in text and 'assert not out.exists()'in text and "(out/'record.json').write_text"in text and 'allOutputInventory'in text
 assert text.index("(out/'record.json').write_text")<text.index('if native==0:')
 assert "git('diff','--name-only',binding['productBase'],actual,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()==binding['productDelta']"in text
assert "argv+=['-ValidateCatalog']"in preptext and "argv+=['-SelectionFile',selection,'-PlanOnly','-NoBuild']"in preptext
assert "catalog['actualSource']==actual"in preptext and "catalog['binaryAfter']==before"in preptext and "catalog['allOutputInventory']==all_before"in preptext
assert "'-NoBuild'"in firsttext and "'-ValidateCatalog'"not in firsttext and "before==prep['binaryAfter']"in firsttext and "all_before==prep['allOutputInventory']"in firsttext
assert "compiled+'-ni-iobs-l01-process-'+mode+'.FIRST.json'"in firsttext and "actual+'-ni-iobs-l01-process-zero-'+mode+'.FIRST.json'"in preptext
ledger=json.loads(frozen(p['ledger']['path']));counts={x['method']:x['staticRows']for x in ledger['rows']};prefix='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.'
counts.update({prefix+'GuaranteedTreatment_CurrentCaptureStopPrecedesPreparationAndExpiresActualOwner':1,prefix+'GuaranteedTreatment_CurrentCaptureObservationPreservesOrdinaryCompletion':2,prefix+'GuaranteedTreatment_LiveSeverityRepairCompletesJointOnceWithZeroCycleProgression':1,prefix+'GuaranteedTreatment_LiveSeverityRepairConsumesMatchingNonzeroReportExactlyOnce':1,'BookOfEternityClient.Tests.PromptDocumentationCoverageTests.MortalJointSeverityRepairGuidance_PreservesPublicTransportAndOnceOnlyClientOwnership':1})
selection=json.loads(frozen('tests/selection.json'))['selections'];catalog={x['id']:x for x in json.loads(frozen('tests/categories.json'))['categories']};assert len(selection)==16 and frozen('tests/selection.json')==frozen('tests/selections/1536-joint-interface-lifecycle-stage.json')
whole={};bycat={}
for row in selection:
 methods={}
 for selector in catalog[row['category']]['selectors']:
  for pattern in selector['tests']:
   matches={m:n for m,n in counts.items() if fnmatch.fnmatchcase(m,pattern)};assert matches and not set(matches)&set(methods);methods.update(matches)
 assert not set(methods)&set(whole);whole.update(methods);bycat[row['category']]=methods
assert (len(whole),sum(whole.values()))==(33,57)
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));selected={};modes={}
for mode,(cats,total) in groups.items():
 rows={m:n for cat in cats for m,n in bycat[cat].items()};assert sum(rows.values())==total and not set(rows)&set(selected);selected.update(rows)
 prior=next(x for x in cloud['pending']if x['mode']==mode);assert {x['method']:x['cases']for x in prior['methods']}==rows
 cat=catalog[cats[0]];assert cat['requirements']['exclusive'] and not cat['requirements']['frontendBuild']
 modes[mode]=dict(categories=cats,methods=len(rows),cases=total,methodCases=rows,timeoutMinutes=cat['timeoutMinutes'])
corrective=modes['races2']['methodCases'];remaining={m:n for k,v in modes.items()if k!='races2'for m,n in v['methodCases'].items()};retained={m:n for m,n in whole.items()if m not in selected}
priorcheck=read(D/'t179pd-ni-iobs-l01-cloud-source-check-20261018.json');retained_evidence=dict(priorcheck['retained']['methodCases'])
for path in ['t179pd-ni-iobs-l01-cloud-legacy3-independent-check-61e712e3-20261018.json','t179pd-ni-iobs-l01-close2-corrected-independent-check-23362e57-20261018.json']:
 for case in read(D/path)['cases']:
  assert case['outcome']=='Passed' and case['method']not in retained_evidence;retained_evidence[case['method']]=1
assert retained==retained_evidence and (len(retained),sum(retained.values()))==(26,42) and (len(corrective),sum(corrective.values()))==(2,2) and (len(remaining),sum(remaining.values()))==(5,13) and (len(selected),sum(selected.values()))==(7,15)
assert cloud['qualifiedCases']['totalDistinctTargets']==42 and cloud['release']['mode'] is None and cloud['readyDriftCorrection']['freshPreparation']=='REQUIRED_AFTER_SOURCE_ACCEPTANCE'
assert cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and cloud['oldCorrectedCrash']['artifactAvailability']=='UNAVAILABLE' and not cloud['oldCorrectedCrash']['recoveryClaim']
oldcloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json',BASE))
for key in ('rootIsolation','processIsolation','stateIsolation','assertions'):assert cloud['crashRequalificationPlan'][key]==oldcloud['crashRequalificationPlan'][key]
assert 'remaining4/5/13'in cloud['crashRequalificationPlan']['gates'] and 'fresh'in cloud['crashRequalificationPlan']['gates']
observer=p['mandatorySupplement'];assert h(frozen(observer['path']))==observer['sha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f' and len(frozen(observer['path']))==7410
assert 'immediately before/after each named actual'in cloud['processCensusScopeCorrection']['mandatoryGate'] and 'HISTORICAL'in cloud['diagnosticCorrection']['phase']
archives=[]
for key in ['legacy3','close2','close2Corrected','races2']:
 v=cloud['actuals'][key];ref=v['archive'];assert h(frozen(ref['path']))==ref['sha256'] and not git(R,'diff','--name-only',BASE,HEAD,'--',str(Path(ref['path']).parent))
 assert h(Path(v['recordPath']).read_bytes())==v['recordSha256'];archives.append(dict(mode=key,archive=ref,recordSha256=v['recordSha256'],unchanged=True))
env=Path('/tmp/1536-cloud-tools/env.sh').read_bytes();assert h(env)=='5d61c19c1341cf9a26b9d361c81da8c89d09924381791c4108d968555f899c53' and b'export HOME='not in env
runtime=Path(cloud['environment']['root'])/'runtime';st=runtime.stat();assert runtime.resolve()==runtime and stat.S_IMODE(st.st_mode)==0o700 and st.st_uid==1000
assert Path('/proc/sys/kernel/random/boot_id').read_text().strip()==cloud['environment']['linuxBootId']
recovery=[]
for rp in [Path('/tmp/1536-cloud-tools/carrier-recovery-8e1fedfe.json')]+[Path(x)for x in sys.argv[2:]]:
 receipt=read(rp);rev=receipt['sha'];base=receipt['base'];t=tree(C,rev);changed_receipt=git(R,'diff','--name-only',base,rev).decode().splitlines();assert len(changed_receipt)==receipt['changedPathsVerified'] and set(changed_receipt)=={x['path']for x in receipt['files']}
 for x in receipt['files']:
  b=git(C,'show',rev+':'+x['path']);assert b==frozen(x['path'],rev) and (len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob']) and t[x['path']]==(x['mode'],'blob',x['gitBlob'])
  if t[x['path']]==rt[x['path']]:assert b==(R/x['path']).read_bytes()==(C/x['path']).read_bytes()
 recovery.append(dict(carrier=rev,base=base,paths=len(changed_receipt),receiptSha256=h(rp.read_bytes()),bytesGitBlobsModesVerified=True))
assert recovery[-1]['carrier']==HEAD
for root in (R,C):assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain')
out=dict(kind='INDEPENDENT_READY_DIAGNOSTIC_CORRECTION_SOURCE_CHECK',checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),candidate=HEAD,base=BASE,firstCandidate=FIRST_CANDIDATE,sourcePacketSha256=h(frozen(PACKET)),sourcePinCount=98,sourcePinDigest=p['pinDigest'],changedPinnedPaths=changedpins,pinsMatchPhysicalBothCheckoutsGitBlobModeBytes=True,productDeltaPaths=21,productionDiff=dict(path=PRODUCT,insertions=3,deletions=0,exactInverseRestoresBase=True,unchangedFromFirstCandidate=True,sha256=h(after)),testsFixturesAssertionsLoggerCatalogSelectorsRunnerDocsUnchanged=True,historicalPacketExact=hist,verificationTools=p['verificationTools'],controllerSyntaxParsedOnly=True,whole=dict(descriptors=16,methods=33,cases=57),corrective=dict(descriptors=1,methods=2,cases=2),remaining=dict(descriptors=4,methods=5,cases=13),modes=modes,retained=dict(methods=26,cases=42,methodCases=retained,provenance='Original37 compiled45cc/4c6d + legacy3 actual9acc/compiled6ab43 + correctedclose2 actualb5d9/compiledcf522. No replay or cross-build equivalence.'),staticSelectionBasis='Exact disjoint selectors expanded against unchanged authored28methods51rows plus unchanged5existingmethods6rows; no new discovery executed.',observerV2SourceUnchangedMandatory=True,originalArchivesPreserved=archives,crashPlanIsolationUnchanged=True,oldCorrectedCrash='UNKNOWN_UNAVAILABLE_NO_RECOVERY',environment=dict(envSha256=h(env),runtime=str(runtime),uid=1000,mode='0700',homeUnchanged=True),recovery=recovery,independentGitHubRemote=remote,bothCheckoutsClean=True,fullGitTreesEqual=True,noGitAlternates=True,resolvedFinding=dict(id='L01_PRODUCT_DELTA_BINDING_20_VS_21',firstCandidateHeld=True,exact21PacketAndBothControllerGuardsReconciled=True),findings=[],eligibility='Only four separately named fresh ZERO preparation modes after saved/recovered SOURCE acceptance. No actual release; fresh independent PREP required.',limits='No build/discovery/test/controller/observer execution, process intervention or repository edits; static SOURCE only, no new runtime PASS.')
dest=D/('t179pd-ni-iobs-l01-ready-diagnostic-source-independent-check-'+HEAD[:8]+'-20261018.json');dest.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(dict(path=str(dest),bytes=dest.stat().st_size,sha256=h(dest.read_bytes()),findings=[],pins=98,productPaths=21,modes={k:[v['methods'],v['cases']]for k,v in modes.items()}),indent=2))
