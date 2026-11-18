"""Causal cleanup predicate regression using shipped driver AST, never game output.

These DATA controls do not establish rejected-turn authority. They prove the
independent safety requirement that historical wait text cannot authorize ESC
against a newer player prompt, even when rejection evidence is incomplete.
"""
import ast
import relay_phase_evidence as evidence
import re
import unittest
import tempfile
import json
from pathlib import Path

SOURCE = Path(__file__).with_name('live-mortal-phase.py')
TREE = ast.parse(SOURCE.read_text())
NS = {'re': re}
for node in TREE.body:
    if isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == 'PLAYER_PROMPT_MARKER' for t in node.targets):
        exec(compile(ast.Module(body=[node], type_ignores=[]), str(SOURCE), 'exec'), NS)
exec(compile(ast.Module(body=[n for n in TREE.body if isinstance(n, ast.FunctionDef)],
                        type_ignores=[]), str(SOURCE), 'exec'), NS)


def actual_cancel_predicate(text, tree=TREE):
    candidates = []
    for node in ast.walk(tree):
        if not isinstance(node, ast.If):
            continue
        # Only this immediate branch sends the existing cleanup ESC. Do not
        # accidentally select an enclosing if or execute the entire driver.
        for statement in node.body:
            if not isinstance(statement, ast.Expr) or not isinstance(statement.value, ast.Call):
                continue
            call = statement.value
            if any(isinstance(arg, ast.Constant) and arg.value ==
                   'observed current wait: cancel once' for arg in call.args):
                candidates.append(node.test)
    if len(candidates) != 1:
        raise AssertionError('Expected exactly one actual cleanup ESC branch')
    scope = dict(NS, text=text, pause_acknowledged=False, cancel_sent=False)
    return eval(compile(ast.Expression(candidates[0]), str(SOURCE), 'eval'), scope)


class RejectedCleanupTests(unittest.TestCase):
    def test_completed_rollback_with_new_prompt_does_not_send_escape(self):
        text = ('Мастер игры размышляет...\n'
                '↩ Изменения мира не были приняты; состояние до хода восстановлено.\n'
                'Ваш ход\n >  ')
        self.assertFalse(actual_cancel_predicate(text))

    def test_new_prompt_without_rejection_authority_still_forbids_old_wait_escape(self):
        self.assertFalse(actual_cancel_predicate('Мастер игры размышляет...\nВаш ход\n >  '))

    def test_actual_current_wait_without_new_prompt_retains_cancel_branch(self):
        self.assertTrue(actual_cancel_predicate('Мастер игры размышляет...\n'))


class GeneralCleanupTests(unittest.TestCase):
    def test_same_general_driver_new_prompt_forbids_historical_wait_escape(self):
        tree = ast.parse(SOURCE.with_name('live-game-one-turn.py').read_text())
        self.assertFalse(actual_cancel_predicate('Мастер игры размышляет...\nВаш ход\n >  ', tree))


class ProducerWiringTests(unittest.TestCase):
    def test_actual_producer_refuses_validation_repair_ready_file(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);session=root/'session';baseline=root/'baseline'
            request={'sessionId':'session','requestId':'request','turnNumber':43}
            for folder,keys in [(baseline,[]),(session,['session|request|43'])]:
                path=folder/'game_state/control/gm_observed_terminal_requests.json'
                path.parent.mkdir(parents=True);path.write_text(json.dumps({'schemaVersion':1,'keys':keys}))
            ledger=session/evidence.TERMINAL_LEDGER_PATH;after=ledger.read_bytes()
            ledger.write_text(json.dumps({'schemaVersion':1,'keys':[]}))
            witness=evidence.capture_terminal_ledger_witness(session);ledger.write_bytes(after)
            request_bytes=json.dumps(request).encode();binding=evidence.bind_terminal_ledger_witness(witness,request_bytes)
            story=session/'story';story.write_bytes(b'original')
            ready=session/'game_state/control/validation_repair_ready.json';ready.write_text('{}')
            scope=dict(NS,json=json,strict_settings=evidence.strict_settings,phase_evidence=evidence,
                relay_response_evidence=lambda *args:[{'Execution':{'Executed':True,'ExitCode':0,'ChildExited':True,'IoDrained':True}}])
            producer=next(n for n in TREE.body if isinstance(n,ast.FunctionDef) and n.name=='observed_rejected_cleanup')
            exec(compile(ast.Module(body=[producer],type_ignores=[]),str(SOURCE),'exec'),scope)
            text='↩ Изменения мира не были приняты; состояние до хода восстановлено.\nВаш ход\n >  '
            self.assertFalse(scope['observed_rejected_cleanup'](request_bytes,None,None,session,baseline,text,b'original',story,witness,binding))

    def test_raw_queue_refusal_never_reaches_cleanup_join(self):
        reached=[]
        def refused(*args):
            reached.append('raw-validator')
            raise ValueError('raw original queue mismatch')
        scope=dict(NS, relay_response_evidence=refused,
                   phase_evidence=evidence)
        producer=next(n for n in TREE.body if isinstance(n,ast.FunctionDef) and n.name=='observed_rejected_cleanup')
        exec(compile(ast.Module(body=[producer],type_ignores=[]),str(SOURCE),'exec'),scope)
        with tempfile.TemporaryDirectory() as directory:
            session=Path(directory);request=b'{"sessionId":"s","requestId":"r","turnNumber":43}'
            witness=evidence.capture_terminal_ledger_witness(session)
            binding=evidence.bind_terminal_ledger_witness(witness,request)
            with self.assertRaisesRegex(ValueError,'raw original queue mismatch'):
                scope['observed_rejected_cleanup'](request,None,None,session,None,'',b'',None,witness,binding)
        self.assertEqual(reached,['raw-validator'])


