import importlib.util,pathlib,tempfile,unittest,json
p=pathlib.Path(__file__).with_name('submit-mm21-once.py');s=importlib.util.spec_from_file_location('gate',p);g=importlib.util.module_from_spec(s);s.loader.exec_module(g)
class Data(unittest.TestCase):
 def setUp(self):
  self.t=tempfile.TemporaryDirectory();self.addCleanup(self.t.cleanup);self.r=pathlib.Path(self.t.name);self.f=self.r/'data';self.f.write_bytes(b'existing')
 def test_existing_correct(self):self.assertTrue(g.matches(self.f,g.sha(b'existing')))
 def test_existing_wrong(self):self.assertFalse(g.matches(self.f,'0'*64))
 def test_missing_expected_missing(self):self.assertTrue(g.matches(self.r/'absent','missing'))
 def test_missing_expected_hash(self):self.assertFalse(g.matches(self.r/'absent',g.sha(b'existing')))
 def test_existing_expected_missing(self):self.assertFalse(g.matches(self.f,'missing'))
 def test_link_refused(self):
  (self.r/'link').symlink_to(self.f);self.assertFalse(g.matches(self.r/'link',g.sha(b'existing')))
 def test_reviewed_packet_exact_and_mismatch(self):
  raw=json.dumps({'Writes':[{'Path':'data','ExpectedSHA256':g.sha(b'existing')},{'Path':'absent','ExpectedSHA256':'missing'}]}).encode();g.validate_packet(self.r,raw,g.sha(raw))
  with self.assertRaisesRegex(ValueError,'Candidate hash'):g.validate_packet(self.r,raw,'0'*64)
  self.f.write_bytes(b'changed')
  with self.assertRaisesRegex(ValueError,'baseline'):g.validate_packet(self.r,raw,g.sha(raw))
 def test_traversal_refused(self):
  with self.assertRaises(ValueError):g.member(self.r,'../outside')
