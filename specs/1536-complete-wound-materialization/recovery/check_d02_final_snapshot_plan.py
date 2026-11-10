"""Bounded read-only acceptance-copy and conditional-plan source checks."""
from pathlib import Path
import hashlib, json, subprocess
REPO=Path('/workspace/The-Book-of-Eternity-Reborn')
CLONE=Path('/workspace/1536-cloud-clean-restore')
REC=REPO/'specs/1536-complete-wound-materialization/recovery'
OUT=Path('/tmp/1536-cloud-tools/review')
CANDIDATE='f2f835c13056c87df5c25faf85ba053f560a2079'
BASE='be89497c746d8393470d2ff9789c262c1d60ed5a'
checks=[]
def sha(data):return hashlib.sha256(data).hexdigest()
def read(p):return Path(p).read_bytes()
def obj(p):return json.loads(read(p))
def git(*args,cwd=REPO):return subprocess.check_output(['git','-C',str(cwd),*args])
def check(name,value):checks.append({'name':name,'passed':bool(value)})
plan_path=REC.parent/'design/l01-future-custody-accepted-single-run-plan-20261018.md'
plan=plan_path.read_text();control=obj(REC/'l01-delivery-d02/control.json')
receipt_path=Path('/tmp/1536-cloud-tools/d01-carrier-recovery-f2f835c1.json');receipt=obj(receipt_path)
check('exact reviewed plan11321B/SHA',len(read(plan_path))==11321 and sha(read(plan_path))=='d92dda57efddd32f1537b3d46bbd8b5a1b8b846a43cad3bef43fd619ffb402a0')
check('both current HEADs exact clean',all(git('rev-parse','HEAD',cwd=p).decode().strip()==CANDIDATE and not git('status','--porcelain',cwd=p).strip() for p in [REPO,CLONE]))
check('exact independent GitHub recovery receipt',receipt['sha']==CANDIDATE and receipt['remoteShaVerified'] and receipt['clean'] and not receipt['localWriterObjectsCopied'] and receipt['changedPaths']==168 and receipt['currentPinsVerified']==149 and receipt['historicalLedgersUnchanged'] and not receipt['productionChanges'])
changed=git('diff','--name-only',BASE,CANDIDATE).decode().splitlines()
check('ten metadata/review-only changed paths',len(changed)==10 and all(x.startswith('specs/1536-complete-wound-materialization/') for x in changed))
check('all ten changed paths exact Git/independent clone',all(read(REPO/p)==read(CLONE/p)==git('show',CANDIDATE+':'+p) for p in changed))
copies={
 'l01-delivery-d02-synthetic-independent-review-20261018.json':'1bdc0c5b159b4b27f2f6ffa99e68d4ce184401f2230a57384cac990428a25cef',
 'l01-delivery-d02-synthetic-independent-review-20261018.md':'b7283bd493d52fbaf955443f1088c0efc4c84128f5634bcef40a3506d48d3e1b',
 'l01-delivery-d02-synthetic-independent-checks-20261018.json':'0d3e6c0c226e71cc098c27c263e4595402aa4615e87a40b3660a1d6bb69b2cd7',
 'check_d02_synthetic_evidence.py':'a4aff5df945f4107fef2aee02a3512bfe32bb3aad3e9a30ecd89a0de94e35c31'}
