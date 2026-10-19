from pathlib import Path
import subprocess,json,hashlib,ast,copy
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');V=Path('/tmp/1536-cloud-tools/review')
SHA='8b9d08f0242ebaec76e74a1fbe28351fd1f6681e';PREV='c0a05c8eef5f4de73fb03272bb1855090eaf60ab';BASE='46c621fb50fe61e5a662ccaf8f00f3fa0f3c6dc3';P='specs/1536-complete-wound-materialization/';REC=P+'recovery/';G=REC+'l01-game-capture-g02/';O=REC+'l01-game-capture-g01/'
H=lambda b:hashlib.sha256(b).hexdigest()
def git(*a):return subprocess.check_output(['git','-C',str(R),*a])
def raw(p,rev=SHA):return git('show',rev+':'+p)
checks=[]
def check(name,value,detail=None):
 checks.append(dict(name=name,passed=bool(value),detail=detail))
 if not value:raise AssertionError(name)
packet=json.loads(raw(G+'source-packet.json'));binding=json.loads(raw(G+'bindings.json'));control=json.loads(raw(G+'control.json'));priorBinding=json.loads(raw(G+'bindings.json',PREV))
rp=Path('/tmp/1536-cloud-tools/g02-recovery-8b9d08f0.json');receipt=json.loads(rp.read_text())
check('exact_saved_recovery',receipt['sha']==SHA and receipt['remoteShaVerified'] and receipt['clean'] and receipt['currentPinsVerified']==173 and receipt['changedPaths']==361)
check('canonical173digest',len(packet['sourcePins'])==173 and H(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['sourcePinDigest'])
check('173_saved_Git_source_pins',all(len(raw(x['path']))==x['bytes'] and H(raw(x['path']))==x['sha256'] for x in packet['sourcePins']))
check('173_independent_restore_pins',all((C/x['path']).read_bytes()==raw(x['path']) for x in packet['sourcePins']))
changes=git('diff','--name-only',PREV,SHA).decode().splitlines()
check('22_affected_delta_paths_feature_only',len(changes)==22 and all(x.startswith(P) for x in changes),changes)
check('22_affected_restore_bytes',all((C/x).read_bytes()==raw(x) for x in changes))
check('no_production_integration_or_catalog_change',not git('diff','--name-only',BASE,SHA,'--','.',':(exclude)'+P))
check('only_three_shared_g01_changes',set(x for x in git('diff','--name-only',BASE,SHA,'--',O).decode().splitlines())=={O+'watchdog.py',O+'Collect.ps1',O+'phase.py'})
check('historical_g01_packet_control_bindings_unchanged',all(raw(O+x)==raw(O+x,BASE) for x in ('source-packet.json','control.json','bindings.json')))
check('exact_three_current_g01_delta_hashes',len(packet['explicitG01Deltas'])==3 and all(H(raw(x['path'],BASE))==x['priorSha256'] and H(raw(x['path']))==x['sha256'] for x in packet['explicitG01Deltas']))
held=['l01-game-capture-g02-source-independent-review-20261019.json','l01-game-capture-g02-source-independent-review-20261019.md','l01-game-capture-g02-source-checks-20261019.json','check_g02_c0_source_20261019.py','check_g02_c0_source_20261019_v1.py','g02-c0-source-reader-history-20261019.json','g02-c0-source-reader-v1-failure-20261019.txt']
check('seven_original_hold_support_bytes_preserved',all(raw(REC+x)==(V/x).read_bytes() for x in held))
check('all_source_synthetic_actual_gates_hold',all(control[x]['status']=='HOLD' for x in ('sourceAdmission','syntheticAdmission','actualAdmission')))
check('separate_one_request_budgets',control['syntheticDiagnosticRequestUpperBound']==control['gameDiagnosticRequestUpperBound']==1 and binding['runId']!=binding['syntheticRunId'])
check('old_outcomes_limits_retained',control['oldG01']=='FAIL_TIMEOUT220_API0_ENOENT_UNCHANGED' and control['old66529']=='UNKNOWN' and control['l01'].startswith('52/57'))
check('unchanged_unit_metadata_reuse_oracle_bindings',all(binding[k]==priorBinding[k] for k in ('unit','metadata','reuse','oracleFiles','oracleMvid','oraclePrepareRecord','oracleCompiledSource','unchangedUnitTree')))
deltas=[(a,b) for a,b in zip(priorBinding['inputs'],binding['inputs']) if a!=b]
check('395_inputs_only_current_watchdog_collect_deltas',len(binding['inputs'])==len(priorBinding['inputs'])==395 and len(deltas)==2 and {b['path'] for a,b in deltas}=={str(R/O/'watchdog.py'),str(R/O/'Collect.ps1')} and all(a['path']==b['path'] and H(raw(str(Path(b['path']).relative_to(R))))==b['sha256'] for a,b in deltas))
wd=raw(O+'watchdog.py').decode();phase=raw(O+'phase.py').decode();ps=raw(O+'Collect.ps1').decode();owner=raw(G+'SyntheticOwner.cs').decode();oldwd=raw(O+'watchdog.py',PREV).decode()
# Only exact source functions and inert dictionaries; no native/process/procfs execution.
role=next(x for x in ast.parse(wd).body if isinstance(x,ast.FunctionDef) and x.name=='request_role');ns={'Path':Path};exec(compile(ast.Module(body=[role],type_ignores=[]),'<reviewed-role>','exec'),ns)
cfg=dict(Dotnet='/dotnet',Runtimeconfig='/r',Depsfile='/d',SupportAssembly='/s',IntegrationAssembly='/i',OracleAssembly='/o',OracleRuntimeconfig='/or',OracleContract='S01_G01_ONE_CAPTURE');m=dict(Own='/own',Root='/own/tmp/r',Cut=1,Nonce='a'*32)
real=['/dotnet','exec','--runtimeconfig','/r','--depsfile','/d','/s','engine-joint-interface-resume','/i',m['Root'],'/own/resume.json','1','/own'];syn=['/dotnet','exec','--runtimeconfig','/or','/o',m['Nonce']]
proof=[]
for name,flag,cmd,contract,want in [('real',False,real,cfg['OracleContract'],True),('synthetic',True,syn,cfg['OracleContract'],True),('missing',None,real,cfg['OracleContract'],False),('string','false',real,cfg['OracleContract'],False),('wrong-contract',True,syn,'WRONG',False),('synthetic-real',True,real,cfg['OracleContract'],False),('real-synthetic',False,syn,cfg['OracleContract'],False)]:
 c=dict(cfg,OracleContract=contract)
 if flag is not None:c['SyntheticOracle']=flag
 try:ns['request_role'](c,dict(m,TargetCommand=cmd));got=True
 except (KeyError,AssertionError):got=False
 proof.append(dict(name=name,passed=got==want))
check('exact_role_seven_inert_cases',all(x['passed'] for x in proof),proof)
node=next(x for x in ast.parse(phase).body if isinstance(x,ast.FunctionDef) and x.name=='validate_synthetic');vn={};exec(compile(ast.Module(body=[node],type_ignores=[]),'<reviewed-validator>','exec'),vn)
valid=dict(sourcePinDigest='digest',status='SYNTHETIC_CAPTURE_PASSED_API1',nativeExit=0,observerErrors=[],afterLiveTools=[],decodedOracleFrames=[1,2,3,4],syntheticOutcome=dict(runId='run',status='SYNTHETIC_CAPTURE_PASSED_API1',observerErrors=[],apiCalls=1,watchdog=dict(apiOutcome='RETURNED',diagnosticRequests=1),targetNativeExit=0,watchdogNativeExit=0,targetJoined=True,watchdogJoined=True,stdoutEof=True,stderrEof=True,done=dict(ValidStop=True)))
proof=[]
for name,keys,value,want in [('success',(),None,True),('source',('sourcePinDigest',),'other',False),('run',('syntheticOutcome','runId'),'other',False),('native',('nativeExit',),1,False),('api0',('syntheticOutcome','apiCalls'),0,False),('frames',('decodedOracleFrames',),[1,2,3],False),('eof',('syntheticOutcome','stdoutEof'),False,False),('missing',('syntheticOutcome',),None,False)]:
 e=copy.deepcopy(valid)
 if keys:
  p=e
  for k in keys[:-1]:p=p[k]
  if value is None:del p[keys[-1]]
  else:p[keys[-1]]=value
 try:vn['validate_synthetic'](e,dict(syntheticRunId='run'),'digest');got=True
 except (KeyError,AssertionError):got=False
 proof.append(dict(name=name,passed=got==want))
check('exact_actual_evidence_validator_eight_inert_cases',all(x['passed'] for x in proof),proof)
check('synthetic_evidence_exact_hash_review_path_before_launch',all(t in phase for t in ("evidence=gate['syntheticEvidence'];assert evidence==review['syntheticEvidence'];verify(evidence)","Path(evidence['path'])==ROOT/'synthetic/record.json'","validate_synthetic(se,cfg,packet['sourcePinDigest'])")) and phase.index("validate_synthetic(se,cfg,packet['sourcePinDigest'])")<phase.index("before,visibility=b01['proc_rows']()"))
check('separate_consumed_permits_and_actual_false',"mode=='synthetic':assert gate['runId']==cfg['syntheticRunId']==review['plannedSyntheticRunId'] and gate['lease']=='CONSUMED_FOR_NAMED_RUN_NO_RETRY'" in phase and "if mode=='actual':assert gate['lease']=='CONSUMED_FOR_NAMED_RUN_NO_RETRY'" in phase and 'SyntheticOracle=False,RunId=' in phase)
check('real_role_and_watchdog_cleanup_unchanged_except_role_rename',wd[wd.index('def main():'):].replace('SyntheticOracle','SyntheticPreApiOracle')==oldwd[oldwd.index('def main():'):])
check('single_common_API_after_final_guard_no_synthetic_earlyreturn',ps.count('[void]$write.Invoke(')==1 and ps.index('$cancel = Test-AdmissionHealth')<ps.index('if ($m.SyntheticOracle)')<ps.index('$result = Invoke-ObservedApi -Request') and 'elseif ($m.SyntheticOracle)' not in ps and "SyntheticContract = 'S01_G01_ONE_CAPTURE'" in ps)
check('S01_launch_accounting_precedes_identity',owner.index('record["targetLaunches"]=1')<owner.index('var targetId=Identity(target.Id)') and owner.index('record["watchdogLaunches"]=1')<owner.index('watchdogTick=Identity(watchdog.Id)'))
check('S01_direct_owned_fallback_is_explicit_and_bounded','else record[item.Name+"Identity"]= "UNKNOWN_DIRECT_CHILD_FALLBACK_ONLY";' in owner and 'Require(Stopwatch.GetTimestamp()<cleanup,"Cleanup budget exhausted");item.P.Kill();forced=true;' in owner)
check('S02_native_recording_now_precedes_drains_assertions',owner.index('record["watchdogNativeExit"]=watchdog.ExitCode')<owner.index('string watchOut=await wo') and owner.index('record["targetNativeExit"]=target.ExitCode')<owner.index('await stdoutDrain.WaitAsync'))
check('S02_natural_and_forced_exit_reconciliation_separate','if(item.P.HasExited)' in owner and 'if(forced)record[item.Name+"CleanupExit"]=item.P.ExitCode;' in owner and 'else if(record[item.Name+"NativeExit"]==null)record[item.Name+"NativeExit"]=item.P.ExitCode;' in owner)
check('remaining_S02_unguarded_final_publication', 'Write(Path.Combine(own,"record.json"),record);\n        }' in owner and 'Console.Error.WriteLine(e);return 1;' in owner and owner.count('Write(Path.Combine(own,"record.json"),record)')==1)
check('synthetic_deadline_before_launch60_and_outer45','deadline=launched+60*Stopwatch.Frequency' in owner and owner.index('deadline=launched+60*Stopwatch.Frequency')<owner.index('target=Start(') and '>=45,"Insufficient synthetic60 window"' in owner and "'one-synthetic-capture',start+45" in phase)
check('positive_request_return_core_report_and_stop_eof_required',all(t in owner for t in ('N(wr,"collectorLaunches")==1','N(wr,"diagnosticRequests")==1 && S(wr,"apiOutcome")=="RETURNED"','new FileInfo(dump).Length>0 && new FileInfo(report).Length>0','done.GetProperty("ValidStop").GetBoolean()','await stdoutDrain.WaitAsync','record["stdoutEof"]=true;record["stderrEof"]=true;')))
check('original_S01_decode_source_unchanged_and_fourframes_required',raw(REC+'l01-managed-stack-s01/smoke.py')==raw(REC+'l01-managed-stack-s01/smoke.py',BASE) and "lib['decode'](read(report),so['ready'],cfg['oracleMvid'])" in phase and "len(record['decodedOracleFrames'])==4" in phase)
check('retained_binaries_source_graph_and_separate_closures',"outside(source)==outside(reuse['integrationCompiledSource'])" in phase and "for name,rows in old['privateBinClosures'].items()" in phase and "record['integrationCompiledSource']=source if scope=='G01' else cfg['reuse']['integrationCompiledSource']" in phase)
check('new_owner_only_compile_for_G02',"compile_tool('SyntheticOwner',[config_dir/'SyntheticOwner.cs'])" in phase and "if scope=='G01':zero(build,'fresh-integration-build',deadline)" in phase)
check('fresh_private_binding_catalog_plan_ZERO',all(t in phase for t in ("'-getProperty:'+props","'-preprocess:'+str(pp)","['-ValidateCatalog','-NoBuild']","'-PlanOnly','-NoBuild'","s['Tests']['Executed']==0","deadline=start+600")))
check('parent6_health26_and_retained_policy23_guards',all(t in phase for t in ('parent[\'passed\']==len(parent[\'cases\'])==6',"health['Passed']==len(health['Cases'])==26","policy['results']['passed']==23","reexecuted=False,sourceImagesVerified=True")))
check('same_game_case_original_900_controller_bound',"METHOD='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss'" in phase and "'one-game-case',start+900" in phase)
check('no_core_rawreport_publication_or_auto_delete_in_new_owner','File.Delete(' not in owner and 'Directory.Delete(' not in owner and 'privateReportPath' in owner and 'privateDumpPath' in owner)
result=dict(kind='G02_CAPTURE_AFFECTED_SOURCE_READONLY_CHECKS',reviewedSource=SHA,previousSource=PREV,base=BASE,sourcePinDigest=packet['sourcePinDigest'],controllerSha256=H(raw(O+'phase.py')),bindingsSha256=H(raw(G+'bindings.json')),checks=checks,passed=sum(x['passed'] for x in checks),failed=0,recoveryReceipt=dict(path=str(rp),sha256=H(rp.read_bytes())),sourceReadSet={p:dict(bytes=len(raw(p)),sha256=H(raw(p))) for p in changes},executionScope='Static source/Git/hash reads plus actual request_role/validate_synthetic AST with inert dictionaries. No SDK/C#/process target/API/procfs or signal actions. Defect-presence flags are not acceptance.')
p=V/'l01-game-capture-g02-capture-source-checks-20261019.json';p.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(path=str(p),sha256=H(p.read_bytes()),passed=result['passed'],failed=0,sourcePinDigest=result['sourcePinDigest'],controllerSha256=result['controllerSha256'],bindingsSha256=result['bindingsSha256'])))