class MainPath(unittest.TestCase):
 def setUp(self):
  import shutil,datetime
  self.t=tempfile.TemporaryDirectory();self.addCleanup(self.t.cleanup);self.root=pathlib.Path(self.t.name).resolve();self.phase=self.root/'p01';self.session=self.root/'play/game_session';self.q=self.phase/'queue/request-data'
  self.q.mkdir(parents=True);relay=self.phase/'relay';relay.mkdir()
  source=pathlib.Path('/workspace/shared/mortal-isolated-20261119/tools/gm-relay')
  for name in ['relay_contract.py','relay_worker.py']:shutil.copyfile(source/name,relay/name)
  self.pins={p.name:g.sha(p.read_bytes()) for p in relay.iterdir()};(self.phase/'relay-phase-package.json').write_text(json.dumps({'RelayPins':self.pins}))
  self.identity={'sessionId':'data-session','requestId':'data-request','turnNumber':42};now=datetime.datetime.now(datetime.timezone.utc);self.turn=dict(self.identity,timestamp=now.isoformat())
  def put(path,value):
   path.parent.mkdir(parents=True,exist_ok=True);raw=json.dumps(value).encode();path.write_bytes(raw);return raw
  turn=put(self.session/'input/turn_request.json',self.turn);pending=put(self.session/'game_state/control/pending_turn_snapshot.json',self.identity);authority=put(self.session/'game_state/control/pending_turn_snapshot.authority.json',{'data':True})
  witnesses={'input/turn_request.json':g.sha(turn),'game_state/control/pending_turn_snapshot.json':g.sha(pending),'game_state/control/pending_turn_snapshot.authority.json':g.sha(authority)}
  header={'QueueId':self.q.name,'Kind':'turn','RequestPath':'input/turn_request.json','SessionPath':str(self.session),'Model':'data-only','Witnesses':witnesses,'PromptSHA256':g.sha(b'prompt'),'RequestSHA256':g.sha(turn),'TurnRequestSHA256':g.sha(turn),'TurnIdentity':self.identity}
  self.header=put(self.q/'request.json',header);(self.q/'prompt.txt').write_bytes(b'prompt');(self.q/'game-request.json').write_bytes(turn)
  self.owner={'RootKey':str(self.root/'play'),'BootId':pathlib.Path('/proc/sys/kernel/random/boot_id').read_text().strip(),'RunId':'11111111111111111111111111111111','GenerationId':'22222222222222222222222222222222','Epoch':1,'Backend':'LinuxSupervisor','HostInstanceId':'33333333333333333333333333333333'}
  put(self.root/'play/.boe_runtime/gm-runs/main.json',{'SchemaVersion':1,'Disposition':'Running','Identity':self.owner,'StopEvidence':None})
  self.expected={'RequestHeaderSHA256':g.sha(self.header),'TurnIdentity':self.identity,'OriginalOwnerIdentity':self.owner,'OriginalDriverStartedAtUtc':(now-datetime.timedelta(seconds=5)).isoformat()}
  self.e=self.phase/'expected.json';self.c=self.phase/'candidate.json';self.packet=json.dumps({'Writes':[{'Path':'output/new.json','ExpectedSHA256':'missing','Content':'{}'}]}).encode();self.c.write_bytes(self.packet)
  self.origin()
 def origin(self,monotonic_age=0):
  import time
  path=self.phase/'launch-origin.json'
  if path.exists():path.chmod(0o600)
  path.write_text(json.dumps({'OriginalDriverStartedAtUtc':self.expected['OriginalDriverStartedAtUtc'],'BootId':self.owner['BootId'],'MonotonicSeconds':time.monotonic()-monotonic_age}))
  path.chmod(0o400);self.expected['LaunchOriginSHA256']=g.sha(path.read_bytes())
 def call(self,worker=None,phase='p01'):
  from unittest.mock import patch
  self.e.write_text(json.dumps(self.expected));argv=['gate','--root',str(self.root),'--phase',phase,'--request-dir',str(self.q),'--candidate',str(self.c),'--candidate-sha',g.sha(self.packet),'--expectations',str(self.e),'--expectations-sha',g.sha(self.e.read_bytes())]
  with patch.object(g.sys,'argv',argv),patch.object(g.subprocess,'run',side_effect=worker) as run:
   try:result=g.main()
   finally:self.calls=run.call_count
  return result
 def test_actual_read_request_single_worker_frozen_packet_no_repeat(self):
  import subprocess
  def worker(argv,**kwargs):
   self.assertEqual(argv[:4],['/usr/bin/python3',str(self.phase/'relay/relay_worker.py'),'answer',str(self.q)])
   self.assertEqual(argv[5:],['--adapter','dots-finish-relay']);self.assertTrue((self.phase/'submission-attempt.json').is_file());self.c.write_bytes(b'changed after check')
   frozen=pathlib.Path(argv[4]);self.assertEqual(frozen.read_bytes(),self.packet);self.assertEqual(frozen.stat().st_mode&0o777,0o400)
   return subprocess.CompletedProcess(argv,0,'inert','')
  self.assertEqual(self.call(worker),0);self.assertEqual(self.calls,1);self.c.write_bytes(self.packet)
  with self.assertRaisesRegex(ValueError,'already attempted'):self.call(worker)
  self.assertEqual(self.calls,0)
 def test_wrong_identity_refused(self):
  self.expected['TurnIdentity']=dict(self.identity,requestId='wrong')
  with self.assertRaisesRegex(ValueError,'request mismatch'):self.call()
  self.assertEqual(self.calls,0)
 def test_driver_deadline_refused(self):
  import datetime
  self.expected['OriginalDriverStartedAtUtc']=(datetime.datetime.now(datetime.timezone.utc)-datetime.timedelta(seconds=610)).isoformat()
  self.origin()
  with self.assertRaisesRegex(ValueError,'reserve'):self.call()
  self.assertEqual(self.calls,0)
 def test_future_driver_start_refused(self):
  import datetime
  self.expected['OriginalDriverStartedAtUtc']=(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(seconds=1)).isoformat()
  self.origin()
  with self.assertRaisesRegex(ValueError,'reserve'):self.call()
 def test_launch_origin_missing_refused(self):
  (self.phase/'launch-origin.json').unlink()
  with self.assertRaises(FileNotFoundError):self.call()
  self.assertEqual(self.calls,0)
 def test_launch_origin_hash_refused(self):
  self.expected['LaunchOriginSHA256']='0'*64
  with self.assertRaisesRegex(ValueError,'origin hash'):self.call()
  self.assertEqual(self.calls,0)
 def test_launch_timestamp_cannot_be_renewed(self):
  self.expected['OriginalDriverStartedAtUtc']='2099-01-01T00:00:00+00:00'
  with self.assertRaisesRegex(ValueError,'clock/boot'):self.call()
  self.assertEqual(self.calls,0)
 def test_monotonic_original_deadline_refused(self):
  self.origin(monotonic_age=610)
  with self.assertRaisesRegex(ValueError,'reserve'):self.call()
  self.assertEqual(self.calls,0)
 def test_contract_drift_refused_before_import(self):
  (self.phase/'relay/relay_contract.py').write_text("raise RuntimeError('MUST NOT IMPORT')")
  with self.assertRaisesRegex(ValueError,'Relay source drift'):self.call()
 def test_current_witness_drift_refused(self):
  (self.session/'input/turn_request.json').write_text('{}')
  with self.assertRaisesRegex(Exception,'Original request/pending changed'):self.call()
  self.assertEqual(self.calls,0)
 def test_wrong_owner_refused(self):
  self.expected['OriginalOwnerIdentity']=dict(self.owner,RunId='foreign')
  with self.assertRaisesRegex(ValueError,'owner no longer Running'):self.call()
  self.assertEqual(self.calls,0)
 def test_closed_queue_refused(self):
  (self.q.parent/'close-request.json').write_text('{}')
  with self.assertRaisesRegex(Exception,'closure requested'):self.call()
  self.assertEqual(self.calls,0)
 def test_worker_uncertainty_never_retried(self):
  def worker(*a,**k):raise TimeoutError('inert uncertainty')
  with self.assertRaises(TimeoutError):self.call(worker)
  self.assertEqual(self.calls,1)
  with self.assertRaisesRegex(ValueError,'already attempted'):self.call(worker)
  self.assertEqual(self.calls,0)

 def phase_two(self):
  old=self.phase;self.phase=self.root/'p02';old.rename(self.phase);self.q=self.phase/'queue/request-data';self.c=self.phase/'candidate.json';self.e=self.phase/'expected.json'
 def test_p02_uses_only_its_queue_origin_and_once_marker(self):
  import subprocess
  self.phase_two()
  def worker(argv,**kwargs):
   self.assertEqual(argv[1],str(self.phase/'relay/relay_worker.py'));self.assertEqual(argv[3],str(self.q));self.assertTrue((self.phase/'submission-attempt.json').exists())
   return subprocess.CompletedProcess(argv,0,'inert','')
  self.assertEqual(self.call(worker,phase='p02'),0);self.assertEqual(self.calls,1)
  with self.assertRaisesRegex(ValueError,'already attempted'):self.call(worker,phase='p02')
  self.assertEqual(self.calls,0)
 def test_p02_queue_cannot_bind_p01(self):
  self.phase_two()
  with self.assertRaisesRegex(ValueError,'exact original queue'):self.call(phase='p01')
  self.assertEqual(self.calls,0)
 def test_unknown_phase_refused_before_worker(self):
  with self.assertRaises(SystemExit):self.call(phase='p99')
  self.assertEqual(self.calls,0)

 def test_p03_uses_only_its_queue_origin_and_once_marker(self):
  import subprocess
  old=self.phase;self.phase=self.root/'p03';old.rename(self.phase)
  self.q=self.phase/'queue/request-data';self.c=self.phase/'candidate.json';self.e=self.phase/'expected.json'
  with self.assertRaisesRegex(ValueError,'exact original queue'):self.call(phase='p02')
  self.assertEqual(self.calls,0)
  def worker(argv,**kwargs):
   self.assertEqual(argv[1],str(self.phase/'relay/relay_worker.py'));self.assertEqual(argv[3],str(self.q))
   return subprocess.CompletedProcess(argv,0,'inert','')
  try:result=self.call(worker,phase='p03')
  except SystemExit:self.fail('Missing closed p03 publisher phase')
  self.assertEqual(result,0);self.assertEqual(self.calls,1)
  with self.assertRaisesRegex(ValueError,'already attempted'):self.call(worker,phase='p03')
  self.assertEqual(self.calls,0)

