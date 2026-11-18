"""Causal cleanup predicate regression using shipped driver AST, never game output.

These DATA controls do not establish rejected-turn authority. They prove the
independent safety requirement that historical wait text cannot authorize ESC
against a newer player prompt, even when rejection evidence is incomplete.
"""
import ast
import re
import unittest
from pathlib import Path

SOURCE = Path(__file__).with_name('live-mortal-phase.py')
TREE = ast.parse(SOURCE.read_text())
NS = {'re': re}
exec(compile(ast.Module(body=[n for n in TREE.body if isinstance(n, ast.FunctionDef)],
                        type_ignores=[]), str(SOURCE), 'exec'), NS)


def actual_cancel_predicate(text):
    candidates = []
    for node in ast.walk(TREE):
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
                'Ваш ход\n > ')
        self.assertFalse(actual_cancel_predicate(text))

    def test_new_prompt_without_rejection_authority_still_forbids_old_wait_escape(self):
        self.assertFalse(actual_cancel_predicate('Мастер игры размышляет...\nВаш ход\n > '))

    def test_actual_current_wait_without_new_prompt_retains_cancel_branch(self):
        self.assertTrue(actual_cancel_predicate('Мастер игры размышляет...\n'))


if __name__ == '__main__':
    unittest.main()
