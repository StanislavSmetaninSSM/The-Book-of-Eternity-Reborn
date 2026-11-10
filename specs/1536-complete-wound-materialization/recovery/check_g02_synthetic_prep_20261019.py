from pathlib import Path
import subprocess,json,hashlib,zipfile,io,re,stat
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');V=Path('/tmp/1536-cloud-tools/review');ROOT=Path('/tmp/g2-2aa1d92a')
SHA='5ac48a247270abc82ef9fb0fe77ef03ee7f1c23f';SRC='913453c077fd9348b51cb3462c8bed5a658dfbde';COMPILED='658a2251a7fbeefd57baf872f867abbfec5c5c78';P='specs/1536-complete-wound-materialization/recovery/';G=P+'l01-game-capture-g02/';O=P+'l01-game-capture-g01/';H=lambda b:hashlib.sha256(b).hexdigest()
def git(*a):return subprocess.check_output(['git','-C',str(R),*a])
def raw(p,rev=SHA):return git('show',rev+':'+p)
def same(row,path=None):
 p=Path(path or row['path']);b=p.read_bytes();return len(b)==row['bytes'] and H(b)==row['sha256']
flags=[]
def ck(name,value,detail=None):
 flags.append(dict(name=name,passed=bool(value),detail=detail))
 if not value:raise AssertionError(name)
