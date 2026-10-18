#!/usr/bin/env python3
"""Read-only independent selected-routing SOURCE check; writes only its /tmp report.
Never invokes a controller, compiler, discovery, test, census, or process-control tool.
"""
import ast,datetime,fnmatch,hashlib,json,os,subprocess
from pathlib import Path
R=Path('/workspace/The-Book-of-Eternity-Reborn'); C=Path('/workspace/1536-cloud-clean-restore'); D=Path('/tmp/1536-cloud-tools/review')
HEAD='fed1f7789db0145de866cd4d73b97ab030871509'; BASE='f390eb0602a99f5a2387ab7dc6ef52571e856b26'; PLAN='c756e04f9e83d5d4f0108b918b65686d9ed4db95'
REC='specs/1536-complete-wound-materialization/recovery/'; PACKET=REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'; CLOUD=REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'; PREP=REC+'t179pd-ni-iobs-l01-process-preparation-run.py'; FIRST=REC+'t179pd-ni-iobs-l01-process-first-run.py'; STORY='BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointStoryTurn.cs'
def h(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root,env={**os.environ,'GIT_OPTIONAL_LOCKS':'0'})
def frozen(path,rev=HEAD):return git(R,'show',rev+':'+path)
def tree(root,rev):return {line.split(b'\t',1)[1].decode():tuple(line.split(b'\t',1)[0].decode().split())for line in git(root,'ls-tree','-rz',rev).split(b'\0')if line}
def assignment(module,name):return next(n.value for n in ast.walk(module)if isinstance(n,ast.Assign)and any(isinstance(t,ast.Name)and t.id==name for t in n.targets))
def info(path):
 p=Path(path);b=p.read_bytes();return dict(path=str(p),bytes=len(b),sha256=h(b))
for root in (R,C):
 assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain')
 assert not (root/'.git/objects/info/alternates').exists()
remote=git(C,'ls-remote','origin','refs/heads/1536-wound-b2-cloud-20261012').decode().strip();assert remote==HEAD+'\trefs/heads/1536-wound-b2-cloud-20261012'
rt=tree(R,HEAD);assert rt==tree(C,HEAD)
p=json.loads(frozen(PACKET));oldraw=frozen(PACKET,BASE);old=json.loads(oldraw)
assert p['sourceBase']==BASE and len(p['sourcePins'])==p['sourcePinCount']==99
assert p['pinDigest']=='70aa551ca427bb9c28b18e205bcbe2e10d1ebb88487309bb73b4a31426e37eb5'==h(json.dumps(p['sourcePins'],sort_keys=True,separators=(',',':')).encode())
oldpins={x['path']:x for x in old['sourcePins']};newpins={x['path']:x for x in p['sourcePins']};assert len(oldpins)==98 and set(newpins)-set(oldpins)=={STORY} and not set(oldpins)-set(newpins)
changedpins=[]
for pin in p['sourcePins']:
 b=frozen(pin['path']);assert b==(R/pin['path']).read_bytes()==(C/pin['path']).read_bytes()
 assert (len(b),h(b),blob(b))==(pin['bytes'],pin['sha256'],pin['gitBlob']) and rt[pin['path']]==(pin['mode'],'blob',pin['gitBlob'])
 if pin['path'] not in oldpins or any(pin[k]!=oldpins[pin['path']][k]for k in ('bytes','sha256','gitBlob','mode')):changedpins.append(pin['path'])