for name,h in copies.items():check('byte-exact accepted artifact '+name,sha(read(REC/name))==h and read(REC/name)==read(OUT/name))
evidence=obj(REC/'l01-delivery-d02-synthetic-independent-review-20261018.json');proof=obj(REC/'l01-delivery-d02-synthetic-independent-checks-20261018.json')
check('accepted73 checks and narrow verdict retained',proof['passed']==73 and not proof['findings'] and all(x['passed'] for x in proof['checks']) and evidence['verdict']=='ACCEPT_D01_DELIVERY_OWN_SYNTHETIC_0_7_ONLY')
check('EVIDENCE control binds exact report and consumed no retry',control['synthetic']['status']=='CONSUMED_EVIDENCE_ACCEPTED_NO_RETRY' and control['synthetic']['retryRelease']=='NONE' and control['synthetic']['evidenceReviewSha256']==copies['l01-delivery-d02-synthetic-independent-review-20261018.json'] and control['synthetic']['evidenceReviewPath']==str((REC/'l01-delivery-d02-synthetic-independent-review-20261018.json').relative_to(REPO)))
check('all native gates closed and doc review only',control['source']['status']=='SOURCE_ACCEPTED_ZERO_PERMITS_CONSUMED_PREP_ACCEPTED' and control['gameplayRelease']==control['snapshotRelease']==control['crash5Release']=='NONE' and control['deliveryQualification']=='ACCEPTED_OWN_SYNTHETIC_0_7_ONLY' and control['futureSnapshotPlanStatus']=='DOCUMENTATION_CANDIDATE_PENDING_FOCUSED_REVIEW')
packet=obj(REC/'l01-delivery-d01-source-packet-20261018.json')
check('149 current pins exact and source packet unchanged',len(packet['sourcePins'])==149 and all(len(read(REPO/p['path']))==p['bytes'] and sha(read(REPO/p['path']))==p['sha256'] for p in packet['sourcePins']) and read(REC/'l01-delivery-d01-source-packet-20261018.json')==git('show',BASE+':'+str((REC/'l01-delivery-d01-source-packet-20261018.json').relative_to(REPO))))
for name in ['l01-delivery-d02-synthetic-first-20261018.json','l01-delivery-d02-synthetic-first-originals-20261018.zip','l01-delivery-d02/phase.py','l01-delivery-d02/bindings.json']:
 check('original evidence/tool unchanged '+name,read(REC/name)==git('show',BASE+':'+str((REC/name).relative_to(REPO))))