idxpath=P+'l01-game-capture-g02-zero-prep-first-20261019.json';idx=json.loads(raw(idxpath));ap=idx['archive']['path'];archive=raw(ap);z=zipfile.ZipFile(io.BytesIO(archive));rec=json.loads(z.read('prepare/record.json'));tools=json.loads(z.read('tools/record.json'));packet=json.loads(raw(G+'source-packet.json'));cfg=json.loads(raw(G+'bindings.json'));ctl=json.loads(raw(G+'control.json'));rrp=Path('/tmp/1536-cloud-tools/g02-recovery-5ac48a24.json');rr=json.loads(rrp.read_text())
ck('exact_remote_clean_independent_recovery',rr['sha']==SHA and rr['remoteShaVerified'] and rr['clean'] and rr['changedPaths']==371 and rr['currentPinsVerified']==173)
ck('source_packet_controller_bindings_unchanged_since_SOURCE',all(raw(p)==raw(p,SRC) for p in (G+'source-packet.json',G+'bindings.json',G+'SyntheticOwner.cs',O+'phase.py',O+'watchdog.py',O+'Collect.ps1')))
ck('173_canonical_source_digest',len(packet['sourcePins'])==173 and H(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['sourcePinDigest'])
ck('current173pins_Git_local_restore',all(len(raw(x['path']))==x['bytes'] and H(raw(x['path']))==x['sha256'] and (R/x['path']).read_bytes()==raw(x['path'])==(C/x['path']).read_bytes() for x in packet['sourcePins']))
ck('archive_index_Git_local_restore',raw(idxpath)==(R/idxpath).read_bytes()==(C/idxpath).read_bytes() and archive==(R/ap).read_bytes()==(C/ap).read_bytes())
ck('archive_exact_size_sha',len(archive)==idx['archive']['bytes']==537294 and H(archive)==idx['archive']['sha256']=='a11339d814431dd4d6713bcb900abf181a222e757bd6c71e8af181d31e3b6441')
ck('47_unique_archive_members_no_extra',len(z.namelist())==len(set(z.namelist()))==len(idx['archive']['files'])==47 and set(z.namelist())=={x['name'] for x in idx['archive']['files']})
chains=[]
for x in idx['archive']['files']:
 n=x['name'];b=z.read(n)
 if n.startswith('runner'):
  sub,name=n.split('/',1);local=Path(rec['runnerRoots'][int(sub[6:])])/name
 else:local=ROOT/n
 chains.append(dict(name=n,path=str(local),bytes=len(b),sha256=H(b),passed=len(b)==x['bytes'] and H(b)==x['sha256'] and local.read_bytes()==b))
ck('47_original_local_zip_git_restore_chains',all(x['passed'] for x in chains))
ck('exact_prepare_tools_record_hashes',same(idx['privatePrepareRecord']) and same(idx['privateToolsRecord']) and H(z.read('prepare/record.json'))==idx['privatePrepareRecord']['sha256']=='77b5e37e8740da928ad245ba3cc4ab358f5af03f0a3a362eeb813236900b632e' and H(z.read('tools/record.json'))==idx['privateToolsRecord']['sha256']=='1107aab0f5504be7c1193e9b1c51834ed0221028b444b089bdd7819ba5707e8f')
release=json.loads(raw(G+'control.json',COMPILED));reviewbytes=raw(release['sourceAdmission']['reviewPath'],COMPILED);review=json.loads(reviewbytes)
ck('saved_SOURCE_release_before_first_ZERO',release['sourceAdmission']['status']=='SOURCE_ACCEPTED_G02_ZERO_ONLY' and H(reviewbytes)==release['sourceAdmission']['reviewSha256'] and review['verdict']=='ACCEPT_G02_AFFECTED_SOURCE_ZERO_ONLY' and review['sourcePinDigest']==packet['sourcePinDigest'] and review['controllerSha256']==H(raw(O+'phase.py')))
ck('PREP_revision_clean_expected_source',rec['source']==idx['source']==COMPILED and rec['sourcePinDigest']==packet['sourcePinDigest'] and all(rec[k]['Revision']['Head']==COMPILED and rec[k]['Revision']['ChangedFiles']==0 for k in ('catalogSummary','planSummary')))
ck('first_ZERO_native_status_counts',rec['status']=='PREPARED_G02_ZERO_ONLY' and rec['nativeExit']==0 and not rec['observerErrors'] and rec['gameCasesPlanned']==rec['diagnosticRequestUpperBound']==0 and 0<rec['durationSeconds']<600)
ck('nine_owned_native_zero_joined_no_residual',len(rec['commands'])==9 and all(x['nativeExit']==x['rawWrapperReturnCode']==0 and x['joined'] and not x['observerErrors'] and not x['remainingSessionProcesses'] and x['terminationSignal'] is None for x in rec['commands']))
ck('nine_unique_birth_identities_same_controller_uid',len({(x['identity']['pid'],x['identity']['birthTick']) for x in rec['commands']})==9 and {x['identity']['parentPid'] for x in rec['commands']}=={140381} and all(x['identity']['uid']==1000 for x in rec['commands']))
command_names=['compile-SyntheticOwner','retained-parent-proof','affected-collector-health-proof','bind-integration','imports-integration','bind-unit','imports-unit','catalog','plan']
ck('nine_command_records_exact_phase_rows',all(json.loads(z.read('prepare/'+n+'.record.json'))==cmd for n,cmd in zip(command_names,rec['commands'])))
compileout=z.read('prepare/compile-SyntheticOwner.stdout').decode();ck('compiler_only7_CS8632_noerrors',len(re.findall(r'warning CS8632:',compileout))==7 and len(re.findall(r'warning ',compileout))==7 and not re.search(r'error [A-Z]+\d+:',compileout) and rec['commands'][0]['durationSeconds']<60)
ck('no_build_restore_game_synthetic_native_in_ZERO',all('build' not in x['command'] and 'restore' not in x['command'] and not any('one-synthetic' in a for a in x['command']) for x in rec['commands']) and sum(any(a.endswith('csc.dll') for a in x['command']) for x in rec['commands'])==1)
ck('four_owner_outputs_current_hashes',len(rec['syntheticOwnerOutputs'])==4 and all(same(x) for x in rec['syntheticOwnerOutputs']))
rsp=z.read('prepare/SyntheticOwner.rsp').decode().splitlines();inventory=json.loads(raw(P+'l01-managed-stack-capability-inventory-20261018.json'))
ck('compiler_rsp_exact163refs_and_single_reviewed_owner', [x[len('-reference:'):] for x in rsp if x.startswith('-reference:')]==[x['path'] for x in inventory['net8ReferenceInputs']] and len(inventory['net8ReferenceInputs'])==163 and [x for x in rsp if x.endswith('.cs')]==[str(R/G/'SyntheticOwner.cs')] and raw(G+'SyntheticOwner.cs',COMPILED)==raw(G+'SyntheticOwner.cs'))
ck('owner_runtime_net8_0_32_disable_rollforward',json.loads(z.read('prepare/SyntheticOwner.runtimeconfig.json'))==dict(runtimeOptions=dict(tfm='net8.0',framework=dict(name='Microsoft.NETCore.App',version='8.0.32'),rollForward='Disable')))
ck('installed395_inputs_and172_compiler_runtime_reference_images',len(cfg['inputs'])==395 and all(same(x) for x in cfg['inputs']) and len(inventory['availableFiles'])==9 and all(same(x) for x in inventory['availableFiles']+inventory['net8ReferenceInputs']))
parent=json.loads(z.read('prepare/retained-parent-proof.stdout'));health=json.loads(z.read('prepare/affected-collector-health-proof.stdout'))
ck('exact_parent6_no_targets_API',parent==rec['parentProof'] and parent['passed']==len(parent['cases'])==6 and all(x['passed'] for x in parent['cases']) and parent['gameProcesses']==parent['fixtureChildren']==parent['requests']==0)
ck('exact_PShealth26_no_game_API',health==rec['collectorHealthProof'] and health['Passed']==len(health['Cases'])==26 and all(x['Passed'] for x in health['Cases']) and health['Requests']==health['GameProcesses']==0 and parent['clockFrequency']==health['ClockFrequency']==1000000000)
pol=rec['retainedTimerPolicyProof'];policy=json.loads(Path(pol['record']['path']).read_text());ck('retained_policy23_immutable_notreexecuted',pol['cases']==23 and not pol['reexecuted'] and same(pol['record']) and policy['nativeExit']==0 and policy['results']['passed']==23 and policy['results']['failed']==0 and all(same(x) for x in policy['sourceImages']))
old=json.loads(Path(cfg['reuse']['prepareRecord']['path']).read_text());oldtools=json.loads(Path(cfg['reuse']['toolsRecord']['path']).read_text())
ck('original_G01_records_exact_retained',all(same(cfg['reuse'][k]) for k in ('prepareRecord','toolsRecord')) and rec['reuseProvenance']==cfg['reuse'])
ck('separate460_439_original_closure_provenance',rec['privateBinClosures']==old['privateBinClosures'] and {k:len(v) for k,v in rec['privateBinClosures'].items()}=={'BookOfEternityClient.IntegrationTests':460,'BookOfEternityClient.Tests':439} and rec['integrationCompiledSource']==old['integrationCompiledSource']=='d293b3bf796289fb6662a778ca408fae76ab2736' and rec['unitCompiledSource']==cfg['unit']['compiledSource']=='d3a1ddcf7a6e2f06ae815877c46314ec81d9f0d7')
for name,rows in rec['privateBinClosures'].items():
 root=ROOT/'artifacts/bin'/name/'debug';actual={str(x.relative_to(root)) for x in root.rglob('*') if x.is_file()}
 ck('current_complete_'+name,actual=={x['path'] for x in rows} and all(same(x,root/x['path']) and not (root/x['path']).is_symlink() and stat.S_IMODE((root/x['path']).stat().st_mode)==x['mode'] for x in rows))
ck('tools8_exact_original_copies_honest_derived_record',tools['kind']=='G02_DERIVED_REUSE_TOOLS_VERIFICATION' and tools['originalRecord']==cfg['reuse']['toolsRecord'] and tools['originalCompiledSource']==oldtools['source'] and len(tools['toolOutputs'])==8 and all(same(x) and x['sha256']==y['sha256'] and x['bytes']==y['bytes'] and Path(x['path']).name==Path(y['path']).name for x,y in zip(tools['toolOutputs'],oldtools['toolOutputs'])))
ck('S01oracle3exact_images_and_accepted_MVID',rec['oracleFiles']==cfg['oracleFiles'] and all(same(x) for x in rec['oracleFiles']) and cfg['oracleMvid']=='35065df3-7a06-4e1b-8b74-ac77cd578120' and same(cfg['oraclePrepareRecord']) and json.loads(Path(cfg['oraclePrepareRecord']['path']).read_text())['abi']['TargetMvid']==cfg['oracleMvid'])
meta=[(group,x,ROOT/'artifacts/obj'/group['name']/Path(x['path']).name) for group in cfg['metadata'] for x in group['files']]
ck('12_staged_NuGet_metadata_exact',len(meta)==12 and all(same(x,p) and same(x) for _,x,p in meta))
inputmap={x['path']:x for x in cfg['inputs']}
for row in rec['evaluatedBindings']:
 key=row['project'];props=json.loads(z.read('prepare/bind-'+key+'.stdout'))['Properties'];name=props['AssemblyName'];root=ROOT/'artifacts/obj'/name
 ck('actual_private_'+key+'_property_binding',props==row['properties'] and props['TargetPath']==str(ROOT/'artifacts/bin'/name/'debug'/(name+'.dll')) and props['ProjectAssetsFile']==str(root/'project.assets.json') and Path(props['MSBuildProjectExtensionsPath'])==root and props['IsTestProject']=='true' and props['TargetFramework']=='net8.0' and props['Configuration']=='Debug')
 paths=set()
 for comment in re.findall(r'<!--(.*?)-->',z.read('prepare/'+key+'.project.xml').decode(),re.S):
  for line in comment.splitlines():
   x=line.strip()
   if x.startswith('/') and x.endswith(('.props','.targets')):paths.add(x)
 ck('exact_'+key+'_129_or127_imports',paths=={x['path'] for x in row['imports']} and len(paths)==(129 if key=='integration' else 127) and all(same(x) for x in row['imports']))
 for imp in row['imports']:
  p=Path(imp['path'])
  if p.is_relative_to(root):expected=next(x for group,x,dst in meta if dst==p);assert imp['sha256']==expected['sha256']
  elif p.is_relative_to(R):assert p.read_bytes()==raw(str(p.relative_to(R)),COMPILED)
  else:assert imp==inputmap[str(p)]
 ck('all_'+key+'_imports_hashbound_no_unknown',True)
cat=json.loads(z.read('runner0/summary.json'));plan=json.loads(z.read('runner1/summary.json'));desc=json.loads(z.read('runner1/plan.json'))
ck('full_catalog810_11802_valid_ZERO',cat==rec['catalogSummary'] and cat['CatalogAudit']==dict(Valid=True,Methods=11802,Categories=810,Unmapped=[],StaleSelectors=[]) and cat['DiscoveryOnly'] and cat['Tests']['Executed']==0 and json.loads(z.read('runner0/catalog-audit.json'))==cat['CatalogAudit'])
expected='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss';d=desc['Descriptors'][0]
ck('exact_singleton_PlanOnly_no_actualcompletion',plan==rec['planSummary'] and plan['PlanOnly'] and plan['Tests']['Executed']==0 and len(desc['Descriptors'])==1 and d['ExpectedMethodCounts']==[dict(Name=expected,Cases=1)] and d['Filter']=='FullyQualifiedName='+expected and d['BudgetMinutes']==15 and d['Category']=='fr063-joint-interface-crash-diagnostic' and plan['Selection']['PlannedDescriptors']==plan['Selection']['PlannedCases']==1 and plan['Selection']['CompletedDescriptors']==plan['Selection']['CompletedCases']==0 and not plan['Selection']['Complete'])
ck('runner_no_build_no_timeout_cleanup_true',all(x['ExitCode']==0 and not x['TimedOut'] and x['OwnedTreeCleanupSucceeded'] and x['RuntimeCleanupSucceeded'] and not any(y['Name'].startswith('Build-') for y in x['PhaseResults']) for x in (cat,plan)))
with zipfile.ZipFile(R/(P+'l01-delivery-d01-first-prep-failure-originals-20261018.zip')) as bz:normal=json.loads(bz.read('phase/record.json'))['binaryInventory']
ck('current_normal899_exact_frozen_inventory',len(normal)==899 and all(same(x,R/x['path']) for x in normal) and rec['normal899UnchangedBefore'] and rec['normal899UnchangedAfter'])
ck('dated_visibility_no_live_tools_not_continuous_claim',rec['afterLiveTools']==[] and rec['beforeTools']==rec['afterTools']==[dict(comm='dotnet',state='Z',count=35)] and rec['beforeVisibility']['executableFallbacks']==rec['afterVisibility']['executableFallbacks']==5)
ck('source_gate_consumed_and_runtime_HOLD',ctl['sourceAdmission']['status']=='CONSUMED_G02_ZERO_PREP_FIRST_OUTCOME' and ctl['syntheticAdmission']['status']==ctl['actualAdmission']['status']=='HOLD')
ck('unused_synthetic_and_actual_private_roots',not (ROOT/'synthetic').exists() and not (ROOT/'actual').exists() and stat.S_IMODE(ROOT.stat().st_mode)==0o700 and ROOT.stat().st_uid==1000 and not ROOT.is_symlink())
synthetic_socket=str(ROOT/'synthetic/own/tmp'/('dotnet-diagnostic-'+'9'*10+'-'+'9'*20+'-socket'));game_socket=str(ROOT/'actual/tmp'/('g-'+'9'*12)/'tmp'/('dotnet-diagnostic-'+'9'*10+'-'+'9'*20+'-socket'))
ck('distinct_ids_and_socket_max_bound',cfg['syntheticRunId']=='3ac823b7efc444babcd176679ae51ecd' and cfg['runId']=='1f936a7091bf42a6b049e1719016e2da' and len(synthetic_socket.encode())==91 and len(game_socket.encode())==103 and max(len(synthetic_socket.encode()),len(game_socket.encode()))<=107)
ck('private_binaries_dump_not_claimed_in_Git_archive',not any(n.endswith(('.dll','.pdb','.core','.crashreport.json','.trx')) for n in z.namelist()) and 'private binaries not restored by archive' in idx['limits'])
result=dict(kind='G02_SYNTHETIC_PREP_INDEPENDENT_CHECKS',reviewedSource=SHA,prepareSource=COMPILED,sourcePinDigest=packet['sourcePinDigest'],controllerSha256=H(raw(O+'phase.py')),bindingsSha256=H(raw(G+'bindings.json')),plannedSyntheticRunId=cfg['syntheticRunId'],prepRecords=dict(tools=idx['privateToolsRecord']['sha256'],prepare=idx['privatePrepareRecord']['sha256']),checks=flags,passed=sum(x['passed'] for x in flags),failed=0,originalArchive=dict(path=ap,sha256=H(archive),bytes=len(archive),members=47),rawChains=chains,recoveryReceipt=dict(path=str(rrp),sha256=H(rrp.read_bytes())),ownerOutputs=rec['syntheticOwnerOutputs'],durations=dict(controllerSeconds=rec['durationSeconds'],compilerSeconds=rec['commands'][0]['durationSeconds'],catalogWrapperSeconds=rec['commands'][7]['durationSeconds'],catalogRunnerWall=cat['WallTime'],planWrapperSeconds=rec['commands'][8]['durationSeconds'],planRunnerWall=plan['WallTime']),limits='Read-only archive/source/output/hash checks; no SDK/procfs/process action. PRIVATE DLLs/PDBs verified locally, not recovered from Git. PREP is ZERO; target/API work remains unrun until a separately consumed synthetic permit.')
out=V/'l01-game-capture-g02-synthetic-prep-checks-20261019.json';out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(path=str(out),sha256=H(out.read_bytes()),passed=result['passed'],failed=0,prepRecords=result['prepRecords'])))
