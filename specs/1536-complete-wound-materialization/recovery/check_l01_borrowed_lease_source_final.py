#!/usr/bin/env python3
"""Focused immutable SOURCE follow-up; executes no product/controller/observer/native checks."""
from pathlib import Path
import json,hashlib,subprocess,datetime,os
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
PREV='ef981a4606fb9f7813ed5390114da823f3704dac';HEAD='9fdecef3fd79614b9eb13b5609c99b9457d51af4';BASE='bb99804e8c7124e4c625ea7557d61cdaa93a8035';P=REC+'t179pd-ni-iobs-l01-source-packet-20261018.json';CLOUD=REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json';U='BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceUncertainResume.cs'
def h(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root,env={**os.environ,'GIT_OPTIONAL_LOCKS':'0'})
def source(path,rev=HEAD):return git(R,'show',rev+':'+path)
def ref(path):
 path=Path(path);b=path.read_bytes();return dict(path=str(path),bytes=len(b),sha256=h(b))
firstpath=D/'t179pd-ni-iobs-l01-borrowed-lease-source-independent-check-ef981a46-20261018.json';assert h(firstpath.read_bytes())=='1bc4b1e95db10a777674c5f0b0d88c8773654772e9a169c53109b80de7c8e68a';first=json.loads(firstpath.read_bytes())
feedback=D/'t179pd-ni-iobs-l01-borrowed-lease-source-feedback-ef981a46-20261018.json';assert h(feedback.read_bytes())=='d5b425280b20df4462a5d79cd43e3aaf1889d8470c3c6b219c0fbfad54f8bb3e'
assert first['candidate']==PREV and len(first['exactSourceInverses'])==7 and len(first['blockingCodeOrBindingFindings'])==1
for root in (R,C):assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain') and not (root/'.git/objects/info/alternates').exists()
remote=git(C,'ls-remote','origin','refs/heads/1536-wound-b2-cloud-20261012').decode().strip();assert remote==HEAD+'\trefs/heads/1536-wound-b2-cloud-20261012'
assert git(R,'ls-tree','-r',HEAD)==git(C,'ls-tree','-r',HEAD)
assert set(git(R,'diff','--name-only',PREV,HEAD).decode().splitlines())=={U,P,CLOUD} and not git(R,'diff','--check',PREV,HEAD)
old='''                try { return new { Bytes = ReadPhysical(path), ReadError = (string?)null }; }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { return new { Bytes = (byte[]?)null, ReadError = error.ToString() }; }'''
new='''                try { return new { Bytes = File.ReadAllBytes(fixture._fs.ResolvePath(path)), Absent = false, ReadError = (string?)null }; }
                catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
                { return new { Bytes = (byte[]?)null, Absent = true, ReadError = (string?)null }; }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { return new { Bytes = (byte[]?)null, Absent = false, ReadError = error.ToString() }; }'''
s=source(U).decode();assert s.count(new)==1 and s.replace(new,old,1).encode()==source(U,PREV)
start=s.index('            // Observe this NEW returned call before its positive oracle.');end=s.index('            Assert.NotNull(_currentResumedProof); Assert.Equal(43,',start)
assert s.index('var handled = await InvokePrivateAsync<bool>')<start<end
telemetry=s[start:end]
assert 'File.Exists'not in telemetry and 'ReadPhysical('not in telemetry and 'AcquireCanonical'not in telemetry and 'InvokePrivate'not in telemetry
assert telemetry.index('FileNotFoundException or DirectoryNotFoundException')<telemetry.index('IOException or UnauthorizedAccessException')
inverse=s[:start]+s[end:];inverse=inverse.replace('AssertUncertainCurrentResumeAsync(GameEngine engine, string firstDiagnostics, Func<string> describeCurrentDiagnostics)','AssertUncertainCurrentResumeAsync(GameEngine engine, string firstDiagnostics)',1);assert inverse.encode()==source(U,BASE)
p=json.loads(source(P));prev=json.loads(source(P,PREV));assert len(p['sourcePins'])==98 and p['sourcePinCount']==98 and p['pinDigest']=='b4e01729c1a4df935f79e7407e6aafe52bf9394d4f6e1ddab6f83a40893f02d5'
assert h(json.dumps(p['sourcePins'],sort_keys=True,separators=(',',':')).encode())==p['pinDigest']
changed=[];oldpins={x['path']:x for x in prev['sourcePins']}
for pin in p['sourcePins']:
 b=source(pin['path']);assert b==(R/pin['path']).read_bytes()==(C/pin['path']).read_bytes() and (len(b),h(b),blob(b))==(pin['bytes'],pin['sha256'],pin['gitBlob'])
 assert git(C,'ls-tree',HEAD,'--',pin['path']).split(b'\t')[0].decode().split()==[pin['mode'],'blob',pin['gitBlob']]
 if pin!=oldpins[pin['path']]:changed.append(pin['path'])