crash=(REPO/'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrash.cs').read_text();lines=crash.splitlines()
diagnostic=(REPO/'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrashDiagnostic.cs').read_text()
delivery=(REPO/'BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs').read_text()
check('exact real argument slots113/114', 'string[] arguments = ["exec", "--runtimeconfig", paths["runtimeconfig"], "--depsfile", paths["depsfile"], paths["support"],' in lines[112] and 'role, paths["integration"], root, output, cut.ToString(CultureInfo.InvariantCulture), own];' in lines[113])
check('nonce originates in parent file45/46 and independent context', 'parent.json' in lines[44] and 'Nonce = nonce, ParentPid = Environment.ProcessId, Cut = cut, Fault = fault, Diagnostic = diagnostic' in lines[45] and 'deliveryNonce: nonce' in crash)
check('comment137 and unchanged220 wait139 source anchors', 'Reader/ReadHealth are live here' in lines[136] and 'await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(220));' in lines[138])
check('exact diagnostic Fact is separate cut1 entry', 'public Task GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss()' in diagnostic and '=> RunJointInterfaceActualProcessLossAsync(1, diagnostic: true);' in diagnostic)
check('available queue/health/residence APIs',all(s in delivery for s in ['internal ChannelReader<Event> Reader => _events.Reader;', 'internal Health ReadHealth()', 'internal static QueueTiming QueueResidence(Event item, TimeSpan delayedAfter)', 'Stopwatch.GetElapsedTime(item.ReceivedTimestamp)']))
check('typed source custody/accepted/late controls and sticky flags',all(s in delivery for s in ['stage == "finalization-return" && detail == Custody','control = "CUSTODY_RETURN"','stage is "finalization-enter" or "finalization-return" && detail == Accepted','control = "ACCEPTED_MARKER"','stage == "late-first-return"','control = "LATE_RETURN"']))
check('plan is conditional future implementation not admission',all(s in plan for s in ['DOCUMENTATION ONLY; GAMEPLAY CLOSED','requires separate SOURCE/fresh affected PREP','This plan alone is not that admission.','STOP BEFORE GAMEPLAY']))
check('exact existing cut1 only and outside original57',all(s in plan for s in ['fr063-joint-interface-crash-diagnostic','cut1','It is outside original57.','No original-five theory, other cut, full suite, Windows, CI or real saves.']))
check('owning parent consumer concurrent unchanged deadline',all(s in plan for s in ['original owning parent','wait starts promptly and runs concurrently','never await the collector before starting it','Bind the single monotonic deadline','accounting for setup overhead']))
check('real nonce/argv role predicate not S01 substitution',all(s in plan for s in ['engine-joint-interface-resume <paths.integration> <root> <output> 1 <own>','it is not an argv nonce','Do not blindly reuse','private parent/cut identity']))
check('typed custody identity and zero unhealthy counters',all(s in plan for s in ['Control == "CUSTODY_RETURN"','Disposition == "IDENTITY_ORDERED"','ControlDetail == "MortalJointCustodyCaptureCompletedBeforePreparation"','loss/malformed/oversize/incomplete/identity/','duplicate/regression/tracking counters must be zero']))
check('60 timer uses receipt and1s local residence not source age',all(s in plan for s in ['QueueResidence(event, TimeSpan.FromSeconds(1))','local residence must be <=1s when arming','receipt (`ReceivedTimestamp`), not dequeue','its age is not a phase or freshness','silence proves no emission']))
check('sticky cancellation before arming and admission',all(s in plan for s in ['finalization-enter','AcceptedOutcomeValidatedBeforeMaterialization','LateReturnSeen','including immediately before','arming and the one collector admission','any newly stale event cancels']))
check('single CAS and25s remaining preserve20/5/220/900',all(s in plan for s in ['at least25s left','collector plus5s owned cleanup','pause/reset/extend child220s or category900s','one CAS admits one','No sampling, second capture, fallback, reset timer or earlier retry.']))
check('external watchdog exact createdump accounting and retained blocker',all(s in plan for s in ['external parent watchdog','independently of suspended','known target\'s task/children','revalidate PID/starttick/UID/PPID/exe/private dump','Do not assume collector exit resumes the target','retain disposable root']))
check('native/observer/API reach/cleanup independent existing accounting',all(s in plan for s in ['capture_native','FINALIZE','Native outcome, observer error,','API reach, collector exit and cleanup status are distinct','incomplete output keeps reach UNKNOWN']))
check('capture race and suspension/latency epistemic limits',all(s in plan for s in ['even if accepted arrives during collection','Capture\nphase is UNKNOWN','API-only/suspension time remains UNKNOWN','not a baseline result','Source→receipt latency and source\nemission completeness are still UNKNOWN']))
check('useful bounded evidence and privacy/no causal overclaim',all(s in plan for s in ['MVID/MethodDef/','IL/native-TID','cannot establish continuous progress','deadlock owner/async causality','Keep full core and raw crash report private','No memory/register addresses']))
check('one original outcome then STOP no diagnostic chain',all(s in plan for s in ['End after the first original diagnostic outcome','lack of useful frames does not release another run','No crash5 requalification or','three separate cloud/\nfuture-Windows-five/artifact403 holds remain.']))
check('top handoff synthetic only and original holds intact',all(s in (REC.parent/'plan.md').read_text().split('## L01 D01 first synthetic0/7')[0] for s in ['qualified only within SOURCE/composite PREP/ownsynthetic scope','retry/gameplay/snapshot/crash5 NONE','old66529UNKNOWN']))
check('final plan task remains incomplete pending this review','- [ ] Final saved one-game-snapshot plan; STOP before gameplay/crash5.' in (REC.parent/'tasks.md').read_text())
result={'kind':'D02_FINAL_CONDITIONAL_SNAPSHOT_PLAN_DOCUMENTATION_CHECKS','candidate':CANDIDATE,'base':BASE,'planPath':str(plan_path.relative_to(REPO)),'planSha256':sha(read(plan_path)),'passed':sum(x['passed'] for x in checks),'findings':[x for x in checks if not x['passed']],'checks':checks,'copiedArtifacts':copies,'sourceAnchorClarification':'Crash.cs137 is the live-consumer comment;139 is WaitForExit. Change citation to137–139 in acceptance metadata save.','nativeActions':0,'procfsReads':0,'repositoryWrites':0,'recoveryReceiptSha256':sha(read(receipt_path)),'checkerSha256':sha(read(__file__))}
p=OUT/'l01-final-snapshot-plan-independent-checks-20261018.json';p.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({'path':str(p),'sha256':sha(read(p)),'passed':result['passed'],'findings':result['findings']}))
