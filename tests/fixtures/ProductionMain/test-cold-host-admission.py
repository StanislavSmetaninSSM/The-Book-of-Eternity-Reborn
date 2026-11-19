"""Opt-in cold-host admission DATA; no game or original-world writes."""
import ast,copy,hashlib,json,pathlib,tempfile,unittest,types
from unittest.mock import patch
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
        expected={'RootKey':'/tmp/new-play','GenerationId':'a'*32,'ForbiddenRunIds':['b'*32,'c'*32]}
        identity={'RootKey':'/tmp/new-play','GenerationId':'a'*32,'RunId':'d'*32,'Epoch':1,'Backend':'LinuxSupervisor','HostInstanceId':'f'*32,'BootId':'8a076780-7e68-4951-a54b-998e6554afca'}
        validate(identity,expected)
        for changes in ({'RootKey':'/tmp/old-play'},{'GenerationId':'e'*32},{'RunId':'b'*32},{'RunId':'c'*32},{'Epoch':True},{'Epoch':0}):
            with self.assertRaises(ValueError):validate(dict(identity,**changes),expected)

class ColdHostWiring(unittest.TestCase):
    def function(self,name):
        source=pathlib.Path(__file__).with_name('live-mortal-phase.py')
        tree=ast.parse(source.read_text());nodes=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name==name]
        self.assertEqual(len(nodes),1,'missing actual cold driver boundary: '+name)
        scope={'Path':pathlib.Path,'phase_evidence':actual,'sha':lambda raw:hashlib.sha256(raw).hexdigest()}
        exec(compile(ast.Module(body=nodes,type_ignores=[]),str(source),'exec'),scope)
        return scope[name]

    def test_actual_cold_setup_reaches_only_existing_package_gate_after_authentication(self):
        function=self.function('prepare_cold_loaded_phase')
        fixture=pathlib.Path('/workspace/shared/relay-alternative/mortal-t034/cold-load-admission/cold-host-data.json')
        raw=fixture.read_bytes();self.assertEqual(hashlib.sha256(raw).hexdigest(),'0eb98ec444c504ce9e6b227cd9211fc445d9b259459e7516ecaa0395fbd8f321')
        descriptor=json.loads(raw);out=pathlib.Path(descriptor['PhasePath']);calls=[];sentinel=RuntimeError('inert package sentinel')
        def package(phase,ship,driver):
            calls.append((phase,ship,driver));self.assertEqual(phase,out);self.assertEqual(ship,pathlib.Path(descriptor['RuntimeRoot']));raise sentinel
        with patch.object(actual,'relay_package_preflight',package):
            with self.assertRaises(RuntimeError) as caught:function(descriptor,out,pathlib.Path('/unused/helper.dll'),'driver',None,'ordinary-save',43)
            self.assertIs(caught.exception,sentinel);self.assertEqual(len(calls),1)
            for previous,route,turn in [({},'ordinary-save',43),(None,'birth',43),(None,'ordinary-save',44)]:
                with self.assertRaises(ValueError):function(descriptor,out,pathlib.Path('/unused/helper.dll'),'driver',previous,route,turn)
            self.assertEqual(len(calls),1)

    def test_actual_cold_operational_branch_preserves_config_and_precreated_queue(self):
        function=self.function('cold_loaded_operational_context')
        with tempfile.TemporaryDirectory() as name:
            root=pathlib.Path(name);out=root/'recovered-live';(out/'queue').mkdir(parents=True,mode=0o700)
            session=root/'play/game_session';session.mkdir(parents=True);config=session/'config.json';raw=b'{"preserved":true}';config.write_bytes(raw)
            admitted={'ConfigBytes':raw,'Command':'exact-reviewed-command','Pipe':'og-abcdef123456','TemporaryRoot':str(root/'rt')}
            value=function(admitted,out,session)
            self.assertEqual(value,('og-abcdef123456','exact-reviewed-command',out/'queue',root/'rt'))
            self.assertEqual(config.read_bytes(),raw);self.assertFalse((root/'rt').exists());self.assertEqual(list((out/'queue').iterdir()),[])
            config.write_bytes(b'{}')
            with self.assertRaises(ValueError):function(admitted,out,session)
            config.write_bytes(raw);(out/'queue/occupied').write_bytes(b'x')
            with self.assertRaises(ValueError):function(admitted,out,session)

if __name__=='__main__':unittest.main()