assert changed==[U] and len(p['productDelta'])==22 and p['productDelta']==prev['productDelta']==git(R,'diff','--name-only',p['productBase'],HEAD,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()
assert p['ledger']['path']==prev['ledger']['path'] and p['ledger']['sha256']==prev['ledger']['sha256']==h(source(p['ledger']['path'])) and 'cloud.borrowedLeaseCorrection'in p['ledger']['currentRuntimePointer']
assert p['historicalCausalReview']==prev['causalReview'] and p['historicalCausalDecision']==prev['causalDecision'] and 'NESTED_LEASE'in p['causalDecision']
assert [x['path']for x in p['causalReview']]==[REC+'t179pd-ni-iobs-l01-current-resume-joint-blocker-review-20261018.json',REC+'t179pd-ni-iobs-l01-borrowed-lease-plan-independent-review-20261018.json']
for x in p['causalReview']+p['historicalCausalReview']:
 b=source(x['path']);assert len(b)==x['bytes']and h(b)==x['sha256']
assert 'not its unique exception/final state'in p['causalLimits']and 'not reconstructed'in p['causalLimits']
for k in ['currentVerificationContract','verificationTools','mandatorySupplement','namedZeroRelease','namedActualRelease','historicalPacket','jointBlockerReview']:
 assert p[k]==prev[k],k
cloud=json.loads(source(CLOUD));oldcloud=json.loads(source(CLOUD,PREV));assert cloud['sourcePinDigest']==p['pinDigest']
for k in ['qualifiedCases','actuals','oldCorrectedCrash','holds','crashRequalificationPlan','environment']:assert cloud[k]==oldcloud[k]
for k in ['path','sha256','bytes']:assert cloud['borrowedLeaseCorrection']['sourcePacket'][k]==dict(path=P,sha256=h(source(P)),bytes=len(source(P)))[k]
assert cloud['borrowedLeaseCorrection']['freshPreparation']['status']=='UNRUN_SOURCE_PENDING'and p['namedActualRelease']['mode']is None
rp=Path('/tmp/1536-cloud-tools/carrier-recovery-9fdecef3.json');r=json.loads(rp.read_bytes());assert r['sha']==HEAD and r['base']==PREV and r['changedPathsVerified']==3 and r['remoteShaVerified']and r['clean']and not r['localSourceOrObjectReuse']
assert set(x['path']for x in r['files'])=={U,P,CLOUD}
for f in r['files']:
 b=source(f['path']);assert b==git(C,'show',HEAD+':'+f['path']) and (len(b),h(b),blob(b))==(f['bytes'],f['sha256'],f['gitBlob'])
 assert git(C,'ls-tree',HEAD,'--',f['path']).split(b'\t')[0].decode().split()==[f['mode'],'blob',f['gitBlob']]
for root in (R,C):assert git(root,'rev-parse','HEAD').decode().strip()==HEAD and not git(root,'status','--porcelain')
out=dict(kind='INDEPENDENT_BORROWED_LEASE_SOURCE_FOCUSED_FINAL_CHECK',checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),candidate=HEAD,previousCandidate=PREV,baseline=BASE,priorCompleteCheck=ref(firstpath),preservedFeedback=ref(feedback),closedFinding='L01-BORROWED-LEASE-SOURCE-OBSERVATION-001',directReadMissingVsReadErrorExplicit=True,exactSingleSourceDeltaInverse=True,allOriginalUncertainAssertionsExactInverseToBaseline=True,priorSevenSourceAndControllerChecksRetainedByExactThreePathDelta=True,sourcePacketSha256=h(source(P)),sourcePinCount=98,sourcePinDigest=p['pinDigest'],changedPinSinceFirst=[U],productDeltaPaths=22,allCurrentPinsMatchBothCleanGitCheckouts=True,metadataConditionsSatisfied=['CURRENT_LEDGER_POINTER','CURRENT_CAUSAL_FIELDS'],currentCausalLimitsPreserved=True,whole=first['whole'],corrective=first['corrective'],remaining=first['remaining'],modes=first['modes'],retained=first['retained'],originalArchivesAndOldUnknownUnchanged=True,extraCompatibilityRuntimeRequired=False,recovery=dict(receipt=ref(rp),changedPaths=3,remoteExact=remote,fullIndependentGitTreesEqual=True,clean=True,noAlternates=True),namedZeroPlan=first['namedZeroPlan'],findings=[],eligibility='ACCEPT_SOURCE_AND_NAMED_ZERO4_ONLY_AFTER_SAVED_RECOVERED_ACCEPTANCE_READBACK; independent fresh PREP required before any actual.',limits='Focused read-only final review carries forward completed independent ef981 source/binding checks; no build/test/discovery/controller/observer/process/root action or repository edit.')
dest=D/'t179pd-ni-iobs-l01-borrowed-lease-source-independent-check-9fdecef3-20261018.json';dest.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(ref(dest)))