class ProfileBinding(unittest.TestCase):
 def test_actual_binding_shape_and_refusals(self):
  import ast,copy,re
  source=p.with_name('prepare-mm21-profile.py').read_text();tree=ast.parse(source)
  nodes=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ('checked_continuation_binding','continuation_spec')];scope={'re':re}
  exec(compile(ast.Module(body=nodes,type_ignores=[]),'<actual-profile-binding>','exec'),scope)
  checked=scope['checked_continuation_binding'];base={'Phase':'p02','PreviousSHA256':'1'*64,'GuardianSHA256':'2'*64,'SeedSHA256':'3'*64,'PhasePackageSHA256':'4'*64,'SourcePins':{},'Action':'Прошу разрешение, ничего не беру','ConsoleRoute':{'Route':'permission-with-denial','StorageId':'storage','ItemId':'item'}}
  self.assertEqual(checked(copy.deepcopy(base)),base)
  for key,value in [('Phase','p01'),('PreviousSHA256','BAD'),('Action','bad\ninput'),('ConsoleRoute',{'Route':'birth'})]:
   bad=copy.deepcopy(base);bad[key]=value
   with self.assertRaises(ValueError):checked(bad)
  bad=copy.deepcopy(base);bad['Extra']=True
  with self.assertRaises(ValueError):checked(bad)

 def test_actual_regular_tree_refuses_links_and_special_entries(self):
  import ast,stat,os,hashlib
  tree=ast.parse(p.with_name('prepare-mm21-profile.py').read_text());nodes=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ('plain_path','regular_tree','exact_tree')];scope={'pathlib':pathlib,'stat':stat,'hashlib':hashlib}
  exec(compile(ast.Module(body=nodes,type_ignores=[]),'<actual-profile-paths>','exec'),scope)
  with tempfile.TemporaryDirectory() as tmp:
   root=pathlib.Path(tmp);source=root/'source';source.mkdir();(source/'file').write_bytes(b'exact')
   self.assertEqual(scope['regular_tree'](source),source)
   pins={'file':hashlib.sha256(b'exact').hexdigest()};self.assertEqual(scope['exact_tree'](source,pins),pins)
   cache=source/'__pycache__';cache.mkdir();(cache/'extra.pyc').write_bytes(b'extra')
   with self.assertRaises(ValueError):scope['exact_tree'](source,pins)
   (cache/'extra.pyc').unlink();cache.rmdir()
   linked=root/'linked';linked.symlink_to(source,target_is_directory=True)
   with self.assertRaises(ValueError):scope['regular_tree'](linked)
   with self.assertRaises(ValueError):scope['plain_path'](linked/'file')
   (source/'samebytes').symlink_to(source/'file')
   with self.assertRaises(ValueError):scope['regular_tree'](source)
   (source/'samebytes').unlink();os.mkfifo(source/'fifo')
   with self.assertRaises(ValueError):scope['regular_tree'](source)

