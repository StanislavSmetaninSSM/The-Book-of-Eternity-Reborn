"""T022 detached synthetic DATA admission tests. No process, world import or authority."""
import ast, copy, hashlib, json, tempfile, unittest
from pathlib import Path
import relay_phase_evidence as evidence
SOURCE=Path(__file__).with_name('live-mortal-phase.py')
TREE=ast.parse(SOURCE.read_text())
NS=dict(Path=Path,json=json,hashlib=hashlib,phase_evidence=evidence)
exec(compile(ast.Module(body=[n for n in TREE.body if isinstance(n,ast.FunctionDef)],type_ignores=[]),str(SOURCE),'exec'),NS)
validns=dict(json=json,hashlib=hashlib)
tree=ast.parse(SOURCE.with_name('test-relay-phase-contract.py').read_text())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='valid'],type_ignores=[]),'valid DATA','exec'),validns)
raw=lambda v:json.dumps(v,ensure_ascii=False,sort_keys=True).encode()
sha=lambda b:hashlib.sha256(b).hexdigest()

class RestoredDiagnosticTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.root=Path(self.tmp.name);self.data=self.root/'data';self.data.mkdir();self.base=self.root/'new';self.base.mkdir(mode=0o700)
        self.nonce='a'*32
        self.request=dict(sessionId='historical-session',requestId='accepted-request',turnNumber=44,playerAction='accepted action')
        self.story=dict(turn=44,realm='Mortal World',player='accepted action',narrative='accepted result')
        self.receipt=dict(valid=True,publicationAuthority=False,sessionId='historical-session',requestId='accepted-request',turn=44,priorCount=2,currentCount=3,snapshotToken='A'*64,declarationFingerprint='sha256:'+'b'*64,receiptFingerprint='sha256:'+'c'*64)
        self.history=dict(entries=[dict(turn=44,sessionId='historical-session',requestId='accepted-request',snapshotToken='A'*64,declarationFingerprint='sha256:'+'b'*64,receiptFingerprint='sha256:'+'c'*64,toMinute=1440)])
        self.files={k:b'{}' for k in evidence.RESTORED_MORTAL_DATA_PATHS}
        self.files.update({'stories/mortal_life_2.jsonl':raw(dict(turn=41,player='[TEST SETUP ONLY]'))+b'\n'+raw(self.story)+b'\n',
            'game_state/world/mortal_living_world_history.json':raw(self.history),
            'game_state/world/world_time.json':raw(dict(currentTimeInMinutes=1440)),
            'game_state/history/chat_log.json':raw(dict(sessionId='historical-session',entries=[]))})
        for k,v in self.files.items():p=self.data/k;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(v)
        self.source=validns['valid']();self.source.update(BasePath='/own',SeedSessionId='historical-session',ActualRequest=self.request,ActualRequestSHA256=sha(raw(self.request)),AcceptedTurnNumber=44,AcceptedStory=self.story,StoryPath='/own/game_session/stories/mortal_life_2.jsonl',StoryAfterSHA256=sha(self.files['stories/mortal_life_2.jsonl']),LivingWorldHistorySHA256=sha(self.files['game_state/world/mortal_living_world_history.json']),TypedReceiptEvidence=self.receipt,FinalWorldPins={k:sha(v) for k,v in self.files.items()},FinalConfigSHA256=sha(raw(dict(Language='ru'))))
        self.guard=raw(dict(echild=True,driverExitCode=0,emergencySignals=0,reaped=1,failures=0,deadline=False))
        self.config=raw(dict(Language='ru'));self.request_raw=raw(self.request);self.source_raw=raw(self.source)
        self.descriptor=dict(kind='restored-accepted-data-diagnostic',DiagnosticOnly=True,SourceProbeSHA256=sha(self.source_raw),SourceGuardianSHA256=sha(self.guard),SourceBasePath=self.source['BasePath'],SourceSessionId='historical-session',AcceptedTurn=44,ExpectedTurn=45,DataPins={k:sha(v) for k,v in self.files.items()},ExcludedRequestId='failed-request',BasePath=str(self.base),RuntimeSource='58df9c846eb29606735c2908c38eaf1ceaa8b4ff')
    def call(self):return evidence.restored_mortal_diagnostic_admission(self.descriptor,self.source_raw,self.guard,self.request_raw,self.config,self.data,self.base,self.nonce)
    def test_exact_detached_data_yields_45_not_continuation(self):self.assertEqual(self.call(),45)
    def test_new_original_request_identity(self):
        request=dict(sessionId='historical-session',requestId='new-request',turnNumber=45)
        self.assertTrue(evidence.restored_diagnostic_request_proved(self.descriptor,raw(request)))
    def test_reused_or_foreign_original_request_refused(self):
        request=dict(sessionId='historical-session',requestId='new-request',turnNumber=45)
        for key,value in [('requestId','failed-request'),('sessionId','foreign'),('turnNumber',44)]:
            with self.subTest(key=key):self.assertFalse(evidence.restored_diagnostic_request_proved(self.descriptor,raw(dict(request,**{key:value}))))
    def test_fixed_exact_names(self):
        self.assertEqual(len(evidence.RESTORED_MORTAL_DATA_PATHS),59)
        self.assertEqual(len(set(evidence.RESTORED_MORTAL_DATA_PATHS)),59)
        self.assertNotIn('game_state/control/gm_bridge_status.json',evidence.RESTORED_MORTAL_DATA_PATHS)
    def test_source_provenance_mutations_refused(self):
        for key,value in [('SourceProbeSHA256','0'*64),('SourceGuardianSHA256','0'*64),('SourceSessionId','foreign'),('AcceptedTurn',45),('ExpectedTurn',46),('DiagnosticOnly',False),('kind','seeded-live-mortal'),('SourceBasePath','/foreign')]:
            with self.subTest(key=key):
                old=self.descriptor[key];self.descriptor[key]=value;self.assertIsNone(self.call());self.descriptor[key]=old
    def test_rehashed_source_wrong_original_identity_refused(self):
        self.source['StoppedRecord']['Identity']['RootKey']='/foreign'
        self.source_raw=raw(self.source);self.descriptor['SourceProbeSHA256']=sha(self.source_raw)
        self.assertIsNone(self.call())
    def test_wrong_raw_request_config_guardian_refused(self):
        for attr,value in [('request_raw',raw(dict(self.request,requestId='foreign'))),('config',b'{}'),('guard',raw(dict(echild=False,driverExitCode=0))),('source_raw',b'{')]:
            with self.subTest(attr=attr):
                old=getattr(self,attr);setattr(self,attr,value);self.assertIsNone(self.call());setattr(self,attr,old)
    def test_missing_changed_extra_operational_file_refused(self):
        p=self.data/'game_state/world/world_time.json';old=p.read_bytes();p.unlink();self.assertIsNone(self.call());p.write_bytes(b'{}');self.assertIsNone(self.call());p.write_bytes(old)
        for rel in ['.boe_runtime/gm-runs/main.json','input/turn_request.json','game_state/control/pending_turn_snapshot.authority.json','game_state/control/gm_daemon_status.json','ready/turn_complete.json','unknown.json']:
            with self.subTest(rel=rel):
                q=self.data/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b'{}');self.assertIsNone(self.call());q.unlink()
    def test_wrong_story_history_clock_even_rehashed_refused(self):
        for rel,value in [('stories/mortal_life_2.jsonl',raw(dict(self.story,turn=45))+b'\n'),('game_state/world/mortal_living_world_history.json',raw(dict(entries=[]))),('game_state/world/world_time.json',raw(dict(currentTimeInMinutes=1680)))]:
            with self.subTest(rel=rel):
                p=self.data/rel;old=p.read_bytes();oldhash=self.descriptor['DataPins'][rel];p.write_bytes(value);self.descriptor['DataPins'][rel]=sha(value);self.assertIsNone(self.call());p.write_bytes(old);self.descriptor['DataPins'][rel]=oldhash
    def test_link_alias_and_overlap_refused(self):
        p=self.data/'lore/codex_entries.json';old=p.read_bytes();target=self.root/'outside';target.write_bytes(old);p.unlink();p.symlink_to(target);self.assertIsNone(self.call());p.unlink();p.write_bytes(old)
        original=self.base;self.base=self.data;self.assertIsNone(self.call());self.base=original
        alias=self.root/'alias';alias.symlink_to(self.base,target_is_directory=True);self.base=alias;self.assertIsNone(self.call());self.base=original
    def test_nonempty_nonprivate_newroot_and_nonce_refused(self):
        p=self.base/'foreign';p.write_bytes(b'x');self.assertIsNone(self.call());p.unlink()
        self.base.chmod(0o755);self.assertIsNone(self.call());self.base.chmod(0o700)
        for nonce in ['', 'not-guid', 'A'*32]:self.nonce=nonce;self.assertIsNone(self.call())
    def test_descriptor_extra_traversal_and_wrong_source_outcome_refused(self):
        self.descriptor['DataPins']['../escape']='0'*64;self.assertIsNone(self.call());del self.descriptor['DataPins']['../escape']
        self.source['Success']=False;self.source_raw=raw(self.source);self.descriptor['SourceProbeSHA256']=sha(self.source_raw);self.assertIsNone(self.call())
    def test_normal_seed_and_continuation_never_accept_diagnostic_result(self):
        seed=dict(kind='seeded-live-mortal',acceptedGmTurn=False,setupTurn=41,sessionId='historical-session',basePath=str(self.base))
        self.assertEqual(NS['phase_admission'](seed,None,41),42)
        with self.assertRaises(ValueError):NS['phase_admission'](self.descriptor,None,44)
        previous=copy.deepcopy(self.source);previous['RestoredAcceptedDataDiagnostic']=True
        # Must reject the diagnostic result before any current-world/owner reads.
        with self.assertRaisesRegex(ValueError,'[Dd]iagnostic'):NS['phase_admission'](seed,previous,44,json.loads(self.guard))

if __name__=='__main__':unittest.main(verbosity=2)