assert set(changedpins)=={STORY,PREP,FIRST}
assert set(git(R,'diff','--name-only',PLAN,HEAD).decode().splitlines())=={STORY,PREP,FIRST,PACKET,CLOUD}
assert {x for x in git(R,'diff','--name-only',BASE,HEAD).decode().splitlines()if not x.startswith('specs/1536-complete-wound-materialization/')}=={STORY}
assert not git(R,'diff','--check',PLAN,HEAD)
legacy_whitespace=subprocess.run(['git','diff','--check',BASE,PLAN],cwd=R,capture_output=True,text=True)
assert legacy_whitespace.returncode==2 and legacy_whitespace.stdout.count('trailing whitespace.')==2
assert all(x in legacy_whitespace.stdout for x in ['check_l01_borrowed_lease_uncertain1_failure-v1-absence.py:16','check_l01_borrowed_lease_uncertain1_failure.py:16'])
assert not git(R,'diff','--name-only',BASE,HEAD,'--','BookOfEternityClient.IntegrationTests','BookOfEternityClient.TestSupport','BookOfEternityClient.Tests','BookOfEternityGMBridge','tests','scripts','docs','OtherGuides','Examples','AGENTS.md')
product=git(R,'diff','--name-only',p['productBase'],HEAD,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines();assert product==p['productDelta'] and len(product)==23 and set(product)-set(old['productDelta'])=={STORY}
hist=p['historicalPacket'];assert frozen(hist['path'])==oldraw and (len(oldraw),h(oldraw))==(hist['bytes'],hist['sha256'])
assert h(frozen(p['ledger']['path']))==p['ledger']['sha256']==old['ledger']['sha256']=='85e23e3546315a04c0f3317433fb3662ea8c893d65850b59e71a9d61e5f418cc'
# Prove the old marker branch, shared Ready gate, prior phase branch and whole
# post-validation/catch/completion body survive byte-exactly after indentation.
s=frozen(STORY).decode();o=frozen(STORY,BASE).decode();assert frozen(STORY,BASE)==frozen(STORY,PLAN)
comment='                // A retained uncertainty is not proof that common publication\n'
oldif='                if (bytes is null) return claimed ? HoldMortalJointStoryTurn("The joint completion marker is missing.") : null;\n'
completion='                var completion = ParseReadySignalMetadata'
marker='                var marker = StrictJsonAuthority.Deserialize'
first=s[s.index(comment):s.index(completion)]
oldfirst=o[o.index(oldif):o.index(completion)]
elsehead='                else\n                {\n';newmarker=first[first.index(elsehead)+len(elsehead):];assert newmarker.endswith('                }\n')
def unindent(x):return ''.join(line[4:]if line.startswith('    ')else line for line in x.splitlines(True))
assert unindent(newmarker[:-len('                }\n')])==o[o.index(marker):o.index(completion)]
phase='                var phase = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync'
phasewrap='                if (bytes is not null)\n                {\n'
leaseend='            }\n\n            var validated = await ValidateAcceptedTurnOutcomeWithRepairLoopAsync'
newphase=s[s.index(phasewrap):s.index(leaseend)];oldphase=o[o.index(phase):o.index(leaseend)]
assert newphase.endswith('                }\n') and unindent(newphase[len(phasewrap):-len('                }\n')])==oldphase
assert s[s.index(completion):s.index(phasewrap)]==o[o.index(completion):o.index(phase)]
helperstart='    // Applicability only, borrowed lease only. The direct validator must still\n';helperend='    private MortalJointStoryTurnDisposition HoldMortalJointStoryTurn(string reason)'
helper=s[s.index(helperstart):s.index(helperend)]
inverse=s.replace(first,oldfirst,1).replace(newphase,oldphase,1).replace(helper,'',1)
assert inverse.encode()==frozen(STORY,BASE)
poststart='            var validated = await ValidateAcceptedTurnOutcomeWithRepairLoopAsync';assert s[s.index(poststart):s.index(helperstart)]==o[o.index(poststart):o.index(helperend)]
assert s[:s.index(comment)]==o[:o.index(oldif)]
assert first.count('return claimed ? HoldMortalJointStoryTurn("The joint completion marker is missing.") : null;')==1
assert 'if (!claimed || _mortalJointRepairCanonicalRefresh?.Disposition != MortalJointRepairPublicationDisposition.PublicationUncertain ||'in first
assert 'inspection.State != MortalQteDecisionPublication.CurrentPublicationState.NotCommitted ||'in first and 'inspection.Reason != "original_baseline" ||'in first
assert first.index('inspection.Reason != "original_baseline"')<first.index('!await IsCurrentSelectedPublishedInterfaceResumeCandidateAsync(lease, frame)')<first.index('_gameLoop.TurnNumber != frame.TurnNumber - 1')
assert s.index('inspection.State == MortalQteDecisionPublication.CurrentPublicationState.IntegrityConflict')<s.index(comment)
required=['_fs.EnsureCanonicalWriteLeaseActive(lease);','RequireOrdinaryJointRepairLease(lease);','ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath)','MortalJointRepairCheckpoint.Parse(JsonSerializer.SerializeToElement(node))','parsed.Checkpoint is not { UsesInterfacePublication: true } checkpoint','binding["sessionId"]!.GetValue<string>() != frame.SessionId','binding["requestId"]!.GetValue<string>() != frame.RequestId','binding["snapshotToken"]!.GetValue<string>() != frame.Payload.ManifestPayloadHash','binding["turnNumber"]!.GetValue<int>() != frame.TurnNumber','ThrowIfRepairSessionReplaced(lease, binding["sessionGeneration"]!.GetValue<string>());','record.RequireCheckpoint(binding);','record.RequireLegacyPending(pending);','if (record.State != "Published" || record.HasErrors) return false;','await MortalJointInterfacePublication.RequireImagesAsync(_fs, lease, bytes, record.AfterBytes);']
assert all(x in helper for x in required)
assert all(x not in helper for x in ['AcquireCanonical','Dispose','ReleaseOwned','WriteFile','Normalize','Register','_mortalJointRepairCanonicalRefresh =','_mortalJointRepairCheckpoint =','_mortalJointRepairCorrectionReady ='])
assert s.count('IsCurrentSelectedPublishedInterfaceResumeCandidateAsync(')==2
# Revalidation and retained uncertainty fallback remain the existing owner path.
v=frozen('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs').decode();resume=frozen('BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointInterfaceResume.cs').decode()
assert 'var resumedInterface = await TryResumeMortalJointInterfaceBeforeCoreAsync(source, activeSnapshotContext, expectedTurn);\n            if (resumedInterface.HasValue) return resumedInterface.Value;\n            if (uncertainJointDiagnostic)\n                return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;'in v
assert resume.index('finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, false, failure); }')<resume.index('_mortalJointRepairCanonicalRefresh = null;')<resume.index('PublishCurrentMortalJointRepairAsync')
assert 'if (validated != AcceptedTurnValidationDisposition.MortalJointRepairCompletionReady)'in s and 'ReadJointProgressionPhaseAsync' in s[s.index(poststart):]
# Controller bodies: parse only; one fixed invocation, source-bound inventories,
# original outcome first, no loops/retry/automatic advance.
preptext=frozen(PREP).decode();firsttext=frozen(FIRST).decode();pa=ast.parse(preptext);fa=ast.parse(firsttext)
groups=ast.literal_eval(assignment(fa,'groups'));assert groups=={'crash5':(['fr063-joint-interface-crash-current'],5),'uncertain1':(['fr063-joint-interface-uncertain-current'],1)}
pending=assignment(pa,'pending_categories');assert ast.literal_eval(pending.body)==set(groups['uncertain1'][0]) and ast.literal_eval(pending.orelse)==set(groups['crash5'][0])
mapping=next(n for n in ast.walk(fa)if isinstance(n,ast.IfExp)and isinstance(n.body,ast.Constant)and n.body.value=='remaining-plan');assert ast.dump(mapping.test)==ast.dump(ast.parse("mode=='crash5'",mode='eval').body)and mapping.orelse.value=='corrective-plan'
count_expr=next(n.value for n in ast.walk(pa)if isinstance(n,ast.Assign)and isinstance(n.value,ast.IfExp)and isinstance(n.value.body,ast.Tuple));assert ast.literal_eval(count_expr.body)==(1,1,1)and ast.literal_eval(count_expr.orelse)==(1,1,5)
for module,text in [(pa,preptext),(fa,firsttext)]:
 assert len([n for n in ast.walk(module)if isinstance(n,ast.Call)and isinstance(n.func,ast.Attribute)and n.func.attr=='Popen'])==1 and not any(isinstance(n,ast.While)for n in ast.walk(module))
 assert "len(binding['sourcePins'])==99 and len(binding['productDelta'])==23"in text and "sentinel.open('x')"in text and 'assert not out.exists()'in text and 'allOutputInventory'in text
 assert text.index("(out/'record.json').write_text")<text.index('if native==0:')
 assert "git('diff','--name-only',binding['productBase'],actual,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()==binding['productDelta']"in text
assert "argv+=['-ValidateCatalog']"in preptext and "argv+=['-SelectionFile',selection,'-PlanOnly','-NoBuild']"in preptext and "catalog['actualSource']==actual"in preptext and "catalog['binaryAfter']==before"in preptext and "catalog['allOutputInventory']==all_before"in preptext
assert "'-NoBuild'"in firsttext and "'-ValidateCatalog'"not in firsttext and "before==prep['binaryAfter']"in firsttext and "all_before==prep['allOutputInventory']"in firsttext and "prep['sourcePinCount']==99"in firsttext
for x in p['verificationTools']+p['causalReview']:
 b=frozen(x['path']);assert (len(b),h(b))==(x['bytes'],x['sha256'])
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
 modes[mode]=dict(categories=cats,methodCases=rows,descriptors=1,methods=1,cases=total,timeoutMinutes=cat['timeoutMinutes'],requiredPrep='remaining-plan'if mode=='crash5'else'corrective-plan')
retained={m:n for m,n in whole.items()if m not in selected};assert (len(retained),sum(retained.values()))==(31,51)
priorcheck=json.loads((D/'t179pd-ni-iobs-l01-cloud-source-check-20261018.json').read_bytes());oldretained=dict(priorcheck['retained']['methodCases'])
for cat in ['fr063-joint-interface-legacy-current','fr063-joint-interface-close-retention','fr063-joint-interface-source-races','fr063-joint-interface-settlement','fr063-joint-interface-current-integrity']:
 assert not set(oldretained)&set(bycat[cat]);oldretained.update(bycat[cat])
assert retained==oldretained
cloud=json.loads(frozen(CLOUD));oldcloud=json.loads(frozen(CLOUD,BASE))
for key in ['qualifiedCases','actuals','oldCorrectedCrash','environment','holds']:assert cloud[key]==oldcloud[key],key
assert cloud['qualifiedCases']['totalDistinctTargets']==51 and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and not cloud['oldCorrectedCrash']['recoveryClaim']
for key in ['rootIsolation','processIsolation','stateIsolation','assertions']:assert cloud['crashRequalificationPlan'][key]==oldcloud['crashRequalificationPlan'][key]
assert p['namedActualRelease']['mode']is None and p['namedZeroRelease']['actualRelease']is False and cloud['release']['mode']is None
assert cloud['selectedResumeRoutingCorrection']['freshPreparation']['status']=='UNRUN_SOURCE_PENDING'
assert 'cloud.selectedResumeRoutingCorrection'in p['ledger']['currentRuntimePointer']
observer=p['mandatorySupplement'];assert h(frozen(observer['path']))==observer['sha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f'
archives=[]
for key,val in cloud['actuals'].items():
 if 'archive'in val:
  ar=val['archive'];assert h(frozen(ar['path']))==ar['sha256'] and not git(R,'diff','--name-only',BASE,HEAD,'--',str(Path(ar['path']).parent));archives.append(dict(mode=key,archiveSha256=ar['sha256'],recordSha256=val.get('recordSha256'),unchanged=True))
receipts=[]
for name,rev,base,count in [('c756e04f',PLAN,BASE,12),('fed1f778',HEAD,PLAN,5)]:
 rp=Path('/tmp/1536-cloud-tools/carrier-recovery-'+name+'.json');rr=json.loads(rp.read_bytes());assert rr['sha']==rev and rr['base']==base and rr['changedPathsVerified']==count and rr['remoteShaVerified']and rr['clean']and not rr['localSourceOrObjectReuse']
 tr=tree(R,rev);assert tr==tree(C,rev)
 assert set(git(R,'diff','--name-only',base,rev).decode().splitlines())=={x['path']for x in rr['files']}
 for x in rr['files']:
  b=frozen(x['path'],rev);assert b==git(C,'show',rev+':'+x['path']) and (len(b),h(b),blob(b))==(x['bytes'],x['sha256'],x['gitBlob']) and tr[x['path']]==(x['mode'],'blob',x['gitBlob'])
  if rev==HEAD:assert b==(R/x['path']).read_bytes()==(C/x['path']).read_bytes()
 receipts.append(dict(**info(rp),sha=rev,base=base,changedPaths=count,originalReportedPinCount=rr.get('sourcePinsVerified')))
labels={mode:'t179pd-ni-iobs-l01-process-prep-first-'+name+'-selected-routing-cloud-813f1057'for mode,name in [('catalog','catalog'),('plan','whole-plan'),('corrective-plan','corrective-plan'),('remaining-plan','remaining-plan')]};assert p['namedZeroRelease']['labels']==labels
for field,values in [('whole',(16,33,57)),('corrective',(1,1,1)),('remaining',(1,1,5))]:assert p['currentVerificationContract'][field]==dict(zip(['descriptors','methods','cases'],values))
assert p['namedZeroRelease']['order']==['catalog','plan','corrective-plan','remaining-plan']
wrapper=Path('/tmp/1536-cloud-tools/run-selected-routing-phase.py');wold=Path('/tmp/1536-cloud-tools/run-borrowed-phase-noncatalog-corrected-version.py')
assert wrapper.read_text()==wold.read_text().replace('borrowed-lease','selected-routing');ast.parse(wrapper.read_text())
attpath=Path('/tmp/1536-cloud-tools/selected-routing-source-recovery-count-attestation-fed1f778.json'); att=json.loads(attpath.read_bytes())
assert att['head']==HEAD and att['actualIndividuallyVerifiedPins']==99 and att['reportedOriginalCount']==98 and att['pinDigest']==p['pinDigest']
assert h(Path(att['originalReceiptPath']).read_bytes())==att['originalReceiptSha256']==receipts[-1]['sha256']
ro=Path('/tmp/1536-cloud-tools/recover-carrier.py');rn=Path('/tmp/1536-cloud-tools/recover-carrier-dynamic-pins.py')
assert rn.read_text()==ro.read_text().replace('sourcePinsVerified=98',"sourcePinsVerified=len(p['sourcePins'])").replace('and98pins','and all current source pins')
ast.parse(rn.read_text());assert "for row in p['sourcePins']:"in ro.read_text()
conditions=[dict(id='CURRENT_CRASH_PREP_GATE',observed=cloud['crashRequalificationPlan']['gates'],required='Set current corrective to 1/1/1 uncertain1; whole16/33/57 and remaining1/1/5 crash remain. SOURCE accepted only after saved recovery; fresh ZERO4/PREP and distinct crash release still required.'),dict(id='BORROWED_PHASE_HISTORICAL',observed={k:cloud['borrowedLeaseCorrection'].get(k)for k in ['status','phase','supersededBy']},required='Add explicit historical phase/supersededBy selectedResumeRoutingCorrection while preserving all original borrowed SOURCE/PREP/actual records.'),dict(id='RECOVERY_PIN_COUNT_READBACK',observed='Original fed1f778 receipt sourcePinsVerified=98 and method says and98pins.',required='Preserve original receipt. Save corrected explicit 99-pin attestation/readback with acceptance; this independent check verifies all99 bytes/hash/blob/modes in both trees.')]
for root in (R,C):assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain')
out=dict(kind='INDEPENDENT_SELECTED_ROUTING_SOURCE_CHECK',checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),candidate=HEAD,baseline=BASE,planCarrier=PLAN,sourcePacketSha256=h(frozen(PACKET)),sourcePinCount=99,sourcePinDigest=p['pinDigest'],changedPinnedPaths=sorted(changedpins),newPin=STORY,productDeltaPaths=23,pinsGitModesBytesAndIndependentCloneExact=True,sourceInverse=dict(path=STORY,currentSha256=h(s.encode()),baselineSha256=h(o.encode()),exactInverse=True,prefixIntegrityConflictUnchanged=True,nonNullMarkerBodyExactModuloIndent=True,sharedReadyGateExact=True,priorPhaseBodyExactModuloIndent=True,fullPostvalidationCatchCompletionAndHoldBodyExact=True),helper=dict(existingOwnedLeaseBorrowedOnly=True,ordinaryPurposeSessionGenerationFrameCheckpoint2PublishedNoErrorsExactPendingAndI=True,readOnlyClassificationNoAuthorityMutation=True,diagnosticRetainedUntilActualSelectedOwnerRelease=True,validationReacquiresAndReprovesAfterClassification=True,uncertainFallbackHoldsIfSelectedDisappears=True),compatibility=dict(retained51NoReplayJustified=True,extraCompatibilityNativeRequired=False,missingMarkerContingencyNotActivated=True,reason='Only same-frame PublicationUncertain plus missing marker and strict current selected PublishedI/original_baseline/prior-runtime/Ready can use new direct routing. IntegrityConflict remains first; Committed/v1/non-null marker and shared postvalidation bodies unchanged. No bare null dispatch on successful classification.'),selectorsFixturesAssertionsCatalogBudgetsRunnerGmDocsUnchanged=True,controllerSyntaxParsedOnly=True,controllerOnePopenEachNoAutoAdvanceRetry=True,verificationTools=p['verificationTools'],historicalPacketExact=hist,whole=dict(descriptors=16,methods=33,cases=57),corrective=dict(descriptors=1,methods=1,cases=1),remaining=dict(descriptors=1,methods=1,cases=5),modes=modes,retained=dict(methods=31,cases=51,methodCases=retained,provenance='Original37 + accepted legacy3/correctedclose2/correctedraces2/correctedsettlement3/borrowedleaseintegrity4 at their distinct original actual/compiled sources. No new cross-build runtime claim.'),staticSelectionBasis='Unchanged catalog/selection, authored ledger plus five existing methods; no discovery/runtime.',originalArchivesPreserved=archives,crashIsolationAndGenuineChildrenFixturesUnchanged=True,oldCorrectedCrash='UNKNOWN_UNAVAILABLE',observerV2UnchangedMandatory=True,wrapper=dict(**info(wrapper),exactPriorCorrectedWrapperWithTagSubstitutionOnly=True,executed=False),recovery=dict(receipts=receipts,fullIndependentGitTreeEqual=True,all99PinsIndependentlyVerified=True,physicalBothCheckoutsClean=True,remoteExact=remote,noAlternates=True),findings=[],metadataAcceptanceSaveConditions=conditions,recoveryCountCorrection=dict(attestation=info(attpath),originalUtility=info(ro),correctedUtility=info(rn),exactTwoMetadataSubstitutionsOnly=True,allPinsLoopUnchanged=True,approvedForFutureRecovery=True,executedByReviewer=False),historicalArtifactWhitespace=dict(scope='Two already saved causal checker line16 trailing spaces; preserve original artifact hashes. Candidate implementation diffcheck clean.',output=legacy_whitespace.stdout),namedZeroPlan=labels,eligibility='ACCEPT SOURCE and proposed four individually named ZERO invocations only after these metadata conditions and saved/pushed/exact-remote/clean-independent-recovery acceptance readback. No actual before fresh independent PREP and its separately saved/recovered named release.',limits='Static SOURCE only. No build/discovery/test/controller/observer execution, current fixture-root/process action, source edit or fresh runtime claim.')
dest=D/'t179pd-ni-iobs-l01-selected-routing-source-independent-check-fed1f778-20261018.json';dest.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(dict(**info(dest),findings=[],metadataConditions=len(conditions),pins=99,product=23,whole=[16,33,57],corrective=[1,1,1],remaining=[1,1,5]),indent=2))
