"""Cold Load navigation DATA only: no game, PTY, provider, or old-world mutation."""
import hashlib, json, pathlib, tempfile, types, unittest, zipfile
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

    def test_actual_archive_identity_crc_and_unique_members(self):
        f=getattr(actual,'read_cold_archive',None);self.assertTrue(callable(f),'missing exact selected-archive admission')
        with tempfile.TemporaryDirectory() as t:
            p=pathlib.Path(t)/'s_20261119_171314.zip'
            def write(duplicate=False):
                with zipfile.ZipFile(p,'w') as z:
                    z.writestr('metadata.json',json.dumps({'saveName':'s','turnNumber':43}))
                    z.writestr('config.json',json.dumps({'MusicEnabled':False,'SoundEnabled':False,'GenerateSceneImages':False,'ImageProvider':'off'}))
                    z.writestr('game_state/test.json','{}')
                    if duplicate:z.writestr('GAME_STATE/test.json','{}')
            write();h=hashlib.sha256(p.read_bytes()).hexdigest();before=p.read_bytes()
            result=f(p,p.name,h);self.assertEqual(result['ArchiveSHA256'],h);self.assertEqual(result['Metadata']['turnNumber'],43)
            for name,digest in (('other.zip',h),(p.name,'0'*64)):
                with self.assertRaises(ValueError):f(p,name,digest)
            self.assertEqual(p.read_bytes(),before)
            write(True)
            with self.assertRaises(ValueError):f(p,p.name,hashlib.sha256(p.read_bytes()).hexdigest())

if __name__=='__main__':unittest.main()
