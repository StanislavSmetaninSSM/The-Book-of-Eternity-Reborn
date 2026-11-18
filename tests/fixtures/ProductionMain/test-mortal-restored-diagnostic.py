"""T022 detached synthetic DATA admission tests. No process, world import or authority."""
import ast, copy, hashlib, json, tempfile, unittest, shutil, sys, re
from pathlib import Path
import relay_phase_evidence as evidence
SOURCE=Path(__file__).with_name('live-mortal-phase.py')
TREE=ast.parse(SOURCE.read_text())
NS=dict(Path=Path,json=json,hashlib=hashlib,phase_evidence=evidence,shutil=shutil,re=re)
exec(compile(ast.Module(body=[n for n in TREE.body if isinstance(n,ast.FunctionDef)],type_ignores=[]),str(SOURCE),'exec'),NS)
validns=dict(json=json,hashlib=hashlib)
tree=ast.parse(SOURCE.with_name('test-relay-phase-contract.py').read_text())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='valid'],type_ignores=[]),'valid DATA','exec'),validns)
raw=lambda v:json.dumps(v,ensure_ascii=False,sort_keys=True).encode()
sha=lambda b:hashlib.sha256(b).hexdigest()

SOURCE_EXCLUDED_NAMES = (
    'game_state/control/gm_bridge_status.json',
    'game_state/control/gm_context_pack/CLI_Agent_Daemon_Specification.md',
    'game_state/control/gm_context_pack/Examples/E_CLI_Afterlife_Turns.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_Effect_Materialization.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_Ink_Feather_Actions.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_Mortal_Item_Materialization.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_Mortal_Living_World.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_Mortal_Npc_Profile_Change.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_Mortal_Npc_Progression.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_Mortal_Npc_Progression_Consequences.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_Mortal_Resources.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_New_Vehicle_Cargo_Materialization.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_Step_Main.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_Training_Showcases.txt',
    'game_state/control/gm_context_pack/Examples/E_CLI_Wound_Materialization.txt',
    'game_state/control/gm_context_pack/Lessons/GM_EXPERIENCE_LESSONS.json',
    'game_state/control/gm_context_pack/Lessons/GM_EXPERIENCE_LESSONS.md',
    'game_state/control/gm_context_pack/OtherGuides/Afterlife_Combat_Terminology_Glossary.md',
    'game_state/control/gm_context_pack/OtherGuides/Afterlife_Contract_Matrix.md',
    'game_state/control/gm_context_pack/OtherGuides/Effect_Materialization_Contract.md',
    'game_state/control/gm_context_pack/OtherGuides/Wound_Materialization_Contract.md',
    'game_state/control/gm_context_pack/Probes/GM_SAFE_PROBES.json',
    'game_state/control/gm_context_pack/Probes/GM_SAFE_PROBES.md',
    'game_state/control/gm_context_pack/README.md',
    'game_state/control/gm_context_pack/Rubrics/GM_LIVE_TEST_RUBRIC.json',
    'game_state/control/gm_context_pack/Rubrics/GM_LIVE_TEST_RUBRIC.md',
    'game_state/control/gm_context_pack/TaskGuides/CLI_Step_Main.txt',
    'game_state/control/gm_context_pack/Templates/ACTOR_REASONING_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/AFTERLIFE_CHRONICLE_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json',
    'game_state/control/gm_context_pack/Templates/MORTAL_COMBAT_STATE_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/MORTAL_ITEM_MATERIALIZATION_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/MORTAL_LIVING_WORLD_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/MORTAL_NPC_UPDATE_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/MORTAL_SKILL_PROGRESSION_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/OUTPUT_ARTIFACT_REPAIR_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/PROGRESSION_REPORT_TEMPLATE.json',
    'game_state/control/gm_context_pack/Templates/TURN_OUTPUT_TEMPLATE.md',
    'game_state/control/gm_context_pack/Templates/VALIDATION_REPAIR_TEMPLATE.md',
    'game_state/control/gm_context_pack/context_pack_manifest.json',
    'game_state/control/gm_daemon_status.json',
    'game_state/control/gm_observed_terminal_requests.json',
    'game_state/control/gm_trajectory_ledger.jsonl',
    'game_state/control/gm_turn_helper.bootstrap.ps1',
)

class RestoredDiagnosticTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.root=Path(self.tmp.name);self.data=self.root/'data';self.data.mkdir();self.base=self.root/'new';self.base.mkdir(mode=0o700)
        self.nonce='a'*32
        self.request=dict(sessionId='historical-session',requestId='accepted-request',turnNumber=44,playerAction='accepted action')
        self.story=dict(turn=44,realm='Mortal World',player='accepted action',narrative='accepted result')
        self.receipt=dict(valid=True,publicationAuthority=False,sessionId='historical-session',requestId='accepted-request',turn=44,priorCount=2,currentCount=3,snapshotToken='A'*64,declarationFingerprint='sha256:'+'b'*64,receiptFingerprint='sha256:'+'c'*64)
        self.history=dict(schemaVersion=1,entries=[dict(turn=t,sessionId='historical-session',requestId='old-request-'+str(t),snapshotToken='D'*64,declarationFingerprint='sha256:'+'e'*64,receiptFingerprint='sha256:'+'f'*64,toMinute=minute) for t,minute in [(42,720),(43,960)]]+[dict(turn=44,sessionId='historical-session',requestId='accepted-request',snapshotToken='A'*64,declarationFingerprint='sha256:'+'b'*64,receiptFingerprint='sha256:'+'c'*64,toMinute=1440)])
        self.files={k:b'{}' for k in evidence.RESTORED_MORTAL_DATA_PATHS}
        self.files.update({'stories/mortal_life_2.jsonl':raw(dict(turn=41,player='[TEST SETUP ONLY]'))+b'\n'+b''.join(raw(dict(turn=t,realm='Mortal World',player='old action',narrative='old result'))+b'\n' for t in [42,43])+raw(self.story)+b'\n',
            'game_state/world/mortal_living_world_history.json':raw(self.history),
            'game_state/world/world_time.json':raw(dict(currentTimeInMinutes=1440)),
            'game_state/control/progression_schedule.json':raw(dict(currentRealm='Mortal World',currentWorldTimeInMinutes=1440,hasAuthoritativeWorldTimeBaseline=True)),
            'game_state/control/pending_dice_state.json':raw(dict(preGeneratedDices1d20=[10]*20,gachaBaseResult=dict(diceUsed=[],baseScore=36,baseRarity='Common',formula='client-computed gacha base (range 4-80)'),isFateLocked=False,createdAtUtc='2026-01-01T00:00:00Z',lastUpdatedUtc='2026-01-01T00:00:00Z')),
            'game_state/control/pending_ink_actions.json':raw(dict(pendingActions=[])),
            'game_state/history/chat_log.json':raw(dict(sessionId='historical-session',entries=[]))})
        for k,v in self.files.items():p=self.data/k;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(v)
        self.source=validns['valid']();self.source.update(BasePath='/own',SeedSessionId='historical-session',ActualRequest=self.request,ActualRequestSHA256=sha(raw(self.request)),AcceptedTurnNumber=44,AcceptedStory=self.story,StoryPath='/own/game_session/stories/mortal_life_2.jsonl',StoryAfterSHA256=sha(self.files['stories/mortal_life_2.jsonl']),LivingWorldHistorySHA256=sha(self.files['game_state/world/mortal_living_world_history.json']),TypedReceiptEvidence=self.receipt,FinalWorldPins={k:sha(v) for k,v in self.files.items()},FinalConfigSHA256=sha(raw(dict(Language='ru'))))
        self.source['FinalWorldPins'].update({name:sha(raw(dict(syntheticExcludedPath=name))) for name in SOURCE_EXCLUDED_NAMES})
        assert len(self.source['FinalWorldPins'])==106
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
    def producer_setup(self):
        out=self.root/'phase';out.mkdir(mode=0o700);(out/'relay').mkdir();(out/'relay/relay.py').write_bytes(b'synthetic DATA')
        runtime=self.root/'runtime';runtime.mkdir();(runtime/'client.bin').write_bytes(b'synthetic DATA, never executable')
        helperdir=self.root/'helper';helperdir.mkdir();helper=helperdir/'helper.dll'
        for name in ['helper.dll','helper.deps.json','helper.runtimeconfig.json']:(helperdir/name).write_bytes(b'{}')
        package=dict(RuntimePins=evidence.relay_inventory(runtime),RelayPins=evidence.relay_inventory(out/'relay'),ReceiptHelperPins=evidence.relay_inventory(helperdir),DriverSHA256=sha(SOURCE.read_bytes()),EvidenceModuleSHA256=sha(Path(evidence.__file__).read_bytes()),ConsoleRouteModuleSHA256=sha(Path(evidence.__file__).with_name('mortal_console_routes.py').read_bytes()))
        (out/'relay-phase-package.json').write_bytes(raw(package));self.source['PhasePackage']=package
        self.source_raw=raw(self.source);self.descriptor['SourceProbeSHA256']=sha(self.source_raw)
        paths={}
        for key,value in [('Probe',self.source_raw),('Guardian',self.guard),('Request',self.request_raw),('Config',self.config)]:
            path=self.root/(key+'.json');path.write_bytes(value);paths[key]=str(path)
        self.descriptor.update(SourceEvidence=paths,DataPath=str(self.data),RuntimePath=str(runtime))
        return out,helper
    def test_actual_preparation_imports_only_bound_data_once(self):
        out,helper=self.producer_setup()
        base,session,ship,turn,package,_=NS['prepare_restored_diagnostic'](self.descriptor,out,helper,SOURCE,self.nonce)
        self.assertEqual(turn,45);self.assertEqual(base,self.base)
        for name,value in self.files.items():self.assertEqual((session/name).read_bytes(),value)
        self.assertEqual((session/'config.json').read_bytes(),self.config)
        self.assertFalse((base/'.boe_runtime').exists());self.assertFalse((session/'input').exists())
        self.assertFalse((session/'game_state/control/gm_bridge_status.json').exists())
        with self.assertRaises(ValueError):NS['prepare_restored_diagnostic'](self.descriptor,out,helper,SOURCE,self.nonce)
    def test_actual_preparation_failed_hash_never_creates_session(self):
        out,helper=self.producer_setup();self.descriptor['DataPins']['lore/codex_entries.json']='0'*64
        with self.assertRaises(ValueError):NS['prepare_restored_diagnostic'](self.descriptor,out,helper,SOURCE,self.nonce)
        self.assertEqual(list(self.base.iterdir()),[])
    def test_diagnostic_owner_join_rejects_reused_or_foreign_identity(self):
        calls=[n for n in ast.walk(TREE) if isinstance(n,ast.Call) and isinstance(n.func,ast.Attribute)]
        joins=[n for n in calls if n.func.attr=='require_restored_diagnostic_owner']
        continues=[n for n in calls if n.func.attr=='send' and any(isinstance(a,ast.Constant) and a.value=='observed Continue selected' for a in n.args)]
        self.assertEqual(len(joins),1);self.assertEqual(len(continues),1)
        self.assertLess(joins[0].lineno,continues[0].lineno)
        self.assertEqual(ast.unparse(joins[0].args[0]),"record['Identity']")
        self.assertEqual(ast.unparse(joins[0].args[3]),'daemon_owner')
        old=self.source['OriginalRunningRecord']['Identity']
        current=dict(old,RootKey=str(self.base),GenerationId='new-generation',RunId='new-run')
        old_daemon=evidence.daemon_pair_decode(self.source['DaemonSettlement'],'Final',True)
        evidence.require_restored_diagnostic_owner(current,self.source,self.base,'new-daemon')
        for key,value in [('RootKey','/foreign'),('GenerationId',old['GenerationId']),('RunId',old['RunId']),('GenerationId',''),('RunId','')]:
            with self.subTest(key=key,value=value),self.assertRaises(ValueError):
                evidence.require_restored_diagnostic_owner(dict(current,**{key:value}),self.source,self.base,'new-daemon')
        for owner in [old_daemon,'']:
            with self.subTest(owner=owner),self.assertRaises(ValueError):
                evidence.require_restored_diagnostic_owner(current,self.source,self.base,owner)
    def test_actual_preparation_never_copies_unpinned_cache(self):
        out,helper=self.producer_setup()
        cache=Path(self.descriptor['RuntimePath'])/'__pycache__';cache.mkdir();(cache/'untrusted.pyc').write_bytes(b'unpinned')
        _,_,ship,_,package,_=NS['prepare_restored_diagnostic'](self.descriptor,out,helper,SOURCE,self.nonce)
        self.assertFalse((ship/'__pycache__').exists())
        self.assertEqual({str(p.relative_to(ship)) for p in ship.rglob('*') if p.is_file()},set(package['RuntimePins']))
    def test_pinned_executable_preserves_bytes_and_safe_owner_execute(self):
        out,helper=self.producer_setup()
        original=Path(self.descriptor['RuntimePath'])/'client.bin'
        original.chmod(0o4755) # Privileged bits must never propagate from source metadata.
        _,_,ship,_,package,_=NS['prepare_restored_diagnostic'](self.descriptor,out,helper,SOURCE,self.nonce)
        copied=ship/'client.bin'
        self.assertEqual(copied.read_bytes(),original.read_bytes())
        self.assertEqual(copied.stat().st_mode & 0o7777,0o700)
        self.assertEqual(sha(copied.read_bytes()),package['RuntimePins']['client.bin'])
    def test_fixed_exact_names(self):
        self.assertEqual(len(evidence.RESTORED_MORTAL_DATA_PATHS),59)
        self.assertEqual(len(set(evidence.RESTORED_MORTAL_DATA_PATHS)),59)
        self.assertNotIn('game_state/control/gm_bridge_status.json',evidence.RESTORED_MORTAL_DATA_PATHS)
    def test_source_provenance_mutations_refused(self):
        for key,value in [('SourceProbeSHA256','0'*64),('SourceGuardianSHA256','0'*64),('SourceSessionId','foreign'),('AcceptedTurn',45),('ExpectedTurn',46),('DiagnosticOnly',False),('kind','seeded-live-mortal'),('SourceBasePath','/foreign')]:
            with self.subTest(key=key):
                old=self.descriptor[key];self.descriptor[key]=value;self.assertIsNone(self.call());self.descriptor[key]=old
    def test_rehashed_source_wrong_original_identity_refused(self):
        self.source['StoppedRecord']['Identity']=dict(self.source['StoppedRecord']['Identity'],RootKey='/foreign')
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
                p=self.data/rel;old=p.read_bytes();oldsource=copy.deepcopy(self.source);oldhash=self.descriptor['DataPins'][rel]
                p.write_bytes(value);self.descriptor['DataPins'][rel]=sha(value);self.source['FinalWorldPins'][rel]=sha(value)
                if rel.startswith('stories/'):self.source['StoryAfterSHA256']=sha(value)
                if rel.endswith('mortal_living_world_history.json'):self.source['LivingWorldHistorySHA256']=sha(value)
                self.source_raw=raw(self.source);self.descriptor['SourceProbeSHA256']=sha(self.source_raw)
                self.assertIsNone(self.call())
                p.write_bytes(old);self.descriptor['DataPins'][rel]=oldhash;self.source=oldsource;self.source_raw=raw(self.source);self.descriptor['SourceProbeSHA256']=sha(self.source_raw)
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
    def test_diagnostic_marker_rejected_before_ordinary_continuation_checks(self):
        seed=dict(kind='seeded-live-mortal',acceptedGmTurn=False,setupTurn=41,sessionId='historical-session',basePath=str(self.base))
        self.assertEqual(NS['phase_admission'](seed,None,41),42)
        with self.assertRaises(ValueError):NS['phase_admission'](self.descriptor,None,44)
        previous=copy.deepcopy(self.source);previous['RestoredAcceptedDataDiagnostic']=True
        # Must reject the diagnostic result before any current-world/owner reads.
        with self.assertRaisesRegex(ValueError,'[Dd]iagnostic'):NS['phase_admission'](seed,previous,44,json.loads(self.guard))

if __name__=='__main__':
    if '--category-contract' in sys.argv:
        result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromModule(sys.modules[__name__]))
        ok=result.wasSuccessful() and result.testsRun==18 and not result.skipped
        print('RESTORED-DATA-RESULT '+json.dumps(dict(testsRun=result.testsRun,failures=len(result.failures),errors=len(result.errors),skipped=len(result.skipped))))
        sys.exit(0 if ok else 1)
    unittest.main(verbosity=2)