class PhysicalTraceProfile(unittest.TestCase):
 def actual(self):
  import ast,os,re,stat,uuid,shutil,sys,hashlib
  path=pathlib.Path(__file__).with_name('prepare-mm21-profile.py');tree=ast.parse(path.read_text())
  ns=dict(pathlib=pathlib,json=json,hashlib=hashlib,os=os,re=re,stat=stat,uuid=uuid,shutil=shutil,sys=sys)
  functions=[n for n in tree.body if isinstance(n,ast.FunctionDef)]
  exec(compile(ast.Module(body=functions,type_ignores=[]),'<actual-profile-functions>','exec'),ns)
  return tree,ns
 def test_exclusive_actual_trace_assets_no_repair(self):
  import os,stat
  from unittest.mock import patch
  tree,ns=self.actual();fn=ns.get('create_physical_trace_assets')
  self.assertTrue(callable(fn),'Missing exclusive trace creation; downstream controls UNEXECUTED')
  for case in ('valid','existing','dangling','partial','overlap','bad-nonce','linked-parent'):
   with self.subTest(case=case),tempfile.TemporaryDirectory() as tmp:
    root=pathlib.Path(tmp).resolve();base=root/'play';base.mkdir();phase=root/'p01';phase.mkdir();trace=phase/'physical-trace';nonce='a'*32
    if case=='existing':trace.mkdir(mode=0o700);(trace/'retained').write_bytes(b'keep')
    if case=='dangling':trace.symlink_to(root/'missing')
    if case=='overlap':phase=base/'p01';phase.mkdir();trace=phase/'physical-trace'
    if case=='bad-nonce':nonce='bad'
    if case=='linked-parent':
     link=root/'linked';link.symlink_to(phase,target_is_directory=True);phase=link;trace=phase/'physical-trace'
    if case=='valid':
     value=fn(base,phase,nonce);self.assertEqual(value,{'Route':'external','Directory':str(trace)})
     self.assertEqual((trace/'owner-nonce').read_bytes(),b'a'*32)
     self.assertEqual(stat.S_IMODE(trace.stat().st_mode),0o700);self.assertEqual(stat.S_IMODE((trace/'owner-nonce').stat().st_mode),0o600)
     with self.assertRaises((ValueError,FileExistsError)):fn(base,phase,nonce)
     self.assertEqual((trace/'owner-nonce').read_bytes(),b'a'*32)
    elif case=='partial':
     original=os.open;failure=OSError('injected marker create refusal')
     def open_file(path,*a,**kw):
      if pathlib.Path(path)==trace/'owner-nonce':raise failure
      return original(path,*a,**kw)
     with patch.object(os,'open',open_file):
      with self.assertRaises(OSError) as caught:fn(base,phase,nonce)
     self.assertIs(caught.exception,failure);self.assertTrue(trace.is_dir());self.assertFalse((trace/'owner-nonce').exists())
     with self.assertRaises((ValueError,FileExistsError)):fn(base,phase,nonce)
     self.assertTrue(trace.is_dir())
    else:
     with self.assertRaises((ValueError,FileExistsError)):fn(base,phase,nonce)
     if case=='existing':self.assertEqual((trace/'retained').read_bytes(),b'keep')
     elif case=='dangling':self.assertTrue(trace.is_symlink())
     else:self.assertFalse(os.path.lexists(trace))
 def test_actual_generated_launcher_binds_exact_trace_profile(self):
  import ast,hashlib,os,stat
  tree,ns=self.actual();nodes=[n for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='script' for t in n.targets)]
  self.assertEqual(len(nodes),1)
  write=ns.get('write_physical_launch_profile');self.assertTrue(callable(write),'Missing actual profile serialization')
  for phase in ('p01','p02'):
   with self.subTest(phase=phase),tempfile.TemporaryDirectory() as tmp:
    root=pathlib.Path(tmp).resolve();base=root/'play';base.mkdir();folder=root/phase;folder.mkdir();launcher=root/('launch-'+phase+'.sh')
    authored={'StopNonce':'a'*32,'FailureNonce':'b'*32,'Route':'birth' if phase=='p01' else 'permission-with-denial'}
    actual,digest=write(base,folder,launcher,authored)
    raw=(folder/'launch-profile.json').read_bytes();self.assertEqual(json.loads(raw),actual);self.assertEqual(hashlib.sha256(raw).hexdigest(),digest);self.assertNotIn('PhysicalTrace',authored)
    self.assertEqual(stat.S_IMODE((folder/'launch-profile.json').stat().st_mode),0o600)
    ns.update(D=root,phase=phase,previous_path=None if phase=='p01' else root/'p01/probe-result.json',failure='b'*32,stop='a'*32,profile_sha=digest)
    exec(compile(ast.Module(body=nodes,type_ignores=[]),'<actual-generated-launcher>','exec'),ns);script=ns['script'];lines=script.splitlines()
    expected=['export BOE_TEST_CANONICAL_LEASE_TRACE_ROUTE=external','export BOE_TEST_CANONICAL_LEASE_TRACE_DIR="$P/physical-trace"','export BOE_TEST_PHYSICAL_TRACE_PROFILE_SHA256='+digest,'export BOE_MAIN_STOP_DIAGNOSTIC_NONCE='+'a'*32]
    for line in expected:self.assertEqual(lines.count(line),1);self.assertLess(script.index(line),script.index('"$ROOT/native/host-guardian"'))
    self.assertIn('750000',script);self.assertIn('set -euo pipefail',script)
    ns['write_profile_artifact'](launcher,script.encode(),0o700);self.assertEqual(launcher.read_bytes(),script.encode());self.assertEqual(stat.S_IMODE(launcher.stat().st_mode),0o700)
    before={str(p.relative_to(root)):p.read_bytes() for p in root.rglob('*') if p.is_file()}
    with self.assertRaises((ValueError,FileExistsError)):write(base,folder,launcher,authored)
    self.assertEqual(before,{str(p.relative_to(root)):p.read_bytes() for p in root.rglob('*') if p.is_file()})
  for occupied in ('profile','launcher','dangling-launcher','preparation'):
   with self.subTest(occupied=occupied),tempfile.TemporaryDirectory() as tmp:
    root=pathlib.Path(tmp).resolve();base=root/'play';base.mkdir();folder=root/'p01';folder.mkdir();launcher=root/'launch-p01.sh'
    target=folder/'launch-profile.json' if occupied=='profile' else (root/'launch-preparation.json' if occupied=='preparation' else launcher)
    if occupied=='dangling-launcher':target.symlink_to(root/'missing')
    else:target.write_bytes(b'keep')
    with self.assertRaises(ValueError):write(base,folder,launcher,{'StopNonce':'a'*32})
    self.assertFalse(os.path.lexists(folder/'physical-trace'))
    if occupied=='dangling-launcher':self.assertTrue(target.is_symlink())
    else:self.assertEqual(target.read_bytes(),b'keep')
  gates=[n for n in tree.body if isinstance(n,ast.Expr) and isinstance(n.value,ast.Call) and isinstance(n.value.func,ast.Name) and n.value.func.id=='require_profile_outputs_absent']
  self.assertEqual(len(gates),1)
  action_write=[n for n in tree.body if isinstance(n,ast.Expr) and isinstance(n.value,ast.Call) and isinstance(n.value.func,ast.Attribute) and n.value.func.attr=='write_bytes']
  self.assertTrue(action_write);self.assertLess(gates[0].lineno,min(n.lineno for n in action_write))

