#!/usr/bin/env python3
"""Read-only SOURCE checker; does not execute reviewed controllers, product, observer or tests."""
from pathlib import Path
import ast,datetime,fnmatch,hashlib,json,os,subprocess
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
HEAD='ef981a4606fb9f7813ed5390114da823f3704dac';BASE='bb99804e8c7124e4c625ea7557d61cdaa93a8035';IMPL='39685dff'
PACKET=REC+'t179pd-ni-iobs-l01-source-packet-20261018.json';PREP=REC+'t179pd-ni-iobs-l01-process-preparation-run.py';FIRST=REC+'t179pd-ni-iobs-l01-process-first-run.py'
def h(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root,env={**os.environ,'GIT_OPTIONAL_LOCKS':'0'})
def frozen(p,rev=HEAD):return git(R,'show',rev+':'+p)
def tree(root,rev):
 return {line.split(b'\t',1)[1].decode():tuple(line.split(b'\t',1)[0].decode().split())for line in git(root,'ls-tree','-rz',rev).split(b'\0')if line}
def assignment(module,name):return next(n.value for n in ast.walk(module)if isinstance(n,ast.Assign)and any(isinstance(t,ast.Name)and t.id==name for t in n.targets))
for root in (R,C):
 assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain')
 assert not (root/'.git/objects/info/alternates').exists()
remote=git(C,'ls-remote','origin','refs/heads/1536-wound-b2-cloud-20261012').decode().strip();assert remote==HEAD+'\trefs/heads/1536-wound-b2-cloud-20261012'
rt=tree(R,HEAD);assert rt==tree(C,HEAD)
p=json.loads(frozen(PACKET));oldraw=frozen(PACKET,BASE);old=json.loads(oldraw)
assert p['sourceBase']==BASE and len(p['sourcePins'])==p['sourcePinCount']==98 and p['pinDigest']=='8d9af639e85516874013910334b135946d3d7482da728e9889f99f8d080e106e'
assert h(json.dumps(p['sourcePins'],sort_keys=True,separators=(',',':')).encode())==p['pinDigest']
oldpins={x['path']:x for x in old['sourcePins']};assert set(oldpins)=={x['path']for x in p['sourcePins']};changedpins=[]
for pin in p['sourcePins']:
 b=frozen(pin['path']);assert b==(R/pin['path']).read_bytes()==(C/pin['path']).read_bytes() and (len(b),h(b),blob(b))==(pin['bytes'],pin['sha256'],pin['gitBlob']) and rt[pin['path']]==(pin['mode'],'blob',pin['gitBlob'])
 if any(pin[k]!=oldpins[pin['path']][k]for k in ('bytes','sha256','gitBlob','mode')):changedpins.append(pin['path'])
