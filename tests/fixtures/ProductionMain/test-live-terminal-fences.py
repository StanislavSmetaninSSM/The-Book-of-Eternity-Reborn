"""Actual driver AST DATA only: never imports or starts the live driver."""
import ast,pathlib,sys,unittest,tempfile,json,hashlib,os,stat
import relay_phase_evidence as evidence
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
    def package(self):
        temporary=tempfile.TemporaryDirectory();self.addCleanup(temporary.cleanup)
        root=pathlib.Path(temporary.name);source=root/'terminal-fixture';deps=root/'terminal-deps'
        for name in evidence.TERMINAL_SOURCE_FILES:
            p=source/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text('authored DATA')
        expected={}
        for index in range(17):
            name='node_modules/@xterm/authored/'+str(index);p=deps/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(str(index));expected[name]=evidence.sha(p.read_bytes())
        provenance={'@xterm/headless':{'version':'6.0.0','files':expected},'@xterm/addon-unicode11':{'version':'0.9.0','files':{}}}
        (source/'SelectionPrompt/terminal/provenance.json').write_text(json.dumps(provenance))
        def pins(folder):return {str(p.relative_to(folder)):evidence.sha(p.read_bytes()) for p in folder.rglob('*') if p.is_file()}
        def modes(folder):return {str(p.relative_to(folder)):stat.S_IMODE(p.stat().st_mode) for p in folder.rglob('*') if p.is_file()}
        manifest={'TerminalFixturePins':pins(source),'TerminalFixtureModes':modes(source),'TerminalDependencyPins':pins(deps),'TerminalDependencyModes':modes(deps),'TerminalNodePath':'/opt/codex/runtimes/codex-primary-runtime/dependencies/node/bin/node','TerminalNodeSHA256':evidence.TERMINAL_NODE_SHA256,'ConsoleRouteModuleSHA256':pins(source)['ProductionMain/mortal_console_routes.py']}
        return root,manifest
    def test_package_exact_join_and_changed_source_refusal(self):
        root,m=self.package();evidence.terminal_package_preflight(root,m)
        (root/'terminal-fixture/SelectionPrompt/terminal/client_ui.py').write_text('changed')
        with self.assertRaises(ValueError):evidence.terminal_package_preflight(root,m)
    def test_package_extra_link_or_mode_refused(self):
        for change in ('extra','link','mode'):
            root,m=self.package();p=root/'terminal-deps/node_modules/@xterm/authored/0'
            if change=='extra':(p.parent/'extra').write_text('unexpected')
            elif change=='link':(p.parent/'linked-dir').symlink_to(p.parent,target_is_directory=True)
            else:p.chmod(stat.S_IMODE(p.stat().st_mode)^stat.S_IXUSR)
            with self.assertRaises(ValueError):evidence.terminal_package_preflight(root,m)
if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(Fences);assert suite.countTestCases()==6
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() and result.testsRun==6 and not result.skipped else 1)
