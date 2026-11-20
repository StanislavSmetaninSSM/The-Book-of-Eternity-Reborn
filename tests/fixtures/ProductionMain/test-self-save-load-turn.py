"""#1515 T035 bounded independent DATA controls; never launches a game or GM."""
import ast, copy, importlib.util, io, json, pathlib, tempfile, unittest, zipfile, shutil
from unittest.mock import patch
import mortal_console_routes as routes
import relay_phase_evidence as evidence
ROOT=pathlib.Path(__file__).parent
spec=importlib.util.spec_from_file_location('load_data',ROOT/'test-relay-load-evidence.py')
load_data=importlib.util.module_from_spec(spec);spec.loader.exec_module(load_data)

class SelfSaveLoadTurn(unittest.TestCase):
    def test_closed_route_is_opt_in_and_default_unchanged(self):
        self.assertEqual(routes.checked_route(None),{'Route':'birth'})
        try:observed=routes.checked_route({'Route':'self-save-load-turn'})
        except ValueError:self.fail('missing explicit self-save-load-turn route')
        self.assertEqual(observed,{'Route':'self-save-load-turn'})
        for extra in ('ArchivePath','Keys','StorageId'):
            with self.assertRaises(ValueError):routes.checked_route({'Route':'self-save-load-turn',extra:'foreign'})

    def test_load_nonce_is_pinned_and_absent_from_default_route(self):
        check=getattr(evidence,'require_route_load_nonce',None)
        self.assertTrue(callable(check),'missing launch-bound Load nonce')
        nonce='e'*32;env={'BOE_TEST_LOAD_OBSERVATION_NONCE':nonce};profile={'LoadObservationNonce':nonce,'GMDecisionDeferred':True}
        self.assertEqual(check('self-save-load-turn',profile,env),nonce)
        self.assertEqual(check('storage-local-proof',{},env),nonce)
        self.assertIsNone(check('birth',{},{}))
        for route,p,e in [('self-save-load-turn',{},env),('self-save-load-turn',profile,{}),('self-save-load-turn',profile,dict(env,BOE_TEST_LOAD_OBSERVATION_NONCE='f'*32)),('birth',profile,env),('ordinary-save',{},env)]:
            with self.assertRaises(ValueError):check(route,p,e)

    def test_launch_profile_is_exact_initial_context_before_side_effects(self):
        check=getattr(evidence,'require_self_save_launch',None)
        self.assertTrue(callable(check),'missing independent launch binding')
        with tempfile.TemporaryDirectory() as name:
            root=pathlib.Path(name);out=root/'p01';out.mkdir();seed=root/'seed-manifest.json';helper=root/'receipt-helper/BookOfEternityClient.TestSupport.dll'
            seed_raw=json.dumps({'kind':'seeded-live-mortal','basePath':str(root/'play')}).encode();action=b'one action';profile={'Route':'self-save-load-turn','ExpectedTurn':42,'PreviousResult':None,'PreviousSHA256':None,'SeedSHA256':evidence.sha(seed_raw),'ActionSHA256':evidence.sha(action)}
            check(out,seed,seed_raw,action,helper,profile,None,0)
            for key,value in [('Route','birth'),('ExpectedTurn',True),('PreviousResult','foreign'),('PreviousSHA256','a'*64),('SeedSHA256','a'*64),('ActionSHA256','a'*64)]:
                with self.subTest(key=key),self.assertRaises(ValueError):check(out,seed,seed_raw,action,helper,dict(profile,**{key:value}),None,0)
            for phase,receipt,previous,optimized in ((root/'p02',helper,None,0),(out,root/'foreign.dll',None,0),(out,helper,{},0),(out,helper,None,1)):
                with self.assertRaises(ValueError):check(phase,seed,seed_raw,action,receipt,profile,previous,optimized)

    def test_only_external_runtime_survives_exact_active_load(self):
        check=getattr(evidence,'self_save_runtime_root',None)
        self.assertTrue(callable(check),'missing external immutable runtime admission')
        with tempfile.TemporaryDirectory() as name:
            root=pathlib.Path(name);base=root/'world';session=base/'game_session';session.mkdir(parents=True);package=root/'package';package.mkdir()
            self.assertEqual(check(base),package)
            nested=session/'_runtime';nested.mkdir()
            with self.assertRaises(ValueError):check(base)
            nested.rmdir();nested.symlink_to(root/'absent')
            with self.assertRaises(ValueError):check(base)
            nested.unlink();package.rmdir();package.symlink_to(root/'absent')
            with self.assertRaises(ValueError):check(base)

    def test_only_initial_setup41_can_enter(self):
        check=getattr(evidence,'require_self_save_initial',None)
        self.assertTrue(callable(check),'missing initial self-Save admission')
        with tempfile.TemporaryDirectory() as name:
            session=pathlib.Path(name)/'game_session';session.mkdir()
            check(session,None,False,False,42)
            for args in (({},False,False,42),(None,True,False,42),(None,False,True,42),(None,False,False,44),(None,False,False,True)):
                with self.assertRaises(ValueError):check(session,*args)
            for directory in ('manual_saves','autosaves','checkpoint_saves'):
                p=session/'saves'/directory/'old.zip';p.parent.mkdir(parents=True);p.write_bytes(b'old')
                with self.assertRaises(ValueError):check(session,None,False,False,42)
                p.unlink()

    def test_archive_pin_is_exact_basename_hash_and_single_menu(self):
        check=getattr(evidence,'self_save_archive_pin',None)
        self.assertTrue(callable(check),'missing exact self-Save archive readback')
        with tempfile.TemporaryDirectory() as name:
            session=pathlib.Path(name)/'game_session';p=session/'saves/manual_saves/s_new.zip';p.parent.mkdir(parents=True);p.write_bytes(b'new')
            pin=check(session,p,b'new')
            self.assertEqual(pin,{'Path':str(p),'Name':p.name,'SHA256':evidence.sha(b'new'),'Mode':p.stat().st_mode&0o777})
            for path,raw in ((p,b'old'),(p.with_name('foreign.zip'),b'new')):
                with self.assertRaises(ValueError):check(session,path,raw)
            extra=p.with_name('old.zip');extra.write_bytes(b'old')
            with self.assertRaises(ValueError):check(session,p,b'new')
            extra.unlink();p.unlink();p.symlink_to(extra)
            with self.assertRaises(ValueError):check(session,p,b'new')

    def test_final_manual_pin_allows_stock_autosave_but_not_selection_reuse(self):
        with tempfile.TemporaryDirectory() as name:
            session=pathlib.Path(name)/'game_session';archive=session/'saves/manual_saves/s_own.zip';archive.parent.mkdir(parents=True);archive.write_bytes(b'own')
            before=evidence.self_save_archive_pin(session,archive,b'own')
            auto=session/'saves/autosaves/auto42.zip';auto.parent.mkdir(parents=True);auto.write_bytes(b'stock-auto42')
            try:final=evidence.self_save_archive_pin(session,archive,b'own',final=True)
            except TypeError:self.fail('missing separate final manual archive pin')
            self.assertEqual(before,final)
            with self.assertRaises(ValueError):evidence.self_save_archive_pin(session,archive,b'own')
            archive.chmod(0o600 if before['Mode']!=0o600 else 0o644)
            self.assertNotEqual(before,evidence.self_save_archive_pin(session,archive,b'own',final=True))
            extra=archive.with_name('foreign.zip');extra.write_bytes(b'foreign')
            with self.assertRaises(ValueError):evidence.self_save_archive_pin(session,archive,b'own',final=True)

    def snapshot_fixture(self,root):
        session=root/'game_session';story=session/'stories/mortal_life_2.jsonl';story.parent.mkdir(parents=True)
        story.write_text('{"turn":41,"player":"setup"}\n');(session/'config.json').write_bytes(b'{"GmBridgeEnabled":true}')
        queue=root/'queue';queue.mkdir()
        for name in evidence.ORDINARY_SAVE_PATHS:
            p=session/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b'{}')
        return session,story,queue

    def test_scoped_snapshot_is_actual_setup_not_fake_acceptance(self):
        check=getattr(evidence,'self_save_load_snapshot',None)
        self.assertTrue(callable(check),'missing setup41 scoped DATA snapshot')
        with tempfile.TemporaryDirectory() as name:
            session,story,queue=self.snapshot_fixture(pathlib.Path(name));before=check(session,story,queue)
            self.assertEqual(before['Turn'],41);self.assertEqual(before['Queue'],{})
            self.assertEqual(before['Files']['stories/mortal_life_2.jsonl'],story.read_bytes().hex())
            self.assertEqual(before['ConfigSHA256'],evidence.sha((session/'config.json').read_bytes()))
            self.assertEqual(before,check(session,story,queue))
            (session/evidence.ORDINARY_SAVE_PATHS[0]).write_bytes(b'{"changed":1}')
            self.assertNotEqual(before,check(session,story,queue))

    def test_snapshot_rejects_pending_queue_and_nonsetup_story(self):
        check=getattr(evidence,'self_save_load_snapshot',None)
        self.assertTrue(callable(check),'missing fresh no-request snapshot')
        with tempfile.TemporaryDirectory() as name:
            session,story,queue=self.snapshot_fixture(pathlib.Path(name))
            for relative in ('input/turn_request.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json','ready/turn_complete.json'):
                p=session/relative;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b'{}')
                with self.assertRaises(ValueError):check(session,story,queue)
                p.unlink()
            p=queue/'request-1';p.mkdir()
            with self.assertRaises(ValueError):check(session,story,queue)
            p.rmdir();story.write_text('{"turn":42}\n')
            with self.assertRaises(ValueError):check(session,story,queue)

    def test_actual_setup_archive_bytes_verify_without_acceptance_claim(self):
        with tempfile.TemporaryDirectory() as name:
            session,story,queue=self.snapshot_fixture(pathlib.Path(name))
            check=getattr(evidence,'self_save_load_snapshot',None)
            self.assertTrue(callable(check),'missing actual setup snapshot')
            snapshot=check(session,story,queue)
            def archive(changed=False):
                stream=io.BytesIO()
                with zipfile.ZipFile(stream,'w') as z:
                    z.writestr('save_metadata.json',json.dumps({'saveName':'s','turnNumber':41}))
                    for path,raw in snapshot['Files'].items():
                        if raw is not None:z.writestr(path,b'foreign' if changed and path==snapshot['StoryPath'] else bytes.fromhex(raw))
                return stream.getvalue()
            self.assertTrue(evidence.verify_ordinary_save_archive(archive(),snapshot,41))
            with self.assertRaises(ValueError):evidence.verify_ordinary_save_archive(archive(True),snapshot,41)
            with self.assertRaises(ValueError):evidence.verify_ordinary_save_archive(archive(),snapshot,42)

    def test_own_archive_requires_full_manifest_exact_config_and_no_runtime(self):
        verify=getattr(evidence,'verify_self_save_archive',None)
        self.assertTrue(callable(verify),'missing full own Save content/manifest verification')
        with tempfile.TemporaryDirectory() as name:
            session,story,queue=self.snapshot_fixture(pathlib.Path(name));snapshot=evidence.self_save_load_snapshot(session,story,queue)
            def archive(defect=None):
                payload={path:bytes.fromhex(raw) for path,raw in snapshot['Files'].items() if raw is not None}
                payload['config.json']=bytes.fromhex(snapshot['ConfigRawHex'])
                payload['save_metadata.json']=json.dumps({'saveName':'s','turnNumber':41}).encode()
                if defect=='config':payload['config.json']=b'{"foreign":true}'
                if defect=='runtime':payload['_runtime/BookOfEternityClient.dll']=b'not-allowed'
                if defect=='daemon':payload['game_state/control/gm_daemon_status.json']=b'{}'
                if defect=='bridge':payload['game_state/control/gm_bridge_status.json']=b'{}'
                if defect=='pending':payload['game_state/control/pending_turn_snapshot/body.json']=b'{}'
                entries=[{'path':path,'length':len(raw),'sha256':evidence.sha(raw)} for path,raw in payload.items()]
                if defect=='length':entries[0]['length']+=1
                if defect=='hash':entries[0]['sha256']='f'*64
                if defect=='missing':entries.pop()
                if defect=='duplicate':entries.append(dict(entries[0]))
                stream=io.BytesIO()
                with zipfile.ZipFile(stream,'w') as z:
                    for path,raw in payload.items():z.writestr(path,raw)
                    z.writestr('save_manifest.json',json.dumps({'schemaVersion':1,'algorithm':'SHA-256','entries':entries}))
                return stream.getvalue()
            proof=verify(archive(),snapshot)
            self.assertTrue(proof['ManifestVerified']);self.assertTrue(proof['ConfigBytesExact']);self.assertTrue(proof['RuntimeAbsent'])
            for defect in ('config','runtime','daemon','bridge','pending','length','hash','missing','duplicate'):
                with self.subTest(defect=defect),self.assertRaises(ValueError):verify(archive(defect),snapshot)

    def test_B_status_requires_terminal_tuple_nonempty_binding_and_current_dispatch(self):
        check=getattr(evidence,'require_self_save_turn_status',None)
        self.assertTrue(callable(check),'missing strict fresh B dispatch join')
        expected,doc,v=self.completed();check(v,v['LoadRunningStatus'],True)
        for key,value in [('terminalRunId','a'*32),('inputBindingId',''),('backend',2),('terminalGuarantee','foreign'),('shellPid',True),('shellPid',999),('terminalOwnerRetained',False),('terminalUncertain',True)]:
            status=copy.deepcopy(v['LoadRunningStatus']);status['status'][key]=value
            with self.subTest(key=key,value=value),self.assertRaises(ValueError):check(v,status,False)
        status=copy.deepcopy(v['LoadRunningStatus']);status['status']['promptDelivery']={'inputBindingId':'binding-A'}
        with self.assertRaises(ValueError):check(v,status,False)

    def test_completed_route_requires_actual_turn_receipt_story_and_terminal_closure(self):
        prove=getattr(evidence,'relay_self_save_load_turn_cleanup_proved',None)
        self.assertTrue(callable(prove),'missing strict acceptance join')
        for key in ('ActualRequest','ActualRequestRawHex','TypedReceiptEvidence','AcceptedStory','AcceptedStoryAfterRawHex','ClientTerminalClosure','ReceiptHelper'):
            expected,doc,v=self.completed();v.pop(key)
            with self.subTest(key=key):self.assertFalse(prove(v,load_data.wire(doc),0,expected))
        expected,doc,v=self.completed();continuity=v['PostLoadDaemonContinuity']
        raw=bytes.fromhex(continuity['PostLoadGuardRawHex'])+b' '
        continuity['PostLoadGuardRawHex']=raw.hex();continuity['PostLoadGuardSHA256']=evidence.sha(raw)
        self.assertTrue(evidence.retained_daemon_load_continuity_proved(v['DaemonSettlement'],continuity))
        self.assertFalse(prove(v,load_data.wire(doc),0,expected),'same daemon requires exact pre/post guard bytes')

    def test_queue_shape_refuses_repairs_extra_directories_and_foreign_request(self):
        check=getattr(evidence,'require_self_save_queue',None)
        self.assertTrue(callable(check),'missing sole turn-only queue boundary')
        with tempfile.TemporaryDirectory() as name:
            queue=pathlib.Path(name);raw=b'{"sessionId":"s","requestId":"r","turnNumber":42}'
            self.assertEqual(check(queue,raw),[])
            request=queue/('request-1-'+'a'*32);request.mkdir();meta={'Kind':'turn','TurnRequestSHA256':evidence.sha(raw),'TurnIdentity':json.loads(raw)};(request/'request.json').write_text(json.dumps(meta))
            good=check(queue,raw,complete=True);self.assertEqual(len(good),1)
            for key,value in [('Kind','repair'),('TurnRequestSHA256','a'*64),('TurnIdentity',{})]:
                (request/'request.json').write_text(json.dumps(dict(meta,**{key:value})))
                with self.subTest(key=key),self.assertRaises(ValueError):check(queue,raw)
            (request/'request.json').write_text(json.dumps(meta))
            extra=queue/'unexpected';extra.mkdir()
            with self.assertRaises(ValueError):check(queue,raw)
            extra.rmdir();extra=queue/('request-2-'+'b'*32);extra.mkdir()
            with self.assertRaises(ValueError):check(queue,raw)

    def test_actual_unresolved_load_never_publishes_relay_close(self):
        tree=ast.parse((ROOT/'live-mortal-phase.py').read_text());main=next(n for n in tree.body if isinstance(n,ast.Try))
        block=next(n for n in main.finalbody if isinstance(n,ast.If) and isinstance(n.test,ast.Name) and n.test.id=='relay_mode')
        calls=[]
        def close(*unused):calls.append('published');return True
        scope={'relay_mode':True,'route':{'Route':'self-save-load-turn'},'load_cut':3,'original':None,'result':{},'relay_queue':pathlib.Path('/unused'),'close_relay_execution':close,'phase_evidence':type('E',(),{'relay_inventory':staticmethod(lambda _: {})})}
        exec(compile(ast.Module(body=[block],type_ignores=[]),'actual unresolved Load finally','exec'),scope)
        self.assertEqual(calls,[],'unresolved B may still start: no queue close writes')
        self.assertFalse(scope['relay_closed']);self.assertIn('RelayCloseFailure',scope['result'])

    def test_actual_preaction_rechecks_B_and_captures_postload_baseline(self):
        tree=ast.parse((ROOT/'live-mortal-phase.py').read_text());node=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='self_save_load_pre_action')
        with tempfile.TemporaryDirectory() as name:
            root=pathlib.Path(name);session,story,queue=self.snapshot_fixture(root);out=root/'p01';out.mkdir()
            archive=session/'saves/manual_saves/s_own.zip';archive.parent.mkdir(parents=True);archive.write_bytes(b'own')
            _,_,value=self.completed();snapshot=evidence.self_save_load_snapshot(session,story,queue);value['SelfSaveLoadProof']['Before']=snapshot
            value['SelfSaveLoadProof']['Archive']=evidence.self_save_archive_pin(session,archive,b'own')
            marker=session/'game_state/control/post-load-B.json';marker.write_bytes(b'B baseline')
            package={'ReceiptHelperPins':{}};scope={'self_save_load_context':{'ArchivePath':str(archive),'ArchiveBytes':b'own'},'phase_evidence':evidence,'out':out,'ship':root/'package','__file__':str(ROOT/'live-mortal-phase.py'),'package_manifest':package,'receipt_helper':root/'helper.dll','base':root,'trace_profile':{},'env':{},'read_json':lambda _:value['TurnRunningRecord'],'record_path':root/'main.json','rpc':lambda _:value['LoadRunningStatus'],'result':value,'session':session,'story':story,'relay_queue':queue,'Path':pathlib.Path,'shutil':shutil,'journal':[],'elapsed':lambda:1.0}
            exec(compile(ast.Module(body=[node],type_ignores=[]),'actual self-Save preaction','exec'),scope)
            with patch.object(evidence,'relay_package_preflight',return_value=(package,'hash')),patch.object(evidence,'receipt_package_proved'),patch.object(evidence,'require_physical_trace_admission'):
                try:scope['self_save_load_pre_action']()
                except NameError as error:self.fail('actual pre-action cannot execute: '+str(error))
            self.assertEqual((scope['baseline']/'game_state/control/post-load-B.json').read_bytes(),b'B baseline')
            self.assertEqual(value['SelfSaveLoadProof']['PreActionReadback'],snapshot)
            self.assertEqual(len(scope['journal']),1);ready=scope['journal'][0]
            self.assertEqual(ready['Phase'],'StandaloneLoadReady');self.assertEqual(ready['LoadObservation'],value['LoadObservation'])
            self.assertEqual(ready['BInputBindingId'],'binding-B');self.assertEqual(ready['Archive'],value['SelfSaveLoadProof']['Archive'])

    def completed(self):
        expected,document,installed=load_data.fixture();v=load_data.cleanup(expected,document,installed)
        v.update(AcceptedGameTurns=1,GenuineActionsSent=1,AcceptedTurnNumber=42,ConsoleRoute={'Route':'self-save-load-turn'})
        a=v['OriginalRunningStatus']['status'];b=v['LoadRunningStatus']['status'];a['inputBindingId']='binding-A';b.update(inputBindingId='binding-B',terminalOwnerRetained=True,terminalUncertain=False,ready=True,backend='LinuxSupervisor',terminalGuarantee='owned',shellPid=102)
        v['ShutdownReceipt']['status']['inputBindingId']='binding-B'
        v['TurnRunningRecord']=copy.deepcopy(installed);v['TurnRunningStatus']=copy.deepcopy(v['LoadRunningStatus'])
        v['LoadObservation']=copy.deepcopy(document)
        v['PostLoadPreActionRecord']=copy.deepcopy(installed);v['PostLoadPreActionStatus']=copy.deepcopy(v['LoadRunningStatus'])
        v['OriginalTurnDelivery']={'disposition':'submission-observed','inputBindingId':'binding-B','operationKind':'turn','operationRevision':'live'}
        v['RelayResponseEvidence']=[{'QueueId':'request-1-'+'a'*32,'Kind':'turn','Dispatch':copy.deepcopy(v['OriginalTurnDelivery'])}]
        pin={'Path':expected['SourceKey'],'Name':'save.zip','SHA256':'a'*64,'Mode':0o600}
        snapshot={'Turn':41,'Files':{'stories/mortal_life_2.jsonl':b'{"turn":41}\n'.hex()},'StoryPath':'stories/mortal_life_2.jsonl','ConfigSHA256':evidence.sha(b'{}'),'ConfigRawHex':b'{}'.hex(),'Queue':{}}
        v['SelfSaveLoadProof']={'Before':snapshot,'AfterSave':copy.deepcopy(snapshot),'AfterLoad':copy.deepcopy(snapshot),'PreActionReadback':copy.deepcopy(snapshot),'Archive':pin,'FinalArchiveReadback':copy.deepcopy(pin),'ArchiveVerified':True,'CleanOutcomeBeforeFirstPlayerClear':True,'RestoreBytesExact':True}
        v['LoadArchiveSHA256']='a'*64;v['FinalConfigSHA256']=snapshot['ConfigSHA256']
        v['SelfSaveLoadProof']['ArchiveContentProof']={'ManifestVerified':True,'ConfigBytesExact':True,'RuntimeAbsent':True,'ConfigSHA256':snapshot['ConfigSHA256'],'ManifestSHA256':evidence.sha(b'{}'),'ManifestRawHex':b'{}'.hex(),'ManifestLength':2,'ManifestEntries':1}
        request={'sessionId':'self-session','requestId':'request42','turnNumber':42,'playerAction':'look'};request_raw=json.dumps(request).encode()
        accepted={'turn':42,'realm':'Mortal World','player':'look','narrative':'independent reply'};before=bytes.fromhex(snapshot['Files'][snapshot['StoryPath']]);after=before+json.dumps(accepted).encode()+b'\n'
        v.update(ActualRequest=request,ActualRequestRawHex=request_raw.hex(),ActualRequestSHA256=evidence.sha(request_raw),SeedSessionId='self-session',AcceptedStory=accepted,AcceptedStoryAfterRawHex=after.hex(),StoryBeforeSHA256=evidence.sha(before),StoryAfterSHA256=evidence.sha(after),ClientTerminalClosure={'ChildRestoredConfiguredBaseline':True,'OwnerRestoredOriginal':True,'TerminalNormalClose':True,'EOF':True,'ExitCode':0},ReceiptHelper={'Completed':True,'StdoutEOF':True,'StderrEOF':True,'ExitCode':0})
        v['SelfSaveQueue']=[{'QueueId':'request-1-'+'a'*32,'Kind':'turn','TurnRequestSHA256':evidence.sha(request_raw)}];v['FinalSelfSaveQueue']=copy.deepcopy(v['SelfSaveQueue'])
        v['FinalWorldPins']['stories/mortal_life_2.jsonl']=evidence.sha(after)
        v['TypedReceiptEvidence']={'kind':'ReceiptAppended','valid':True,'publicationAuthority':False,'sessionId':'self-session','requestId':'request42','turn':42,'snapshotToken':'A'*64,'priorCount':0,'currentCount':1,'historyBefore':{'present':False,'sha256':None},'historyAfter':{'present':True,'sha256':'b'*64},'declarationFingerprint':'sha256:'+'c'*64,'receiptFingerprint':'sha256:'+'d'*64}
        return expected,document,v

    def test_completion_preserves_A_and_accepts_only_B(self):
        prove=getattr(evidence,'relay_self_save_load_turn_cleanup_proved',None)
        self.assertTrue(callable(prove),'missing separate self-Save/Load/turn cleanup join')
        expected,doc,value=self.completed();before=copy.deepcopy(value)
        self.assertTrue(prove(value,load_data.wire(doc),0,expected));self.assertEqual(before,value)
        self.assertFalse(evidence.relay_phase_cleanup_proved(value))
        self.assertFalse(evidence.relay_load_phase_cleanup_proved(value,load_data.wire(doc),0,expected))

    def test_completion_refuses_A_binding_or_stop_and_changed_readbacks(self):
        prove=getattr(evidence,'relay_self_save_load_turn_cleanup_proved',None)
        self.assertTrue(callable(prove),'missing B-only turn join')
        for defect in ('delivery-A','reply-A','record-A','pre-action-A','stop-A','changed-story','changed-settings','changed-zip','wrong-source','accepted-zero','daemon-replaced'):
            expected,doc,v=self.completed()
            if defect=='delivery-A':v['OriginalTurnDelivery']['inputBindingId']='binding-A'
            elif defect=='reply-A':v['RelayResponseEvidence'][0]['Dispatch']['inputBindingId']='binding-A'
            elif defect=='record-A':v['TurnRunningRecord']=v['OriginalRunningRecord']
            elif defect=='pre-action-A':v['PostLoadPreActionStatus']['status']['inputBindingId']='binding-A'
            elif defect=='stop-A':v['StoppedRecord']['Identity']=v['OriginalRunningRecord']['Identity']
            elif defect=='changed-story':v['SelfSaveLoadProof']['PreActionReadback']['Files']['stories/mortal_life_2.jsonl']='00'
            elif defect=='changed-settings':v['FinalConfigSHA256']='d'*64
            elif defect=='changed-zip':v['SelfSaveLoadProof']['FinalArchiveReadback']['SHA256']='d'*64
            elif defect=='wrong-source':v['SelfSaveLoadProof']['Archive']['Path']='/foreign/save.zip'
            elif defect=='accepted-zero':v['AcceptedGameTurns']=0
            elif defect=='daemon-replaced':v['PostLoadDaemonContinuity']['PostLoadProcessIdentity']['StartTicks']+=1
            with self.subTest(defect=defect):self.assertFalse(prove(v,load_data.wire(doc),0,expected))

    def test_actual_driver_uses_B_binding_for_all_turn_evidence(self):
        tree=ast.parse((ROOT/'live-mortal-phase.py').read_text())
        turn=next(n for n in tree.body if isinstance(n,ast.Try))
        references=[ast.unparse(n) for n in ast.walk(turn) if isinstance(n,ast.Subscript) and ast.unparse(n).endswith("['inputBindingId']") and "RunningStatus" in ast.unparse(n) ]
        self.assertTrue(references)
        self.assertTrue(all("TurnRunningStatus" in x for x in references),references)

    def test_actual_driver_self_route_reaches_existing_single_turn_else(self):
        tree=ast.parse((ROOT/'live-mortal-phase.py').read_text())
        functions={n.name:n for n in tree.body if isinstance(n,ast.FunctionDef)}
        self.assertIn('self_save_load_before_turn',functions)
        calls=[n for n in ast.walk(tree) if isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=='self_save_load_before_turn']
        self.assertEqual(len(calls),1)
        # The sole genuine turn submission remains the existing path, not a copy.
        sends=[n for n in ast.walk(tree) if isinstance(n,ast.Call) and isinstance(n.func,ast.Attribute) and n.func.attr=='send' and any(isinstance(a,ast.Constant) and a.value=='one genuine game action' for a in n.args)]
        self.assertEqual(len(sends),1)

if __name__=='__main__':unittest.main()
