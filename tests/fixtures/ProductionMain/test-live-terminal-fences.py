"""Actual driver AST DATA only: never imports or starts the live driver."""
import ast,pathlib,sys,unittest
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'SelectionPrompt'))
source=pathlib.Path(__file__).with_name('live-mortal-phase.py').read_text()
tree=ast.parse(source)
node=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='clean_save_player_interval')
namespace={};exec(compile(ast.Module(body=[node],type_ignores=[]),'<actual-driver-function>','exec'),namespace)
clean=namespace['clean_save_player_interval']
class Fences(unittest.TestCase):
    def test_exact_success_before_actual_clear(self):
        self.assertTrue(clean('✅ Игра сохранена\r\n'.encode()+b'\x1b[2J\x1b[Hplayer'))
    def test_warning_before_clear_even_when_final_cells_clean(self):
        for warning in ('Требуется служебная проверка хранилища сохранений.','unknown outcome','Требуется\x1b[1m служебная проверка'):
            self.assertFalse(clean(('✅ Игра сохранена\r\n'+warning+'\r\n').encode()+b'\x1b[2Jplayer'))
    def test_missing_success_or_clear_refuses(self):
        for raw in (b'\x1b[2Jplayer','✅ Игра сохранена\r\n'.encode(),b'unknown\x1b[2J'):
            self.assertFalse(clean(raw))
    def test_no_unconditional_client_control_c(self):
        terminal=next(n for n in tree.body if isinstance(n,ast.ClassDef) and n.name=='Terminal')
        send=next(n for n in terminal.body if isinstance(n,ast.FunctionDef) and n.name=='send')
        self.assertEqual(send.args.kwonlyargs[0].arg,'cleanup')
        self.assertIs(send.args.kw_defaults[0].value,False)
        for n in ast.walk(tree):
            if isinstance(n,ast.Call) and isinstance(n.func,ast.Attribute) and isinstance(n.func.value,ast.Name) and n.func.value.id=='client' and n.func.attr=='send':
                self.assertGreaterEqual(len(n.args),3)
                if isinstance(n.args[0],ast.Constant):self.assertNotEqual(n.args[0].value,b'\x03')
if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(Fences);assert suite.countTestCases()==4
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() and result.testsRun==4 and not result.skipped else 1)