prod='BookOfEternityClient/Core/GameEngine/';tests='BookOfEternityClient.IntegrationTests/'
resume=prod+'GameEngine.MortalJointInterfaceResume.cs';spiritual=prod+'GameEngine.SpiritualContinuation.cs';validation=prod+'GameEngine.ValidationAndRepair.cs';crash=tests+'GameEngineTurnLifecycleTests.JointInterfaceCrash.cs';lifecycle=tests+'GameEngineTurnLifecycleTests.JointInterfaceLifecycle.cs';uncertain=tests+'GameEngineTurnLifecycleTests.JointInterfaceUncertainResume.cs';joint=tests+'MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs'
sourcepaths={resume,spiritual,validation,crash,lifecycle,uncertain,joint};assert set(changedpins)==sourcepaths|{PREP,FIRST}
changed=git(R,'diff','--name-only',BASE,HEAD).decode().splitlines();assert {x for x in changed if not x.startswith('specs/1536-complete-wound-materialization/')}==sourcepaths
assert not git(R,'diff','--check',BASE,HEAD)
assert not git(R,'diff','--name-only',BASE,HEAD,'--','BookOfEternityClient.TestSupport','BookOfEternityClient.Tests','BookOfEternityGMBridge','tests','scripts','docs','AGENTS.md')
productdiff=git(R,'diff','--name-only',p['productBase'],HEAD,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines();assert productdiff==p['productDelta'] and len(productdiff)==22 and set(productdiff)-set(old['productDelta'])=={spiritual}
hist=p['historicalPacket'];assert frozen(hist['path'])==oldraw and h(oldraw)==hist['sha256'] and len(oldraw)==hist['bytes']
assert h(frozen(p['ledger']['path']))==p['ledger']['sha256']==old['ledger']['sha256']
inverses=[]
def inverse(path,replacements):
 s=frozen(path).decode()
 for after,before in replacements:
  assert s.count(after)==1,(path,after[:80],s.count(after));s=s.replace(after,before,1)
 assert s.encode()==frozen(path,BASE)==frozen(path,IMPL),path
 inverses.append(dict(path=path,exactInverseToBaseline=True,sha256=h(frozen(path)),baseSha256=h(frozen(path,BASE)),substitutions=len(replacements)))
inverse(resume,[('var upstream = await CollectAcceptedTurnUpstreamRawStateIssuesCoreAsync(lease);','var upstream = await CollectAcceptedTurnUpstreamRawStateIssuesAsync();')])
inverse(spiritual,[('''        return await ValidateAcceptedTurnUpstreamRawItemsWithLeaseAsync(lease);
    }

    // The caller owns acquisition and awaited release; this helper only borrows.
    private async Task<UpstreamAcceptedTurnValidation> ValidateAcceptedTurnUpstreamRawItemsWithLeaseAsync(
        FileSystemManager.CanonicalWriteLease lease)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
''','')])
inverse(validation,[('''    private async Task<UpstreamAcceptedTurnValidation> CollectAcceptedTurnUpstreamRawStateIssuesAsync() =>
        await CollectAcceptedTurnUpstreamRawStateIssuesCoreAsync(null);

    // Ordinary callers retain the self-owned item lease. Selected current resume
    // lends its active lease so all admission remains inside that owning fence.
    private async Task<UpstreamAcceptedTurnValidation> CollectAcceptedTurnUpstreamRawStateIssuesCoreAsync(
        FileSystemManager.CanonicalWriteLease? ownedLease)
''','''    private async Task<UpstreamAcceptedTurnValidation> CollectAcceptedTurnUpstreamRawStateIssuesAsync()
'''),('''        var itemValidation = ownedLease is null
            ? await ValidateAcceptedTurnUpstreamRawItemsAsync()
            : await ValidateAcceptedTurnUpstreamRawItemsWithLeaseAsync(ownedLease);''','''        var itemValidation = await ValidateAcceptedTurnUpstreamRawItemsAsync();''')])
inverse(lifecycle,[('AssertHeldAsync(GameEngine engine, string owningDiagnostics, Func<string> describeCurrentDiagnostics)','AssertHeldAsync(GameEngine engine, string owningDiagnostics)'),('AssertUncertainCurrentResumeAsync(engine, owningDiagnostics, describeCurrentDiagnostics);','AssertUncertainCurrentResumeAsync(engine, owningDiagnostics);')])
inverse(joint,[('AssertHeldAsync(engine, logger.Describe(), logger.Describe);','AssertHeldAsync(engine, logger.Describe());')])
crashaddition='''        var integrityGuardEntries = new List<object>();
        const string integrityReason = "joint_interface_actual_publication_preimage_or_readback_changed";
        static int ReasonCount(string logs) => logs.Split(integrityReason, StringSplitOptions.None).Length - 1;
        void RequireCurrentInterfaceGuard(int entry, string logsBefore, string logsAfter)
        {
            if (fault != "CurrentInterface") return;
            var countBefore = ReasonCount(logsBefore); var countAfter = ReasonCount(logsAfter);
            var observed = new { Boundary = "iobs_actual_current_interface_guard_entry", Entry = entry,
                Reason = integrityReason, CountBefore = countBefore, CountAfter = countAfter,
                LogsBefore = logsBefore, LogsAfter = logsAfter };
            integrityGuardEntries.Add(observed);
            Console.WriteLine(JsonSerializer.Serialize(observed)); // retained even when the stronger oracle fails
            Assert.True(countAfter > countBefore, "Each real entry must newly report " + integrityReason);
        }
'''
inverse(crash,[(crashaddition,''),('            var firstLogsBefore = log.Describe();\n',''),('            var firstLogsAfter = log.Describe();\n            RequireCurrentInterfaceGuard(1, firstLogsBefore, firstLogsAfter);\n',''),('            var secondLogsBefore = log.Describe();\n',''),('            var secondLogsAfter = log.Describe();\n            RequireCurrentInterfaceGuard(2, secondLogsBefore, secondLogsAfter);\n',''),('                IntegrityGuardEntries = integrityGuardEntries,\n','')])
u=frozen(uncertain).decode();ustart=u.index('            // Observe this NEW returned call before its positive oracle.');uend=u.index('            Assert.NotNull(_currentResumedProof); Assert.Equal(43,',ustart);telemetry=u[ustart:uend]
assert telemetry.count('fixture.WritePhysicalInjuryDiagnostic(')==2 and 'describeCurrentDiagnostics()'in telemetry and 'ReadPhysical(path)'in telemetry and 'ReadError = error.ToString()'in telemetry
assert all(x not in telemetry for x in ['AcquireCanonical','ValidateCurrent','RequireCurrent','Register','Dispose','InvokePrivate','throw;'])
assert u.index('var handled = await InvokePrivateAsync<bool>')<ustart<uend and telemetry.index('uncertain_new_call_returned')<telemetry.index('object ObserveImage')<telemetry.index('uncertain_new_call_raw_images')
assert 'catch (Exception error) when (error is IOException or UnauthorizedAccessException)'in telemetry and 'CurrentProof = _currentResumedProof?.ToJson()'in telemetry
inverse(uncertain,[('AssertUncertainCurrentResumeAsync(GameEngine engine, string firstDiagnostics, Func<string> describeCurrentDiagnostics)','AssertUncertainCurrentResumeAsync(GameEngine engine, string firstDiagnostics)'),(telemetry,'')])
sp=frozen(spiritual).decode();v=frozen(validation).decode();res=frozen(resume).decode()
assert sp.count('ValidateAcceptedTurnUpstreamRawItemsAsync(')==1 and v.count('await CollectAcceptedTurnUpstreamRawStateIssuesAsync();')==2
assert '"ValidateAcceptedTurnUpstreamRawItemsAsync"'in frozen(tests+'GameEngineTurnLifecycleTests.SpiritualEntryGuards.cs').decode()
assert v.count('await CollectAcceptedTurnUpstreamRawStateIssuesCoreAsync(null);')==1 and res.count('CollectAcceptedTurnUpstreamRawStateIssuesCoreAsync(lease)')==1
assert res.index('RequireOrdinaryJointRepairLease(lease);')<res.index('CollectAcceptedTurnUpstreamRawStateIssuesCoreAsync(lease)')<res.index('finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, false, failure); }')
assert frozen('BookOfEternityClient/Core/FileSystemManager.cs')==frozen('BookOfEternityClient/Core/FileSystemManager.cs',BASE)
logger=tests+'GameEngineTurnLifecycleTests.SpiritualContinuation.cs';assert frozen(logger)==frozen(logger,BASE)
for x in p['verificationTools']+[p['correction']['planReview']]+p['jointBlockerReview']:
 b=frozen(x['path']);assert (len(b),h(b))==(x['bytes'],x['sha256'])
preptext=frozen(PREP).decode();firsttext=frozen(FIRST).decode();pa=ast.parse(preptext);fa=ast.parse(firsttext)
groups=ast.literal_eval(assignment(fa,'groups'));assert groups=={'crash5':(['fr063-joint-interface-crash-current'],5),'integrity4':(['fr063-joint-interface-current-integrity'],4),'uncertain1':(['fr063-joint-interface-uncertain-current'],1)}
pending=assignment(pa,'pending_categories');assert ast.literal_eval(pending.body)=={groups[x][0][0]for x in ['integrity4','uncertain1']} and ast.literal_eval(pending.orelse)==set(groups['crash5'][0])
mapping=next(n for n in ast.walk(fa)if isinstance(n,ast.IfExp)and isinstance(n.body,ast.Constant)and n.body.value=='remaining-plan');assert ast.dump(mapping.test)==ast.dump(ast.parse("mode=='crash5'",mode='eval').body)and mapping.orelse.value=='corrective-plan'
count_expr=next(n.value for n in ast.walk(pa)if isinstance(n,ast.Assign)and isinstance(n.value,ast.IfExp)and isinstance(n.value.body,ast.Tuple));assert ast.literal_eval(count_expr.body)==(2,2,5)and ast.literal_eval(count_expr.orelse)==(1,1,5)
for module,text in [(pa,preptext),(fa,firsttext)]:
 assert len([n for n in ast.walk(module)if isinstance(n,ast.Call)and isinstance(n.func,ast.Attribute)and n.func.attr=='Popen'])==1 and not any(isinstance(n,ast.While)for n in ast.walk(module))
 assert "len(binding['productDelta'])==22"in text and "sentinel.open('x')"in text and 'assert not out.exists()'in text and "(out/'record.json').write_text"in text and 'allOutputInventory'in text
 assert text.index("(out/'record.json').write_text")<text.index('if native==0:')
 assert "git('diff','--name-only',binding['productBase'],actual,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()==binding['productDelta']"in text
assert "argv+=['-ValidateCatalog']"in preptext and "argv+=['-SelectionFile',selection,'-PlanOnly','-NoBuild']"in preptext and "catalog['actualSource']==actual"in preptext and "catalog['binaryAfter']==before"in preptext and "catalog['allOutputInventory']==all_before"in preptext
assert "'-NoBuild'"in firsttext and "'-ValidateCatalog'"not in firsttext and "before==prep['binaryAfter']"in firsttext and "all_before==prep['allOutputInventory']"in firsttext
ledger=json.loads(frozen(p['ledger']['path']));counts={x['method']:x['staticRows']for x in ledger['rows']};prefix='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.'
counts.update({prefix+'GuaranteedTreatment_CurrentCaptureStopPrecedesPreparationAndExpiresActualOwner':1,prefix+'GuaranteedTreatment_CurrentCaptureObservationPreservesOrdinaryCompletion':2,prefix+'GuaranteedTreatment_LiveSeverityRepairCompletesJointOnceWithZeroCycleProgression':1,prefix+'GuaranteedTreatment_LiveSeverityRepairConsumesMatchingNonzeroReportExactlyOnce':1,'BookOfEternityClient.Tests.PromptDocumentationCoverageTests.MortalJointSeverityRepairGuidance_PreservesPublicTransportAndOnceOnlyClientOwnership':1})
selection=json.loads(frozen('tests/selection.json'))['selections'];catalog={x['id']:x for x in json.loads(frozen('tests/categories.json'))['categories']};assert len(selection)==16 and frozen('tests/selection.json')==frozen('tests/selections/1536-joint-interface-lifecycle-stage.json')
whole={};bycat={}
for row in selection:
 methods={}
 for selector in catalog[row['category']]['selectors']:
  for pattern in selector['tests']:
   matches={m:n for m,n in counts.items()if fnmatch.fnmatchcase(m,pattern)};assert matches and not set(matches)&set(methods);methods.update(matches)
 assert not set(methods)&set(whole);whole.update(methods);bycat[row['category']]=methods
assert (len(whole),sum(whole.values()))==(33,57)
modes={};selected={}
for mode,(cats,total)in groups.items():
 rows={m:n for cat in cats for m,n in bycat[cat].items()};assert len(rows)==1 and sum(rows.values())==total and not set(rows)&set(selected);selected.update(rows)
 cat=catalog[cats[0]];assert cat['requirements']['exclusive'] and not cat['requirements']['frontendBuild'] and cat['timeoutMinutes']==(10 if mode=='uncertain1' else 15)
 modes[mode]=dict(categories=cats,methodCases=rows,methods=1,cases=total,timeoutMinutes=cat['timeoutMinutes'],requiredPrep='remaining-plan'if mode=='crash5'else'corrective-plan')
corrective={m:n for k,v in modes.items()if k!='crash5'for m,n in v['methodCases'].items()};remaining=modes['crash5']['methodCases'];retained={m:n for m,n in whole.items()if m not in selected}
assert (len(retained),sum(retained.values()),len(corrective),sum(corrective.values()),len(remaining),sum(remaining.values()))==(30,47,2,5,1,5)
priorcheck=json.loads((D/'t179pd-ni-iobs-l01-cloud-source-check-20261018.json').read_bytes());oldretained=dict(priorcheck['retained']['methodCases'])
for cat in ['fr063-joint-interface-legacy-current','fr063-joint-interface-close-retention','fr063-joint-interface-source-races','fr063-joint-interface-settlement']:
 assert not set(oldretained)&set(bycat[cat]);oldretained.update(bycat[cat])
assert retained==oldretained
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));oldcloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json',BASE))
for key in ['qualifiedCases','actuals','oldCorrectedCrash','environment','holds']:
 assert cloud[key]==oldcloud[key],key