class P03Continuation(unittest.TestCase):
 def actual(self):
  return PhysicalTraceProfile().actual()
 def test_closed_phase_map_binding_and_turn(self):
  import copy
  tree,ns=self.actual();fn=ns.get('continuation_spec')
  self.assertTrue(callable(fn),'Missing closed continuation map; downstream controls UNEXECUTED')
  for phase,expected in [('p02',('p01',42,43)),('p03',('p02',43,44))]:
   self.assertEqual(fn(phase),expected);self.assertEqual(fn(phase,{'AcceptedTurnNumber':expected[1]}),expected)
   for turn in (41,44,True,'43'):
    with self.assertRaises(ValueError):fn(phase,{'AcceptedTurnNumber':turn})
  for phase in ('p01','p04','../p03','p003',None):
   with self.assertRaises(ValueError):fn(phase)
  base={'Phase':'p03','PreviousSHA256':'1'*64,'GuardianSHA256':'2'*64,'SeedSHA256':'3'*64,'PhasePackageSHA256':'4'*64,'SourcePins':{},'Action':'Прошу передать просьбу совету, ничего не беру','ConsoleRoute':{'Route':'ordinary-save'}}
  self.assertEqual(ns['checked_continuation_binding'](copy.deepcopy(base)),base)
  for route in ({'Route':'permission-with-denial','StorageId':'s','ItemId':'i'},{'Route':'birth'},{'Route':'ordinary-save','StorageId':'s'}):
   bad=copy.deepcopy(base);bad['ConsoleRoute']=route
   with self.assertRaises(ValueError):ns['checked_continuation_binding'](bad)
 def test_actual_prior_save_archive_required(self):
  import copy,hashlib,sys,shutil
  tree,ns=self.actual();fn=ns.get('require_previous_manual_save')
  self.assertTrue(callable(fn),'Missing current manual archive admission; downstream controls UNEXECUTED')
  sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[3]/'tests/fixtures/ProductionMain'))
  import relay_phase_evidence as pe
  # Immutable accepted probe/ZIP DATA only; never modify or invoke the world.
  outcome_raw=pathlib.Path('/workspace/shared/relay-alternative/mortal-t034/mm28-p02-preparation/outcome.json').read_bytes()
  self.assertEqual(hashlib.sha256(outcome_raw).hexdigest(),'128b6ffe714382178d3db0bb19d71d2079b847d213e78ac9facf800360ce4fde')
  outcome=json.loads(outcome_raw)
  probe_path=pathlib.Path('/workspace/shared/mm28-20261119/p02/probe-result.json')
  self.assertEqual(hashlib.sha256(probe_path.read_bytes()).hexdigest(),outcome['ProbeSHA256'])
  previous=json.loads(probe_path.read_text());digest=previous['OrdinarySaveProof']['ArchiveSHA256']
  archives=[pathlib.Path(name) for name,h in outcome['Files'].items() if '/manual_saves/' in name and h==digest]
  self.assertEqual(len(archives),1);raw=archives[0].read_bytes();self.assertEqual(hashlib.sha256(raw).hexdigest(),digest)
  for case in ('valid','missing','duplicate','changed','receipt','scoped','link'):
   with self.subTest(case=case),tempfile.TemporaryDirectory() as tmp:
    session=pathlib.Path(tmp);folder=session/'saves/manual_saves';folder.mkdir(parents=True);a=folder/'save.zip';a.write_bytes(raw);prev=copy.deepcopy(previous)
    if case=='missing':a.unlink()
    if case=='duplicate':(folder/'duplicate.zip').write_bytes(raw)
    if case=='changed':a.write_bytes(raw+b'changed')
    if case=='receipt':prev['OrdinarySaveProof']['ArchiveVerified']=False
    if case=='scoped':
     for side in ('Before','After'):prev['OrdinarySaveProof'][side]['Files']['game_state/core/player_status.json']='7b7d'
    if case=='link':a.unlink();a.symlink_to(archives[0])
    before={str(f.relative_to(session)):f.read_bytes() for f in folder.iterdir() if not f.is_symlink()}
    if case=='valid':self.assertEqual(fn(prev,session,43,pe),digest)
    else:
     with self.assertRaises(ValueError):fn(prev,session,43,pe)
    self.assertEqual(before,{str(f.relative_to(session)):f.read_bytes() for f in folder.iterdir() if not f.is_symlink()})
 def test_actual_generated_p03_turn_and_previous(self):
  import ast
  tree,ns=self.actual();root=pathlib.Path('/tmp/authored-p03')
  ns.update(D=root,phase='p03',previous_path=root/'p02/probe-result.json',previous={'AcceptedTurnNumber':43},failure='b'*32,stop='a'*32,profile_sha='c'*64)
  script=next(n for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='script' for t in n.targets))
  exec(compile(ast.Module(body=[script],type_ignores=[]),'<actual-p03-launcher>','exec'),ns)
  self.assertIn('"$ROOT/p02/probe-result.json"',ns['script']);self.assertNotIn('"$ROOT/p01/probe-result.json"',ns['script'])
  profile=next(n.value for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='profile' for t in n.targets) and isinstance(n.value,ast.Dict))
  value=next(v for k,v in zip(profile.keys,profile.values) if isinstance(k,ast.Constant) and k.value=='ExpectedTurn')
  self.assertEqual(eval(compile(ast.Expression(value),'<actual-p03-turn>','eval'),ns),44)

