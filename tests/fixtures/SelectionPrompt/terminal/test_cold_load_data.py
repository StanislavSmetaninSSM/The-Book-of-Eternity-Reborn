"""Cold Load navigation DATA only: no game, PTY, provider, or old-world mutation."""
import hashlib, json, pathlib, tempfile, types, unittest, zipfile, sys
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent.parent))
import cold_load_navigation as actual

CHOICES=('🌟 Новая игра','📂 Загрузить профиль','Тренировка QTE','Вылазка Дарена','⚙️ Опции','ℹ️ Об игре','🚪 Выход')
def grid(selected):
    rows=['Выберите следующий шаг','']+[(('➤ ' if n==selected else '◆ ')+str(n+1)+'. '+s) for n,s in enumerate(CHOICES)]+['↑/↓ • W/S • 1-7 • Enter']
    raw='\r\n'.join(rows).encode()
    return raw,types.SimpleNamespace(pending=False,cut=len(raw),lines=[r for r in rows if r]+['']*16)

class ColdLoadData(unittest.TestCase):
    def test_actual_cold_grid_exact_target_and_fresh_cells(self):
        f=getattr(actual,'cold_main_ready',None);self.assertTrue(callable(f),'missing cold-grid predicate')
        for selected in (0,1):
            raw,v=grid(selected);self.assertTrue(f(v,raw,0,selected))
            self.assertFalse(f(v,raw,0,1-selected))
            for suffix in (b'partial',b'\x1b[0m',raw[:-9]):
                newer=raw+suffix;v.cut=len(newer);self.assertFalse(f(v,newer,len(raw),selected))
            raw,v=grid(selected);v.pending=True;self.assertFalse(f(v,raw,0,selected))
            v.pending=False;v.lines=['wrong']*25;self.assertFalse(f(v,raw,0,selected))
        raw,v=grid(1)
        for broken in (raw.replace(b'2. ',b'3. ',1),raw.replace('📂 Загрузить профиль'.encode(),b''),raw.replace(b'1-7',b'1-8')):
            v.cut=len(broken);self.assertFalse(f(v,broken,0,1))

    def test_actual_clean_load_message_rejects_followup_and_diagnostics(self):
        f=getattr(actual,'clean_cold_load_message',None);self.assertTrue(callable(f),'missing exact cold-load outcome predicate')
        good='\r\nСохранение загружено.\r\n'
        self.assertTrue(f(good));self.assertTrue(f('\r\n\x1b[32mСохранение загружено.\x1b[0m\r\n'))
        for bad in (good+'Требуется служебная проверка завершения операции.\r\n',good+'Продолжение остановлено.\r\n',good+'boe-main-operation x\n',good+good,'Сохранение не загружено.\n',good[:-1],good+'partial'):
            self.assertFalse(f(bad),repr(bad))

    def test_actual_whole_load_transition_close_success_fresh_prompt(self):
        from fullroute_inputs import routes
        menu=routes.local_load_menu('Двор',43,10,'19.11.2026 22:13')
        height=len(menu['header'].split('\n'))+len(menu['choices'])
        close=b'\r'+b'\x1b[2K\x1b[1A'*height+b'\x1b[2K\x1b[?25h'
        clear=b'\x1b[2J\x1b[3J\x1b[1;1H'
        success='\r\nСохранение загружено.\r\n'.encode()
        player='── ✦ Ваш ход ✦ ──\r\n >  \x1b[s'.encode()
        raw=close+success+clear+player
        def state(b):return types.SimpleNamespace(pending=False,cut=len(b),lines=['── ✦ Ваш ход ✦ ──',' >  ']+['']*23,cursor={'x':4,'y':1})
        self.assertTrue(actual.cold_load_completed(raw,0,menu,state(raw)))
        for bad in (raw.replace(close,close[1:],1),b'old'+raw,raw.replace(success,success+'Продолжение остановлено.\r\n'.encode()),raw.replace(clear,b'',1),raw[:-1]):
            self.assertFalse(actual.cold_load_completed(bad,0,menu,state(bad)))
        v=state(raw);v.cut-=1;self.assertFalse(actual.cold_load_completed(raw,0,menu,v))
        v=state(raw);v.pending=True;self.assertFalse(actual.cold_load_completed(raw,0,menu,v))
        self.assertFalse(actual.cold_load_completed(raw,len(raw),menu,state(raw)))

    def test_actual_archive_identity_crc_and_unique_members(self):
        f=getattr(actual,'read_cold_archive',None);self.assertTrue(callable(f),'missing exact selected-archive admission')
        with tempfile.TemporaryDirectory() as t:
            p=pathlib.Path(t)/'s_20261119_171314.zip'
            def write(duplicate=False):
                with zipfile.ZipFile(p,'w') as z:
                    z.writestr('save_metadata.json',json.dumps({'saveName':'s','turnNumber':43}))
                    z.writestr('config.json',json.dumps({'MusicEnabled':False,'SoundEnabled':False,'GenerateSceneImages':False,'ShowImagesInConsole':False,'ImageProvider':'off'}))
                    z.writestr('game_state/test.json','{}')
                    if duplicate:z.writestr('GAME_STATE/test.json','{}')
            write();h=hashlib.sha256(p.read_bytes()).hexdigest();before=p.read_bytes()
            result=f(p,p.name,h);self.assertEqual(result['ArchiveSHA256'],h);self.assertEqual(result['Metadata']['turnNumber'],43)
            for name,digest in (('other.zip',h),(p.name,'0'*64)):
                with self.assertRaises(ValueError):f(p,name,digest)
            self.assertEqual(p.read_bytes(),before)
            write(True)
            with self.assertRaises(ValueError):f(p,p.name,hashlib.sha256(p.read_bytes()).hexdigest())