class RejectedJoinTests(unittest.TestCase):
    def observation(self):
        request = {'sessionId': 'session', 'requestId': 'request', 'turnNumber': 43}
        return dict(request=request, keys_before=['session|previous|42'],
                    keys_after=['session|previous|42', 'session|request|43'],
                    action_text=('Мастер игры размышляет...\n'
                        '↩ Изменения мира не были приняты; состояние до хода восстановлено.\n'
                        '✦ Ваш ход ✦\n >  '),
                    story_before=b'original story', story_after=b'original story',
                    pending_paths=[], relay_observation={'RequestIdentity': dict(request),
                        'ValidatedOriginalQueue': True, 'Executions': [
                            {'Executed': True, 'ExitCode': 0, 'ChildExited': True, 'IoDrained': True}]})

    def test_complete_join_permits_cleanup_only(self):
        self.assertTrue(evidence.rejected_restored_for_cleanup(**self.observation()))

    def test_conservative_negative_joins(self):
        changes = [
            ('stale_key', lambda v: v['keys_before'].append('session|request|43')),
            ('wrong_key', lambda v: v.update(keys_after=['session|wrong|43'])),
            ('quoted_marker', lambda v: v.update(action_text=v['action_text'].replace('↩ Изменения', 'quoted "↩ Изменения'))),
            ('future_marker', lambda v: v.update(action_text='Состояние до хода будет восстановлено.\nВаш ход\n >  ')),
            ('incomplete_marker', lambda v: v.update(action_text='состояние до хода восстановлено.\nВаш ход\n >  ')),
            ('prompt_before_marker', lambda v: v.update(action_text='Ваш ход\n >  \n↩ Изменения мира не были приняты; состояние до хода восстановлено.')),
            ('newer_error_pause', lambda v: v.update(action_text=v['action_text']+'\nНажмите любую клавишу для продолжения...')),
            ('newer_wait', lambda v: v.update(action_text=v['action_text']+'\nМастер игры размышляет...')),
            ('changed_story', lambda v: v.update(story_after=b'new story')),
            ('pending', lambda v: v.update(pending_paths=['input/turn_request.json'])),
            ('repair', lambda v: v.update(pending_paths=['output/validation_repair_request.json'])),
            ('ready', lambda v: v.update(pending_paths=['ready/turn_complete.json'])),
            ('incomplete_execution', lambda v: v['relay_observation']['Executions'][0].update(IoDrained=False)),
            ('wrong_execution_identity', lambda v: v['relay_observation']['RequestIdentity'].update(requestId='foreign')),
            ('no_raw_queue_join', lambda v: v['relay_observation'].update(ValidatedOriginalQueue=False)),
        ]
        for name, mutate in changes:
            with self.subTest(name=name):
                value=self.observation();mutate(value)
                self.assertFalse(evidence.rejected_restored_for_cleanup(**value))


