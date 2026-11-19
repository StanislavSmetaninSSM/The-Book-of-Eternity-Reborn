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
  with self.assertRaises(SystemExit):self.call(phase='p03')
  self.assertEqual(self.calls,0)

class ProfileBinding(unittest.TestCase):
 def test_actual_binding_shape_and_refusals(self):
  import ast,copy,re
  source=p.with_name('prepare-mm21-profile.py').read_text();tree=ast.parse(source)
  node=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='checked_continuation_binding');scope={'re':re}
  exec(compile(ast.Module(body=[node],type_ignores=[]),'<actual-profile-binding>','exec'),scope)
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

if __name__=='__main__':unittest.main(verbosity=2)

