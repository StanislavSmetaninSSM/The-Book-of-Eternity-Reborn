from pathlib import Path
import json,hashlib,subprocess
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');V=Path('/tmp/1536-cloud-tools/review')
SHA='913453c077fd9348b51cb3462c8bed5a658dfbde';OLD='8b9d08f0242ebaec76e74a1fbe28351fd1f6681e';P='specs/1536-complete-wound-materialization/recovery/';G=P+'l01-game-capture-g02/';O=P+'l01-game-capture-g01/';H=lambda b:hashlib.sha256(b).hexdigest()
def git(*a):return subprocess.check_output(['git','-C',str(R),*a])
def raw(p,rev=SHA):return git('show',rev+':'+p)
flags=[]
def ck(name,value,detail=None):
 flags.append(dict(name=name,passed=bool(value),detail=detail))
 if not value:raise AssertionError(name)
a=json.loads(raw(G+'source-packet.json',OLD));b=json.loads(raw(G+'source-packet.json'));s=raw(G+'SyntheticOwner.cs').decode();previous=raw(G+'SyntheticOwner.cs',OLD).decode();receiptPath=Path('/tmp/1536-cloud-tools/g02-recovery-913453c0.json');receipt=json.loads(receiptPath.read_text())
ck('exact_remote_clean_recovery_receipt',receipt['sha']==SHA and receipt['remoteShaVerified'] and receipt['clean'] and receipt['currentPinsVerified']==173 and receipt['sourcePinDigest']==b['sourcePinDigest'])
changes=git('diff','--name-only',OLD,SHA).decode().splitlines();ck('only_owner_packet_delta',set(changes)=={G+'SyntheticOwner.cs',G+'source-packet.json'})
ck('exact_two_changed_paths_restored',all((C/x).read_bytes()==raw(x) for x in changes))
ck('canonical_current_digest_173',len(b['sourcePins'])==173 and H(json.dumps(b['sourcePins'],sort_keys=True,separators=(',',':')).encode())==b['sourcePinDigest'])
delta=[(x,y) for x,y in zip(a['sourcePins'],b['sourcePins']) if x!=y];ck('only_one_pin_changed',len(a['sourcePins'])==len(b['sourcePins'])==173 and len(delta)==1 and delta[0][0]['path']==delta[0][1]['path']==G+'SyntheticOwner.cs')
ck('new_owner_pin_exact',delta[0][1]['sha256']==H(raw(G+'SyntheticOwner.cs')) and delta[0][1]['bytes']==len(raw(G+'SyntheticOwner.cs')))
ck('packet_other_fields_equal',{k:v for k,v in a.items() if k not in ('sourcePins','sourcePinDigest')}=={k:v for k,v in b.items() if k not in ('sourcePins','sourcePinDigest')})
old='            Write(Path.Combine(own,"record.json"),record);\n'
new='''            try {Write(Path.Combine(own,"record.json"),record);}
            catch(Exception e) {
                errors.Add("Record persistence: "+e);record["status"]="FROZEN_FIRST_SYNTHETIC_CAPTURE_FAILURE";
            }
            // Independent metadata channel preserves known actor exits even if private persistence fails.
            try {Console.Out.WriteLine("L01_OWNER_ACCOUNTING "+JsonSerializer.Serialize(record));Console.Out.Flush();}
            catch(Exception e) {
                errors.Add("Summary emission: "+e);record["status"]="FROZEN_FIRST_SYNTHETIC_CAPTURE_FAILURE";
                // Bounded evidence update only. This never relaunches a target/collector/API.
                try {Write(Path.Combine(own,"record.json"),record);}
                catch(Exception update) {errors.Add("Record update: "+update);}
            }
'''
ck('exact_inverse_restores_previously_reviewed_owner',s.count(new)==1 and s.replace(new,old)==previous)
ck('current_controller_bindings_control_unchanged',all(raw(x)==raw(x,OLD) for x in (O+'phase.py',G+'bindings.json',G+'control.json',O+'Collect.ps1',O+'watchdog.py')))
ck('persist_failure_sticky_status_and_errors','errors.Add("Record persistence: "+e);record["status"]="FROZEN_FIRST_SYNTHETIC_CAPTURE_FAILURE";' in new)
ck('independent_full_record_emit_after_persist_catch',new.index('catch(Exception e)')<new.index('Console.Out.WriteLine("L01_OWNER_ACCOUNTING "+JsonSerializer.Serialize(record));Console.Out.Flush();'))
ck('emit_failure_sticky_and_bounded_private_update','errors.Add("Summary emission: "+e);record["status"]="FROZEN_FIRST_SYNTHETIC_CAPTURE_FAILURE";' in new and new.count('try {Write(Path.Combine(own,"record.json"),record);}')==2 and 'catch(Exception update)' in new)
ck('observerErrors_list_shared_with_record','record["observerErrors"]=errors;' in s and s.index('record["observerErrors"]=errors;')<s.index(new))
ck('actor_fields_not_modified_by_publication',not any(x in new for x in ('NativeExit','CleanupExit','Joined','apiCalls','Process.Start','Kill(','Invoke-ObservedApi')))
ck('publication_failure_remains_owner_verification_nonzero','Require(errors.Count==0,"Synthetic capture failed; retain root and HOLD game");' in s and 'try {await MainCore(args);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}' in s)
ck('outer_zero_failure_stops_before_decode',"zero([DOTNET,str(ROOT/'prepare/SyntheticOwner.dll')" in raw(O+'phase.py').decode() and raw(O+'phase.py').decode().index("'one-synthetic-capture',start+45)")<raw(O+'phase.py').decode().index("record['syntheticOutcome']=read(phase/'own/record.json')"))
control=json.loads(raw(G+'control.json'));ck('all_runtime_and_zero_gates_still_hold',all(control[x]['status']=='HOLD' for x in ('sourceAdmission','syntheticAdmission','actualAdmission')))
ck('unused_G02_root',not Path(json.loads(raw(G+'bindings.json'))['root']).exists())
prior=V/'l01-game-capture-g02-capture-source-checks-20261019.json';p=json.loads(prior.read_text());ck('prior36_checks_and15_inert_cases_retained',p['reviewedSource']==OLD and p['passed']==36 and p['failed']==0 and H(prior.read_bytes())=='04eafbb963e24460ba6564451532ceef7a16fad58af26a2035f60a615090def5')
result=dict(kind='G02_FINAL_PUBLICATION_AFFECTED_SOURCE_CHECKS',reviewedSource=SHA,previousSource=OLD,sourcePinDigest=b['sourcePinDigest'],controllerSha256=H(raw(O+'phase.py')),bindingsSha256=H(raw(G+'bindings.json')),checks=flags,passed=sum(x['passed'] for x in flags),failed=0,reusedProof=dict(path=str(prior),sha256=H(prior.read_bytes()),checks=36,inertCases=15,reexecuted=False),recoveryReceipt=dict(path=str(receiptPath),sha256=H(receiptPath.read_bytes())),executionScope='Read-only exact Git/files/hash and final-publication control-flow inspection only. No new C#/SDK/fake process/native/API/procfs execution. No old corpus replay.')
out=V/'l01-game-capture-g02-final-source-checks-20261019.json';out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(path=str(out),sha256=H(out.read_bytes()),passed=result['passed'],controllerSha256=result['controllerSha256'],sourcePinDigest=result['sourcePinDigest'],bindingsSha256=result['bindingsSha256'])))