class LedgerWitnessTests(unittest.TestCase):
    def request(self):
        return b'{"sessionId":"session","requestId":"request","turnNumber":43}'

    def test_real_absent_file_capture_and_exact_binding(self):
        with tempfile.TemporaryDirectory() as directory:
            session=Path(directory)/'session';session.mkdir()
            witness=evidence.capture_terminal_ledger_witness(session)
            self.assertIsInstance(witness,bytes)
            value=json.loads(witness)
            self.assertIs(value['Present'],False)
            self.assertEqual(value['SessionPath'],str(session.resolve()))
            self.assertEqual(value['RelativePath'],'game_state/control/gm_observed_terminal_requests.json')
            binding=evidence.bind_terminal_ledger_witness(witness,self.request())
            self.assertEqual(evidence.terminal_ledger_witness_keys(witness,binding,session,self.request()),[])

    def test_real_present_capture_preserves_exact_bytes(self):
        with tempfile.TemporaryDirectory() as directory:
            session=Path(directory);path=session/'game_state/control/gm_observed_terminal_requests.json'
            path.parent.mkdir(parents=True)
            raw=b'{ "schemaVersion":1, "keys":["session|old|42"] }\n';path.write_bytes(raw)
            witness=evidence.capture_terminal_ledger_witness(session)
            self.assertIsInstance(witness,bytes)
            self.assertEqual(bytes.fromhex(json.loads(witness)['RawHex']),raw)
            binding=evidence.bind_terminal_ledger_witness(witness,self.request())
            path.write_bytes(b'changed after immutable capture')
            self.assertEqual(evidence.terminal_ledger_witness_keys(witness,binding,session,self.request()),['session|old|42'])

    def test_capture_permission_error_is_not_absence(self):
        # OS permission error injected at the one read boundary: chmod is not
        # reliable when the test account owns elevated filesystem privileges.
        from unittest.mock import patch
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(Path,'read_bytes',side_effect=PermissionError('injected read denial')):
                with self.assertRaises(PermissionError):
                    evidence.capture_terminal_ledger_witness(Path(directory))

    def test_capture_real_non_file_io_error_is_not_absence(self):
        with tempfile.TemporaryDirectory() as directory:
            session=Path(directory);path=session/'game_state/control/gm_observed_terminal_requests.json'
            path.mkdir(parents=True)
            with self.assertRaises(OSError):evidence.capture_terminal_ledger_witness(session)

    def test_missing_truncated_and_foreign_witness_refuse(self):
        session=Path('/synthetic/session');request=self.request()
        raw=json.dumps({'SessionPath':str(session),'RelativePath':'game_state/control/gm_observed_terminal_requests.json',
                        'Present':False,'RawHex':None,'SHA256':None}).encode()
        binding=json.dumps({'WitnessSHA256':evidence.sha(raw),'RequestSHA256':evidence.sha(request),
                            'RequestIdentity':json.loads(request)}).encode()
        mutations=[(None,binding,session,request),(b'{',binding,session,request),
                   (raw,None,session,request),(raw,b'{',session,request),
                   (raw,binding,Path('/foreign/session'),request),
                   (raw,binding,session,request.replace(b'request',b'foreign')),
                   (raw.replace(b'gm_observed_terminal_requests.json',b'foreign.json'),binding,session,request),
                   (raw,binding.replace(evidence.sha(raw).encode(),b'0'*64),session,request)]
        for args in mutations:
            with self.subTest(args=args):self.assertIsNone(evidence.terminal_ledger_witness_keys(*args))

    def test_late_witness_replacement_refused_by_actual_producer_before_queue(self):
        with tempfile.TemporaryDirectory() as directory:
            session=Path(directory);request=self.request()
            original=evidence.capture_terminal_ledger_witness(session)
            binding=evidence.bind_terminal_ledger_witness(original,request)
            ledger=session/evidence.TERMINAL_LEDGER_PATH;ledger.parent.mkdir(parents=True)
            ledger.write_text(json.dumps({'schemaVersion':1,'keys':['session|request|43']}))
            replacement=evidence.capture_terminal_ledger_witness(session)
            scope=dict(NS,phase_evidence=evidence)
            calls=[]
            def no_queue(*args):
                calls.append(args);raise AssertionError('Invalid replacement reached raw queue')
            scope['relay_response_evidence']=no_queue
            producer=next(n for n in TREE.body if isinstance(n,ast.FunctionDef) and n.name=='observed_rejected_cleanup')
            exec(compile(ast.Module(body=[producer],type_ignores=[]),str(SOURCE),'exec'),scope)
            self.assertFalse(scope['observed_rejected_cleanup'](request,None,None,session,None,'',b'',None,replacement,binding))
            self.assertEqual(calls,[])
            self.assertEqual(evidence.terminal_ledger_witness_keys(original,binding,session,request),[])

    def test_actual_driver_capture_precedes_send_binding_follows_request(self):
        producer_calls=[n for n in ast.walk(TREE) if isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=='observed_rejected_cleanup']
        self.assertEqual(len(producer_calls),2)
        for call in producer_calls:
            self.assertEqual([ast.unparse(a) for a in call.args[-2:]],['ledger_witness','ledger_binding'])
        calls={}
        for n in ast.walk(TREE):
            if isinstance(n,ast.Call):
                name=n.func.attr if isinstance(n.func,ast.Attribute) else n.func.id if isinstance(n.func,ast.Name) else ''
                if name in ('capture_terminal_ledger_witness','bind_terminal_ledger_witness'):
                    calls.setdefault(name,[]).append(n.lineno)
                if name=='send' and any(isinstance(a,ast.Constant) and a.value=='one genuine game action' for a in n.args):calls['send']=[n.lineno]
        self.assertEqual(len(calls.get('capture_terminal_ledger_witness',[])),1)
        self.assertEqual(len(calls.get('bind_terminal_ledger_witness',[])),1)
        self.assertLess(calls['capture_terminal_ledger_witness'][0],calls['send'][0])
        request_reads=[n.lineno for n in ast.walk(TREE) if isinstance(n,ast.Assign) and
            any(isinstance(t,ast.Name) and t.id=='request_bytes' for t in n.targets) and
            isinstance(n.value,ast.Call) and isinstance(n.value.func,ast.Attribute) and n.value.func.attr=='read_bytes']
        self.assertEqual(len(request_reads),1)
        self.assertGreater(calls['bind_terminal_ledger_witness'][0],request_reads[0])


if __name__ == '__main__':
    unittest.main()
