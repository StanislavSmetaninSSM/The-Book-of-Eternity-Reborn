"""Opt-in cold-host admission DATA; no game or original-world writes."""
import copy,hashlib,json,pathlib,tempfile,unittest
import relay_phase_evidence as actual

class ColdHostAdmission(unittest.TestCase):
    def test_real_closed_receipts_and_exact_retained_data_are_required(self):
        admit=getattr(actual,'require_cold_loaded_host',None)
        self.assertTrue(callable(admit),'missing explicit cold-loaded host admission')
        # Explicit opt-in immutable accepted evidence, read-only current closed root.
        fixture=pathlib.Path('/workspace/shared/relay-alternative/mortal-t034/cold-load-admission/cold-host-data.json')
        raw=fixture.read_bytes();self.assertEqual(hashlib.sha256(raw).hexdigest(),'0eb98ec444c504ce9e6b227cd9211fc445d9b259459e7516ecaa0395fbd8f321')
        descriptor=json.loads(raw);out=pathlib.Path(descriptor['PhasePath'])
        before={str(p):p.read_bytes() for p in pathlib.Path(descriptor['BasePath']).rglob('*') if p.is_file()}
        result=admit(descriptor,out,previous=None,route='ordinary-save',current_max=43)
        self.assertEqual(result['ExpectedTurn'],44);self.assertEqual(result['SessionId'],descriptor['SessionId'])
        self.assertEqual(result['TemporaryRoot'],str(out.parent/'rt'))
        self.assertEqual(result['ConfigBytes'],before[str(pathlib.Path(descriptor['BasePath'])/'game_session/config.json')])
        for previous,route,turn in [({},'ordinary-save',43),(None,'birth',43),(None,'ordinary-save',44)]:
            with self.assertRaises(ValueError):admit(descriptor,out,previous=previous,route=route,current_max=turn)
        for field,value in [('Kind','seeded-live-mortal'),('ExpectedCurrentTurn',44),('ExpectedTurn',45),('SessionId','foreign'),('TemporaryRoot',str(out/'tmp')),('ArchiveSHA256','0'*64)]:
            bad=copy.deepcopy(descriptor);bad[field]=value
            with self.assertRaises(ValueError):admit(bad,out,previous=None,route='ordinary-save',current_max=43)
        for field in ('ColdOutcome','RebindOutcome','OriginalConfig'):
            bad=copy.deepcopy(descriptor);bad[field]['SHA256']='0'*64
            with self.assertRaises(ValueError):admit(bad,out,previous=None,route='ordinary-save',current_max=43)
        bad=copy.deepcopy(descriptor);bad['PlayRows']['Files']['game_session/config.json']['sha256']='0'*64
        with self.assertRaises(ValueError):admit(bad,out,previous=None,route='ordinary-save',current_max=43)
        self.assertEqual(before,{str(p):p.read_bytes() for p in pathlib.Path(descriptor['BasePath']).rglob('*') if p.is_file()})
        self.assertEqual(list((out/'queue').iterdir()),[])

    def test_actual_pending_namespace_refuses_without_any_owner_adoption(self):
        check=getattr(actual,'require_cold_loaded_namespace',None)
        self.assertTrue(callable(check),'missing actual closed namespace check')
        with tempfile.TemporaryDirectory() as name:
            base=pathlib.Path(name)/'play';(base/'game_session').mkdir(parents=True)
            check(base)
            for relative in ('.boe_runtime/gm-runs/main.json','game_session/game_state/control/gm_bridge_status.json','game_session/game_state/control/pending_turn_snapshot.json','game_session/game_state/control/pending_turn_snapshot.authority.json','game_session/game_state/control/validation_repair_request.json','game_session/input/turn_request.json','game_session/ready/turn_complete.json'):
                path=base/relative;path.parent.mkdir(parents=True,exist_ok=True);path.write_text('{}')
                with self.assertRaises(ValueError):check(base)
                path.unlink()
            path=base/'game_session/game_state/control/pending_turn_snapshot';path.mkdir()
            with self.assertRaises(ValueError):check(base)

    def test_cold_host_requires_no_new_owner_adoption_or_old_temp_alias(self):
        validate=getattr(actual,'require_cold_loaded_owner',None)
        self.assertTrue(callable(validate),'missing fresh cold-host owner witness')
        expected={'RootKey':'/tmp/new-play','GenerationId':'new-generation','ForbiddenRunIds':['old-save-owner','failed-p03-owner']}
        identity={'RootKey':'/tmp/new-play','GenerationId':'new-generation','RunId':'new-original','Epoch':1,'Backend':'LinuxSupervisor'}
        validate(identity,expected)
        for changes in ({'RootKey':'/tmp/old-play'},{'GenerationId':'old-generation'},{'RunId':'old-save-owner'},{'RunId':'failed-p03-owner'},{'Epoch':True},{'Epoch':0}):
            with self.assertRaises(ValueError):validate(dict(identity,**changes),expected)

if __name__=='__main__':unittest.main()