assert cloud['qualifiedCases']['totalDistinctTargets']==47 and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and not cloud['oldCorrectedCrash']['recoveryClaim']
for key in ['rootIsolation','processIsolation','stateIsolation','assertions']:assert cloud['crashRequalificationPlan'][key]==oldcloud['crashRequalificationPlan'][key]
assert p['namedActualRelease']['mode']is None and p['namedZeroRelease']['actualRelease']is False
assert cloud['borrowedLeaseCorrection']['freshPreparation']['status']=='UNRUN_SOURCE_PENDING'
observer=p['mandatorySupplement'];assert h(frozen(observer['path']))==observer['sha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f'
archives=[]
for key,val in cloud['actuals'].items():
 if 'archive'in val:
  ar=val['archive'];assert h(frozen(ar['path']))==ar['sha256'] and not git(R,'diff','--name-only',BASE,HEAD,'--',str(Path(ar['path']).parent));archives.append(dict(mode=key,archiveSha256=ar['sha256'],recordSha256=val.get('recordSha256'),unchanged=True))
rp=Path('/tmp/1536-cloud-tools/carrier-recovery-ef981a46.json');rr=json.loads(rp.read_bytes());assert rr['sha']==HEAD and rr['changedPathsVerified']==11 and rr['remoteShaVerified']and rr['clean']and not rr['localSourceOrObjectReuse']
assert set(git(R,'diff','--name-only',rr['base'],HEAD).decode().splitlines())=={x['path']for x in rr['files']}
for x in rr['files']:
 b=frozen(x['path']);assert b==(R/x['path']).read_bytes()==(C/x['path']).read_bytes() and (len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob']) and rt[x['path']]==(x['mode'],'blob',x['gitBlob'])
labels={mode:'t179pd-ni-iobs-l01-process-prep-first-'+name+'-borrowed-lease-cloud-813f1057'for mode,name in [('catalog','catalog'),('plan','whole-plan'),('corrective-plan','corrective-plan'),('remaining-plan','remaining-plan')]};assert p['namedZeroRelease']['labels']==labels
for field,values in [('whole',(16,33,57)),('corrective',(2,2,5)),('remaining',(1,1,5))]:assert p['currentVerificationContract'][field]==dict(zip(['descriptors','methods','cases'],values))
conditions=[dict(id='CURRENT_LEDGER_POINTER',observed=p['ledger']['currentRuntimePointer'],required='Point to cloud.borrowedLeaseCorrection and packet.currentVerificationContract; preserve authored ledger bytes.'),dict(id='CURRENT_CAUSAL_FIELDS',observedDecision=p['causalDecision'],observedReviewPaths=[x['path']for x in p['causalReview']],required='Current causalReview/Decision must identify saved joint blocker plus accepted borrowed-lease plan and this SOURCE; preserve prior settlement causal references explicitly as historical, and retain uncertain1 direct-versus-inferred cause limits.')]
for root in (R,C):assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain')
out=dict(kind='INDEPENDENT_BORROWED_LEASE_SOURCE_CHECK',checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),candidate=HEAD,baseline=BASE,implementationBase=git(R,'rev-parse',IMPL).decode().strip(),sourcePacketSha256=h(frozen(PACKET)),sourcePinCount=98,sourcePinDigest=p['pinDigest'],changedPinnedPaths=sorted(changedpins),productDeltaPaths=22,pinsGitModesBytesAndIndependentCloneExact=True,exactSourceInverses=inverses,allOriginalAssertionsUnchangedExceptAdditiveCurrentInterfaceGuard=True,ordinaryNoargBodyOrderOwnershipReflectionPreserved=True,extraCompatibilityNativeRequired=False,uncertainTelemetryNoLeaseRecoveryAuthorityCall=True,currentInterfaceEachEntryDeltaEmittedBeforeAssert=True,loggerPolicyUnchanged=True,selectorsCatalogBudgetsTestSupportRunnerDocsUnchanged=True,controllerSyntaxParsedOnly=True,controllerOnePopenEachNoAutoAdvanceRetry=True,verificationTools=p['verificationTools'],historicalPacketExact=hist,whole=dict(descriptors=16,methods=33,cases=57),corrective=dict(descriptors=2,methods=2,cases=5),remaining=dict(descriptors=1,methods=1,cases=5),modes=modes,retained=dict(methods=30,cases=47,methodCases=retained,provenance='Unchanged original37 + accepted legacy3/correctedclose2/correctedraces2/correctedsettlement3, each actual/compiled binding retained; no cross-build equivalence.'),staticSelectionBasis='Unchanged catalog/selector bytes and authored ledger method-row map plus five existing methods; no discovery or execution.',originalArchivesPreserved=archives,crashIsolationAndGenuineChildHelperUnchangedByExactInverse=True,oldCorrectedCrash='UNKNOWN_UNAVAILABLE',observerV2UnchangedMandatory=True,recovery=dict(receiptPath=str(rp),receiptSha256=h(rp.read_bytes()),changedPaths=11,fullIndependentGitTreeEqual=True,physicalBothCheckoutsClean=True,remoteExact=remote,noAlternates=True),blockingCodeOrBindingFindings=[{'id': 'L01-BORROWED-LEASE-SOURCE-OBSERVATION-001', 'severity': 'bounded-source-acceptance-blocker', 'path': 'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceUncertainResume.cs', 'lines': '83-87 in ef981a46', 'fact': 'ObserveImage delegates to ReadPhysical, whose File.Exists gate can return false on an inaccessible/existence-check error before the explicit exception catch; null bytes with null ReadError therefore do not strictly distinguish absent from unreadable.', 'impact': 'New diagnostic precision only; no change to original positive assertions or product lease ownership. Native remains held until the scoped telemetry correction is reviewed.', 'requiredCorrection': 'Read File.ReadAllBytes(fixture._fs.ResolvePath(path)) directly in ObserveImage; classify FileNotFoundException/DirectoryNotFoundException as absent and IOException/UnauthorizedAccessException as ReadError. No lease, recovery, parsing, existing helper/assertion change, or swallowed real caller exception.'}],metadataAcceptanceSaveConditions=conditions,namedZeroPlan=labels,eligibility='HOLD SOURCE acceptance for the bounded ObserveImage absence/error distinction; after correction, recheck its changed pin/digest/recovery and preserve metadata save conditions. No ZERO or actual release.',limits='Static SOURCE only. No build/discovery/test/controller/observer execution, current fixture-root/process action, source edit or fresh runtime claim.')
dest=D/'t179pd-ni-iobs-l01-borrowed-lease-source-independent-check-ef981a46-20261018.json';dest.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(dict(path=str(dest),bytes=dest.stat().st_size,sha256=h(dest.read_bytes()),blockingFindings=[{'id': 'L01-BORROWED-LEASE-SOURCE-OBSERVATION-001', 'severity': 'bounded-source-acceptance-blocker', 'path': 'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceUncertainResume.cs', 'lines': '83-87 in ef981a46', 'fact': 'ObserveImage delegates to ReadPhysical, whose File.Exists gate can return false on an inaccessible/existence-check error before the explicit exception catch; null bytes with null ReadError therefore do not strictly distinguish absent from unreadable.', 'impact': 'New diagnostic precision only; no change to original positive assertions or product lease ownership. Native remains held until the scoped telemetry correction is reviewed.', 'requiredCorrection': 'Read File.ReadAllBytes(fixture._fs.ResolvePath(path)) directly in ObserveImage; classify FileNotFoundException/DirectoryNotFoundException as absent and IOException/UnauthorizedAccessException as ReadError. No lease, recovery, parsing, existing helper/assertion change, or swallowed real caller exception.'}],metadataConditions=2,pins=98,product=22),indent=2))
