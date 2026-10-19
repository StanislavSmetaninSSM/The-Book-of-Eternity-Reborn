from pathlib import Path
import subprocess,json,hashlib,ast
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');V=Path('/tmp/1536-cloud-tools/review')
SHA='c0a05c8eef5f4de73fb03272bb1855090eaf60ab';BASE='46c621fb50fe61e5a662ccaf8f00f3fa0f3c6dc3';P='specs/1536-complete-wound-materialization/';G=P+'recovery/l01-game-capture-g02/';O=P+'recovery/l01-game-capture-g01/'
H=lambda b:hashlib.sha256(b).hexdigest()
def git(*a):return subprocess.check_output(['git','-C',str(R),*a])
def raw(p,rev=SHA):return git('show',rev+':'+p)
checks=[]
def check(name,value,detail=None):
 checks.append(dict(name=name,passed=bool(value),detail=detail))
 if not value:raise AssertionError(name)
packet=json.loads(raw(G+'source-packet.json'));binding=json.loads(raw(G+'bindings.json'));control=json.loads(raw(G+'control.json'))
receiptpath=Path('/tmp/1536-cloud-tools/g02-recovery-c0a05c8e.json');receipt=json.loads(receiptpath.read_text())
check('exact_saved_recovery',receipt['sha']==SHA and receipt['remoteShaVerified'] and receipt['clean'] and receipt['currentPinsVerified']==170)
check('source_digest',H(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['sourcePinDigest'])
check('170_exact_git_pins',len(packet['sourcePins'])==170 and all(len(raw(x['path']))==x['bytes'] and H(raw(x['path']))==x['sha256'] for x in packet['sourcePins']))
check('170_exact_restore_pins',all((C/x['path']).read_bytes()==raw(x['path']) for x in packet['sourcePins']))
changes=git('diff','--name-only',BASE,SHA).decode().splitlines()
check('15_changed_feature_only_paths',len(changes)==15 and all(x.startswith(P) for x in changes),changes)
check('changed_path_restore_matches',all((C/x).read_bytes()==raw(x) for x in changes))
check('only_three_shared_g01_deltas',set(x for x in changes if x.startswith(O))=={O+'Collect.ps1',O+'watchdog.py',O+'phase.py'})
check('three_g01_deltas_exact',len(packet['explicitG01Deltas'])==3 and all(H(raw(x['path'],BASE))==x['priorSha256'] and H(raw(x['path']))==x['sha256'] for x in packet['explicitG01Deltas']))
check('old_g01_packet_control_unchanged',all(raw(O+x)==raw(O+x,BASE) for x in ('source-packet.json','control.json','bindings.json')))
check('no_production_csharp_catalog_selection_changes',not git('diff','--name-only',BASE,SHA,'--','BookOfEternityClient','BookOfEternityClient.IntegrationTests','BookOfEternityClient.TestSupport','tests'))
check('all_current_gates_HOLD',all(control[k]['status']=='HOLD' for k in ('sourceAdmission','syntheticAdmission','actualAdmission')))
check('historical_outcome_scope_retained',control['oldG01']=='FAIL_TIMEOUT220_API0_ENOENT_UNCHANGED' and control['old66529']=='UNKNOWN' and control['l01'].startswith('52/57'))
old_binding=json.loads(raw(O+'bindings.json',BASE))
check('395_installed_inputs_and_unit_metadata_unchanged',binding['inputs']==old_binding['inputs'] and len(binding['inputs'])==395 and binding['unit']==old_binding['unit'] and binding['metadata']==old_binding['metadata'])
check('retained_g01_record_bindings',binding['reuse']['prepareRecord']['sha256']=='cc1bdf563029fd4452ba5d7fb9b69ade452cf23c545af588c8b6f9bc9a4903f4' and binding['reuse']['toolsRecord']['sha256']=='410857f197e1da419f110f9f52eacbe751543c590994e81aa4498ea327e81d12')
check('retained_record_bytes',all(H(Path(binding['reuse'][k]['path']).read_bytes())==binding['reuse'][k]['sha256'] for k in ('prepareRecord','toolsRecord')))
check('retained_s01_oracle_images',all(H(Path(x['path']).read_bytes())==x['sha256'] and Path(x['path']).stat().st_size==x['bytes'] for x in binding['oracleFiles']))
check('distinct_named_roles',binding['runId']!=binding['syntheticRunId'] and binding['root']=='/tmp/g2-2aa1d92a')
wd=raw(O+'watchdog.py').decode();phase=raw(O+'phase.py').decode();ps=raw(O+'Collect.ps1').decode();owner=raw(G+'SyntheticOwner.cs').decode()
# Execute ONLY the exact source request_role function, with inert dictionaries/Path construction.
node=next(n for n in ast.parse(wd).body if isinstance(n,ast.FunctionDef) and n.name=='request_role');ns={'Path':Path};exec(compile(ast.Module(body=[node],type_ignores=[]),'<reviewed-request_role>','exec'),ns)
cfg=dict(Dotnet='/dotnet',Runtimeconfig='/r',Depsfile='/d',SupportAssembly='/s',IntegrationAssembly='/i',OracleAssembly='/o',OracleRuntimeconfig='/or',OracleContract='S01_G01_PREAPI_ZERO_REQUESTS');m=dict(Own='/own',Root='/own/tmp/r',Cut=1,Nonce='a'*32)
real=['/dotnet','exec','--runtimeconfig','/r','--depsfile','/d','/s','engine-joint-interface-resume','/i',m['Root'],'/own/resume.json','1','/own'];syn=['/dotnet','exec','--runtimeconfig','/or','/o',m['Nonce']]
roles=[]
for name,flag,command,contract,expected in [('real',False,real,cfg['OracleContract'],True),('synthetic',True,syn,cfg['OracleContract'],True),('missing',None,real,cfg['OracleContract'],False),('string','false',real,cfg['OracleContract'],False),('wrong-contract',True,syn,'WRONG',False),('synthetic-real-argv',True,real,cfg['OracleContract'],False),('real-synthetic-argv',False,syn,cfg['OracleContract'],False)]:
 c=dict(cfg,OracleContract=contract)
 if flag is not None:c['SyntheticPreApiOracle']=flag
 try:ns['request_role'](c,dict(m,TargetCommand=command));actual=True
 except (AssertionError,KeyError):actual=False
 roles.append(dict(name=name,passed=actual==expected))
check('exact_role_inert_seven_cases',all(x['passed'] for x in roles),roles)
check('common_guard_before_synthetic_branch',ps.index('$cancel = Test-AdmissionHealth')<ps.index('elseif ($m.SyntheticPreApiOracle)')<ps.index('$result = Invoke-ObservedApi -Request'))
check('real_controller_explicit_false',"SyntheticPreApiOracle=False,RunId=gate['runId']" in phase)
check('synthetic_positive_ready_not_exit_only','events.Count(x=>S(x,"Stage")=="pre_api_ready")==1' in owner and '!events.Any(x=>S(x,"Stage")=="request_started")' in owner)
check('existing_case_and_budgets_retained',"METHOD='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss'" in phase and "'one-game-case',start+900" in phase and "deadline=start+600" in phase)
check('reuse_separate_closure_and_graph_guards',"outside(source)==outside(reuse['integrationCompiledSource'])" in phase and "for name,rows in old['privateBinClosures'].items()" in phase and "d02['mirror']" in phase)
check('fresh_properties_imports_catalog_plan_guards',all(t in phase for t in ("'-getProperty:'+props","'-preprocess:'+str(pp)","['-ValidateCatalog','-NoBuild']","'-PlanOnly','-NoBuild'","s['Tests']['Executed']==0")))
check('finding_S01_identity_can_precede_tick_and_fallback_refuses',all(t in owner for t in ('target=Start(command[0],command.Skip(1),own,tmp); var targetId=Identity(target.Id); targetTick=targetId.Tick;','watchdogTick=Identity(watchdog.Id).Tick;','id.Tick==item.Tick && id.Parent==owner','long targetTick=0,watchdogTick=0;')))
check('finding_S02_native_record_after_fallible_observers',owner.index('File.WriteAllText(Path.Combine(capture,"watchdog.stdout")')<owner.index('record["watchdogNativeExit"]') and owner.index('Require(target.ExitCode==0')<owner.index('record["targetNativeExit"]'))
actual=phase.split("elif mode=='actual':",1)[1].split('\n        else:',1)[0]
check('finding_S03_no_positive_synthetic_evidence_in_actual',"check_prep()" in actual and 'preapi' not in actual and 'syntheticOutcome' not in actual and 'preApiEvidence' not in phase and "r['status']=='PREPARED_'+scope+'_ZERO_ONLY'" in phase)
readset={p:dict(bytes=len(raw(p)),sha256=H(raw(p))) for p in changes}
result=dict(kind='G02_C0_AFFECTED_SOURCE_READONLY_CHECKS',reviewedSource=SHA,base=BASE,sourcePinDigest=packet['sourcePinDigest'],controllerSha256=H(raw(O+'phase.py')),bindingsSha256=H(raw(G+'bindings.json')),checks=checks,passed=sum(x['passed'] for x in checks),failed=0,sourceReadSet=readset,recoveryReceipt=dict(path=str(receiptpath),sha256=H(receiptpath.read_bytes())),executionScope='Read-only Git/files/hash and exact request_role AST with inert dictionaries only. No C#/SDK/native child/API/procfs/runtime actions. Finding checks confirm presence of defects, not acceptance.',scopeUpdate='Later parent/user steering requires actual synthetic WriteDump plus readable/decodeable S01 frames; reviewed API0 proposal is superseded and cannot admit execution.')
out=V/'l01-game-capture-g02-source-checks-20261019.json';out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(path=str(out),sha256=H(out.read_bytes()),passed=result['passed'],failed=0,controller=result['controllerSha256'],digest=result['sourcePinDigest'])))
