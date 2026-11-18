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
            story=session/'story';story.write_bytes(b'original')
            ready=session/'game_state/control/validation_repair_ready.json';ready.write_text('{}')
            scope=dict(NS,json=json,strict_settings=evidence.strict_settings,phase_evidence=evidence,
                relay_response_evidence=lambda *args:[{'Execution':{'Executed':True,'ExitCode':0,'ChildExited':True,'IoDrained':True}}])
            producer=next(n for n in TREE.body if isinstance(n,ast.FunctionDef) and n.name=='observed_rejected_cleanup')
            exec(compile(ast.Module(body=[producer],type_ignores=[]),str(SOURCE),'exec'),scope)
            text='↩ Изменения мира не были приняты; состояние до хода восстановлено.\nВаш ход\n >  '
            self.assertFalse(scope['observed_rejected_cleanup'](json.dumps(request).encode(),None,None,session,baseline,text,b'original',story))

    def test_raw_queue_refusal_never_reaches_cleanup_join(self):
        reached=[]
        def refused(*args):
            reached.append('raw-validator')
            raise ValueError('raw original queue mismatch')
        scope=dict(NS, relay_response_evidence=refused,
                   phase_evidence=type('Evidence', (), {'rejected_restored_for_cleanup':lambda *args: reached.append('join')})())
        producer=next(n for n in TREE.body if isinstance(n,ast.FunctionDef) and n.name=='observed_rejected_cleanup')
        exec(compile(ast.Module(body=[producer],type_ignores=[]),str(SOURCE),'exec'),scope)
        with self.assertRaisesRegex(ValueError,'raw original queue mismatch'):
            scope['observed_rejected_cleanup'](b'{}',None,None,None,None,'',b'',None)
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


if __name__ == '__main__':
    unittest.main()
