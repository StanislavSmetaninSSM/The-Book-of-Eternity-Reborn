"""Actual driver AST DATA only: never imports or starts the live driver."""
import ast,pathlib,sys,unittest,tempfile,json,hashlib,os,stat,types,copy
from unittest.mock import patch
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
    def test_actual_constructor_rolls_back_each_owned_acquisition(self):
        terminal=next(n for n in tree.body if isinstance(n,ast.ClassDef) and n.name=='Terminal')
        init=next(n for n in terminal.body if isinstance(n,ast.FunctionDef) and n.name=='__init__')
        for stage in ('ioctl','baseline','preflight','sidecar','popen','evidence','journal'):
            events=[];primary=ValueError('injected '+stage);peers=[]
            def step(name,value=None):
                events.append(name)
                if stage==name:raise primary
                return value
            attrs=[0,0,0,3,0,0,[]]
            child=types.SimpleNamespace(pid=123,stdout=None,poll=lambda:None,
                terminate=lambda:events.append('terminate'),wait=lambda **kw:events.append('wait'),kill=lambda:events.append('kill'))
            def abort():
                events.append('abort')
                if stage=='journal':raise RuntimeError('injected cleanup failure')
            console=types.SimpleNamespace(abort=abort)
            fakeos=types.SimpleNamespace(set_blocking=lambda *a:None,close=lambda fd:events.append(('close',fd)))
            class Path:
                def __truediv__(self,other):return self
                def mkdir(self,**kw):pass
                def __str__(self):return 'inert-path'
            ns=dict(pty=types.SimpleNamespace(openpty=lambda:(10,11)),fcntl=types.SimpleNamespace(ioctl=lambda *a:step('ioctl')),
                termios=types.SimpleNamespace(TIOCSWINSZ=1,TCSANOW=2,ECHO=1,ECHONL=2,
                    tcgetattr=lambda fd:step('baseline',attrs if 'configured' not in events else [0,0,0,0,0,0,[]]),
                    tcsetattr=lambda *a:events.append('configured')),
                struct=types.SimpleNamespace(pack=lambda *a:b''),copy=copy,os=fakeos,
                terminal_fixture_admission=lambda:step('preflight',(Path(),Path(),Path())),
                sys=types.SimpleNamespace(path=[],dont_write_bytecode=False),out=Path(),start=0,work_seconds=660,client_cleanup_seconds=680,
                console_routes=types.SimpleNamespace(client_environment=lambda env:env),env={},ship='inert',
                subprocess=types.SimpleNamespace(Popen=lambda *a,**kw:step('popen',child),TimeoutExpired=TimeoutError),
                evidence=types.SimpleNamespace(raw=lambda *a:step('evidence')),observed_processes=[],
                process_observer=types.SimpleNamespace(register=lambda pid:pid),peers=peers,
                journal=types.SimpleNamespace(append=lambda *a:step('journal')),elapsed=lambda:0)
            module=types.ModuleType('client_session');module.ClientSession=lambda *a:step('sidecar',console)
            node=ast.ClassDef(name='Terminal',bases=[],keywords=[],body=[init],decorator_list=[])
            with patch.dict(sys.modules,{'client_session':module}):
                exec(compile(ast.fix_missing_locations(ast.Module(body=[node],type_ignores=[])),'<actual-init>','exec'),ns)
                with self.assertRaises(ValueError) as caught:ns['Terminal']('client',['never executed'])
            self.assertIs(caught.exception,primary)
            if stage=='journal':self.assertIn('injected cleanup failure',' '.join(primary.__notes__))
            self.assertIn(('close',10),events);self.assertIn(('close',11),events);self.assertEqual(peers,[])
            if stage in ('popen','evidence','journal'):self.assertIn('abort',events)
            if stage in ('evidence','journal'):
                self.assertIn('terminate',events);self.assertIn('wait',events)

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
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(Fences);assert suite.countTestCases()==7
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() and result.testsRun==7 and not result.skipped else 1)