class CurrentProducer(unittest.TestCase):
 def test_exact_current_producer_zero_overlays_and_source_additions(self):
  import ast,copy,hashlib,types
  source=pathlib.Path(__file__).with_name('manual-mm21.py');tree=ast.parse(source.read_text())
  functions=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ('require_current_producer_contract','checked_tools','require_published_rows')]
  self.assertEqual(len(functions),3,'Missing exact producer admission functions')
  ns={'pathlib':pathlib};exec(compile(ast.Module(body=functions,type_ignores=[]),'<actual-producer-contract>','exec'),ns);fn=ns['require_current_producer_contract']
  driver='tests/fixtures/ProductionMain/live-mortal-phase.py';addition='specs/1515-mortal-transport-storage/manual-mm21/publish-current.py'
  src={'engine.cs':'a',driver:'b',addition:'c'}
  valid={'owningBuildSourcePins':{'engine.cs':'a',driver:'old'},'allowedNonCompilerSourceChanges':{driver:'b'},'allowedNonCompilerSourceAdditions':{addition:'c'},'outputPins':{'client.dll':'d'},'producerOutputPins':{'client.dll':'d'},'contentOverlays':[]}
  fn(valid,src)
  # Exactly the reviewed ordinary-save route and directly affected Save guard.
  for name in ('tests/fixtures/ProductionMain/mortal_console_routes.py','tests/fixtures/ProductionMain/test-mortal-console-routes.py'):
   b=copy.deepcopy(valid);current=dict(src);b['owningBuildSourcePins'][name]='old';current[name]='reviewed';b['allowedNonCompilerSourceChanges'][name]='reviewed'
   try:fn(b,current)
   except ValueError as ex:self.fail('Reviewed ordinary-save fixture path not admitted: '+name+': '+str(ex))
   b['allowedNonCompilerSourceChanges'][name]='different'
   with self.assertRaises(ValueError):fn(b,current)
  for case in ('changed-output','extra-output','overlay','missing-addition','unreviewed-change','removed-source','compiled-addition','compiled-change','props-addition','arbitrary-python','resource-change'):
   with self.subTest(case=case):
    b=copy.deepcopy(valid);current=dict(src)
    if case=='changed-output':b['producerOutputPins']['client.dll']='old'
    elif case=='extra-output':b['producerOutputPins']['extra']='e'
    elif case=='overlay':b['contentOverlays']=[{'no-op':True}]
    elif case=='missing-addition':b['allowedNonCompilerSourceAdditions']={}
    elif case=='unreviewed-change':b['allowedNonCompilerSourceChanges']={}
    elif case=='removed-source':current.pop('engine.cs')
    elif case=='compiled-addition':current['new.cs']='e';b['allowedNonCompilerSourceAdditions']['new.cs']='e'
    elif case=='props-addition':current['new.props']='e';b['allowedNonCompilerSourceAdditions']['new.props']='e'
    elif case=='arbitrary-python':current['arbitrary.py']='e';b['allowedNonCompilerSourceAdditions']['arbitrary.py']='e'
    elif case=='resource-change':
     b['owningBuildSourcePins']['resource.json']='old';current['resource.json']='new';b['allowedNonCompilerSourceChanges']['resource.json']='new'
    else:current['engine.cs']='changed';b['allowedNonCompilerSourceChanges']['engine.cs']='changed'
    with self.assertRaises(ValueError):fn(b,current)
  rows=[{'path':'member','sha256':'hash','mode':0o644}];expected={'member':{'sha256':'hash','mode':0o644}}
  ns['require_published_rows'](rows,expected)
  for changed in ([dict(rows[0],mode=0o755)],[dict(rows[0],sha256='changed')],rows+rows,[dict(rows[0],path='foreign')]):
   with self.assertRaises(ValueError):ns['require_published_rows'](changed,expected)
  with tempfile.TemporaryDirectory() as tmp:
   root=pathlib.Path(tmp);pins={}
   for name in ('dotnet','pwsh','python3','cc','readelf','git'):
    path=root/name;path.write_bytes(name.encode());pins[name]={'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
   ns.update(b={'toolPins':pins},shutil=types.SimpleNamespace(which=lambda name:str(root/name)),h=lambda path:hashlib.sha256(path.read_bytes()).hexdigest())
   ns['checked_tools']()
   (root/'dotnet').write_bytes(b'changed')
   with self.assertRaises(ValueError):ns['checked_tools']()
   (root/'dotnet').write_bytes(b'dotnet');ns['shutil']=types.SimpleNamespace(which=lambda name:None if name=='dotnet' else str(root/name))
   with self.assertRaises(ValueError):ns['checked_tools']()
   ns['shutil']=types.SimpleNamespace(which=lambda name:str(root/name));pins.pop('git')
   with self.assertRaises(ValueError):ns['checked_tools']()

if __name__=='__main__':unittest.main(verbosity=2)