class ColdFrozenRepair(unittest.TestCase):
    def test_exact_frozen_cold_raw_and_cells_complete_without_leading_newline(self):
        from fullroute_inputs import routes
        e=pathlib.Path('/workspace/shared/relay-alternative/mortal-t034/cold-load-admission/outcome.json')
        self.assertEqual(hashlib.sha256(e.read_bytes()).hexdigest(),'a5fc3a40a293d37ccba53667e9a6c0b1aa05e4d23b8f0eccf24a635e51e27d60')
        pins=json.loads(e.read_bytes())['Artifacts'];root=pathlib.Path('/workspace/shared/mm29-cold-load-20261119/probe')
        def sealed(name):
            p=root/name;raw=p.read_bytes();self.assertEqual(hashlib.sha256(raw).hexdigest(),pins[str(p)]);return raw
        raw=sealed('pty.raw');records=[json.loads(x) for x in sealed('terminal.protocol.jsonl').splitlines()]
        snaps=[x['reply'] for x in records if x.get('reply',{}).get('op')=='SNAPSHOT'];last=snaps[-1]
        v=types.SimpleNamespace(pending=False,cut=len(raw),lines=last['lines'],cursor=last['cursor'])
        menu=routes.local_load_menu('Хозяйственный двор у нижней лестницы',43,799,'19.11.2026 22:13')
        self.assertTrue(actual.cold_load_completed(raw,6989,menu,v),'real direct success must complete')
        direct='Сохранение загружено.\r\n'.encode();self.assertEqual(raw.count(direct),1)
        for prefix in (b'\r\n',):
            b=raw.replace(direct,prefix+direct);v.cut=len(b);self.assertTrue(actual.cold_load_completed(b,6989,menu,v))
        for prefix in (b'\r\n\r\n',b' ',b'diagnostic\r\n'):
            b=raw.replace(direct,prefix+direct);v.cut=len(b);self.assertFalse(actual.cold_load_completed(b,6989,menu,v))
        b=raw.replace(direct,direct+'Требуется служебная проверка завершения операции.\r\n'.encode());v.cut=len(b);self.assertFalse(actual.cold_load_completed(b,6989,menu,v))

    def test_actual_wrapper_requires_guardian_driver_and_normal_probe_closure(self):
        import ast
        p=pathlib.Path(__file__).resolve().parents[4]/'specs/1515-mortal-transport-storage/manual-mm21/run-cold-load.py'
        nodes=[n for n in ast.parse(p.read_text()).body if isinstance(n,ast.FunctionDef) and n.name=='cold_run_succeeded']
        self.assertEqual(len(nodes),1,'missing full cold outcome conjunction')
        scope={};exec(compile(ast.Module(body=nodes,type_ignores=[]),str(p),'exec'),scope);f=scope['cold_run_succeeded']
        guardian={'echild':True,'driverExitCode':0,'emergencySignals':0,'failures':0,'deadline':False}
        probe={'Success':True,'Failure':None,'CleanupFailures':[],'EOF':True,'ChildRestoredConfiguredBaseline':True,'OwnerRestoredOriginal':True,'TerminalNormalClose':True,'TerminalExitCode':0,'ChildExitCode':0}
        self.assertTrue(f(0,guardian,probe))
        for key,val in [('driverExitCode',1),('echild',False),('deadline',True),('emergencySignals',1),('failures',1)]:
            self.assertFalse(f(0,dict(guardian,**{key:val}),probe))
        for key,val in [('Success',False),('Failure','timeout'),('EOF',False),('ChildRestoredConfiguredBaseline',False),('OwnerRestoredOriginal',False),('TerminalNormalClose',False),('TerminalExitCode',-15),('ChildExitCode',143),('CleanupFailures',['unknown'])]:
            self.assertFalse(f(0,guardian,dict(probe,**{key:val})))
        self.assertFalse(f(1,guardian,probe))
        old=pathlib.Path('/workspace/shared/mm29-cold-load-20261119')
        evidence=pathlib.Path('/workspace/shared/relay-alternative/mortal-t034/cold-load-admission/outcome.json').read_bytes()
        self.assertEqual(hashlib.sha256(evidence).hexdigest(),'a5fc3a40a293d37ccba53667e9a6c0b1aa05e4d23b8f0eccf24a635e51e27d60')
        pins=json.loads(evidence)['Artifacts']
        for path in (old/'guardian.json',old/'probe/result.json'):self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(),pins[str(path)])
        self.assertFalse(f(0,json.loads((old/'guardian.json').read_bytes()),json.loads((old/'probe/result.json').read_bytes())))

if __name__=='__main__':unittest.main()
